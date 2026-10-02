using System.Text.Json.Serialization;

namespace SalesWorkflow.Models;

public sealed class CatalogResult
{
    [JsonPropertyName("has_results")]
    public bool HasResults { get; set; }

    [JsonPropertyName("products")]
    public List<ProductInfo> Products { get; set; } = [];

    [JsonPropertyName("message_for_user")]
    public string MessageForUser { get; set; } = string.Empty;

    [JsonPropertyName("requires_human")]
    public bool RequiresHuman { get; set; }

    [JsonPropertyName("original_intent")]
    public string OriginalIntent { get; set; } = string.Empty;
}

public sealed class ProductInfo
{
    [JsonPropertyName("sku")]
    public string Sku { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "BRL";

    [JsonPropertyName("image_url")]
    public string ImageUrl { get; set; } = string.Empty;

    [JsonPropertyName("in_stock")]
    public bool InStock { get; set; }

    [JsonPropertyName("stock_qty")]
    public int StockQty { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("size")]
    public string? Size { get; set; }

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("compatible_skus")]
    public List<string> CompatibleSkus { get; set; } = [];
}
