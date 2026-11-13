using System.ClientModel.Primitives;
using System.Globalization;
using Azure;
using Azure.AI.DocumentIntelligence;

namespace DocMng;

internal sealed record DocumentAnalysis(
    ExtractedInvoice Invoice,
    string RawJson,
    int? HttpStatusCode,
    TimeSpan Duration);

internal sealed class DocumentIntelligenceService
{
    private readonly DocumentIntelligenceClient _client;
    private readonly string _modelId;
    public string ModelId => _modelId;

    public DocumentIntelligenceService(string endpoint, string apiKey, string modelId)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) ||
            (endpointUri.Scheme != Uri.UriSchemeHttps && endpointUri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("DOCUMENT_INTELLIGENCE_ENDPOINT must be an absolute HTTP(S) URL.");
        }

        _client = new DocumentIntelligenceClient(endpointUri, new AzureKeyCredential(apiKey));
        _modelId = modelId;
    }

    public async Task<DocumentAnalysis> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await using var stream = File.OpenRead(filePath);
        var operation = await _client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            _modelId,
            BinaryData.FromStream(stream),
            cancellationToken);
        stopwatch.Stop();

        var result = operation.Value;
        var analyzedDocument = result.Documents.FirstOrDefault()
            ?? throw new InvalidDataException("Document Intelligence returned no analyzed invoice.");
        var invoice = MapInvoice(analyzedDocument);
        var rawJson = ModelReaderWriter.Write(result, ModelReaderWriterOptions.Json).ToString();

        return new DocumentAnalysis(invoice, rawJson, operation.GetRawResponse()?.Status, stopwatch.Elapsed);
    }

    private static ExtractedInvoice MapInvoice(AnalyzedDocument document)
    {
        var fields = document.Fields;
        var subtotal = GetMoney(fields, "SubTotal");
        var totalTax = GetMoney(fields, "TotalTax");
        var invoiceTotal = GetMoney(fields, "InvoiceTotal");

        return new ExtractedInvoice(
            GetString(fields, "VendorName"),
            GetString(fields, "CustomerName"),
            GetString(fields, "InvoiceId"),
            GetDate(fields, "InvoiceDate"),
            GetDate(fields, "DueDate"),
            GetString(fields, "PurchaseOrder"),
            subtotal,
            totalTax,
            invoiceTotal,
            GetCurrencyCode(fields, "InvoiceTotal")
                ?? GetCurrencyCode(fields, "SubTotal")
                ?? GetCurrencyCode(fields, "TotalTax"),
            GetLines(fields));
    }

    private static string? GetString(DocumentFieldDictionary fields, string name)
    {
        return fields.TryGetValue(name, out var field)
            ? field.ValueString ?? field.Content
            : null;
    }

    private static DateTime? GetDate(DocumentFieldDictionary fields, string name)
    {
        return fields.TryGetValue(name, out var field)
            ? field.ValueDate?.DateTime
            : null;
    }

    private static decimal? GetMoney(DocumentFieldDictionary fields, string name)
    {
        if (!fields.TryGetValue(name, out var field))
        {
            return null;
        }

        var amount = field.ValueCurrency?.Amount ?? field.ValueDouble;
        return amount.HasValue
            ? Convert.ToDecimal(amount.Value, CultureInfo.InvariantCulture)
            : field.ValueInt64.HasValue ? field.ValueInt64.Value : null;
    }

    private static string? GetCurrencyCode(DocumentFieldDictionary fields, string name)
    {
        return fields.TryGetValue(name, out var field)
            ? field.ValueCurrency?.CurrencyCode
            : null;
    }

    private static IReadOnlyList<ExtractedInvoiceLine> GetLines(DocumentFieldDictionary fields)
    {
        if (!fields.TryGetValue("Items", out var itemsField) || itemsField.ValueList is null)
        {
            return [];
        }

        return itemsField.ValueList
            .Select(item => item.ValueDictionary)
            .Where(item => item is not null)
            .Select(item => new ExtractedInvoiceLine(
                GetString(item!, "Description"),
                GetDecimal(item!, "Quantity"),
                GetString(item!, "Unit"),
                GetDecimal(item!, "UnitPrice"),
                GetDecimal(item!, "Amount"),
                GetString(item!, "ProductCode"),
                GetString(item!, "Tax")))
            .ToArray();
    }

    private static string? GetString(IReadOnlyDictionary<string, DocumentField> fields, string name)
    {
        return fields.TryGetValue(name, out var field)
            ? field.ValueString ?? field.Content
            : null;
    }

    private static decimal? GetDecimal(IReadOnlyDictionary<string, DocumentField> fields, string name)
    {
        if (!fields.TryGetValue(name, out var field))
        {
            return null;
        }

        var amount = field.ValueCurrency?.Amount ?? field.ValueDouble;
        return amount.HasValue
            ? Convert.ToDecimal(amount.Value, CultureInfo.InvariantCulture)
            : field.ValueInt64.HasValue ? field.ValueInt64.Value : null;
    }
}
