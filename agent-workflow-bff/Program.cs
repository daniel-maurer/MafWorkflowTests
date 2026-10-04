using AgentWorkflow.Bff.Contracts;
using AgentWorkflow.Bff.Hubs;
using AgentWorkflow.Bff.Options;
using AgentWorkflow.Bff.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Identity.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<WorkflowConfigOptions>(builder.Configuration.GetSection(WorkflowConfigOptions.SectionName));

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

builder.Services.AddCors(options => 
{
    options.AddPolicy("Frontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5173", "http://127.0.0.1:5173"];

        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var authMode = builder.Configuration.GetValue<string>("Auth:Mode") ?? "Mock";
if (authMode.Equals("Entra", StringComparison.OrdinalIgnoreCase))
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

    builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.Events ??= new JwtBearerEvents();
        var previous = options.Events.OnMessageReceived;

        options.Events.OnMessageReceived = async context =>
        {
            if (previous is not null)
            {
                await previous(context);
            }

            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrWhiteSpace(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
        };
    });
}
else
{
    builder.Services
        .AddAuthentication(MockBearerDefaults.AuthenticationScheme)
        .AddScheme<MockBearerOptions, MockBearerHandler>(MockBearerDefaults.AuthenticationScheme, _ => { });
}

builder.Services.AddAuthorization();

builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.PayloadSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

builder.Services.AddSingleton<IWorkflowConfigStore, JsonWorkflowConfigStore>();
builder.Services.AddSingleton<ISessionRegistry, InMemorySessionRegistry>();
builder.Services.AddSingleton<IFrontendEventPublisher, FrontendEventPublisher>();
builder.Services.AddSingleton<IMafCommandPublisher, MafCommandPublisher>();
builder.Services.AddSingleton<IBffNoSqlStore, JsonBffNoSqlStore>();

builder.Services.AddHttpClient("SalesAdmin", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["SalesAdminApi:BaseUrl"] ?? "http://localhost:5100/api/");
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("Frontend");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api").RequireAuthorization();

api.MapGet("/workflows", async (IWorkflowConfigStore configs, CancellationToken ct) =>
{
    var workflows = await configs.GetAllAsync(ct);
    return Results.Ok(workflows);
});

api.MapGet("/workflows/{workflowId}", async (string workflowId, IWorkflowConfigStore configs, CancellationToken ct) =>
{
    var workflow = await configs.GetByIdAsync(workflowId, ct);
    return workflow is null
        ? Results.NotFound(ErrorEnvelope.NotFound("WORKFLOW_NOT_FOUND", $"No workflow with id '{workflowId}'."))
        : Results.Ok(workflow);
});

api.MapPost("/workflow-sessions", async (
    CreateWorkflowSessionRequest request,
    IWorkflowConfigStore configs,
    ISessionRegistry sessions,
    IMafCommandPublisher maf,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.WorkflowId))
    {
        return Results.BadRequest(ErrorEnvelope.InvalidArgument("workflowId is required."));
    }

    var workflow = await configs.GetByIdAsync(request.WorkflowId, ct);
    if (workflow is null)
    {
        return Results.NotFound(ErrorEnvelope.NotFound("WORKFLOW_NOT_FOUND", $"No workflow with id '{request.WorkflowId}'."));
    }

    var session = sessions.Create(request.WorkflowId);

    await maf.StartWorkflowAsync(new MafStartWorkflowCommand(
        session.SessionId,
        request.WorkflowId,
        session.TicketId,
        request.InitialMessage,
        workflow.Maf.WorkflowName,
        workflow.Maf.Version,
        workflow.Maf.InputSchema,
        request.CustomerId,
        request.CustomerData), ct);

    return Results.Ok(new CreateWorkflowSessionResponse(session.SessionId, session.TicketId));
});

