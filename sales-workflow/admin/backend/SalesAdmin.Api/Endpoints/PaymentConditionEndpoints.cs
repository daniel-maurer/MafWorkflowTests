using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesAdmin.Api.Data;
using SalesAdmin.Api.Entities;

namespace SalesAdmin.Api.Endpoints;

public static class PaymentConditionEndpoints
{
    public static RouteGroupBuilder MapPaymentConditionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async ([FromServices] SalesAdminDbContext db, [FromQuery] bool? active) =>
        {
            var query = db.PaymentConditions.AsQueryable();
            if (active.HasValue) query = query.Where(p => p.Active == active.Value);

            var items = await query.OrderBy(p => p.PaymentMethod).ThenBy(p => p.MaxInstallments).ToListAsync();
            return Results.Ok(items);
        });

        group.MapGet("/{id:guid}", async (Guid id, [FromServices] SalesAdminDbContext db) =>
        {
            var item = await db.PaymentConditions.FindAsync(id);
            return item is not null ? Results.Ok(item) : Results.NotFound();
        });

        group.MapPost("/", async ([FromBody] CreatePaymentConditionDto dto, [FromServices] SalesAdminDbContext db) =>
        {
            var condition = new PaymentCondition
            {
                Name = dto.Name.Trim(),
                PaymentMethod = dto.PaymentMethod.Trim().ToLowerInvariant(),
                MaxInstallments = dto.MaxInstallments,
                InterestFree = dto.InterestFree,
                InterestRate = dto.InterestRate,
                AdditionalDiscount = dto.AdditionalDiscount,
                Active = dto.Active
            };

            db.PaymentConditions.Add(condition);
            await db.SaveChangesAsync();
            return Results.Created($"/api/payment-conditions/{condition.Id}", condition);
        });

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdatePaymentConditionDto dto, [FromServices] SalesAdminDbContext db) =>
        {
            var condition = await db.PaymentConditions.FindAsync(id);
            if (condition is null) return Results.NotFound();

            condition.Name = dto.Name.Trim();
            condition.PaymentMethod = dto.PaymentMethod.Trim().ToLowerInvariant();
            condition.MaxInstallments = dto.MaxInstallments;
            condition.InterestFree = dto.InterestFree;
            condition.InterestRate = dto.InterestRate;
            condition.AdditionalDiscount = dto.AdditionalDiscount;
            condition.Active = dto.Active;
            condition.UpdatedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok(condition);
        });

        group.MapDelete("/{id:guid}", async (Guid id, [FromServices] SalesAdminDbContext db) =>
        {
            var condition = await db.PaymentConditions.FindAsync(id);
            if (condition is null) return Results.NotFound();

            condition.Active = false;
            condition.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return group;
    }
}

public sealed record CreatePaymentConditionDto(
    string Name,
    string PaymentMethod,
    int MaxInstallments,
    bool InterestFree = true,
    decimal InterestRate = 0,
    decimal AdditionalDiscount = 0,
    bool Active = true);

public sealed record UpdatePaymentConditionDto(
    string Name,
    string PaymentMethod,
    int MaxInstallments,
    bool InterestFree = true,
    decimal InterestRate = 0,
    decimal AdditionalDiscount = 0,
    bool Active = true);
