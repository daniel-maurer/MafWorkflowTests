using System.ComponentModel;
using System.Text.Json;

namespace VetWorkflow;

public static class VetSchedulingTools
{
    [Description("Consulta horários disponíveis na agenda do veterinário para o tipo de atendimento solicitado.")]
    public static async Task<string> CheckAvailability(
        [Description("Tipo de atendimento: consulta, retorno, vacina, exame, vermifugação, castração")] string appointmentType,
        [Description("Data preferida pelo tutor ou período sugerido (ex: 'essa semana', '2026-10-05')")] string preferredDate,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Verificando disponibilidade para {appointmentType} em {preferredDate}");
        await Task.Delay(30, cancellationToken);

        var today = DateTime.UtcNow.Date;
        var slot1 = today.AddDays(1).AddHours(10);
        var slot2 = today.AddDays(1).AddHours(14);
        var slot3 = today.AddDays(2).AddHours(16);

        return JsonSerializer.Serialize(new
        {
            availableSlots = new[]
            {
                slot1.ToString("yyyy-MM-dd HH:mm"),
                slot2.ToString("yyyy-MM-dd HH:mm"),
                slot3.ToString("yyyy-MM-dd HH:mm")
            },
            formattedSuggestion = $"Temos horários disponíveis: amanhã às 10:00 e 14:00, ou depois de amanhã às 16:00."
        });
    }

    [Description("Registra um agendamento confirmado na agenda do veterinário.")]
    public static async Task<string> BookAppointment(
        [Description("ID ou nome do paciente")] string patientId,
        [Description("Tipo de atendimento")] string appointmentType,
        [Description("Data e hora escolhida")] string dateTime,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Agendando {appointmentType} para {patientId} em {dateTime}");
        await Task.Delay(50, cancellationToken);

        string appointmentId = $"APT-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        return JsonSerializer.Serialize(new
        {
            success = true,
            appointmentId,
            patientId,
            appointmentType,
            scheduledDateTime = dateTime,
            status = "Confirmado",
            instructions = "Favor chegar com 10 minutos de antecedência. Cães na guia e gatos na caixa de transporte."
        });
    }

    [Description("Configura um lembrete automático para o tutor sobre o agendamento.")]
    public static async Task<string> SetReminder(
        [Description("ID do agendamento")] string appointmentId,
        [Description("Horas de antecedência para disparo do lembrete")] int hoursBeforeReminder,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Configurando lembrete para agendamento {appointmentId} com {hoursBeforeReminder}h de antecedência");
        await Task.Delay(20, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            success = true,
            reminderId = $"REM-{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            appointmentId,
            hoursBefore = hoursBeforeReminder,
            channel = "WhatsApp / Notificação"
        });
    }
}
