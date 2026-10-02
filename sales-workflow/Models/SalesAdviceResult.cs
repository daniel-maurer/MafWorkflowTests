using System.Text.Json.Serialization;

namespace SalesWorkflow.Models;

public sealed class SalesAdviceResult
{
    [JsonPropertyName("message_for_user")]
    public string MessageForUser { get; set; } = string.Empty;

    [JsonPropertyName("customer_wants_quote")]
    public bool CustomerWantsQuote { get; set; }

    [JsonPropertyName("selected_products")]
    public List<ProductInfo> SelectedProducts { get; set; } = [];

    [JsonPropertyName("suggested_complements")]
    public List<ProductInfo> SuggestedComplements { get; set; } = [];

    [JsonPropertyName("suggested_alternatives")]
    public List<ProductInfo> SuggestedAlternatives { get; set; } = [];

    [JsonPropertyName("suggested_kits")]
    public List<KitSuggestion> SuggestedKits { get; set; } = [];
}

public sealed class KitSuggestion
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("products")]
    public List<string> ProductSkus { get; set; } = [];

    [JsonPropertyName("kit_price")]
    public decimal KitPrice { get; set; }

    [JsonPropertyName("original_price")]
    public decimal OriginalPrice { get; set; }

    [JsonPropertyName("discount_pct")]
    public decimal DiscountPct { get; set; }
}
