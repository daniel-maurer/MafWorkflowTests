using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
namespace SupportWorkflow;

/// <summary>
/// Executor responsible for identifying if a problem is a known issue and providing resolution.
/// Limits iterations to prevent infinite loops.
/// </summary>
internal sealed class FrequentProblemExecutor : Executor<TriageResult>
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
    public override async ValueTask HandleAsync(TriageResult triageResult, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var summary = triageResult.Summary;

        if (string.IsNullOrEmpty(triageResult.Summary))
        {
            throw new InvalidOperationException("No problem summary found in triage result.");
        }

        string currentProblem = summary;
        int iterationCount = 0;

        var history = new List<string> { currentProblem };
        while (iterationCount < Constants.MaxWorkflowIterations)
        {
            iterationCount++;
            string agentInput = $"Problema do Usuário: {string.Join("\n", history)}";
            
            try
            {
                var response = await this._frequentProblemAgent.RunAsync(agentInput, cancellationToken: cancellationToken);
                var frequentProblemResult = JsonSerializer.Deserialize<FrequentProblemResult>(response.Text);
                
                if (frequentProblemResult == null)
                {
                    throw new InvalidOperationException("Failed to deserialize FrequentProblemResult from agent response.");
                }
                
                if (frequentProblemResult.IsKnown || frequentProblemResult.IsComplex)
                {
                    await context.YieldOutputAsync(frequentProblemResult, cancellationToken);
                    return;
                }
                
                string question = frequentProblemResult.MessageForUser;
                if (string.IsNullOrEmpty(question))
                {
                    question = "Por favor, forneça mais detalhes sobre o problema.";
                }
                
                string nextResponse = _consoleInteractor.GetUserResponse(question);
                history.Add(nextResponse);
            }
            catch (Exception ex)
            {
                var errorResult = new FrequentProblemResult
                {
                    IsKnown = false,
                    IsComplex = true,
                    MessageForUser = $"An error occurred during analysis: {ex.Message}"
                };
                await context.YieldOutputAsync(errorResult, cancellationToken);
                throw;
            }
        }
        
        // If max iterations reached, escalate to complex case
        var escalationResult = new FrequentProblemResult
        {
            IsKnown = false,
            IsComplex = true,
            MessageForUser = "The issue requires further investigation. It will be escalated to a specialist."
        };
        await context.YieldOutputAsync(escalationResult, cancellationToken);
    }
}