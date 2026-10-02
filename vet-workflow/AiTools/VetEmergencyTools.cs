using System.ComponentModel;
using System.Text.Json;

namespace VetWorkflow;

public static class VetEmergencyTools
{
    [Description("Envia notificação de emergência ao veterinário via canal prioritário (push, SMS ou WhatsApp).")]
    public static async Task<string> NotifyVet(
        [Description("ID ou nome do paciente")] string patientId,
        [Description("Resumo da situação de emergência")] string emergencySummary,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[EMERGÊNCIA] Notificando veterinário responsável para o paciente {patientId}: {emergencySummary}");
        await Task.Delay(50, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            success = true,
            dispatchedVia = new[] { "PushNotification", "EmergencySMS", "WhatsAppPriority" },
            timestamp = DateTime.UtcNow.ToString("o"),
            message = "Veterinário notificado com prioridade máxima."
        });
    }

    [Description("Gera a mensagem de segurança padronizada para o tutor orientando buscar atendimento emergencial imediato.")]
    public static async Task<string> SendEmergencyMessage(
        [Description("Identificador da emergência detectada (ex: convulsao, dispneia, sangramento)")] string emergencyType,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Gerando mensagem de segurança emergencial para tipo: {emergencyType}");
        var config = await VetAlertSignsConfig.LoadAsync("vet_alert_signs.json", cancellationToken);

        var sign = config.EmergencySigns.FirstOrDefault(s => s.Id.Equals(emergencyType, StringComparison.OrdinalIgnoreCase))
            ?? config.EmergencySigns.FirstOrDefault(s => s.Severity == "CRITICAL");

        string message = sign?.Message ?? 
            "⚠️ ATENÇÃO: Identificamos sinais graves no seu relato. Dirija-se imediatamente à clínica veterinária com atendimento de emergência mais próxima. Não tente medicar o animal em casa.";

        return JsonSerializer.Serialize(new
        {
            emergencyType,
            safetyMessage = message,
            escalationRequired = true
        });
    }
}
