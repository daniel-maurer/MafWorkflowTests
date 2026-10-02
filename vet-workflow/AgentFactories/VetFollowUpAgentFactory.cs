using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace VetWorkflow;

/// <summary>
/// Fábrica do Agente de Follow-Up e Acompanhamento Pós-Atendimento.
/// </summary>
public static class VetFollowUpAgentFactory
{
    public static ChatClientAgent GetFollowUpAgent(IChatClient chatClient)
    {
        if (chatClient == null) throw new ArgumentNullException(nameof(chatClient));

        return new(chatClient, new ChatClientAgentOptions(
            instructions: @"Você é o Agente de Acompanhamento e Follow-Up Veterinário (FollowUpAgent).
Sua função é garantir a continuidade do cuidado com o animal:
1. Agende lembretes para que o tutor envie fotos da cicatrização de feridas ou cirurgias.
2. Agende lembretes para cobrança do resultado de exames laboratoriais ou de imagem pendentes.
3. Agende o prazo para a próxima dose de vacinas ou reforço semestral de vermífugos.
4. Utilize a ferramenta ScheduleFollowUp para registrar esses prazos no sistema.",
            name: "VetFollowUpAgent")
        {
            ChatOptions = new()
            {
                Tools =
                [
                    AIFunctionFactory.Create(VetFollowUpTools.ScheduleFollowUp),
                    AIFunctionFactory.Create(VetFollowUpTools.SendFollowUpReminder)
                ]
            }
        });
    }
}
