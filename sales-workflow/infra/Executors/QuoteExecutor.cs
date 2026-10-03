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

        var cart = await context.ReadStateAsync<List<ProductInfo>>(Constants.CartItemsKey, Constants.SalesStateScope) ?? [];
        var allProductsToQuote = cart.ToList();
        foreach (var p in adviceResult.SelectedProducts)
        {
            if (!allProductsToQuote.Any(existing => string.Equals(existing.Sku, p.Sku, StringComparison.OrdinalIgnoreCase)))
            {
                allProductsToQuote.Add(p);
            }
        }

        if (allProductsToQuote.Count == 0)
        {
            allProductsToQuote = adviceResult.SelectedProducts;
        }

        var skus = allProductsToQuote.Select(p => p.Sku).ToList();
        var kitInfo = !string.IsNullOrWhiteSpace(adviceResult.AcceptedKitName)
            ? $"Pacote/Combo Aceito: {adviceResult.AcceptedKitName} com {adviceResult.DiscountPercent}% de desconto comercial."
            : "Orçamento com os produtos selecionados pelo cliente.";

        var prompt = $@"INFORMAÇÕES DA NEGOCIAÇÃO:
{kitInfo}

PRODUTOS SELECIONADOS PELO CLIENTE (orçar EXATAMENTE estes itens, NÃO inclua nenhum outro item):
{JsonSerializer.Serialize(allProductsToQuote.Select(p => new { p.Sku, p.Name, p.Price }))}

INSTRUÇÕES ESTRITAS:
1. Chame GenerateQuote com a lista de SKUs: {JsonSerializer.Serialize(skus)} e quantidades (1 para cada item).
{(adviceResult.DiscountPercent > 0 ? $"2. O cliente aceitou o pacote promocional! Chame ApplyDiscount informando o quoteId retornado e o percentual {adviceResult.DiscountPercent} de desconto.\n3." : "2.")} Responda SEMPRE no esquema JSON de QuoteResult com o valor final.";

        var response = await _quoteAgent.RunAsync(prompt, cancellationToken: cancellationToken);

        if (!AgentResponseParser.TryDeserializeAgentResponse(response.Text, out QuoteResult? quoteResult) || quoteResult is null)
        {
            Logger.LogWarning("[QuoteExecutor] Falha na desserialização de QuoteResult, montando orçamento padrão.");
            var subtotal = allProductsToQuote.Sum(p => p.Price);
            var discount = adviceResult.DiscountPercent > 0
                ? Math.Round(subtotal * (adviceResult.DiscountPercent / 100m), 2)
                : 0m;
            var total = Math.Max(0, subtotal - discount);
            var quoteId = $"QT-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

            quoteResult = new QuoteResult
            {
                QuoteId = quoteId,
                Subtotal = subtotal,
                Discount = discount,
                Total = total,
                Currency = "BRL",
                ValidUntil = DateTimeOffset.UtcNow.AddDays(7),
                PaymentConditions = $"Pix à vista com 5% adicional (R$ {total * 0.95m:N2}) ou até 10x sem juros no cartão.",
                MessageForUser = $"Orçamento {quoteId} gerado com sucesso! Total: R$ {total:N2} com validade de 7 dias.",
                Items = allProductsToQuote.Select(p => new QuoteItem
                {
                    Sku = p.Sku,
                    Name = p.Name,
                    Quantity = 1,
                    UnitPrice = p.Price,
                    Total = p.Price
                }).ToList()
            };
        }
        else
        {
            // Garante consistência rigorosa dos itens com os produtos aceitos pelo cliente
            var approvedSkus = allProductsToQuote.Select(p => p.Sku).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (quoteResult.Items == null || quoteResult.Items.Count == 0 || quoteResult.Items.Any(i => !approvedSkus.Contains(i.Sku)) || quoteResult.Items.Count != allProductsToQuote.Count)
            {
                Logger.LogInfo("[QuoteExecutor] Ajustando itens do orçamento para refletir fielmente a seleção do cliente.");
                quoteResult.Items = allProductsToQuote.Select(p => new QuoteItem
                {
                    Sku = p.Sku,
                    Name = p.Name,
                    Quantity = 1,
                    UnitPrice = p.Price,
                    Total = p.Price
                }).ToList();
                quoteResult.Subtotal = quoteResult.Items.Sum(i => i.Total);
                if (adviceResult.DiscountPercent > 0)
                {
                    quoteResult.Discount = Math.Round(quoteResult.Subtotal * (adviceResult.DiscountPercent / 100m), 2);
                }
                quoteResult.Total = Math.Max(0, quoteResult.Subtotal - quoteResult.Discount);
                quoteResult.PaymentConditions = $"Pix à vista com 5% adicional (R$ {quoteResult.Total * 0.95m:N2}) ou até 10x sem juros no cartão.";
            }
            else if (adviceResult.DiscountPercent > 0 && quoteResult.Discount == 0)
            {
                var discountAmount = Math.Round(quoteResult.Subtotal * (adviceResult.DiscountPercent / 100m), 2);
                quoteResult.Discount = discountAmount;
                quoteResult.Total = Math.Max(0, quoteResult.Subtotal - discountAmount);
                quoteResult.PaymentConditions = $"Pix à vista com 5% adicional (R$ {quoteResult.Total * 0.95m:N2}) ou até 10x sem juros no cartão.";
            }
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
        if (quoteResult.Discount > 0)
        {
            quoteTools.Add(new AgentToolCall { Name = "ApplyDiscount", Args = $"{adviceResult.DiscountPercent}% off (-R$ {quoteResult.Discount:N2})", Ok = true });
        }

        var kitHeader = !string.IsNullOrWhiteSpace(adviceResult.AcceptedKitName)
            ? $"• **Combo/Kit**: {adviceResult.AcceptedKitName}\n"
            : string.Empty;

        var formattedMessage = $"{quoteResult.MessageForUser}\n\n" +
            $"📋 **Orçamento {quoteResult.QuoteId}**\n" +
            kitHeader +
            $"• **Subtotal**: R$ {quoteResult.Subtotal:N2}\n" +
            (quoteResult.Discount > 0 ? $"• **Desconto Aplicado ({adviceResult.DiscountPercent}%)**: -R$ {quoteResult.Discount:N2}\n" : string.Empty) +
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
