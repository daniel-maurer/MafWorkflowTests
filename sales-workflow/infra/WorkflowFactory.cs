using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using SalesWorkflow.AgentFactories;
using SalesWorkflow.AiTools;
using SalesWorkflow.Executors;
using SalesWorkflow.Models;
using SalesWorkflow.Services;

namespace SalesWorkflow;

public static class WorkflowFactory
{
    internal static Workflow BuildSalesWorkflow(
        IChatClient chatClient,
        IUserInteractor interactor,
        SalesAdminClient salesAdminClient,
        InstructionCache instructionCache)
    {
        RequestPort userMessagePort = RequestPort.Create<string, string>("UserMessage");

        // === Tools de Instância ===
        var catalogTools = new CatalogTools(salesAdminClient);
        var quoteTools = new QuoteTools(salesAdminClient);
        var cartTools = new CartTools(salesAdminClient);
        FollowUpTools.AdminClient = salesAdminClient;
        FollowUpTools.Interactor = interactor as ISalesUserInteractor;

        // === Agentes ===
        var intentAgent = IntentAgentFactory.GetIntentAgent(chatClient, instructionCache);
        var catalogAgent = CatalogAgentFactory.GetCatalogAgent(chatClient, catalogTools, instructionCache);
        var salesAdvisorAgent = SalesAdvisorAgentFactory.GetSalesAdvisorAgent(chatClient, catalogTools, instructionCache);
        var decisionAgent = SalesAdvisorAgentFactory.GetCustomerDecisionAgent(chatClient, instructionCache);
        var quoteAgent = QuoteAgentFactory.GetQuoteAgent(chatClient, quoteTools, instructionCache);
        var followUpAgent = FollowUpAgentFactory.GetFollowUpAgent(chatClient, cartTools, instructionCache);
        var salesRecordAgent = SalesRecordAgentFactory.GetSalesRecordAgent(chatClient, instructionCache);

        // === Executores ===
        var intentExecutor = new IntentExecutor(intentAgent, interactor, salesAdminClient);
        var catalogExecutor = new CatalogExecutor(catalogAgent, interactor, catalogTools);
        var salesAdvisorExecutor = new SalesAdvisorExecutor(salesAdvisorAgent, decisionAgent, interactor, salesAdminClient, catalogTools);
        var loopSearchAdapter = new LoopSearchAdapterExecutor(interactor);
        var quoteExecutor = new QuoteExecutor(quoteAgent, interactor, salesAdminClient);
        var followUpExecutor = new FollowUpExecutor(followUpAgent, interactor);
        var humanSellerExecutor = new HumanSellerExecutor(interactor);
        var salesRecordExecutor = new SalesRecordExecutor(salesRecordAgent, interactor);

        return new WorkflowBuilder(userMessagePort)
            // Superstep 1 -> Superstep 2
            .AddEdge(userMessagePort, intentExecutor)
            .AddEdge(intentExecutor, catalogExecutor)
            // Superstep 2 -> Superstep 3 (Condicional)
            .AddEdge(catalogExecutor, salesAdvisorExecutor, condition: GetProductFoundCondition())
            .AddEdge(catalogExecutor, humanSellerExecutor, condition: GetNeedsHumanCondition())
            // Loop: Se o cliente quiser buscar mais produtos ou complementar o carrinho
            .AddEdge(salesAdvisorExecutor, loopSearchAdapter, condition: GetNeedsMoreSearchCondition())
            .AddEdge(loopSearchAdapter, catalogExecutor)
            // Superstep 3 -> Superstep 4 (Condicional)
            .AddEdge(salesAdvisorExecutor, quoteExecutor, condition: GetWantsQuoteCondition())
            .AddEdge(salesAdvisorExecutor, salesRecordExecutor, condition: GetNoQuoteCondition())
            // Superstep 4 -> Superstep 5
            .AddEdge(quoteExecutor, followUpExecutor)
            // Superstep 5/3 -> Convergência para SalesRecordExecutor
            .AddEdge(followUpExecutor, salesRecordExecutor)
            .AddEdge(humanSellerExecutor, salesRecordExecutor)
            .Build();
    }

    private static Func<object?, bool> GetProductFoundCondition()
        => result => result is CatalogResult cr && cr.HasResults && !cr.RequiresHuman;

    private static Func<object?, bool> GetNeedsHumanCondition()
        => result => result is CatalogResult cr && (!cr.HasResults || cr.RequiresHuman);

    private static Func<object?, bool> GetNeedsMoreSearchCondition()
        => result => result is SalesAdviceResult sar && 
                     (string.Equals(sar.NextAction, "search_more", StringComparison.OrdinalIgnoreCase) || 
                      !string.IsNullOrWhiteSpace(sar.NewSearchQuery));

    private static Func<object?, bool> GetWantsQuoteCondition()
        => result => result is SalesAdviceResult sar && 
                     !string.Equals(sar.NextAction, "search_more", StringComparison.OrdinalIgnoreCase) &&
                     string.IsNullOrWhiteSpace(sar.NewSearchQuery) &&
                     (string.Equals(sar.NextAction, "checkout", StringComparison.OrdinalIgnoreCase) || sar.CustomerWantsQuote);

    private static Func<object?, bool> GetNoQuoteCondition()
        => result => result is SalesAdviceResult sar && 
                     !string.Equals(sar.NextAction, "search_more", StringComparison.OrdinalIgnoreCase) &&
                     string.IsNullOrWhiteSpace(sar.NewSearchQuery) &&
                     (string.Equals(sar.NextAction, "decline", StringComparison.OrdinalIgnoreCase) || !sar.CustomerWantsQuote);
}
