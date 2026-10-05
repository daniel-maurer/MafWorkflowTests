using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using SalesWorkflow.Models;

namespace SalesWorkflow.Executors;

internal sealed class LoopSearchAdapterExecutor : Executor<CatalogResult, IntentResult>
{
    private readonly IUserInteractor _userInteractor;

    public LoopSearchAdapterExecutor(IUserInteractor userInteractor) : base("LoopSearchAdapterExecutor")
    {
        _userInteractor = userInteractor;
    }

    public override async ValueTask<IntentResult> HandleAsync(
        CatalogResult catalogResult,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        string query = !string.IsNullOrWhiteSpace(catalogResult.NewSearchQuery)
            ? catalogResult.NewSearchQuery
            : "produtos";

        Logger.LogInfo($"[LoopSearchAdapterExecutor] Criando IntentResult direto para o catálogo no loop: '{query}'");

        var summaryText = !string.IsNullOrWhiteSpace(catalogResult.CustomerInquiry)
            ? $"O cliente perguntou/solicitou: \"{catalogResult.CustomerInquiry}\" (Termos de busca: {query})"
            : $"Busca de produto ou refinamento solicitado pelo cliente: {query}";

        var intentResult = new IntentResult
        {
            IsUnderstood = true,
            Intent = "product_search",
            ExtractedProductQuery = query,
            Summary = summaryText,
            CustomerSentiment = "positive",
            RequiresHuman = false
        };

        // Mantém o histórico de interação sincronizado
        var history = await context.ReadStateAsync<List<ChatMessage>>(
            Constants.InteractionHistoryKey,
            Constants.SalesStateScope) ?? [];
        var historyText = !string.IsNullOrWhiteSpace(catalogResult.CustomerInquiry)
            ? catalogResult.CustomerInquiry
            : $"Quero ver: {query}";
        history.Add(new ChatMessage(ChatRole.User, historyText));
        await context.QueueStateUpdateAsync(Constants.InteractionHistoryKey, history, Constants.SalesStateScope);
        await context.QueueStateUpdateAsync(Constants.IntentKey, intentResult.Intent, Constants.SalesStateScope);

        await context.YieldOutputAsync(intentResult, cancellationToken);
        return intentResult;
    }
}
