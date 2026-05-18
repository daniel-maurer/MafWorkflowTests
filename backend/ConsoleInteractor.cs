namespace SupportWorkflow;

/// <summary>
/// Handles console-based user interaction for the support workflow.
/// </summary>
internal interface IUserInteractor
{
    Task<string> GetUserResponseAsync(string prompt, CancellationToken cancellationToken = default);
    Task SendUserResponseAsync(string prompt, CancellationToken cancellationToken = default);
    Task SetAgentTypingAsync(string label, bool on, CancellationToken cancellationToken = default);
    Task PublishTraceAsync(string title, string icon = "terminal", string color = "primary", CancellationToken cancellationToken = default);
    Task PublishAgentStateAsync(string agentId, string state, string tag, CancellationToken cancellationToken = default);
    Task PublishContextAsync(string status, string chatTitle, string chatSubtitle, string activeAgentId, bool humanMode, CancellationToken cancellationToken = default);
    Task PublishSplitModeAsync(bool on, CancellationToken cancellationToken = default);
}
