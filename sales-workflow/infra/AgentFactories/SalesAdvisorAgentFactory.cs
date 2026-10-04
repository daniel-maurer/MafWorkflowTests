using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SalesWorkflow.AiTools;
using SalesWorkflow.Models;
using SalesWorkflow.Services;

namespace SalesWorkflow.AgentFactories;

public static class SalesAdvisorAgentFactory
{
    public static ChatClientAgent GetSalesAdvisorAgent(IChatClient chatClient, CatalogTools catalogTools, InstructionCache instructionCache)
    {
        return new(chatClient, new ChatClientAgentOptions(
            instructions: instructionCache.GetInstruction("sales", "SalesAdvisorAgent", ""),
            name: "SalesAdvisorAgent")
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    AIJsonUtilities.CreateJsonSchema(typeof(SalesAdviceResult))),
                Tools =
                [
                    AIFunctionFactory.Create(catalogTools.CalculateKitPrice)
                ]
            }
        });
    }

    public static ChatClientAgent GetCustomerDecisionAgent(IChatClient chatClient, InstructionCache instructionCache)
    {
        return new(chatClient, new ChatClientAgentOptions(
            instructions: instructionCache.GetInstruction("sales", "CustomerDecisionAgent", ""),
            name: "CustomerDecisionAgent")
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    AIJsonUtilities.CreateJsonSchema(typeof(CustomerChoiceEvaluation)))
            }
        });
    }
}
