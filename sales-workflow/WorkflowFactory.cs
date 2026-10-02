using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using SalesWorkflow.AgentFactories;
using SalesWorkflow.Executors;
using SalesWorkflow.Models;

namespace SalesWorkflow;

public static class WorkflowFactory
{
    internal static Workflow BuildSalesWorkflow(IChatClient chatClient, IUserInteractor interactor)
    {
        RequestPort userMessagePort = RequestPort.Create<string, string>("UserMessage");

        // === Agentes ===
        var intentAgent = IntentAgentFactory.GetIntentAgent(chatClient);
        var catalogAgent = CatalogAgentFactory.GetCatalogAgent(chatClient);
        var salesAdvisorAgent = SalesAdvisorAgentFactory.GetSalesAdvisorAgent(chatClient);
        var quoteAgent = QuoteAgentFactory.GetQuoteAgent(chatClient);
        var followUpAgent = FollowUpAgentFactory.GetFollowUpAgent(chatClient);
        var salesRecordAgent = SalesRecordAgentFactory.GetSalesRecordAgent(chatClient);

        // === Executores ===
        var intentExecutor = new IntentExecutor(intentAgent, interactor);
        var catalogExecutor = new CatalogExecutor(catalogAgent, interactor);
        var salesAdvisorExecutor = new SalesAdvisorExecutor(salesAdvisorAgent, interactor);
        var quoteExecutor = new QuoteExecutor(quoteAgent, interactor);
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

    private static Func<object?, bool> GetWantsQuoteCondition()
        => result => result is SalesAdviceResult sar && sar.CustomerWantsQuote;

    private static Func<object?, bool> GetNoQuoteCondition()
        => result => result is SalesAdviceResult sar && !sar.CustomerWantsQuote;
}
