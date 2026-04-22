using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace SupportWorkflow;

/// <summary>
/// Executor responsible for recording and analyzing patterns from human support interactions.
/// Captures patterns for future automation and creates entries in the knowledge base.
/// </summary>
internal sealed class PatternRecordExecutor : Executor<ResolutionResult, PatternRecordResult>
{
    private readonly AIAgent _patternRecordAgent;
    private readonly ConsoleInteractor _consoleInteractor;

    /// <summary>
    /// Initializes a new instance of the PatternRecordExecutor.
    /// </summary>
    /// <param name="patternRecordAgent">The pattern record agent for analysis</param>
    /// <param name="consoleInteractor">The console interactor for user communication</param>
    public PatternRecordExecutor(AIAgent patternRecordAgent, ConsoleInteractor consoleInteractor) : base("PatternRecordExecutor")
    {
        this._patternRecordAgent = patternRecordAgent ?? throw new ArgumentNullException(nameof(patternRecordAgent));
        this._consoleInteractor = consoleInteractor ?? throw new ArgumentNullException(nameof(consoleInteractor));
    }

    /// <summary>
    /// Handles the pattern recording and analysis of a resolved support issue.
    /// </summary>
    /// <param name="resolutionResult">The result from the resolution/human support executor</param>
    /// <param name="context">The workflow context for state management</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A PatternRecordResult containing the analysis</returns>
    public override async ValueTask<PatternRecordResult> HandleAsync(
        ResolutionResult resolutionResult,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        if (resolutionResult == null)
        {
            throw new ArgumentNullException(nameof(resolutionResult), "ResolutionResult cannot be null");
        }

        Logger.LogInfo("Starting pattern record analysis for resolved issue");
        Logger.LogDebug($"Issue resolved: {resolutionResult.IsResolved}");

        // Only record patterns if issue was actually resolved
        if (!resolutionResult.IsResolved)
        {
            Logger.LogInfo("Skipping pattern recording - issue was not resolved");
            return CreateEmptyResult();
        }

        // Get problem summary and escalation reason from context
        var problemSummary = await context.ReadStateAsync<string>(Constants.ProblemSummaryKey, Constants.TriageStateScope) ?? "Unknown problem";
        var escalationReason = resolutionResult.EscalationReason ?? "No specific reason";
        var solution = resolutionResult.MessageForUser;

        // Prepare context for the pattern record agent
        string analysisPrompt = BuildAnalysisPrompt(problemSummary, escalationReason, solution);

        Logger.LogDebug($"Analyzing pattern for: {problemSummary}");

        try
        {
            var history = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.User, analysisPrompt)
            };

            var response = await this._patternRecordAgent.RunAsync(history, cancellationToken: cancellationToken);
            var patternResult = JsonSerializer.Deserialize<PatternRecordResult>(response.Text);

