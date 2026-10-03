using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using SalesWorkflow.Models;

namespace SalesWorkflow.Executors;

internal sealed class LoopSearchAdapterExecutor : Executor<SalesAdviceResult, IntentResult>
{
    private readonly IUserInteractor _userInteractor;

    public LoopSearchAdapterExecutor(IUserInteractor userInteractor) : base("LoopSearchAdapterExecutor")
    {
        _userInteractor = userInteractor;
    }

    public override async ValueTask<IntentResult> HandleAsync(
        SalesAdviceResult adviceResult,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        string query = !string.IsNullOrWhiteSpace(adviceResult.NewSearchQuery)
            ? adviceResult.NewSearchQuery
            : "produtos";

        Logger.LogInfo($"[LoopSearchAdapterExecutor] Criando IntentResult direto para o catálogo no loop: '{query}'");

        var intentResult = new IntentResult
        {
            IsUnderstood = true,
            Intent = "product_search",
            ExtractedProductQuery = query,
            Summary = $"Busca de produto adicional solicitada pelo cliente: {query}",
            CustomerSentiment = "positive",
            RequiresHuman = false
        };

        // Mantém o histórico de interação sincronizado
        var history = await context.ReadStateAsync<List<ChatMessage>>(
            Constants.InteractionHistoryKey,
            Constants.SalesStateScope) ?? [];
        history.Add(new ChatMessage(ChatRole.User, $"Quero ver também: {query}"));
        await context.QueueStateUpdateAsync(Constants.InteractionHistoryKey, history, Constants.SalesStateScope);
        await context.QueueStateUpdateAsync(Constants.IntentKey, intentResult.Intent, Constants.SalesStateScope);

        await context.YieldOutputAsync(intentResult, cancellationToken);
        return intentResult;
    }
}
