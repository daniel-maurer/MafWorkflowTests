using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace VetWorkflow;

/// <summary>
/// Executor responsável por coletar e estruturar as informações essenciais do paciente e tutor de forma conversacional e inteligente.
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
            new(ChatRole.User, $"Histórico completo da conversa até o momento:\n\"{workflowContext.InitialUserMessage}\"\nTema da triagem: {workflowContext.Triage.Theme}. Identifique e estruture os dados do pet.")
        };

        const int MaxCollectionClarifications = 2;
        int attempts = 0;
        PatientData patient = new();

        while (attempts <= MaxCollectionClarifications)
        {
            var response = await _dataCollectorAgent.RunAsync(history, cancellationToken: cancellationToken);

            Logger.LogInfo($"[DataCollectionExecutor] Resposta do agente: {response.Text}");

            if (AgentResponseParser.TryDeserializeAgentResponse<PatientData>(response.Text, out var parsedPatient) && parsedPatient != null)
            {
                patient = parsedPatient;
            }

            // Normalização semântica de espécie em português
            ApplySpeciesHeuristics(workflowContext.InitialUserMessage, patient);

            // Se os dados essenciais (nome do animal e espécie) já foram identificados ou se o agente marcou como completo
            bool hasEssentialData = !string.IsNullOrWhiteSpace(patient.PetName) 
                && !string.Equals(patient.PetName, "Pet", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(patient.Species);

            if (hasEssentialData || patient.IsComplete || attempts >= MaxCollectionClarifications)
            {
                patient.IsComplete = true;
                break;
            }

            // Caso faltem dados fundamentais, formula pergunta contextual
            await _userInteractor.SetAgentTypingAsync("Organizando informações do paciente...", false, cancellationToken);

            string question = GenerateContextualQuestion(workflowContext.InitialUserMessage, patient);

            await _userInteractor.PublishTraceAsync($"Solicitando dados pendentes: {question}", "info", cancellationToken);
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

    private static void ApplySpeciesHeuristics(string text, PatientData patient)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var lower = text.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(patient.Species) || patient.Species.Equals("outro", StringComparison.OrdinalIgnoreCase))
        {
            if (lower.Contains("cadela") || lower.Contains("cadelinha") || lower.Contains("cão") || lower.Contains("cao") || lower.Contains("cachorro") || lower.Contains("cachorrinha"))
            {
                patient.Species = "cão";
            }
            else if (lower.Contains("gata") || lower.Contains("gato") || lower.Contains("gatinha") || lower.Contains("gatinho") || lower.Contains("felin"))
            {
                patient.Species = "gato";
            }
        }
    }

    private static string GenerateContextualQuestion(string userText, PatientData patient)
    {
        if (!string.IsNullOrWhiteSpace(patient.QuestionForTutor))
        {
            // Se o agente sugeriu pergunta, verifica se ela não pergunta coisas que já sabemos
            bool knowsSpecies = !string.IsNullOrWhiteSpace(patient.Species);
            if (!knowsSpecies || (!patient.QuestionForTutor.Contains("cão ou gato", StringComparison.OrdinalIgnoreCase) && !patient.QuestionForTutor.Contains("cao ou gato", StringComparison.OrdinalIgnoreCase)))
            {
                return patient.QuestionForTutor;
            }
        }

        var lower = userText.ToLowerInvariant();
        bool isFemaleDog = lower.Contains("cadela") || lower.Contains("cadelinha");
        bool isDog = isFemaleDog || lower.Contains("cão") || lower.Contains("cao") || lower.Contains("cachorro");
        bool isCat = lower.Contains("gata") || lower.Contains("gato") || lower.Contains("felin");

        if (isFemaleDog)
        {
            return "Como se chama a sua cadelinha e qual a idade ou peso aproximado dela para anotarmos na ficha?";
        }

        if (isDog)
        {
            return "Como se chama o seu cãozinho e qual a idade ou peso aproximado dele para anotarmos na ficha?";
        }

        if (isCat)
        {
            return "Como se chama o seu gatinho(a) e qual a idade aproximada para anotarmos na ficha?";
        }

        return "Para organizarmos a ficha do atendimento, você poderia me informar o nome do seu pet e qual a espécie dele (cão ou gato)?";
    }
}
