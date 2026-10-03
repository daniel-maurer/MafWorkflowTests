using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using SalesWorkflow.Models;

namespace SalesWorkflow.Executors;

internal sealed class IntentExecutor : Executor<string, IntentResult>
{
    private readonly AIAgent _intentAgent;
    private readonly IUserInteractor _userInteractor;

    public IntentExecutor(AIAgent intentAgent, IUserInteractor userInteractor) : base("IntentExecutor")
    {
        _intentAgent = intentAgent;
        _userInteractor = userInteractor;
    }

    public override async ValueTask<IntentResult> HandleAsync(
        string userMessage,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[IntentExecutor] Recebida mensagem do usuário: {userMessage}");

        await _userInteractor.SetAgentTypingAsync("Analisando sua solicitação comercial...", true, cancellationToken);
        await _userInteractor.PublishAgentStateAsync("intent", "active", "Running", cancellationToken);
        await _userInteractor.PublishContextAsync(
            "analyzing-intent",
            "Assistente Comercial",
            "Identificando intenção e termos do produto...",
            "intent",
            false,
            cancellationToken);

        var history = await context.ReadStateAsync<List<ChatMessage>>(
            Constants.InteractionHistoryKey,
            Constants.SalesStateScope) ?? [];

        history.Add(new ChatMessage(ChatRole.User, userMessage));

        IntentResult? intentResult = null;
        int attempts = 0;

        while (attempts <= Constants.MaxClarificationAttempts)
        {
            var response = await _intentAgent.RunAsync(history, cancellationToken: cancellationToken);

            if (!AgentResponseParser.TryDeserializeAgentResponse(response.Text, out intentResult) || intentResult is null)
            {
                Logger.LogWarning($"[IntentExecutor] Falha na desserialização de IntentResult. Tentativa {attempts + 1}");
                intentResult = new IntentResult
                {
                    IsUnderstood = true,
                    Intent = "product_search",
                    Summary = userMessage,
                    ExtractedProductQuery = userMessage,
                    RequiresHuman = false
                };
                break;
            }

            if (intentResult.IsUnderstood)
            {
                break;
            }

            attempts++;
            if (attempts > Constants.MaxClarificationAttempts)
            {
                intentResult.IsUnderstood = true;
                break;
            }

            // Precisa de clarificação
            var clarificationPrompt = string.IsNullOrWhiteSpace(intentResult.QuestionForUser)
                ? "Como posso te ajudar com nossos produtos hoje?"
                : intentResult.QuestionForUser;

            await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);
            var userClarification = await _userInteractor.GetUserResponseAsync(
                clarificationPrompt,
                "intent",
                audience: MessageAudience.Both,
                cancellationToken: cancellationToken);

            history.Add(new ChatMessage(ChatRole.Assistant, clarificationPrompt));
            history.Add(new ChatMessage(ChatRole.User, userClarification));
            await _userInteractor.SetAgentTypingAsync("Refinando entendimento...", true, cancellationToken);
        }

        intentResult ??= new IntentResult { IsUnderstood = true, Intent = "product_search", Summary = userMessage };

        await context.QueueStateUpdateAsync(Constants.InteractionHistoryKey, history, Constants.SalesStateScope);
        await context.QueueStateUpdateAsync(Constants.IntentKey, intentResult.Intent, Constants.SalesStateScope);

        await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);
        await _userInteractor.PublishAgentStateAsync("intent", "done", "Done", cancellationToken);
        await _userInteractor.PublishTraceAsync(
            $"Intenção identificada: {intentResult.Intent} (Sentimento: {intentResult.CustomerSentiment})",
            "info",
            cancellationToken);

        // Feedback no painel de atendimento/telemetria
        await _userInteractor.SendUserResponseAsync(
            $"[Triagem Comercial] Intenção: {intentResult.Intent} | Busca: '{intentResult.ExtractedProductQuery}'",
            "intent",
            audience: MessageAudience.Attendant,
            cancellationToken: cancellationToken);

        await context.YieldOutputAsync(intentResult, cancellationToken);
        return intentResult;
    }
}
