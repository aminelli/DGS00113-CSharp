using System.Globalization;
using System.Security.Cryptography;
using Azure;

namespace DocMng;

internal sealed class InvoiceProcessor(
    DocumentIntelligenceService documentIntelligence,
    SqlInvoiceRepository repository,
    ProcessingLog processingLog)
{
    public async Task ProcessFolderAsync(InvoiceFolders folders, CancellationToken cancellationToken = default)
    {
        var files = Directory.EnumerateFiles(folders.Invoices)
            .Where(file => string.Equals(Path.GetExtension(file), ".pdf", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var filePath in files)
        {
            await ProcessFileAsync(filePath, folders, cancellationToken);
        }
    }

    private async Task ProcessFileAsync(
        string filePath,
        InvoiceFolders folders,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(filePath);
        var fileHash = string.Empty;
        DocumentAnalysis? analysis = null;
        var persisted = false;

        try
        {
            fileHash = await ComputeHashAsync(filePath, cancellationToken);
            var existingInvoiceId = await repository.FindInvoiceByHashAsync(fileHash, cancellationToken);
            if (existingInvoiceId.HasValue)
            {
                try
                {
                    var archivedName = MoveWithoutOverwrite(filePath, folders.Archive, fileHash);
                    await processingLog.WriteAsync(
                        fileName,
                        fileHash,
                        "AlreadyProcessedArchived",
                        $"Existing invoice {existingInvoiceId.Value}; moved as {archivedName}.");
                }
                catch (Exception exception)
                {
                    await processingLog.WriteAsync(
                        fileName,
                        fileHash,
                        "ArchiveMoveFailed",
                        $"Existing invoice {existingInvoiceId.Value}; source remains in Invoices. {exception.Message}");
                }

                return;
            }

            var callStartedAt = DateTimeOffset.UtcNow;
            try
            {
                analysis = await documentIntelligence.AnalyzeAsync(filePath, cancellationToken);
            }
            catch (Exception exception)
            {
                var duration = DateTimeOffset.UtcNow - callStartedAt;
                await repository.RecordCallAttemptAsync(
                    fileName,
                    fileHash,
                    documentIntelligence.ModelId,
                    callStartedAt,
                    duration,
                    false,
                    (exception as RequestFailedException)?.Status,
                    exception.Message,
                    cancellationToken);
                throw;
            }

            await repository.RecordCallAttemptAsync(
                fileName,
                fileHash,
                documentIntelligence.ModelId,
                callStartedAt,
                analysis.Duration,
                true,
                analysis.HttpStatusCode,
                null,
                cancellationToken);

            DisplayInvoice(fileName, analysis.Invoice);
            await repository.SaveSuccessfulImportAsync(
                fileName,
                fileHash,
                analysis.Invoice,
                analysis.RawJson,
                cancellationToken);
            persisted = true;
        }
        catch (Exception exception)
        {
            var errorMessage = exception.Message;
            if (!persisted)
            {
                try
                {
                    await repository.RecordFailedImportAsync(
                        fileName,
                        fileHash,
                        analysis?.RawJson,
                        errorMessage,
                        cancellationToken);
                }
                catch (Exception logException)
                {
                    errorMessage = $"{errorMessage} | Could not write SQL import log: {logException.Message}";
                }
            }

            try
            {
                var errorName = MoveWithoutOverwrite(filePath, folders.Errors, fileHash);
                await processingLog.WriteAsync(fileName, fileHash, "Failed", $"{errorMessage} | Moved as {errorName}.");
            }
            catch (Exception moveException)
            {
                await processingLog.WriteAsync(
                    fileName,
                    fileHash,
                    "ErrorMoveFailed",
                    $"{errorMessage} | Could not move the source file to Errors: {moveException.Message}");
            }

            return;
        }

        try
        {
            var archivedName = MoveWithoutOverwrite(filePath, folders.Archive, fileHash);
            await processingLog.WriteAsync(fileName, fileHash, "Succeeded", $"Archived as {archivedName}.");
        }
        catch (Exception exception)
        {
            await processingLog.WriteAsync(
                fileName,
                fileHash,
                "ArchiveMoveFailed",
                $"Invoice was saved to SQL; source remains in Invoices for retry. {exception.Message}");
        }
    }

    private static async Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static string MoveWithoutOverwrite(string sourcePath, string destinationFolder, string fileHash)
    {
        var originalName = Path.GetFileName(sourcePath);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(originalName);
        var extension = Path.GetExtension(originalName);
        var destinationPath = Path.Combine(destinationFolder, originalName);

        if (File.Exists(destinationPath))
        {
            var suffix = string.IsNullOrEmpty(fileHash) ? Guid.NewGuid().ToString("N")[..8] : fileHash[..8];
            destinationPath = Path.Combine(destinationFolder, $"{nameWithoutExtension}_{suffix}{extension}");
            var counter = 1;
            while (File.Exists(destinationPath))
            {
                destinationPath = Path.Combine(destinationFolder, $"{nameWithoutExtension}_{suffix}_{counter++}{extension}");
            }
        }

        File.Move(sourcePath, destinationPath);
        return Path.GetFileName(destinationPath);
    }

    private static void DisplayInvoice(string fileName, ExtractedInvoice invoice)
    {
        Console.WriteLine($"Invoice: {fileName}");
        Console.WriteLine($"  Vendor: {invoice.VendorName ?? "(not recognized)"}");
        Console.WriteLine($"  Customer: {invoice.CustomerName ?? "(not recognized)"}");
        Console.WriteLine($"  Number: {invoice.InvoiceNumber ?? "(not recognized)"}");
        Console.WriteLine($"  Date: {invoice.InvoiceDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "(not recognized)"}");
        Console.WriteLine($"  Due date: {invoice.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "(not recognized)"}");
        Console.WriteLine($"  Purchase order: {invoice.PurchaseOrder ?? "(not recognized)"}");
        Console.WriteLine($"  Subtotal: {FormatAmount(invoice.Subtotal)} {invoice.CurrencyCode}");
        Console.WriteLine($"  Tax: {FormatAmount(invoice.TotalTax)} {invoice.CurrencyCode}");
        Console.WriteLine($"  Total: {FormatAmount(invoice.InvoiceTotal)} {invoice.CurrencyCode}");
        Console.WriteLine("  Lines:");

        if (invoice.Lines.Count == 0)
        {
            Console.WriteLine("    (no line items recognized)");
            return;
        }

        for (var index = 0; index < invoice.Lines.Count; index++)
        {
            var line = invoice.Lines[index];
            Console.WriteLine(
                $"    {index + 1}. {line.Description ?? "(no description)"} | " +
                $"Qty: {FormatAmount(line.Quantity)} {line.Unit} | " +
                $"Unit price: {FormatAmount(line.UnitPrice)} | Amount: {FormatAmount(line.Amount)}");
        }
    }

    private static string FormatAmount(decimal? amount) =>
        amount?.ToString(CultureInfo.InvariantCulture) ?? "(not recognized)";
}