            if (patternResult != null)
            {
                Logger.LogInfo($"Pattern analyzed: {patternResult.PatternType} - {patternResult.PatternDescription}");
                Logger.LogDebug($"Pattern ready for automation: {patternResult.ReadyForAutomation}");

                // Persist pattern to knowledge base
                await PersistPatternAsync(patternResult, cancellationToken);

                // Display pattern information to user
                DisplayPatternInfo(patternResult);

                await context.YieldOutputAsync(patternResult, cancellationToken);
                return patternResult;
            }
            else
            {
                Logger.LogError("Failed to deserialize pattern record result");
                return CreateEmptyResult();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error during pattern recording: {ex.Message}");
            return CreateEmptyResult();
        }
    }

    /// <summary>
    /// Builds the analysis prompt for the pattern record agent.
    /// </summary>
    private static string BuildAnalysisPrompt(string problemSummary, string escalationReason, string solution)
    {
        return $@"Analise o seguinte problema de suporte resolvido e identifique padrões:

PROBLEMA RELATADO:
{problemSummary}

RAZÃO DA ESCALAÇÃO:
{escalationReason}

SOLUÇÃO APLICADA:
{solution}

Por favor, analise este caso e extraia:
1. Que tipo de padrão isto representa?
2. Quais são as palavras-chave?
3. Qual é a característica temporal (se houver)?
4. Quais são os sintomas típicos?
5. Qual é a solução padrão?
6. Com que frequência este padrão provavelmente ocorre?
7. Qual é a taxa de sucesso esperada?
8. Este padrão está pronto para automação?

Seja específico e forneça informações que possam ser usadas para treinar o sistema.";
    }

    /// <summary>
    /// Persists the identified pattern to the knowledge base.
    /// </summary>
    private async Task PersistPatternAsync(PatternRecordResult patternResult, CancellationToken cancellationToken)
    {
        try
        {
            // Convert PatternRecordResult to PatternRecord
            var pattern = new PatternRecord
            {
                PatternDescription = patternResult.PatternDescription,
                Confidence = patternResult.SuccessRate,
                Frequency = 1,
                FirstDetected = DateTime.UtcNow,
                LastDetected = DateTime.UtcNow,
                ExampleSymptoms = patternResult.ExampleSymptoms,
                ExampleSolutions = new List<string> { patternResult.Solution },
                TemporalCharacteristics = patternResult.TemporalInfo,
                PromotedToKnownIssue = patternResult.ReadyForAutomation
            };

            // Read existing patterns
            var existingPatterns = await KnowledgeBasePersistence.ReadDetectedPatternsAsync(cancellationToken);

            // Check if pattern already exists
            var duplicatePattern = existingPatterns.FirstOrDefault(p =>
                p.PatternDescription.Equals(patternResult.PatternDescription, StringComparison.OrdinalIgnoreCase));

            if (duplicatePattern != null)
            {
                // Update existing pattern
                duplicatePattern.Frequency++;
                duplicatePattern.LastDetected = DateTime.UtcNow;

                // Add new examples
                foreach (var symptom in patternResult.ExampleSymptoms)
                {
                    if (!duplicatePattern.ExampleSymptoms.Contains(symptom, StringComparer.OrdinalIgnoreCase))
                    {
                        duplicatePattern.ExampleSymptoms.Add(symptom);
                    }
                }

                foreach (var solution in new[] { patternResult.Solution })
                {
                    if (!duplicatePattern.ExampleSolutions.Contains(solution, StringComparer.OrdinalIgnoreCase))
                    {
                        duplicatePattern.ExampleSolutions.Add(solution);
                    }
                }

                Logger.LogInfo($"Updated existing pattern: {patternResult.PatternDescription} (Frequency: {duplicatePattern.Frequency})");
            }
            else
            {
                // Add new pattern
                existingPatterns.Add(pattern);
                Logger.LogInfo($"Recorded new pattern: {patternResult.PatternDescription}");
            }

            // Check if pattern should be promoted
            if (patternResult.ReadyForAutomation || (existingPatterns.FirstOrDefault(p => 
                p.PatternDescription == patternResult.PatternDescription)?.Frequency >= 3 && 
                patternResult.SuccessRate >= 0.75))
            {
                Logger.LogInfo($"Pattern '{patternResult.PatternDescription}' ready for promotion");
                var patternToPromote = existingPatterns.FirstOrDefault(p =>
                    p.PatternDescription == patternResult.PatternDescription);

                if (patternToPromote != null)
                {
                    await KnowledgeBasePersistence.PromotePatternToKnownIssueAsync(patternToPromote, cancellationToken);
                }
            }

            // Save updated patterns
            await KnowledgeBasePersistence.WriteDetectedPatternsAsync(existingPatterns, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to persist pattern: {ex.Message}");
        }
    }

    /// <summary>
    /// Displays pattern information to the user.
    /// </summary>
    private void DisplayPatternInfo(PatternRecordResult pattern)
    {
        Logger.OutputUser("\n" + new string('=', 80));
        Logger.OutputUser("[SISTEMA] Análise de Padrão Completada");
        Logger.OutputUser(new string('=', 80));
        Logger.OutputUser($"Tipo de Padrão: {pattern.PatternType}");
        Logger.OutputUser($"Descrição: {pattern.PatternDescription}");

        if (!string.IsNullOrEmpty(pattern.TemporalInfo))
        {
            Logger.OutputUser($"Característica Temporal: {pattern.TemporalInfo}");
        }

        Logger.OutputUser($"Taxa de Sucesso: {pattern.SuccessRate:P}");
        Logger.OutputUser($"Pronto para Automação: {(pattern.ReadyForAutomation ? "✓ Sim" : "✗ Não")}");
        Logger.OutputUser(new string('=', 80) + "\n");
    }

    /// <summary>
    /// Creates an empty pattern record result when pattern recording fails.
    /// </summary>
    private static PatternRecordResult CreateEmptyResult()
    {
        return new PatternRecordResult
        {
            PatternType = "Unknown",
            PatternDescription = "Pattern analysis failed",
            Keywords = new List<string>(),
            ExampleSymptoms = new List<string>(),
            Solution = string.Empty,
            SuccessRate = 0,
            ReadyForAutomation = false
        };
    }
}
