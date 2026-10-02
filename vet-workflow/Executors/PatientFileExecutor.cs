using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace VetWorkflow;

/// <summary>
/// Executor responsável por consultar histórico e consolidar o prontuário/ficha digital do paciente.
/// </summary>
internal sealed class PatientFileExecutor : Executor<VetWorkflowContext, VetWorkflowContext>
{
    private readonly AIAgent _patientFileAgent;
    private readonly IUserInteractor _userInteractor;

    public PatientFileExecutor(AIAgent patientFileAgent, IUserInteractor userInteractor) : base("PatientFileExecutor")
    {
        _patientFileAgent = patientFileAgent ?? throw new ArgumentNullException(nameof(patientFileAgent));
        _userInteractor = userInteractor ?? throw new ArgumentNullException(nameof(userInteractor));
    }

    public override async ValueTask<VetWorkflowContext> HandleAsync(VetWorkflowContext workflowContext, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        await _userInteractor.PublishAgentStateAsync("patient-file", "active", "Registrando Ficha", cancellationToken);
        await _userInteractor.PublishTraceAsync($"[Prontuário] Buscando histórico anterior para {workflowContext.Patient.PetName}...", "info", cancellationToken);

        // 1. Busca histórico anterior
        string historyJson = await VetPatientFileTools.SearchPatientHistory(
            workflowContext.Patient.PetName,
            workflowContext.Patient.TutorName ?? "Tutor",
            cancellationToken);

        // 2. Salva/atualiza ficha digital
        string patientDataJson = JsonSerializer.Serialize(workflowContext.Patient);
        string saveResultJson = await VetPatientFileTools.CreateOrUpdatePatientFile(
            workflowContext.Patient.PetName,
            workflowContext.Patient.TutorName ?? "Tutor Cadastrado",
            workflowContext.Patient.TutorPhone ?? "(11) 99999-0000",
            patientDataJson,
            cancellationToken);

        try
        {
            var doc = JsonDocument.Parse(saveResultJson);
            if (doc.RootElement.TryGetProperty("patientId", out var idProp))
            {
                workflowContext.Patient.PatientId = idProp.GetString() ?? $"PAT-{Guid.NewGuid():N}"[..8];
            }
        }
        catch { }

        await _userInteractor.PublishTraceAsync($"[Prontuário Atualizado] Paciente ID: {workflowContext.Patient.PatientId} salvo no sistema", "success", cancellationToken);
        await _userInteractor.PublishAgentStateAsync("patient-file", "done", "Ficha Salva", cancellationToken);

        await context.YieldOutputAsync(workflowContext, cancellationToken);
        return workflowContext;
    }
}
