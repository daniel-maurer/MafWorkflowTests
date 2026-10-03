using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SalesWorkflow.AiTools;
using SalesWorkflow.Models;

namespace SalesWorkflow.AgentFactories;

public static class FollowUpAgentFactory
{
    public static ChatClientAgent GetFollowUpAgent(IChatClient chatClient)
    {
        return new(chatClient, new ChatClientAgentOptions(
            instructions: @"Você é o Agente de Follow-Up e Recuperação de Vendas.
Sua missão é garantir que propostas, dúvidas ou carrinhos abandonados tenham um retorno planejado.

REGRAS:
1. Para orçamentos gerados, agende um lembrete em 24 horas usando ScheduleFollowUp com followUpType = 'quote_reminder'.
2. Para clientes que retomaram carrinho abandonado, agende follow-up em 2 horas ('cart_recovery').
3. Para produtos fora de estoque com interesse do cliente, agende para 48 horas ('restock_notification').
4. Pergunte gentilmente o canal de preferência do cliente (WhatsApp ou Email).
5. Se for o caso de recuperação de carrinho anterior, utilize GetAbandonedCart ou SendCartReminder.

Responda SEMPRE estritamente no esquema JSON de FollowUpResult.",
            name: "FollowUpAgent")
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    AIJsonUtilities.CreateJsonSchema(typeof(FollowUpResult))),
                Tools =
                [
                    AIFunctionFactory.Create(FollowUpTools.ScheduleFollowUp),
                    AIFunctionFactory.Create(CartTools.GetAbandonedCart),
                    AIFunctionFactory.Create(CartTools.SendCartReminder)
                ]
            }
        });
    }
}
