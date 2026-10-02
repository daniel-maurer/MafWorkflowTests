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
    private readonly IUserInteractor _userInteractor;

    public SalesAdvisorExecutor(AIAgent salesAdvisorAgent, IUserInteractor userInteractor) : base("SalesAdvisorExecutor")
    {
        _salesAdvisorAgent = salesAdvisorAgent;
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
                CustomerWantsQuote = true,
                SelectedProducts = catalogResult.Products
            };
        }

        adviceResult.SelectedProducts = catalogResult.Products;

        await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);
        await _userInteractor.PublishAgentStateAsync("sales-advisor", "done", "Done", cancellationToken);

        var complementsCount = adviceResult.SuggestedComplements.Count;
        var kitsCount = adviceResult.SuggestedKits.Count;
        await _userInteractor.PublishTraceAsync(
            $"Consultoria de vendas: {complementsCount} complemento(s) e {kitsCount} kit(s) recomendados.",
            "info",
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(adviceResult.MessageForUser))
        {
            var tools = new List<AgentToolCall>();
            if (kitsCount > 0)
            {
                tools.Add(new AgentToolCall { Name = "SuggestKits", Args = $"{kitsCount} kits", Ok = true });
            }
            if (complementsCount > 0)
            {
                tools.Add(new AgentToolCall { Name = "GetCompatibleProducts", Args = $"{complementsCount} itens", Ok = true });
            }

            await _userInteractor.SendUserResponseAsync(
                adviceResult.MessageForUser,
                "sales-advisor",
                tools: tools,
                audience: MessageAudience.Both,
                cancellationToken: cancellationToken);
        }

        await context.YieldOutputAsync(adviceResult, cancellationToken);
        return adviceResult;
    }
}
