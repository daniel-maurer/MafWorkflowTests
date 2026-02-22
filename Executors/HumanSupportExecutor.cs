using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace SupportWorkflow;

/// <summary>
/// Executor responsible for simulating human support interaction.
/// Provides a conversation flow between user, support agent, and human specialist.
/// </summary>
internal sealed class HumanSupportExecutor : Executor<FrequentProblemResult, ResolutionResult>
{
    private readonly ConsoleInteractor _consoleInteractor;

    /// <summary>
    /// Initializes a new instance of the HumanSupportExecutor.
    /// </summary>
    /// <param name="consoleInteractor">The console interactor for user communication</param>
    public HumanSupportExecutor(ConsoleInteractor consoleInteractor) : base("HumanSupportExecutor")
    {
        this._consoleInteractor = consoleInteractor ?? throw new ArgumentNullException(nameof(consoleInteractor));
    }

    /// <summary>
    /// Handles the simulated human support interaction for complex or unknown issues.
    /// </summary>
    /// <param name="frequentProblemResult">The result from the frequent problem executor</param>
    /// <param name="context">The workflow context for state management</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A ResolutionResult indicating the outcome of human support</returns>
    public override async ValueTask<ResolutionResult> HandleAsync(FrequentProblemResult frequentProblemResult, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (frequentProblemResult == null)
        {
            throw new ArgumentNullException(nameof(frequentProblemResult), "FrequentProblemResult cannot be null");
        }

        Logger.LogInfo("Starting human support handling for complex/unknown issue");
        Logger.OutputUser("\n" + new string('=', 80));
        Logger.OutputUser("[ATENDENTE SUPORTE] Vamos passar o atendimento para um humano especialista");
        Logger.OutputUser(new string('=', 80));
        
        // Simulate agent saying they'll escalate
        await Task.Delay(500, cancellationToken);
        
        Logger.OutputUser("\n[ATENDENTE SUPORTE] Aguarde um momento enquanto você é conectado...\n");
        Logger.LogDebug("Waiting for human support connection...");
        await Task.Delay(1000, cancellationToken);

        // Simulate human support agent taking over
        Logger.OutputUser("[ATENDENTE HUMANO] Olá! Estou com as informações do seu problema e já estou resolvendo.");
        Logger.OutputUser("[ATENDENTE HUMANO] Por favor aguarde enquanto analiso a situação...\n");
        Logger.LogDebug("Human agent taking over conversation");
        await Task.Delay(1000, cancellationToken);

        // Get user acknowledgment
        string userAck = _consoleInteractor.GetUserResponse("[USUÁRIO] Sua resposta");
        Logger.OutputUser($"\n[USUÁRIO] {userAck}\n");
        Logger.LogDebug($"User acknowledged: {userAck}");
        await Task.Delay(500, cancellationToken);

        // Human specialist provides solution
        string resolution = GetSimulatedResolution(frequentProblemResult.MessageForUser);
        Logger.OutputUser("[ESPECIALISTA HUMANO] " + resolution);
        Logger.OutputUser("");
        Logger.LogDebug($"Solution provided by specialist");
        await Task.Delay(1000, cancellationToken);

        // Get user confirmation
        string confirmation = _consoleInteractor.GetUserResponse("[USUÁRIO] Sua resposta");
        Logger.OutputUser($"\n[USUÁRIO] {confirmation}\n");
        Logger.LogDebug($"User confirmation: {confirmation}");
        
        bool isResolved = confirmation.ToLower() is "ok" or "obrigado" or "tá bom" or "valeu" or "sim" or "s" or "yes" or "ok, obrigado" or "muito obrigado";

        Logger.OutputUser(new string('=', 80));
        Logger.OutputUser("[SISTEMA] Finalizando atendimento com suporte humano");
        Logger.OutputUser(new string('=', 80) + "\n");
        Logger.LogInfo($"Human support interaction completed - Issue resolved: {isResolved}");

        var resolutionResult = new ResolutionResult
        {
            IsResolved = isResolved,
            RequiresHuman = false, // Already handled by human
            MessageForUser = $"Atendimento humano concluído. Problema resolvido: {isResolved}",
            ActionsExecuted = new List<string> { "HumanSupport" },
            EscalationReason = "Problema complexo ou desconhecido - resolvido por especialista humano"
        };

        await context.YieldOutputAsync(resolutionResult, cancellationToken);
        return resolutionResult;
    }

    /// <summary>
    /// Generates a simulated resolution response based on the problem description.
    /// </summary>
    /// <param name="problemDescription">The description of the problem from the user</param>
    /// <returns>A simulated resolution message</returns>
    private string GetSimulatedResolution(string problemDescription)
    {
        var resolutions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "queda", "Seu problema é uma queda do sistema. Em 15 minutos o sistema voltará ao ar. Não precisa fazer nada, só aguardar." },
            { "lento", "O sistema está lento porque temos uma manutenção em andamento. Deverá voltar ao normal em 30 minutos. Recomendo fazer uma pausa." },
            { "erro", "Identificamos um erro na sua conta. Vou resetar suas permissões agora. Tente fazer login novamente em 2 minutos." },
            { "acesso", "Seu acesso foi bloqueado por segurança. Vou desbloqueá-lo e enviar um email com instruções para resetar sua senha." },
            { "conexão", "Temos um problema com a conexão do seu servidor. Estou reiniciando-o agora, deve estar online em 5 minutos." },
            { "dados", "Seus dados foram recuperados com sucesso. Estou enviando um arquivo com todas as informações por email." }
        };

        // Try to find a matching resolution
        foreach (var resolution in resolutions)
        {
            if (problemDescription.Contains(resolution.Key, StringComparison.OrdinalIgnoreCase))
            {
                return resolution.Value;
            }
        }

        // Default resolution
        return "Identifiquei seu problema. Estou tomando as ações necessárias para resolvê-lo. Você receberá um email em breve com mais detalhes. Obrigado pela paciência!";
    }
}
