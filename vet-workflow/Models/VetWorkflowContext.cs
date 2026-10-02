namespace VetWorkflow;

/// <summary>
/// Contexto compartilhado transmitido entre os executores no grafo do VetAssistant Workflow.
/// </summary>
public sealed class VetWorkflowContext
{
    public string SessionId { get; set; } = string.Empty;
    public string InitialUserMessage { get; set; } = string.Empty;
    public VetTriageResult Triage { get; set; } = new();
    public PatientData Patient { get; set; } = new();
    public AppointmentRequest? Appointment { get; set; }
    public string? PostCareNotes { get; set; }
    public ClinicalSummary? Summary { get; set; }
    public bool HandedOffToVet { get; set; } = false;
    public bool EmergencyAlertSent { get; set; } = false;
}
