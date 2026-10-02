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

        var prompt = $@"Produtos principais selecionados pelo catálogo:
{JsonSerializer.Serialize(catalogResult.Products)}

Opções de complementos e acessórios disponíveis no estoque:
{JsonSerializer.Serialize(candidateComplements)}

Intenção original do cliente: '{catalogResult.OriginalIntent}'.
Analise esses itens, sugira complementos pertinentes dentre as opções acima e monte kits ou combos atrativos com desconto.
Pergunte cordialmente se o cliente deseja que seja montado um orçamento formal com condições de pagamento.
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
- Produtos originais buscados:
{JsonSerializer.Serialize(catalogResult.Products.Select(p => new { p.Sku, p.Name, p.Price }))}

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
1. 'wants_quote': true se o cliente quer orçamento (aceitou kit, combo, produto avulso ou confirmou interesse). false se recusou explicitamente, cancelou ou encerrou.
2. 'accepted_kit_name': nome exato do kit ou combo aceito (ex: 'Kit Estilo Completo'). Se o cliente disse genericamente 'aceito o kit', 'quero o kit' ou 'opção 1' e houver kits sugeridos, selecione o primeiro kit sugerido. Se quis apenas o produto original ou nenhum kit, deixe null.
3. 'accepted_skus': lista de SKUs que devem ser incluídos no orçamento de acordo com a escolha do cliente. Se escolheu um kit, inclua todos os SKUs desse kit. Se quis apenas o produto original, inclua apenas o SKU original.
4. 'discount_percent': percentual de desconto comercial a conceder (se aceitou kit, use o discount_pct do kit; se não, 0).
5. 'wants_only_original': true se o cliente indicou preferência estritamente pelo produto original sem kits/acessórios.
6. 'reason': breve explicação do seu entendimento da intenção do cliente.
7. 'message_for_user': mensagem amigável confirmando a escolha e transição para o orçamento.

Responda SEMPRE estritamente no esquema JSON de CustomerChoiceEvaluation.";

        var decisionResponse = await _decisionAgent.RunAsync(decisionPrompt, cancellationToken: cancellationToken);

        if (AgentResponseParser.TryDeserializeAgentResponse(decisionResponse.Text, out CustomerChoiceEvaluation? evaluation) && evaluation is not null)
        {
            Logger.LogInfo($"[SalesAdvisorExecutor] Avaliação do agente: wants_quote={evaluation.WantsQuote}, kit='{evaluation.AcceptedKitName}', desc={evaluation.DiscountPercent}%, razão='{evaluation.Reason}'");

            adviceResult.CustomerWantsQuote = evaluation.WantsQuote;
            adviceResult.AcceptedKitName = evaluation.AcceptedKitName;
            adviceResult.DiscountPercent = evaluation.DiscountPercent;

            if (evaluation.WantsQuote)
            {
                var skusToInclude = evaluation.AcceptedSkus ?? [];
                adviceResult.SelectedProducts = allCatalog
                    .Where(p => skusToInclude.Contains(p.Sku, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                // Se porventura a lista de SKUs não retornou itens, assegura os produtos originais
                if (adviceResult.SelectedProducts.Count == 0)
                {
                    adviceResult.SelectedProducts = catalogResult.Products;
                }

                adviceResult.MessageForUser = !string.IsNullOrWhiteSpace(evaluation.MessageForUser)
                    ? evaluation.MessageForUser
                    : $"Excelente escolha! Vou preparar a sua proposta comercial agora mesmo.";
            }
            else
            {
                adviceResult.SelectedProducts = [];
                adviceResult.MessageForUser = !string.IsNullOrWhiteSpace(evaluation.MessageForUser)
                    ? evaluation.MessageForUser
                    : "Sem problemas! Fico à sua disposição caso precise no futuro.";
            }
        }
        else
        {
            Logger.LogWarning("[SalesAdvisorExecutor] Falha na desserialização de CustomerChoiceEvaluation, utilizando fallback padrão.");
            adviceResult.CustomerWantsQuote = true;
            adviceResult.SelectedProducts = catalogResult.Products;
            adviceResult.DiscountPercent = 0;
            adviceResult.AcceptedKitName = null;
            adviceResult.MessageForUser = "Perfeito! Vou preparar a sua proposta comercial agora mesmo.";
        }

        // Limpa complementos para evitar vazamento de itens não selecionados
        adviceResult.SuggestedComplements.Clear();

        await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);
        await _userInteractor.PublishAgentStateAsync("sales-advisor", "done", "Done", cancellationToken);

        if (!adviceResult.CustomerWantsQuote)
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
                $"Cliente confirmou interesse na proposta. Pacote: {(adviceResult.AcceptedKitName ?? "Produto principal")} ({adviceResult.DiscountPercent}% desc). Itens: {itemNames}",
                "success",
                cancellationToken);
        }

        await context.YieldOutputAsync(adviceResult, cancellationToken);
        return adviceResult;
    }
}
