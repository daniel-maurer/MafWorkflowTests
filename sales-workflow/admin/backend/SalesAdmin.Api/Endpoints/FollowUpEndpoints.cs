using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesAdmin.Api.Data;
using SalesAdmin.Api.Entities;

namespace SalesAdmin.Api.Endpoints;

public static class FollowUpEndpoints
{
    public static RouteGroupBuilder MapFollowUpEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/follow-ups
        group.MapGet("/", async (
            SalesAdminDbContext db,
            [FromQuery] Guid? customerId,
            [FromQuery] string? module,
            [FromQuery] string? status,
            [FromQuery] DateTimeOffset? dateFrom,
            [FromQuery] DateTimeOffset? dateTo,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50) =>
        {
            var query = db.FollowUps.AsNoTracking().Include(f => f.Customer).AsQueryable();

            if (customerId.HasValue)
            {
                query = query.Where(f => f.CustomerId == customerId.Value);
            }

            if (!string.IsNullOrWhiteSpace(module))
            {
                var mod = module.Trim().ToLowerInvariant();
                query = query.Where(f => f.Module.ToLower() == mod);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var st = status.Trim().ToLowerInvariant();
                query = query.Where(f => f.Status.ToLower() == st);
            }

            if (dateFrom.HasValue)
            {
                query = query.Where(f => f.ScheduledFor >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                query = query.Where(f => f.ScheduledFor <= dateTo.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(f =>
                    f.ReferenceTitle != null && f.ReferenceTitle.ToLower().Contains(term) ||
                    f.MessageText.ToLower().Contains(term) ||
                    f.FollowUpType.ToLower().Contains(term) ||
                    (f.Customer != null && (f.Customer.Name.ToLower().Contains(term) || f.Customer.Email.ToLower().Contains(term))));
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderBy(f => f.ScheduledFor)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(f => new FollowUpResponse(
                    f.Id,
                    f.CustomerId,
                    f.Customer != null ? f.Customer.Name : "Cliente Desconhecido",
                    f.Customer != null ? f.Customer.Email : "",
                    f.Customer != null ? f.Customer.Phone : null,
                    f.Module,
                    f.FollowUpType,
                    f.ReferenceId,
                    f.ReferenceTitle,
                    f.ScheduledFor,
                    f.Channel,
                    f.MessageText,
                    f.Status,
                    f.SentAt,
                    f.Notes,
                    f.CustomDataJson,
                    f.CreatedAt
                ))
                .ToListAsync();

            return Results.Ok(new { items, total, page, pageSize });
        }).RequireAuthorization();

        // GET /api/follow-ups/{id}
        group.MapGet("/{id:guid}", async (SalesAdminDbContext db, Guid id) =>
        {
            var f = await db.FollowUps
                .AsNoTracking()
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (f is null) return Results.NotFound(new { message = "Follow-up não encontrado" });

            return Results.Ok(new FollowUpResponse(
                f.Id,
                f.CustomerId,
                f.Customer != null ? f.Customer.Name : "Cliente Desconhecido",
                f.Customer != null ? f.Customer.Email : "",
                f.Customer != null ? f.Customer.Phone : null,
                f.Module,
                f.FollowUpType,
                f.ReferenceId,
                f.ReferenceTitle,
                f.ScheduledFor,
                f.Channel,
                f.MessageText,
                f.Status,
                f.SentAt,
                f.Notes,
                f.CustomDataJson,
                f.CreatedAt
            ));
        }).RequireAuthorization();

        // POST /api/follow-ups
        group.MapPost("/", async (SalesAdminDbContext db, CreateFollowUpRequest request) =>
        {
            // Valida ou busca cliente
            Customer? customer = null;
            if (request.CustomerId.HasValue)
            {
                customer = await db.Customers.FindAsync(request.CustomerId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(request.CustomerIdentifier))
            {
                var ident = request.CustomerIdentifier.Trim().ToLowerInvariant();
                customer = await db.Customers.FirstOrDefaultAsync(c =>
                    c.Email.ToLower() == ident ||
                    (c.Phone != null && c.Phone.Contains(ident)) ||
                    c.Name.ToLower().Contains(ident));
            }

            if (customer is null)
            {
                // Se nenhum cliente foi encontrado, cria um lead básico para associar o follow-up
                var fallbackEmail = $"lead_{Guid.NewGuid().ToString("N")[..8]}@lead.local";
                customer = new Customer
                {
                    Name = !string.IsNullOrWhiteSpace(request.CustomerIdentifier) ? request.CustomerIdentifier.Trim() : "Cliente Atendimento",
                    Email = fallbackEmail,
                    Active = true
                };
                db.Customers.Add(customer);
                await db.SaveChangesAsync();
            }

            var record = new FollowUpRecord
            {
                CustomerId = customer.Id,
                Module = string.IsNullOrWhiteSpace(request.Module) ? "sales" : request.Module.Trim().ToLowerInvariant(),
                FollowUpType = string.IsNullOrWhiteSpace(request.FollowUpType) ? "reminder" : request.FollowUpType.Trim(),
                ReferenceId = request.ReferenceId?.Trim(),
                ReferenceTitle = request.ReferenceTitle?.Trim(),
                ScheduledFor = request.ScheduledFor == default ? DateTimeOffset.UtcNow.AddDays(1) : request.ScheduledFor,
                Channel = string.IsNullOrWhiteSpace(request.Channel) ? "WhatsApp" : request.Channel.Trim(),
                MessageText = request.MessageText ?? string.Empty,
                Status = "Pending",
                Notes = request.Notes,
                CustomDataJson = request.CustomDataJson
            };

            db.FollowUps.Add(record);
            await db.SaveChangesAsync();

            return Results.Created($"/api/follow-ups/{record.Id}", new FollowUpResponse(
                record.Id,
                customer.Id,
                customer.Name,
                customer.Email,
                customer.Phone,
                record.Module,
                record.FollowUpType,
                record.ReferenceId,
                record.ReferenceTitle,
                record.ScheduledFor,
                record.Channel,
                record.MessageText,
                record.Status,
                record.SentAt,
                record.Notes,
                record.CustomDataJson,
                record.CreatedAt
            ));
        }).RequireAuthorization();

        // PATCH /api/follow-ups/{id}/status
        group.MapPatch("/{id:guid}/status", async (SalesAdminDbContext db, Guid id, UpdateFollowUpStatusRequest request) =>
        {
            var record = await db.FollowUps.FindAsync(id);
            if (record is null) return Results.NotFound(new { message = "Follow-up não encontrado" });

            record.Status = request.Status.Trim();
            if (request.Status.Equals("Sent", StringComparison.OrdinalIgnoreCase) || request.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            {
                record.SentAt = request.SentAt ?? DateTimeOffset.UtcNow;
            }
            if (request.Notes != null)
            {
                record.Notes = request.Notes;
            }
            record.UpdatedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Status atualizado com sucesso", status = record.Status, sentAt = record.SentAt });
        }).RequireAuthorization();

        // PUT /api/follow-ups/{id}
        group.MapPut("/{id:guid}", async (SalesAdminDbContext db, Guid id, UpdateFollowUpRequest request) =>
        {
            var record = await db.FollowUps.FindAsync(id);
            if (record is null) return Results.NotFound(new { message = "Follow-up não encontrado" });

            if (request.ScheduledFor.HasValue) record.ScheduledFor = request.ScheduledFor.Value;
            if (request.Channel != null) record.Channel = request.Channel;
            if (request.MessageText != null) record.MessageText = request.MessageText;
            if (request.ReferenceTitle != null) record.ReferenceTitle = request.ReferenceTitle;
            if (request.Notes != null) record.Notes = request.Notes;
            if (request.Status != null) record.Status = request.Status;
            record.UpdatedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok(record);
        }).RequireAuthorization();

        // DELETE /api/follow-ups/{id}
        group.MapDelete("/{id:guid}", async (SalesAdminDbContext db, Guid id) =>
        {
            var record = await db.FollowUps.FindAsync(id);
            if (record is null) return Results.NotFound(new { message = "Follow-up não encontrado" });

            db.FollowUps.Remove(record);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization();

        return group;
    }
}

public sealed record FollowUpResponse(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    string Module,
    string FollowUpType,
    string? ReferenceId,
    string? ReferenceTitle,
    DateTimeOffset ScheduledFor,
    string Channel,
    string MessageText,
    string Status,
    DateTimeOffset? SentAt,
    string? Notes,
    string? CustomDataJson,
    DateTimeOffset CreatedAt
);

public sealed record CreateFollowUpRequest(
    Guid? CustomerId,
    string? CustomerIdentifier,
    string? Module,
    string? FollowUpType,
    string? ReferenceId,
    string? ReferenceTitle,
    DateTimeOffset ScheduledFor,
    string? Channel,
    string MessageText,
    string? Notes,
    string? CustomDataJson
);

public sealed record UpdateFollowUpStatusRequest(
    string Status,
    string? Notes,
    DateTimeOffset? SentAt
);

public sealed record UpdateFollowUpRequest(
    DateTimeOffset? ScheduledFor,
    string? Channel,
    string? MessageText,
    string? ReferenceTitle,
    string? Notes,
    string? Status
);
