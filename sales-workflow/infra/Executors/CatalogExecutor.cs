using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using SalesWorkflow.AiTools;
using SalesWorkflow.Models;
using SalesWorkflow.Services;

namespace SalesWorkflow.Executors;

internal sealed class CatalogExecutor : Executor<IntentResult, CatalogResult>
{
    private readonly AIAgent _catalogAgent;
    private readonly AIAgent _decisionAgent;
    private readonly IUserInteractor _userInteractor;
    private readonly CatalogTools _catalogTools;
    private readonly SalesAdminClient _salesAdminClient;

    public CatalogExecutor(
        AIAgent catalogAgent,
        AIAgent decisionAgent,
        IUserInteractor userInteractor,
        CatalogTools catalogTools,
        SalesAdminClient salesAdminClient) : base("CatalogExecutor")
    {
        _catalogAgent = catalogAgent;
        _decisionAgent = decisionAgent;
        _userInteractor = userInteractor;
        _catalogTools = catalogTools;
        _salesAdminClient = salesAdminClient;
    }

    public override async ValueTask<CatalogResult> HandleAsync(
        IntentResult intentResult,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[CatalogExecutor] Processando intenção '{intentResult.Intent}' com query '{intentResult.ExtractedProductQuery}'");

        await _userInteractor.SetAgentTypingAsync("Consultando catálogo e disponibilidade em estoque via RAG...", true, cancellationToken);
        await _userInteractor.PublishAgentStateAsync("catalog", "active", "Running", cancellationToken);
        await _userInteractor.PublishContextAsync(
            "searching-catalog",
            "Assistente Comercial",
            "Consultando catálogo de produtos...",
            "catalog",
            false,
            cancellationToken);

        // Se for intenção direta de reclamação ou negociação avançada, já pode requerer humano
        if (intentResult.RequiresHuman)
        {
            await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);
            await _userInteractor.PublishAgentStateAsync("catalog", "done", "Done", cancellationToken);

            var directHandoff = new CatalogResult
            {
                HasResults = false,
                RequiresHuman = true,
                OriginalIntent = intentResult.Intent,
                MessageForUser = "Identificamos que seu caso envolve uma condição especial ou atendimento personalizado. Vou transferir para nossa equipe comercial agora mesmo."
            };

            await context.YieldOutputAsync(directHandoff, cancellationToken);
            return directHandoff;
        }

        _catalogTools.ResetSearchCounter();

        var prompt = $@"Intenção: '{intentResult.Intent}'
Contexto do cliente: '{intentResult.Summary}'
Termo de busca: '{intentResult.ExtractedProductQuery}'
Filtros: {JsonSerializer.Serialize(intentResult.Filters ?? new ProductFilters())}";

        var response = await _catalogAgent.RunAsync(prompt, cancellationToken: cancellationToken);

        if (!AgentResponseParser.TryDeserializeAgentResponse(response.Text, out CatalogResult? catalogResult) || catalogResult is null)
        {
            Logger.LogWarning("[CatalogExecutor] Falha na desserialização de CatalogResult, utilizando fallback de busca.");
            catalogResult = new CatalogResult
            {
                HasResults = false,
                RequiresHuman = true,
                OriginalIntent = intentResult.Intent,
                MessageForUser = "Não encontrei o item especificado no momento. Gostaria de falar com um de nossos consultores?"
            };
        }

