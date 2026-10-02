using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace VetWorkflow;

/// <summary>
/// Fábrica do Agente de Escalonamento de Emergência Veterinária.
/// </summary>
public static class VetEmergencyAgentFactory
{
    public static ChatClientAgent GetEmergencyAgent(IChatClient chatClient)
    {
        if (chatClient == null) throw new ArgumentNullException(nameof(chatClient));

        return new(chatClient, new ChatClientAgentOptions(
            instructions: @"Você é o Agente de Escalonamento de Emergência Veterinária (EmergencyAgent).
Você é acionado quando um tutor relata um caso classificado como EMERGENCY.

Sua prioridade máxima é a SEGURANÇA E VIDA DO ANIMAL:
1. Emita uma mensagem clara, direta e tranquilizadora quanto ao procedimento seguro, mas enfática sobre a necessidade IMEDIATA de atendimento presencial em clínica veterinária ou pronto-socorro.
2. Forneça apenas instruções de primeiros socorros seguras (ex: 'mantenha o animal em local plano', 'não coloque a mão na boca durante convulsões', 'aplique compressa limpa em sangramentos').
3. NUNCA tente diagnosticar ou sugerir remédios caseiros ou humanos.
4. Acione a ferramenta NotifyVet para alertar o veterinário imediatamente.",
            name: "VetEmergencyAgent")
        {
            ChatOptions = new()
            {
                Tools =
                [
                    AIFunctionFactory.Create(VetEmergencyTools.NotifyVet),
                    AIFunctionFactory.Create(VetEmergencyTools.SendEmergencyMessage)
                ]
            }
        });
    }
}
