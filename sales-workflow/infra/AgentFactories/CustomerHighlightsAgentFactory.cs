using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SalesWorkflow.Models;
using SalesWorkflow.Services;

namespace SalesWorkflow.AgentFactories;

public static class CustomerHighlightsAgentFactory
{
    public static ChatClientAgent GetCustomerHighlightsAgent(IChatClient chatClient, InstructionCache instructionCache)
    {
        return new(chatClient, new ChatClientAgentOptions(
            instructions: instructionCache.GetInstruction("sales", "CustomerHighlightsAgent", ""),
            name: "CustomerHighlightsAgent")
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    AIJsonUtilities.CreateJsonSchema(typeof(CustomerHighlightsResult)))
            }
        });
    }
}
