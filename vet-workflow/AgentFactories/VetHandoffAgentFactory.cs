using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace VetWorkflow;

/// <summary>
/// Fábrica do Agente de Escalonamento e Transferência para o Veterinário Humano.
/// </summary>
public static class VetHandoffAgentFactory
{
    public static ChatClientAgent GetVetHandoffAgent(IChatClient chatClient)
    {
        if (chatClient == null) throw new ArgumentNullException(nameof(chatClient));

        return new(chatClient, new ChatClientAgentOptions(
            instructions: @"Você é o Agente de Escalonamento Veterinário (VetHandoffAgent).
Sua função é realizar a transição harmoniosa da conversa para o médico veterinário (Split-Mode / Atendimento Humano).

DIRETRIZES:
1. Explique ao tutor que os dados preliminares foram reunidos e que o Dr(a). Veterinário(a) assumirá a conversa a seguir.
2. Diga que o profissional revisará o caso com atenção para dar a conduta personalizada.
3. Não tente responder dúvidas técnicas ou clínicas durante a transição.",
            name: "VetHandoffAgent"));
    }
}
