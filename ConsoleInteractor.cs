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
/*
internal sealed class ConsoleInteractor : IUserInteractor
{
    /// <summary>
    /// Gets a user response from the console after displaying a prompt.
    /// Validates that the input is not empty before returning.
    /// </summary>
    /// <param name="prompt">The prompt to display to the user</param>
    /// <param name="cancellationToken">Cancellation token for the input request</param>
    /// <returns>The non-empty user input</returns>
    public async Task<string> GetUserResponseAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            prompt = "Por favor, responda:";
        }

        while (true)
        {
            Console.Write(prompt + " ");
            string? input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input))
            {
                return input;
            }
            Console.WriteLine("Entrada inválida. Por favor, responda corretamente.");
        }
    }

    public Task SetAgentTypingAsync(string label, bool on, CancellationToken cancellationToken = default)
    {
        // Console interactor does not support typing indicators,
        // so this is a no-op in terminal mode.
        return Task.CompletedTask;
    }

    public Task PublishTraceAsync(string title, string icon = "terminal", string color = "primary", CancellationToken cancellationToken = default)
    {
        // Console interactor does not support trace publishing,
        // so this is a no-op in terminal mode.
        return Task.CompletedTask;
    }

    public Task PublishAgentStateAsync(string agentId, string state, string tag, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task PublishContextAsync(string status, string chatTitle, string chatSubtitle, string activeAgentId, bool humanMode, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task PublishSplitModeAsync(bool on, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task SendUserResponseAsync(string prompt, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

*/