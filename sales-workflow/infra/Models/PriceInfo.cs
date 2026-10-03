using System.Text.Json.Serialization;

namespace SalesWorkflow.Models;

public sealed class PriceInfo
{
    [JsonPropertyName("sku")] public string Sku { get; set; } = string.Empty;
    [JsonPropertyName("price")] public decimal Price { get; set; }
    [JsonPropertyName("original_price")] public decimal OriginalPrice { get; set; }
    [JsonPropertyName("currency")] public string Currency { get; set; } = "BRL";
    [JsonPropertyName("pix_price")] public decimal PixPrice { get; set; }
    [JsonPropertyName("installments")] public string Installments { get; set; } = string.Empty;
}

public sealed class CheckStockResult
{
    [JsonPropertyName("sku")] public string Sku { get; set; } = string.Empty;
    [JsonPropertyName("in_stock")] public bool InStock { get; set; }
    [JsonPropertyName("quantity")] public int Quantity { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
}
