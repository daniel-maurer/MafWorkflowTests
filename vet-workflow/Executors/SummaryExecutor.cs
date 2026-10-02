using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace VetWorkflow;

/// <summary>
/// Executor responsável por consolidar o resumo clínico-administrativo pré-consulta e atualizar o painel de pendências.
/// </summary>
internal sealed class SummaryExecutor : Executor<VetWorkflowContext, ClinicalSummary>
{
    private readonly AIAgent _summaryAgent;
    private readonly IUserInteractor _userInteractor;

    public SummaryExecutor(AIAgent summaryAgent, IUserInteractor userInteractor) : base("SummaryExecutor")
    {
        _summaryAgent = summaryAgent ?? throw new ArgumentNullException(nameof(summaryAgent));
        _userInteractor = userInteractor ?? throw new ArgumentNullException(nameof(userInteractor));
    }

    public override async ValueTask<ClinicalSummary> HandleAsync(VetWorkflowContext workflowContext, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        await _userInteractor.PublishAgentStateAsync("summary", "active", "Gerando Resumo", cancellationToken);
        await _userInteractor.PublishTraceAsync("[Resumo Clínico] Consolidando síntese do atendimento para o prontuário...", "info", cancellationToken);

        string patientId = string.IsNullOrWhiteSpace(workflowContext.Patient.PatientId) 
            ? "PAT-PENDING" 
            : workflowContext.Patient.PatientId;

        var pendingActions = new List<string>();
        if (workflowContext.Triage.Urgency == "EMERGENCY")
        {
            pendingActions.Add("ATENDIMENTO IMEDIATO: Paciente orientado a comparecer ao pronto-socorro");
        }
        else if (workflowContext.Appointment is not null)
        {
            pendingActions.Add($"Agendamento confirmado para {workflowContext.Appointment.ScheduledDateTime} ({workflowContext.Appointment.AppointmentType})");
        }
        else if (workflowContext.HandedOffToVet)
        {
            pendingActions.Add("Atendimento finalizado pelo veterinário no chat");
        }

        string vetGuidancePart = !string.IsNullOrWhiteSpace(workflowContext.VetInstructions)
            ? $"\n• **Orientação do Veterinário:** {workflowContext.VetInstructions}"
            : string.Empty;

        var summary = new ClinicalSummary
        {
            PatientId = patientId,
            SessionId = workflowContext.SessionId,
            Patient = workflowContext.Patient,
            TriageUrgency = workflowContext.Triage.Urgency,
            Theme = workflowContext.Triage.Theme,
            ChiefComplaint = workflowContext.InitialUserMessage,
            ConversationSummary = $"Animal: {workflowContext.Patient.PetName} ({workflowContext.Patient.Species}, {workflowContext.Patient.Age ?? "idade n/i"}). Motivo: {workflowContext.Triage.Theme}. Urgência: {workflowContext.Triage.Urgency}.",
            PendingActions = pendingActions
        };

        workflowContext.Summary = summary;

        // Atualiza ferramentas
        await VetSummaryTools.GenerateClinicalSummary(patientId, workflowContext.SessionId, cancellationToken);
        await VetSummaryTools.UpdatePendingDashboard(patientId, workflowContext.Triage.Urgency, summary.ConversationSummary, cancellationToken);

        await _userInteractor.PublishTraceAsync($"[Prontuário Finalizado] Resumo pronto para o prontuário de {workflowContext.Patient.PetName}", "success", cancellationToken);

        // Envia mensagem estruturada no painel do veterinário/atendente
        string attendantSummaryCard = $"📝 **Resumo do Atendimento (Prontuário):**\n" +
            $"• **Paciente:** {workflowContext.Patient.PetName} ({workflowContext.Patient.Species} - {workflowContext.Patient.Breed ?? "SRD"})\n" +
            $"• **Classificação:** {workflowContext.Triage.Urgency} ({workflowContext.Triage.Theme})\n" +
            $"• **Relato Inicial:** {workflowContext.Patient.Symptoms ?? workflowContext.InitialUserMessage}" +
            vetGuidancePart + "\n" +
            $"• **Status / Ações:** {string.Join("; ", pendingActions)}";

        await _userInteractor.SendUserResponseAsync(
            attendantSummaryCard,
            agentId: "summary",
            audience: MessageAudience.Attendant,
            cancellationToken: cancellationToken);

        await _userInteractor.PublishAgentStateAsync("summary", "done", "Concluído", cancellationToken);

        await context.YieldOutputAsync(summary, cancellationToken);
        return summary;
    }
}
