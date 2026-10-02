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

    public TriageExecutor(AIAgent triageAgent, IUserInteractor userInteractor) : base("TriageExecutor")
    {
        this._triageAgent = triageAgent ?? throw new ArgumentNullException(nameof(triageAgent));
        this._userInteractor = userInteractor ?? throw new ArgumentNullException(nameof(userInteractor));
    }

    public override async ValueTask<TriageResult> HandleAsync(string userMessage, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var history = await context.ReadStateAsync<List<ChatMessage>>(Constants.ConversationHistoryKey, Constants.TriageStateScope) ?? new List<ChatMessage>();
        history.Add(new ChatMessage(ChatRole.User, userMessage));

        await _userInteractor.PublishTraceAsync("Starting triage analysis", "info", cancellationToken);

        Logger.LogInfo("Starting triage analysis for user message");
        Logger.LogDebug($"User message: {userMessage}");

        const int MaxClarificationAttempts = 2;
        int clarificationAttempts = 0;
        bool isUnderstood = false;

        while (!isUnderstood && clarificationAttempts <= MaxClarificationAttempts)
        {
            await _userInteractor.SetAgentTypingAsync("Triage Agent analyzing", true, cancellationToken);
            await _userInteractor.PublishTraceAsync("Analyzing user message", "info", cancellationToken);
            var response = await this._triageAgent.RunAsync(history, cancellationToken: cancellationToken);

            if (!AgentResponseParser.TryDeserializeAgentResponse(response.Text, out TriageResult? detectionResult))
            {
                clarificationAttempts++;
                Logger.LogWarning("Triage agent produced no valid structured response.");
                if (clarificationAttempts > MaxClarificationAttempts)
                {
                    break;
                }
                continue;
            }

            if (detectionResult.IsUnderstood)
            {
                await _userInteractor.SetAgentTypingAsync("Triage Agent analyzing", false, cancellationToken);
                await _userInteractor.PublishTraceAsync("Problem understood", "success", cancellationToken);
                history.Add(new ChatMessage(ChatRole.Assistant, detectionResult.Summary));

                await context.QueueStateUpdateAsync(Constants.ConversationHistoryKey, history, Constants.TriageStateScope);
                await context.QueueStateUpdateAsync(Constants.ProblemSummaryKey, detectionResult.Summary, Constants.TriageStateScope);

                isUnderstood = true;
                Logger.LogInfo("Triage analysis complete - problem understood");
                Logger.LogExecutorResult($"[Triagem] Resumo do Problema: {detectionResult.Summary}");

                await _userInteractor.SendUserResponseAsync(
                    detectionResult.Summary,
                    "triage",
                    audience: MessageAudience.Attendant,
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

            await _userInteractor.SetAgentTypingAsync("Triage Agent analyzing", false, cancellationToken);
            await _userInteractor.PublishTraceAsync("Requesting additional information", "warning", cancellationToken);
            Logger.LogDebug("Need more information - asking follow-up question");
            history.Add(new ChatMessage(ChatRole.Assistant, detectionResult.QuestionForUser));
            string nextUserMessage = await _userInteractor.GetUserResponseAsync(detectionResult.QuestionForUser, "triage", cancellationToken: cancellationToken);
            await _userInteractor.SetAgentTypingAsync("Triage Agent analyzing", true, cancellationToken);
            history.Add(new ChatMessage(ChatRole.User, nextUserMessage));
            await context.QueueStateUpdateAsync(Constants.ConversationHistoryKey, history, Constants.TriageStateScope);
            clarificationAttempts++;
        }

        Logger.LogWarning("Triage clarification limit reached. Escalating to human support with best-effort summary.");
        await _userInteractor.PublishTraceAsync("Unable to classify issue after multiple clarifications", "warning", cancellationToken);
        var fallbackResult = new TriageResult
        {
            IsUnderstood = true,
            Summary = userMessage,
            Urgency = "unknown"
        };
        await _userInteractor.PublishAgentStateAsync("triage", "done", "Done", cancellationToken);
        await _userInteractor.PublishAgentStateAsync("freq", "active", "Running", cancellationToken);
        await _userInteractor.PublishContextAsync(
            "searching-kb",
            "Searching knowledge base",
            "Searching knowledge base for known solutions.",
            "freq",
            false,
            cancellationToken);
        await context.YieldOutputAsync(fallbackResult, cancellationToken);
        return fallbackResult;
    }
}
