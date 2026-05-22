using System.Text.Json;
using System.Text.Encodings.Web;

namespace SupportWorkflow;

/// <summary>
/// Utility class for reading and writing to the knowledge base files (know_issues.json and detected_patterns.json).
/// </summary>
public static class KnowledgeBasePersistence
{
    private const string KnownIssuesFile = "know_issues.json";
    private const string DetectedPatternsFile = "detected_patterns.json";
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private static readonly bool EnableKnownIssueWrites = bool.TryParse(Environment.GetEnvironmentVariable("ENABLE_KNOWN_ISSUE_WRITES"), out var enabled) && enabled;
    private static readonly int PromotionThreshold = int.TryParse(Environment.GetEnvironmentVariable("PATTERN_PROMOTION_THRESHOLD"), out var threshold) ? threshold : 3;

    public static bool KnownIssueWritesEnabled => EnableKnownIssueWrites;
    public static int PatternPromotionThreshold => PromotionThreshold;

    /// <summary>
    /// Reads all known issues from the knowledge base file.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of known issues</returns>
    public static async Task<List<KnownIssue>> ReadKnownIssuesAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(KnownIssuesFile))
        {
            return new List<KnownIssue>();
        }

        try
        {
            var jsonContent = await File.ReadAllTextAsync(KnownIssuesFile, cancellationToken);
            return JsonSerializer.Deserialize<List<KnownIssue>>(jsonContent) ?? new List<KnownIssue>();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to read known issues: {ex.Message}");
            return new List<KnownIssue>();
        }
    }

    /// <summary>
    /// Writes known issues to the knowledge base file.
    /// </summary>
    /// <param name="issues">List of known issues to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static async Task WriteKnownIssuesAsync(List<KnownIssue> issues, CancellationToken cancellationToken = default)
    {
        if (!EnableKnownIssueWrites)
        {
            Logger.LogInfo("Known issue writes are disabled; skipping write to know_issues.json.");
            return;
        }

        try
        {
            var jsonContent = JsonSerializer.Serialize(issues, JsonOptions);
            await File.WriteAllTextAsync(KnownIssuesFile, jsonContent, System.Text.Encoding.UTF8, cancellationToken);
            Logger.LogInfo($"Successfully saved {issues.Count} known issues to {KnownIssuesFile}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to write known issues: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads all detected patterns from the patterns file.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of detected patterns</returns>
    public static async Task<List<PatternRecord>> ReadDetectedPatternsAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(DetectedPatternsFile))
        {
            return new List<PatternRecord>();
        }

        try
        {
            var jsonContent = await File.ReadAllTextAsync(DetectedPatternsFile, cancellationToken);
            return JsonSerializer.Deserialize<List<PatternRecord>>(jsonContent) ?? new List<PatternRecord>();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to read detected patterns: {ex.Message}");
            return new List<PatternRecord>();
        }
    }

    /// <summary>
    /// Writes detected patterns to the patterns file.
    /// </summary>
    /// <param name="patterns">List of detected patterns to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static async Task WriteDetectedPatternsAsync(List<PatternRecord> patterns, CancellationToken cancellationToken = default)
    {
        try
        {
            var jsonContent = JsonSerializer.Serialize(patterns, JsonOptions);
            await File.WriteAllTextAsync(DetectedPatternsFile, jsonContent, System.Text.Encoding.UTF8, cancellationToken);
            Logger.LogInfo($"Successfully saved {patterns.Count} detected patterns to {DetectedPatternsFile}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to write detected patterns: {ex.Message}");
        }
    }

    /// <summary>
    /// Converts a detected pattern to a known issue and adds it to the knowledge base.
    /// </summary>
    /// <param name="pattern">The pattern to promote to a known issue</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static async Task PromotePatternToKnownIssueAsync(PatternRecord pattern, CancellationToken cancellationToken = default)
    {
        if (!EnableKnownIssueWrites)
        {
            Logger.LogInfo($"Known issue promotion is disabled; skipping promotion of pattern: {pattern.PatternDescription}");
            return;
        }

        // Check if pattern frequency meets the threshold
        if (pattern.Frequency < PromotionThreshold)
        {
            Logger.LogInfo($"Pattern frequency {pattern.Frequency} is below threshold {PromotionThreshold}; skipping promotion of pattern: {pattern.PatternDescription}");
            return;
        }

        try
        {
            var knownIssues = await ReadKnownIssuesAsync(cancellationToken);

            // Check if a similar issue already exists
            if (knownIssues.Any(ki => AreIssuesSimilar(ki.Problem, pattern.PatternDescription)))
            {
                Logger.LogInfo($"Similar known issue already exists; skipping promotion of pattern: {pattern.PatternDescription}");
                // Still mark as promoted to avoid re-attempts
                pattern.PromotedToKnownIssue = true;
                pattern.LinkedKnownIssue = knownIssues.First(ki => AreIssuesSimilar(ki.Problem, pattern.PatternDescription)).Problem;
                return;
            }

            // Create a new known issue from the pattern
            var newIssue = new KnownIssue
            {
                Problem = pattern.PatternDescription,
                Symptoms = pattern.ExampleSymptoms,
                Keywords = ExtractKeywords(pattern),
                Solution = pattern.ExampleSolutions.FirstOrDefault() ?? string.Empty,
                ActionRequired = false,
                SuccessRate = 0.95
            };

            knownIssues.Add(newIssue);
            await WriteKnownIssuesAsync(knownIssues, cancellationToken);

            // Mark pattern as promoted
            pattern.PromotedToKnownIssue = true;
            pattern.LinkedKnownIssue = newIssue.Problem;

            Logger.LogInfo($"Pattern promoted to known issue: {pattern.PatternDescription}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to promote pattern to known issue: {ex.Message}");
        }
    }

    private static bool AreIssuesSimilar(string desc1, string desc2)
    {
        if (string.IsNullOrEmpty(desc1) || string.IsNullOrEmpty(desc2))
            return false;

        return desc1.Trim().Equals(desc2.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts keywords from a pattern for use in the knowledge base.
    /// </summary>
    private static List<string> ExtractKeywords(PatternRecord pattern)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add symptoms as keywords
        foreach (var symptom in pattern.ExampleSymptoms)
        {
            var words = symptom.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words.Where(w => w.Length > 3))
            {
                keywords.Add(word.ToLower());
            }
        }

        return keywords.ToList();
    }
}
