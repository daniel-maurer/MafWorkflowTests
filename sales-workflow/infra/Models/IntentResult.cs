using System.Text.Json.Serialization;

namespace SalesWorkflow.Models;

public sealed class IntentResult
{
    [JsonPropertyName("is_understood")]
    public bool IsUnderstood { get; set; }

    [JsonPropertyName("intent")]
    public string Intent { get; set; } = string.Empty;

    [JsonPropertyName("question_for_user")]
    public string QuestionForUser { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("extracted_product_query")]
    public string ExtractedProductQuery { get; set; } = string.Empty;

    [JsonPropertyName("extracted_filters")]
    public ProductFilters? Filters { get; set; }

    [JsonPropertyName("customer_sentiment")]
    public string CustomerSentiment { get; set; } = "neutral";

    [JsonPropertyName("requires_human")]
    public bool RequiresHuman { get; set; }
}

public sealed class ProductFilters
{
    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("size")]
    public string? Size { get; set; }

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("min_price")]
    public decimal? MinPrice { get; set; }

    [JsonPropertyName("max_price")]
    public decimal? MaxPrice { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }
}
