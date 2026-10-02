using System.ComponentModel;
using System.Text.Json;

namespace VetWorkflow;

public static class VetTriageTools
{
    private static VetAlertSignsConfig? _alertConfig;

    private static async Task<VetAlertSignsConfig> GetAlertConfigAsync(CancellationToken ct = default)
    {
        _alertConfig ??= await VetAlertSignsConfig.LoadAsync("vet_alert_signs.json", ct);
        return _alertConfig;
    }

    [Description("Classifica a urgência da mensagem do tutor com base nos sintomas relatados. Retorna: EMERGENCY, URGENT ou ROUTINE.")]
    public static async Task<string> ClassifyUrgency(
        [Description("Texto da mensagem do tutor descrevendo o problema ou solicitação")] string messageText,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Classificando urgência para: {messageText}");
        var config = await GetAlertConfigAsync(cancellationToken);

        var lower = messageText.ToLowerInvariant();
        var detected = new List<string>();

        foreach (var sign in config.EmergencySigns)
        {
            if (sign.Keywords.Any(k => lower.Contains(k.ToLowerInvariant())))
            {
                detected.Add(sign.Id);
            }
        }

        if (detected.Count > 0)
        {
            return JsonSerializer.Serialize(new
            {
                urgency = "EMERGENCY",
                alertSignsDetected = detected,
                confidence = 0.99,
                reason = "Sinais de alerta crítico detectados na mensagem do tutor."
            });
        }

        // Sintomas urgentes não críticos
        string[] urgentKeywords = { "vomitando", "vômito", "diarreia", "febre", "mancando", "machucou", "dor", "não come", "olho vermelho", "tosse", "ferida" };
        if (urgentKeywords.Any(k => lower.Contains(k)))
        {
            return JsonSerializer.Serialize(new
            {
                urgency = "URGENT",
                alertSignsDetected = Array.Empty<string>(),
                confidence = 0.88,
                reason = "Sintomas clínicos agudos que necessitam de avaliação nas próximas horas."
            });
        }

        return JsonSerializer.Serialize(new
        {
            urgency = "ROUTINE",
            alertSignsDetected = Array.Empty<string>(),
            confidence = 0.95,
            reason = "Solicitação de rotina, agendamento preventivo ou dúvida administrativa."
        });
    }

    [Description("Verifica se a mensagem contém sinais de alerta pré-configurados que exigem escalonamento imediato.")]
    public static async Task<string> DetectAlertSigns(
        [Description("Texto da mensagem do tutor")] string messageText,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Detectando sinais de alerta em: {messageText}");
        var config = await GetAlertConfigAsync(cancellationToken);

        var lower = messageText.ToLowerInvariant();
        var matches = config.EmergencySigns
            .Where(sign => sign.Keywords.Any(k => lower.Contains(k.ToLowerInvariant())))
            .Select(sign => new { sign.Id, sign.Severity, sign.Message })
            .ToList();

        return JsonSerializer.Serialize(new
        {
            hasAlertSigns = matches.Count > 0,
            matchedSigns = matches
        });
    }
}
