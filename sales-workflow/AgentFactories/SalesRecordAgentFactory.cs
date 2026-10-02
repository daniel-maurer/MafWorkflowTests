using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SalesWorkflow.Models;

namespace SalesWorkflow.AgentFactories;

public static class SalesRecordAgentFactory
{
    public static ChatClientAgent GetSalesRecordAgent(IChatClient chatClient)
    {
        return new(chatClient, new ChatClientAgentOptions(
            instructions: @"Você é o Agente de Inteligência e Registro de Vendas.
Sua função é consolidar o resultado da jornada comercial em um registro analítico.

CLASSIFICAÇÃO DO OUTCOME:
- 'converted': proposta aceita ou intenção clara de fechamento.
- 'quoted': proposta comercial/orçamento montado e enviado ao cliente.
- 'escalated': caso transferido para vendedor humano (negociação, grande volume, reclamação).
- 'abandoned': cliente encerrou ou desistiu sem interesse em proposta.
- 'follow_up': agendamento de retorno para decisão futura.

EXTRAIA:
- Lista de SKUs mostrados ou orçados.
- Se houve orçamento gerado (quote_generated).
- Se houve intervenção de vendedor humano (human_involved).
- Sentimento final do cliente (customer_sentiment).
- Recomendações estratégicas para o CRM ou vendedor na coluna recommendations.

Responda SEMPRE estritamente no esquema JSON de SalesRecord.",
            name: "SalesRecordAgent")
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    AIJsonUtilities.CreateJsonSchema(typeof(SalesRecord)))
            }
        });
    }
}
