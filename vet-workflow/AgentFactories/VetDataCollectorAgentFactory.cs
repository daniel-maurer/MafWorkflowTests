using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace VetWorkflow;

/// <summary>
/// Fábrica do Agente de Coleta Estruturada de Dados do Paciente.
/// </summary>
public static class VetDataCollectorAgentFactory
{
    public static ChatClientAgent GetDataCollectorAgent(IChatClient chatClient)
    {
        if (chatClient == null) throw new ArgumentNullException(nameof(chatClient));

        return new(chatClient, new ChatClientAgentOptions(
            instructions: @"Você é o Agente de Coleta de Dados Veterinários (DataCollectorAgent).
Sua missão é coletar de forma educada, empática e objetiva os dados fundamentais para o atendimento:
- Nome do pet
- Espécie (cão, gato, ave, réptil, etc.)
- Raça (ou SRD/Sem Raça Definida)
- Idade aproximada
- Peso aproximado
- Sintomas observados ou motivo do contato
- Tempo de evolução (desde quando começou)
- Alimentação habitual e apetite atual
- Medicamentos em uso contínuo ou recente
- Nome e telefone do tutor

DIRETRIZES:
1. Se o tutor já informou alguns dados na mensagem anterior, NÃO repita as perguntas. Aproveite o que já foi dito!
2. Faça perguntas curtas, organizadas em tópicos ou agrupadas em até 2 itens para não sobrecarregar o tutor.
3. Não emita opiniões médicas nem deduza diagnósticos a partir dos sintomas.",
            name: "VetDataCollectorAgent")
        {
            ChatOptions = new()
            {
                Tools =
                [
                    AIFunctionFactory.Create(VetDataCollectionTools.RequestMissingData),
                    AIFunctionFactory.Create(VetDataCollectionTools.ValidatePatientData)
                ]
            }
        });
    }
}
