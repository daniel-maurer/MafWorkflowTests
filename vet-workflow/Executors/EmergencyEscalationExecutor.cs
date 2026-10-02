using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace VetWorkflow;

/// <summary>
/// Executor responsável pelo escalonamento imediato de emergências médicas veterinárias.
/// Emite aviso prioritário ao profissional e mensagem de segurança padronizada ao tutor.
/// </summary>
internal sealed class EmergencyEscalationExecutor : Executor<VetWorkflowContext, VetWorkflowContext>
{
    private readonly AIAgent _emergencyAgent;
    private readonly IUserInteractor _userInteractor;

    public EmergencyEscalationExecutor(AIAgent emergencyAgent, IUserInteractor userInteractor) : base("EmergencyEscalationExecutor")
    {
        _emergencyAgent = emergencyAgent ?? throw new ArgumentNullException(nameof(emergencyAgent));
        _userInteractor = userInteractor ?? throw new ArgumentNullException(nameof(userInteractor));
    }

    public override async ValueTask<VetWorkflowContext> HandleAsync(VetWorkflowContext workflowContext, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        await _userInteractor.PublishAgentStateAsync("emergency-escalation", "active", "Escalonando", cancellationToken);
        await _userInteractor.PublishTraceAsync("🚨 CASO CRÍTICO DETECTADO: Acionando escalonamento de emergência", "error", cancellationToken);

        string signType = workflowContext.Triage.AlertSignsDetected.FirstOrDefault() ?? "emergencia_geral";

        // 1. Mensagem de segurança padronizada de emergência
        string safetyResult = await VetEmergencyTools.SendEmergencyMessage(signType, cancellationToken);
        string safetyText = "⚠️ EMERGÊNCIA MÉDICA DETECTADA: Por favor, dirija-se imediatamente com o seu animal à clínica veterinária com atendimento de emergência mais próxima. Mantenha o animal confortável e seguro. O médico veterinário de plantão já foi notificado.";

        try
        {
            var doc = JsonDocument.Parse(safetyResult);
            if (doc.RootElement.TryGetProperty("safetyMessage", out var msgProp) && !string.IsNullOrWhiteSpace(msgProp.GetString()))
            {
                safetyText = msgProp.GetString()!;
            }
        }
        catch { }

        // Sanitiza para garantir conformidade
        safetyText = VetSafetyGuardrail.SanitizeOrEscalate(safetyText, out _);

        // 2. Notificação ao veterinário
        await VetEmergencyTools.NotifyVet(
            workflowContext.Patient.PetName ?? "Paciente em Triagem",
            $"EMERGÊNCIA ({signType}): {workflowContext.InitialUserMessage}",
            cancellationToken);

        await _userInteractor.PublishTraceAsync("Alerta de emergência despachado para o veterinário", "error", cancellationToken);

        // 3. Enviar mensagem de segurança com destaque para o tutor
        await _userInteractor.SendUserResponseAsync(
            safetyText,
            agentId: "emergency-escalation",
            audience: MessageAudience.Both,
            cancellationToken: cancellationToken);

        // Ativa split mode / modo humano imediatamente
        await _userInteractor.PublishSplitModeAsync(true, cancellationToken);
        await _userInteractor.PublishContextAsync(
            "emergency",
            "Atendimento de Emergência",
            "Escalonamento imediato para atendimento presencial/veterinário.",
            "emergency-escalation",
            true,
            cancellationToken);

        await _userInteractor.PublishAgentStateAsync("emergency-escalation", "done", "Escalonado", cancellationToken);

        workflowContext.EmergencyAlertSent = true;
        workflowContext.HandedOffToVet = true;

        await context.YieldOutputAsync(workflowContext, cancellationToken);
        return workflowContext;
    }
}
