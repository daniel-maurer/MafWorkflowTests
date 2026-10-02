using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace VetWorkflow;

/// <summary>
/// Executor responsável pela triagem de urgência (Emergência, Urgente ou Rotina) e identificação do tema.
/// Realiza esclarecimento iterativo caso o tutor envie apenas saudações ou mensagens vagas.
/// </summary>
internal sealed class VetTriageExecutor : Executor<string, VetWorkflowContext>
{
    private readonly AIAgent _triageAgent;
    private readonly IUserInteractor _userInteractor;

    public VetTriageExecutor(AIAgent triageAgent, IUserInteractor userInteractor) : base("VetTriageExecutor")
    {
        _triageAgent = triageAgent ?? throw new ArgumentNullException(nameof(triageAgent));
        _userInteractor = userInteractor ?? throw new ArgumentNullException(nameof(userInteractor));
    }

    public override async ValueTask<VetWorkflowContext> HandleAsync(string userMessage, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        await _userInteractor.PublishAgentStateAsync("vet-triage", "active", "Classificando", cancellationToken);
        await _userInteractor.SetAgentTypingAsync("Triagem Veterinária analisando...", true, cancellationToken);
        await _userInteractor.PublishTraceAsync($"[Triagem] Analisando mensagem do tutor: \"{userMessage}\"", "info", cancellationToken);

        Logger.LogInfo($"[VetTriageExecutor] Iniciando triagem da mensagem: {userMessage}");

        var history = new List<ChatMessage>
        {
            new(ChatRole.User, userMessage)
        };

        const int MaxClarificationAttempts = 3;
        int clarificationAttempts = 0;
        bool isUnderstood = false;
        VetTriageResult triageResult = new();
        string accumulatedUserMessages = userMessage;

        while (!isUnderstood && clarificationAttempts <= MaxClarificationAttempts)
        {
            // 1. Checagem direta rápida de segurança por ferramentas determinísticas a cada iteração
            string triageToolResult = await VetTriageTools.ClassifyUrgency(accumulatedUserMessages, cancellationToken);
            var directClassification = JsonSerializer.Deserialize<JsonElement>(triageToolResult);

            string urgency = directClassification.TryGetProperty("urgency", out var urgProp) ? urgProp.GetString() ?? "ROUTINE" : "ROUTINE";
            var alertSigns = new List<string>();
            if (directClassification.TryGetProperty("alertSignsDetected", out var signsProp) && signsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in signsProp.EnumerateArray())
                {
                    alertSigns.Add(item.GetString() ?? string.Empty);
                }
            }

            // Se ferramenta determinística detectou EMERGÊNCIA crítica com palavras-chave claras, encerra triagem imediatamente
            if (urgency == "EMERGENCY")
            {
                triageResult = new VetTriageResult
                {
                    Urgency = "EMERGENCY",
                    Theme = "sintoma",
                    AlertSignsDetected = alertSigns.ToArray(),
                    Confidence = 0.99,
                    Reasoning = "Sinais críticos de emergência identificados imediatamente na mensagem do tutor.",
                    IsUnderstood = true,
                    Summary = accumulatedUserMessages
                };
                isUnderstood = true;
                break;
            }

            // 2. Consulta ao LLM de Triagem
            var response = await _triageAgent.RunAsync(history, cancellationToken: cancellationToken);

            if (AgentResponseParser.TryDeserializeAgentResponse<VetTriageResult>(response.Text, out var parsedResult) && parsedResult != null)
            {
                triageResult = parsedResult;
            }
            else
            {
                triageResult = new VetTriageResult
                {
                    Urgency = urgency,
                    Theme = "administrativo",
                    AlertSignsDetected = alertSigns.ToArray(),
                    Confidence = 0.8,
                    Reasoning = "Classificação automática por regras de triagem.",
                    IsUnderstood = true,
                    Summary = accumulatedUserMessages
                };
            }

            // Se o agente compreendeu a solicitação (não é apenas saudação ou mensagem vaga), encerra loop
            if (triageResult.IsUnderstood)
            {
                isUnderstood = true;
                break;
            }

            // Caso precise de esclarecimento (ex: saudação 'Olá' ou solicitação vaga)
            await _userInteractor.SetAgentTypingAsync("Triagem Veterinária analisando...", false, cancellationToken);

            string questionToAsk = !string.IsNullOrWhiteSpace(triageResult.QuestionForUser)
                ? triageResult.QuestionForUser
                : "Olá! Tudo bem? Sou o assistente da clínica veterinária. Como posso ajudar você e seu pet hoje? Você gostaria de agendar uma consulta/vacina, relatar algum sintoma ou tirar dúvidas?";

            await _userInteractor.PublishTraceAsync("Solicitando detalhamento da necessidade do tutor...", "info", cancellationToken);

            history.Add(new ChatMessage(ChatRole.Assistant, questionToAsk));

            // Envia pergunta ao tutor no chat e aguarda a próxima mensagem interativa
            string nextUserMessage = await _userInteractor.GetUserResponseAsync(
                questionToAsk,
                agentId: "vet-triage",
                audience: MessageAudience.Both,
                cancellationToken: cancellationToken);

            await _userInteractor.SetAgentTypingAsync("Triagem Veterinária analisando...", true, cancellationToken);
            history.Add(new ChatMessage(ChatRole.User, nextUserMessage));
            accumulatedUserMessages += "\n" + nextUserMessage;
            clarificationAttempts++;
        }

        await _userInteractor.SetAgentTypingAsync("Triagem Veterinária analisando...", false, cancellationToken);

        string level = triageResult.Urgency == "EMERGENCY" ? "error" : (triageResult.Urgency == "URGENT" ? "warning" : "success");
        await _userInteractor.PublishTraceAsync($"[Triagem Concluída] Urgência: {triageResult.Urgency} | Tema: {triageResult.Theme}", level, cancellationToken);
        await _userInteractor.PublishAgentStateAsync("vet-triage", "done", triageResult.Urgency, cancellationToken);

        var vetContext = new VetWorkflowContext
        {
            InitialUserMessage = accumulatedUserMessages,
            Triage = triageResult
        };

        await context.YieldOutputAsync(vetContext, cancellationToken);
        return vetContext;
    }
}
