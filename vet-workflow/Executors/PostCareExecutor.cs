using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace VetWorkflow;

/// <summary>
/// Executor responsável por resgatar e transmitir orientações de cuidados pós-consulta aprovadas pelo veterinário.
/// </summary>
internal sealed class PostCareExecutor : Executor<VetWorkflowContext, VetWorkflowContext>
{
    private readonly AIAgent _postCareAgent;
    private readonly IUserInteractor _userInteractor;

    public PostCareExecutor(AIAgent postCareAgent, IUserInteractor userInteractor) : base("PostCareExecutor")
    {
        _postCareAgent = postCareAgent ?? throw new ArgumentNullException(nameof(postCareAgent));
        _userInteractor = userInteractor ?? throw new ArgumentNullException(nameof(userInteractor));
    }

    public override async ValueTask<VetWorkflowContext> HandleAsync(VetWorkflowContext workflowContext, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        await _userInteractor.PublishAgentStateAsync("post-care", "active", "Buscando Orientações", cancellationToken);
        await _userInteractor.SetAgentTypingAsync("Carregando orientações pós-atendimento aprovadas...", true, cancellationToken);
        await _userInteractor.PublishTraceAsync("[Pós-Consulta] Resgatando protocolo oficial de cuidados...", "info", cancellationToken);

        string procedureQuery = workflowContext.InitialUserMessage.ToLowerInvariant().Contains("castra") ? "castração" : "vacina";
        string approvedInstructionsJson = await VetPostCareTools.GetApprovedInstructions(procedureQuery, cancellationToken);

        string instructionsText = "Siga o repouso recomendado, mantenha curativo limpo e seco, e use colar elizabetano até a cicatrização dos pontos.";
        string redFlags = "Em caso de inchaço, secreção ou apatia, avise a clínica.";

        try
        {
            var doc = JsonDocument.Parse(approvedInstructionsJson);
            if (doc.RootElement.TryGetProperty("instructions", out var instProp))
            {
                instructionsText = instProp.GetString() ?? instructionsText;
            }
            if (doc.RootElement.TryGetProperty("redFlags", out var rfProp))
            {
                redFlags = rfProp.GetString() ?? redFlags;
            }
        }
        catch { }

        // Validação de segurança obrigatória
        instructionsText = VetSafetyGuardrail.SanitizeOrEscalate(instructionsText, out _);

        string clientMessage = $"📋 **Orientações de Cuidados Pós-Atendimento:**\n\n" +
            $"{instructionsText}\n\n" +
            $"⚠️ **Sinais de Atenção:** {redFlags}\n\n" +
            $"*Importante: Todas as medicações devem seguir rigorosamente a receita emitida pelo médico veterinário.*";

        await VetPostCareTools.SendPostCareMessage(workflowContext.Patient.PatientId, instructionsText, cancellationToken);

        await _userInteractor.SetAgentTypingAsync("Carregando orientações pós-atendimento aprovadas...", false, cancellationToken);
        await _userInteractor.PublishTraceAsync("[Pós-Consulta] Protocolo oficial aprovado enviado com sucesso", "success", cancellationToken);

        await _userInteractor.SendUserResponseAsync(
            clientMessage,
            agentId: "post-care",
            audience: MessageAudience.Both,
            cancellationToken: cancellationToken);

        await _userInteractor.PublishAgentStateAsync("post-care", "done", "Enviado", cancellationToken);
        workflowContext.PostCareNotes = instructionsText;

        await context.YieldOutputAsync(workflowContext, cancellationToken);
        return workflowContext;
    }
}
