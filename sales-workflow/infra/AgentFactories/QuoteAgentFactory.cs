using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SalesWorkflow.AiTools;
using SalesWorkflow.Models;
using SalesWorkflow.Services;

namespace SalesWorkflow.AgentFactories;

public static class QuoteAgentFactory
{
    public static ChatClientAgent GetQuoteAgent(IChatClient chatClient, QuoteTools quoteTools, InstructionCache instructionCache)
    {
        return new(chatClient, new ChatClientAgentOptions(
            instructions: instructionCache.GetInstruction("sales", "QuoteAgent", ""),
            name: "QuoteAgent")
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    AIJsonUtilities.CreateJsonSchema(typeof(QuoteResult))),
                Tools =
                [
                    AIFunctionFactory.Create(quoteTools.GenerateQuote),
                    AIFunctionFactory.Create(quoteTools.ApplyDiscount),
                    AIFunctionFactory.Create(quoteTools.GetPaymentConditions),
                    AIFunctionFactory.Create(QuoteTools.SendQuote)
                ]
            }
        });
    }
}
