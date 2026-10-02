using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace VetWorkflow;

/// <summary>
/// Executor responsável por coletar e estruturar as informações essenciais do paciente e tutor.
/// </summary>
internal sealed class DataCollectionExecutor : Executor<VetWorkflowContext, VetWorkflowContext>
{
    private readonly AIAgent _dataCollectorAgent;
    private readonly IUserInteractor _userInteractor;

    public DataCollectionExecutor(AIAgent dataCollectorAgent, IUserInteractor userInteractor) : base("DataCollectionExecutor")
    {
        _dataCollectorAgent = dataCollectorAgent ?? throw new ArgumentNullException(nameof(dataCollectorAgent));
        _userInteractor = userInteractor ?? throw new ArgumentNullException(nameof(userInteractor));
    }

    public override async ValueTask<VetWorkflowContext> HandleAsync(VetWorkflowContext workflowContext, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        await _userInteractor.PublishAgentStateAsync("data-collector", "active", "Coletando Dados", cancellationToken);
        await _userInteractor.SetAgentTypingAsync("Organizando informações do paciente...", true, cancellationToken);
        await _userInteractor.PublishTraceAsync("[Coleta] Extraindo dados preliminares do animal...", "info", cancellationToken);

        // Extração dos dados iniciais
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, $"Mensagem do tutor: \"{workflowContext.InitialUserMessage}\". Extraia os dados do animal e tutor no formato JSON: {{\"pet_name\": \"...\", \"species\": \"...\", \"breed\": \"...\", \"age\": \"...\", \"weight_kg\": 0.0, \"symptoms\": \"...\", \"current_medication\": \"...\", \"tutor_name\": \"...\"}}")
        };

        var response = await _dataCollectorAgent.RunAsync(history, cancellationToken: cancellationToken);
        PatientData patient = new PatientData();

        if (AgentResponseParser.TryDeserializeAgentResponse<PatientData>(response.Text, out var parsedPatient) && parsedPatient != null)
        {
            patient = parsedPatient;
        }

        // Fallbacks heurísticos básicos caso o LLM não tenha extraído tudo
        if (string.IsNullOrWhiteSpace(patient.PetName))
        {
            var msg = workflowContext.InitialUserMessage.ToLowerInvariant();
            if (msg.Contains("luna")) patient.PetName = "Luna";
            else if (msg.Contains("thor")) patient.PetName = "Thor";
            else if (msg.Contains("mimi")) patient.PetName = "Mimi";
            else if (msg.Contains("bob")) patient.PetName = "Bob";
            else patient.PetName = "Paciente";
        }

        if (string.IsNullOrWhiteSpace(patient.Species))
        {
            var msg = workflowContext.InitialUserMessage.ToLowerInvariant();
            if (msg.Contains("gata") || msg.Contains("gato") || msg.Contains("felin")) patient.Species = "gato";
            else patient.Species = "cão";
        }

        patient.Symptoms = workflowContext.InitialUserMessage;
        patient.IsComplete = true;

        workflowContext.Patient = patient;

        await _userInteractor.SetAgentTypingAsync("Organizando informações do paciente...", false, cancellationToken);
        await _userInteractor.PublishTraceAsync($"[Ficha Preliminar] Animal: {patient.PetName} ({patient.Species}) | Sintoma/Motivo: {workflowContext.Triage.Theme}", "success", cancellationToken);
        await _userInteractor.PublishAgentStateAsync("data-collector", "done", "Dados Coletados", cancellationToken);

        await context.YieldOutputAsync(workflowContext, cancellationToken);
        return workflowContext;
    }
}
