using System.ComponentModel;

namespace SupportWorkflow;

public static class PatternIdentificationTools
{
    [Description(
        "Record a newly identified pattern as a potential known issue. Call this after successfully resolving an issue to teach the system about the pattern."
    )]
    public static Task<PatternRecordingResult> RecordPatternAsync(
        [Description("The problem title/pattern name (e.g., 'Meal Voucher Delayed Payment')")]
            string problemTitle,
        [Description(
            "List of keywords/symptoms that identify this problem (e.g., ['vale refeição', 'atraso', 'não recebi'])"
        )]
            List<string> symptoms,
        [Description("The solution that worked to resolve this issue")] string solution,
        [Description(
            "Confidence level 0.0-1.0: 0.9+ (very confident), 0.7-0.8 (confident), <0.7 (needs verification)"
        )]
            double successRate,
        [Description(
            "Tools/actions used to resolve (e.g., ['CheckPaymentStatus', 'ContactFinance'])"
        )]
            List<string> tools,
        [Description("Estimated resolution time in seconds")] int estimatedResolutionTime = 300,
        CancellationToken cancellationToken = default
    )
    {
        return RecordPatternInternalAsync(
            problemTitle,
            symptoms,
            solution,
            successRate,
            tools,
            estimatedResolutionTime,
            cancellationToken
        );
    }

    [Description(
        "Directly promote a high-confidence pattern to a known issue. Use only when very confident (0.8+) that this should be in the knowledge base."
    )]
    public static Task<PatternPromotionResult> PromotePatternToKnownIssueAsync(
        [Description("Problem title for known issues")] string problemTitle,
        [Description("Keywords/symptoms for this issue")] List<string> symptoms,
        [Description("Verified solution")] string solution,
        [Description("Confidence level (0.75+ required, 0.8+ recommended)")] double successRate,
        [Description("Required tools/MCP actions")] List<string> tools,
        CancellationToken cancellationToken = default
    )
    {
        return PromotePatternInternalAsync(
            problemTitle,
            symptoms,
            solution,
            successRate,
            tools,
            cancellationToken
        );
    }

    [Description("Get the recorded patterns")]
    public static async Task<List<PatternRecord>> GetRecordedPatternsAsync(
        CancellationToken CancellationToken = default
    )
    {
        return await KnowledgeBasePersistence.ReadDetectedPatternsAsync(CancellationToken);
    }

    private static async Task<PatternRecordingResult> RecordPatternInternalAsync(
        string problemTitle,
        List<string> symptoms,
        string solution,
        double successRate,
        List<string> tools,
        int estimatedResolutionTime,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(problemTitle))
                throw new ArgumentException("Problem title cannot be empty");

            if (symptoms == null || symptoms.Count == 0)
                throw new ArgumentException("At least one symptom/keyword is required");

            if (successRate < 0 || successRate > 1)
                throw new ArgumentException("Success rate must be between 0.0 and 1.0");

            var existingPatterns = await KnowledgeBasePersistence.ReadDetectedPatternsAsync(
                cancellationToken
            );

            var newPattern = new PatternRecord
            {
                PatternDescription = problemTitle,
                Frequency = 1,
                FirstDetected = DateTime.UtcNow,
                LastDetected = DateTime.UtcNow,
                ExampleSymptoms = symptoms.Take(5).ToList(), // Limit to 5 examples
                ExampleSolutions = new List<string> { solution },
                TemporalCharacteristics = null,
            };

            // Check if pattern already exists (by description)
            var existing = existingPatterns.FirstOrDefault(p =>
                p.PatternDescription.Equals(problemTitle, StringComparison.OrdinalIgnoreCase)
            );

