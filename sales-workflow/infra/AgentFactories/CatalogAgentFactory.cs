using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SalesWorkflow.AiTools;
using SalesWorkflow.Models;
using SalesWorkflow.Services;

namespace SalesWorkflow.AgentFactories;

public static class CatalogAgentFactory
{
    public static ChatClientAgent GetCatalogAgent(IChatClient chatClient, CatalogTools catalogTools, StoreTools storeTools, CampaignTools campaignTools, InstructionCache instructionCache)
    {
        return new(chatClient, new ChatClientAgentOptions(
            instructions: instructionCache.GetInstruction("sales", "CatalogAgent", ""),
            name: "CatalogAgent")
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    AIJsonUtilities.CreateJsonSchema(typeof(CatalogResult))),
                Tools =
                [
                    AIFunctionFactory.Create(catalogTools.SearchProducts),
                    AIFunctionFactory.Create(catalogTools.GetCategories),
                    AIFunctionFactory.Create(storeTools.GetDeliveryMethods),
                    AIFunctionFactory.Create(storeTools.GetStoreInfo),
                    AIFunctionFactory.Create(campaignTools.GetActiveCampaigns)
                ]
            }
        });
    }
}
