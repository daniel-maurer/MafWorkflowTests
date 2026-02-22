using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace SupportWorkflow;

public static class WorkflowFactory
{
    /// <summary>
    /// Builds a multi-agent support workflow with conditional routing.
    /// 
    /// Workflow:
    /// 1. Triage Agent → Understands the problem
    /// 2. Frequent Problem Agent → Analyzes if it's known or complex
    /// 3. Conditional Routing:
    ///    - If problem is known and not complex → Resolution Agent (automated)
    ///    - If problem is unknown or complex → Human Support Executor (simulated)
    /// </summary>
    /// <param name="chatClient">The chat client to use for all agents</param>
    /// <returns>A configured workflow instance</returns>
    /// <throws>ArgumentNullException if chatClient is null</throws>
    internal static Workflow BuildWorkflow(IChatClient chatClient)
    {
        if (chatClient == null)
        {
            throw new ArgumentNullException(nameof(chatClient), "Chat client cannot be null");
        }

        RequestPort userMessageRequestPort = RequestPort.Create<string, string>("UserMessage");
        var consoleInteractor = new ConsoleInteractor();

        // Create all agents
        AIAgent triageAgent = TriageAgentFactory.GetTriageAgent(chatClient);
        var triageExecutor = new TriageExecutor(triageAgent, consoleInteractor);

        AIAgent frequentProblemAgent = FrequentProblemAgentFactory.GetFrequentProblemAgent(chatClient);
        var frequentProblemExecutor = new FrequentProblemExecutor(frequentProblemAgent, consoleInteractor);

        AIAgent resolutionAgent = ResolutionAgentFactory.GetResolutionAgent(chatClient);
        var resolutionExecutor = new ResolutionExecutor(resolutionAgent, consoleInteractor);
        
        // Human support executor for complex or unknown issues
        var humanSupportExecutor = new HumanSupportExecutor(consoleInteractor);

        // Build workflow with conditional edges
        return new WorkflowBuilder(userMessageRequestPort)
            .AddEdge(userMessageRequestPort, triageExecutor)
            .AddEdge(triageExecutor, frequentProblemExecutor)
            // Route to Resolution Agent if problem is known AND not complex
            .AddEdge(frequentProblemExecutor, resolutionExecutor, 
                condition: GetKnownProblemCondition())
            // Route to Human Support if problem is unknown OR complex
            .AddEdge(frequentProblemExecutor, humanSupportExecutor, 
                condition: GetComplexProblemCondition())
            .Build();
    }

    /// <summary>
    /// Creates a condition that checks if a problem is known and not complex.
    /// </summary>
    private static Func<object?, bool> GetKnownProblemCondition() =>
        result => result is FrequentProblemResult fpResult && fpResult.IsKnown && !fpResult.IsComplex;

    /// <summary>
    /// Creates a condition that checks if a problem is unknown or complex.
    /// </summary>
    private static Func<object?, bool> GetComplexProblemCondition() =>
        result => result is FrequentProblemResult fpResult && (!fpResult.IsKnown || fpResult.IsComplex);
}