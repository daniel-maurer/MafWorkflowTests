using System.Text.Json.Serialization;

namespace SalesWorkflow.Models;

public sealed class CustomerHighlightsResult
{
    [JsonPropertyName("highlights")]
    public string Highlights { get; set; } = string.Empty;
}
