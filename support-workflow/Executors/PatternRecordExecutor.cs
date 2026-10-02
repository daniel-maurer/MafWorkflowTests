using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace SupportWorkflow;

/// <summary>
/// Executor responsible for recording and analyzing patterns from human support interactions.
/// Captures patterns for future automation and creates entries in the knowledge base.
/// 
/// Architecture: This executor orchestrates the pattern analysis workflow. The analysis prompt
/// is managed by PatternRecordAgentFactory (factory owns configuration), keeping this executor
/// focused on orchestration logic and workflow completion.
/// </summary>
internal sealed class PatternRecordExecutor : Executor<ResolutionResult, PatternRecord>
{
    private readonly AIAgent _patternRecordAgent;
    private readonly IUserInteractor _userInteractor;

    // Safety constants to prevent infinite loops and hangs
    private const int MaxPatternAnalysisAttempts = 1;
    private const int PatternAnalysisTimeoutSeconds = 30;

    public PatternRecordExecutor(AIAgent patternRecordAgent, IUserInteractor userInteractor) : base("PatternRecordExecutor")
    {
        this._patternRecordAgent = patternRecordAgent ?? throw new ArgumentNullException(nameof(patternRecordAgent));
        this._userInteractor = userInteractor ?? throw new ArgumentNullException(nameof(userInteractor));
    }

    public override async ValueTask<PatternRecord> HandleAsync(
        ResolutionResult resolutionResult,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        if (resolutionResult == null)
        {
            throw new ArgumentNullException(nameof(resolutionResult), "ResolutionResult cannot be null");
        }

        await _userInteractor.PublishAgentStateAsync("pattern", "active", "Running", cancellationToken);
        await _userInteractor.PublishContextAsync(
            "recording",
            "Recording pattern",
            "Recording the resolution pattern for future automation.",
            "pattern",
            false,
            cancellationToken);

        Logger.LogInfo("Starting pattern record analysis for resolved issue");
        Logger.LogDebug($"Issue resolved: {resolutionResult.IsResolved}");

        if (!resolutionResult.IsResolved)
        {
            await _userInteractor.PublishTraceAsync("Pattern recording skipped - issue not resolved", "info", cancellationToken);
            Logger.LogInfo("Skipping pattern recording - issue was not resolved");
            await _userInteractor.PublishAgentStateAsync("pattern", "done", "Skipped", cancellationToken);
            await _userInteractor.PublishContextAsync(
                "resolved",
                "Session closed",
                "Issue not resolved — pattern recording skipped.",
                string.Empty,
                false,
                cancellationToken);
            return CreateEmptyResult();
        }

        await _userInteractor.PublishTraceAsync("Analyzing pattern from resolved issue", "info", cancellationToken);

        var problemSummary = await context.ReadStateAsync<string>(Constants.ProblemSummaryKey, Constants.TriageStateScope) ?? "Unknown problem";
        var escalationReason = resolutionResult.EscalationReason ?? "No specific reason";
        var solution = resolutionResult.MessageForUser;

        // Read existing patterns from knowledge base to inject into the prompt
        var existingPatterns = await KnowledgeBasePersistence.ReadDetectedPatternsAsync(cancellationToken);
        string existingPatternsText = "";
        if (existingPatterns.Count > 0)
        {
            existingPatternsText = string.Join("\n", existingPatterns.Select(p =>
                $"- Descrição: \"{p.PatternDescription}\" | Frequência: {p.Frequency} | Solução: \"{(p.ExampleSolutions.Count > 0 ? p.ExampleSolutions[0] : string.Empty)}\""));
        }
        else
        {
            existingPatternsText = "(Nenhum padrão registrado ainda)";
        }

        // Create a timeout token combining user cancellation + timeout to prevent hangs
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(PatternAnalysisTimeoutSeconds));

        int attemptCount = 0;
        PatternRecord? result = null;

        while (attemptCount < MaxPatternAnalysisAttempts && result == null)
        {
            try
            {
                attemptCount++;

                // Build analysis prompt using factory template (factory owns configuration)
                var promptTemplate = PatternRecordAgentFactory.GetAnalysisPromptTemplate();
                var analysisPrompt = string.Format(
                    promptTemplate,
                    problemSummary,
                    escalationReason,
                    solution,
                    existingPatternsText);

                Logger.LogDebug($"Analyzing pattern for: {problemSummary}");

                await _userInteractor.SetAgentTypingAsync("Pattern Record Agent analyzing", true, cancellationToken);
                await _userInteractor.PublishTraceAsync("Extracting pattern characteristics", "info", cancellationToken);

                // Call agent with timeout to prevent indefinite waiting
                var response = await this._patternRecordAgent.RunAsync(
                    analysisPrompt,
                    cancellationToken: timeoutCts.Token);

                if (AgentResponseParser.TryDeserializeAgentResponse(response.Text, out PatternRecord? patternResult) && patternResult != null)
                {
                    Logger.LogInfo($"Pattern analyzed: {patternResult.PatternDescription}");

                    await PersistPatternAsync(patternResult, cancellationToken);
                    DisplayPatternInfo(patternResult);

                    await _userInteractor.PublishTraceAsync("Pattern analysis complete, recording to knowledge base", "success", cancellationToken);
                    await context.YieldOutputAsync(patternResult, cancellationToken);
                    await _userInteractor.PublishAgentStateAsync("pattern", "done", "Done", cancellationToken);
                    await _userInteractor.PublishContextAsync(
                        "resolved",
                        "Session Resolved ✓",
                        "Issue resolved and pattern recorded.",
                        "pattern",
                        false,
                        cancellationToken);

                    result = patternResult;
                }
                else
                {
                    await _userInteractor.PublishTraceAsync("Failed to analyze pattern - invalid response format", "error", cancellationToken);
                    Logger.LogError($"Failed to deserialize pattern record result. Raw response: {response.Text}");
                    result = CreateEmptyResult();
                }
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
            {
                await _userInteractor.PublishTraceAsync($"Pattern analysis timed out after {PatternAnalysisTimeoutSeconds} seconds", "error", cancellationToken);
                Logger.LogError($"Pattern analysis timed out after {PatternAnalysisTimeoutSeconds} seconds");
                result = CreateEmptyResult();
            }
            catch (Exception ex)
            {
                await _userInteractor.PublishTraceAsync($"Error during pattern analysis: {ex.Message}", "error", cancellationToken);
                Logger.LogError($"Error during pattern recording (attempt {attemptCount}): {ex.Message}");

                if (attemptCount >= MaxPatternAnalysisAttempts)
                {
                    result = CreateEmptyResult();
                }
            }
            finally
            {
                await _userInteractor.SetAgentTypingAsync("Pattern Record Agent analyzing", false, cancellationToken);
            }
        }

        return result ?? CreateEmptyResult();
    }