        // Resgate inteligente: Se o agente marcou que não encontrou mas a ferramenta retornou produtos com estoque
        if ((!catalogResult.HasResults || catalogResult.Products == null || catalogResult.Products.Count == 0) && _catalogTools.LastFoundProducts.Count > 0)
        {
            var inStockProducts = _catalogTools.LastFoundProducts.Where(p => p.InStock && p.StockQty > 0).Take(2).ToList();
            if (inStockProducts.Count > 0)
            {
                Logger.LogInfo($"[CatalogExecutor] Resgatando {inStockProducts.Count} produto(s) em estoque encontrados pela ferramenta que atendem a busca '{intentResult.ExtractedProductQuery}'.");
                catalogResult.HasResults = true;
                catalogResult.RequiresHuman = false;
                catalogResult.Products = inStockProducts;
                catalogResult.MessageForUser = inStockProducts.Count == 1
                    ? $"Encontrei esta excelente opção em nosso estoque para '{intentResult.ExtractedProductQuery}':\n\n" +
                      $"• **{inStockProducts[0].Name}** — R$ {inStockProducts[0].Price:N2} (Cor: {inStockProducts[0].Color}, Tam: {inStockProducts[0].Size})\n\n" +
                      "O que achou dessa opção? Deseja que eu monte um orçamento ou gostaria de ver outras opções?"
                    : $"Encontrei ótimas opções em nosso estoque que atendem perfeitamente ao seu pedido de '{intentResult.ExtractedProductQuery}':\n\n" +
                      string.Join("\n", inStockProducts.Select(p => $"• **{p.Name}** — R$ {p.Price:N2} (Cor: {p.Color}, Tam: {p.Size})")) +
                      "\n\nQual dessas opções você prefere, gostaria de ver outras opções ou deseja que eu monte um orçamento?";
            }
        }

        catalogResult.OriginalIntent = intentResult.Intent;

        await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);
        await _userInteractor.PublishAgentStateAsync("catalog", "done", "Done", cancellationToken);

        if (!catalogResult.HasResults || catalogResult.Products == null || catalogResult.Products.Count == 0)
        {
            await _userInteractor.PublishTraceAsync("Nenhum produto correspondente encontrado no catálogo.", "warning", cancellationToken);
            await _userInteractor.SendUserResponseAsync(
                catalogResult.MessageForUser,
                "catalog",
                audience: MessageAudience.Both,
                cancellationToken: cancellationToken);

            await context.YieldOutputAsync(catalogResult, cancellationToken);
            return catalogResult;
        }

        // Enriquecer produtos com ImageUrl e dados complementares se estiverem faltando
        foreach (var p in catalogResult.Products)
        {
            var found = _catalogTools.LastFoundProducts.FirstOrDefault(x => string.Equals(x.Sku, p.Sku, StringComparison.OrdinalIgnoreCase));
            if (found != null)
            {
                if (string.IsNullOrWhiteSpace(p.ImageUrl) && !string.IsNullOrWhiteSpace(found.ImageUrl))
                    p.ImageUrl = found.ImageUrl;
                if (string.IsNullOrWhiteSpace(p.Color) && !string.IsNullOrWhiteSpace(found.Color))
                    p.Color = found.Color;
                if (string.IsNullOrWhiteSpace(p.Size) && !string.IsNullOrWhiteSpace(found.Size))
                    p.Size = found.Size;
            }
            if (string.IsNullOrWhiteSpace(p.ImageUrl) && !string.IsNullOrWhiteSpace(p.Sku))
            {
                p.ImageUrl = $"/images/products/{p.Sku}.svg";
            }
        }

        await context.QueueStateUpdateAsync(Constants.SelectedProductsKey, catalogResult.Products, Constants.SalesStateScope);

        var productNames = string.Join(", ", catalogResult.Products.Select(p => $"{p.Name} (R$ {p.Price:N2})"));
        await _userInteractor.PublishTraceAsync($"Produtos encontrados: {productNames}", "success", cancellationToken);

        // Garante que o texto exibido ao cliente contenha sempre os preços individuais de cada item
        var displayMessage = catalogResult.MessageForUser;
        if (!displayMessage.Contains("R$") && catalogResult.Products.Count > 0)
        {
            if (catalogResult.Products.Count == 1)
            {
                var p = catalogResult.Products[0];
                displayMessage = $"Encontrei esta excelente opção no catálogo para você:\n\n" +
                    $"• **{p.Name}** — R$ {p.Price:N2} (Cor: {p.Color ?? "N/A"}, Tam: {p.Size ?? "N/A"})\n\n" +
                    "O que achou dessa opção? Deseja ver mais detalhes, ver outras opções ou prefere que eu monte um orçamento?";
            }
            else
            {
                displayMessage = $"Encontrei as seguintes opções no catálogo para você:\n\n" +
                    string.Join("\n", catalogResult.Products.Select((p, idx) =>
                        $"{idx + 1}. **{p.Name}** — R$ {p.Price:N2} (Cor: {p.Color ?? "N/A"}, Tam: {p.Size ?? "N/A"})")) +
                    "\n\nQual dessas opções você mais gostou? Deseja ver mais detalhes, pesquisar outro modelo ou prefere que eu monte um orçamento?";
            }
        }

        var toolCalls = catalogResult.Products.Select(p => new AgentToolCall
        {
            Name = "SearchProducts",
            Args = $"sku: {p.Sku}",
            Ok = p.InStock
        }).ToList();

        var images = catalogResult.Products
            .Where(p => !string.IsNullOrWhiteSpace(p.ImageUrl))
            .Select(p => new MafImagePayload
            {
                Url = p.ImageUrl,
                Alt = p.Name,
                Sku = p.Sku
            }).ToList();

        // ── Interação Direta e Conversacional com o Cliente ──
        await _userInteractor.PublishAgentStateAsync("catalog", "active", "Aguardando cliente", cancellationToken);

        var customerAnswer = await _userInteractor.GetUserResponseAsync(
            displayMessage,
            "catalog",
            tools: toolCalls,
            audience: MessageAudience.Both,
            images: images.Count > 0 ? images : null,
            cancellationToken: cancellationToken);

        await _userInteractor.PublishAgentStateAsync("catalog", "done", "Done", cancellationToken);
        await _userInteractor.SetAgentTypingAsync("Analisando sua resposta...", true, cancellationToken);

        // Avaliação inteligente da decisão do cliente
        var decisionPrompt = $@"Produtos apresentados:
{JsonSerializer.Serialize(catalogResult.Products.Select(p => new { p.Sku, p.Name, p.Price, p.Color, p.Size, p.Category }))}

