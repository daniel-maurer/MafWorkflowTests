using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using SalesWorkflow.Models;

namespace SalesWorkflow.Executors;

internal sealed class QuoteExecutor : Executor<SalesAdviceResult, QuoteResult>
{
    private readonly AIAgent _quoteAgent;
    private readonly IUserInteractor _userInteractor;

    public QuoteExecutor(AIAgent quoteAgent, IUserInteractor userInteractor) : base("QuoteExecutor")
    {
        _quoteAgent = quoteAgent;
        _userInteractor = userInteractor;
    }

    public override async ValueTask<QuoteResult> HandleAsync(
        SalesAdviceResult adviceResult,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo("[QuoteExecutor] Gerando proposta comercial e orçamento formal.");

        await _userInteractor.SetAgentTypingAsync("Calculando valores, descontos e gerando proposta comercial...", true, cancellationToken);
        await _userInteractor.PublishAgentStateAsync("quote", "active", "Running", cancellationToken);
        await _userInteractor.PublishContextAsync(
            "generating-quote",
            "Assistente Comercial",
            "Montando proposta comercial e orçamento formal...",
            "quote",
            false,
            cancellationToken);

        var prompt = $@"Produtos a serem orçados:
{JsonSerializer.Serialize(adviceResult.SelectedProducts)}

Complementos opcionais:
{JsonSerializer.Serialize(adviceResult.SuggestedComplements)}

Gere um orçamento formal usando GenerateQuote, calcule condições e descontos.
Responda SEMPRE no esquema JSON de QuoteResult.";

        var response = await _quoteAgent.RunAsync(prompt, cancellationToken: cancellationToken);

        if (!AgentResponseParser.TryDeserializeAgentResponse(response.Text, out QuoteResult? quoteResult) || quoteResult is null)
        {
            Logger.LogWarning("[QuoteExecutor] Falha na desserialização de QuoteResult, montando orçamento padrão.");
            var subtotal = adviceResult.SelectedProducts.Sum(p => p.Price);
            var quoteId = $"QT-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
            quoteResult = new QuoteResult
            {
                QuoteId = quoteId,
                Subtotal = subtotal,
                Discount = 0,
                Total = subtotal,
                Currency = "BRL",
                ValidUntil = DateTimeOffset.UtcNow.AddDays(7),
                PaymentConditions = "À vista no Pix com 5% de desconto ou até 10x sem juros no cartão.",
                MessageForUser = $"Proposta {quoteId} gerada no valor de R$ {subtotal:N2}.",
                Items = adviceResult.SelectedProducts.Select(p => new QuoteItem
                {
                    Sku = p.Sku,
                    Name = p.Name,
                    Quantity = 1,
                    UnitPrice = p.Price,
                    Total = p.Price
                }).ToList()
            };
        }

        await context.QueueStateUpdateAsync(Constants.QuoteIdKey, quoteResult.QuoteId, Constants.SalesStateScope);

        await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);
        await _userInteractor.PublishAgentStateAsync("quote", "done", "Done", cancellationToken);
        await _userInteractor.PublishTraceAsync(
            $"Orçamento {quoteResult.QuoteId} gerado com sucesso: Total R$ {quoteResult.Total:N2} (Válido até {quoteResult.ValidUntil:dd/MM/yyyy})",
            "success",
            cancellationToken);

        var quoteTools = new List<AgentToolCall>
        {
            new AgentToolCall { Name = "GenerateQuote", Args = $"quoteId: {quoteResult.QuoteId}, total: R$ {quoteResult.Total:N2}", Ok = true }
        };

        var formattedMessage = $"{quoteResult.MessageForUser}\n\n" +
            $"📋 **Orçamento {quoteResult.QuoteId}**\n" +
            $"• **Subtotal**: R$ {quoteResult.Subtotal:N2}\n" +
            (quoteResult.Discount > 0 ? $"• **Desconto Aplicado**: -R$ {quoteResult.Discount:N2}\n" : string.Empty) +
            $"• **Total**: **R$ {quoteResult.Total:N2}**\n" +
            $"• **Validade**: até {quoteResult.ValidUntil:dd/MM/yyyy}\n" +
            $"• **Condições**: {quoteResult.PaymentConditions}\n\n" +
            "Deseja receber este orçamento por WhatsApp ou e-mail?";

        await _userInteractor.SendUserResponseAsync(
            formattedMessage,
            "quote",
            tools: quoteTools,
            audience: MessageAudience.Both,
            cancellationToken: cancellationToken);

        await context.YieldOutputAsync(quoteResult, cancellationToken);
        return quoteResult;
    }
}
