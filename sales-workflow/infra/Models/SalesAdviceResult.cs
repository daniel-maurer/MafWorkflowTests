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

    [JsonPropertyName("accepted_kit_name")]
    public string? AcceptedKitName { get; set; }

    [JsonPropertyName("discount_percent")]
    public decimal DiscountPercent { get; set; }

    [JsonPropertyName("next_action")]
    public string NextAction { get; set; } = string.Empty;

    [JsonPropertyName("new_search_query")]
    public string? NewSearchQuery { get; set; }
}

public sealed class CustomerChoiceEvaluation
{
    [JsonPropertyName("next_action")]
    public string NextAction { get; set; } = "checkout"; // "checkout", "search_more", "decline"

    [JsonPropertyName("new_search_query")]
    public string? NewSearchQuery { get; set; }

    [JsonPropertyName("wants_quote")]
    public bool WantsQuote { get; set; }

    [JsonPropertyName("accepted_kit_name")]
    public string? AcceptedKitName { get; set; }

    [JsonPropertyName("accepted_skus")]
    public List<string> AcceptedSkus { get; set; } = [];

    [JsonPropertyName("discount_percent")]
    public decimal DiscountPercent { get; set; }

    [JsonPropertyName("wants_only_original")]
    public bool WantsOnlyOriginal { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("message_for_user")]
    public string MessageForUser { get; set; } = string.Empty;

    [JsonPropertyName("target_sku")]
    public string? TargetSku { get; set; }
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

public sealed class KitPriceCalculation
{
    [JsonPropertyName("product_skus")]
    public List<string> ProductSkus { get; set; } = [];

    [JsonPropertyName("original_price")]
    public decimal OriginalPrice { get; set; }

    [JsonPropertyName("discount_percent")]
    public decimal DiscountPercent { get; set; }

    [JsonPropertyName("discount_amount")]
    public decimal DiscountAmount { get; set; }

    [JsonPropertyName("final_price")]
    public decimal FinalPrice { get; set; }
}