api.MapGet("/workflow-sessions/{sessionId}", (string sessionId, ISessionRegistry sessions) =>
{
    var snapshot = sessions.GetSnapshot(sessionId);
    return snapshot is null
        ? Results.NotFound(ErrorEnvelope.NotFound("SESSION_NOT_FOUND", $"No session with id '{sessionId}'."))
        : Results.Ok(snapshot);
});

api.MapPost("/workflow-sessions/{sessionId}/reset", async (
    string sessionId,
    ResetWorkflowSessionRequest? request,
    ISessionRegistry sessions,
    IMafCommandPublisher maf,
    CancellationToken ct) =>
{
    var snapshot = sessions.Reset(sessionId);
    if (snapshot is null)
    {
        return Results.NotFound(ErrorEnvelope.NotFound("SESSION_NOT_FOUND", $"No session with id '{sessionId}'."));
    }

    await maf.ResetWorkflowAsync(new MafSessionCommand(sessionId, request?.CustomerId, request?.CustomerData), ct);
    return Results.Ok(snapshot);
});

// Proxy para clientes na Sales Admin API
api.MapGet("/customers/find", async (string identifier, IHttpClientFactory httpFactory, CancellationToken ct) =>
{
    try
    {
        var client = httpFactory.CreateClient("SalesAdmin");
        var res = await client.GetAsync($"customers/find?identifier={Uri.EscapeDataString(identifier)}", ct);
        if (!res.IsSuccessStatusCode)
        {
            return Results.NotFound(new { error = "Cliente não encontrado" });
        }
        var content = await res.Content.ReadAsStringAsync(ct);
        return Results.Content(content, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Erro ao conectar com Sales Admin API: {ex.Message}");
    }
}).WithTags("Customers").AllowAnonymous();

api.MapGet("/customers", async (string? search, IHttpClientFactory httpFactory, CancellationToken ct) =>
{
    try
    {
        var client = httpFactory.CreateClient("SalesAdmin");
        var url = string.IsNullOrWhiteSpace(search) ? "customers?pageSize=10" : $"customers?search={Uri.EscapeDataString(search)}&pageSize=10";
        var res = await client.GetAsync(url, ct);
        if (!res.IsSuccessStatusCode) return Results.Ok(new { items = Array.Empty<object>(), total = 0 });
        var content = await res.Content.ReadAsStringAsync(ct);
        return Results.Content(content, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Erro ao conectar com Sales Admin API: {ex.Message}");
    }
}).WithTags("Customers").AllowAnonymous();

api.MapGet("/workflow-sessions/{sessionId}/messages", (string sessionId, DateTimeOffset? since, ISessionRegistry sessions) =>
{
    var messages = sessions.GetMessages(sessionId, since);
    return messages is null
        ? Results.NotFound(ErrorEnvelope.NotFound("SESSION_NOT_FOUND", $"No session with id '{sessionId}'."))
        : Results.Ok(new PagedResponse<MessageDto>(messages, messages.Count));
});

api.MapGet("/workflow-sessions/{sessionId}/trace", (string sessionId, DateTimeOffset? since, ISessionRegistry sessions) =>
{
    var trace = sessions.GetTrace(sessionId, since);
    return trace is null
        ? Results.NotFound(ErrorEnvelope.NotFound("SESSION_NOT_FOUND", $"No session with id '{sessionId}'."))
        : Results.Ok(new PagedResponse<TraceEventDto>(trace, trace.Count));
});

api.MapGet("/knowledge-base", async (string workflowId, string? query, IWorkflowConfigStore configs, CancellationToken ct) =>
{
    var workflow = await configs.GetByIdAsync(workflowId, ct);
    if (workflow is null)
    {
        return Results.NotFound(ErrorEnvelope.NotFound("WORKFLOW_NOT_FOUND", $"No workflow with id '{workflowId}'."));
    }

    var items = workflow.FixedKnowledgeBase
        .Where(item =>
            string.IsNullOrWhiteSpace(query)
            || item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.Summary.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase)))
        .ToArray();

    return Results.Ok(new PagedResponse<KbItemDto>(items, items.Length));
});

