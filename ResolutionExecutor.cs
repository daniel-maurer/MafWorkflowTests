using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace SupportWorkflow;

/// <summary>
/// Executor responsible for attempting automatic resolution of support issues.
/// Coordinates with available tools and escalates complex issues to human support.
/// </summary>
internal sealed class ResolutionExecutor : Executor<string, string>
{
    private readonly AIAgent _resolutionAgent;
    private readonly ConsoleInteractor _consoleInteractor;

    /// <summary>
    /// Initializes a new instance of the ResolutionExecutor.
    /// </summary>
    /// <param name="resolutionAgent">The agent for attempting issue resolution</param>
    /// <param name="consoleInteractor">The console interactor for user communication</param>
    public ResolutionExecutor(AIAgent resolutionAgent, ConsoleInteractor consoleInteractor) : base("ResolutionExecutor")
    {
        this._resolutionAgent = resolutionAgent ?? throw new ArgumentNullException(nameof(resolutionAgent));
        this._consoleInteractor = consoleInteractor ?? throw new ArgumentNullException(nameof(consoleInteractor));
    }

    /// <summary>
    /// Handles the resolution attempt for a support issue using available tools.
    /// </summary>
    /// <param name="userMessage">Additional context or user confirmation message</param>
    /// <param name="context">The workflow context for state management</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A resolution message for the user</returns>
    public override async ValueTask<string> HandleAsync(string userMessage, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var history = await context.ReadStateAsync<List<ChatMessage>>(Constants.ConversationHistoryKey, Constants.TriageStateScope) ?? new();
        
        if (history.Count == 0)
        {
            return "No context found for resolution. Please start with a new ticket.";
        }

        history.Add(new ChatMessage(ChatRole.User, userMessage));
        
        try
        {
            var response = await this._resolutionAgent.RunAsync(history, cancellationToken: cancellationToken);
            
            history.Add(new ChatMessage(ChatRole.Assistant, response.Text));
            await context.QueueStateUpdateAsync(Constants.ConversationHistoryKey, history, Constants.TriageStateScope);
            
            return response.Text;
        }
        catch (OperationCanceledException)
        {
            return "Resolution process was cancelled. Please try again.";
        }
        catch (Exception ex)
        {
            return $"Resolution process failed: {ex.Message}. Please contact support.";
        }
    }
}