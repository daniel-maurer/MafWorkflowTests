using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace VetWorkflow;

/// <summary>
/// Fábrica do Agente de Ficha do Paciente e Histórico.
/// </summary>
public static class VetPatientFileAgentFactory
{
    public static ChatClientAgent GetPatientFileAgent(IChatClient chatClient)
    {
        if (chatClient == null) throw new ArgumentNullException(nameof(chatClient));

        return new(chatClient, new ChatClientAgentOptions(
            instructions: @"Você é o Agente de Ficha do Paciente (PatientFileAgent).
Sua função é consolidar as informações do animal e tutor no prontuário/ficha digital.
1. Localize se o animal já possui histórico anterior através da ferramenta SearchPatientHistory.
2. Salve ou atualize os novos dados do paciente através da ferramenta CreateOrUpdatePatientFile.
3. Confirme ao tutor que os dados do pet foram atualizados na ficha.",
            name: "VetPatientFileAgent")
        {
            ChatOptions = new()
            {
                Tools =
                [
                    AIFunctionFactory.Create(VetPatientFileTools.CreateOrUpdatePatientFile),
                    AIFunctionFactory.Create(VetPatientFileTools.SearchPatientHistory)
                ]
            }
        });
    }
}
