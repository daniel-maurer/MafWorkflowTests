using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using SalesWorkflow.Models;
using SalesWorkflow.Services;
using SalesWorkflow.Utilities;

namespace SalesWorkflow.Executors;

internal sealed class QuoteExecutor : Executor<SalesAdviceResult, QuoteResult>
{
    private readonly AIAgent _quoteAgent;
    private readonly IUserInteractor _userInteractor;
    private readonly SalesAdminClient _salesAdminClient;

    public QuoteExecutor(
        AIAgent quoteAgent,
        IUserInteractor userInteractor,
        SalesAdminClient salesAdminClient) : base("QuoteExecutor")
    {
        _quoteAgent = quoteAgent;
        _userInteractor = userInteractor;
        _salesAdminClient = salesAdminClient;
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
            "Gerando proposta comercial...",
            "quote",
            false,
            cancellationToken);

        var cart = await context.ReadStateAsync<List<ProductInfo>>(Constants.CartItemsKey, Constants.SalesStateScope) ?? [];
        var allProductsToQuote = (adviceResult.SelectedProducts != null && adviceResult.SelectedProducts.Count > 0)
            ? adviceResult.SelectedProducts
            : cart;

        if (allProductsToQuote.Count == 0)
        {
            allProductsToQuote = await context.ReadStateAsync<List<ProductInfo>>(Constants.SelectedProductsKey, Constants.SalesStateScope) ?? [];
        }

        var conditions = await _salesAdminClient.GetPaymentConditionsAsync(cancellationToken);

