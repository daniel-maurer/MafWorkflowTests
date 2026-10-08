using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesAdmin.Api.Data;
using SalesAdmin.Api.Entities;
using SalesAdmin.Api.Services;

namespace SalesAdmin.Api.Endpoints;

public static class CustomerEndpoints
{
    public static RouteGroupBuilder MapCustomerEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            [FromServices] SalesAdminDbContext db,
            [FromQuery] string? search,
            [FromQuery] bool? active,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20) =>
        {
            var query = db.Customers.Include(c => c.Addresses).AsQueryable();

            if (active.HasValue) query = query.Where(c => c.Active == active.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(c =>
                    c.Name.Contains(term) ||
                    c.Email.Contains(term) ||
                    (c.Phone != null && c.Phone.Contains(term)) ||
                    (c.DocumentNumber != null && c.DocumentNumber.Contains(term)));
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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
            var customer = await db.Customers.Include(c => c.Addresses).FirstOrDefaultAsync(c => c.Id == id);
            return customer is not null ? Results.Ok(customer) : Results.NotFound();
        });

        group.MapGet("/find", async ([FromQuery] string identifier, [FromServices] SalesAdminDbContext db, CancellationToken ct) =>
        {
            var term = identifier.Trim();
            var isGuid = Guid.TryParse(term, out var guid);

            var customer = await db.Customers
                .Include(c => c.Addresses)
                .FirstOrDefaultAsync(c =>
                    (isGuid && c.Id == guid) ||
                    c.Email.ToLower() == term.ToLower() ||
                    (c.Phone != null && c.Phone.Contains(term)) ||
                    (c.DocumentNumber != null && c.DocumentNumber.Contains(term)) ||
                    c.Name.ToLower().Contains(term.ToLower()), ct);

            return customer is not null ? Results.Ok(customer) : Results.NotFound();
        });

        group.MapPost("/", async (
            [FromBody] CreateCustomerDto dto,
            [FromServices] SalesAdminDbContext db,
            [FromServices] EmbeddingService embeddingService,
            CancellationToken ct) =>
        {
            var email = string.IsNullOrWhiteSpace(dto.Email)
                ? $"cliente_{Guid.NewGuid().ToString("N")[..8]}@lead.local"
                : dto.Email.Trim().ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                var existing = await db.Customers.Include(c => c.Addresses).FirstOrDefaultAsync(c => c.Email == email, ct);
                if (existing is not null)
                {
                    return Results.Ok(existing);
                }
            }

            var customer = new Customer
            {
                Name = dto.Name.Trim(),
                Email = email,
                Phone = dto.Phone?.Trim(),
                DocumentNumber = dto.DocumentNumber?.Trim(),
                DocumentType = dto.DocumentType?.Trim(),
                CustomerType = dto.CustomerType ?? "individual",
                Notes = dto.Notes?.Trim(),
                Active = dto.Active
            };

            if (dto.Addresses is { Count: > 0 })
            {
                foreach (var a in dto.Addresses)
                {
                    customer.Addresses.Add(new CustomerAddress
                    {
                        Label = a.Label,
                        Street = a.Street,
                        Number = a.Number,
                        Complement = a.Complement,
                        Neighborhood = a.Neighborhood,
                        City = a.City,
                        State = a.State,
                        ZipCode = a.ZipCode,
                        Country = a.Country ?? "BR",
                        IsDefault = a.IsDefault
                    });
                }
            }

            var text = EmbeddingService.BuildCustomerEmbeddingText(customer);
            customer.Embedding = await embeddingService.GenerateEmbeddingAsync(text, ct);

            db.Customers.Add(customer);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/customers/{customer.Id}", customer);
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateCustomerDto dto,
            [FromServices] SalesAdminDbContext db,
            [FromServices] EmbeddingService embeddingService,
            CancellationToken ct) =>
        {
            var customer = await db.Customers.Include(c => c.Addresses).FirstOrDefaultAsync(c => c.Id == id, ct);
            if (customer is null) return Results.NotFound();

            customer.Name = dto.Name.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                customer.Email = dto.Email.Trim().ToLowerInvariant();
            }
            customer.Phone = dto.Phone?.Trim() ?? customer.Phone;
            customer.DocumentNumber = dto.DocumentNumber?.Trim();
            customer.DocumentType = dto.DocumentType?.Trim();
            customer.CustomerType = dto.CustomerType ?? "individual";
            customer.Notes = dto.Notes?.Trim();
            customer.Active = dto.Active;
            customer.UpdatedAt = DateTimeOffset.UtcNow;

            var text = EmbeddingService.BuildCustomerEmbeddingText(customer);
            customer.Embedding = await embeddingService.GenerateEmbeddingAsync(text, ct);

            await db.SaveChangesAsync(ct);
            return Results.Ok(customer);
        });

        group.MapDelete("/{id:guid}", async (Guid id, [FromServices] SalesAdminDbContext db) =>
        {
            var customer = await db.Customers.FindAsync(id);
            if (customer is null) return Results.NotFound();

            customer.Active = false;
            customer.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Endereços
        group.MapPost("/{id:guid}/addresses", async (
            Guid id,
            [FromBody] CreateAddressDto dto,
            [FromServices] SalesAdminDbContext db,
            CancellationToken ct) =>
        {
            var customer = await db.Customers.Include(c => c.Addresses).FirstOrDefaultAsync(c => c.Id == id, ct);
            if (customer is null) return Results.NotFound();

            if (dto.IsDefault)
            {
                foreach (var a in customer.Addresses) a.IsDefault = false;
            }

            var address = new CustomerAddress
            {
                CustomerId = customer.Id,
                Label = dto.Label,
                Street = dto.Street,
                Number = dto.Number,
                Complement = dto.Complement,
                Neighborhood = dto.Neighborhood,
                City = dto.City,
                State = dto.State,
                ZipCode = dto.ZipCode,
                Country = dto.Country ?? "BR",
                IsDefault = dto.IsDefault || customer.Addresses.Count == 0
            };

            db.CustomerAddresses.Add(address);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/customers/{id}/addresses/{address.Id}", address);
        });

        group.MapDelete("/{customerId:guid}/addresses/{addressId:guid}", async (
            Guid customerId,
            Guid addressId,
            [FromServices] SalesAdminDbContext db) =>
        {
            var address = await db.CustomerAddresses.FirstOrDefaultAsync(a => a.Id == addressId && a.CustomerId == customerId);
            if (address is null) return Results.NotFound();

            db.CustomerAddresses.Remove(address);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapGet("/{id:guid}/follow-ups", async (Guid id, [FromServices] SalesAdminDbContext db) =>
        {
            var followUps = await db.FollowUps
                .AsNoTracking()
                .Where(f => f.CustomerId == id)
                .OrderByDescending(f => f.ScheduledFor)
                .Select(f => new
                {
                    f.Id,
                    f.CustomerId,
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
                })
                .ToListAsync();

            return Results.Ok(followUps);
        });

        return group;
    }
}

public sealed record CreateCustomerDto(
    string Name,
    string? Email,
    string? Phone,
    string? DocumentNumber,
    string? DocumentType,
    string? CustomerType,
    string? Notes,
    bool Active = true,
    List<CreateAddressDto>? Addresses = null);

public sealed record UpdateCustomerDto(
    string Name,
    string? Email,
    string? Phone,
    string? DocumentNumber,
    string? DocumentType,
    string? CustomerType,
    string? Notes,
    bool Active = true);

public sealed record CreateAddressDto(
    string Label,
    string Street,
    string Number,
    string? Complement,
    string Neighborhood,
    string City,
    string State,
    string ZipCode,
    string? Country = "BR",
    bool IsDefault = false);
