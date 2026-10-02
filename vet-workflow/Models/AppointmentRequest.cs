using System.Text.Json.Serialization;

namespace VetWorkflow;

/// <summary>
/// Modelo de solicitação e confirmação de agendamento na agenda veterinária.
/// </summary>
public sealed class AppointmentRequest
{
    [JsonPropertyName("appointment_id")]
    public string AppointmentId { get; set; } = string.Empty;

    [JsonPropertyName("patient_id")]
    public string PatientId { get; set; } = string.Empty;

    [JsonPropertyName("pet_name")]
    public string PetName { get; set; } = string.Empty;

    [JsonPropertyName("tutor_name")]
    public string TutorName { get; set; } = string.Empty;

    [JsonPropertyName("appointment_type")]
    public string AppointmentType { get; set; } = string.Empty; // consulta, retorno, vacina, exame, vermifugação, castração

    [JsonPropertyName("scheduled_date_time")]
    public string ScheduledDateTime { get; set; } = string.Empty;

    [JsonPropertyName("reminder_hours_before")]
    public int ReminderHoursBefore { get; set; } = 24;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Confirmed"; // Confirmed, Pending, Cancelled

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}
