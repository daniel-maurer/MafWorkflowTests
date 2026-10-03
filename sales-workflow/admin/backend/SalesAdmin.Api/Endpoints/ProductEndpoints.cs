using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesAdmin.Api.Data;
using SalesAdmin.Api.Entities;
using SalesAdmin.Api.Services;

namespace SalesAdmin.Api.Endpoints;

public static class ProductEndpoints
{
    public static RouteGroupBuilder MapProductEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            [FromServices] SalesAdminDbContext db,
            [FromQuery] string? search,
            [FromQuery] Guid? categoryId,
            [FromQuery] string? brand,
            [FromQuery] bool? active,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20) =>
        {
            var query = db.Products.Include(p => p.Category).AsQueryable();

            if (active.HasValue)
            {
                query = query.Where(p => p.Active == active.Value);
            }

            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(brand))
            {
                query = query.Where(p => p.Brand == brand);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(p =>
                    p.Name.Contains(term) ||
                    p.Sku.Contains(term) ||
                    p.Description.Contains(term) ||
                    (p.Brand != null && p.Brand.Contains(term)));
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.Id,
                    p.Sku,
                    p.Name,
                    p.Description,
                    p.Price,
                    p.Currency,
                    p.InStock,
                    p.StockQty,
                    p.Color,
                    p.Size,
                    p.Brand,
                    p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    p.Tags,
                    p.CompatibleSkus,
                    p.ImageUrl,
                    p.Active,
                    HasEmbedding = p.Embedding != null,
                    p.CreatedAt,
                    p.UpdatedAt
                })
                .ToListAsync();

            return Results.Ok(new
            {
                Total = totalCount,
                Page = page,
                PageSize = pageSize,
                Items = items
            });
        });

        group.MapGet("/{id:guid}", async (Guid id, [FromServices] SalesAdminDbContext db) =>
        {
            var product = await db.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product is null) return Results.NotFound();

            return Results.Ok(ToResponse(product));
        });

        group.MapGet("/sku/{sku}", async (string sku, [FromServices] SalesAdminDbContext db) =>
        {
            var product = await db.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase));

            if (product is null) return Results.NotFound();

            return Results.Ok(ToResponse(product));
        });

        group.MapPost("/", async (
            [FromBody] CreateProductDto dto,
            [FromServices] SalesAdminDbContext db,
            [FromServices] EmbeddingService embeddingService,
            CancellationToken ct) =>
        {
            if (await db.Products.AnyAsync(p => p.Sku == dto.Sku, ct))
            {
                return Results.Conflict(new { error = $"SKU '{dto.Sku}' já está cadastrado." });
            }

            var product = new Product
            {
                Sku = dto.Sku.Trim(),
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim() ?? string.Empty,
                Price = dto.Price,
                Currency = dto.Currency ?? "BRL",
                InStock = dto.InStock,
                StockQty = dto.StockQty,
                Color = dto.Color,
                Size = dto.Size,
                Brand = dto.Brand,
                CategoryId = dto.CategoryId,
                Tags = dto.Tags ?? [],
                CompatibleSkus = dto.CompatibleSkus ?? [],
                ImageUrl = dto.ImageUrl,
                Active = dto.Active
            };

            if (product.CategoryId.HasValue)
            {
                product.Category = await db.Categories.FindAsync([product.CategoryId.Value], ct);
            }

            // Sync-on-write: gera embedding automaticamente
            var textToEmbed = EmbeddingService.BuildProductEmbeddingText(product);
            product.Embedding = await embeddingService.GenerateEmbeddingAsync(textToEmbed, ct);

            db.Products.Add(product);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/products/{product.Id}", ToResponse(product));
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateProductDto dto,
            [FromServices] SalesAdminDbContext db,
            [FromServices] EmbeddingService embeddingService,
            CancellationToken ct) =>
        {
            var product = await db.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            if (product is null) return Results.NotFound();

            if (await db.Products.AnyAsync(p => p.Sku == dto.Sku && p.Id != id, ct))
            {
                return Results.Conflict(new { error = $"SKU '{dto.Sku}' já está sendo utilizado por outro produto." });
            }

            product.Sku = dto.Sku.Trim();
            product.Name = dto.Name.Trim();
            product.Description = dto.Description?.Trim() ?? string.Empty;
            product.Price = dto.Price;
            product.Currency = dto.Currency ?? "BRL";
            product.InStock = dto.InStock;
            product.StockQty = dto.StockQty;
            product.Color = dto.Color;
            product.Size = dto.Size;
            product.Brand = dto.Brand;
            product.CategoryId = dto.CategoryId;
            product.Tags = dto.Tags ?? [];
            product.CompatibleSkus = dto.CompatibleSkus ?? [];
            product.ImageUrl = dto.ImageUrl;
            product.Active = dto.Active;
            product.UpdatedAt = DateTimeOffset.UtcNow;

            if (product.CategoryId.HasValue)
            {
                product.Category = await db.Categories.FindAsync([product.CategoryId.Value], ct);
            }
            else
            {
                product.Category = null;
            }

            // Sync-on-write: atualiza embedding
            var textToEmbed = EmbeddingService.BuildProductEmbeddingText(product);
            product.Embedding = await embeddingService.GenerateEmbeddingAsync(textToEmbed, ct);

            await db.SaveChangesAsync(ct);
            return Results.Ok(ToResponse(product));
        });

        group.MapDelete("/{id:guid}", async (Guid id, [FromServices] SalesAdminDbContext db) =>
        {
            var product = await db.Products.FindAsync(id);
            if (product is null) return Results.NotFound();

            // Soft delete
            product.Active = false;
            product.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        group.MapPost("/reindex-embeddings", async (
            [FromServices] SalesAdminDbContext db,
            [FromServices] EmbeddingService embeddingService,
            CancellationToken ct) =>
        {
            var products = await db.Products.Include(p => p.Category).ToListAsync(ct);
            int count = 0;

            foreach (var p in products)
            {
                var text = EmbeddingService.BuildProductEmbeddingText(p);
                p.Embedding = await embeddingService.GenerateEmbeddingAsync(text, ct);
                count++;
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { message = $"{count} produtos reindexados com sucesso." });
        });

        return group;
    }

    private static ProductResponseDto ToResponse(Product p) => new(
        p.Id,
        p.Sku,
        p.Name,
        p.Description,
        p.Price,
        p.Currency,
        p.InStock,
        p.StockQty,
        p.Color,
        p.Size,
        p.Brand,
        p.CategoryId,
        p.Category != null ? p.Category.Name : null,
        p.Tags,
        p.CompatibleSkus,
        p.ImageUrl,
        p.Active,
        p.CreatedAt,
        p.UpdatedAt
    );
}

public sealed record ProductResponseDto(
    Guid Id,
    string Sku,
    string Name,
    string Description,
    decimal Price,
    string Currency,
    bool InStock,
    int StockQty,
    string? Color,
    string? Size,
    string? Brand,
    Guid? CategoryId,
    string? CategoryName,
    List<string> Tags,
    List<string> CompatibleSkus,
    string? ImageUrl,
    bool Active,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateProductDto(
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    string? Currency,
    bool InStock,
    int StockQty,
    string? Color,
    string? Size,
    string? Brand,
    Guid? CategoryId,
    List<string>? Tags,
    List<string>? CompatibleSkus,
    string? ImageUrl,
    bool Active = true);

public sealed record UpdateProductDto(
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    string? Currency,
    bool InStock,
    int StockQty,
    string? Color,
    string? Size,
    string? Brand,
    Guid? CategoryId,
    List<string>? Tags,
    List<string>? CompatibleSkus,
    string? ImageUrl,
    bool Active = true);
