using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace VetWorkflow;

public static class WorkflowFactory
{
    /// <summary>
    /// Constrói o fluxo de trabalho VetAssistant com Microsoft Agent Framework, branching condicional e transições dinâmicas.
    /// </summary>
    internal static Workflow BuildVetAssistantWorkflow(IChatClient chatClient, IUserInteractor userInteractor)
    {
        if (chatClient == null) throw new ArgumentNullException(nameof(chatClient));
        if (userInteractor == null) throw new ArgumentNullException(nameof(userInteractor));

        RequestPort userMessagePort = RequestPort.Create<string, string>("UserMessage");

        // Agentes
        var triageAgent = VetTriageAgentFactory.GetTriageAgent(chatClient);
        var emergencyAgent = VetEmergencyAgentFactory.GetEmergencyAgent(chatClient);
        var dataCollectorAgent = VetDataCollectorAgentFactory.GetDataCollectorAgent(chatClient);
        var patientFileAgent = VetPatientFileAgentFactory.GetPatientFileAgent(chatClient);
        var schedulingAgent = VetSchedulingAgentFactory.GetSchedulingAgent(chatClient);
        var postCareAgent = VetPostCareAgentFactory.GetPostCareAgent(chatClient);
        var vetHandoffAgent = VetHandoffAgentFactory.GetVetHandoffAgent(chatClient);
        var followUpAgent = VetFollowUpAgentFactory.GetFollowUpAgent(chatClient);
        var summaryAgent = VetSummaryAgentFactory.GetSummaryAgent(chatClient);

        // Executores
        var triageExec = new VetTriageExecutor(triageAgent, userInteractor);
        var emergencyExec = new EmergencyEscalationExecutor(emergencyAgent, userInteractor);
        var dataCollectorExec = new DataCollectionExecutor(dataCollectorAgent, userInteractor);
        var patientFileExec = new PatientFileExecutor(patientFileAgent, userInteractor);
        var schedulingExec = new SchedulingExecutor(schedulingAgent, userInteractor);
        var postCareExec = new PostCareExecutor(postCareAgent, userInteractor);
        var vetHandoffExec = new VetHandoffExecutor(vetHandoffAgent, userInteractor);
        var followUpExec = new FollowUpExecutor(followUpAgent, userInteractor);
        var summaryExec = new SummaryExecutor(summaryAgent, userInteractor);

        return new WorkflowBuilder(userMessagePort)
            .AddEdge(userMessagePort, triageExec)

            // Branching pós-triagem: Emergência vs Rotina/Urgência
            .AddEdge(triageExec, emergencyExec, condition: GetEmergencyCondition())
            .AddEdge(triageExec, dataCollectorExec, condition: GetNonEmergencyCondition())

            // Coleta para prontuário/ficha
            .AddEdge(dataCollectorExec, patientFileExec)

            // Branching pós-ficha: Agendamento vs Orientações vs Handoff Clínico
            .AddEdge(patientFileExec, schedulingExec, condition: GetSchedulingCondition())
            .AddEdge(patientFileExec, postCareExec, condition: GetPostCareCondition())
            .AddEdge(patientFileExec, vetHandoffExec, condition: GetVetHandoffCondition())

            // Branching dinâmico pós-atendimento do veterinário:
            // 1. Se o veterinário orientou marcar consulta -> Agente de Agendamento assume
            .AddEdge(vetHandoffExec, schedulingExec, condition: GetHandoffNeedsSchedulingCondition())
            // 2. Se o veterinário concluiu sem necessidade de agendamento -> segue para Follow-Up
            .AddEdge(vetHandoffExec, followUpExec, condition: GetHandoffCompletedCondition())

            // Fan-in: As rotas convergem para FollowUp
            .AddEdge(emergencyExec, followUpExec)
            .AddEdge(schedulingExec, followUpExec)
            .AddEdge(postCareExec, followUpExec)

            // FollowUp para Resumo Clínico Final
            .AddEdge(followUpExec, summaryExec)

            .Build();
    }

    private static Func<object?, bool> GetEmergencyCondition() =>
        res => res is VetWorkflowContext ctx && ctx.Triage.Urgency == "EMERGENCY";

    private static Func<object?, bool> GetNonEmergencyCondition() =>
        res => res is VetWorkflowContext ctx && ctx.Triage.Urgency != "EMERGENCY";

    private static Func<object?, bool> GetSchedulingCondition() =>
        res => res is VetWorkflowContext ctx && IsSchedulingTheme(ctx.Triage.Theme);

    private static Func<object?, bool> GetPostCareCondition() =>
        res => res is VetWorkflowContext ctx && ctx.Triage.Theme == "orientação_pós_consulta";

    private static Func<object?, bool> GetVetHandoffCondition() =>
        res => res is VetWorkflowContext ctx && !IsSchedulingTheme(ctx.Triage.Theme) && ctx.Triage.Theme != "orientação_pós_consulta";

    private static Func<object?, bool> GetHandoffNeedsSchedulingCondition() =>
        res => res is VetWorkflowContext ctx && ctx.NextAction == "scheduling";

    private static Func<object?, bool> GetHandoffCompletedCondition() =>
        res => res is VetWorkflowContext ctx && ctx.NextAction != "scheduling";

    private static bool IsSchedulingTheme(string theme) =>
        theme is "vacina" or "retorno" or "exame" or "vermifugação" or "castração" or "consulta" or "agendamento";
}
