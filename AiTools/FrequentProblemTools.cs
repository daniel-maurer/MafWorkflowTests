using System.ComponentModel;
using System.Text.Json;

namespace SupportWorkflow;

/// <summary>
/// Tools for the frequent problem detection agent to access and search known issues.
/// </summary>
public static class FrequentProblemTools
{
    /// <summary>
    /// Retrieves known issues that match the provided keywords from the knowledge base.
    /// </summary>
    /// <param name="keyWords">List of keywords to search for known issues. Each keyword is matched against issue keywords.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of matching KnownIssue objects</returns>
    [Description("Get the known issues for a given set of keywords in pt-BR.")]
    public static async Task<List<KnownIssue>> GetKnownIssuesAsync(
        [Description("The keywords to search for known issues. Keyword is a unique word.")] List<string> keyWords,
        CancellationToken cancellationToken = default)
    {
        string knownIssuesPath = "know_issues.json";

        if (!File.Exists(knownIssuesPath))
        {
            throw new FileNotFoundException("File known_issues.json not found.", knownIssuesPath);
        }

        try
        {
            var jsonContent = await File.ReadAllTextAsync(knownIssuesPath, cancellationToken);

            var listKnownIssues = JsonSerializer.Deserialize<List<KnownIssue>>(jsonContent) ?? [];

            var result = listKnownIssues.Where(issue => 
                issue.Keywords.Any(kw => keyWords.Contains(kw, StringComparer.OrdinalIgnoreCase))).ToList();

            return result;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse known_issues.json: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves promoted patterns (high-confidence patterns that have become known issues) matching provided keywords.
    /// These patterns have frequency >= 3 and confidence >= 0.75, and have been promoted to the known issues list.
    /// </summary>
    /// <param name="keyWords">List of keywords to search for promoted patterns.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of matching KnownIssue objects that were promoted from patterns</returns>
    [Description("Get promoted patterns (high-confidence known issues) for a given set of keywords.")]
    public static async Task<List<KnownIssue>> GetPromotedPatternsAsync(
        [Description("The keywords to search for promoted patterns.")] List<string> keyWords,
        CancellationToken cancellationToken = default)
    {
        if (!KnowledgeBasePersistence.KnownIssueWritesEnabled)
        {
            return new List<KnownIssue>();
        }

        string detectedPatternsPath = "detected_patterns.json";

        if (!File.Exists(detectedPatternsPath))
        {
            return new List<KnownIssue>();
        }

        try
        {
            var jsonContent = await File.ReadAllTextAsync(detectedPatternsPath, cancellationToken);
            var detectedPatterns = JsonSerializer.Deserialize<List<PatternRecord>>(jsonContent) ?? [];

            // Filter for promoted patterns with high confidence
            var promotedPatterns = detectedPatterns
                .Where(p => p.PromotedToKnownIssue && p.Frequency >= 3 && p.Confidence >= 0.75)
                .ToList();

            if (promotedPatterns.Count == 0)
            {
                return new List<KnownIssue>();
            }

            // Convert promoted patterns to KnownIssue objects for consistent return type
            var result = promotedPatterns
                .Where(pattern =>
                {
                    var patternKeywords = ExtractKeywordsFromPattern(pattern);
                    return patternKeywords.Any(pk => keyWords.Any(kw => pk.Equals(kw, StringComparison.OrdinalIgnoreCase) || kw.Contains(pk, StringComparison.OrdinalIgnoreCase)));
                })
                .Select(pattern => new KnownIssue
                {
                    Problem = pattern.PatternDescription,
                    Symptoms = pattern.ExampleSymptoms,
                    Keywords = ExtractKeywordsFromPattern(pattern),
                    Solution = pattern.ExampleSolutions.FirstOrDefault() ?? pattern.TemporalCharacteristics ?? string.Empty,
                    ActionRequired = true,
                    SuccessRate = pattern.Confidence
                })
                .ToList();

            return result;
        }
        catch (JsonException ex)
        {
            Logger.LogError($"Failed to parse detected_patterns.json: {ex.Message}");
            return new List<KnownIssue>();
        }
    }

    /// <summary>
    /// Extracts keywords from a detected pattern.
    /// </summary>
    private static List<string> ExtractKeywordsFromPattern(PatternRecord pattern)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Extract from pattern description
        var words = pattern.PatternDescription.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words.Where(w => w.Length > 2))
        {
            keywords.Add(word.ToLower());
        }

        // Extract from first symptom
        if (pattern.ExampleSymptoms.Count > 0)
        {
            var symptomWords = pattern.ExampleSymptoms[0].Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in symptomWords.Where(w => w.Length > 2))
            {
                keywords.Add(word.ToLower());
            }
        }

        return keywords.ToList();
    }
}