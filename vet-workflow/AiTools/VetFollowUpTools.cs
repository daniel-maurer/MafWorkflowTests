using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace VetWorkflow;

public static class VetFollowUpTools
{
    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri(Environment.GetEnvironmentVariable("ADMIN_API_BASE_URL") ?? "http://localhost:5100/api/"),
        Timeout = TimeSpan.FromSeconds(5)
    };

    static VetFollowUpTools()
    {
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "mock-token:vet-workflow-worker");
    }

    [Description("Programa um follow-up automático para cobrar evolução, foto ou resultado de exame do tutor.")]
    public static async Task<string> ScheduleFollowUp(
        [Description("ID ou nome do paciente/tutor")] string patientId,
        [Description("Tipo de follow-up: evolução, foto_ferida, resultado_exame, retorno_vacina")] string followUpType,
        [Description("Data/hora para o lembrete (formato ISO 8601 ou relativo, ex: +2h, +24h, +30d)")] string scheduledDateTime,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Programando follow-up '{followUpType}' para {patientId} em {scheduledDateTime}");

        var scheduledTime = ParseScheduledDate(scheduledDateTime);
        string messageText = followUpType switch
        {
            "foto_ferida" => "Olá! Como está a cicatrização dos pontos hoje? Poderia nos enviar uma foto nítida do local para o Dr. verificar?",
            "resultado_exame" => "Olá! Já conseguiu retirar o resultado dos exames solicitados? Assim que tiver, envie a foto ou PDF aqui!",
            "retorno_vacina" => "Lembrete: O prazo para o reforço da vacina está se aproximando. Deseja escolher um horário para essa semana?",
            _ => $"Olá! Como o paciente ({patientId}) está evoluindo desde o nosso último contato? Esperamos que esteja melhorando!"
        };

        string followUpId = $"FLW-{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        try
        {
            var payload = new
            {
                CustomerIdentifier = patientId,
                Module = "vet",
                FollowUpType = followUpType,
                ReferenceId = followUpId,
                ReferenceTitle = $"Paciente/Tutor: {patientId}",
                ScheduledFor = scheduledTime,
                Channel = "WhatsApp",
                MessageText = messageText,
                Notes = $"Agendado pelo Vet Assistant Workflow para acompanhamento de {followUpType}"
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            await HttpClient.PostAsync("follow-ups", content, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[TOOL] Não foi possível persistir follow-up na API central: {ex.Message}");
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            followUpId,
            patientId,
            followUpType,
            scheduledDateTime = scheduledTime.ToString("o"),
            status = "Agendado",
            channel = "WhatsApp / Notificação",
            messagePreview = messageText
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

    private static DateTimeOffset ParseScheduledDate(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return DateTimeOffset.UtcNow.AddDays(1);
        var trimmed = input.Trim().ToLowerInvariant();
        if (trimmed.StartsWith("+"))
        {
            trimmed = trimmed[1..];
            if (trimmed.EndsWith("h") && int.TryParse(trimmed[..^1], out var hours))
                return DateTimeOffset.UtcNow.AddHours(hours);
            if (trimmed.EndsWith("d") && int.TryParse(trimmed[..^1], out var days))
                return DateTimeOffset.UtcNow.AddDays(days);
            if (trimmed.EndsWith("m") && int.TryParse(trimmed[..^1], out var minutes))
                return DateTimeOffset.UtcNow.AddMinutes(minutes);
        }

        if (DateTimeOffset.TryParse(input, out var parsed))
            return parsed;

        return DateTimeOffset.UtcNow.AddDays(1);
    }
}
