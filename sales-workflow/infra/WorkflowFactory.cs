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
        var decisionAgent = SalesAdvisorAgentFactory.GetCustomerDecisionAgent(chatClient, instructionCache);
        var quoteAgent = QuoteAgentFactory.GetQuoteAgent(chatClient, quoteTools, instructionCache);
        var followUpAgent = FollowUpAgentFactory.GetFollowUpAgent(chatClient, cartTools, instructionCache);
        var salesRecordAgent = SalesRecordAgentFactory.GetSalesRecordAgent(chatClient, instructionCache);

        // === Executores ===
        var intentExecutor = new IntentExecutor(intentAgent, interactor, salesAdminClient);
        var catalogExecutor = new CatalogExecutor(catalogAgent, decisionAgent, interactor, catalogTools, salesAdminClient);
        var loopSearchAdapter = new LoopSearchAdapterExecutor(interactor);
        var quoteExecutor = new QuoteExecutor(quoteAgent, interactor, salesAdminClient);
        var followUpExecutor = new FollowUpExecutor(followUpAgent, interactor);
        var humanSellerExecutor = new HumanSellerExecutor(interactor);
        var salesRecordExecutor = new SalesRecordExecutor(salesRecordAgent, interactor);

        return new WorkflowBuilder(userMessagePort)
            // Superstep 1: Triagem de Intenção e Cliente
            .AddEdge(userMessagePort, intentExecutor)
            .AddEdge(intentExecutor, catalogExecutor)

            // Superstep 2: Catálogo -> Decisão Direta do Cliente
            .AddEdge(catalogExecutor, quoteExecutor, condition: GetWantsQuoteCondition())
            .AddEdge(catalogExecutor, loopSearchAdapter, condition: GetNeedsMoreSearchCondition())
            .AddEdge(loopSearchAdapter, catalogExecutor)
            .AddEdge(catalogExecutor, salesRecordExecutor, condition: GetNoQuoteCondition())
            .AddEdge(catalogExecutor, humanSellerExecutor, condition: GetNeedsHumanCondition())

            // Superstep 3: Orçamento -> Follow-Up
            .AddEdge(quoteExecutor, followUpExecutor)

            // Superstep 4: Finalização e Registro Analítico
            .AddEdge(followUpExecutor, salesRecordExecutor)
            .AddEdge(humanSellerExecutor, salesRecordExecutor)
            .Build();
    }

    private static Func<object?, bool> GetNeedsMoreSearchCondition()
        => result => result is CatalogResult cr && 
                     (string.Equals(cr.NextAction, "search_more", StringComparison.OrdinalIgnoreCase) || 
                      !string.IsNullOrWhiteSpace(cr.NewSearchQuery));

    private static Func<object?, bool> GetWantsQuoteCondition()
        => result => result is CatalogResult cr && 
                     cr.HasResults &&
                     !string.Equals(cr.NextAction, "search_more", StringComparison.OrdinalIgnoreCase) &&
                     string.IsNullOrWhiteSpace(cr.NewSearchQuery) &&
                     (string.Equals(cr.NextAction, "checkout", StringComparison.OrdinalIgnoreCase) || cr.CustomerWantsQuote);

    private static Func<object?, bool> GetNoQuoteCondition()
        => result => result is CatalogResult cr && 
                     cr.HasResults &&
                     !string.Equals(cr.NextAction, "search_more", StringComparison.OrdinalIgnoreCase) &&
                     string.IsNullOrWhiteSpace(cr.NewSearchQuery) &&
                     (string.Equals(cr.NextAction, "decline", StringComparison.OrdinalIgnoreCase) || !cr.CustomerWantsQuote);

    private static Func<object?, bool> GetNeedsHumanCondition()
        => result => result is CatalogResult cr && (!cr.HasResults || cr.RequiresHuman);
}
