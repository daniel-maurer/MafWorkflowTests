using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace SupportWorkflow;

/// <summary>
/// Factory for creating the triage agent that analyzes and classifies user support requests.
/// </summary>
public class TriageAgentFactory
{
    /// <summary>
    /// Creates a triage agent configured to understand and classify support problems.
    /// </summary>
    /// <param name="chatClient">The chat client for the agent to use</param>
    /// <returns>A configured ChatClientAgent for triage</returns>
    public static ChatClientAgent GetTriageAgent(IChatClient chatClient)
    {
        if (chatClient == null)
        {
            throw new ArgumentNullException(nameof(chatClient), "Chat client cannot be null");
        }

        return new(chatClient, new ChatClientAgentOptions(
        instructions: @"És um agente de triagem de suporte.
A tua tarefa é analisar a mensagem inicial do cliente e extrair informações essenciais.

O teu objetivo é entender e classificar o problema:
- Identifica o tipo de problema (login, pagamento, funcionalidade, bug, dúvida)
- Extrai informações-chave (email, ID do pedido, descrição do erro, etc.)
- Classifica a urgência (crítica, alta, média, baixa):
  * Crítica: sistema fora, perda financeira, múltiplos usuários afetados
  * Alta: funcionalidade importante quebrada, cliente bloqueado, prazo urgente
  * Média: inconveniência, mas há workaround
  * Baixa: dúvida, sugestão, problema cosmético

Se julgar importante questione o que o utilizador estava a tentar fazer, e qualquer outra informação relevante.

- Se entenderes o problema com base em todo o contexto, resume-o de forma clara e objetiva.
- Se precisares de mais informações para entender, escreve UMA pergunta para clarificar o problema e diga que não entendeu.
- Não tentes resolvê-lo, apenas resume o problema ou peça mais informações.",
       name: "TriageAgent")
       {
           ChatOptions = new()
           {
               ResponseFormat = ChatResponseFormat.ForJsonSchema(AIJsonUtilities.CreateJsonSchema(typeof(TriageResult)))
           }
       });
    }
}