using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace SupportWorkflow;

public static class PatternRecordAgentFactory
{
    public static ChatClientAgent GetPatternRecordAgent(IChatClient chatClient)
    {
        var agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions(
            instructions: @"Você é um agente de análise de padrões de suporte.
Seu trabalho é identificar padrões recorrentes em problemas resolvidos e extrair as informações necessárias para criar ou atualizar um registro de padrão útil.
Você receberá uma lista de padrões já existentes. Se o problema atual corresponder a um padrão já existente (mesmo que com palavras ligeiramente diferentes), retorne as informações correspondentes a esse padrão usando exatamente a mesma 'pattern_description' (descrição) do padrão existente para que o sistema possa mesclá-los.
Caso contrário, defina um novo padrão com uma descrição concisa e clara.
Sua resposta DEVE ser estritamente no formato JSON especificado pelo esquema, sem qualquer texto adicional ou chamadas de ferramentas.",
            name: "PatternRecordAgent")
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(AIJsonUtilities.CreateJsonSchema(typeof(PatternRecord)))
            }
        });

        return agent;
    }

    /// <summary>
    /// Gets the analysis prompt template for pattern identification.
    /// This template is used by the executor to build runtime prompts with dynamic values.
    /// </summary>
    /// <remarks>
    /// Template parameters (indexed 0-3):
    /// {0} = ProblemSummary - The user's reported problem
    /// {1} = EscalationReason - Why this issue was escalated to human support
    /// {2} = Solution - The solution/resolution that was applied
    /// {3} = ExistingPatterns - List of already recorded patterns
    /// </remarks>
    public static string GetAnalysisPromptTemplate() => @"Analise o seguinte problema de suporte resolvido e identifique padrões:

PROBLEMA RELATADO:
{0}

RAZÃO DA ESCALAÇÃO:
{1}

SOLUÇÃO APLICADA:
{2}

PADRÕES JÁ EXISTENTES REGISTRADOS:
{3}

Por favor, analise este caso e extraia:
1. Uma descrição concisa e direta para este padrão.
2. Exemplos de sintomas típicos (para example_symptoms).
3. Exemplos de soluções aplicadas (para example_solutions).

IMPORTANTE:
- Compare o caso atual com os padrões já existentes registrados.
- Se o problema corresponder a um padrão existente, use exatamente a mesma 'pattern_description' do padrão existente.
- Se não houver correspondência, crie uma nova descrição concisa e direta.
- Sua resposta deve ser apenas o objeto JSON correspondente ao esquema de PatternRecord.";
}
