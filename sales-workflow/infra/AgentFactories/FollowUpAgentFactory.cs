using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SalesWorkflow.AiTools;
using SalesWorkflow.Models;
using SalesWorkflow.Services;

namespace SalesWorkflow.AgentFactories;

public static class FollowUpAgentFactory
{
    public static ChatClientAgent GetFollowUpAgent(IChatClient chatClient, CartTools cartTools, InstructionCache instructionCache)
    {
        return new(chatClient, new ChatClientAgentOptions(
            instructions: instructionCache.GetInstruction("sales", "FollowUpAgent", ""),
            name: "FollowUpAgent")
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    AIJsonUtilities.CreateJsonSchema(typeof(FollowUpResult))),
                Tools =
                [
                    AIFunctionFactory.Create(FollowUpTools.ScheduleFollowUp),
                    AIFunctionFactory.Create(cartTools.GetAbandonedCart),
                    AIFunctionFactory.Create(CartTools.SendCartReminder)
                ]
            }
        });
    }
}
