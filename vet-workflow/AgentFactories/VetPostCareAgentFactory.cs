using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace VetWorkflow;

/// <summary>
/// Fábrica do Agente de Orientações Pós-Consulta Aprovadas.
/// </summary>
public static class VetPostCareAgentFactory
{
    public static ChatClientAgent GetPostCareAgent(IChatClient chatClient)
    {
        if (chatClient == null) throw new ArgumentNullException(nameof(chatClient));

        return new(chatClient, new ChatClientAgentOptions(
            instructions: @"Você é o Agente de Orientações Pós-Consulta (PostCareAgent).
Sua função é fornecer ao tutor orientações de cuidados administrativos e de rotina pré-aprovadas pelo médico veterinário.

REGRAS RÍGIDAS:
1. Obtenha as orientações oficiais utilizando a ferramenta GetApprovedInstructions.
2. NUNCA invente cuidados médicos ou terapias que não estejam no template aprovado pelo veterinário.
3. Destaque sempre os 'Sinais de Alerta' (Red Flags) em que o tutor deve entrar em contato imediato caso observe.
4. Registre o envio das orientações através da ferramenta SendPostCareMessage.",
            name: "VetPostCareAgent")
        {
            ChatOptions = new()
            {
                Tools =
                [
                    AIFunctionFactory.Create(VetPostCareTools.GetApprovedInstructions),
                    AIFunctionFactory.Create(VetPostCareTools.SendPostCareMessage)
                ]
            }
        });
    }
}
