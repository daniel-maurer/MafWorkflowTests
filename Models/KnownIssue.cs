using System.Text.Json.Serialization;

namespace SupportWorkflow;

/// <summary>
/// Represents a known issue in the knowledge base with symptoms, keywords, and resolution information.
/// </summary>
public class KnownIssue
{
    /// <summary>
    /// Gets or sets the problem title.
    /// </summary>
    [JsonPropertyName("problema")]
    public string Problem { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of symptoms associated with this problem.
    /// </summary>
    [JsonPropertyName("sintomas")]
    public List<string> Symptoms { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets keywords for searching/matching this issue.
    /// </summary>
    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the solution or workaround for this problem.
    /// </summary>
    [JsonPropertyName("solucao")]
    public string? Solution { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this issue requires action from support team.
    /// </summary>
    [JsonPropertyName("requer_acao")]
    public bool ActionRequired { get; set; }

    /// <summary>
    /// Gets or sets the MCP (Model Context Protocol) action to execute for resolution.
    /// </summary>
    [JsonPropertyName("acao_mcp")]
    public string? McpAction { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the success rate of the resolution for this issue (0-1).
    /// </summary>
    [JsonPropertyName("taxa_sucesso")]
    public double SuccessRate { get; set; } = 0;

    /// <summary>
    /// Gets or sets the expected time to resolve this issue.
    /// </summary>
    [JsonPropertyName("prazo_resolucao")]
    public TimeSpan? ResolutionTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets the list of tools/functions that need to be called to resolve this issue.
    /// </summary>
    [JsonPropertyName("tools_required")]
    public List<string> ToolsRequired { get; set; } = new List<string>();
}