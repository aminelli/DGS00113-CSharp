using Azure;
using Azure.AI.Vision.ImageAnalysis;

namespace PlateOcr.Tests;

public sealed class RequiresVisionFactAttribute : FactAttribute
{
    public RequiresVisionFactAttribute()
    {
        if (PhotoFolder.Find() is null)
        {
            Skip = "The FOTO folder was not found in a parent directory of the test output.";
        }
        else if (PhotoFolder.GetImages().Length == 0)
        {
            Skip = "The FOTO folder contains no JPEG or PNG images.";
        }
        else if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VISION_ENDPOINT")) ||
                 string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VISION_KEY")))
        {
            Skip = "Set VISION_ENDPOINT and VISION_KEY to run the Azure OCR integration test.";
        }
    }
}

internal static class PhotoFolder
{
    public static string? Find()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {

            // string candidate = Path.Combine(directory.FullName, "FOTO");
            string candidate = "D:\\Temp\\cSharp\\LABS\\ComputerVision\\L001\\FOTO";
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static string[] GetImages()
    {
        string? folder = Find();
        return folder is null ? [] : ImageFileEnumerator.Enumerate(folder);
    }
}

public sealed class PhotoFolderIntegrationTests
{
    [Fact]
    public void FotoFolderIsLocatedFromTestOutput()
    {
        Assert.NotNull(PhotoFolder.Find());
    }

    [RequiresVisionFact]
    public async Task AnalyzesEveryImageInFotoFolderWithAzureOcr()
    {
        var client = new ImageAnalysisClient(
            new Uri(Environment.GetEnvironmentVariable("VISION_ENDPOINT")),
            new AzureKeyCredential(Environment.GetEnvironmentVariable("VISION_KEY")));
       
        foreach (string imagePath in PhotoFolder.GetImages())
        {
            ImageAnalysisResult analysis = await client.AnalyzeAsync(
                BinaryData.FromBytes(await File.ReadAllBytesAsync(imagePath)),
                VisualFeatures.Read);

            IReadOnlyList<string> candidates = PlateCandidateDetector.FindCandidates(
                analysis.Read.Blocks.SelectMany(block => block.Lines).Select(line => line.Text));

            Assert.NotNull(candidates);
        }
    }
}
