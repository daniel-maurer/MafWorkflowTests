using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using SalesWorkflow.AiTools;
using SalesWorkflow.Models;
using SalesWorkflow.Services;

namespace SalesWorkflow.Executors;

internal sealed class SalesAdvisorExecutor : Executor<CatalogResult, SalesAdviceResult>
{
    private readonly AIAgent _salesAdvisorAgent;
    private readonly AIAgent _decisionAgent;
    private readonly IUserInteractor _userInteractor;
    private readonly SalesAdminClient _salesAdminClient;
    private readonly CatalogTools _catalogTools;

    public SalesAdvisorExecutor(
        AIAgent salesAdvisorAgent,
        AIAgent decisionAgent,
        IUserInteractor userInteractor,
        SalesAdminClient salesAdminClient,
        CatalogTools catalogTools) : base("SalesAdvisorExecutor")
    {
        _salesAdvisorAgent = salesAdvisorAgent;
        _decisionAgent = decisionAgent;
        _userInteractor = userInteractor;
        _salesAdminClient = salesAdminClient;
        _catalogTools = catalogTools;
    }

    public override async ValueTask<SalesAdviceResult> HandleAsync(
        CatalogResult catalogResult,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[SalesAdvisorExecutor] Analisando oportunidades de cross-sell e kits para {catalogResult.Products.Count} produtos.");

        await _userInteractor.SetAgentTypingAsync("Preparando sugestões de complementos e ofertas especiais...", true, cancellationToken);
        await _userInteractor.PublishAgentStateAsync("sales-advisor", "active", "Running", cancellationToken);
        await _userInteractor.PublishContextAsync(
            "recommending-options",
            "Assistente Comercial",
            "Consultor de vendas avaliando complementos e kits...",
            "sales-advisor",
            false,
            cancellationToken);

        // 1. Busca complementos reais via API (produtos compatíveis cadastrados ou busca semântica)
        var candidateComplements = new List<ProductInfo>();

        foreach (var prod in catalogResult.Products)
        {
            if (prod.CompatibleSkus != null && prod.CompatibleSkus.Count > 0)
            {
                var matches = await _salesAdminClient.GetProductsBySkusAsync(prod.CompatibleSkus, cancellationToken);
                candidateComplements.AddRange(matches);
            }
        }

        if (candidateComplements.Count == 0 && catalogResult.Products.Count > 0)
        {
            var mainProd = catalogResult.Products[0];
            var term = $"{mainProd.Category} {mainProd.Brand}".Trim();
            if (string.IsNullOrWhiteSpace(term)) term = "acessórios complementares";
            var fallbacks = await _salesAdminClient.SearchProductsSemanticAsync(term, top: 3, cancellationToken);
            candidateComplements.AddRange(fallbacks.Where(p => !string.Equals(p.Sku, mainProd.Sku, StringComparison.OrdinalIgnoreCase)).Take(2));
        }

        var prompt = $@"Os seguintes produtos principais foram identificados no catálogo:
{JsonSerializer.Serialize(catalogResult.Products)}

Opções de complementos e acessórios compatíveis disponíveis:
{JsonSerializer.Serialize(candidateComplements)}

INSTRUÇÕES:
1. Monte sugestões pertinentes de complementos.
2. Monte de 1 a 2 opções de kits/combos atrativos com desconto especial (ex: 10% no combo).
3. OBRIGATÓRIO: Use CalculateKitPrice para calcular os preços exatos dos kits com desconto. NUNCA calcule de cabeça.
4. Apresente os kits com preços exatos e pergunte se o cliente deseja fechar o combo ou gerar um orçamento formal.
5. Defina customer_wants_quote = false por enquanto.
Responda SEMPRE estritamente no esquema JSON de SalesAdviceResult.";

        var response = await _salesAdvisorAgent.RunAsync(prompt, cancellationToken: cancellationToken);

        if (!AgentResponseParser.TryDeserializeAgentResponse(response.Text, out SalesAdviceResult? adviceResult) || adviceResult is null)
        {
            Logger.LogWarning("[SalesAdvisorExecutor] Falha na desserialização de SalesAdviceResult.");
            adviceResult = new SalesAdviceResult
            {
                SuggestedComplements = candidateComplements,
                CustomerWantsQuote = false,
                MessageForUser = "Gostaria de adicionar algum complemento ao seu pedido com condições especiais?"
            };
        }

        adviceResult.SelectedProducts = catalogResult.Products;

        // Garante cálculos exatos e coerentes para cada kit sugerido com base nos preços reais
        foreach (var kit in adviceResult.SuggestedKits)
        {
            var calc = await _catalogTools.CalculateKitPrice(kit.ProductSkus, kit.DiscountPct > 0 ? kit.DiscountPct : 10m, cancellationToken);
            kit.OriginalPrice = calc.OriginalPrice;
            kit.KitPrice = calc.FinalPrice;
            kit.DiscountPct = calc.DiscountPercent;
        }

        await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);
        await _userInteractor.PublishAgentStateAsync("sales-advisor", "active", "Aguardando cliente", cancellationToken);

