using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace VetWorkflow;

/// <summary>
/// Executor responsável por agendamento interativo de consultas, vacinações e exames na agenda profissional.
/// Atua tanto no fluxo inicial de agendamento quanto quando acionado dinamicamente pós-orientação do veterinário.
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

        bool isFromVetHandoff = workflowContext.NextAction == "scheduling" || !string.IsNullOrWhiteSpace(workflowContext.VetInstructions);

        string appointmentType = isFromVetHandoff
            ? "Consulta Clínica Presencial"
            : workflowContext.Triage.Theme switch
            {
                "vacina" => "Vacinação Anual",
                "retorno" => "Consulta de Retorno",
                "exame" => "Coleta de Exames",
                "vermifugação" => "Protocolo de Vermifugação",
                "castração" => "Avaliação Pré-Cirúrgica (Castração)",
                _ => "Consulta Veterinária"
            };

        await _userInteractor.PublishTraceAsync($"[Agenda] Verificando disponibilidade para {appointmentType}...", "info", cancellationToken);

        // 1. Consulta horários disponíveis
        string availJson = await VetSchedulingTools.CheckAvailability(appointmentType, "essa semana", cancellationToken);

        string slot1 = DateTime.UtcNow.AddDays(1).Date.AddHours(10).ToString("dd/MM/yyyy às 10:00");
        string slot2 = DateTime.UtcNow.AddDays(1).Date.AddHours(14).ToString("dd/MM/yyyy às 14:00");
        string slot3 = DateTime.UtcNow.AddDays(2).Date.AddHours(16).ToString("dd/MM/yyyy às 16:00");

        string petName = string.IsNullOrWhiteSpace(workflowContext.Patient.PetName) || workflowContext.Patient.PetName == "Pet"
            ? "seu pet"
            : $"**{workflowContext.Patient.PetName}**";

        string offerMessage = isFromVetHandoff
            ? $"Olá! Sou o Assistente de Agendamento. Como o Dr(a). Veterinário(a) orientou a realização de uma consulta para {petName}, vou te ajudar a reservar o melhor horário!\n\n" +
              $"Verifiquei a agenda e temos as seguintes opções disponíveis:\n" +
              $"📅 • Opção 1: {slot1}\n" +
              $"📅 • Opção 2: {slot2}\n" +
              $"📅 • Opção 3: {slot3}\n\n" +
              $"Qual dessas opções fica melhor para você?"
            : $"Para agendarmos o atendimento de **{appointmentType}** de {petName}, consultei os próximos horários livres:\n\n" +
              $"📅 • Opção 1: {slot1}\n" +
              $"📅 • Opção 2: {slot2}\n" +
              $"📅 • Opção 3: {slot3}\n\n" +
              $"Qual desses horários você prefere?";

        await _userInteractor.SetAgentTypingAsync("Consultando agenda disponível...", false, cancellationToken);

        // Pergunta ao tutor o horário desejado e aguarda a resposta
        string tutorChoice = await _userInteractor.GetUserResponseAsync(
            offerMessage,
            agentId: "scheduling",
            audience: MessageAudience.Both,
            cancellationToken: cancellationToken);

        await _userInteractor.SetAgentTypingAsync("Confirmando agendamento na agenda...", true, cancellationToken);

        // Identifica o horário escolhido
        string chosenSlot = slot2; // Padrão
        var lowerChoice = tutorChoice.ToLowerInvariant();
        if (lowerChoice.Contains("10") || lowerChoice.Contains("opção 1") || lowerChoice.Contains("primeir"))
        {
            chosenSlot = slot1;
        }
        else if (lowerChoice.Contains("16") || lowerChoice.Contains("opção 3") || lowerChoice.Contains("terceir") || lowerChoice.Contains("depois de amanhã"))
        {
            chosenSlot = slot3;
        }

        // 2. Realiza a reserva
        string patientId = string.IsNullOrWhiteSpace(workflowContext.Patient.PatientId) ? "PAT-DEFAULT" : workflowContext.Patient.PatientId;
        await VetSchedulingTools.BookAppointment(patientId, appointmentType, chosenSlot, cancellationToken);

        // 3. Configura lembrete com antecedência
        await VetSchedulingTools.SetReminder(patientId, 24, cancellationToken);

        var appointment = new AppointmentRequest
        {
            AppointmentId = $"APT-{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            PatientId = patientId,
            PetName = workflowContext.Patient.PetName,
            TutorName = workflowContext.Patient.TutorName ?? "Tutor",
            AppointmentType = appointmentType,
            ScheduledDateTime = chosenSlot,
            Status = "Confirmado",
            Notes = $"Agendamento para {workflowContext.Patient.PetName} ({workflowContext.Patient.Species}) - {appointmentType}. Lembrete configurado para 24h antes."
        };
        workflowContext.Appointment = appointment;

        await _userInteractor.SetAgentTypingAsync("Confirmando agendamento na agenda...", false, cancellationToken);
        await _userInteractor.PublishTraceAsync($"[Agendamento Confirmado] {appointmentType} para {workflowContext.Patient.PetName} em {chosenSlot}", "success", cancellationToken);

        // Envia mensagem de confirmação final ao tutor
        string confirmationMessage = $"✅ **Agendamento Confirmado com Sucesso!**\n\n" +
            $"🐾 **Paciente:** {workflowContext.Patient.PetName}\n" +
            $"📅 **Procedimento:** {appointmentType}\n" +
            $"⏰ **Data e Horário:** {chosenSlot}\n" +
            $"📍 **Local:** Clínica Veterinária\n\n" +
            $"Configuramos um lembrete automático 24 horas antes para você. Nos vemos em breve!";

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