Mensagem enviada ao cliente:
{displayMessage}

Resposta do cliente:
'{customerAnswer}'";

        var decisionResponse = await _decisionAgent.RunAsync(decisionPrompt, cancellationToken: cancellationToken);
        await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);

        if (!AgentResponseParser.TryDeserializeAgentResponse(decisionResponse.Text, out CustomerChoiceEvaluation? evaluation) || evaluation is null)
        {
            var lower = customerAnswer.ToLowerInvariant();
            var wantsPhoto = lower.Contains("foto") || lower.Contains("imagem") || lower.Contains("fotos") || lower.Contains("imagens");
            var wantsMore = lower.Contains("esportiv") || lower.Contains("outro") || lower.Contains("outra") || lower.Contains("não ") || lower.Contains("nao ") || lower.Contains("além");
            var wantsQuote = lower.Contains("quero") || lower.Contains("levar") || lower.Contains("sim") || lower.Contains("fechar") || lower.Contains("orçamento") || lower.Contains("todas") || lower.Contains("três") || lower.Contains("tres");

            evaluation = new CustomerChoiceEvaluation
            {
                NextAction = wantsPhoto ? "show_photo" : (wantsMore ? "search_more" : (wantsQuote ? "checkout" : "decline")),
                WantsQuote = !wantsPhoto && !wantsMore && wantsQuote,
                NewSearchQuery = wantsMore ? customerAnswer : null,
                AcceptedSkus = wantsQuote ? catalogResult.Products.Select(p => p.Sku).ToList() : [],
                TargetSku = wantsPhoto ? catalogResult.Products.FirstOrDefault()?.Sku : null
            };
        }

        Logger.LogInfo($"[CatalogExecutor] Decisão avaliada: next_action='{evaluation.NextAction}', wants_quote={evaluation.WantsQuote}, query='{evaluation.NewSearchQuery}', skus={string.Join(",", evaluation.AcceptedSkus ?? [])}");

        // Loop interativo para fotos e dúvidas antes de seguir para checkout ou refinamento de busca
        int conversationTurns = 0;
        const int maxTurns = 5;

        while (conversationTurns < maxTurns)
        {
            conversationTurns++;

            var isPhotoRequest = string.Equals(evaluation.NextAction, "show_photo", StringComparison.OrdinalIgnoreCase) ||
                System.Text.RegularExpressions.Regex.IsMatch(customerAnswer, @"\b(foto|imagem|fotos|imagens|ver\s+ela|ver\s+ele)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (isPhotoRequest)
            {
                // Identifica o produto alvo
                ProductInfo? targetProduct = null;
                if (!string.IsNullOrWhiteSpace(evaluation.TargetSku))
                {
                    targetProduct = catalogResult.Products.FirstOrDefault(p => string.Equals(p.Sku, evaluation.TargetSku, StringComparison.OrdinalIgnoreCase));
                }
                if (targetProduct == null && evaluation.AcceptedSkus?.Count > 0)
                {
                    targetProduct = catalogResult.Products.FirstOrDefault(p => string.Equals(p.Sku, evaluation.AcceptedSkus[0], StringComparison.OrdinalIgnoreCase));
                }
                if (targetProduct == null)
                {
                    targetProduct = catalogResult.Products.FirstOrDefault(p =>
                        (!string.IsNullOrWhiteSpace(p.Color) && customerAnswer.Contains(p.Color, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(p.Name) && customerAnswer.Contains(p.Name, StringComparison.OrdinalIgnoreCase)));
                }
                // Fallback: o produto principal (primeiro da lista)
                targetProduct ??= catalogResult.Products.FirstOrDefault();

                if (targetProduct != null)
                {
                    var imgUrl = !string.IsNullOrWhiteSpace(targetProduct.ImageUrl)
                        ? targetProduct.ImageUrl
                        : $"/images/products/{targetProduct.Sku}.svg";

                    var photoImages = new List<MafImagePayload>
                    {
                        new()
                        {
                            Url = imgUrl,
                            Alt = targetProduct.Name,
                            Sku = targetProduct.Sku
                        }
                    };

                    var photoMsg = !string.IsNullOrWhiteSpace(evaluation.MessageForUser)
                        ? evaluation.MessageForUser
                        : $"Com certeza! Aqui está a foto da **{targetProduct.Name}** — R$ {targetProduct.Price:N2} (Cor: {targetProduct.Color ?? "N/A"}, Tam: {targetProduct.Size ?? "N/A"}):";

                    if (!photoMsg.Contains("?"))
                    {
                        photoMsg += "\n\nO que você achou dela? Gostaria de incluí-la no orçamento ou prefere ver outras opções?";
                    }

                    var nextUserReply = await _userInteractor.GetUserResponseAsync(
                        photoMsg,
                        "catalog",
                        images: photoImages,
                        audience: MessageAudience.Both,
                        cancellationToken: cancellationToken);

                    customerAnswer = nextUserReply;

                    // Reavalia a resposta do cliente
                    var reevalPrompt = $@"Produto visualizado: '{targetProduct.Name}' (SKU: {targetProduct.Sku}, Preço: R$ {targetProduct.Price:N2})
Produtos disponíveis: {JsonSerializer.Serialize(catalogResult.Products.Select(p => new { p.Sku, p.Name, p.Price }))}

Resposta do cliente:
'{customerAnswer}'";

                    var reevalResponse = await _decisionAgent.RunAsync(reevalPrompt, cancellationToken: cancellationToken);
                    if (AgentResponseParser.TryDeserializeAgentResponse(reevalResponse.Text, out CustomerChoiceEvaluation? nextEval) && nextEval is not null)
                    {
                        evaluation = nextEval;
                    }
                    else
                    {
                        var lower = customerAnswer.ToLowerInvariant();
                        var likes = lower.Contains("sim") || lower.Contains("gostei") || lower.Contains("quero") || lower.Contains("legal") || lower.Contains("ótimo") || lower.Contains("otimo") || lower.Contains("orçamento") || lower.Contains("fechar");
                        var wantsOther = lower.Contains("outro") || lower.Contains("outra") || lower.Contains("não ") || lower.Contains("nao ") || lower.Contains("diferente");
                        evaluation = new CustomerChoiceEvaluation
                        {
                            NextAction = likes ? "checkout" : (wantsOther ? "search_more" : "decline"),
                            WantsQuote = likes,
                            AcceptedSkus = likes ? [targetProduct.Sku] : [],
                            NewSearchQuery = wantsOther ? customerAnswer : null
                        };
                    }

                    continue;
                }
            }
            else if (string.Equals(evaluation.NextAction, "question", StringComparison.OrdinalIgnoreCase))
            {
                var questionAnswer = !string.IsNullOrWhiteSpace(evaluation.MessageForUser)
                    ? evaluation.MessageForUser
                    : "Aqui estão os valores individuais:\n" + string.Join("\n", catalogResult.Products.Select(p => $"• **{p.Name}** — R$ {p.Price:N2}"));

                var nextPrompt = $"{questionAnswer}\n\nGostaria de incluir alguma dessas peças no orçamento ou prefere pesquisar outro modelo?";
                var nextUserReply = await _userInteractor.GetUserResponseAsync(
                    nextPrompt,
                    "catalog",
                    audience: MessageAudience.Both,
                    cancellationToken: cancellationToken);

                customerAnswer = nextUserReply;

                var secondPrompt = $@"Produtos disponíveis: {JsonSerializer.Serialize(catalogResult.Products.Select(p => new { p.Sku, p.Name, p.Price }))}

Resposta do cliente:
'{customerAnswer}'";

                var secondEvalResponse = await _decisionAgent.RunAsync(secondPrompt, cancellationToken: cancellationToken);
                if (AgentResponseParser.TryDeserializeAgentResponse(secondEvalResponse.Text, out CustomerChoiceEvaluation? secondEval) && secondEval is not null)
                {
                    evaluation = secondEval;
                }
                else
                {
                    evaluation.NextAction = customerAnswer.ToLowerInvariant().Contains("não") ? "decline" : "checkout";
                    evaluation.WantsQuote = evaluation.NextAction == "checkout";
                    if (evaluation.WantsQuote)
                    {
                        evaluation.AcceptedSkus = catalogResult.Products.Select(p => p.Sku).ToList();
                    }
                }

                continue;
            }

            break;
        }

        // Aplica o roteamento
        catalogResult.CustomerInquiry = customerAnswer;
        catalogResult.NextAction = evaluation.NextAction;
        catalogResult.NewSearchQuery = evaluation.NewSearchQuery;
        catalogResult.CustomerWantsQuote = evaluation.WantsQuote || string.Equals(evaluation.NextAction, "checkout", StringComparison.OrdinalIgnoreCase);

        var cart = await context.ReadStateAsync<List<ProductInfo>>(Constants.CartItemsKey, Constants.SalesStateScope) ?? [];

        if (catalogResult.CustomerWantsQuote)
        {
            var skusToInclude = evaluation.AcceptedSkus ?? [];
            var chosenProducts = new List<ProductInfo>();

            if (skusToInclude.Count > 0)
            {
                chosenProducts = catalogResult.Products.Where(p => skusToInclude.Contains(p.Sku, StringComparer.OrdinalIgnoreCase)).ToList();
                if (chosenProducts.Count == 0)
                {
                    chosenProducts = await _salesAdminClient.GetProductsBySkusAsync(skusToInclude, cancellationToken);
                }
            }

            if (chosenProducts.Count == 0)
            {
                chosenProducts = catalogResult.Products;
            }

            catalogResult.SelectedProducts = chosenProducts;

            foreach (var p in chosenProducts)
            {
                if (!cart.Any(c => string.Equals(c.Sku, p.Sku, StringComparison.OrdinalIgnoreCase)))
                {
                    cart.Add(p);
                }
            }
            await context.QueueStateUpdateAsync(Constants.CartItemsKey, cart, Constants.SalesStateScope);
        }
        else if (string.Equals(catalogResult.NextAction, "decline", StringComparison.OrdinalIgnoreCase))
        {
            await _userInteractor.SendUserResponseAsync(
                !string.IsNullOrWhiteSpace(evaluation.MessageForUser) ? evaluation.MessageForUser : "Sem problemas! Fico à total disposição se precisar de algo no futuro.",
                "catalog",
                audience: MessageAudience.Both,
                cancellationToken: cancellationToken);
        }

        await context.YieldOutputAsync(catalogResult, cancellationToken);
        return catalogResult;
    }
}
