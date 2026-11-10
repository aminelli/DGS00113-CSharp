using System.Text.RegularExpressions;

namespace PlateOcr;

public static partial class PlateCandidateDetector
{
    private const int MinimumCandidateLength = 6;
    private const int MaximumCandidateLength = 10;

    [GeneratedRegex("[A-Z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    public static IReadOnlyList<string> FindCandidates(IEnumerable<string> recognizedLines)
    {
        var candidates = new HashSet<string>(StringComparer.Ordinal);

        foreach (string line in recognizedLines)
        {
            string[] tokens = TokenRegex()
                .Matches(line.ToUpperInvariant())
                .Select(match => match.Value)
                .ToArray();

            for (int start = 0; start < tokens.Length; start++)
            {
                string candidate = string.Empty;
                for (int tokenCount = 1; tokenCount <= 3 && start + tokenCount <= tokens.Length; tokenCount++)
                {
                    candidate += tokens[start + tokenCount - 1];
                    if (candidate.Length is < MinimumCandidateLength or > MaximumCandidateLength)
                    {
                        continue;
                    }

                    bool hasLetter = candidate.Any(char.IsAsciiLetter);
                    bool hasDigit = candidate.Any(char.IsAsciiDigit);
                    if (hasLetter && hasDigit)
                    {
                        candidates.Add(candidate);
                    }
                }
            }
        }

        return candidates.Order(StringComparer.Ordinal).ToArray();
    }
}
