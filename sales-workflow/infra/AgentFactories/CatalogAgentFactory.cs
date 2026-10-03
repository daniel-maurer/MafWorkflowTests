using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SalesWorkflow.AiTools;
using SalesWorkflow.Models;

namespace SalesWorkflow.AgentFactories;

public static class CatalogAgentFactory
{
    public static ChatClientAgent GetCatalogAgent(IChatClient chatClient, CatalogTools catalogTools)
    {
        return new(chatClient, new ChatClientAgentOptions(
            instructions: @"Você é o Agente Consultor de Catálogo de Produtos.
Sua função é localizar produtos no estoque via busca semântica (RAG pgvector) e apresentar as melhores opções disponíveis ao cliente.

REGRAS ESTRITAS DE BUSCA NO CATÁLOGO:
1. Você pode realizar no máximo 5 buscas (SearchProducts) usando palavras ou variações semânticas.
2. NUNCA repita a mesma palavra ou termo de busca que você já pesquisou.
3. Se encontrar produtos relevantes, NÃO faça novas buscas: use os produtos encontrados imediatamente e gere a resposta final.
4. Se após no máximo 5 tentativas com termos diferentes você NÃO encontrar o produto, encerre a busca:
   - Defina has_results = false.
   - Defina requires_human = true.
   - Escreva em 'message_for_user' que o produto não foi localizado e ofereça atendimento com um consultor humano.
5. Conclua imediatamente gerando o JSON de CatalogResult. Não tente buscar infinitamente.

Responda SEMPRE estritamente no esquema JSON de CatalogResult.",
            name: "CatalogAgent")
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    AIJsonUtilities.CreateJsonSchema(typeof(CatalogResult))),
                Tools =
                [
                    AIFunctionFactory.Create(catalogTools.SearchProducts),
                    AIFunctionFactory.Create(catalogTools.CheckStock)
                ]
            }
        });
    }
}
