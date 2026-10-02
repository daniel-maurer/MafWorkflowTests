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

    /// <summary>
    /// Próxima ação dinâmica determinada pós-avaliação (ex: "scheduling", "completed").
    /// </summary>
    public string NextAction { get; set; } = string.Empty;

    /// <summary>
    /// Orientações ou mensagens registradas pelo veterinário humano durante o split-mode.
    /// </summary>
    public string VetInstructions { get; set; } = string.Empty;
}
