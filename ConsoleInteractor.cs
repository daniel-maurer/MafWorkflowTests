namespace SupportWorkflow;

/// <summary>
/// Handles console-based user interaction for the support workflow.
/// </summary>
internal interface IUserInteractor
{
    Task<string> GetUserResponseAsync(string prompt, CancellationToken cancellationToken = default);
}

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
}