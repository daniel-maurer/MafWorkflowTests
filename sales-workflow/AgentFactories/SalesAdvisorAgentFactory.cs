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
2. Sugira complementos pertinentes (ex: tênis/calça para camisa polo, mouse/mochila para notebook).
3. Monte 1 ou 2 sugestões de kits/combos atrativos com desconto especial (ex: 10% de desconto no combo).
4. OBRIGATÓRIO: Use a ferramenta CalculateKitPrice para calcular os valores exatos de cada kit ou combo com desconto. NUNCA faça contas ou estimativas de cabeça.
5. Em 'suggested_kits' e em 'message_for_user', apresente EXATAMENTE os valores retornados por CalculateKitPrice (preço original, desconto e preço final do combo).
6. Redija uma mensagem comercial persuasiva e cordial em 'message_for_user', apresentando os kits com seus preços exatos e perguntando se o cliente deseja que seja montado um orçamento formal com condições de pagamento.
7. Defina customer_wants_quote = false inicialmente (a confirmação virá da resposta do cliente).

Responda SEMPRE estritamente no esquema JSON de SalesAdviceResult.",
            name: "SalesAdvisorAgent")
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    AIJsonUtilities.CreateJsonSchema(typeof(SalesAdviceResult))),
                Tools =
                [
                    AIFunctionFactory.Create(CatalogTools.CalculateKitPrice)
                ]
            }
        });
    }

    public static ChatClientAgent GetCustomerDecisionAgent(IChatClient chatClient)
    {
        return new(chatClient, new ChatClientAgentOptions(
            instructions: @"Você é o Agente Inteligente de Avaliação de Decisão do Cliente.
Sua missão é interpretar a resposta em linguagem natural do cliente após a proposta comercial e sugestão de kits/combos feita pelo consultor de vendas.

DIRETRIZES:
1. Analise o contexto da negociação (produtos principais, kits sugeridos, complementos e a proposta apresentada) e a resposta do cliente.
2. Identifique a intenção real do cliente com naturalidade e sensibilidade ao contexto comercial e expressões em português (ex: 'aceito o kit', 'quero o kit', 'pode mandar', 'fechado', 'sim', 'opção 1', 'só a camisa', 'não quero').
3. Se o cliente concordar com a proposta ou com o kit sugerido:
   - defina wants_quote = true
   - defina accepted_kit_name com o nome do kit aceito (se ele disse apenas 'o kit' ou 'aceito', atribua o primeiro kit sugerido)
   - liste os SKUs que compõem o kit em accepted_skus
   - atribua o discount_percent correspondente ao desconto do kit oferecido
4. Se o cliente preferir apenas o produto original ('só a camisa', 'sem kit', 'apenas o principal'):
   - defina wants_quote = true
   - defina accepted_kit_name = null
   - liste apenas o SKU do produto principal em accepted_skus
   - defina discount_percent = 0
   - defina wants_only_original = true
5. Se o cliente recusar expressamente ('não quero', 'cancela', 'deixa pra lá', 'agora não'):
   - defina wants_quote = false
   - defina accepted_skus = []
   - defina discount_percent = 0
6. Redija uma mensagem cordial e acolhedora em 'message_for_user' confirmando o que foi compreendido para o cliente.

Responda SEMPRE estritamente no esquema JSON de CustomerChoiceEvaluation.",
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
