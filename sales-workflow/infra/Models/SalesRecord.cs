using System.Text.Json.Serialization;

namespace SalesWorkflow.Models;

public sealed class SalesRecord
{
    [JsonPropertyName("interaction_id")]
    public string InteractionId { get; set; } = string.Empty;

    [JsonPropertyName("outcome")]
    public string Outcome { get; set; } = string.Empty;

    [JsonPropertyName("products_shown")]
    public List<string> ProductsShown { get; set; } = [];

    [JsonPropertyName("quote_generated")]
    public bool QuoteGenerated { get; set; }

    [JsonPropertyName("human_involved")]
    public bool HumanInvolved { get; set; }

    [JsonPropertyName("customer_sentiment")]
    public string CustomerSentiment { get; set; } = "neutral";

    [JsonPropertyName("recommendations")]
    public string Recommendations { get; set; } = string.Empty;
}