        var complementsCount = adviceResult.SuggestedComplements.Count;
        var kitsCount = adviceResult.SuggestedKits.Count;
        await _userInteractor.PublishTraceAsync(
            $"Consultoria de vendas: {complementsCount} complemento(s) e {kitsCount} kit(s) recomendados.",
            "info",
            cancellationToken);

        var tools = new List<AgentToolCall>();
        if (kitsCount > 0)
        {
            tools.Add(new AgentToolCall { Name = "SuggestKits", Args = $"{kitsCount} kits", Ok = true });
        }
        if (complementsCount > 0)
        {
            tools.Add(new AgentToolCall { Name = "GetCompatibleProducts", Args = $"{complementsCount} itens", Ok = true });
        }

        var advisorImages = new List<MafImagePayload>();
        foreach (var kit in adviceResult.SuggestedKits)
        {
            var kitProds = await _salesAdminClient.GetProductsBySkusAsync(kit.ProductSkus, cancellationToken);
            foreach (var kp in kitProds.Where(p => !string.IsNullOrWhiteSpace(p.ImageUrl)))
            {
                if (!advisorImages.Any(img => img.Sku == kp.Sku))
                {
                    advisorImages.Add(new MafImagePayload { Url = kp.ImageUrl, Alt = kp.Name, Sku = kp.Sku });
                }
            }
        }

        var customerAnswer = await _userInteractor.GetUserResponseAsync(
            adviceResult.MessageForUser,
            "sales-advisor",
            tools: tools.Count > 0 ? tools : null,
            audience: MessageAudience.Both,
            images: advisorImages.Count > 0 ? advisorImages : null,
            cancellationToken: cancellationToken);

        await _userInteractor.PublishAgentStateAsync("sales-advisor", "done", "Done", cancellationToken);
        await _userInteractor.SetAgentTypingAsync("Analisando sua resposta...", true, cancellationToken);

        var decisionPrompt = $@"O consultor de vendas apresentou a seguinte proposta ao cliente:
Mensagem do consultor: '{adviceResult.MessageForUser}'
Produtos principais originais: {JsonSerializer.Serialize(catalogResult.Products.Select(p => new { p.Sku, p.Name, p.Price }))}
Kits sugeridos: {JsonSerializer.Serialize(adviceResult.SuggestedKits)}
Complementos sugeridos: {JsonSerializer.Serialize(adviceResult.SuggestedComplements.Select(p => new { p.Sku, p.Name, p.Price }))}

Resposta do cliente: '{customerAnswer}'

Classifique a intenção e ação a seguir ('next_action'):
- 'search_more': se pediu para ver/adicionar outro produto.
- 'checkout': se aceitou o kit, ou quis fechar com o que tem.
- 'decline': se recusou.
Responda estritamente no esquema JSON de CustomerChoiceEvaluation.";

