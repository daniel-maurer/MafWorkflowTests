using System.Text.Json.Serialization;

namespace SalesWorkflow.Models;

public sealed class AbandonedCartInfo
{
    [JsonPropertyName("customerId")] public string CustomerId { get; set; } = string.Empty;
    [JsonPropertyName("cartId")] public string CartId { get; set; } = string.Empty;
    [JsonPropertyName("abandonedAt")] public DateTimeOffset AbandonedAt { get; set; }
    [JsonPropertyName("items")] public List<AbandonedCartItem> Items { get; set; } = [];
    [JsonPropertyName("total")] public decimal Total { get; set; }
    [JsonPropertyName("itemsStillAvailable")] public bool ItemsStillAvailable { get; set; } = true;
}

public sealed class AbandonedCartItem
{
    [JsonPropertyName("sku")] public string Sku { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("quantity")] public int Quantity { get; set; } = 1;
    [JsonPropertyName("price")] public decimal Price { get; set; }
}
