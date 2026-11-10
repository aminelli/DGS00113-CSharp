using System.Text.Json;
using Azure;
using Azure.AI.Vision.ImageAnalysis;

namespace PlateOcr;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static async Task<int> Main(string[] args)
    {
        AppOptions options;
        try
        {
            options = AppOptions.Parse(args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine("Use --help to see available options.");
            return 2;
        }

        if (options.ShowHelp)
        {
            PrintUsage();
            return 0;
        }

        string inputDirectory;
        try
        {
            inputDirectory = Path.GetFullPath(options.InputDirectory);
            if (!Directory.Exists(inputDirectory))
            {
                Console.Error.WriteLine($"Input directory does not exist: {inputDirectory}");
                return 2;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or
                                           UnauthorizedAccessException or NotSupportedException)
        {
            Console.Error.WriteLine($"Invalid input directory: {exception.GetType().Name}");
            return 2;
        }

        string[] imagePaths;
        try
        {
            imagePaths = ImageFileEnumerator.Enumerate(inputDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Unable to enumerate input directory: {exception.GetType().Name}");
            return 2;
        }

        string outputPath;
        try
        {
            outputPath = Path.GetFullPath(options.OutputPath);
            if (Directory.Exists(outputPath) || Path.EndsInDirectorySeparator(options.OutputPath))
            {
                outputPath = Path.Combine(outputPath, $"results_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or
                                           UnauthorizedAccessException or NotSupportedException)
        {
            Console.Error.WriteLine($"Invalid output path: {exception.GetType().Name}");
            return 2;
        }

        if (imagePaths.Any(path => string.Equals(
                Path.GetFullPath(path),
                outputPath,
                StringComparison.OrdinalIgnoreCase)))
        {
            Console.Error.WriteLine("The JSON output path cannot overwrite an input image.");
            return 2;
        }

        if (imagePaths.Length == 0)
        {
            Console.Error.WriteLine("No JPEG or PNG images were found in the input directory.");
        }

        ImageAnalysisClient? client = null;
        if (imagePaths.Length > 0)
        {
            string? endpoint = Environment.GetEnvironmentVariable("VISION_ENDPOINT");
            string? key = Environment.GetEnvironmentVariable("VISION_KEY");

            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
            {
                Console.Error.WriteLine(
                    "Set both VISION_ENDPOINT and VISION_KEY environment variables before processing images.");
                return 2;
            }

            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? endpointUri) ||
                endpointUri.Scheme != Uri.UriSchemeHttps)
            {
                Console.Error.WriteLine("VISION_ENDPOINT must be an absolute HTTPS URL.");
                return 2;
            }

            client = new ImageAnalysisClient(endpointUri, new AzureKeyCredential(key));
        }

        var imageResults = new List<ImageResult>(imagePaths.Length);
        foreach (string imagePath in imagePaths)
        {
            ImageResult result = await ProcessImageAsync(client!, imagePath);
            imageResults.Add(result);

            if (result.Status == ImageStatus.Error)
            {
                Console.Error.WriteLine($"{result.FileName}: {result.Error}");
            }
            else
            {
                Console.Error.WriteLine(
                    $"{result.FileName}: {result.Status}; candidates={result.PlateCandidates.Count}");
            }
        }

        var report = new BatchReport(
            DateTimeOffset.UtcNow,
            new BatchSummary(
                imageResults.Count,
                imageResults.Count(result => result.Status == ImageStatus.Detected),
                imageResults.Count(result => result.Status == ImageStatus.NotDetected),
                imageResults.Count(result => result.Status == ImageStatus.Error)),
            imageResults);

        try
        {
            string? outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            string json = JsonSerializer.Serialize(report, JsonOptions);
            await File.WriteAllTextAsync(outputPath, json);
            Console.Error.WriteLine($"JSON report written to: {outputPath}");
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or
                                           UnauthorizedAccessException or NotSupportedException)
        {
            Console.Error.WriteLine($"Unable to write JSON report: {exception.GetType().Name}");
            return 2;
        }

        Console.Error.WriteLine(
            $"Summary: total={report.Summary.TotalImages}, detected={report.Summary.Detected}, " +
            $"notDetected={report.Summary.NotDetected}, errors={report.Summary.Errors}");

        return report.Summary.Errors == 0 ? 0 : 1;
    }

    private static async Task<ImageResult> ProcessImageAsync(
        ImageAnalysisClient client,
        string imagePath)
    {
        string fileName = Path.GetFileName(imagePath);
        try
        {
            byte[] imageBytes = await File.ReadAllBytesAsync(imagePath);
            
            ImageAnalysisResult analysis = await client.AnalyzeAsync(
                BinaryData.FromBytes(imageBytes),
                VisualFeatures.Read);

            IEnumerable<string> lines = analysis.Read.Blocks
                .SelectMany(block => block.Lines)
                .Select(line => line.Text);
            IReadOnlyList<string> candidates = PlateCandidateDetector.FindCandidates(lines);

            return new ImageResult(
                fileName,
                candidates.Count > 0 ? ImageStatus.Detected : ImageStatus.NotDetected,
                candidates,
                null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RequestFailedException exception)
        {
            string code = string.IsNullOrWhiteSpace(exception.ErrorCode)
                ? $"http-{exception.Status}"
                : exception.ErrorCode;
            return new ImageResult(
                fileName,
                ImageStatus.Error,
                Array.Empty<string>(),
                $"Azure Vision request failed ({code}, HTTP {exception.Status}). Check the resource, key, quota, and network.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ImageResult(
                fileName,
                ImageStatus.Error,
                Array.Empty<string>(),
                $"Unable to read the image ({exception.GetType().Name}).");
        }
        catch (Exception exception)
        {
            return new ImageResult(
                fileName,
                ImageStatus.Error,
                Array.Empty<string>(),
                $"Image processing failed ({exception.GetType().Name}).");
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            PlateOcr - scans JPEG/PNG files in one directory and writes a JSON report.

            Usage:
              PlateOcr [--input <directory>] [--output <json-file-or-directory>]

            Options:
              --input   Input directory (default: ./images)
              --output  JSON file or existing directory (default: ./JSON/results_yyyyMMdd_HHmmss.json).
                        End a new directory path with a separator to create it.
              --help    Show this help

            Required environment variables when images are present:
              VISION_ENDPOINT
              VISION_KEY
            """);
    }
}

internal sealed record AppOptions(string InputDirectory, string OutputPath, bool ShowHelp)
{
    public static AppOptions Parse(string[] args)
    {
        string inputDirectory = Path.Combine(Environment.CurrentDirectory, "images");
        string outputPath = Path.Combine(
            Environment.CurrentDirectory,
            "JSON",
            $"results_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        bool showHelp = false;

        for (int index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--help" or "-h":
                    showHelp = true;
                    break;
                case "--input":
                    inputDirectory = ReadValue(args, ref index, "--input");
                    break;
                case "--output":
                    outputPath = ReadValue(args, ref index, "--output");
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[index]}");
            }
        }

        return new AppOptions(inputDirectory, outputPath, showHelp);
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"A value is required after {option}.");
        }

        return args[++index];
    }
}

internal sealed record BatchReport(
    DateTimeOffset GeneratedAtUtc,
    BatchSummary Summary,
    IReadOnlyList<ImageResult> Results);

internal sealed record BatchSummary(int TotalImages, int Detected, int NotDetected, int Errors);

internal sealed record ImageResult(
    string FileName,
    string Status,
    IReadOnlyList<string> PlateCandidates,
    string? Error);

internal static class ImageStatus
{
    public const string Detected = "detected";
    public const string NotDetected = "notDetected";
    public const string Error = "error";
}
