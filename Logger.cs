namespace SupportWorkflow;

/// <summary>
/// Logger utility for the support workflow system.
/// Logs are prefixed with [LOG] to distinguish them from user messages.
/// Can be disabled by setting EnableLogging to false.
/// </summary>
public static class Logger
{
    private const string AnsiReset = "\u001b[0m";
    private const string AnsiBlue = "\u001b[34m";
    private const string AnsiGreen = "\u001b[32m";
    private const string AnsiOrange = "\u001b[38;5;214m";
    private const string AnsiGrey = "\u001b[38;5;244m";

    private static void WriteColored(string message, string color)
    {
        Console.WriteLine($"{color}{message}{AnsiReset}");
    }

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

    public static void LogExecutorResult(string message)
    {

        WriteColored(message, AnsiBlue);
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
        WriteColored(message, AnsiOrange);
    }

    /// <summary>
    /// Outputs a system/automatic message in blue.
    /// </summary>
    public static void OutputSystem(string message)
    {
        if (EnableLogging)
        {
            WriteColored(message, AnsiGrey);
        }
    }

    /// <summary>
    /// Outputs an agent or AI message in green.
    /// </summary>
    public static void OutputAgent(string message)
    {
        WriteColored(message, AnsiGreen);
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
