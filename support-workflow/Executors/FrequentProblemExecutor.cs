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
        await _userInteractor.PublishTraceAsync("Starting frequent problem analysis", "info", cancellationToken);
        await _userInteractor.SetAgentTypingAsync("Frequent Problem Agent searching", true, cancellationToken);
        
        try
        {
            await _userInteractor.PublishTraceAsync("Searching knowledge base for known issues", "info", cancellationToken);
            var response = await this._frequentProblemAgent.RunAsync(agentInput, cancellationToken: cancellationToken);
            
            if (!AgentResponseParser.TryDeserializeAgentResponse(response.Text, out FrequentProblemResult? frequentProblemResult))
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
                    await _userInteractor.PublishTraceAsync("Matching issue found, loading details", "success", cancellationToken);
                    
                    KnownIssue? matchedIssue = null;
                    var allIssues = await KnowledgeBasePersistence.ReadKnownIssuesAsync(cancellationToken);

                    // 1. Prioritize title/problem match if the agent specified one
                    if (frequentProblemResult.MatchedIssue != null && !string.IsNullOrEmpty(frequentProblemResult.MatchedIssue.Problem))
                    {
                        matchedIssue = allIssues.FirstOrDefault(ki => ki.Problem.Equals(frequentProblemResult.MatchedIssue.Problem, StringComparison.OrdinalIgnoreCase));
                    }

                    // 2. Fallback to ranking issues by keyword/symptom score
                    var rankedIssues = GetRankedIssues(allIssues, summary);
                    if (matchedIssue == null && rankedIssues.Count > 0)
                    {
                        matchedIssue = rankedIssues[0];
                    }

                    if (matchedIssue != null)
                    {
                        frequentProblemResult.MatchedIssue = matchedIssue;
                        frequentProblemResult.RequiredTools = matchedIssue.ToolsRequired ?? new List<string>();
                        frequentProblemResult.SuccessRate = matchedIssue.SuccessRate;

                        Logger.LogExecutorResult($"[Problemas Frequentes] Problema conhecido: {matchedIssue.Problem}");
                        Logger.LogDebug($"Required tools: {string.Join(", ", frequentProblemResult.RequiredTools)}");

                        // Publish matching KB entries sorted by relevance
                        var kbItems = rankedIssues.Select(issue => new KbEntry
                        {
                            Title = issue.Problem,
                            Category = string.Empty,
                            Score = issue.SuccessRate,
                            Summary = issue.Symptoms.FirstOrDefault() ?? issue.Solution ?? string.Empty,
                            ResolutionType = issue.McpAction ?? "knowledge-base",
                            Tags = issue.Keywords?.ToArray() ?? Array.Empty<string>(),
                        }).ToList();

                        // Ensure our selected matchedIssue is at the top/present in the KB display
                        if (kbItems.All(ki => !ki.Title.Equals(matchedIssue.Problem, StringComparison.OrdinalIgnoreCase)))
                        {
                            kbItems.Insert(0, new KbEntry
                            {
                                Title = matchedIssue.Problem,
                                Category = string.Empty,
                                Score = matchedIssue.SuccessRate,
                                Summary = matchedIssue.Symptoms.FirstOrDefault() ?? matchedIssue.Solution ?? string.Empty,
                                ResolutionType = matchedIssue.McpAction ?? "knowledge-base",
                                Tags = matchedIssue.Keywords?.ToArray() ?? Array.Empty<string>(),
                            });
                        }

                        await _userInteractor.PublishKnowledgeBaseAsync(kbItems, cancellationToken);
                    }
                    else
                    {
                        await _userInteractor.PublishTraceAsync("No matching issue found in knowledge base", "warning", cancellationToken);
                        Logger.LogExecutorResult("[Problemas Frequentes] Nenhuma issue conhecida corresponde ao problema identificado. Encaminhando para suporte humano.");

                        frequentProblemResult.IsKnown = false;
                        frequentProblemResult.MessageForUser = "Problema não corresponde a uma issue conhecida. Encaminhando para suporte humano.";
                        frequentProblemResult.RequiredTools = new List<string>();
                        frequentProblemResult.SuccessRate = 0;

                        await _userInteractor.PublishKnowledgeBaseAsync(Array.Empty<KbEntry>(), cancellationToken);
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
            await _userInteractor.PublishTraceAsync("Unknown or complex problem, routing to human support", "info", cancellationToken);
            await context.YieldOutputAsync(frequentProblemResult, cancellationToken);
            return frequentProblemResult;
        }
        catch (Exception ex)
        {
            await _userInteractor.PublishTraceAsync("Error during frequent problem analysis", "error", cancellationToken);
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

    /// <summary>
    /// Ranks known issues by keyword and symptom overlap.
    /// </summary>
    private static List<KnownIssue> GetRankedIssues(List<KnownIssue> allIssues, string summary)
    {
        if (string.IsNullOrEmpty(summary) || allIssues == null || allIssues.Count == 0)
            return new List<KnownIssue>();

        var summaryWords = summary
            .Split(new[] { ' ', ',', '.', ':', ';', '?', '!', '\n', '\t', '-', '_', '/', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim().ToLowerInvariant())
            .Where(w => w.Length > 2)
            .ToList();

        var scoredIssues = new List<(KnownIssue Issue, double Score)>();

        foreach (var issue in allIssues)
        {
            double score = 0;

            // 1. Keyword match: count how many keywords from the issue match the summary words
            if (issue.Keywords != null)
            {
                foreach (var kw in issue.Keywords)
                {
                    var cleanKw = kw.Trim().TrimEnd('.').TrimEnd(',').ToLowerInvariant();
                    if (string.IsNullOrEmpty(cleanKw) || cleanKw.Length <= 2)
                        continue;

                    if (summaryWords.Contains(cleanKw) || (cleanKw.Length > 3 && summary.Contains(cleanKw, StringComparison.OrdinalIgnoreCase)))
                    {
                        // Give higher weight to more specific keywords, lower weight to generic ones
                        if (cleanKw == "creche" || cleanKw == "transporte" || cleanKw == "refeição" || cleanKw == "senha" || cleanKw == "bloqueada" || cleanKw == "bloqueio")
                        {
                            score += 10.0;
                        }
                        else if (cleanKw == "cliente" || cleanKw == "pagamento" || cleanKw == "recebido" || cleanKw == "recebeu" || cleanKw == "atraso" || cleanKw == "vale")
                        {
                            score += 1.0;
                        }
                        else
                        {
                            score += 2.0;
                        }
                    }
                }
            }

            // 2. Symptoms match: check if any of the symptoms have high overlap
            if (issue.Symptoms != null)
            {
                foreach (var symptom in issue.Symptoms)
                {
                    var symptomWords = symptom
                        .Split(new[] { ' ', ',', '.', ':', ';', '?', '!', '\n', '\t', '-', '_', '/' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(w => w.Trim().ToLowerInvariant())
                        .Where(w => w.Length > 2)
                        .ToList();

                    int overlap = symptomWords.Intersect(summaryWords).Count();
                    score += overlap * 1.5;
                }
            }

            if (score > 0)
            {
                scoredIssues.Add((issue, score));
            }
        }

        return scoredIssues
            .OrderByDescending(si => si.Score)
            .Select(si => si.Issue)
            .ToList();
    }

}