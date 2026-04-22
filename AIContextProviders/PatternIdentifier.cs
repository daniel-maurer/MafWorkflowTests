namespace SupportWorkflow;

/// <summary>
/// Identifies patterns in user issues after human support resolution.
/// Analyzes symptoms, temporal characteristics, and solutions to detect recurring problems
/// that could become known issues in the knowledge base.
/// </summary>
public class PatternIdentifier
{
    /// <summary>
    /// Analyzes a human support interaction to identify potential patterns.
    /// Uses smart deduplication to prevent duplicate patterns from multiple detection methods.
    /// </summary>
    /// <param name="userProblem">The original problem description from the user</param>
    /// <param name="humanSolution">The solution provided by human support</param>
    /// <param name="isResolved">Whether the issue was resolved</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of identified patterns with deduplication applied</returns>
    public static async Task<List<PatternRecord>> IdentifyPatternsAsync(
        string userProblem,
        string humanSolution,
        bool isResolved,
        CancellationToken cancellationToken = default)
    {
        var identifiedPatterns = new List<PatternRecord>();

        if (!isResolved || string.IsNullOrEmpty(userProblem))
        {
            return identifiedPatterns;
        }
        // Use a general agent-style pattern detector instead of rigid hard-coded categories.
        var genericPattern = DetectAgentDrivenPattern(userProblem, humanSolution);
        if (genericPattern != null)
        {
            identifiedPatterns.Add(genericPattern);
        }

        // Record and update patterns
        if (identifiedPatterns.Count > 0)
        {
            await RecordPatternsAsync(identifiedPatterns, cancellationToken);
        }

        return identifiedPatterns;
    }

    /// <summary>
    /// Uses agent-style inference to create a generic pattern from an issue description.
    /// This avoids brittle hard-coded categories and lets the system classify unknown problems.
    /// </summary>
    private static PatternRecord? DetectAgentDrivenPattern(string userProblem, string humanSolution)
    {
        var lowerProblem = userProblem.ToLowerInvariant();
        var lowerSolution = (humanSolution ?? string.Empty).ToLowerInvariant();

        var commonWords = new[]
        {
            "the", "and", "you", "que", "for", "with", "from", "não", "nao", "uma", "um", "as", "em", "de", "ou", "o", "a", "is", "are", "was", "were"
        };

        var problemKeywords = ExtractImportantWords(lowerProblem, commonWords);
        var solutionKeywords = ExtractImportantWords(lowerSolution, commonWords);

        if (problemKeywords.Count == 0)
        {
            return null;
        }

        var descriptionKeywords = problemKeywords.Take(5).ToList();
        var description = descriptionKeywords.Count == 1
            ? $"Pattern involving {descriptionKeywords[0]}"
            : $"Pattern involving {string.Join(", ", descriptionKeywords)}";

        var confidence = Math.Min(
            0.90,
            0.55 + Math.Min(0.30, problemKeywords.Count * 0.06 + solutionKeywords.Count * 0.04)
        );

        if (HasTemporalContext(lowerProblem))
        {
            confidence = Math.Max(confidence, 0.65);
        }

        return new PatternRecord
        {
            PatternDescription = description,
            Confidence = confidence,
            Frequency = 1,
            FirstDetected = DateTime.UtcNow,
            LastDetected = DateTime.UtcNow,
            ExampleSymptoms = new List<string> { userProblem },
            ExampleSolutions = new List<string> { string.IsNullOrWhiteSpace(humanSolution) ? "Solution needs verification" : humanSolution },
            TemporalCharacteristics = HasTemporalContext(lowerProblem) ? "Timing or delay aspect detected" : null,
            PromotedToKnownIssue = false
        };
    }

    private static bool HasTemporalContext(string lowerText)
    {
        var timeKeywords = new[]
        {
            "atraso", "delay", "dia", "day", "hora", "hour", "semana", "week", "prazo", "deadline", "tempo", "time", "quando", "when", "dentro de", "até", "later"
        };
        return timeKeywords.Any(lowerText.Contains);
    }

