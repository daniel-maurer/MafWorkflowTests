namespace MafWorkflow.Shared;

public sealed class KbEntry
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public double Score { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string ResolutionType { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}
