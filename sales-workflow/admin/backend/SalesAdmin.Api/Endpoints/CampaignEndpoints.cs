using Microsoft.EntityFrameworkCore;
using SalesAdmin.Api.Data;
using SalesAdmin.Api.Entities;

namespace SalesAdmin.Api.Endpoints;

public static class CampaignEndpoints
{
    public static void MapCampaignEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/campaigns").WithTags("Campaigns");

        group.MapGet("/", async (SalesAdminDbContext db, bool? active) =>
        {
            var query = db.Campaigns.AsQueryable();
            
            if (active.HasValue)
            {
                query = query.Where(c => c.IsActive == active.Value);
            }

            var campaigns = await query
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();
                
            return Results.Ok(campaigns);
        });

        group.MapGet("/active-now", async (SalesAdminDbContext db) =>
        {
            var now = DateTimeOffset.UtcNow;
            var campaigns = await db.Campaigns
                .Where(c => c.IsActive && c.StartDate <= now && c.EndDate >= now)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();
            return Results.Ok(campaigns);
        });

        group.MapGet("/{id:guid}", async (SalesAdminDbContext db, Guid id) =>
        {
            var campaign = await db.Campaigns.FindAsync(id);
            return campaign is not null ? Results.Ok(campaign) : Results.NotFound();
        });

        group.MapPost("/", async (SalesAdminDbContext db, Campaign campaign) =>
        {
            campaign.CreatedAt = DateTimeOffset.UtcNow;
            db.Campaigns.Add(campaign);
            await db.SaveChangesAsync();
            return Results.Created($"/api/campaigns/{campaign.Id}", campaign);
        });

        group.MapPut("/{id:guid}", async (SalesAdminDbContext db, Guid id, Campaign updated) =>
        {
            var campaign = await db.Campaigns.FindAsync(id);
            if (campaign is null) return Results.NotFound();

            campaign.Name = updated.Name;
            campaign.Description = updated.Description;
            campaign.StartDate = updated.StartDate;
            campaign.EndDate = updated.EndDate;
            campaign.IsActive = updated.IsActive;
            campaign.FreeShipping = updated.FreeShipping;
            campaign.GlobalDiscountPercent = updated.GlobalDiscountPercent;
            campaign.Discount1Item = updated.Discount1Item;
            campaign.Discount2Items = updated.Discount2Items;
            campaign.Discount3PlusItems = updated.Discount3PlusItems;
            campaign.CustomRulesJson = updated.CustomRulesJson;

            await db.SaveChangesAsync();
            return Results.Ok(campaign);
        });

        group.MapDelete("/{id:guid}", async (SalesAdminDbContext db, Guid id) =>
        {
            var campaign = await db.Campaigns.FindAsync(id);
            if (campaign is null) return Results.NotFound();

            db.Campaigns.Remove(campaign);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
