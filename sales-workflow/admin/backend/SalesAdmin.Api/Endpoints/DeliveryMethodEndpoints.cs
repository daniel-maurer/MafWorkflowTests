using Microsoft.EntityFrameworkCore;
using SalesAdmin.Api.Data;
using SalesAdmin.Api.Entities;

namespace SalesAdmin.Api.Endpoints;

public static class DeliveryMethodEndpoints
{
    public static void MapDeliveryMethodEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/delivery-methods").WithTags("Delivery Methods");

        group.MapGet("/", async (SalesAdminDbContext db) =>
        {
            var methods = await db.DeliveryMethods.ToListAsync();
            return Results.Ok(methods);
        });

        group.MapPost("/", async (SalesAdminDbContext db, DeliveryMethod method) =>
        {
            db.DeliveryMethods.Add(method);
            await db.SaveChangesAsync();
            return Results.Created($"/api/delivery-methods/{method.Id}", method);
        });

        group.MapPut("/{id}", async (SalesAdminDbContext db, Guid id, DeliveryMethod updated) =>
        {
            var method = await db.DeliveryMethods.FindAsync(id);
            if (method is null) return Results.NotFound();

            method.Name = updated.Name;
            method.Type = updated.Type;
            method.Description = updated.Description;
            method.Price = updated.Price;
            method.Active = updated.Active;
            method.UpdatedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapDelete("/{id}", async (SalesAdminDbContext db, Guid id) =>
        {
            var method = await db.DeliveryMethods.FindAsync(id);
            if (method is null) return Results.NotFound();

            db.DeliveryMethods.Remove(method);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
