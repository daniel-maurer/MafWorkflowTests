using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace VetWorkflow;

/// <summary>
/// Executor responsável pela triagem de urgência (Emergência, Urgente ou Rotina) e identificação do tema.
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

        // 1. Checagem direta rápida de segurança por ferramentas determinísticas
        string triageToolResult = await VetTriageTools.ClassifyUrgency(userMessage, cancellationToken);
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

        // 2. Consulta ao LLM para enriquecer classificação e tema
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, userMessage)
        };

        var response = await _triageAgent.RunAsync(history, cancellationToken: cancellationToken);
        VetTriageResult triageResult;

        if (AgentResponseParser.TryDeserializeAgentResponse<VetTriageResult>(response.Text, out var parsedResult) && parsedResult != null)
        {
            triageResult = parsedResult;
            // Se a ferramenta determinística detectou emergência, ela se sobrepõe
            if (urgency == "EMERGENCY")
            {
                triageResult.Urgency = "EMERGENCY";
                triageResult.AlertSignsDetected = alertSigns.ToArray();
            }
        }
        else
        {
            triageResult = new VetTriageResult
            {
                Urgency = urgency,
                Theme = "sintoma",
                AlertSignsDetected = alertSigns.ToArray(),
                Confidence = 0.9,
                Reasoning = "Classificação automática por regras de triagem rápida.",
                IsUnderstood = true,
                Summary = userMessage
            };
        }

        await _userInteractor.SetAgentTypingAsync("Triagem Veterinária analisando...", false, cancellationToken);

        string level = triageResult.Urgency == "EMERGENCY" ? "error" : (triageResult.Urgency == "URGENT" ? "warning" : "success");
        await _userInteractor.PublishTraceAsync($"[Triagem Concluída] Urgência: {triageResult.Urgency} | Tema: {triageResult.Theme}", level, cancellationToken);
        await _userInteractor.PublishAgentStateAsync("vet-triage", "done", triageResult.Urgency, cancellationToken);

        var vetContext = new VetWorkflowContext
        {
            InitialUserMessage = userMessage,
            Triage = triageResult
        };

        await context.YieldOutputAsync(vetContext, cancellationToken);
        return vetContext;
    }
}
