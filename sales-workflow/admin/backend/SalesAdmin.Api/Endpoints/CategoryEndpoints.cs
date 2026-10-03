using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesAdmin.Api.Data;
using SalesAdmin.Api.Entities;

namespace SalesAdmin.Api.Endpoints;

public static class CategoryEndpoints
{
    public static RouteGroupBuilder MapCategoryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async ([FromServices] SalesAdminDbContext db, [FromQuery] bool? active) =>
        {
            var query = db.Categories.AsQueryable();
            if (active.HasValue) query = query.Where(c => c.Active == active.Value);

            var items = await query.OrderBy(c => c.Name).ToListAsync();
            return Results.Ok(items);
        });

        group.MapGet("/{id:guid}", async (Guid id, [FromServices] SalesAdminDbContext db) =>
        {
            var category = await db.Categories.FindAsync(id);
            return category is not null ? Results.Ok(category) : Results.NotFound();
        });

        group.MapPost("/", async ([FromBody] CreateCategoryDto dto, [FromServices] SalesAdminDbContext db) =>
        {
            var slug = string.IsNullOrWhiteSpace(dto.Slug)
                ? dto.Name.Trim().ToLowerInvariant().Replace(" ", "-")
                : dto.Slug.Trim();

            if (await db.Categories.AnyAsync(c => c.Slug == slug))
            {
                return Results.Conflict(new { error = $"Slug '{slug}' já existe." });
            }

            var category = new Category
            {
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim(),
                Slug = slug,
                Active = dto.Active
            };

            db.Categories.Add(category);
            await db.SaveChangesAsync();
            return Results.Created($"/api/categories/{category.Id}", category);
        });

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdateCategoryDto dto, [FromServices] SalesAdminDbContext db) =>
        {
            var category = await db.Categories.FindAsync(id);
            if (category is null) return Results.NotFound();

            category.Name = dto.Name.Trim();
            category.Description = dto.Description?.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Slug)) category.Slug = dto.Slug.Trim();
            category.Active = dto.Active;
            category.UpdatedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok(category);
        });

        group.MapDelete("/{id:guid}", async (Guid id, [FromServices] SalesAdminDbContext db) =>
        {
            var category = await db.Categories.FindAsync(id);
            if (category is null) return Results.NotFound();

            category.Active = false;
            category.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return group;
    }
}

public sealed record CreateCategoryDto(string Name, string? Description, string? Slug, bool Active = true);
public sealed record UpdateCategoryDto(string Name, string? Description, string? Slug, bool Active = true);
