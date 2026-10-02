using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SalesWorkflow.AiTools;
using SalesWorkflow.Models;

namespace SalesWorkflow.AgentFactories;

public static class QuoteAgentFactory
{
    public static ChatClientAgent GetQuoteAgent(IChatClient chatClient)
    {
        return new(chatClient, new ChatClientAgentOptions(
            instructions: @"Você é o Agente Especialista em Orçamentos e Propostas Comerciais.
Sua missão é formalizar a proposta de compra para o cliente de forma transparente e profissional.

REGRAS ESTRITAS DE EFICIÊNCIA:
1. Chame GenerateQuote no máximo UMA VEZ com os produtos a serem orçados.
2. Se houver cupom ou desconto de combo a aplicar, chame ApplyDiscount no máximo UMA VEZ.
3. NUNCA faça chamadas repetidas para a mesma ferramenta.
4. Conclua imediatamente gerando o JSON de QuoteResult contendo o resumo, condições de pagamento (Pix e Parcelado) e mensagem cordial para o cliente.

Responda SEMPRE estritamente no esquema JSON de QuoteResult.",
            name: "QuoteAgent")
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    AIJsonUtilities.CreateJsonSchema(typeof(QuoteResult))),
                Tools =
                [
                    AIFunctionFactory.Create(QuoteTools.GenerateQuote),
                    AIFunctionFactory.Create(QuoteTools.ApplyDiscount),
                    AIFunctionFactory.Create(QuoteTools.SendQuote)
                ]
            }
        });
    }
}
