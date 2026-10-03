using System.Text.Json.Serialization;

namespace SalesWorkflow.Models;

public sealed class DiscountInfo
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("discountType")] public string DiscountType { get; set; } = "percentage";
    [JsonPropertyName("discountValue")] public decimal DiscountValue { get; set; }
    [JsonPropertyName("minOrderValue")] public decimal? MinOrderValue { get; set; }
    [JsonPropertyName("maxDiscountAmount")] public decimal? MaxDiscountAmount { get; set; }
    [JsonPropertyName("validFrom")] public DateTimeOffset ValidFrom { get; set; }
    [JsonPropertyName("validUntil")] public DateTimeOffset? ValidUntil { get; set; }
    [JsonPropertyName("paymentMethods")] public List<string> PaymentMethods { get; set; } = [];
    [JsonPropertyName("stackable")] public bool Stackable { get; set; }
    [JsonPropertyName("isValid")] public bool IsValid { get; set; } = true;
    [JsonPropertyName("targetProductSkus")] public List<string> TargetProductSkus { get; set; } = [];
}
