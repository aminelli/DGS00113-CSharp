using Microsoft.Data.SqlClient;

namespace DocMng;

internal sealed record AppConfiguration(
    string SqlConnectionString,
    string? DocumentIntelligenceEndpoint,
    string? DocumentIntelligenceApiKey,
    string ModelId,
    string DocsRoot)
{
    public static AppConfiguration Load()
    {
        var sqlConnectionString = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(sqlConnectionString))
        {
            var password = Environment.GetEnvironmentVariable("SQLSERVER_PASSWORD") ?? "Corso2025";
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "Set SQLSERVER_CONNECTION_STRING or SQLSERVER_PASSWORD before starting the application.");
            }

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = BuildDataSource(),
                InitialCatalog = Environment.GetEnvironmentVariable("SQLSERVER_DATABASE") ?? "InvoiceDb",
                UserID = Environment.GetEnvironmentVariable("SQLSERVER_USER") ?? "sa",
                Password = password,
                Encrypt = false,
                TrustServerCertificate = ReadBoolean("SQLSERVER_TRUST_SERVER_CERTIFICATE", defaultValue: false)
            };
            sqlConnectionString = builder.ConnectionString;
        }

        return new AppConfiguration(
            sqlConnectionString,
            Environment.GetEnvironmentVariable("DOCUMENT_INTELLIGENCE_ENDPOINT"),
            Environment.GetEnvironmentVariable("DOCUMENT_INTELLIGENCE_API_KEY"),
            Environment.GetEnvironmentVariable("DOCUMENT_INTELLIGENCE_MODEL_ID"),
            Environment.GetEnvironmentVariable("DOCS_ROOT") ?? "D:\\Temp\\cSharp\\AI\\DocMng\\Docs");
    }

    private static string BuildDataSource()
    {
        var host = Environment.GetEnvironmentVariable("SQLSERVER_HOST") ?? "127.0.0.1";
        var portValue = Environment.GetEnvironmentVariable("SQLSERVER_PORT") ?? "1434";
        if (string.IsNullOrWhiteSpace(portValue))
        {
            return host;
        }

        if (!int.TryParse(portValue, out var port) || port is < 1 or > 65535)
        {
            throw new InvalidOperationException("SQLSERVER_PORT must be an integer between 1 and 65535.");
        }

        return $"{host},{port}";
    }

    private static bool ReadBoolean(string name, bool defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : bool.TryParse(value, out var parsed)
                ? parsed
                : throw new InvalidOperationException($"{name} must be 'true' or 'false'.");
    }
}
