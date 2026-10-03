namespace SalesAdmin.Api.Entities;

public sealed class PaymentCondition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = "pix"; // "pix", "credit_card", "debit_card", "boleto"
    public int MaxInstallments { get; set; } = 1;
    public bool InterestFree { get; set; } = true;
    public decimal InterestRate { get; set; } = 0;
    public decimal AdditionalDiscount { get; set; } = 0;
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