        var prompt = $@"Gere um orçamento formal para os seguintes produtos confirmados pelo cliente:
{JsonSerializer.Serialize(allProductsToQuote.Select(p => new { p.Sku, p.Name, p.Price }))}

Condições especiais aplicáveis:
- Desconto especial acordado: {adviceResult.DiscountPercent}%
- Nome do combo/kit: {(string.IsNullOrWhiteSpace(adviceResult.AcceptedKitName) ? "Itens avulsos selecionados" : adviceResult.AcceptedKitName)}
- Formas de pagamento ativas: {JsonSerializer.Serialize(conditions.Where(c => c.Active))}

INSTRUÇÕES:
1. Chame GenerateQuote com a lista de SKUs e quantidades.
2. Não invente regras de pagamento — utilize as opções retornadas pelo sistema.
3. Conclua imediatamente gerando o JSON de QuoteResult.
Responda SEMPRE estritamente no esquema JSON de QuoteResult.";

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
                PaymentConditions = PaymentConditionFormatter.Format(conditions, total),
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
                quoteResult.PaymentConditions = PaymentConditionFormatter.Format(conditions, quoteResult.Total);
            }
            else if (adviceResult.DiscountPercent > 0 && quoteResult.Discount == 0)
            {
                var discountAmount = Math.Round(quoteResult.Subtotal * (adviceResult.DiscountPercent / 100m), 2);
                quoteResult.Discount = discountAmount;
                quoteResult.Total = Math.Max(0, quoteResult.Subtotal - discountAmount);
                quoteResult.PaymentConditions = PaymentConditionFormatter.Format(conditions, quoteResult.Total);
            }
            else if (string.IsNullOrWhiteSpace(quoteResult.PaymentConditions) || quoteResult.PaymentConditions.Contains("5% adicional"))
            {
                quoteResult.PaymentConditions = PaymentConditionFormatter.Format(conditions, quoteResult.Total);
            }
        }

        await context.QueueStateUpdateAsync(Constants.QuoteIdKey, quoteResult.QuoteId, Constants.SalesStateScope);

        // ── Consulta de Entrega e Cadastro Progressivo de Endereço ──
        CustomerInfo? customer = null;
        if (_userInteractor is ISalesUserInteractor salesUi)
        {
            customer = salesUi.CurrentCustomer;
        }
        if (customer == null)
        {
            customer = await context.ReadStateAsync<CustomerInfo>(Constants.CustomerDataKey, Constants.SalesStateScope);
        }

        if (customer != null && !string.IsNullOrWhiteSpace(customer.Id))
        {
            await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);

            string deliveryQuestion;
            if (customer.Addresses != null && customer.Addresses.Count > 0)
            {
                var defaultAddr = customer.Addresses.FirstOrDefault(a => a.IsDefault) ?? customer.Addresses[0];
                deliveryQuestion = $"Gostaria que a gente enviasse o pedido para entrega? Já temos seu endereço cadastrado: {defaultAddr.Street}, {defaultAddr.Number} - {defaultAddr.City}/{defaultAddr.State}. Deseja confirmar para este endereço ou cadastrar outro? (Responda 'sim' para confirmar, informe um novo endereço, ou 'não' para retirar na loja).";
            }
            else
            {
                deliveryQuestion = "Você gostaria que a gente enviasse o seu pedido para entrega? (Se sim, por favor informe seu endereço completo: Rua, Número, Bairro, Cidade e CEP. Se preferir retirar na loja física, basta responder 'não').";
            }

            var deliveryAnswer = await _userInteractor.GetUserResponseAsync(
                deliveryQuestion,
                "quote",
                audience: MessageAudience.Both,
                cancellationToken: cancellationToken);

            if (!string.IsNullOrWhiteSpace(deliveryAnswer) &&
                !deliveryAnswer.Trim().Equals("não", StringComparison.OrdinalIgnoreCase) &&
                !deliveryAnswer.Trim().Equals("nao", StringComparison.OrdinalIgnoreCase) &&
                !deliveryAnswer.Trim().Equals("retirar", StringComparison.OrdinalIgnoreCase) &&
                !deliveryAnswer.Trim().Equals("loja", StringComparison.OrdinalIgnoreCase))
            {
                var cleanAns = deliveryAnswer.Trim().ToLowerInvariant();
                var isConfirmation = cleanAns is "sim" or "quero" or "pode enviar" or "confirmo" or "isso" or "ok";

                if (isConfirmation && customer.Addresses != null && customer.Addresses.Count > 0)
                {
                    var defaultAddr = customer.Addresses.FirstOrDefault(a => a.IsDefault) ?? customer.Addresses[0];
                    await _userInteractor.PublishTraceAsync($"Entrega confirmada no endereço existente do cliente: {defaultAddr.Street}, {defaultAddr.Number}", "success", cancellationToken);
                    quoteResult.MessageForUser += $"\n\n📦 **Entrega**: Pedido agendado para envio em: {defaultAddr.Street}, {defaultAddr.Number} - {defaultAddr.City}/{defaultAddr.State}.";
                }
                else
                {
                    var parsed = AddressParser.Parse(deliveryAnswer);
                    var saved = await _salesAdminClient.AddCustomerAddressAsync(customer.Id, parsed, cancellationToken);
                    if (saved != null)
                    {
                        customer.Addresses ??= [];
                        customer.Addresses.Add(saved);
                        if (_userInteractor is ISalesUserInteractor si) si.CurrentCustomer = customer;
                        await context.QueueStateUpdateAsync(Constants.CustomerDataKey, customer, Constants.SalesStateScope);
                        await _userInteractor.PublishTraceAsync($"Novo endereço cadastrado no perfil do cliente: {saved.Street}, {saved.Number} - {saved.City}/{saved.State}", "success", cancellationToken);
                        quoteResult.MessageForUser += $"\n\n📦 **Entrega cadastrada**: {saved.Street}, {saved.Number}, {saved.Neighborhood} - {saved.City}/{saved.State}, CEP {saved.ZipCode}.";
                    }
                }
            }
            else
            {
                await _userInteractor.PublishTraceAsync("Cliente optou por retirada em loja física.", "info", cancellationToken);
                quoteResult.MessageForUser += "\n\n🏬 **Retirada**: O pedido estará disponível para retirada em nossa loja física após confirmação.";
            }
        }

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
            quoteTools.Add(new AgentToolCall { Name = "ApplyDiscount", Args = $"desconto: R$ {quoteResult.Discount:N2}", Ok = true });
        }

        var quoteImages = allProductsToQuote
            .Where(p => !string.IsNullOrWhiteSpace(p.ImageUrl))
            .Select(p => new MafImagePayload { Url = p.ImageUrl, Alt = p.Name, Sku = p.Sku })
            .ToList();

        var quoteMsg = $"{quoteResult.MessageForUser}\n\n**Condições de Pagamento:**\n{quoteResult.PaymentConditions}";

        await _userInteractor.SendUserResponseAsync(
            quoteMsg,
            "quote",
            tools: quoteTools,
            audience: MessageAudience.Both,
            images: quoteImages.Count > 0 ? quoteImages : null,
            cancellationToken: cancellationToken);

        await context.YieldOutputAsync(quoteResult, cancellationToken);
        return quoteResult;
    }
}