api.MapGet("/scenarios", async (string workflowId, IWorkflowConfigStore configs, CancellationToken ct) =>
{
    var workflow = await configs.GetByIdAsync(workflowId, ct);
    return workflow is null
        ? Results.NotFound(ErrorEnvelope.NotFound("WORKFLOW_NOT_FOUND", $"No workflow with id '{workflowId}'."))
        : Results.Ok(workflow.Scenarios);
});

// ===== Simulation Endpoints for Sales Workflow =====

api.MapGet("/products/{sku}/image", (string sku, IWebHostEnvironment env) =>
{
    var extensions = new[] { ".svg", ".webp", ".png", ".jpg", ".jpeg" };
    var webRoot = env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    foreach (var ext in extensions)
    {
        var path = Path.Combine(webRoot, "images", "products", $"{sku}{ext}");
        if (File.Exists(path))
        {
            var contentType = ext switch
            {
                ".svg" => "image/svg+xml",
                ".webp" => "image/webp",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                _ => "application/octet-stream"
            };
            return Results.File(path, contentType);
        }
    }

    var fallbackSvg = $"""
        <svg xmlns="http://www.w3.org/2000/svg" width="300" height="300" viewBox="0 0 300 300">
            <rect width="300" height="300" rx="16" fill="#1e293b"/>
            <rect x="20" y="20" width="260" height="260" rx="12" fill="#0f172a" stroke="#334155" stroke-width="2"/>
            <text x="150" y="140" text-anchor="middle" fill="#94a3b8" font-family="sans-serif" font-size="14" font-weight="bold">PRODUTO</text>
            <text x="150" y="170" text-anchor="middle" fill="#38bdf8" font-family="sans-serif" font-size="16" font-weight="bold">{sku}</text>
        </svg>
        """;
    return Results.Content(fallbackSvg, "image/svg+xml");
}).WithTags("Products").AllowAnonymous();

// ── NoSQL Storage: Theme Settings ──
api.MapGet("/settings/theme", async (IBffNoSqlStore noSqlStore, CancellationToken ct) =>
{
    var themeDoc = await noSqlStore.GetDocumentAsync<ThemeSettingDto>("settings", "theme", ct);
    themeDoc ??= new ThemeSettingDto { Theme = "escuro", UpdatedAt = DateTimeOffset.UtcNow };
    return Results.Ok(themeDoc);
}).WithTags("Settings").AllowAnonymous();

api.MapPost("/settings/theme", async (ThemeSettingRequest request, IBffNoSqlStore noSqlStore, CancellationToken ct) =>
{
    var raw = request?.Theme?.Trim().ToLowerInvariant() ?? "escuro";
    var normalized = (raw == "clara" || raw == "claro" || raw == "light") ? "claro" : "escuro";
    var doc = new ThemeSettingDto { Theme = normalized, UpdatedAt = DateTimeOffset.UtcNow };
    await noSqlStore.UpsertDocumentAsync("settings", "theme", doc, ct);
    return Results.Ok(doc);
}).WithTags("Settings").AllowAnonymous();

api.MapPut("/settings/theme", async (ThemeSettingRequest request, IBffNoSqlStore noSqlStore, CancellationToken ct) =>
{
    var raw = request?.Theme?.Trim().ToLowerInvariant() ?? "escuro";
    var normalized = (raw == "clara" || raw == "claro" || raw == "light") ? "claro" : "escuro";
    var doc = new ThemeSettingDto { Theme = normalized, UpdatedAt = DateTimeOffset.UtcNow };
    await noSqlStore.UpsertDocumentAsync("settings", "theme", doc, ct);
    return Results.Ok(doc);
}).WithTags("Settings").AllowAnonymous();

app.MapHub<FrontendWorkflowHub>("/hubs/workflow").RequireAuthorization();
app.MapHub<MafBridgeHub>("/hubs/maf").RequireAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();

public partial class Program { }

