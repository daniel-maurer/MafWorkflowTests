using System.ComponentModel.DataAnnotations.Schema;

namespace SalesAdmin.Api.Entities;

[Table("campaigns")]
public sealed class Campaign
{
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("start_date")]
    public DateTimeOffset StartDate { get; set; }

    [Column("end_date")]
    public DateTimeOffset EndDate { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("free_shipping")]
    public bool FreeShipping { get; set; } = false;

    [Column("global_discount_percent")]
    public decimal GlobalDiscountPercent { get; set; } = 0;

    [Column("discount_1_item")]
    public decimal Discount1Item { get; set; } = 0;

    [Column("discount_2_items")]
    public decimal Discount2Items { get; set; } = 0;

    [Column("discount_3_plus_items")]
    public decimal Discount3PlusItems { get; set; } = 0;

    [Column("custom_rules_json")]
    public string? CustomRulesJson { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
