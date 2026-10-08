namespace SalesAdmin.Api.Entities;

public sealed class DeliveryMethod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "delivery"; // "delivery", "pickup"
    public string? Description { get; set; }
    public decimal Price { get; set; } = 0;
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
