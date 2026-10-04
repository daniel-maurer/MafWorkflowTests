using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SalesWorkflow.Models;
using SalesWorkflow.Services;

namespace SalesWorkflow.AgentFactories;

public static class IntentAgentFactory
{
    public static ChatClientAgent GetIntentAgent(IChatClient chatClient, InstructionCache instructionCache)
    {
        return new(chatClient, new ChatClientAgentOptions(
            instructions: instructionCache.GetInstruction("sales", "IntentAgent", ""),
            name: "IntentAgent")
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    AIJsonUtilities.CreateJsonSchema(typeof(IntentResult)))
            }
        });
    }
}
