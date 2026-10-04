using Microsoft.EntityFrameworkCore;
using SalesAdmin.Api.Data;
using SalesAdmin.Api.Entities;
using MafWorkflow.Shared.Contracts;

namespace SalesAdmin.Api.Endpoints;

public static class AgentInstructionEndpoints
{
    public static RouteGroupBuilder MapAgentInstructionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (SalesAdminDbContext db, string? workflowType) =>
        {
            var query = db.AgentInstructions.AsQueryable();
            if (!string.IsNullOrWhiteSpace(workflowType))
            {
                query = query.Where(a => a.WorkflowType == workflowType || a.WorkflowType == "global");
            }

            var items = await query.OrderBy(a => a.WorkflowType == "global" ? 0 : 1).ThenBy(a => a.AgentRole).ToListAsync();
            var dtos = items.Select(a => new AgentInstructionDto
            {
                Id = a.Id,
                WorkflowType = a.WorkflowType,
                AgentRole = a.AgentRole,
                Instructions = a.Instructions,
                IsActive = a.IsActive,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt
            });
            return Results.Ok(dtos);
        });

        group.MapPost("/", async (AgentInstructionDto dto, SalesAdminDbContext db) =>
        {
            var entity = new AgentInstruction
            {
                Id = Guid.NewGuid(),
                WorkflowType = dto.WorkflowType,
                AgentRole = dto.AgentRole,
                Instructions = dto.Instructions,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.AgentInstructions.Add(entity);
            await db.SaveChangesAsync();

            dto.Id = entity.Id;
            dto.CreatedAt = entity.CreatedAt;
            dto.UpdatedAt = entity.UpdatedAt;
            return Results.Created($"/api/agent-instructions/{entity.Id}", dto);
        });

        group.MapPut("/{id:guid}", async (Guid id, AgentInstructionDto dto, SalesAdminDbContext db) =>
        {
            var entity = await db.AgentInstructions.FindAsync(id);
            if (entity is null) return Results.NotFound();

            entity.WorkflowType = dto.WorkflowType;
            entity.AgentRole = dto.AgentRole;
            entity.Instructions = dto.Instructions;
            entity.IsActive = dto.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}", async (Guid id, SalesAdminDbContext db) =>
        {
            var entity = await db.AgentInstructions.FindAsync(id);
            if (entity is null) return Results.NotFound();

            db.AgentInstructions.Remove(entity);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return group;
    }
}
