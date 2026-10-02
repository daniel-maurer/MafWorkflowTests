using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace VetWorkflow;

/// <summary>
/// Executor responsável por coletar e estruturar as informações essenciais do paciente e tutor de forma conversacional.
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
        await _userInteractor.PublishTraceAsync("[Coleta] Estruturando dados do animal a partir do relato...", "info", cancellationToken);

        var history = new List<ChatMessage>
        {
            new(ChatRole.User, $"Relato e histórico da conversa com o tutor:\n\"{workflowContext.InitialUserMessage}\"\nTema identificado na triagem: {workflowContext.Triage.Theme}. Extraia e estruture os dados do animal e tutor.")
        };

        const int MaxCollectionClarifications = 2;
        int attempts = 0;
        PatientData patient = new();

        while (attempts <= MaxCollectionClarifications)
        {
            var response = await _dataCollectorAgent.RunAsync(history, cancellationToken: cancellationToken);

            if (AgentResponseParser.TryDeserializeAgentResponse<PatientData>(response.Text, out var parsedPatient) && parsedPatient != null)
            {
                patient = parsedPatient;
            }

            // Se os dados essenciais (nome do animal e espécie) já foram fornecidos ou se o agente marcou como completo
            bool hasEssentialData = !string.IsNullOrWhiteSpace(patient.PetName) && !string.IsNullOrWhiteSpace(patient.Species);

            if (hasEssentialData || patient.IsComplete || attempts >= MaxCollectionClarifications)
            {
                break;
            }

            // Caso faltem dados fundamentais (ex: tutor não informou o nome do pet nem espécie)
            await _userInteractor.SetAgentTypingAsync("Organizando informações do paciente...", false, cancellationToken);

            string question = !string.IsNullOrWhiteSpace(patient.QuestionForTutor)
                ? patient.QuestionForTutor
                : "Para organizarmos a ficha do atendimento, você poderia me informar o nome do seu pet e se é cão ou gato?";

            await _userInteractor.PublishTraceAsync("Solicitando dados cadastrais complementares do pet...", "info", cancellationToken);
            history.Add(new ChatMessage(ChatRole.Assistant, question));

            string tutorAnswer = await _userInteractor.GetUserResponseAsync(
                question,
                agentId: "data-collector",
                audience: MessageAudience.Both,
                cancellationToken: cancellationToken);

            await _userInteractor.SetAgentTypingAsync("Organizando informações do paciente...", true, cancellationToken);
            history.Add(new ChatMessage(ChatRole.User, tutorAnswer));
            workflowContext.InitialUserMessage += "\n" + tutorAnswer;
            attempts++;
        }

        // Se após a interação ainda não houver nome informado, define fallback neutro
        if (string.IsNullOrWhiteSpace(patient.PetName))
        {
            patient.PetName = "Pet";
        }

        if (string.IsNullOrWhiteSpace(patient.Species))
        {
            patient.Species = "não especificada";
        }

        if (string.IsNullOrWhiteSpace(patient.Symptoms))
        {
            patient.Symptoms = workflowContext.InitialUserMessage;
        }

        patient.IsComplete = true;
        workflowContext.Patient = patient;

        await _userInteractor.SetAgentTypingAsync("Organizando informações do paciente...", false, cancellationToken);
        await _userInteractor.PublishTraceAsync($"[Ficha Preliminar] Animal: {patient.PetName} ({patient.Species}) | Motivo: {workflowContext.Triage.Theme}", "success", cancellationToken);
        await _userInteractor.PublishAgentStateAsync("data-collector", "done", "Dados Coletados", cancellationToken);

        await context.YieldOutputAsync(workflowContext, cancellationToken);
        return workflowContext;
    }
}
