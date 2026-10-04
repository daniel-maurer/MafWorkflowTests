using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SalesWorkflow.Models;
using SalesWorkflow.Services;

namespace SalesWorkflow.AgentFactories;

public static class SalesRecordAgentFactory
{
    public static ChatClientAgent GetSalesRecordAgent(IChatClient chatClient, InstructionCache instructionCache)
    {
        return new(chatClient, new ChatClientAgentOptions(
            instructions: instructionCache.GetInstruction("sales", "SalesRecordAgent", ""),
            name: "SalesRecordAgent")
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    AIJsonUtilities.CreateJsonSchema(typeof(SalesRecord)))
            }
        });
    }
}
