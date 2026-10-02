using System.ComponentModel;
using System.Text.Json;

namespace VetWorkflow;

public static class VetSummaryTools
{
    private static readonly List<ClinicalSummary> _summaries = new();

    [Description("Gera um resumo clínico-administrativo estruturado da interação para o veterinário revisar antes da consulta.")]
    public static async Task<string> GenerateClinicalSummary(
        [Description("ID ou nome do paciente")] string patientId,
        [Description("ID da sessão de atendimento")] string sessionId,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Gerando resumo clínico para {patientId} na sessão {sessionId}");
        await Task.Delay(30, cancellationToken);

        var summary = new ClinicalSummary
        {
            PatientId = patientId,
            SessionId = sessionId,
            TriageUrgency = "ROUTINE",
            Theme = "Atendimento Administrativo",
            ChiefComplaint = "Acompanhamento e suporte",
            ConversationSummary = "Dados coletados e orientações repassadas ao tutor.",
            PendingActions = new List<string> { "Revisar ficha antes do atendimento", "Confirmar comparecimento" }
        };

        _summaries.Add(summary);

        return JsonSerializer.Serialize(new
        {
            success = true,
            summaryGenerated = true,
            patientId,
            timestamp = summary.GeneratedAt.ToString("o"),
            overview = "Resumo clínico-administrativo pronto para revisão do médico veterinário."
        });
    }

    [Description("Atualiza o painel de pendências do veterinário com itens que requerem atenção profissional.")]
    public static async Task<string> UpdatePendingDashboard(
        [Description("ID ou nome do paciente")] string patientId,
        [Description("Tipo de pendência: revisão_resumo, resultado_exame, retorno_agendado, emergência_atendida")] string pendingType,
        [Description("Descrição curta da pendência")] string description,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Atualizando painel de pendências para {patientId}: [{pendingType}] {description}");
        await Task.Delay(20, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            success = true,
            dashboardUpdated = true,
            entry = new
            {
                patientId,
                pendingType,
                description,
                createdAt = DateTime.UtcNow.ToString("o")
            }
        });
    }
}
