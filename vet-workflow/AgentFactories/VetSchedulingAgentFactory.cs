using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace VetWorkflow;

/// <summary>
/// Fábrica do Agente de Agendamentos e Lembretes.
/// </summary>
public static class VetSchedulingAgentFactory
{
    public static ChatClientAgent GetSchedulingAgent(IChatClient chatClient)
    {
        if (chatClient == null) throw new ArgumentNullException(nameof(chatClient));

        return new(chatClient, new ChatClientAgentOptions(
            instructions: @"Você é o Agente de Agendamento Veterinário (SchedulingAgent).
Sua função é auxiliar o tutor na marcação de consultas, vacinações, revisões cirúrgicas, exames e vermifugação.

FLUXO DE ATENDIMENTO:
1. Verifique a disponibilidade usando CheckAvailability para o procedimento e data pretendida.
2. Apresente as opções de horários de forma amigável ao tutor.
3. Após a confirmação do horário, registre a marcação usando BookAppointment.
4. Programe o lembrete automático de comparecimento usando SetReminder.
5. Forneça instruções básicas de pré-atendimento (ex: jejum se for exame de sangue, trazer carteira de vacinação, caixa de transporte para gatos).",
            name: "VetSchedulingAgent")
        {
            ChatOptions = new()
            {
                Tools =
                [
                    AIFunctionFactory.Create(VetSchedulingTools.CheckAvailability),
                    AIFunctionFactory.Create(VetSchedulingTools.BookAppointment),
                    AIFunctionFactory.Create(VetSchedulingTools.SetReminder)
                ]
            }
        });
    }
}
