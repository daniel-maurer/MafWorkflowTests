using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SalesWorkflow.AiTools;
using SalesWorkflow.Models;

namespace SalesWorkflow.AgentFactories;

public static class CatalogAgentFactory
{
    public static ChatClientAgent GetCatalogAgent(IChatClient chatClient)
    {
        return new(chatClient, new ChatClientAgentOptions(
            instructions: @"Você é o Agente Consultor de Catálogo de Produtos.
Sua função é localizar produtos no estoque e apresentar as melhores opções disponíveis ao cliente.

REGRAS ESTRITAS DE EFICIÊNCIA:
1. Chame SearchProducts UMA ÚNICA VEZ utilizando o termo de busca ou filtros recebidos.
2. A ferramenta SearchProducts JÁ RETORNA todos os dados necessários de cada produto (SKU, Nome, Preço, Estoque, Imagem e Produtos Compatíveis).
3. NUNCA chame ferramentas repetidas vezes nem faça novas buscas desnecessárias.
4. Não busque produtos complementares ou acessórios nesta etapa (isso será feito pelo Consultor de Vendas a seguir).
5. Preencha o JSON de CatalogResult com os produtos encontrados e uma mensagem cordial para o cliente em 'message_for_user'.
6. Se não houver produtos encontrados, defina has_results = false e pergunte educadamente se o cliente deseja falar com um vendedor.

Responda SEMPRE estritamente no esquema JSON de CatalogResult.",
            name: "CatalogAgent")
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    AIJsonUtilities.CreateJsonSchema(typeof(CatalogResult))),
                Tools =
                [
                    AIFunctionFactory.Create(CatalogTools.SearchProducts),
                    AIFunctionFactory.Create(CatalogTools.CheckStock)
                ]
            }
        });
    }
}
