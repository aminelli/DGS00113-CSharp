using System.Diagnostics;
using System.Text.Json;
using PlateOcr;

namespace PlateOcr.Tests;

public sealed class OutputPathTests
{
    [Theory]
    [InlineData("existing-directory")]
    [InlineData("new-directory")]
    [InlineData("file")]
    [InlineData("default")]
    public async Task WritesReportToRequestedOutput(string outputKind)
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string input = Path.Combine(root, "input");
        string outputDirectory = Path.Combine(root, "JSON");
        Directory.CreateDirectory(input);

        try
        {
            if (outputKind == "existing-directory")
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = root,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(typeof(PlateCandidateDetector).Assembly.Location);
            startInfo.ArgumentList.Add("--input");
            startInfo.ArgumentList.Add(input);
            if (outputKind != "default")
            {
                startInfo.ArgumentList.Add("--output");
                startInfo.ArgumentList.Add(outputKind switch
                {
                    "existing-directory" => outputDirectory,
                    "new-directory" => outputDirectory + Path.DirectorySeparatorChar,
                    "file" => Path.Combine(outputDirectory, "custom.json"),
                    _ => throw new InvalidOperationException()
                });
            }

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unable to start PlateOcr.");
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            Assert.True(process.ExitCode == 0, error);

            string reportPath = Assert.Single(Directory.GetFiles(outputDirectory));
            if (outputKind == "file")
            {
                Assert.Equal("custom.json", Path.GetFileName(reportPath));
            }
            else
            {
                Assert.Matches(@"^results_\d{8}_\d{6}\.json$", Path.GetFileName(reportPath));
            }

            using JsonDocument report = JsonDocument.Parse(await File.ReadAllTextAsync(reportPath));
            Assert.Equal(0, report.RootElement.GetProperty("summary").GetProperty("totalImages").GetInt32());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
