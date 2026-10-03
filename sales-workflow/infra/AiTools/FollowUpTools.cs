using System.ComponentModel;
using SalesWorkflow.Models;

namespace SalesWorkflow.AiTools;

public static class FollowUpTools
{
    [Description("Agenda um retorno programado ou lembrete de atendimento para o cliente.")]
    public static async Task<FollowUpResult> ScheduleFollowUp(
        [Description("Identificador do orçamento ou sessão")] string referenceId,
        [Description("Tipo: 'quote_reminder', 'cart_recovery', 'restock_notification'")] string followUpType,
        [Description("Horas a partir de agora para enviar o lembrete")] int hoursFromNow,
        [Description("Canal preferencial: 'whatsapp', 'email' ou 'sms'")] string channel = "whatsapp",
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Agendando follow-up ({followUpType}) para daqui a {hoursFromNow} horas via {channel} (Ref: {referenceId})");
        await Task.Delay(80, cancellationToken);

        var scheduledTime = DateTimeOffset.UtcNow.AddHours(hoursFromNow);
        return new FollowUpResult
        {
            FollowUpScheduled = true,
            ScheduledAt = scheduledTime,
            FollowUpType = followUpType,
            Channel = channel,
            MessageForUser = $"Follow-up agendado com sucesso para {scheduledTime:dd/MM/yyyy HH:mm} via {channel}."
        };
    }
}
