using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace VetWorkflow;

/// <summary>
/// Executor responsável por programar lembretes e tarefas de continuidade do cuidado (follow-ups).
/// </summary>
internal sealed class FollowUpExecutor : Executor<VetWorkflowContext, VetWorkflowContext>
{
    private readonly AIAgent _followUpAgent;
    private readonly IUserInteractor _userInteractor;

    public FollowUpExecutor(AIAgent followUpAgent, IUserInteractor userInteractor) : base("FollowUpExecutor")
    {
        _followUpAgent = followUpAgent ?? throw new ArgumentNullException(nameof(followUpAgent));
        _userInteractor = userInteractor ?? throw new ArgumentNullException(nameof(userInteractor));
    }

    public override async ValueTask<VetWorkflowContext> HandleAsync(VetWorkflowContext workflowContext, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        await _userInteractor.PublishAgentStateAsync("follow-up", "active", "Programando Lembretes", cancellationToken);
        await _userInteractor.PublishTraceAsync("[Follow-Up] Configurando automação de continuidade do cuidado...", "info", cancellationToken);

        string patientId = string.IsNullOrWhiteSpace(workflowContext.Patient.PatientId) 
            ? "PAT-DEFAULT" 
            : workflowContext.Patient.PatientId;

        string followUpType;
        string scheduledWhen;

        if (workflowContext.Triage.Urgency == "EMERGENCY")
        {
            followUpType = "evolução_emergência";
            scheduledWhen = "+2h (Verificação de estabilização pós-chegada)";
        }
        else if (workflowContext.Triage.Theme == "orientação_pós_consulta")
        {
            followUpType = "foto_ferida";
            scheduledWhen = "+3d (Solicitar foto da cicatrização)";
        }
        else if (workflowContext.Triage.Theme == "vacina")
        {
            followUpType = "retorno_vacina";
            scheduledWhen = "+21d (Lembrete da próxima dose/reforço)";
        }
        else
        {
            followUpType = "evolução_geral";
            scheduledWhen = "+24h (Checagem de bem-estar)";
        }

        await VetFollowUpTools.ScheduleFollowUp(patientId, followUpType, scheduledWhen, cancellationToken);

        await _userInteractor.PublishTraceAsync($"[Follow-Up Agendado] Tipo: {followUpType} ({scheduledWhen})", "success", cancellationToken);
        await _userInteractor.PublishAgentStateAsync("follow-up", "done", "Programado", cancellationToken);

        await context.YieldOutputAsync(workflowContext, cancellationToken);
        return workflowContext;
    }
}
