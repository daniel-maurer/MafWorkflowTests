using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace SupportWorkflow;

/// <summary>
/// Executor responsible for triaging user support requests and understanding the problem.
/// Iteratively gathers information until the problem is fully understood.
/// </summary>
internal sealed class TriageExecutor : Executor<string, TriageResult>
{
    private readonly AIAgent _triageAgent;
    private readonly ConsoleInteractor _consoleInteractor;

    /// <summary>
    /// Initializes a new instance of the TriageExecutor.
    /// </summary>
    /// <param name="triageAgent">The triage agent to use for analysis</param>
    /// <param name="consoleInteractor">The console interactor for user communication</param>
    public TriageExecutor(AIAgent triageAgent, ConsoleInteractor consoleInteractor) : base("TriageExecutor")
    {
        this._triageAgent = triageAgent ?? throw new ArgumentNullException(nameof(triageAgent));
        this._consoleInteractor = consoleInteractor ?? throw new ArgumentNullException(nameof(consoleInteractor));
    }

    /// <summary>
    /// Handles the triage of a user message by analyzing and classifying the support request.
    /// </summary>
    /// <param name="userMessage">The initial user message describing their issue</param>
    /// <param name="context">The workflow context for state management</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A TriageResult containing the analysis</returns>
    public override async ValueTask<TriageResult> HandleAsync(string userMessage, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var history = await context.ReadStateAsync<List<ChatMessage>>(Constants.ConversationHistoryKey, Constants.TriageStateScope) ?? new List<ChatMessage>();
        history.Add(new ChatMessage(ChatRole.User, userMessage));
        
        Logger.LogInfo("Starting triage analysis for user message");
        Logger.LogDebug($"User message: {userMessage}");
        
        bool isUnderstood = false;

        while (!isUnderstood)
        {
            var response = await this._triageAgent.RunAsync(history, cancellationToken: cancellationToken);
            var detectionResult = JsonSerializer.Deserialize<TriageResult>(response.Text);

            if (detectionResult != null && detectionResult.IsUnderstood)
            {
                history.Add(new ChatMessage(ChatRole.Assistant, detectionResult.Summary));
                
                await context.QueueStateUpdateAsync(Constants.ConversationHistoryKey, history, Constants.TriageStateScope);
                await context.QueueStateUpdateAsync(Constants.ProblemSummaryKey, detectionResult.Summary, Constants.TriageStateScope);
                
                isUnderstood = true;
                
                Logger.LogInfo("Triage analysis complete - problem understood");
                Logger.LogDebug($"Problem summary: {detectionResult.Summary}");

                await context.YieldOutputAsync(detectionResult.Summary, cancellationToken);

                return detectionResult;
            }
            else
            {
                if (detectionResult != null)
                {
                    Logger.LogDebug("Need more information - asking follow-up question");
                    history.Add(new ChatMessage(ChatRole.Assistant, detectionResult.QuestionForUser));
                    string nextUserMessage = _consoleInteractor.GetUserResponse(detectionResult.QuestionForUser);
                    history.Add(new ChatMessage(ChatRole.User, nextUserMessage));
                    await context.QueueStateUpdateAsync(Constants.ConversationHistoryKey, history, Constants.TriageStateScope);
                }
            }
        }

        Logger.LogError("Failed to understand problem after multiple triage attempts");
        throw new InvalidOperationException("Failed to understand the problem after multiple attempts.");
    }
}