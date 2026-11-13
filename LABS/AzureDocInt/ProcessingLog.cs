using System.Text.Json;

namespace DocMng;

internal sealed class ProcessingLog(string logsFolder)
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task WriteAsync(string fileName, string fileHash, string status, string? message)
    {
        var entry = new ProcessingLogEntry(DateTimeOffset.UtcNow, fileName, fileHash, status, message);
        var json = JsonSerializer.Serialize(entry);
        Console.WriteLine(json);

        await _writeLock.WaitAsync();
        try
        {
            var logPath = Path.Combine(logsFolder, $"processing-{DateTime.UtcNow:yyyyMMdd}.jsonl");
            await File.AppendAllTextAsync(logPath, json + Environment.NewLine);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
