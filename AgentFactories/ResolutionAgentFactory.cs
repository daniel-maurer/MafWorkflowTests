using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace SupportWorkflow;

/// <summary>
/// Factory for creating the resolution agent that attempts to automatically resolve support issues.
/// </summary>
public class ResolutionAgentFactory
{
    /// <summary>
    /// Creates an agent configured to resolve support issues automatically using available tools.
    /// </summary>
    /// <param name="chatClient">The chat client for the agent to use</param>
    /// <returns>A configured ChatClientAgent for issue resolution</returns>
    public static ChatClientAgent GetResolutionAgent(IChatClient chatClient)
    {
        if (chatClient == null)
        {
            throw new ArgumentNullException(nameof(chatClient), "Chat client cannot be null");
        }

        return new(chatClient, new ChatClientAgentOptions(
        instructions: @"És um agente de resolução de suporte.
A tua tarefa é tentar resolver o problema do cliente automaticamente.

O teu objetivo é solucionar o problema usando as ferramentas disponíveis:
- Analisa a solução sugerida pelo agente de problemas frequentes
- Se tens acesso às ferramentas necessárias (MCP), executa as ações
- Explica ao cliente o que fizeste de forma clara e simples
- Confirma se o problema foi resolvido
- Se a ação falhar ou não tiveres a ferramenta necessária, informa que será escalado para humano

Problemas que PODES resolver sozinho:
- Reset de senha
- Desbloqueio de conta
- Consulta de status
- Reenvio de emails
- Verificações simples no sistema

Problemas que DEVEM ir para humano:
- Reembolsos acima de R$ 500
- Alterações de plano/contrato
- Bugs no sistema
- Solicitações de cancelamento
- Qualquer coisa que envolva decisão de negócio

Sempre pede confirmação ao cliente após resolver.",
       name: "ResolutionAgent")
       {
           ChatOptions = new()
           {
               ResponseFormat = ChatResponseFormat.ForJsonSchema(AIJsonUtilities.CreateJsonSchema(typeof(FrequentProblemResult))),
               Tools = 
               [
                   AIFunctionFactory.Create(ResolutionAgentTools.UnlockAccount),
                   AIFunctionFactory.Create(ResolutionAgentTools.SendEmail)
               ]
           }
       });
    }
}