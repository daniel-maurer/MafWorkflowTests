using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace VetWorkflow;

/// <summary>
/// Executor responsável por agendamento de consultas, vacinações e exames na agenda profissional.
/// </summary>
internal sealed class SchedulingExecutor : Executor<VetWorkflowContext, VetWorkflowContext>
{
    private readonly AIAgent _schedulingAgent;
    private readonly IUserInteractor _userInteractor;

    public SchedulingExecutor(AIAgent schedulingAgent, IUserInteractor userInteractor) : base("SchedulingExecutor")
    {
        _schedulingAgent = schedulingAgent ?? throw new ArgumentNullException(nameof(schedulingAgent));
        _userInteractor = userInteractor ?? throw new ArgumentNullException(nameof(userInteractor));
    }

    public override async ValueTask<VetWorkflowContext> HandleAsync(VetWorkflowContext workflowContext, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        await _userInteractor.PublishAgentStateAsync("scheduling", "active", "Agendando", cancellationToken);
        await _userInteractor.SetAgentTypingAsync("Consultando agenda disponível...", true, cancellationToken);
        await _userInteractor.PublishTraceAsync($"[Agenda] Verificando disponibilidade para {workflowContext.Triage.Theme}...", "info", cancellationToken);

        string appointmentType = workflowContext.Triage.Theme switch
        {
            "vacina" => "Vacinação Anual",
            "retorno" => "Consulta de Retorno",
            "exame" => "Coleta de Exames",
            "vermifugação" => "Protocolo de Vermifugação",
            "castração" => "Avaliação Pré-Cirúrgica (Castração)",
            _ => "Consulta Veterinária"
        };

        // 1. Checa disponibilidade
        string availJson = await VetSchedulingTools.CheckAvailability(appointmentType, "essa semana", cancellationToken);

        // 2. Realiza o agendamento sugerido
        var scheduledTime = DateTime.UtcNow.AddDays(1).Date.AddHours(14).ToString("dd/MM/yyyy HH:mm");
        string bookJson = await VetSchedulingTools.BookAppointment(
            workflowContext.Patient.PatientId,
            appointmentType,
            scheduledTime,
            cancellationToken);

        // 3. Configura lembrete
        await VetSchedulingTools.SetReminder(workflowContext.Patient.PatientId, 24, cancellationToken);

        var appointment = new AppointmentRequest
        {
            AppointmentId = $"APT-{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            PatientId = workflowContext.Patient.PatientId,
            PetName = workflowContext.Patient.PetName,
            TutorName = workflowContext.Patient.TutorName ?? "Tutor",
            AppointmentType = appointmentType,
            ScheduledDateTime = scheduledTime,
            Status = "Confirmado",
            Notes = $"Agendamento realizado para {workflowContext.Patient.PetName} ({workflowContext.Patient.Species}). Lembrete configurado com 24h de antecedência."
        };
        workflowContext.Appointment = appointment;

        await _userInteractor.SetAgentTypingAsync("Consultando agenda disponível...", false, cancellationToken);
        await _userInteractor.PublishTraceAsync($"[Agendamento Confirmado] {appointmentType} para {workflowContext.Patient.PetName} em {scheduledTime}", "success", cancellationToken);

        // Envia mensagem de confirmação para o chat do tutor
        string confirmationMessage = $"✅ Agendamento registrado com sucesso!\n\n" +
            $"🐾 **Paciente:** {workflowContext.Patient.PetName}\n" +
            $"📅 **Procedimento:** {appointmentType}\n" +
            $"⏰ **Horário:** {scheduledTime}\n\n" +
            $"Configuramos um lembrete automático 24 horas antes. Por favor, traga a carteira de vacinação do seu pet!";

        await _userInteractor.SendUserResponseAsync(
            confirmationMessage,
            agentId: "scheduling",
            audience: MessageAudience.Both,
            cancellationToken: cancellationToken);

        await _userInteractor.PublishAgentStateAsync("scheduling", "done", "Agendado", cancellationToken);

        await context.YieldOutputAsync(workflowContext, cancellationToken);
        return workflowContext;
    }
}
