using System.Text.Json.Serialization;

namespace SalesWorkflow.Models;

public sealed class PaymentConditionInfo
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("paymentMethod")] public string PaymentMethod { get; set; } = "pix";
    [JsonPropertyName("maxInstallments")] public int MaxInstallments { get; set; } = 1;
    [JsonPropertyName("interestFree")] public bool InterestFree { get; set; } = true;
    [JsonPropertyName("interestRate")] public decimal InterestRate { get; set; }
    [JsonPropertyName("additionalDiscount")] public decimal AdditionalDiscount { get; set; }
    [JsonPropertyName("active")] public bool Active { get; set; } = true;
}
