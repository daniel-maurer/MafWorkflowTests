using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using SalesWorkflow.AiTools;
using SalesWorkflow.Models;

namespace SalesWorkflow.Executors;

internal sealed class SalesAdvisorExecutor : Executor<CatalogResult, SalesAdviceResult>
{
    private readonly AIAgent _salesAdvisorAgent;
    private readonly AIAgent _decisionAgent;
    private readonly IUserInteractor _userInteractor;

    public SalesAdvisorExecutor(
        AIAgent salesAdvisorAgent,
        AIAgent decisionAgent,
        IUserInteractor userInteractor) : base("SalesAdvisorExecutor")
    {
        _salesAdvisorAgent = salesAdvisorAgent;
        _decisionAgent = decisionAgent;
        _userInteractor = userInteractor;
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

        var allCatalog = CatalogTools.LoadCatalog();
        var candidateComplements = new List<ProductInfo>();

        foreach (var prod in catalogResult.Products)
        {
            if (prod.CompatibleSkus != null && prod.CompatibleSkus.Count > 0)
            {
                var matches = allCatalog.Where(p => prod.CompatibleSkus.Contains(p.Sku, StringComparer.OrdinalIgnoreCase));
                candidateComplements.AddRange(matches);
            }
        }

        if (candidateComplements.Count == 0 && catalogResult.Products.Count > 0)
        {
            var mainProd = catalogResult.Products[0];
            var fallbacks = allCatalog.Where(p => !string.Equals(p.Sku, mainProd.Sku, StringComparison.OrdinalIgnoreCase)).Take(2);
            candidateComplements.AddRange(fallbacks);
        }

        candidateComplements = candidateComplements.DistinctBy(p => p.Sku).ToList();

        var cart = await context.ReadStateAsync<List<ProductInfo>>(Constants.CartItemsKey, Constants.SalesStateScope) ?? [];
        var cartContext = cart.Count > 0
            ? $"\nItens já selecionados e adicionados ao carrinho pelo cliente anteriormente:\n{JsonSerializer.Serialize(cart.Select(p => new { p.Sku, p.Name, p.Price }))}\n"
            : string.Empty;

        var prompt = $@"Produtos recém-localizados no catálogo para esta etapa:
{JsonSerializer.Serialize(catalogResult.Products.Select(p => new { p.Sku, p.Name, p.Price }))}
{cartContext}
Opções de complementos e acessórios disponíveis no estoque:
{JsonSerializer.Serialize(candidateComplements.Select(p => new { p.Sku, p.Name, p.Price }))}

INSTRUÇÕES DO CONSULTOR DE VENDAS:
1. O FOCO PRINCIPAL da sua mensagem deve ser o produto recém-localizado pelo catálogo: {string.Join(", ", catalogResult.Products.Select(p => p.Name))}.
2. Apresente cordialmente o novo produto localizado.
3. Se houver complementos pertinentes ou se o cliente já tiver itens no carrinho, você pode sugerir kits ou combos especiais combinando-os com desconto.
4. OBRIGATÓRIO: Use SEMPRE CalculateKitPrice para calcular os preços de qualquer kit ou combo com desconto. NUNCA faça contas de cabeça.
5. Em 'message_for_user', pergunte se o cliente gostaria que fosse gerado o orçamento formal com condições de pagamento.
Responda SEMPRE no esquema JSON de SalesAdviceResult.";

        var response = await _salesAdvisorAgent.RunAsync(prompt, cancellationToken: cancellationToken);

        if (!AgentResponseParser.TryDeserializeAgentResponse(response.Text, out SalesAdviceResult? adviceResult) || adviceResult is null)
        {
            Logger.LogWarning("[SalesAdvisorExecutor] Falha na desserialização de SalesAdviceResult.");
            adviceResult = new SalesAdviceResult
            {
                MessageForUser = "Gostaria que eu montasse um orçamento formal com condições de pagamento?",
                CustomerWantsQuote = false,
                SelectedProducts = catalogResult.Products
            };
        }
        else
        {
            adviceResult.CustomerWantsQuote = false;
        }

        adviceResult.SelectedProducts = catalogResult.Products;

        // Garante cálculos exatos e coerentes para cada kit sugerido
        foreach (var kit in adviceResult.SuggestedKits)
        {
            var calc = CatalogTools.CalculateKitPrice(kit.ProductSkus, kit.DiscountPct > 0 ? kit.DiscountPct : 10m);
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

        var complementImages = adviceResult.SuggestedComplements
            .Where(p => !string.IsNullOrWhiteSpace(p.ImageUrl))
            .Select(p => new MafImagePayload
            {
                Url = p.ImageUrl,
                Alt = p.Name,
                Sku = p.Sku
            }).ToList();

        var promptText = string.IsNullOrWhiteSpace(adviceResult.MessageForUser)
            ? "Gostaria que eu montasse um orçamento formal com condições de pagamento?"
            : adviceResult.MessageForUser;

        // Aguarda a resposta do usuário antes de prosseguir para orçamento
        string userDecision = await _userInteractor.GetUserResponseAsync(
            promptText,
            "sales-advisor",
            tools: tools,
            audience: MessageAudience.Both,
            images: complementImages.Count > 0 ? complementImages : null,
            cancellationToken: cancellationToken);

        Logger.LogInfo($"[SalesAdvisorExecutor] Resposta do cliente: '{userDecision}'");

        await _userInteractor.SetAgentTypingAsync("Interpretando sua escolha e calculando a melhor proposta...", true, cancellationToken);

        var decisionPrompt = $@"Você é o consultor de vendas avaliando a resposta do cliente após a oferta comercial.

CONTEXTO DA NEGOCIAÇÃO:
- Produtos buscados no catálogo nesta rodada:
{JsonSerializer.Serialize(catalogResult.Products.Select(p => new { p.Sku, p.Name, p.Price }))}
{cartContext}
- Kits / Combos sugeridos pelo consultor:
{JsonSerializer.Serialize(adviceResult.SuggestedKits.Select(k => new { k.Name, k.ProductSkus, k.KitPrice, k.OriginalPrice, k.DiscountPct }))}

- Complementos / Acessórios sugeridos:
{JsonSerializer.Serialize(adviceResult.SuggestedComplements.Select(p => new { p.Sku, p.Name, p.Price }))}

- Proposta apresentada ao cliente:
""{promptText}""

RESPOSTA DO CLIENTE:
""{userDecision}""

SUA TAREFA:
Interprete a intenção e a decisão do cliente em linguagem natural:
1. 'next_action': 
   - 'search_more': se o cliente quiser buscar, ver ou adicionar outro produto (ex: 'além da camisa quero um mouse', 'quero também um mouse', 'vcs tem tênis?', 'gostaria de outro item').
   - 'checkout': se o cliente concordar com a proposta, kit ou quiser fechar o orçamento com os itens atuais (ex: 'sim', 'aceito', 'pode mandar', 'só a camisa', 'fechar orçamento').
   - 'decline': se o cliente recusar expressamente ou cancelar (ex: 'não quero', 'cancela', 'deixa pra lá').
2. 'new_search_query': se next_action for 'search_more', extraia o termo ou descrição exata do novo produto a ser pesquisado (ex: 'mouse'). Se for checkout ou decline, deixe null.
3. 'wants_quote': true se next_action for 'checkout'. false caso contrário.
4. 'accepted_kit_name': nome exato do kit ou combo aceito, ou null.
5. 'accepted_skus': lista de SKUs que o cliente aceitou levar ou manter nesta rodada (por exemplo, se ele disse 'além da camisa quero um mouse', ele aceitou a camisa, então inclua o SKU da camisa!).
6. 'discount_percent': percentual de desconto comercial a conceder (se aceitou kit, o desconto do kit; se não, 0).
7. 'wants_only_original': true se o cliente indicou preferência estritamente pelo produto original sem kits/acessórios.
8. 'reason': breve explicação do seu entendimento da intenção do cliente.
9. 'message_for_user': mensagem amigável para o cliente. Se next_action for 'search_more', confirme que vai buscar o novo produto (ex: 'Perfeito! Vou verificar as opções de mouse para você agora mesmo.').

Responda SEMPRE estritamente no esquema JSON de CustomerChoiceEvaluation.";

        var decisionResponse = await _decisionAgent.RunAsync(decisionPrompt, cancellationToken: cancellationToken);

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
            var roundProducts = allCatalog
                .Where(p => skusToInclude.Contains(p.Sku, StringComparer.OrdinalIgnoreCase))
                .ToList();

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
                await context.QueueStateUpdateAsync(Constants.CartItemsKey, cart, Constants.SalesStateScope);
                adviceResult.SelectedProducts = cart;

                if (string.Equals(adviceResult.NextAction, "search_more", StringComparison.OrdinalIgnoreCase))
                {
                    adviceResult.MessageForUser = !string.IsNullOrWhiteSpace(evaluation.MessageForUser)
                        ? evaluation.MessageForUser
                        : $"Perfeito! Vou verificar as opções de {adviceResult.NewSearchQuery} para você.";
                }
                else
                {
                    adviceResult.MessageForUser = !string.IsNullOrWhiteSpace(evaluation.MessageForUser)
                        ? evaluation.MessageForUser
                        : "Excelente escolha! Vou preparar a sua proposta comercial agora mesmo.";
                }
            }
        }
        else
        {
            Logger.LogWarning("[SalesAdvisorExecutor] Falha na desserialização de CustomerChoiceEvaluation, utilizando fallback padrão.");
            adviceResult.NextAction = "checkout";
            adviceResult.CustomerWantsQuote = true;
            adviceResult.DiscountPercent = 0;
            adviceResult.AcceptedKitName = null;
            adviceResult.MessageForUser = "Perfeito! Vou preparar a sua proposta comercial agora mesmo.";

            cart = await context.ReadStateAsync<List<ProductInfo>>(Constants.CartItemsKey, Constants.SalesStateScope) ?? cart;
            foreach (var p in catalogResult.Products)
            {
                if (!cart.Any(existing => string.Equals(existing.Sku, p.Sku, StringComparison.OrdinalIgnoreCase)))
                {
                    cart.Add(p);
                }
            }
            await context.QueueStateUpdateAsync(Constants.CartItemsKey, cart, Constants.SalesStateScope);
            adviceResult.SelectedProducts = cart;
        }

