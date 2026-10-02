using System.ComponentModel;
using System.Text.Json;

namespace VetWorkflow;

public static class VetFollowUpTools
{
    [Description("Programa um follow-up automático para cobrar evolução, foto ou resultado de exame do tutor.")]
    public static async Task<string> ScheduleFollowUp(
        [Description("ID ou nome do paciente")] string patientId,
        [Description("Tipo de follow-up: evolução, foto_ferida, resultado_exame, retorno_vacina")] string followUpType,
        [Description("Data/hora para o lembrete (formato ISO 8601 ou relativo, ex: +2h, +24h, +30d)")] string scheduledDateTime,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Programando follow-up '{followUpType}' para {patientId} em {scheduledDateTime}");
        await Task.Delay(20, cancellationToken);

        string followUpId = $"FLW-{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        return JsonSerializer.Serialize(new
        {
            success = true,
            followUpId,
            patientId,
            followUpType,
            scheduledDateTime,
            status = "Agendado",
            channel = "WhatsApp / Notificação"
        });
    }

    [Description("Envia mensagem de follow-up ao tutor solicitando informações pendentes.")]
    public static async Task<string> SendFollowUpReminder(
        [Description("ID ou nome do paciente")] string patientId,
        [Description("Tipo de follow-up")] string followUpType,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Enviando mensagem de follow-up '{followUpType}' para {patientId}");
        await Task.Delay(20, cancellationToken);

        string text = followUpType switch
        {
            "foto_ferida" => "Olá! Como está a cicatrização dos pontos hoje? Poderia nos enviar uma foto nítida do local para o Dr. verificar?",
            "resultado_exame" => "Olá! Já conseguiu retirar o resultado dos exames solicitados? Assim que tiver, envie a foto ou PDF aqui!",
            "retorno_vacina" => "Lembrete: O prazo para o reforço da vacina está se aproximando. Deseja escolher um horário para essa semana?",
            _ => "Olá! Como o seu pet está evoluindo desde o nosso último contato? Esperamos que esteja melhorando!"
        };

        return JsonSerializer.Serialize(new
        {
            sent = true,
            patientId,
            messageText = text
        });
    }
}