            if (existing != null)
            {
                // Update existing pattern
                existing.Frequency++;
                existing.LastDetected = DateTime.UtcNow;
                existing.Confidence = Math.Min(0.95, existing.Confidence + (successRate * 0.1));

                // Merge symptoms
                foreach (var symptom in symptoms)
                {
                    if (
                        !existing.ExampleSymptoms.Contains(
                            symptom,
                            StringComparer.OrdinalIgnoreCase
                        )
                    )
                    {
                        existing.ExampleSymptoms.Add(symptom);
                    }
                }

                if (!existing.ExampleSolutions.Contains(solution, StringComparer.OrdinalIgnoreCase))
                {
                    existing.ExampleSolutions.Add(solution);
                }

                Logger.LogInfo(
                    $"[AGENT] Updated existing pattern: {problemTitle} (Freq: {existing.Frequency}, Conf: {existing.Confidence:F2})"
                );
            }
            else
            {
                // Add new pattern
                existingPatterns.Add(newPattern);
                Logger.LogInfo(
                    $"[AGENT] Recorded new pattern: {problemTitle} (Conf: {successRate})"
                );
            }

            // Save patterns
            await KnowledgeBasePersistence.WriteDetectedPatternsAsync(
                existingPatterns,
                cancellationToken
            );

            // Check if pattern meets promotion criteria
            var patternToCheck = existing ?? newPattern;
            bool promoted = false;

            if (
                KnowledgeBasePersistence.KnownIssueWritesEnabled
                && !patternToCheck.PromotedToKnownIssue
                && patternToCheck.Frequency >= 3
                && patternToCheck.Confidence >= 0.75
            )
            {
                Logger.LogInfo(
                    $"[AGENT] Pattern '{problemTitle}' meets promotion criteria (Freq: {patternToCheck.Frequency}, Conf: {patternToCheck.Confidence:F2})"
                );
                await KnowledgeBasePersistence.PromotePatternToKnownIssueAsync(
                    patternToCheck,
                    cancellationToken
                );
                promoted = true;
            }

