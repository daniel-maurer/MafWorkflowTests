using System.Text.Json.Serialization;

namespace VetWorkflow;

/// <summary>
/// Dados cadastrais e clínicos preliminares do paciente e tutor.
/// </summary>
public sealed class PatientData
{
    [JsonPropertyName("patient_id")]
    public string PatientId { get; set; } = string.Empty;

    [JsonPropertyName("pet_name")]
    public string PetName { get; set; } = string.Empty;

    [JsonPropertyName("species")]
    public string Species { get; set; } = string.Empty; // cão, gato, ave, réptil, outro

    [JsonPropertyName("breed")]
    public string? Breed { get; set; }

    [JsonPropertyName("age")]
    public string? Age { get; set; }

    [JsonPropertyName("weight_kg")]
    public decimal? WeightKg { get; set; }

    [JsonPropertyName("symptoms")]
    public string? Symptoms { get; set; }

    [JsonPropertyName("symptom_duration")]
    public string? SymptomDuration { get; set; }

    [JsonPropertyName("current_diet")]
    public string? CurrentDiet { get; set; }

    [JsonPropertyName("current_medication")]
    public string? CurrentMedication { get; set; }

    [JsonPropertyName("tutor_name")]
    public string? TutorName { get; set; }

    [JsonPropertyName("tutor_phone")]
    public string? TutorPhone { get; set; }

    [JsonPropertyName("media_urls")]
    public List<string> MediaUrls { get; set; } = new();

    [JsonPropertyName("collected_at")]
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("is_complete")]
    public bool IsComplete { get; set; } = false;

    [JsonPropertyName("missing_fields")]
    public List<string> MissingFields { get; set; } = new();
}
