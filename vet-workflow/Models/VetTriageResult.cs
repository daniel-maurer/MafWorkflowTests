using System.Text.Json.Serialization;

namespace VetWorkflow;

/// <summary>
/// Resultado da classificação da mensagem do tutor na triagem veterinária.
/// </summary>
public sealed class VetTriageResult
{
    [JsonPropertyName("urgency")]
    public string Urgency { get; set; } = "ROUTINE"; // EMERGENCY, URGENT, ROUTINE

    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "sintoma"; // sintoma, vacina, retorno, exame, vermifugação, castração, orientação_pós_consulta, administrativo, outro

    [JsonPropertyName("alert_signs_detected")]
    public string[] AlertSignsDetected { get; set; } = Array.Empty<string>();

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; } = 1.0;

    [JsonPropertyName("reasoning")]
    public string Reasoning { get; set; } = string.Empty;

    [JsonPropertyName("is_understood")]
    public bool IsUnderstood { get; set; } = true;

    [JsonPropertyName("question_for_user")]
    public string QuestionForUser { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
}