            return new PatternRecordingResult
            {
                Success = true,
                Message = promoted
                    ? $"Pattern recorded and promoted to known issue: {problemTitle}"
                    : $"Pattern recorded: {problemTitle}",
                PatternId = problemTitle,
                Frequency = existing?.Frequency ?? 1,
                Confidence = patternToCheck.Confidence,
                PromotedToKnownIssue = promoted,
                NextSteps = promoted
                    ? "This issue is now in the knowledge base and will auto-resolve next time"
                    : $"Pattern will be promoted after {3 - (existing?.Frequency ?? 1)} more occurrences",
            };
        }
        catch (Exception ex)
        {
            Logger.LogError($"[AGENT] Failed to record pattern: {ex.Message}");
            return new PatternRecordingResult
            {
                Success = false,
                Message = $"Failed to record pattern: {ex.Message}",
                PatternId = problemTitle,
                Frequency = 0,
                Confidence = 0,
            };
        }
    }

    /// <summary>
    /// Internal implementation of pattern promotion.
    /// </summary>
    private static async Task<PatternPromotionResult> PromotePatternInternalAsync(
        string problemTitle,
        List<string> symptoms,
        string solution,
        double successRate,
        List<string> tools,
        CancellationToken cancellationToken
    )
    {
        if (!KnowledgeBasePersistence.KnownIssueWritesEnabled)
        {
            Logger.LogInfo($"Known issue promotion disabled; skipping direct promotion of '{problemTitle}'");
            return new PatternPromotionResult
            {
                Success = false,
                Message = "Known issue promotion is disabled.",
                IssueProblem = problemTitle,
            };
        }

        try
        {
            // Validate confidence level
            if (successRate < 0.75)
                throw new ArgumentException(
                    "Success rate must be at least 0.75 to promote to known issue"
                );

            // Create a known issue
            var newIssue = new KnownIssue
            {
                Problem = problemTitle,
                Symptoms = symptoms.Take(5).ToList(),
                Keywords = ExtractKeywords(symptoms.Concat(new[] { problemTitle })),
                Solution = solution,
                ActionRequired = true,
                SuccessRate = successRate,
                ToolsRequired = tools,
                RecordedByAgent = true,
                RecordedDate = DateTime.UtcNow,
                AgentVersion = "PatternIdentificationAgent",
            };

            // Read existing known issues
            var knownIssues = await KnowledgeBasePersistence.ReadKnownIssuesAsync(
                cancellationToken
            );

            // Check if already exists
            if (
                !knownIssues.Any(ki =>
                    ki.Problem.Equals(problemTitle, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                knownIssues.Add(newIssue);
                await KnowledgeBasePersistence.WriteKnownIssuesAsync(
                    knownIssues,
                    cancellationToken
                );

                Logger.LogInfo(
                    $"[AGENT] Promoted to known issue: {problemTitle} (Conf: {successRate}, RecordedByAgent: true)"
                );

                return new PatternPromotionResult
                {
                    Success = true,
                    Message = $"Successfully promoted to known issue: {problemTitle}",
                    IssueProblem = problemTitle,
                    Keywords = newIssue.Keywords,
                    RecordedByAgent = true,
                    RecordedDate = newIssue.RecordedDate,
                    NextSteps =
                        "This issue is now in the knowledge base and will auto-resolve future occurrences",
                };
            }
            else
            {
                return new PatternPromotionResult
                {
                    Success = false,
                    Message = $"Issue already exists in knowledge base: {problemTitle}",
                    IssueProblem = problemTitle,
                };
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[AGENT] Failed to promote pattern: {ex.Message}");
            return new PatternPromotionResult
            {
                Success = false,
                Message = $"Failed to promote pattern: {ex.Message}",
                IssueProblem = problemTitle,
            };
        }
    }

    /// <summary>
    /// Extracts keywords from symptoms/text.
    /// </summary>
    private static List<string> ExtractKeywords(IEnumerable<string> texts)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var text in texts.Where(t => !string.IsNullOrEmpty(t)))
        {
            var words = text.Split(
                new[] { ' ', '-', '_', ',', '.' },
                StringSplitOptions.RemoveEmptyEntries
            );
            foreach (var word in words.Where(w => w.Length >= 3))
            {
                keywords.Add(word.ToLower());
            }
        }

        return keywords.ToList();
    }
}

/// <summary>
/// Result of pattern analysis.
/// </summary>
public class PatternAnalysisResult
{
    public List<IdentifiedPattern> PatternsIdentified { get; set; } = new();
    public string OverallRecommendation { get; set; } = string.Empty;
    public bool ShouldRecord { get; set; }
    public IdentifiedPattern? BestPatternToRecord { get; set; }
    public string? AnalysisDetails { get; set; }
}

/// <summary>
/// Single identified pattern.
/// </summary>
public class IdentifiedPattern
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public List<string> Keywords { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
}

/// <summary>
/// Guidance on when to record patterns.
/// </summary>
public class RecordingGuidanceResult
{
    public double ConfidenceLevel { get; set; }
    public bool ShouldRecord { get; set; }
    public bool ShouldPromote { get; set; }
    public double RecordingThreshold { get; set; }
    public double PromotionThreshold { get; set; }
    public int FrequencyThreshold { get; set; }
    public List<string> Recommendations { get; set; } = new();
    public List<string> Examples { get; set; } = new();
}

/// <summary>
/// Result of pattern recording operation.
/// </summary>
public class PatternRecordingResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string PatternId { get; set; } = string.Empty;
    public int Frequency { get; set; }
    public double Confidence { get; set; }
    public bool PromotedToKnownIssue { get; set; }
    public string? NextSteps { get; set; }
}

/// <summary>
/// Result of pattern promotion to known issue.
/// </summary>
public class PatternPromotionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string IssueProblem { get; set; } = string.Empty;
    public List<string>? Keywords { get; set; }
    public bool RecordedByAgent { get; set; }
    public DateTime? RecordedDate { get; set; }
    public string? NextSteps { get; set; }
}
