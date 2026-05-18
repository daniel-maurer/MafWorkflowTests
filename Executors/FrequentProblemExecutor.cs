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
    private readonly IUserInteractor _userInteractor;
    private readonly string _knownIssuesPath;

    /// <summary>
    /// Initializes a new instance of the FrequentProblemExecutor.
    /// </summary>
    /// <param name="frequentProblemAgent">The agent for detecting frequent/known problems</param>
    /// <param name="consoleInteractor">The console interactor for user communication</param>
    /// <param name="knownIssuesPath">Optional path to the known issues file</param>
    public FrequentProblemExecutor(AIAgent frequentProblemAgent, IUserInteractor userInteractor, string knownIssuesPath = "know_issues.json") : base("FrequentProblemExecutor")
    {
        this._frequentProblemAgent = frequentProblemAgent ?? throw new ArgumentNullException(nameof(frequentProblemAgent));
        this._userInteractor = userInteractor ?? throw new ArgumentNullException(nameof(userInteractor));
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

            await _userInteractor.PublishAgentStateAsync("freq", "active", "Running", cancellationToken);
            await _userInteractor.PublishContextAsync(
                "searching-kb",
                "Knowledge base search",
                "Searching knowledge base for known issues.",
                "freq",
                false,
                cancellationToken);

        if (string.IsNullOrEmpty(triageResult.Summary))
        {
            throw new InvalidOperationException("No problem summary found in triage result.");
        }

        string currentProblem = summary;

        var history = new List<string> { currentProblem };
        string agentInput = $"Problema do Usuário: {string.Join("\n", history)}";
        await _userInteractor.PublishTraceAsync("Starting frequent problem analysis", TraceConstants.IconFileSearch, TraceConstants.ColorPrimary, cancellationToken);
        await _userInteractor.SetAgentTypingAsync("Frequent Problem Agent searching", true, cancellationToken);
        
        try
        {
            await _userInteractor.PublishTraceAsync("Searching knowledge base for known issues", TraceConstants.IconDatabase, TraceConstants.ColorPrimary, cancellationToken);
            var response = await this._frequentProblemAgent.RunAsync(agentInput, cancellationToken: cancellationToken);
            var frequentProblemResult = JsonSerializer.Deserialize<FrequentProblemResult>(response.Text);
            
            if (frequentProblemResult == null)
            {
                throw new InvalidOperationException("Failed to deserialize FrequentProblemResult from agent response.");
            }
            
            Logger.LogDebug($"Frequent Problem Analysis Result - IsKnown: {frequentProblemResult.IsKnown}");
            
            // Always return the result - let the workflow routing decide what to do next
            // If it's known and not complex, route to resolution; otherwise route to human support
            if (frequentProblemResult.IsKnown)
            {
                    await _userInteractor.PublishAgentStateAsync("freq", "done", "Done", cancellationToken);
                    await _userInteractor.PublishAgentStateAsync("resolution", "active", "Running", cancellationToken);
                    await _userInteractor.PublishContextAsync(
                        "resolving",
                        "Automated resolution",
                        "Attempting automated resolution.",
                        "resolution",
                        false,
                        cancellationToken);
                // If known, try to load the full issue details from the knowledge base
                if (string.IsNullOrEmpty(frequentProblemResult.MessageForUser) == false)
                {
                    await _userInteractor.PublishTraceAsync("Matching issue found, loading details", TraceConstants.IconTag, TraceConstants.ColorSuccess, cancellationToken);
                    var searchKeywords = ExtractKeywords(summary);
                    var matchedIssues = await FrequentProblemTools.GetKnownIssuesAsync(searchKeywords, cancellationToken);
                    
                    if (matchedIssues.Count > 0)
                    {
                        frequentProblemResult.MatchedIssue = matchedIssues[0];
                        frequentProblemResult.RequiredTools = matchedIssues[0].ToolsRequired ?? new List<string>();
                        frequentProblemResult.SuccessRate = matchedIssues[0].SuccessRate;
                        
                        Logger.LogExecutorResult($"[Problemas Frequentes] Problema conhecido: {matchedIssues[0].Problem}");
                        Logger.LogDebug($"Required tools: {string.Join(", ", frequentProblemResult.RequiredTools)}");
                    }
                    else
                    {
                        await _userInteractor.PublishTraceAsync("No matching issue found in knowledge base", TraceConstants.IconSiren, TraceConstants.ColorWarning, cancellationToken);
                        Logger.LogExecutorResult("[Problemas Frequentes] Nenhuma issue conhecida corresponde ao problema identificado. Encaminhando para suporte humano.");

                        frequentProblemResult.IsKnown = false;
                        frequentProblemResult.MessageForUser = "Problema não corresponde a uma issue conhecida. Encaminhando para suporte humano.";
                        frequentProblemResult.RequiredTools = new List<string>();
                        frequentProblemResult.SuccessRate = 0;
                    }
                }
                
                await context.YieldOutputAsync(frequentProblemResult, cancellationToken);
                return frequentProblemResult;
            }
            
            // For unknown or complex problems, return the result to route to human support
                await _userInteractor.PublishAgentStateAsync("freq", "done", "Done", cancellationToken);
                await _userInteractor.PublishAgentStateAsync("human-support", "active", "Waiting", cancellationToken);
                await _userInteractor.PublishContextAsync(
                    "human-chat",
                    "Human handoff",
                    "Escalating to human support.",
                    "human-support",
                    true,
                    cancellationToken);
                await _userInteractor.PublishSplitModeAsync(true, cancellationToken);
            await _userInteractor.PublishTraceAsync("Unknown or complex problem, routing to human support", TraceConstants.IconUserCheck, TraceConstants.ColorPrimary, cancellationToken);
            await context.YieldOutputAsync(frequentProblemResult, cancellationToken);
            return frequentProblemResult;
        }
        catch (Exception ex)
        {
            await _userInteractor.PublishTraceAsync("Error during frequent problem analysis", TraceConstants.IconSiren, TraceConstants.ColorError, cancellationToken);
            Logger.LogError($"Frequent Problem Executor exception: {ex.GetType().Name} - {ex.Message}");
            
            var errorResult = new FrequentProblemResult
            {
                IsKnown = false,
                MessageForUser = "Ocorreu um erro durante a análise do problema. Por favor, tente novamente ou contacte o suporte."
            };
            await context.YieldOutputAsync(errorResult, cancellationToken);
            throw;
        }
        finally
        {
            await _userInteractor.SetAgentTypingAsync("Frequent Problem Agent searching", false, cancellationToken);
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