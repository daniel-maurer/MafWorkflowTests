namespace VetWorkflow;

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

    public static bool EnableLogging { get; set; } = true;

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

    public static void LogError(string message)
    {
        if (EnableLogging)
        {
            Console.WriteLine($"[LOG ERROR] {message}");
        }
    }

    public static void LogWarning(string message)
    {
        if (EnableLogging)
        {
            Console.WriteLine($"[LOG WARNING] {message}");
        }
    }

    public static void LogDebug(string message)
    {
        if (EnableLogging)
        {
            Console.WriteLine($"[LOG DEBUG] {message}");
        }
    }

    public static void OutputUser(string message)
    {
        WriteColored(message, AnsiOrange);
    }

    public static void OutputSystem(string message)
    {
        if (EnableLogging)
        {
            WriteColored(message, AnsiGrey);
        }
    }

    public static void OutputAgent(string message)
    {
        WriteColored(message, AnsiGreen);
    }

    public static void DisableLogging()
    {
        EnableLogging = false;
    }

    public static void EnableAllLogging()
    {
        EnableLogging = true;
    }
}
