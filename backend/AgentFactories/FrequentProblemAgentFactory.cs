using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace SupportWorkflow;

/// <summary>
/// Factory for creating the frequent problem detection agent that matches problems against known issues.
/// </summary>
public class FrequentProblemAgentFactory
{
    /// <summary>
    /// Creates an agent configured to identify and resolve frequent/known problems.
    /// </summary>
    /// <param name="chatClient">The chat client for the agent to use</param>
    /// <returns>A configured ChatClientAgent for frequent problem detection</returns>
    public static ChatClientAgent GetFrequentProblemAgent(IChatClient chatClient)
    {
        if (chatClient == null)
        {
            throw new ArgumentNullException(nameof(chatClient), "Chat client cannot be null");
        }

        return new(
            chatClient,
            new ChatClientAgentOptions(
                instructions: @"És um agente de suporte.
                    A tua tarefa é analisar o resumo do problema do cliente, e verificar se é um dos problemas conhecidos.
                    O teu objetivo é encontrar se o problema está listado nos problemas conhecidos através de palavras chaves.

                    - Se o problema é conhecido, informe o próximo agente as ações necessárias para resolve-lo.
                    - Se não tiver solução informe se possui um prazo de solução cadastrado.
                    - Se o problema é complexo, informe que será atendido por um humano.",
                name: "FrequentProblemAgent"
            )
            {
                ChatOptions = new()
                {
                    ResponseFormat = ChatResponseFormat.ForJsonSchema(AIJsonUtilities.CreateJsonSchema(typeof(FrequentProblemResult))),
                    Tools = [AIFunctionFactory.Create(FrequentProblemTools.GetKnownIssuesAsync)]
                }
            }
        );
    }
}