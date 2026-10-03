using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SalesWorkflow.AiTools;
using SalesWorkflow.Models;

namespace SalesWorkflow.AgentFactories;

public static class QuoteAgentFactory
{
    public static ChatClientAgent GetQuoteAgent(IChatClient chatClient, QuoteTools quoteTools)
    {
        return new(chatClient, new ChatClientAgentOptions(
            instructions: @"Você é o Agente Especialista em Orçamentos e Propostas Comerciais.
Sua missão é formalizar a proposta de compra para o cliente de forma transparente e profissional.

REGRAS ESTRITAS DE EFICIÊNCIA:
1. Chame GenerateQuote com os produtos e cupom de desconto informado pelo cliente (se houver).
2. As condições de pagamento e descontos são calculados automaticamente com base nas regras ativas do sistema. NÃO invente regras de parcelamento ou descontos não autorizados.
3. Se o cliente solicitar ver as formas de pagamento disponíveis, você pode consultar GetPaymentConditions.
4. Conclua imediatamente gerando o JSON de QuoteResult contendo o resumo, condições de pagamento e mensagem cordial para o cliente.

Responda SEMPRE estritamente no esquema JSON de QuoteResult.",
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
