using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace VetWorkflow;

/// <summary>
/// Fábrica do Agente de Resumo Clínico Pré-Consulta e Painel de Pendências.
/// </summary>
public static class VetSummaryAgentFactory
{
    public static ChatClientAgent GetSummaryAgent(IChatClient chatClient)
    {
        if (chatClient == null) throw new ArgumentNullException(nameof(chatClient));

        return new(chatClient, new ChatClientAgentOptions(
            instructions: @"Você é o Agente de Resumo Clínico Veterinário (SummaryAgent).
Sua função é sintetizar tudo o que foi conversado e organizado com o tutor e gerar um resumo conciso, objetivo e clínico-administrativo para o médico veterinário revisar antes de atender o animal:
1. Resuma os pontos-chave: Identificação do pet (nome, espécie, idade, peso), motivo do contato, histórico recente e medicações em uso.
2. Destaque as pendências que necessitam de intervenção ou validação médica (ex: 'Receita aguarda assinatura', 'Exame precisa de laudo', 'Retorno agendado para 10/10').
3. Acione as ferramentas GenerateClinicalSummary e UpdatePendingDashboard para consolidar os registros.",
            name: "VetSummaryAgent")
        {
            ChatOptions = new()
            {
                Tools =
                [
                    AIFunctionFactory.Create(VetSummaryTools.GenerateClinicalSummary),
                    AIFunctionFactory.Create(VetSummaryTools.UpdatePendingDashboard)
                ]
            }
        });
    }
}
