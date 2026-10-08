using Microsoft.EntityFrameworkCore;
using SalesAdmin.Api.Data;
using SalesAdmin.Api.Entities;

namespace SalesAdmin.Api.Endpoints;

public static class ConversationEndpoints
{
    public static void MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/conversations").WithTags("Conversations");

        group.MapGet("/", async (SalesAdminDbContext db) =>
        {
            var conversations = await db.Conversations
                                        .Include(c => c.Customer)
                                        .Include(c => c.Messages)
                                        .OrderByDescending(c => c.CreatedAt)
                                        .ToListAsync();
            return Results.Ok(conversations);
        });

        group.MapGet("/{id}", async (SalesAdminDbContext db, Guid id) =>
        {
            var conversation = await db.Conversations
                                       .Include(c => c.Customer)
                                       .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
                                       .FirstOrDefaultAsync(c => c.Id == id);
            return conversation is not null ? Results.Ok(conversation) : Results.NotFound();
        });

        group.MapPost("/", async (SalesAdminDbContext db, Conversation conversation) =>
        {
            if (!string.IsNullOrWhiteSpace(conversation.SessionId))
            {
                var existing = await db.Conversations
                    .Include(c => c.Messages)
                    .FirstOrDefaultAsync(c => c.SessionId == conversation.SessionId);

                if (existing != null)
                {
                    existing.Status = conversation.Status;
                    if (conversation.CustomerId != null)
                    {
                        existing.CustomerId = conversation.CustomerId;
                    }
                    existing.UpdatedAt = DateTimeOffset.UtcNow;

                    foreach (var msg in conversation.Messages)
                    {
                        if (!existing.Messages.Any(m => m.Role == msg.Role && m.Content == msg.Content))
                        {
                            msg.ConversationId = existing.Id;
                            db.ConversationMessages.Add(msg);
                        }
                    }

                    await db.SaveChangesAsync();
                    return Results.Ok(existing);
                }
            }

            db.Conversations.Add(conversation);
            await db.SaveChangesAsync();
            return Results.Created($"/api/conversations/{conversation.Id}", conversation);
        });

        group.MapPost("/sync-message", async (SalesAdminDbContext db, SyncConversationMessageRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.SessionId) || string.IsNullOrWhiteSpace(req.Content))
            {
                return Results.BadRequest(new { error = "SessionId and Content are required." });
            }

            var conversation = await db.Conversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.SessionId == req.SessionId);

            if (conversation is null)
            {
                conversation = new Conversation
                {
                    Id = Guid.NewGuid(),
                    SessionId = req.SessionId,
                    CustomerId = req.CustomerId,
                    Status = req.Status ?? "active",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                db.Conversations.Add(conversation);
            }
            else
            {
                if (conversation.CustomerId == null && req.CustomerId != null)
                {
                    conversation.CustomerId = req.CustomerId;
                }
                if (!string.IsNullOrWhiteSpace(req.Status))
                {
                    conversation.Status = req.Status;
                }
                conversation.UpdatedAt = DateTimeOffset.UtcNow;
            }

            var isDuplicate = conversation.Messages.Any(m =>
                m.Role == req.Role &&
                m.Content == req.Content &&
                (DateTimeOffset.UtcNow - m.CreatedAt).TotalSeconds < 3);

            if (!isDuplicate)
            {
                var msg = new ConversationMessage
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversation.Id,
                    Role = req.Role,
                    Content = req.Content,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.ConversationMessages.Add(msg);
                conversation.Messages.Add(msg);
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { conversationId = conversation.Id, sessionId = conversation.SessionId });
        });

        group.MapPost("/{id}/messages", async (SalesAdminDbContext db, Guid id, ConversationMessage message) =>
        {
            var conversation = await db.Conversations.FindAsync(id);
            if (conversation is null) return Results.NotFound();

            message.ConversationId = id;
            db.ConversationMessages.Add(message);
            await db.SaveChangesAsync();
            return Results.Created($"/api/conversations/{id}/messages/{message.Id}", message);
        });

        group.MapDelete("/{id}", async (SalesAdminDbContext db, Guid id) =>
        {
            var conversation = await db.Conversations.FindAsync(id);
            if (conversation is null) return Results.NotFound();

            db.Conversations.Remove(conversation);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}

public sealed record SyncConversationMessageRequest(
    string SessionId,
    Guid? CustomerId,
    string Role,
    string Content,
    string? Status = "active"
);
