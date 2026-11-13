using DocMng;

try
{
    var configuration = AppConfiguration.Load();
    var repository = new SqlInvoiceRepository(configuration.SqlConnectionString);
    await repository.InitializeAsync();

    if (args.Contains("--init-db", StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine("Database schema initialized.");
        return;
    }

    var endpoint = configuration.DocumentIntelligenceEndpoint
        ?? throw new InvalidOperationException("Set DOCUMENT_INTELLIGENCE_ENDPOINT before processing invoices.");
    var apiKey = configuration.DocumentIntelligenceApiKey
        ?? throw new InvalidOperationException("Set DOCUMENT_INTELLIGENCE_API_KEY before processing invoices.");

    var folders = InvoiceFolders.Create(configuration.DocsRoot);
    var processor = new InvoiceProcessor(
        new DocumentIntelligenceService(endpoint, apiKey, configuration.ModelId),
        repository,
        new ProcessingLog(folders.Logs));

    await processor.ProcessFolderAsync(folders);
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Fatal error: {exception.Message}");
    Environment.ExitCode = 1;
}