    private async Task PersistPatternAsync(PatternRecord patternResult, CancellationToken cancellationToken)
    {
        if (!ShouldPersistPattern(patternResult))
        {
            Logger.LogInfo($"Skipped recording generic or low-value pattern: {patternResult.PatternDescription}");
            return;
        }

        try
        {
            var pattern = patternResult;
            pattern.Frequency = 1;
            pattern.FirstDetected = DateTime.UtcNow;
            pattern.LastDetected = DateTime.UtcNow;
            pattern.PromotedToKnownIssue = false;

            var existingPatterns = await KnowledgeBasePersistence.ReadDetectedPatternsAsync(cancellationToken);

            var duplicatePattern = existingPatterns.FirstOrDefault(p =>
                p.PatternDescription.Equals(pattern.PatternDescription, StringComparison.OrdinalIgnoreCase));

            if (duplicatePattern != null)
            {
                duplicatePattern.Frequency++;
                duplicatePattern.LastDetected = DateTime.UtcNow;

                foreach (var symptom in pattern.ExampleSymptoms)
                {
                    if (!duplicatePattern.ExampleSymptoms.Contains(symptom, StringComparer.OrdinalIgnoreCase))
                    {
                        duplicatePattern.ExampleSymptoms.Add(symptom);
                    }
                }

                foreach (var sol in pattern.ExampleSolutions)
                {
                    if (!duplicatePattern.ExampleSolutions.Contains(sol, StringComparer.OrdinalIgnoreCase))
                    {
                        duplicatePattern.ExampleSolutions.Add(sol);
                    }
                }

                // Limit example lists to 3 items to keep pattern record simple
                if (duplicatePattern.ExampleSymptoms.Count > 3)
                {
                    duplicatePattern.ExampleSymptoms = duplicatePattern.ExampleSymptoms.Take(3).ToList();
                }
                if (duplicatePattern.ExampleSolutions.Count > 3)
                {
                    duplicatePattern.ExampleSolutions = duplicatePattern.ExampleSolutions.Take(3).ToList();
                }

                Logger.LogInfo($"[AGENT] Updated existing pattern: {patternResult.PatternDescription} (Freq: {duplicatePattern.Frequency})");
            }
            else
            {
                // Limit initial list sizes to 3
                pattern.ExampleSymptoms = pattern.ExampleSymptoms.Take(3).ToList();
                pattern.ExampleSolutions = pattern.ExampleSolutions.Take(3).ToList();
                existingPatterns.Add(pattern);
                Logger.LogInfo($"Recorded new pattern: {patternResult.PatternDescription}");
            }

            var patternToPromote = duplicatePattern ?? pattern;
            await KnowledgeBasePersistence.PromotePatternToKnownIssueAsync(patternToPromote, cancellationToken);

            await KnowledgeBasePersistence.WriteDetectedPatternsAsync(existingPatterns, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error persisting pattern: {ex.Message}");
        }
    }

    private static bool ShouldPersistPattern(PatternRecord patternResult)
    {
        if (patternResult == null)
            return false;

        if (string.IsNullOrWhiteSpace(patternResult.PatternDescription))
            return false;

        var lowerDesc = patternResult.PatternDescription.ToLower();
        if (lowerDesc.Contains("generic") || lowerDesc.Contains("unknown") || lowerDesc.Contains("other") || lowerDesc.Contains("miscellaneous"))
            return false;

        return true;
    }

    private void DisplayPatternInfo(PatternRecord pattern)
    {
        if (pattern == null)
            return;

        Logger.OutputAgent($"\n[Padrão Identificado]");
        Logger.OutputAgent($"Descrição: {pattern.PatternDescription}");
        Logger.OutputAgent($"Frequência: {pattern.Frequency}");
        Logger.OutputAgent($"Últimos Sintomas: {string.Join(" | ", pattern.ExampleSymptoms)}");
        Logger.OutputAgent($"Últimas Soluções: {string.Join(" | ", pattern.ExampleSolutions)}");
    }

    private PatternRecord CreateEmptyResult()
    {
        return new PatternRecord
        {
            PatternDescription = "Pattern analysis failed"
        };
    }
}
