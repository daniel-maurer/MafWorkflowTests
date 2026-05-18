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
    private readonly IUserInteractor _userInteractor;

    /// <summary>
    /// Initializes a new instance of the TriageExecutor.
    /// </summary>
    /// <param name="triageAgent">The triage agent to use for analysis</param>
    /// <param name="consoleInteractor">The console interactor for user communication</param>
    public TriageExecutor(AIAgent triageAgent, IUserInteractor userInteractor) : base("TriageExecutor")
    {
        this._triageAgent = triageAgent ?? throw new ArgumentNullException(nameof(triageAgent));
        this._userInteractor = userInteractor ?? throw new ArgumentNullException(nameof(userInteractor));
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
        
        await _userInteractor.PublishTraceAsync("Starting triage analysis", TraceConstants.IconTerminal, TraceConstants.ColorPrimary, cancellationToken);
        
        Logger.LogInfo("Starting triage analysis for user message");
        Logger.LogDebug($"User message: {userMessage}");
        
        bool isUnderstood = false;

        while (!isUnderstood)
        {
            await _userInteractor.SetAgentTypingAsync("Triage Agent analyzing", true, cancellationToken);
            await _userInteractor.PublishTraceAsync("Analyzing user message", TraceConstants.IconGitBranch, TraceConstants.ColorPrimary, cancellationToken);
            var response = await this._triageAgent.RunAsync(history, cancellationToken: cancellationToken);
            var detectionResult = JsonSerializer.Deserialize<TriageResult>(response.Text);

            if (detectionResult != null && detectionResult.IsUnderstood)
            {
                await _userInteractor.SetAgentTypingAsync("Triage Agent analyzing", false, cancellationToken);
                await _userInteractor.PublishTraceAsync("Problem understood", TraceConstants.IconUserCheck, TraceConstants.ColorSuccess, cancellationToken);
                history.Add(new ChatMessage(ChatRole.Assistant, detectionResult.Summary));
                
                await context.QueueStateUpdateAsync(Constants.ConversationHistoryKey, history, Constants.TriageStateScope);
                await context.QueueStateUpdateAsync(Constants.ProblemSummaryKey, detectionResult.Summary, Constants.TriageStateScope);
                
                isUnderstood = true;
                
                Logger.LogInfo("Triage analysis complete - problem understood");
                Logger.LogExecutorResult($"[Triagem] Resumo do Problema: {detectionResult.Summary}");

                // Surface the triage classification as a Triage Agent chat bubble so the user sees
                // it under the correct identity/icon/color (instead of a generic "MAF Agent" row).
                await _userInteractor.SendUserResponseAsync(
                    detectionResult.Summary,
                    BffWorkflowClient.AgentRegistry["triage"],
                    cancellationToken: cancellationToken);

                await _userInteractor.PublishAgentStateAsync("triage", "done", "Done", cancellationToken);
                await _userInteractor.PublishAgentStateAsync("freq", "active", "Running", cancellationToken);
                await _userInteractor.PublishContextAsync(
                    "searching-kb",
                    "Searching knowledge base",
                    "Searching knowledge base for known solutions.",
                    "freq",
                    false,
                    cancellationToken);

                await context.YieldOutputAsync(detectionResult.Summary, cancellationToken);

                return detectionResult;
            }
            else
            {
                if (detectionResult != null)
                {
                    await _userInteractor.SetAgentTypingAsync("Triage Agent analyzing", false, cancellationToken);
                    await _userInteractor.PublishTraceAsync("Requesting additional information", TraceConstants.IconFileSearch, TraceConstants.ColorWarning, cancellationToken);
                    Logger.LogDebug("Need more information - asking follow-up question");
                    history.Add(new ChatMessage(ChatRole.Assistant, detectionResult.QuestionForUser));
                    string nextUserMessage = await _userInteractor.GetUserResponseAsync(detectionResult.QuestionForUser, BffWorkflowClient.AgentRegistry["triage"], cancellationToken: cancellationToken);
                    await _userInteractor.SetAgentTypingAsync("Triage Agent analyzing", true, cancellationToken);
                    history.Add(new ChatMessage(ChatRole.User, nextUserMessage));
                    await context.QueueStateUpdateAsync(Constants.ConversationHistoryKey, history, Constants.TriageStateScope);
                }
            }
        }

        await _userInteractor.PublishTraceAsync("Triage analysis failed - multiple attempts exhausted", TraceConstants.IconSiren, TraceConstants.ColorError, cancellationToken);
        Logger.LogError("Failed to understand problem after multiple triage attempts");
        throw new InvalidOperationException("Failed to understand the problem after multiple attempts.");
    }
}