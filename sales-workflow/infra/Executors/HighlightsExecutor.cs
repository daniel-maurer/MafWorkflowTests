using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using SalesWorkflow.Models;
using SalesWorkflow.Services;
using MafWorkflow.Shared.Contracts;

namespace SalesWorkflow.Executors;

internal sealed class HighlightsExecutor : Executor<object, object>
{
    private readonly AIAgent _highlightsAgent;
    private readonly SalesAdminClient _adminClient;
    private readonly IUserInteractor _userInteractor;

    public HighlightsExecutor(AIAgent highlightsAgent, SalesAdminClient adminClient, IUserInteractor userInteractor) : base("HighlightsExecutor")
    {
        _highlightsAgent = highlightsAgent;
        _adminClient = adminClient;
        _userInteractor = userInteractor;
    }

    public override async ValueTask<object> HandleAsync(
        object inputData,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        await SaveHighlightsAndConversationAsync(context, _highlightsAgent, _adminClient, _userInteractor, cancellationToken);
        await context.YieldOutputAsync(inputData, cancellationToken);
        return inputData;
    }

    public static async Task SaveHighlightsAndConversationAsync(
        IWorkflowContext context,
        AIAgent highlightsAgent,
        SalesAdminClient adminClient,
        IUserInteractor userInteractor,
        CancellationToken cancellationToken = default)
    {
        var customerIdStr = await context.ReadStateAsync<string>(Constants.CustomerIdKey, Constants.SalesStateScope);
        var customerData = await context.ReadStateAsync<CustomerInfo>(Constants.CustomerDataKey, Constants.SalesStateScope);
        if (customerData == null && userInteractor is ISalesUserInteractor salesUi)
        {
            customerData = salesUi.CurrentCustomer;
        }
        if (string.IsNullOrWhiteSpace(customerIdStr) && customerData != null)
        {
            customerIdStr = customerData.Id;
        }

        var history = await context.ReadStateAsync<List<ChatMessage>>(Constants.InteractionHistoryKey, Constants.SalesStateScope) ?? [];

        if (string.IsNullOrWhiteSpace(customerIdStr) || history.Count == 0 || customerData == null)
        {
            Logger.LogWarning($"[HighlightsExecutor] Dados insuficientes para salvar highlights (CustomerId: {customerIdStr}, HistoryCount: {history.Count}, HasCustomer: {customerData != null}).");
            return;
        }

        await userInteractor.PublishAgentStateAsync("highlights", "active", "Running", cancellationToken);
        Logger.LogInfo($"[HighlightsExecutor] Extraindo highlights e salvando conversa para cliente {customerData.Name} ({history.Count} mensagens no histórico).");

        var prompt = $@"Analise o histórico completo desta conversa de atendimento comercial e extraia highlights e observações importantes sobre o cliente (ex: ocasiões de uso como praia, casamento ou trabalho; estilo preferido como informal, social ou esportivo; tamanhos como G, M; cores preferidas; se o cliente é brincalhão ou direto; preferências de entrega ou retirada na loja física, etc).
Mantenha os highlights anteriores do cliente se houverem, complementando com as novas descobertas desta sessão.
Seja conciso e prático em tópicos.

Histórico da conversa:
{JsonSerializer.Serialize(history.Where(h => h.Role != ChatRole.System).Select(x => new { Role = x.Role.ToString(), Text = x.Text }))}

Highlights anteriores do cliente:
{customerData.Notes ?? "Nenhum"}

Responda SEMPRE estritamente no esquema JSON de CustomerHighlightsResult contendo apenas a string 'highlights' com o texto formatado.";

        try
        {
            var response = await highlightsAgent.RunAsync(prompt, cancellationToken: cancellationToken);

            if (AgentResponseParser.TryDeserializeAgentResponse(response.Text, out CustomerHighlightsResult? highlightsResult) && highlightsResult is not null)
            {
                customerData.Notes = highlightsResult.Highlights;
                await adminClient.UpdateCustomerAsync(customerIdStr, new UpdateCustomerRequest
                {
                    Name = customerData.Name,
                    Email = customerData.Email,
                    Phone = customerData.Phone,
                    DocumentNumber = customerData.DocumentNumber,
                    DocumentType = customerData.DocumentType,
                    CustomerType = customerData.CustomerType,
                    Notes = highlightsResult.Highlights,
                    Active = true
                }, cancellationToken);

                await context.QueueStateUpdateAsync(Constants.CustomerDataKey, customerData, Constants.SalesStateScope);
                await userInteractor.PublishTraceAsync($"Observações/Highlights salvos no perfil de {customerData.Name}: {highlightsResult.Highlights}", "success", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[HighlightsExecutor] Falha ao extrair highlights com a IA: {ex.Message}");
        }

        try
        {
            var actualSessionId = (userInteractor as ISalesUserInteractor)?.SessionId;
            var conversationReq = new CreateConversationRequest
            {
                CustomerId = Guid.TryParse(customerIdStr, out var cid) ? cid : null,
                SessionId = !string.IsNullOrWhiteSpace(actualSessionId) ? actualSessionId : Guid.NewGuid().ToString("N"),
                Status = "Closed",
                Messages = history.Where(h => h.Role != ChatRole.System && !string.IsNullOrWhiteSpace(h.Text)).Select(h => new CreateConversationMessageRequest
                {
                    Role = h.Role == ChatRole.User ? "user" : "assistant",
                    Content = h.Text ?? ""
                }).ToList()
            };
            var savedConv = await adminClient.SaveConversationAsync(conversationReq, cancellationToken);
            if (savedConv != null)
            {
                await userInteractor.PublishTraceAsync($"Histórico completo da conversa ({conversationReq.Messages.Count} msgs) salvo no painel Admin.", "success", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[HighlightsExecutor] Falha ao salvar log da conversa no Admin: {ex.Message}");
        }

        await userInteractor.PublishAgentStateAsync("highlights", "done", "Done", cancellationToken);
    }
}
