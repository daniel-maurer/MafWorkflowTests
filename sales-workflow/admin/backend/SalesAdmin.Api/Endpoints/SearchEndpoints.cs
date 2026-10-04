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
        // ── RAG: Busca Híbrida de Produtos (Semântica + Lexical) ──
        group.MapGet("/products", async (
            [FromQuery] string q,
            [FromQuery] int? top,
            [FromServices] SalesAdminDbContext db,
            [FromServices] EmbeddingService embeddingService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest(new { error = "Query 'q' é obrigatória." });
            var limit = top ?? 5;
            if (limit <= 0) limit = 5;

            var rawQuery = q.Trim();
            var queryVector = await embeddingService.GenerateEmbeddingAsync(rawQuery, ct);
            var queryTerms = rawQuery.ToLowerInvariant()
                .Split(new[] { ' ', ',', '-', '/', ';', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => t.Length > 1)
                .ToArray();

            var allProducts = await db.Products
                .Include(p => p.Category)
                .Where(p => p.Active)
                .ToListAsync(ct);

            var scored = allProducts.Select(p =>
            {
                double lexicalScore = 0;
                var pName = (p.Name ?? "").ToLowerInvariant();
                var pDesc = (p.Description ?? "").ToLowerInvariant();
                var pBrand = (p.Brand ?? "").ToLowerInvariant();
                var pCat = (p.Category?.Name ?? "").ToLowerInvariant();
                var pTags = (p.Tags != null ? string.Join(" ", p.Tags) : "").ToLowerInvariant();
                var pSku = (p.Sku ?? "").ToLowerInvariant();

                int matchedTermsCount = 0;
                foreach (var term in queryTerms)
                {
                    bool termMatched = false;
                    if (pSku.Contains(term)) { lexicalScore += 25.0; termMatched = true; }
                    if (pName.Contains(term)) { lexicalScore += 25.0; termMatched = true; }
                    if (pTags.Contains(term)) { lexicalScore += 18.0; termMatched = true; }
                    if (pCat.Contains(term)) { lexicalScore += 12.0; termMatched = true; }
                    if (pBrand.Contains(term)) { lexicalScore += 10.0; termMatched = true; }
                    if (pDesc.Contains(term)) { lexicalScore += 6.0; termMatched = true; }

                    // Sinonímias frequentes
                    if ((term == "camisa" && (pName.Contains("camiseta") || pDesc.Contains("camiseta"))) ||
                        (term == "camiseta" && (pName.Contains("camisa") || pDesc.Contains("camisa"))))
                    {
                        lexicalScore += 18.0;
                        termMatched = true;
                    }
                    if ((term == "tenis" || term == "tênis") && (pCat.Contains("calçado") || pTags.Contains("sneaker") || pDesc.Contains("esportivo")))
                    {
                        lexicalScore += 15.0;
                        termMatched = true;
                    }
                    if ((term == "fone" || term == "headphone" || term == "headset") && (pCat.Contains("áudio") || pDesc.Contains("bluetooth")))
                    {
                        lexicalScore += 15.0;
                        termMatched = true;
                    }
                    if ((term == "notebook" || term == "laptop") && (pCat.Contains("informática") || pDesc.Contains("intel") || pDesc.Contains("ssd")))
                    {
                        lexicalScore += 15.0;
                        termMatched = true;
                    }
                    if (term == "mouse" && (pCat.Contains("periférico") || pDesc.Contains("óptico")))
                    {
                        lexicalScore += 15.0;
                        termMatched = true;
                    }

                    if (termMatched) matchedTermsCount++;
                }

                // Super bônus multiplicador quando o produto casa múltiplos termos da consulta (ex: 'esportiva' + 'azul')
                if (queryTerms.Length > 1 && matchedTermsCount > 1)
                {
                    lexicalScore *= (1.0 + (matchedTermsCount * 1.5));
                }

                if (p.InStock && p.StockQty > 0)
                {
                    lexicalScore += 5.0;
                }

                // Vector similarity via dot product
                double vectorSim = 0;
                if (p.Embedding != null)
                {
                    var v1 = p.Embedding.ToArray();
                    var v2 = queryVector.ToArray();
                    double dot = 0;
                    for (int i = 0; i < Math.Min(v1.Length, v2.Length); i++)
                    {
                        dot += v1[i] * v2[i];
                    }
                    vectorSim = dot;
                }

                double combinedScore = (lexicalScore * 2.0) + (vectorSim > 0 ? vectorSim * 10.0 : 0);

                return new
                {
                    Product = p,
                    CombinedScore = combinedScore,
                    LexicalScore = lexicalScore,
                    VectorScore = vectorSim
                };
            }).ToList();

            var hasLexicalMatches = scored.Any(x => x.LexicalScore > 0);
            var filteredResults = hasLexicalMatches
                ? scored.Where(x => x.LexicalScore > 0).OrderByDescending(x => x.CombinedScore).Take(limit)
                : scored.OrderByDescending(x => x.CombinedScore).Take(limit);

            var items = filteredResults.Select(x => new
            {
                id = x.Product.Id,
                sku = x.Product.Sku,
                name = x.Product.Name,
                description = x.Product.Description,
                price = x.Product.Price,
                currency = x.Product.Currency,
                in_stock = x.Product.InStock,
                stock_qty = x.Product.StockQty,
                color = x.Product.Color,
                size = x.Product.Size,
                brand = x.Product.Brand,
                category = x.Product.Category != null ? x.Product.Category.Name : null,
                tags = x.Product.Tags,
                compatible_skus = x.Product.CompatibleSkus,
                image_url = x.Product.ImageUrl,
                score = Math.Round(x.CombinedScore, 4)
            }).ToList();

            return Results.Ok(items);
        });

        // ── RAG: Busca Semântica de Descontos / Cupons ──
        group.MapGet("/discounts", async (
            [FromQuery] string q,
            [FromQuery] int? top,
            [FromServices] SalesAdminDbContext db,
            [FromServices] EmbeddingService embeddingService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest(new { error = "Query 'q' é obrigatória." });
            var limit = top ?? 5;
            if (limit <= 0) limit = 5;

            var queryVector = await embeddingService.GenerateEmbeddingAsync(q.Trim(), ct);
            var now = DateTimeOffset.UtcNow;

            try
            {
                var items = await db.Discounts
                    .Include(d => d.TargetProducts)
                    .Where(d => d.Active && d.Embedding != null && d.ValidFrom <= now && (d.ValidUntil == null || d.ValidUntil >= now))
                    .OrderBy(d => d.Embedding!.CosineDistance(queryVector))
                    .Take(limit)
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
                return Results.Ok(await db.Discounts.Where(d => d.Active).Take(limit).ToListAsync(ct));
            }
        });

        // ── RAG: Busca Semântica de Clientes ──
        group.MapGet("/customers", async (
            [FromQuery] string q,
            [FromQuery] int? top,
            [FromServices] SalesAdminDbContext db,
            [FromServices] EmbeddingService embeddingService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest(new { error = "Query 'q' é obrigatória." });
            var limit = top ?? 5;
            if (limit <= 0) limit = 5;

            var queryVector = await embeddingService.GenerateEmbeddingAsync(q.Trim(), ct);

            try
            {
                var items = await db.Customers
                    .Include(c => c.Addresses)
                    .Where(c => c.Active && c.Embedding != null)
                    .OrderBy(c => c.Embedding!.CosineDistance(queryVector))
                    .Take(limit)
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
                return Results.Ok(await db.Customers.Where(c => c.Active).Take(limit).ToListAsync(ct));
            }
        });

        return group;
    }
}
