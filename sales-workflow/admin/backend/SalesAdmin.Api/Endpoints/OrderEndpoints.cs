using Microsoft.EntityFrameworkCore;
using SalesAdmin.Api.Data;
using SalesAdmin.Api.Entities;

namespace SalesAdmin.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Orders");

        group.MapGet("/", async (SalesAdminDbContext db) =>
        {
            var orders = await db.Orders
                                 .Include(o => o.Items)
                                 .OrderByDescending(o => o.CreatedAt)
                                 .ToListAsync();
            return Results.Ok(orders);
        });

        group.MapGet("/{id}", async (SalesAdminDbContext db, Guid id) =>
        {
            var order = await db.Orders
                                .Include(o => o.Items)
                                .FirstOrDefaultAsync(o => o.Id == id);
            return order is not null ? Results.Ok(order) : Results.NotFound();
        });

        group.MapPost("/", async (SalesAdminDbContext db, Order order) =>
        {
            foreach (var item in order.Items)
            {
                if (item.ProductId == Guid.Empty && !string.IsNullOrWhiteSpace(item.Sku))
                {
                    var product = await db.Products.FirstOrDefaultAsync(p => p.Sku == item.Sku);
                    if (product != null)
                    {
                        item.ProductId = product.Id;
                    }
                }

                if (item.ProductId == Guid.Empty && !string.IsNullOrWhiteSpace(item.Name))
                {
                    var product = await db.Products.FirstOrDefaultAsync(p => p.Name == item.Name);
                    if (product != null)
                    {
                        item.ProductId = product.Id;
                    }
                }

                if (item.ProductId == Guid.Empty)
                {
                    var fallback = await db.Products.FirstOrDefaultAsync();
                    if (fallback != null)
                    {
                        item.ProductId = fallback.Id;
                    }
                }
            }

            db.Orders.Add(order);
            await db.SaveChangesAsync();
            return Results.Created($"/api/orders/{order.Id}", order);
        });

        group.MapDelete("/{id}", async (SalesAdminDbContext db, Guid id) =>
        {
            var order = await db.Orders.FindAsync(id);
            if (order is null) return Results.NotFound();

            db.Orders.Remove(order);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
