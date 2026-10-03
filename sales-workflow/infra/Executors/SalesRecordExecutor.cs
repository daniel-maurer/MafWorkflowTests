using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using SalesWorkflow.Models;

namespace SalesWorkflow.Executors;

internal sealed class SalesRecordExecutor : Executor<object, SalesRecord>
{
    private readonly AIAgent _salesRecordAgent;
    private readonly IUserInteractor _userInteractor;

    public SalesRecordExecutor(AIAgent salesRecordAgent, IUserInteractor userInteractor) : base("SalesRecordExecutor")
    {
        _salesRecordAgent = salesRecordAgent;
        _userInteractor = userInteractor;
    }

    public override async ValueTask<SalesRecord> HandleAsync(
        object inputData,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[SalesRecordExecutor] Consolidando registro da sessão de vendas. Tipo do input: {inputData?.GetType().Name}");

        await _userInteractor.PublishAgentStateAsync("sales-record", "active", "Running", cancellationToken);

        var prompt = $@"Consolide o registro analítico desta jornada de atendimento comercial.
Dados recebidos da etapa anterior:
{JsonSerializer.Serialize(inputData)}

Determine:
- outcome (converted, quoted, escalated, abandoned, follow_up)
- products_shown (lista de SKUs)
- quote_generated (bool)
- human_involved (bool)
- customer_sentiment (positive, neutral, frustrated, angry)
- recommendations (recomendações comerciais)

Responda SEMPRE no esquema JSON de SalesRecord.";

        var response = await _salesRecordAgent.RunAsync(prompt, cancellationToken: cancellationToken);

        if (!AgentResponseParser.TryDeserializeAgentResponse(response.Text, out SalesRecord? salesRecord) || salesRecord is null)
        {
            salesRecord = new SalesRecord
            {
                InteractionId = $"REC-{Guid.NewGuid():N}"[..12],
                Outcome = inputData is SalesResolutionResult ? "escalated" : "quoted",
                CustomerSentiment = "neutral",
                Recommendations = "Acompanhar retorno do cliente.",
                QuoteGenerated = inputData is QuoteResult || inputData is FollowUpResult
            };
        }

        salesRecord.InteractionId = string.IsNullOrWhiteSpace(salesRecord.InteractionId)
            ? $"REC-{Guid.NewGuid():N}"[..12]
            : salesRecord.InteractionId;

        await _userInteractor.PublishAgentStateAsync("sales-record", "done", "Done", cancellationToken);
        await _userInteractor.PublishContextAsync(
            "resolved",
            "Atendimento Comercial Concluído",
            $"Registro {salesRecord.InteractionId} gerado. Desfecho: {salesRecord.Outcome}.",
            string.Empty,
            false,
            cancellationToken);

        await _userInteractor.PublishTraceAsync(
            $"Métricas da sessão registradas: Desfecho={salesRecord.Outcome} | Sentimento={salesRecord.CustomerSentiment} | Orçamento={salesRecord.QuoteGenerated}",
            "success",
            cancellationToken);

        await context.YieldOutputAsync(salesRecord, cancellationToken);
        return salesRecord;
    }
}
