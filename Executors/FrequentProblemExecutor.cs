using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace SupportWorkflow;

/// <summary>
/// Executor responsible for identifying if a problem is a known issue and providing resolution.
/// Limits iterations to prevent infinite loops.
/// </summary>
internal sealed class FrequentProblemExecutor : Executor<TriageResult, FrequentProblemResult>
{
    private readonly AIAgent _frequentProblemAgent;
    private readonly ConsoleInteractor _consoleInteractor;
    private readonly string _knownIssuesPath;

    /// <summary>
    /// Initializes a new instance of the FrequentProblemExecutor.
    /// </summary>
    /// <param name="frequentProblemAgent">The agent for detecting frequent/known problems</param>
    /// <param name="consoleInteractor">The console interactor for user communication</param>
    /// <param name="knownIssuesPath">Optional path to the known issues file</param>
    public FrequentProblemExecutor(AIAgent frequentProblemAgent, ConsoleInteractor consoleInteractor, string knownIssuesPath = "know_issues.json") : base("FrequentProblemExecutor")
    {
        this._frequentProblemAgent = frequentProblemAgent ?? throw new ArgumentNullException(nameof(frequentProblemAgent));
        this._consoleInteractor = consoleInteractor ?? throw new ArgumentNullException(nameof(consoleInteractor));
        _knownIssuesPath = knownIssuesPath;
    }

    /// <summary>
    /// Handles the analysis of a triage result against known issues.
    /// </summary>
    /// <param name="triageResult">The result from the triage executor</param>
    /// <param name="context">The workflow context for state management</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A FrequentProblemResult containing the analysis</returns>
    public override async ValueTask<FrequentProblemResult> HandleAsync(TriageResult triageResult, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var summary = triageResult.Summary;

        if (string.IsNullOrEmpty(triageResult.Summary))
        {
            throw new InvalidOperationException("No problem summary found in triage result.");
        }

        string currentProblem = summary;

        var history = new List<string> { currentProblem };
        string agentInput = $"Problema do Usuário: {string.Join("\n", history)}";
        
        try
        {
            // First, check if this matches a promoted pattern (high-confidence auto-resolution)
            var keywords = ExtractKeywords(summary);
            var promotedIssues = await FrequentProblemTools.GetPromotedPatternsAsync(keywords, cancellationToken);
            
            if (promotedIssues.Count > 0)
            {
                Logger.LogInfo($"Found promoted pattern for auto-resolution: {promotedIssues[0].Problem}");
                var promotedResult = new FrequentProblemResult
                {
                    IsKnown = true,
                    IsComplex = false,
                    MatchedIssue = promotedIssues[0],
                    RequiredTools = promotedIssues[0].ToolsRequired ?? new List<string>(),
                    SuccessRate = promotedIssues[0].SuccessRate,
                    MessageForUser = $"✓ Encontrada solução automática para: {promotedIssues[0].Problem}"
                };
                await context.YieldOutputAsync(promotedResult, cancellationToken);
                return promotedResult;
            }
            
            var response = await this._frequentProblemAgent.RunAsync(agentInput, cancellationToken: cancellationToken);
            var frequentProblemResult = JsonSerializer.Deserialize<FrequentProblemResult>(response.Text);
            
            if (frequentProblemResult == null)
            {
                throw new InvalidOperationException("Failed to deserialize FrequentProblemResult from agent response.");
            }
            
            Logger.LogDebug($"Frequent Problem Analysis Result - IsKnown: {frequentProblemResult.IsKnown}, IsComplex: {frequentProblemResult.IsComplex}");
            
            // Always return the result - let the workflow routing decide what to do next
            // If it's known and not complex, route to resolution; otherwise route to human support
            if (frequentProblemResult.IsKnown && !frequentProblemResult.IsComplex)
            {
                // If known, try to load the full issue details from the knowledge base
                if (string.IsNullOrEmpty(frequentProblemResult.MessageForUser) == false)
                {
                    var searchKeywords = ExtractKeywords(summary);
                    var matchedIssues = await FrequentProblemTools.GetKnownIssuesAsync(searchKeywords, cancellationToken);
                    
                    if (matchedIssues.Count > 0)
                    {
                        frequentProblemResult.MatchedIssue = matchedIssues[0];
                        frequentProblemResult.RequiredTools = matchedIssues[0].ToolsRequired ?? new List<string>();
                        frequentProblemResult.SuccessRate = matchedIssues[0].SuccessRate;
                        
                        Logger.LogInfo($"Matched issue: {matchedIssues[0].Problem}");
                        Logger.LogDebug($"Required tools: {string.Join(", ", frequentProblemResult.RequiredTools)}");
                    }
                }
                
                await context.YieldOutputAsync(frequentProblemResult, cancellationToken);
                return frequentProblemResult;
            }
            
            // For unknown or complex problems, return the result to route to human support
            await context.YieldOutputAsync(frequentProblemResult, cancellationToken);
            return frequentProblemResult;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Frequent Problem Executor exception: {ex.GetType().Name} - {ex.Message}");
            
            var errorResult = new FrequentProblemResult
            {
                IsKnown = false,
                IsComplex = true,
                MessageForUser = "Ocorreu um erro durante a análise do problema. Por favor, tente novamente ou contacte o suporte."
            };
            await context.YieldOutputAsync(errorResult, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Extracts keywords from text by splitting on common delimiters.
    /// </summary>
    private static List<string> ExtractKeywords(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new List<string>();

        return text
            .Split(new[] { ' ', ',', '.', ':', ';', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length > 2) // Filter out very short words
            .Take(10) // Limit to first 10 words
            .ToList();
    }
}