    /// <summary>
    /// Extracts important words (non-common, 3+ characters) from text.
    /// </summary>
    private static HashSet<string> ExtractImportantWords(string text, string[] commonWords)
    {
        var words = System.Text.RegularExpressions.Regex.Split(text, @"[^a-záéíóúãõâêçñ0-9]+")
            .Where(w => w.Length >= 3 && !commonWords.Contains(w, StringComparer.OrdinalIgnoreCase))
            .Take(7);

        return new HashSet<string>(words, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a pattern is a semantic duplicate of existing patterns (e.g., same issue detected by different methods).
    /// </summary>
    private static bool IsDuplicatePattern(PatternRecord newPattern, List<PatternRecord> existingPatterns)
    {
        // Check for high keyword/symptom overlap indicating same issue
        foreach (var existing in existingPatterns)
        {
            var overlapScore = CalculatePatternSimilarity(newPattern, existing);
            if (overlapScore > 0.7) // 70% similarity threshold
            {
                Logger.LogDebug($"Duplicate pattern detected: '{newPattern.PatternDescription}' is similar to '{existing.PatternDescription}' (similarity: {overlapScore:F2})");
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Finds a semantically similar pattern in the existing patterns list.
    /// Returns the best match if similarity is above threshold.
    /// </summary>
    private static PatternRecord? FindSemanticPatternMatch(PatternRecord newPattern, List<PatternRecord> existingPatterns)
    {
        PatternRecord? bestMatch = null;
        double highestSimilarity = 0.6; // Similarity threshold (60%)

        foreach (var existing in existingPatterns)
        {
            var similarity = CalculatePatternSimilarity(newPattern, existing);
            if (similarity > highestSimilarity)
            {
                highestSimilarity = similarity;
                bestMatch = existing;
            }
        }

        if (bestMatch != null)
        {
            Logger.LogDebug($"Found semantic match: '{newPattern.PatternDescription}' matches '{bestMatch.PatternDescription}' (similarity: {highestSimilarity:F2})");
        }

        return bestMatch;
    }

    /// <summary>
    /// Calculates similarity between two patterns based on symptom/keyword overlap.
    /// Returns a score between 0 and 1 using Jaccard similarity.
    /// </summary>
    private static double CalculatePatternSimilarity(PatternRecord pattern1, PatternRecord pattern2)
    {
        // Extract keywords from descriptions
        var keywords1 = ExtractImportantWords(pattern1.PatternDescription.ToLower(), new[] { "pattern", "involving", "and", "or" });
        var keywords2 = ExtractImportantWords(pattern2.PatternDescription.ToLower(), new[] { "pattern", "involving", "and", "or" });

        // Extract keywords from first 2 symptoms
        foreach (var symptom in pattern1.ExampleSymptoms.Take(2))
        {
            var symptomWords = ExtractImportantWords(symptom.ToLower(), new[] { "the", "and", "or" });
            foreach (var word in symptomWords)
                keywords1.Add(word);
        }

        foreach (var symptom in pattern2.ExampleSymptoms.Take(2))
        {
            var symptomWords = ExtractImportantWords(symptom.ToLower(), new[] { "the", "and", "or" });
            foreach (var word in symptomWords)
                keywords2.Add(word);
        }

        // Calculate Jaccard similarity: intersection / union
        if (keywords1.Count == 0 || keywords2.Count == 0)
            return 0;

        var intersection = keywords1.Intersect(keywords2, StringComparer.OrdinalIgnoreCase).Count();
        var union = keywords1.Union(keywords2, StringComparer.OrdinalIgnoreCase).Count();

        return (double)intersection / union;
    }

    /// <summary>
    /// Records identified patterns to the detected_patterns.json file and updates existing patterns.
    /// Uses smart merging to consolidate semantically similar patterns.
    /// </summary>
    private static async Task RecordPatternsAsync(List<PatternRecord> newPatterns, CancellationToken cancellationToken)
    {
        try
        {
            var existingPatterns = await KnowledgeBasePersistence.ReadDetectedPatternsAsync(cancellationToken);

            // Merge new patterns with existing ones
            foreach (var newPattern in newPatterns)
            {
                // Try exact match first (case-insensitive description)
                var existingPattern = existingPatterns.FirstOrDefault(p =>
                    p.PatternDescription.Equals(newPattern.PatternDescription, StringComparison.OrdinalIgnoreCase));

                // If no exact match, try semantic match (keywords/symptoms overlap)
                if (existingPattern == null)
                {
                    existingPattern = FindSemanticPatternMatch(newPattern, existingPatterns);
                }

                if (existingPattern != null)
                {
                    // Update existing pattern
                    existingPattern.Frequency++;
                    existingPattern.LastDetected = DateTime.UtcNow;

                    // Add new examples
                    foreach (var symptom in newPattern.ExampleSymptoms)
                    {
                        if (!existingPattern.ExampleSymptoms.Contains(symptom, StringComparer.OrdinalIgnoreCase))
                        {
                            existingPattern.ExampleSymptoms.Add(symptom);
                        }
                    }

                    foreach (var solution in newPattern.ExampleSolutions)
                    {
                        if (!existingPattern.ExampleSolutions.Contains(solution, StringComparer.OrdinalIgnoreCase))
                        {
                            existingPattern.ExampleSolutions.Add(solution);
                        }
                    }

                    // Update confidence based on frequency
                    existingPattern.Confidence = Math.Min(0.95, existingPattern.Confidence + (newPattern.Confidence * 0.1));

                    Logger.LogInfo($"Updated existing pattern: {existingPattern.PatternDescription} (Frequency: {existingPattern.Frequency}, Confidence: {existingPattern.Confidence:F2})");
                }
                else
                {
                    // Add new pattern
                    existingPatterns.Add(newPattern);
                    Logger.LogInfo($"Recorded new pattern: {newPattern.PatternDescription} (Confidence: {newPattern.Confidence})");
                }
            }

            // Check if any patterns should be promoted to known issues
            foreach (var pattern in existingPatterns)
            {
                if (!pattern.PromotedToKnownIssue && pattern.Frequency >= 3 && pattern.Confidence >= 0.75)
                {
                    Logger.LogInfo($"Pattern '{pattern.PatternDescription}' meets promotion criteria (Frequency: {pattern.Frequency}, Confidence: {pattern.Confidence:F2})");
                    await KnowledgeBasePersistence.PromotePatternToKnownIssueAsync(pattern, cancellationToken);
                }
            }

            await KnowledgeBasePersistence.WriteDetectedPatternsAsync(existingPatterns, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to record patterns: {ex.Message}");
        }
    }
}
