using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace VetWorkflow;

/// <summary>
/// Executor responsável por transferir casos clínicos ou complexos para o médico veterinário em modo humano / split-mode.
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
        await _userInteractor.PublishAgentStateAsync("vet-handoff", "active", "Escalonando", cancellationToken);
        await _userInteractor.PublishTraceAsync($"[Escalonamento] Transição para avaliação direta do médico veterinário...", "info", cancellationToken);

        string message = $"Dr(a). Veterinário(a) foi notificado(a) e está revisando os dados de **{workflowContext.Patient.PetName}** ({workflowContext.Patient.Species}).\n\n" +
            $"Como se trata de avaliação clínica de sintomas ou exames, o profissional continuará a orientação personalizada com você diretamente por aqui em instantes.";

        await _userInteractor.SendUserResponseAsync(
            message,
            agentId: "vet-handoff",
            audience: MessageAudience.Both,
            cancellationToken: cancellationToken);

        // Ativa o split-mode para intervenção do profissional humano
        await _userInteractor.PublishSplitModeAsync(true, cancellationToken);
        await _userInteractor.PublishContextAsync(
            "waiting_vet",
            $"Atendimento: {workflowContext.Patient.PetName}",
            "Aguardando avaliação clínica do médico veterinário.",
            "vet-handoff",
            true,
            cancellationToken);

        await _userInteractor.PublishTraceAsync("[Handoff Ativo] Modo híbrido / veterinário habilitado no painel", "success", cancellationToken);
        await _userInteractor.PublishAgentStateAsync("vet-handoff", "done", "Aguardando Vet", cancellationToken);

        workflowContext.HandedOffToVet = true;

        await context.YieldOutputAsync(workflowContext, cancellationToken);
        return workflowContext;
    }
}
