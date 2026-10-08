using Microsoft.EntityFrameworkCore;
using SalesAdmin.Api.Data;
using SalesAdmin.Api.Entities;

namespace SalesAdmin.Api.Endpoints;

public static class StoreInfoEndpoints
{
    public static void MapStoreInfoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/store-info").WithTags("Store Info");

        group.MapGet("/", async (SalesAdminDbContext db) =>
        {
            var info = await db.StoreInfo.FirstOrDefaultAsync();
            return Results.Ok(info);
        });

        group.MapPost("/", async (SalesAdminDbContext db, StoreInfo info) =>
        {
            db.StoreInfo.Add(info);
            await db.SaveChangesAsync();
            return Results.Created($"/api/store-info/{info.Id}", info);
        });

        group.MapPut("/{id}", async (SalesAdminDbContext db, Guid id, StoreInfo updated) =>
        {
            var info = await db.StoreInfo.FindAsync(id);
            if (info is null) return Results.NotFound();

            info.Name = updated.Name;
            info.Address = updated.Address;
            info.Phone = updated.Phone;
            info.Email = updated.Email;
            info.UpdatedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
