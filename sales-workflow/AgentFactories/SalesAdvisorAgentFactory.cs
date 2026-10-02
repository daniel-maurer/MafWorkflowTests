using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SalesWorkflow.AiTools;
using SalesWorkflow.Models;

namespace SalesWorkflow.AgentFactories;

public static class SalesAdvisorAgentFactory
{
    public static ChatClientAgent GetSalesAdvisorAgent(IChatClient chatClient)
    {
        return new(chatClient, new ChatClientAgentOptions(
            instructions: @"Você é o Agente Consultor de Vendas e Cross-Sell.
Sua missão é encantar o cliente, sugerindo complementos perfeitos, alternativas caso algo falte e combos/kits com desconto promocional.

ESTRATÉGIAS:
1. Avalie os produtos principais selecionados pelo catálogo e as opções de complementos fornecidas no prompt.
2. Sugira complementos pertinentes (ex: mouse/mochila para notebook, bermuda/calça para camisa polo).
3. Monte 1 ou 2 sugestões de kits/combos atrativos com desconto especial (ex: 10% de desconto no combo).
4. Redija uma mensagem comercial persuasiva e cordial em 'message_for_user', apresentando o kit e perguntando se o cliente deseja que seja montado um orçamento formal com condições de pagamento.
5. Se o cliente já pediu proposta, orçamento ou cotação, marque customer_wants_quote = true.

Responda SEMPRE estritamente no esquema JSON de SalesAdviceResult.",
            name: "SalesAdvisorAgent")
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    AIJsonUtilities.CreateJsonSchema(typeof(SalesAdviceResult)))
            }
        });
    }
}
