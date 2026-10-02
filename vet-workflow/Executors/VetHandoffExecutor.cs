using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace VetWorkflow;

/// <summary>
/// Executor responsável por transferir casos clínicos ou complexos para o médico veterinário em modo humano / split-mode.
/// Aguarda a resposta do profissional e o clique em 'Mark as Solved', capturando a conduta médica para roteamento dinâmico.
/// </summary>
internal sealed class VetHandoffExecutor : Executor<VetWorkflowContext, VetWorkflowContext>
{
    private readonly AIAgent _vetHandoffAgent;
    private readonly IUserInteractor _userInteractor;

    public VetHandoffExecutor(AIAgent vetHandoffAgent, IUserInteractor userInteractor) : base("VetHandoffExecutor")
    {
        _vetHandoffAgent = vetHandoffAgent ?? throw new ArgumentNullException(nameof(vetHandoffAgent));
        _userInteractor = userInteractor ?? throw new ArgumentNullException(nameof(userInteractor));
    }

    public override async ValueTask<VetWorkflowContext> HandleAsync(VetWorkflowContext workflowContext, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        await _userInteractor.PublishAgentStateAsync("vet-handoff", "active", "Aguardando Vet", cancellationToken);
        await _userInteractor.PublishTraceAsync($"[Escalonamento] Notificando médico veterinário para assumir o caso...", "info", cancellationToken);

        string petName = string.IsNullOrWhiteSpace(workflowContext.Patient.PetName) || workflowContext.Patient.PetName == "Pet"
            ? "seu pet"
            : $"**{workflowContext.Patient.PetName}** ({workflowContext.Patient.Species})";

        string handoffMessage = workflowContext.Triage.Theme switch
        {
            "sintoma" => $"Reuni todas as informações sobre os sintomas de {petName} e encaminhei para o Dr(a). Veterinário(a). Como se trata de avaliação clínica de saúde, o profissional já foi avisado e continuará a orientação personalizada com você diretamente por aqui em instantes.",
            "exame" => $"Os dados e resultados de exame de {petName} foram encaminhados para o Dr(a). Veterinário(a), que avaliará os laudos com atenção e responderá diretamente por aqui.",
            "administrativo" => $"Encaminhei sua solicitação para a nossa equipe veterinária, que responderá diretamente por aqui em instantes.",
            _ => $"Dr(a). Veterinário(a) foi notificado(a) com os dados de {petName} e continuará o atendimento personalizado com você por aqui em instantes."
        };

        await _userInteractor.SendUserResponseAsync(
            handoffMessage,
            agentId: "vet-handoff",
            audience: MessageAudience.Both,
            cancellationToken: cancellationToken);

        // Ativa o split-mode para intervenção do profissional humano
        await _userInteractor.PublishSplitModeAsync(true, cancellationToken);
        await _userInteractor.PublishContextAsync(
            "waiting_vet",
            $"Atendimento: {workflowContext.Patient.PetName}",
            "Veterinário em atendimento direto com o tutor.",
            "vet-handoff",
            true,
            cancellationToken);

        await _userInteractor.PublishTraceAsync("[Handoff Ativo] Modo veterinário habilitado no painel. Aguardando interação do profissional...", "info", cancellationToken);

        // Aguarda a conversa do veterinário até que o profissional clique em 'Mark as Solved' ou envie token de finalização
        bool ended = false;
        var vetMessages = new List<string>();

        while (!ended)
        {
            string message = await _userInteractor.GetUserResponseAsync(
                string.Empty,
                agentId: "vet-handoff",
                audience: MessageAudience.Both,
                cancellationToken: cancellationToken);

            if (string.Equals(message, WorkflowControlTokens.MarkResolved, StringComparison.Ordinal))
            {
                ended = true;
                break;
            }

            if (string.Equals(message, WorkflowControlTokens.Cancel, StringComparison.Ordinal))
            {
                ended = true;
                break;
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                vetMessages.Add(message);
                workflowContext.InitialUserMessage += "\n[Veterinário]: " + message;
                Logger.LogInfo($"[VetHandoffExecutor] Mensagem registrada na consulta: {message}");
            }
        }

        string allVetGuidance = string.Join("\n", vetMessages);
        workflowContext.VetInstructions = allVetGuidance;
        workflowContext.HandedOffToVet = true;

        bool needsScheduling = ContainsSchedulingIntent(allVetGuidance);

        if (needsScheduling)
        {
            await _userInteractor.PublishTraceAsync("Veterinário indicou agendamento de consulta. Transferindo para o Agente de Agendamento.", "success", cancellationToken);
            workflowContext.NextAction = "scheduling";

            await _userInteractor.SendSystemMessageAsync(
                "O Dr(a). Veterinário(a) concluiu a orientação e indicou a necessidade de consulta. O Assistente de Agendamento assumirá a conversa para marcar o horário.",
                systemStyle: "handoff",
                audience: MessageAudience.Both,
                cancellationToken: cancellationToken);
        }
        else
        {
            await _userInteractor.PublishTraceAsync("Atendimento clínico concluído pelo veterinário.", "success", cancellationToken);
            workflowContext.NextAction = "completed";

            await _userInteractor.SendSystemMessageAsync(
                "Atendimento clínico com o médico veterinário concluído.",
                systemStyle: "resolved",
                audience: MessageAudience.Both,
                cancellationToken: cancellationToken);
        }

        // Desativa o split-mode ao concluir
        await _userInteractor.PublishSplitModeAsync(false, cancellationToken);
        await _userInteractor.PublishAgentStateAsync("vet-handoff", "done", "Concluído", cancellationToken);

        await context.YieldOutputAsync(workflowContext, cancellationToken);
        return workflowContext;
    }

    private static bool ContainsSchedulingIntent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var lower = text.ToLowerInvariant();
        string[] schedulingKeywords =
        {
            "marcar", "agendar", "agenda", "consulta", "avaliação presencial", 
            "trazer ele", "trazer ela", "trazer o", "trazer a", "exame de sangue", "ultrassom", "raio-x"
        };

        return schedulingKeywords.Any(k => lower.Contains(k));
    }
}
