namespace SupportWorkflow;

/// <summary>
/// Logger utility for the support workflow system.
/// Logs are prefixed with [LOG] to distinguish them from user messages.
/// Can be disabled by setting EnableLogging to false.
/// </summary>
public static class Logger
{
    /// <summary>
    /// Gets or sets whether logging is enabled.
    /// Set to false to disable all logs.
    /// </summary>
    public static bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Logs an informational message with [LOG] prefix.
    /// </summary>
    /// <param name="message">The message to log</param>
    public static void LogInfo(string message)
    {
        if (EnableLogging)
        {
            Console.WriteLine($"[LOG] {message}");
        }
    }

    /// <summary>
    /// Logs an error message with [LOG ERROR] prefix.
    /// </summary>
    /// <param name="message">The error message to log</param>
    public static void LogError(string message)
    {
        if (EnableLogging)
        {
            Console.WriteLine($"[LOG ERROR] {message}");
        }
    }

    /// <summary>
    /// Logs a debug message with [LOG DEBUG] prefix.
    /// </summary>
    /// <param name="message">The debug message to log</param>
    public static void LogDebug(string message)
    {
        if (EnableLogging)
        {
            Console.WriteLine($"[LOG DEBUG] {message}");
        }
    }

    /// <summary>
    /// Outputs a user message (not a log).
    /// These messages appear without the [LOG] prefix.
    /// </summary>
    /// <param name="message">The user message to display</param>
    public static void OutputUser(string message)
    {
        Console.WriteLine(message);
    }

    /// <summary>
    /// Disables all logging output.
    /// </summary>
    public static void DisableLogging()
    {
        EnableLogging = false;
    }

    /// <summary>
    /// Enables logging output.
    /// </summary>
    public static void EnableAllLogging()
    {
        EnableLogging = true;
    }
}
