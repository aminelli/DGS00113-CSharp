using PlateOcr;
using System;
using System.IO;
using System.Linq;

namespace PlateOcr.Tests;

public sealed class PlateCandidateDetectorTests
{
    [Fact]
    public void FindsCandidateAcrossThreeOcrTokens()
    {
        IReadOnlyList<string> candidates =
            PlateCandidateDetector.FindCandidates(["Vehicle plate: AA 123 BB"]);

        Assert.Contains("AA123BB", candidates);
    }

    [Fact]
    public void IgnoresTextWithoutBothLettersAndDigits()
    {
        IReadOnlyList<string> candidates =
            PlateCandidateDetector.FindCandidates(["TRUCK", "1234567", "9:35 AM"]);

        Assert.Empty(candidates);
    }

    [Fact]
    public void NormalizesCaseAndRemovesDuplicateCandidates()
    {
        IReadOnlyList<string> candidates =
            PlateCandidateDetector.FindCandidates(["ab123cd", "AB123CD"]);

        Assert.Equal(["AB123CD"], candidates);
    }

    [Fact]
    public void EnumeratesOnlyTopLevelJpegAndPngFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string nestedDirectory = Path.Combine(directory, "nested");
        Directory.CreateDirectory(nestedDirectory);

        try
        {
            File.WriteAllText(Path.Combine(directory, "one.JPG"), string.Empty);
            File.WriteAllText(Path.Combine(directory, "two.jpeg"), string.Empty);
            File.WriteAllText(Path.Combine(directory, "three.png"), string.Empty);
            File.WriteAllText(Path.Combine(directory, "ignored.webp"), string.Empty);
            File.WriteAllText(Path.Combine(nestedDirectory, "nested.jpg"), string.Empty);

            string[] images = ImageFileEnumerator.Enumerate(directory);

            Assert.Equal(3, images.Length);
            Assert.DoesNotContain(images, path => path.Contains("nested", StringComparison.Ordinal));
            Assert.Equal(["one.JPG", "three.png", "two.jpeg"], images.Select(Path.GetFileName));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
