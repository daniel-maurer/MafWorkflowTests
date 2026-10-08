using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using SalesWorkflow.Models;
using SalesWorkflow.Services;
using SalesWorkflow.Utilities;

namespace SalesWorkflow.Executors;

internal sealed class QuoteExecutor : Executor<CatalogResult, QuoteResult>
{
    private readonly AIAgent _quoteAgent;
    private readonly AIAgent _highlightsAgent;
    private readonly IUserInteractor _userInteractor;
    private readonly SalesAdminClient _salesAdminClient;

    public QuoteExecutor(
        AIAgent quoteAgent,
        AIAgent highlightsAgent,
        IUserInteractor userInteractor,
        SalesAdminClient salesAdminClient) : base("QuoteExecutor")
    {
        _quoteAgent = quoteAgent;
        _highlightsAgent = highlightsAgent;
        _userInteractor = userInteractor;
        _salesAdminClient = salesAdminClient;
    }

    public override async ValueTask<QuoteResult> HandleAsync(
        CatalogResult catalogResult,
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

        var history = await context.ReadStateAsync<List<ChatMessage>>(Constants.InteractionHistoryKey, Constants.SalesStateScope) ?? [];

        var cart = await context.ReadStateAsync<List<ProductInfo>>(Constants.CartItemsKey, Constants.SalesStateScope) ?? [];
        var allProductsToQuote = (catalogResult.SelectedProducts != null && catalogResult.SelectedProducts.Count > 0)
            ? catalogResult.SelectedProducts
            : (catalogResult.Products != null && catalogResult.Products.Count > 0 ? catalogResult.Products : cart);

        if (allProductsToQuote.Count == 0)
        {
            allProductsToQuote = await context.ReadStateAsync<List<ProductInfo>>(Constants.SelectedProductsKey, Constants.SalesStateScope) ?? [];
        }

        var conditions = await _salesAdminClient.GetPaymentConditionsAsync(cancellationToken);
        var activeCampaigns = await _salesAdminClient.GetActiveCampaignsAsync(cancellationToken);

        var prompt = $@"Gere um orçamento formal para os seguintes produtos confirmados pelo cliente:
{JsonSerializer.Serialize(allProductsToQuote.Select(p => new { p.Sku, p.Name, p.Price }))}

Condições especiais aplicáveis:
- Formas de pagamento ativas: {JsonSerializer.Serialize(conditions.Where(c => c.Active))}
- Campanhas ativas no sistema: {JsonSerializer.Serialize(activeCampaigns.Where(c => c.IsActive))}

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
            var discount = allProductsToQuote.Count >= 2 ? Math.Round(subtotal * 0.10m, 2) : 0m;
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
                if (allProductsToQuote.Count >= 2 && quoteResult.Discount == 0)
                {
                    quoteResult.Discount = Math.Round(quoteResult.Subtotal * 0.10m, 2);
                }
                quoteResult.Total = Math.Max(0, quoteResult.Subtotal - quoteResult.Discount);
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

        var deliveryAnswer = "Retirada";

        if (customer != null && !string.IsNullOrWhiteSpace(customer.Id))
        {
            await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);

            var deliveryMethods = await _salesAdminClient.GetDeliveryMethodsAsync(cancellationToken);
            var deliveryOptions = deliveryMethods.Count > 0 
                ? string.Join(", ", deliveryMethods.Where(m => m.Active).Select(m => $"{m.Name} (R$ {m.Price:N2})")) 
                : "Entrega Padrão (R$ 15,00) ou Retirada na Loja";

            string deliveryQuestion;
            if (customer.Addresses != null && customer.Addresses.Count > 0)
            {
                var defaultAddr = customer.Addresses.FirstOrDefault(a => a.IsDefault) ?? customer.Addresses[0];
                deliveryQuestion = $"Para o envio, temos as opções: {deliveryOptions}.\n\nJá temos seu endereço cadastrado: {defaultAddr.Street}, {defaultAddr.Number} - {defaultAddr.City}/{defaultAddr.State}. Deseja confirmar a entrega neste endereço, informar um novo, ou prefere retirar na loja física?";
            }
            else
            {
                deliveryQuestion = $"Temos as seguintes opções de entrega: {deliveryOptions}.\n\nPara qual opção você tem preferência? (Se for entrega, por favor informe seu endereço completo. Se for retirada, basta confirmar).";
            }

            deliveryAnswer = await _userInteractor.GetUserResponseAsync(
                deliveryQuestion,
                "quote",
                audience: MessageAudience.Both,
                cancellationToken: cancellationToken);

            history.Add(new ChatMessage(ChatRole.Assistant, deliveryQuestion));
            history.Add(new ChatMessage(ChatRole.User, deliveryAnswer));
            await context.QueueStateUpdateAsync(Constants.InteractionHistoryKey, history, Constants.SalesStateScope);

            if (!string.IsNullOrWhiteSpace(deliveryAnswer) &&
                !deliveryAnswer.Trim().Equals("não", StringComparison.OrdinalIgnoreCase) &&
                !deliveryAnswer.Trim().Equals("nao", StringComparison.OrdinalIgnoreCase) &&
                !deliveryAnswer.Trim().Equals("retirar", StringComparison.OrdinalIgnoreCase) &&
                !deliveryAnswer.Trim().Contains("loja", StringComparison.OrdinalIgnoreCase))
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

        var itemsBreakdown = string.Join("\n", allProductsToQuote.Select(p =>
            $"• **{p.Name}** — R$ {p.Price:N2} (Cor: {p.Color ?? "N/A"}, Tam: {p.Size ?? "N/A"})"));

        // Destaque da campanha no resumo do orçamento
        var campaignHighlight = string.Empty;
        if (activeCampaigns.Count > 0)
        {
            var mainCamp = activeCampaigns.FirstOrDefault(c => c.IsActive) ?? activeCampaigns[0];
            campaignHighlight = $"\n🎉 **Campanha Aplicada ({mainCamp.Name})**: {mainCamp.Description}";
        }

        var quoteSummary = $@"📋 **Itens do seu Orçamento ({quoteResult.QuoteId})**:
{itemsBreakdown}

**Resumo:**
Subtotal: R$ {quoteResult.Subtotal:N2}{(quoteResult.Discount > 0 ? $" | Desconto da Campanha: -R$ {quoteResult.Discount:N2}" : "")} | **Total: R$ {quoteResult.Total:N2}**{campaignHighlight}";

        var quoteMsg = $"{quoteSummary}\n\n{quoteResult.MessageForUser}\n\n**Condições de Pagamento:**\n{quoteResult.PaymentConditions}";

        var closingQuestion = $"{quoteMsg}\n\nVocê tem alguma dúvida sobre os itens, valores ou formas de pagamento, ou deseja confirmar o pedido?";

        // Apresenta a proposta e aguarda confirmação ou dúvidas do cliente
        var userClosingReply = await _userInteractor.GetUserResponseAsync(
            closingQuestion,
            "quote",
            tools: quoteTools,
            audience: MessageAudience.Both,
            images: quoteImages.Count > 0 ? quoteImages : null,
            cancellationToken: cancellationToken);

        history.Add(new ChatMessage(ChatRole.Assistant, closingQuestion));
        history.Add(new ChatMessage(ChatRole.User, userClosingReply));
        await context.QueueStateUpdateAsync(Constants.InteractionHistoryKey, history, Constants.SalesStateScope);

        if (!string.IsNullOrWhiteSpace(userClosingReply))
        {
            var lower = userClosingReply.Trim().ToLowerInvariant();
            if (lower.Contains("preço") || lower.Contains("preco") || lower.Contains("cada") || lower.Contains("quanto"))
            {
                var priceAnswer = "Aqui está o preço individual de cada produto do seu pedido:\n\n" +
                    string.Join("\n", allProductsToQuote.Select(p => $"• **{p.Name}** — R$ {p.Price:N2}")) +
                    $"\n\nTotal: R$ {quoteResult.Total:N2}. Fico à disposição se precisar de qualquer outra informação!";

                history.Add(new ChatMessage(ChatRole.Assistant, priceAnswer));
                await context.QueueStateUpdateAsync(Constants.InteractionHistoryKey, history, Constants.SalesStateScope);

                await _userInteractor.SendUserResponseAsync(
                    priceAnswer,
                    "quote",
                    audience: MessageAudience.Both,
                    cancellationToken: cancellationToken);
            }
            else if (!lower.Contains("não") && !lower.Contains("nao") && !lower.Contains("cancela"))
            {
                var confirmMsg = "Perfeito! Seu pedido foi confirmado com sucesso. Agradecemos muito pela preferência e estamos à disposição!";
                history.Add(new ChatMessage(ChatRole.Assistant, confirmMsg));
                await context.QueueStateUpdateAsync(Constants.InteractionHistoryKey, history, Constants.SalesStateScope);

                await _userInteractor.SendUserResponseAsync(
                    confirmMsg,
                    "quote",
                    audience: MessageAudience.Both,
                    cancellationToken: cancellationToken);

                if (customer != null && !string.IsNullOrWhiteSpace(customer.Id))
                {
                    try
                    {
                        var isPickup = deliveryAnswer.Contains("retir", StringComparison.OrdinalIgnoreCase) || 
                                       deliveryAnswer.Contains("loja", StringComparison.OrdinalIgnoreCase);

                        var orderReq = new CreateOrderRequest
                        {
                            CustomerId = Guid.Parse(customer.Id),
                            TotalAmount = quoteResult.Total,
                            Status = "Confirmed",
                            DeliveryMethod = isPickup ? "Retirada na Loja Física" : "Entrega em Domicílio",
                            PaymentMethod = quoteResult.PaymentConditions,
                            Items = quoteResult.Items.Select(i => new CreateOrderItemRequest
                            {
                                ProductId = Guid.Empty, // Auto-resolvido pelo backend pelo Sku/Name
                                Sku = i.Sku,
                                Name = i.Name,
                                Quantity = i.Quantity,
                                UnitPrice = i.UnitPrice,
                                TotalPrice = i.Total
                            }).ToList()
                        };
                        var createdOrder = await _salesAdminClient.CreateOrderAsync(orderReq, cancellationToken);
                        if (createdOrder != null)
                        {
                            await _userInteractor.PublishTraceAsync($"Pedido gravado com sucesso no banco de dados para {customer.Name} (Total: R$ {quoteResult.Total:N2}).", "success", cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"[QuoteExecutor] Falha ao registrar pedido final: {ex.Message}");
                    }
                }

                // Dispara o salvamento dos highlights da conversa e log no Admin imediatamente após confirmação
                try
                {
                    await HighlightsExecutor.SaveHighlightsAndConversationAsync(context, _highlightsAgent, _salesAdminClient, _userInteractor, cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[QuoteExecutor] Falha ao salvar highlights e log de conversa: {ex.Message}");
                }
            }
        }

        await context.YieldOutputAsync(quoteResult, cancellationToken);
        return quoteResult;
    }
}
