using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace SupportWorkflow;

public static class WorkflowFactory
{
    /// <summary>
    /// Builds a multi-agent support workflow with triage, frequent problem detection, and resolution agents.
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

        AIAgent triageAgent = TriageAgentFactory.GetTriageAgent(chatClient);
        var triageExecutor = new TriageExecutor(triageAgent, consoleInteractor);

        AIAgent frequentProblemAgent = FrequentProblemAgentFactory.GetFrequentProblemAgent(chatClient);
        var frequentProblemExecutor = new FrequentProblemExecutor(frequentProblemAgent, consoleInteractor);

        AIAgent resolutionAgent = ResolutionAgentFactory.GetResolutionAgent(chatClient);
        var resolutionExecutor = new ResolutionExecutor(resolutionAgent, consoleInteractor);

        return new WorkflowBuilder(userMessageRequestPort)
            .AddEdge(userMessageRequestPort, triageExecutor)
            .AddEdge(triageExecutor, frequentProblemExecutor)
            .AddEdge(frequentProblemExecutor, resolutionExecutor)
            .Build();
    }
}