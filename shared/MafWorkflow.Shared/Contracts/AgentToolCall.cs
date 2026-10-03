namespace MafWorkflow.Shared;

public sealed class AgentToolCall
{
    public string Name { get; init; } = string.Empty;
    public string Args { get; init; } = string.Empty;
    public bool Ok { get; init; } = true;
}
