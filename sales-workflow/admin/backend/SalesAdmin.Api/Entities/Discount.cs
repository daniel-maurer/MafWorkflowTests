using System.Text.Json.Serialization;
using Pgvector;

namespace SalesAdmin.Api.Entities;

public sealed class Discount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DiscountType { get; set; } = "percentage"; // "percentage" | "fixed_amount"
    public decimal DiscountValue { get; set; }
    public decimal? MinOrderValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public DateTimeOffset ValidFrom { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ValidUntil { get; set; }
    public List<string> PaymentMethods { get; set; } = []; // ["pix", "credit_card", "debit_card", "boleto"]
    public int? MaxUses { get; set; }
    public int CurrentUses { get; set; } = 0;
    public bool Active { get; set; } = true;
    public bool Stackable { get; set; } = false;

    [JsonIgnore]
    public Vector? Embedding { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public List<Product> TargetProducts { get; set; } = [];
}
