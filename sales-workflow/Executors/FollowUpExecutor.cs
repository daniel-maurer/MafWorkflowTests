using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using SalesWorkflow.Models;

namespace SalesWorkflow.Executors;

internal sealed class FollowUpExecutor : Executor<QuoteResult, FollowUpResult>
{
    private readonly AIAgent _followUpAgent;
    private readonly IUserInteractor _userInteractor;

    public FollowUpExecutor(AIAgent followUpAgent, IUserInteractor userInteractor) : base("FollowUpExecutor")
    {
        _followUpAgent = followUpAgent;
        _userInteractor = userInteractor;
    }

    public override async ValueTask<FollowUpResult> HandleAsync(
        QuoteResult quoteResult,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[FollowUpExecutor] Configurando follow-up para orçamento '{quoteResult.QuoteId}'.");

        await _userInteractor.SetAgentTypingAsync("Programando acompanhamento da sua proposta comercial...", true, cancellationToken);
        await _userInteractor.PublishAgentStateAsync("follow-up", "active", "Running", cancellationToken);
        await _userInteractor.PublishContextAsync(
            "scheduling-followup",
            "Assistente Comercial",
            "Agendando retorno comercial e acompanhamento...",
            "follow-up",
            false,
            cancellationToken);

        var prompt = $@"Orçamento gerado:
ID: {quoteResult.QuoteId}
Total: R$ {quoteResult.Total:N2}
Itens: {JsonSerializer.Serialize(quoteResult.Items)}

Agende um retorno automático via ScheduleFollowUp em 24h para verificar se o cliente tem dúvidas.
Responda SEMPRE no esquema JSON de FollowUpResult.";

        var response = await _followUpAgent.RunAsync(prompt, cancellationToken: cancellationToken);

        if (!AgentResponseParser.TryDeserializeAgentResponse(response.Text, out FollowUpResult? followUpResult) || followUpResult is null)
        {
            Logger.LogWarning("[FollowUpExecutor] Falha na desserialização de FollowUpResult, usando agendamento padrão.");
            followUpResult = new FollowUpResult
            {
                FollowUpScheduled = true,
                ScheduledAt = DateTimeOffset.UtcNow.AddHours(24),
                FollowUpType = "quote_reminder",
                Channel = "whatsapp",
                MessageForUser = "Deixei um lembrete programado para daqui a 24 horas para saber se você conseguiu avaliar nossa proposta. Fico à disposição!"
            };
        }

        await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);
        await _userInteractor.PublishAgentStateAsync("follow-up", "done", "Done", cancellationToken);
        await _userInteractor.PublishTraceAsync(
            $"Follow-up comercial programado: tipo {followUpResult.FollowUpType} via {followUpResult.Channel} em {followUpResult.ScheduledAt:dd/MM HH:mm}.",
            "info",
            cancellationToken);

        var tools = new List<AgentToolCall>
        {
            new AgentToolCall
            {
                Name = "ScheduleFollowUp",
                Args = $"type: {followUpResult.FollowUpType}, channel: {followUpResult.Channel}",
                Ok = followUpResult.FollowUpScheduled
            }
        };

        await _userInteractor.SendUserResponseAsync(
            followUpResult.MessageForUser,
            "follow-up",
            tools: tools,
            audience: MessageAudience.Both,
            cancellationToken: cancellationToken);

        await context.YieldOutputAsync(followUpResult, cancellationToken);
        return followUpResult;
    }
}
