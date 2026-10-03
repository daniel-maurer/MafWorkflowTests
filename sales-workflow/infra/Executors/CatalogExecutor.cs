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

INSTRUÇÕES:
- Faça no máximo 5 buscas (SearchProducts) usando palavras ou variações semânticas.
- NUNCA repita o mesmo termo que já buscou.
- Se encontrar produtos, pare de buscar imediatamente e gere a resposta.
- Se após até 5 tentativas com termos diferentes não encontrar nada, encerre: defina has_results = false e requires_human = true.
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

        catalogResult.OriginalIntent = intentResult.Intent;

        await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);
        await _userInteractor.PublishAgentStateAsync("catalog", "done", "Done", cancellationToken);

        if (catalogResult.HasResults && catalogResult.Products.Count > 0)
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
