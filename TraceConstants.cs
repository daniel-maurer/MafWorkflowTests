namespace SupportWorkflow;

/// <summary>
/// Constants for trace icons and messages used throughout the workflow.
/// </summary>
internal static class TraceConstants
{
    // Trace Icons
    public const string IconTerminal = "terminal";
    public const string IconGitBranch = "git-branch";
    public const string IconBarChart = "bar-chart-2";
    public const string IconDatabase = "database";
    public const string IconWrench = "wrench";
    public const string IconSiren = "siren";
    public const string IconFileSearch = "file-search";
    public const string IconTag = "tag";
    public const string IconUserCheck = "user-check";
    public const string IconBookOpen = "book-open";
    public const string IconPickaxe = "pickaxe";
    public const string IconPenLine = "pen-line";

    // Agent Names
    public const string AgentTriage = "Triage Agent analyzing";
    public const string AgentFrequentProblem = "Frequent Problem Agent searching";
    public const string AgentResolution = "Resolution Agent executing";
    public const string AgentHumanSupport = "Human Support Agent";
    public const string AgentPatternRecord = "Pattern Record Agent analyzing";

    // Trace Levels
    public const string LevelInfo = "info";
    public const string LevelDebug = "debug";
    public const string LevelWarn = "warn";
    public const string LevelError = "error";
    public const string LevelSuccess = "success";

    // Trace Colors (frontend display variants)
    public const string ColorPrimary = "primary";
    public const string ColorSuccess = "success";
    public const string ColorError = "error";
    public const string ColorWarning = "warning";
}