        // Limpa complementos para evitar vazamento de itens não selecionados
        adviceResult.SuggestedComplements.Clear();

        await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);
        await _userInteractor.PublishAgentStateAsync("sales-advisor", "done", "Done", cancellationToken);

        if (string.Equals(adviceResult.NextAction, "search_more", StringComparison.OrdinalIgnoreCase))
        {
            var cartNames = string.Join(", ", adviceResult.SelectedProducts.Select(p => p.Name));
            await _userInteractor.PublishTraceAsync(
                $"Cliente optou por adicionar mais itens. Novo termo: '{adviceResult.NewSearchQuery}'. Carrinho atual: {cartNames}",
                "info",
                cancellationToken);

            await _userInteractor.SendUserResponseAsync(
                adviceResult.MessageForUser,
                "sales-advisor",
                audience: MessageAudience.Both,
                cancellationToken: cancellationToken);
        }
        else if (string.Equals(adviceResult.NextAction, "decline", StringComparison.OrdinalIgnoreCase))
        {
            await _userInteractor.PublishTraceAsync("Cliente optou por não gerar proposta/orçamento no momento.", "info", cancellationToken);
            await _userInteractor.SendUserResponseAsync(
                adviceResult.MessageForUser,
                "sales-advisor",
                audience: MessageAudience.Both,
                cancellationToken: cancellationToken);
        }
        else
        {
            var itemNames = string.Join(", ", adviceResult.SelectedProducts.Select(p => p.Name));
            await _userInteractor.PublishTraceAsync(
                $"Cliente confirmou interesse na proposta. Pacote: {(adviceResult.AcceptedKitName ?? "Produto(s) selecionado(s)")} ({adviceResult.DiscountPercent}% desc). Itens no carrinho: {itemNames}",
                "success",
                cancellationToken);
        }

        await context.YieldOutputAsync(adviceResult, cancellationToken);
        return adviceResult;
    }
}
