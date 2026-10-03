using System.Text.Json.Serialization;
using Pgvector;

namespace SalesAdmin.Api.Entities;

public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "BRL";
    public bool InStock { get; set; } = true;
    public int StockQty { get; set; } = 0;
    public string? Color { get; set; }
    public string? Size { get; set; }
    public string? Brand { get; set; }
    public Guid? CategoryId { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<string> CompatibleSkus { get; set; } = [];
    public string? ImageUrl { get; set; }

    [JsonIgnore]
    public Vector? Embedding { get; set; }

    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Category? Category { get; set; }
    public List<ProductImage> Images { get; set; } = [];

    [JsonIgnore]
    public List<Discount> Discounts { get; set; } = [];
}
