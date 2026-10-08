using System.Security.Claims;
using System.Text.Encodings.Web;
using Azure.AI.OpenAI;
using Azure.Identity;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;
using SalesAdmin.Api.Data;
using SalesAdmin.Api.Endpoints;
using SalesAdmin.Api.Services;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

// ── Database (EF Core + Npgsql + pgvector) ──
builder.Services.AddDbContext<SalesAdminDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("SalesAdmin")
        ?? "Host=localhost;Port=5432;Database=sales_admin;Username=maf_admin;Password=maf_dev_2024";

    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.UseVector();
    });
});

// ── Embeddings / Azure OpenAI ──
var azureEndpoint = builder.Configuration["AzureOpenAi:Endpoint"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
var embeddingDeployment = builder.Configuration["AzureOpenAi:EmbeddingDeployment"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_EMBEDDING_DEPLOYMENT")
    ?? "text-embedding-3-small";

if (!string.IsNullOrWhiteSpace(azureEndpoint) && Uri.TryCreate(azureEndpoint, UriKind.Absolute, out var endpointUri))
{
    try
    {
        var openAiClient = new AzureOpenAIClient(endpointUri, new AzureCliCredential());
        var embeddingGenerator = openAiClient.GetEmbeddingClient(embeddingDeployment).AsIEmbeddingGenerator();
        builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(embeddingGenerator);
        builder.Services.AddSingleton<EmbeddingService>();
    }
    catch
    {
        builder.Services.AddSingleton<EmbeddingService>(new EmbeddingService(null));
    }
}
else
{
    builder.Services.AddSingleton<EmbeddingService>(new EmbeddingService(null));
}

// ── CORS ──
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [
    "http://localhost:5173", "http://127.0.0.1:5173",
    "http://localhost:5174", "http://127.0.0.1:5174"
];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ── Authentication ──
var authMode = builder.Configuration["Auth:Mode"] ?? "Mock";
if (string.Equals(authMode, "Entra", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
}
else
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "Mock";
        options.DefaultChallengeScheme = "Mock";
    })
    .AddScheme<AuthenticationSchemeOptions, MockBearerHandler>("Mock", _ => { });
}

builder.Services.AddAuthorization();

// ── Swagger ──
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MAF Sales Admin API",
        Version = "v1",
        Description = "API de Gestão do Catálogo, Produtos, Descontos e Clientes com Busca Vetorial (pgvector)"
    });
});

var app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sales Admin API v1");
    c.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();

// ── Map Endpoints ──
var api = app.MapGroup("/api");
api.MapGroup("/products").MapProductEndpoints().WithTags("Products");
api.MapGroup("/categories").MapCategoryEndpoints().WithTags("Categories");
api.MapGroup("/discounts").MapDiscountEndpoints().WithTags("Discounts");
api.MapGroup("/customers").MapCustomerEndpoints().WithTags("Customers");
api.MapGroup("/payment-conditions").MapPaymentConditionEndpoints().WithTags("Payment Conditions");
api.MapGroup("/follow-ups").MapFollowUpEndpoints().WithTags("Follow-Ups");
api.MapGroup("/search").MapSearchEndpoints().WithTags("Vector Search (RAG)");
api.MapGroup("/agent-instructions").MapAgentInstructionEndpoints().WithTags("Agent Instructions");
app.MapDeliveryMethodEndpoints();
app.MapStoreInfoEndpoints();
app.MapOrderEndpoints();
app.MapConversationEndpoints();
app.MapCampaignEndpoints();

// Status endpoint
api.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow })).AllowAnonymous();

// ── Seed / Database Init ──
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SalesAdminDbContext>();
    var embeddingService = scope.ServiceProvider.GetRequiredService<EmbeddingService>();
    try
    {
        await DbInitializer.InitializeAsync(db, embeddingService, app.Configuration, app.Environment);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Aviso ao conectar/inicializar banco de dados: {ex.Message}. Certifique-se de que o container PostgreSQL está em execução.");
    }
}

app.Run();

// ── Mock Bearer Handler ──
public sealed class MockBearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? token = null;
        var authHeader = Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = authHeader["Bearer ".Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var username = token.StartsWith("mock-token:", StringComparison.OrdinalIgnoreCase)
            ? token["mock-token:".Length..]
            : "mock-user";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, username),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Email, $"{username}@mock.local"),
            new Claim(ClaimTypes.Role, "Admin"),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
