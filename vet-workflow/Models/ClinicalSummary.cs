using System.Text.Json.Serialization;

namespace VetWorkflow;

/// <summary>
/// Resumo clínico-administrativo pré-consulta gerado para o médico veterinário.
/// </summary>
public sealed class ClinicalSummary
{
    [JsonPropertyName("patient_id")]
    public string PatientId { get; set; } = string.Empty;

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("patient")]
    public PatientData? Patient { get; set; }

    [JsonPropertyName("triage_urgency")]
    public string TriageUrgency { get; set; } = string.Empty; // EMERGENCY, URGENT, ROUTINE

    [JsonPropertyName("theme")]
    public string Theme { get; set; } = string.Empty;

    [JsonPropertyName("chief_complaint")]
    public string ChiefComplaint { get; set; } = string.Empty;

    [JsonPropertyName("conversation_summary")]
    public string ConversationSummary { get; set; } = string.Empty;

    [JsonPropertyName("pending_actions")]
    public List<string> PendingActions { get; set; } = new();

    [JsonPropertyName("generated_at")]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
