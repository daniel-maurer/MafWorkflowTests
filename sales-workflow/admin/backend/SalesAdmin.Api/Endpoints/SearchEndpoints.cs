using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using SalesAdmin.Api.Data;
using SalesAdmin.Api.Services;

namespace SalesAdmin.Api.Endpoints;

public static class SearchEndpoints
{
    public static RouteGroupBuilder MapSearchEndpoints(this RouteGroupBuilder group)
    {
        // ── RAG: Busca Semântica de Produtos ──
        group.MapGet("/products", async (
            [FromQuery] string q,
            [FromQuery] int top,
            [FromServices] SalesAdminDbContext db,
            [FromServices] EmbeddingService embeddingService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest(new { error = "Query 'q' é obrigatória." });
            if (top <= 0) top = 5;

            var queryVector = await embeddingService.GenerateEmbeddingAsync(q.Trim(), ct);

            try
            {
                // Busca vetorial via pgvector (Cosine Distance: <=>)
                var items = await db.Products
                    .Include(p => p.Category)
                    .Where(p => p.Active && p.Embedding != null)
                    .OrderBy(p => p.Embedding!.CosineDistance(queryVector))
                    .Take(top)
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
                        Category = p.Category != null ? p.Category.Name : null,
                        p.Tags,
                        p.CompatibleSkus,
                        p.ImageUrl,
                        Score = 1 - p.Embedding!.CosineDistance(queryVector)
                    })
                    .ToListAsync(ct);

                return Results.Ok(items);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SearchEndpoints] Erro na busca vetorial: {ex.Message}. Fallback para busca textual.");
                // Fallback textual caso o banco não tenha a extensão vector instalada
                var terms = q.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var fallback = await db.Products
                    .Include(p => p.Category)
                    .Where(p => p.Active)
                    .Take(top)
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
                        Category = p.Category != null ? p.Category.Name : null,
                        p.Tags,
                        p.CompatibleSkus,
                        p.ImageUrl,
                        Score = 0.8
                    })
                    .ToListAsync(ct);

                return Results.Ok(fallback);
            }
        });

        // ── RAG: Busca Semântica de Descontos / Cupons ──
        group.MapGet("/discounts", async (
            [FromQuery] string q,
            [FromQuery] int top,
            [FromServices] SalesAdminDbContext db,
            [FromServices] EmbeddingService embeddingService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest(new { error = "Query 'q' é obrigatória." });
            if (top <= 0) top = 5;

            var queryVector = await embeddingService.GenerateEmbeddingAsync(q.Trim(), ct);
            var now = DateTimeOffset.UtcNow;

            try
            {
                var items = await db.Discounts
                    .Include(d => d.TargetProducts)
                    .Where(d => d.Active && d.Embedding != null && d.ValidFrom <= now && (d.ValidUntil == null || d.ValidUntil >= now))
                    .OrderBy(d => d.Embedding!.CosineDistance(queryVector))
                    .Take(top)
                    .Select(d => new
                    {
                        d.Id,
                        d.Code,
                        d.Name,
                        d.Description,
                        d.DiscountType,
                        d.DiscountValue,
                        d.MinOrderValue,
                        d.MaxDiscountAmount,
                        d.PaymentMethods,
                        d.Stackable,
                        TargetProductSkus = d.TargetProducts.Select(p => p.Sku).ToList(),
                        Score = 1 - d.Embedding!.CosineDistance(queryVector)
                    })
                    .ToListAsync(ct);

                return Results.Ok(items);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SearchEndpoints] Erro na busca vetorial de descontos: {ex.Message}.");
                return Results.Ok(await db.Discounts.Where(d => d.Active).Take(top).ToListAsync(ct));
            }
        });

        // ── RAG: Busca Semântica de Clientes ──
        group.MapGet("/customers", async (
            [FromQuery] string q,
            [FromQuery] int top,
            [FromServices] SalesAdminDbContext db,
            [FromServices] EmbeddingService embeddingService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest(new { error = "Query 'q' é obrigatória." });
            if (top <= 0) top = 5;

            var queryVector = await embeddingService.GenerateEmbeddingAsync(q.Trim(), ct);

            try
            {
                var items = await db.Customers
                    .Include(c => c.Addresses)
                    .Where(c => c.Active && c.Embedding != null)
                    .OrderBy(c => c.Embedding!.CosineDistance(queryVector))
                    .Take(top)
                    .Select(c => new
                    {
                        c.Id,
                        c.Name,
                        c.Email,
                        c.Phone,
                        c.DocumentNumber,
                        c.DocumentType,
                        c.CustomerType,
                        c.Notes,
                        Addresses = c.Addresses,
                        Score = 1 - c.Embedding!.CosineDistance(queryVector)
                    })
                    .ToListAsync(ct);

                return Results.Ok(items);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SearchEndpoints] Erro na busca vetorial de clientes: {ex.Message}.");
                return Results.Ok(await db.Customers.Where(c => c.Active).Take(top).ToListAsync(ct));
            }
        });

        return group;
    }
}