        var decisionResponse = await _decisionAgent.RunAsync(decisionPrompt, cancellationToken: cancellationToken);
        await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);

        var cart = await context.ReadStateAsync<List<ProductInfo>>(Constants.CartItemsKey, Constants.SalesStateScope) ?? [];

        if (AgentResponseParser.TryDeserializeAgentResponse(decisionResponse.Text, out CustomerChoiceEvaluation? evaluation) && evaluation is not null)
        {
            Logger.LogInfo($"[SalesAdvisorExecutor] Avaliação do agente: next_action='{evaluation.NextAction}', wants_quote={evaluation.WantsQuote}, query='{evaluation.NewSearchQuery}', kit='{evaluation.AcceptedKitName}', desc={evaluation.DiscountPercent}%, razão='{evaluation.Reason}'");

            adviceResult.NextAction = !string.IsNullOrWhiteSpace(evaluation.NextAction)
                ? evaluation.NextAction
                : (evaluation.WantsQuote ? "checkout" : "decline");
            adviceResult.NewSearchQuery = evaluation.NewSearchQuery;
            adviceResult.AcceptedKitName = evaluation.AcceptedKitName;
            adviceResult.DiscountPercent = evaluation.DiscountPercent;
            adviceResult.CustomerWantsQuote = string.Equals(adviceResult.NextAction, "checkout", StringComparison.OrdinalIgnoreCase);

            var skusToInclude = evaluation.AcceptedSkus ?? [];
            var roundProducts = await _salesAdminClient.GetProductsBySkusAsync(skusToInclude, cancellationToken);

            if (roundProducts.Count == 0 && (adviceResult.CustomerWantsQuote || string.Equals(adviceResult.NextAction, "search_more", StringComparison.OrdinalIgnoreCase)))
            {
                roundProducts = catalogResult.Products;
            }

            cart = await context.ReadStateAsync<List<ProductInfo>>(Constants.CartItemsKey, Constants.SalesStateScope) ?? cart;
            foreach (var p in roundProducts)
            {
                if (!cart.Any(existing => string.Equals(existing.Sku, p.Sku, StringComparison.OrdinalIgnoreCase)))
                {
                    cart.Add(p);
                }
            }

            if (string.Equals(adviceResult.NextAction, "decline", StringComparison.OrdinalIgnoreCase))
            {
                adviceResult.SelectedProducts = [];
                adviceResult.MessageForUser = !string.IsNullOrWhiteSpace(evaluation.MessageForUser)
                    ? evaluation.MessageForUser
                    : "Sem problemas! Fico à sua disposição caso precise no futuro.";
            }
            else
            {
                adviceResult.SelectedProducts = cart.ToList();
                if (!string.IsNullOrWhiteSpace(evaluation.MessageForUser))
                {
                    adviceResult.MessageForUser = evaluation.MessageForUser;
                }
            }

            await _userInteractor.PublishTraceAsync(
                $"Decisão do cliente: {adviceResult.NextAction} | Itens acumulados no carrinho: {cart.Count}",
                string.Equals(adviceResult.NextAction, "decline", StringComparison.OrdinalIgnoreCase) ? "warning" : "success",
                cancellationToken);
        }
        else
        {
            var lower = customerAnswer.ToLowerInvariant();
            var wantsSearchMore = lower.Contains("além") || lower.Contains("também") || lower.Contains("outro") || lower.Contains("tem ") || lower.Contains("quero ver");

            if (wantsSearchMore)
            {
                adviceResult.NextAction = "search_more";
                adviceResult.CustomerWantsQuote = false;
                adviceResult.NewSearchQuery = customerAnswer;
                foreach (var p in catalogResult.Products)
                {
                    if (!cart.Any(existing => string.Equals(existing.Sku, p.Sku, StringComparison.OrdinalIgnoreCase)))
                    {
                        cart.Add(p);
                    }
                }
                adviceResult.SelectedProducts = cart.ToList();
                adviceResult.MessageForUser = $"Entendido! Vou pesquisar: '{customerAnswer}'.";
            }
            else
            {
                var accepted = lower.Contains("sim") || lower.Contains("kit") || lower.Contains("aceito") || lower.Contains("combo") || lower.Contains("fechado") || lower.Contains("quero") || lower.Contains("pode ser");
                adviceResult.NextAction = accepted ? "checkout" : "decline";
                adviceResult.CustomerWantsQuote = accepted;

                if (accepted)
                {
                    var bestKit = adviceResult.SuggestedKits.FirstOrDefault();
                    if (bestKit is not null)
                    {
                        var kitProds = await _salesAdminClient.GetProductsBySkusAsync(bestKit.ProductSkus, cancellationToken);
                        foreach (var p in kitProds)
                        {
                            if (!cart.Any(existing => string.Equals(existing.Sku, p.Sku, StringComparison.OrdinalIgnoreCase)))
                            {
                                cart.Add(p);
                            }
                        }
                        adviceResult.DiscountPercent = bestKit.DiscountPct;
                        adviceResult.AcceptedKitName = bestKit.Name;
                    }
                    else
                    {
                        foreach (var p in catalogResult.Products)
                        {
                            if (!cart.Any(existing => string.Equals(existing.Sku, p.Sku, StringComparison.OrdinalIgnoreCase)))
                            {
                                cart.Add(p);
                            }
                        }
                    }
                    adviceResult.SelectedProducts = cart.ToList();
                }
                else
                {
                    adviceResult.SelectedProducts = [];
                    adviceResult.MessageForUser = "Sem problemas! Fico à sua disposição caso mude de ideia ou precise de mais informações.";
                }
            }
        }

        await context.QueueStateUpdateAsync(Constants.CartItemsKey, cart, Constants.SalesStateScope);
        await context.QueueStateUpdateAsync(Constants.FinalAdviceKey, adviceResult, Constants.SalesStateScope);
        await context.YieldOutputAsync(adviceResult, cancellationToken);
        return adviceResult;
    }
}
