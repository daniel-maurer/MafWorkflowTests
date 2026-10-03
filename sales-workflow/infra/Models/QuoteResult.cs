using System.Text.Json.Serialization;

namespace SalesWorkflow.Models;

public sealed class QuoteResult
{
    [JsonPropertyName("quote_id")]
    public string QuoteId { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<QuoteItem> Items { get; set; } = [];

    [JsonPropertyName("subtotal")]
    public decimal Subtotal { get; set; }

    [JsonPropertyName("discount")]
    public decimal Discount { get; set; }

    [JsonPropertyName("total")]
    public decimal Total { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "BRL";

    [JsonPropertyName("valid_until")]
    public DateTimeOffset ValidUntil { get; set; }

    [JsonPropertyName("payment_conditions")]
    public string PaymentConditions { get; set; } = string.Empty;

    [JsonPropertyName("message_for_user")]
    public string MessageForUser { get; set; } = string.Empty;

    [JsonPropertyName("customer_accepted")]
    public bool CustomerAccepted { get; set; }
}

public sealed class QuoteItem
{
    [JsonPropertyName("sku")]
    public string Sku { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("total")]
    public decimal Total { get; set; }
}
