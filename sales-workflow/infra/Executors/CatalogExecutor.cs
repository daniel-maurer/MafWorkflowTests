using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using SalesWorkflow.AiTools;
using SalesWorkflow.Models;

namespace SalesWorkflow.Executors;

internal sealed class CatalogExecutor : Executor<IntentResult, CatalogResult>
{
    private readonly AIAgent _catalogAgent;
    private readonly IUserInteractor _userInteractor;
    private readonly CatalogTools _catalogTools;

    public CatalogExecutor(AIAgent catalogAgent, IUserInteractor userInteractor, CatalogTools catalogTools) : base("CatalogExecutor")
    {
        _catalogAgent = catalogAgent;
        _userInteractor = userInteractor;
        _catalogTools = catalogTools;
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

        var prompt = $@"O cliente possui a intenção '{intentResult.Intent}'.
Termo de busca extraído: '{intentResult.ExtractedProductQuery}'.
Filtros: {JsonSerializer.Serialize(intentResult.Filters ?? new ProductFilters())}.
Resumo: {intentResult.Summary}.

DIRETRIZES DE BUSCA E RANQUEAMENTO NO CATÁLOGO:
1. Realize a busca usando o termo informado pelo cliente: '{intentResult.ExtractedProductQuery}'.
2. AVALIAÇÃO E RANQUEAMENTO:
   - Ordene os produtos retornados do MAIS próximo para o MENOS próximo do que o cliente pediu.
   - O 1º LUGAR da lista 'products' deve ser a opção que mais perfeitamente atende a todos os critérios (modelo, tipo, cor, estilo).
   - O 2º e 3º lugares devem ser alternativas próximas interessantes para o cliente comparar.
3. Se houver produtos em estoque compatíveis, defina has_results = true, requires_human = false, e formule 'message_for_user' destacando a primeira opção como a principal recomendação e apresentando as demais como opções adicionais.
4. Apenas se após as buscas não existir nenhum produto compatível no estoque, encerre com has_results = false e requires_human = true.

Responda SEMPRE estritamente no esquema JSON de CatalogResult.";

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
            var inStockProducts = _catalogTools.LastFoundProducts.Where(p => p.InStock && p.StockQty > 0).Take(5).ToList();
            if (inStockProducts.Count > 0)
            {
                Logger.LogInfo($"[CatalogExecutor] Resgatando {inStockProducts.Count} produto(s) em estoque encontrados pela ferramenta que atendem a busca '{intentResult.ExtractedProductQuery}'.");
                catalogResult.HasResults = true;
                catalogResult.RequiresHuman = false;
                catalogResult.Products = inStockProducts;
                catalogResult.MessageForUser = $"Encontrei ótimas opções em nosso estoque que atendem perfeitamente ao seu pedido de '{intentResult.ExtractedProductQuery}':\n\n" +
                    string.Join("\n", inStockProducts.Select(p => $"• **{p.Name}** — R$ {p.Price:N2} (Cor: {p.Color}, Tam: {p.Size})")) +
                    "\n\nAlguma dessas opções te agrada ou você gostaria de ver mais detalhes?";
            }
        }

        catalogResult.OriginalIntent = intentResult.Intent;

        await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);
        await _userInteractor.PublishAgentStateAsync("catalog", "done", "Done", cancellationToken);

        if (catalogResult.HasResults && catalogResult.Products is { Count: > 0 })
        {
            await context.QueueStateUpdateAsync(Constants.SelectedProductsKey, catalogResult.Products, Constants.SalesStateScope);

            var productNames = string.Join(", ", catalogResult.Products.Select(p => $"{p.Name} (R$ {p.Price:N2})"));
            await _userInteractor.PublishTraceAsync($"Produtos encontrados: {productNames}", "success", cancellationToken);

            var displayMessage = string.IsNullOrWhiteSpace(catalogResult.MessageForUser)
                ? $"Encontrei as seguintes opções no catálogo:\n\n" + string.Join("\n", catalogResult.Products.Select(p => $"• **{p.Name}** — R$ {p.Price:N2} (Estoque: {(p.InStock ? "Disponível" : "Esgotado")})"))
                : catalogResult.MessageForUser;

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

            await _userInteractor.SendUserResponseAsync(
                displayMessage,
                "catalog",
                tools: toolCalls,
                audience: MessageAudience.Both,
                images: images.Count > 0 ? images : null,
                cancellationToken: cancellationToken);
        }
        else
        {
            await _userInteractor.PublishTraceAsync("Nenhum produto correspondente encontrado no catálogo.", "warning", cancellationToken);
            await _userInteractor.SendUserResponseAsync(
                catalogResult.MessageForUser,
                "catalog",
                audience: MessageAudience.Both,
                cancellationToken: cancellationToken);
        }

        await context.YieldOutputAsync(catalogResult, cancellationToken);
        return catalogResult;
    }
}
