namespace SupportWorkflow;

/// <summary>
/// Handles console-based user interaction for the support workflow.
/// </summary>
internal sealed class ConsoleInteractor
{
    /// <summary>
    /// Gets a user response from the console after displaying a prompt.
    /// Validates that the input is not empty before returning.
    /// </summary>
    /// <param name="prompt">The prompt to display to the user</param>
    /// <returns>The non-empty user input</returns>
    public string GetUserResponse(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            prompt = "Please enter your response:";
        }

        while (true)
        {
            Console.Write(prompt + " ");
            string? input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input))
            {
                return input;
            }
            Console.WriteLine("Invalid input. Please enter a valid response.");
        }
    }
}