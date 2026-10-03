using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesAdmin.Api.Data;
using SalesAdmin.Api.Entities;
using SalesAdmin.Api.Services;

namespace SalesAdmin.Api.Endpoints;

public static class DiscountEndpoints
{
    public static RouteGroupBuilder MapDiscountEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            [FromServices] SalesAdminDbContext db,
            [FromQuery] bool? active,
            [FromQuery] bool? validNow,
            [FromQuery] string? paymentMethod) =>
        {
            var query = db.Discounts.Include(d => d.TargetProducts).AsQueryable();

            if (active.HasValue) query = query.Where(d => d.Active == active.Value);

            if (validNow == true)
            {
                var now = DateTimeOffset.UtcNow;
                query = query.Where(d => d.Active && d.ValidFrom <= now && (d.ValidUntil == null || d.ValidUntil >= now));
            }

            if (!string.IsNullOrWhiteSpace(paymentMethod))
            {
                query = query.Where(d => d.PaymentMethods.Contains(paymentMethod));
            }

            var items = await query.OrderByDescending(d => d.CreatedAt).ToListAsync();
            return Results.Ok(items);
        });

        group.MapGet("/{id:guid}", async (Guid id, [FromServices] SalesAdminDbContext db) =>
        {
            var discount = await db.Discounts.Include(d => d.TargetProducts).FirstOrDefaultAsync(d => d.Id == id);
            return discount is not null ? Results.Ok(discount) : Results.NotFound();
        });

        group.MapGet("/code/{code}", async (string code, [FromServices] SalesAdminDbContext db) =>
        {
            var now = DateTimeOffset.UtcNow;
            var discount = await db.Discounts
                .Include(d => d.TargetProducts)
                .FirstOrDefaultAsync(d => d.Code.Equals(code, StringComparison.OrdinalIgnoreCase) && d.Active);

            if (discount is null) return Results.NotFound(new { error = "Cupom não encontrado ou inativo." });

            var isValid = discount.ValidFrom <= now &&
                          (discount.ValidUntil == null || discount.ValidUntil >= now) &&
                          (!discount.MaxUses.HasValue || discount.CurrentUses < discount.MaxUses.Value);

            return Results.Ok(new
            {
                discount.Id,
                discount.Code,
                discount.Name,
                discount.Description,
                discount.DiscountType,
                discount.DiscountValue,
                discount.MinOrderValue,
                discount.MaxDiscountAmount,
                discount.ValidFrom,
                discount.ValidUntil,
                discount.PaymentMethods,
                discount.Stackable,
                IsValid = isValid,
                TargetProductSkus = discount.TargetProducts.Select(p => p.Sku).ToList()
            });
        });

        group.MapPost("/", async (
            [FromBody] CreateDiscountDto dto,
            [FromServices] SalesAdminDbContext db,
            [FromServices] EmbeddingService embeddingService,
            CancellationToken ct) =>
        {
            if (await db.Discounts.AnyAsync(d => d.Code == dto.Code, ct))
            {
                return Results.Conflict(new { error = $"Cupom com o código '{dto.Code}' já existe." });
            }

            var discount = new Discount
            {
                Code = dto.Code.Trim().ToUpperInvariant(),
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim(),
                DiscountType = dto.DiscountType.Trim().ToLowerInvariant(),
                DiscountValue = dto.DiscountValue,
                MinOrderValue = dto.MinOrderValue,
                MaxDiscountAmount = dto.MaxDiscountAmount,
                ValidFrom = dto.ValidFrom ?? DateTimeOffset.UtcNow,
                ValidUntil = dto.ValidUntil,
                PaymentMethods = dto.PaymentMethods ?? ["pix", "credit_card", "boleto"],
                MaxUses = dto.MaxUses,
                Active = dto.Active,
                Stackable = dto.Stackable
            };

            var text = EmbeddingService.BuildDiscountEmbeddingText(discount);
            discount.Embedding = await embeddingService.GenerateEmbeddingAsync(text, ct);

            db.Discounts.Add(discount);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/discounts/{discount.Id}", discount);
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateDiscountDto dto,
            [FromServices] SalesAdminDbContext db,
            [FromServices] EmbeddingService embeddingService,
            CancellationToken ct) =>
        {
            var discount = await db.Discounts.FindAsync([id], ct);
            if (discount is null) return Results.NotFound();

            discount.Code = dto.Code.Trim().ToUpperInvariant();
            discount.Name = dto.Name.Trim();
            discount.Description = dto.Description?.Trim();
            discount.DiscountType = dto.DiscountType.Trim().ToLowerInvariant();
            discount.DiscountValue = dto.DiscountValue;
            discount.MinOrderValue = dto.MinOrderValue;
            discount.MaxDiscountAmount = dto.MaxDiscountAmount;
            if (dto.ValidFrom.HasValue) discount.ValidFrom = dto.ValidFrom.Value;
            discount.ValidUntil = dto.ValidUntil;
            discount.PaymentMethods = dto.PaymentMethods ?? [];
            discount.MaxUses = dto.MaxUses;
            discount.Active = dto.Active;
            discount.Stackable = dto.Stackable;
            discount.UpdatedAt = DateTimeOffset.UtcNow;

            var text = EmbeddingService.BuildDiscountEmbeddingText(discount);
            discount.Embedding = await embeddingService.GenerateEmbeddingAsync(text, ct);

            await db.SaveChangesAsync(ct);
            return Results.Ok(discount);
        });

        group.MapPut("/{id:guid}/products", async (
            Guid id,
            [FromBody] List<Guid> productIds,
            [FromServices] SalesAdminDbContext db,
            CancellationToken ct) =>
        {
            var discount = await db.Discounts.Include(d => d.TargetProducts).FirstOrDefaultAsync(d => d.Id == id, ct);
            if (discount is null) return Results.NotFound();

            var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync(ct);
            discount.TargetProducts.Clear();
            discount.TargetProducts.AddRange(products);
            discount.UpdatedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);
            return Results.Ok(discount);
        });

        group.MapDelete("/{id:guid}", async (Guid id, [FromServices] SalesAdminDbContext db) =>
        {
            var discount = await db.Discounts.FindAsync(id);
            if (discount is null) return Results.NotFound();

            discount.Active = false;
            discount.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return group;
    }
}

public sealed record CreateDiscountDto(
    string Code,
    string Name,
    string? Description,
    string DiscountType,
    decimal DiscountValue,
    decimal? MinOrderValue,
    decimal? MaxDiscountAmount,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    List<string>? PaymentMethods,
    int? MaxUses,
    bool Active = true,
    bool Stackable = false);

public sealed record UpdateDiscountDto(
    string Code,
    string Name,
    string? Description,
    string DiscountType,
    decimal DiscountValue,
    decimal? MinOrderValue,
    decimal? MaxDiscountAmount,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    List<string>? PaymentMethods,
    int? MaxUses,
    bool Active = true,
    bool Stackable = false);
