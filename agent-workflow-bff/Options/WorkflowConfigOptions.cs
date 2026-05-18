namespace AgentWorkflow.Bff.Options;

public sealed class WorkflowConfigOptions
{
    public const string SectionName = "WorkflowConfig";

    public string Folder { get; init; } = "Workflows";
    public bool ReloadOnChange { get; init; } = false;
}
