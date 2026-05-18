using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace SupportWorkflow;

public static class PatternRecordAgentFactory
{
    public static ChatClientAgent GetPatternRecordAgent(IChatClient chatClient) =>
        new(chatClient, new ChatClientAgentOptions(
            instructions: @"Você é um agente de análise de padrões de suporte.
Seu trabalho é identificar padrões recorrentes em problemas resolvidos e extrair:
- Tipo de padrão
- Descrição clara
- Palavras-chave
- Características temporais
- Sintomas típicos
- Solução sugerida
- Taxa de sucesso esperada
- Se está pronto para automação

Responda em JSON usando o esquema fornecido. Seja específico e forneça valores válidos.",
            name: "PatternRecordAgent")
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(AIJsonUtilities.CreateJsonSchema(typeof(PatternRecordResult)))
            }
        });
}
