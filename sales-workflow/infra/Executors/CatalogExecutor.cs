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

        var history = await context.ReadStateAsync<List<ChatMessage>>(Constants.InteractionHistoryKey, Constants.SalesStateScope) ?? [];

        var categories = await _salesAdminClient.GetCategoriesAsync(active: true, ct: cancellationToken);
        var categoriesNames = categories.Count > 0
            ? string.Join(", ", categories.Select(c => c.Name))
            : "Celulares & Smartphones, Informática, Áudio & Vídeo, Periféricos, Vestuário, Calçados, Wearables";

        // Proteção: se porventura chegou ao catálogo sem termo de busca e sem solicitar humano, recupera do histórico ou pergunta
        if (string.IsNullOrWhiteSpace(intentResult.ExtractedProductQuery) && !intentResult.RequiresHuman)
        {
            var recovered = RecoverProductFromHistory(history);
            if (!string.IsNullOrWhiteSpace(recovered))
            {
                Logger.LogInfo($"[CatalogExecutor] Recuperando produto do histórico: '{recovered}'");
                intentResult.ExtractedProductQuery = recovered;
            }
            else
            {
                var askMsg = $"Como posso te ajudar hoje? Na MAF Store temos opções incríveis em {categoriesNames}! Você tem interesse em algum departamento ou produto específico?";
                await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);
                var userProduct = await _userInteractor.GetUserResponseAsync(
                    askMsg,
                    "catalog",
                    audience: MessageAudience.Both,
                    cancellationToken: cancellationToken);

                intentResult.ExtractedProductQuery = userProduct;
                history.Add(new ChatMessage(ChatRole.Assistant, askMsg));
                history.Add(new ChatMessage(ChatRole.User, userProduct));
                await context.QueueStateUpdateAsync(Constants.InteractionHistoryKey, history, Constants.SalesStateScope);
            }
        }

        _catalogTools.ResetSearchCounter();

        var activeCampaigns = await _salesAdminClient.GetActiveCampaignsAsync(cancellationToken);
        var campaignInfo = activeCampaigns.Count > 0
            ? string.Join("; ", activeCampaigns.Select(c => $"{c.Name}: {c.Description}"))
            : "Nenhuma campanha ativa no momento.";

        var prompt = $@"Intenção: '{intentResult.Intent}'
Contexto do cliente: '{intentResult.Summary}'
Termo de busca: '{intentResult.ExtractedProductQuery}'
Departamentos e Categorias da loja: {categoriesNames}
Campanhas promocionais ativas: {campaignInfo}
Filtros: {JsonSerializer.Serialize(intentResult.Filters ?? new ProductFilters())}

INSTRUÇÃO COMERCIAL:
1. Apresente as opções de produto encontradas e destaque as campanhas promocionais ativas para incentivar o cliente a aproveitar as condições (ex: descontos progressivos e frete grátis).
2. Se o cliente perguntou que tipos de produtos vendemos ou pediu sugestões gerais ('o que tem de legal', 'o que está saindo bem', 'novidades'), mencione os nossos departamentos disponíveis ({categoriesNames}) e apresente os produtos em destaque de estoque.";

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

        // Destaca a campanha ativa na apresentação do catálogo
        if (activeCampaigns.Count > 0)
        {
            var mainCamp = activeCampaigns.FirstOrDefault(c => c.IsActive) ?? activeCampaigns[0];
            var campBanner = $"\n\n🎉 **Aproveite nossa campanha '{mainCamp.Name}'**: {mainCamp.Description}";
            if (!displayMessage.Contains(mainCamp.Name, StringComparison.OrdinalIgnoreCase))
            {
                displayMessage += campBanner;
            }
        }

        displayMessage = displayMessage.Replace("https://mafstore.com/images/products/", "/images/products/");

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

        history.Add(new ChatMessage(ChatRole.Assistant, displayMessage));
        history.Add(new ChatMessage(ChatRole.User, customerAnswer));
        await context.QueueStateUpdateAsync(Constants.InteractionHistoryKey, history, Constants.SalesStateScope);

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

                    history.Add(new ChatMessage(ChatRole.Assistant, photoMsg));
                    history.Add(new ChatMessage(ChatRole.User, nextUserReply));
                    await context.QueueStateUpdateAsync(Constants.InteractionHistoryKey, history, Constants.SalesStateScope);

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

                history.Add(new ChatMessage(ChatRole.Assistant, nextPrompt));
                history.Add(new ChatMessage(ChatRole.User, nextUserReply));
                await context.QueueStateUpdateAsync(Constants.InteractionHistoryKey, history, Constants.SalesStateScope);

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

    private static string RecoverProductFromHistory(List<ChatMessage> history)
    {
        if (history == null || history.Count == 0) return string.Empty;

        var productKeywords = new[]
        {
            "tênis de corrida", "tenis de corrida", "tênis corrida", "tenis corrida", "tênis esportivo", "tenis esportivo",
            "tênis", "tenis", "sapatênis", "sapatenis", "sapato",
            "camisa polo", "camisa de praia", "camisa praia", "camisa social", "camisa",
            "camiseta", "bermuda", "calça", "calca", "short", "jaqueta", "casaco", "blusão", "blusao"
        };

        for (int i = history.Count - 1; i >= 0; i--)
        {
            var msg = history[i];
            if (msg.Role != ChatRole.User || string.IsNullOrWhiteSpace(msg.Text)) continue;

            var lower = msg.Text.ToLowerInvariant();
            foreach (var kw in productKeywords)
            {
                if (lower.Contains(kw))
                {
                    var clean = msg.Text.Trim();
                    var prefixes = new[] { "quero um ", "quero uma ", "quero ", "preciso de um ", "preciso de uma ", "preciso de ", "gostaria de ", "tem ", "busco um ", "busco uma " };
                    foreach (var p in prefixes)
                    {
                        var idx = clean.IndexOf(p, StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0)
                        {
                            var sub = clean[(idx + p.Length)..].Trim();
                            if (!string.IsNullOrWhiteSpace(sub) && sub.Length <= 40) return sub;
                        }
                    }
                    return kw;
                }
            }
        }

        return string.Empty;
    }
}
