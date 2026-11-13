using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace DocMng;

internal sealed class SqlInvoiceRepository
{
    private readonly string _connectionString;
    private readonly string _databaseName;

    public SqlInvoiceRepository(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        _databaseName = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(_databaseName) ||
            !Regex.IsMatch(_databaseName, "^[A-Za-z0-9_]{1,128}$", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException(
                "SQLSERVER_CONNECTION_STRING must specify a database name containing only letters, digits, or underscores.");
        }

        _connectionString = builder.ConnectionString;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var builder = new SqlConnectionStringBuilder(_connectionString);
        builder.InitialCatalog = "master";

        await using (var masterConnection = new SqlConnection(builder.ConnectionString))
        {
            await OpenWithRetryAsync(masterConnection, cancellationToken);
            await using var createDatabase = masterConnection.CreateCommand();
            createDatabase.CommandText = """
                DECLARE @createDatabaseSql NVARCHAR(300);
                IF DB_ID(@databaseName) IS NULL
                BEGIN
                    SET @createDatabaseSql = N'CREATE DATABASE ' + QUOTENAME(@databaseName);
                    EXEC sp_executesql @createDatabaseSql;
                END;
                """;
            createDatabase.Parameters.Add("@databaseName", SqlDbType.NVarChar, 128).Value = _databaseName;
            await createDatabase.ExecuteNonQueryAsync(cancellationToken);
        }

        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Database", "Schema.sql");
        if (!File.Exists(schemaPath))
        {
            throw new FileNotFoundException("The database schema script was not found.", schemaPath);
        }

        var schema = await File.ReadAllTextAsync(schemaPath, cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await OpenWithRetryAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = schema;
        command.CommandTimeout = 60;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<long?> FindInvoiceByHashAsync(string fileHash, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await OpenWithRetryAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT InvoiceId FROM dbo.Invoice WHERE SourceFileHash = @fileHash;";
        command.Parameters.Add("@fileHash", SqlDbType.Char, 64).Value = fileHash;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToInt64(result);
    }

    public async Task RecordCallAttemptAsync(
        string fileName,
        string fileHash,
        string modelId,
        DateTimeOffset startedAtUtc,
        TimeSpan duration,
        bool succeeded,
        int? httpStatusCode,
        string? errorMessage,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await OpenWithRetryAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.DocumentIntelligenceCallLog
                (SourceFileName, SourceFileHash, ModelId, StartedAtUtc, DurationMilliseconds,
                 Succeeded, HttpStatusCode, ErrorMessage)
            VALUES
                (@fileName, @fileHash, @modelId, @startedAtUtc, @durationMilliseconds,
                 @succeeded, @httpStatusCode, @errorMessage);
            """;
        AddString(command, "@fileName", 260, fileName);
        command.Parameters.Add("@fileHash", SqlDbType.Char, 64).Value = fileHash;
        AddString(command, "@modelId", 100, modelId);
        command.Parameters.Add("@startedAtUtc", SqlDbType.DateTime2).Value = startedAtUtc.UtcDateTime;
        command.Parameters.Add("@durationMilliseconds", SqlDbType.BigInt).Value = (long)duration.TotalMilliseconds;
        command.Parameters.Add("@succeeded", SqlDbType.Bit).Value = succeeded;
        command.Parameters.Add("@httpStatusCode", SqlDbType.Int).Value = (object?)httpStatusCode ?? DBNull.Value;
        AddNullableString(command, "@errorMessage", 2000, Clip(errorMessage));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveSuccessfulImportAsync(
        string fileName,
        string fileHash,
        ExtractedInvoice invoice,
        string rawJson,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await OpenWithRetryAsync(connection, cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using var invoiceCommand = connection.CreateCommand();
            invoiceCommand.Transaction = transaction;
            invoiceCommand.CommandText = """
                INSERT INTO dbo.Invoice
                    (SourceFileName, SourceFileHash, VendorName, CustomerName, InvoiceNumber,
                     InvoiceDate, DueDate, PurchaseOrder, Subtotal, TotalTax, InvoiceTotal, CurrencyCode)
                OUTPUT INSERTED.InvoiceId
                VALUES
                    (@fileName, @fileHash, @vendorName, @customerName, @invoiceNumber,
                     @invoiceDate, @dueDate, @purchaseOrder, @subtotal, @totalTax, @invoiceTotal, @currencyCode);
                """;
            AddString(invoiceCommand, "@fileName", 260, fileName);
            invoiceCommand.Parameters.Add("@fileHash", SqlDbType.Char, 64).Value = fileHash;
            AddNullableString(invoiceCommand, "@vendorName", 500, invoice.VendorName);
            AddNullableString(invoiceCommand, "@customerName", 500, invoice.CustomerName);
            AddNullableString(invoiceCommand, "@invoiceNumber", 200, invoice.InvoiceNumber);
            AddNullableDate(invoiceCommand, "@invoiceDate", invoice.InvoiceDate);
            AddNullableDate(invoiceCommand, "@dueDate", invoice.DueDate);
            AddNullableString(invoiceCommand, "@purchaseOrder", 200, invoice.PurchaseOrder);
            AddDecimal(invoiceCommand, "@subtotal", invoice.Subtotal, 18, 2);
            AddDecimal(invoiceCommand, "@totalTax", invoice.TotalTax, 18, 2);
            AddDecimal(invoiceCommand, "@invoiceTotal", invoice.InvoiceTotal, 18, 2);
            AddNullableString(invoiceCommand, "@currencyCode", 10, invoice.CurrencyCode);
            var invoiceId = Convert.ToInt64(await invoiceCommand.ExecuteScalarAsync(cancellationToken));

            for (var index = 0; index < invoice.Lines.Count; index++)
            {
                var line = invoice.Lines[index];
                await using var lineCommand = connection.CreateCommand();
                lineCommand.Transaction = transaction;
                lineCommand.CommandText = """
                    INSERT INTO dbo.InvoiceLine
                        (InvoiceId, LineNumber, Description, Quantity, Unit, UnitPrice, Amount, ProductCode, Tax)
                    VALUES
                        (@invoiceId, @lineNumber, @description, @quantity, @unit, @unitPrice, @amount, @productCode, @tax);
                    """;
                lineCommand.Parameters.Add("@invoiceId", SqlDbType.BigInt).Value = invoiceId;
                lineCommand.Parameters.Add("@lineNumber", SqlDbType.Int).Value = index + 1;
                AddNullableString(lineCommand, "@description", 1000, line.Description);
                AddDecimal(lineCommand, "@quantity", line.Quantity, 18, 4);
                AddNullableString(lineCommand, "@unit", 100, line.Unit);
                AddDecimal(lineCommand, "@unitPrice", line.UnitPrice, 18, 4);
                AddDecimal(lineCommand, "@amount", line.Amount, 18, 2);
                AddNullableString(lineCommand, "@productCode", 200, line.ProductCode);
                AddNullableString(lineCommand, "@tax", 100, line.Tax);
                await lineCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var importCommand = connection.CreateCommand();
            importCommand.Transaction = transaction;
            importCommand.CommandText = """
                INSERT INTO dbo.JsonImportLog
                    (InvoiceId, SourceFileName, SourceFileHash, RawJson, Status)
                VALUES
                    (@invoiceId, @fileName, @fileHash, @rawJson, N'Succeeded');
                """;
            importCommand.Parameters.Add("@invoiceId", SqlDbType.BigInt).Value = invoiceId;
            AddString(importCommand, "@fileName", 260, fileName);
            importCommand.Parameters.Add("@fileHash", SqlDbType.Char, 64).Value = fileHash;
            importCommand.Parameters.Add("@rawJson", SqlDbType.NVarChar, -1).Value = rawJson;
            await importCommand.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task RecordFailedImportAsync(
        string fileName,
        string fileHash,
        string? rawJson,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.JsonImportLog
                (SourceFileName, SourceFileHash, RawJson, Status, ErrorMessage)
            VALUES
                (@fileName, @fileHash, @rawJson, N'Failed', @errorMessage);
            """;
        AddString(command, "@fileName", 260, fileName);
        command.Parameters.Add("@fileHash", SqlDbType.Char, 64).Value = fileHash;
        command.Parameters.Add("@rawJson", SqlDbType.NVarChar, -1).Value = (object?)rawJson ?? DBNull.Value;
        AddString(command, "@errorMessage", 2000, Clip(errorMessage) ?? string.Empty);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddString(SqlCommand command, string name, int size, string value)
    {
        command.Parameters.Add(name, SqlDbType.NVarChar, size).Value = value;
    }

    private static void AddNullableString(SqlCommand command, string name, int size, string? value)
    {
        command.Parameters.Add(name, SqlDbType.NVarChar, size).Value = (object?)value ?? DBNull.Value;
    }

    private static void AddNullableDate(SqlCommand command, string name, DateTime? value)
    {
        command.Parameters.Add(name, SqlDbType.Date).Value = (object?)value ?? DBNull.Value;
    }

    private static void AddDecimal(SqlCommand command, string name, decimal? value, byte precision, byte scale)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = precision;
        parameter.Scale = scale;
        parameter.Value = (object?)value ?? DBNull.Value;
    }

    private static async Task OpenWithRetryAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const int maxAttempts = 30;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await connection.OpenAsync(cancellationToken);
                return;
            }
            catch (SqlException exception) when (attempt < maxAttempts && IsTransient(exception))
            {
                Console.Error.WriteLine($"SQL connection attempt {attempt} failed during startup; retrying.");
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(attempt, 5)), cancellationToken);
            }
        }
    }

    private static bool IsTransient(SqlException exception)
    {
        int[] transientErrorNumbers = [2, 20, 53, 64, 233, 18456, 11001, 10053, 10054, 10060, 4060];
        return exception.Errors.Cast<SqlError>().Any(error => transientErrorNumbers.Contains(error.Number));
    }

    private static string? Clip(string? value) => value is { Length: > 2000 } ? value[..2000] : value;
}
