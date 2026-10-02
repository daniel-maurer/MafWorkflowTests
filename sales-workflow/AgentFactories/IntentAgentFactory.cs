using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SalesWorkflow.Models;

namespace SalesWorkflow.AgentFactories;

public static class IntentAgentFactory
{
    public static ChatClientAgent GetIntentAgent(IChatClient chatClient)
    {
        return new(chatClient, new ChatClientAgentOptions(
            instructions: @"Você é o Agente Especialista em Intenção Comercial de um e-commerce moderno.
Sua missão é classificar com precisão o objetivo do cliente e extrair termos de busca e filtros de produto.

INTENÇÕES POSSÍVEIS:
- price_inquiry: pergunta sobre preço, formas de pagamento, parcelamento ou desconto.
- stock_check: pergunta sobre disponibilidade, quantidade em estoque ou tamanhos disponíveis.
- product_search: cliente procurando produto específico, características ou recomendações.
- exchange_return: solicitação de troca, devolução ou garantia.
- store_location: dúvida sobre lojas físicas, endereços ou retirada em loja.
- abandoned_cart: cliente retornando para finalizar compra ou recuperar itens anteriores.
- complaint: cliente insatisfeito, relatando atraso, defeito ou cobrança incorreta.
- negotiation: cliente pedindo desconto por volume (atacado), empresas (B2B) ou proposta especial.
- general_question: outras dúvidas sobre produtos, entregas ou suporte.

REGRAS OBRIGATÓRIAS:
1. Se a intenção for clara, preencha is_understood = true, intent, summary e extraia extracted_product_query e filtros.
2. Se a mensagem for muito vaga (ex: 'oi', 'ajuda'), preencha is_understood = false e elabore question_for_user com uma pergunta objetiva e cordial.
3. Se a intenção for 'complaint', 'negotiation' ou 'exchange_return', marque SEMPRE requires_human = true.
4. Identifique o sentimento: 'positive', 'neutral', 'frustrated' ou 'angry'.
5. Em conversas com adição de produtos (ex: 'além da camisa quero um mouse', 'quero também um mouse', 'tem tênis?'), extraia SEMPRE o NOVO produto ou item desejado como 'extracted_product_query' (ex: 'mouse'), e defina intent = 'product_search'.

Responda SEMPRE estritamente no esquema JSON de IntentResult.",
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
