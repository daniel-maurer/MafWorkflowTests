using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SalesAdmin.Api.Entities;
using SalesAdmin.Api.Services;

namespace SalesAdmin.Api.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(
        SalesAdminDbContext db,
        EmbeddingService embeddingService,
        IConfiguration configuration,
        IWebHostEnvironment env)
    {
        // Garante que o schema e tabelas foram criados
        await db.Database.EnsureCreatedAsync();

        // Garante a tabela follow_ups caso EnsureCreatedAsync já tenha rodado anteriormente
        try
        {
            await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS follow_ups (
                ""Id"" uuid NOT NULL PRIMARY KEY,
                ""CustomerId"" uuid NOT NULL REFERENCES customers(""Id"") ON DELETE CASCADE,
                ""Module"" varchar(50) NOT NULL,
                ""FollowUpType"" varchar(50) NOT NULL,
                ""ReferenceId"" varchar(100),
                ""ReferenceTitle"" varchar(200),
                ""ScheduledFor"" timestamp with time zone NOT NULL,
                ""Channel"" varchar(30) NOT NULL,
                ""MessageText"" text NOT NULL,
                ""Status"" varchar(30) NOT NULL,
                ""SentAt"" timestamp with time zone,
                ""Notes"" text,
                ""CustomDataJson"" text,
                ""CreatedAt"" timestamp with time zone NOT NULL,
                ""UpdatedAt"" timestamp with time zone NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_follow_ups_customer_id ON follow_ups(""CustomerId"");
            CREATE INDEX IF NOT EXISTS ix_follow_ups_scheduled_status ON follow_ups(""ScheduledFor"", ""Status"");
            CREATE INDEX IF NOT EXISTS ix_follow_ups_module ON follow_ups(""Module"");
            ");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DbInitializer] Aviso ao verificar/criar tabela follow_ups: {ex.Message}");
        }

        // 1. Seed de Condições de Pagamento
        if (!await db.PaymentConditions.AnyAsync())
        {
            db.PaymentConditions.AddRange(
            [
                new PaymentCondition
                {
                    Name = "Pix à vista",
                    PaymentMethod = "pix",
                    MaxInstallments = 1,
                    InterestFree = true,
                    AdditionalDiscount = 5.0m,
                    Active = true
                },
                new PaymentCondition
                {
                    Name = "Cartão de Crédito 10x",
                    PaymentMethod = "credit_card",
                    MaxInstallments = 10,
                    InterestFree = true,
                    AdditionalDiscount = 0,
                    Active = true
                },
                new PaymentCondition
                {
                    Name = "Boleto Bancário",
                    PaymentMethod = "boleto",
                    MaxInstallments = 1,
                    InterestFree = true,
                    AdditionalDiscount = 0,
                    Active = true
                }
            ]);
            await db.SaveChangesAsync();
        }

        // 2. Seed de Descontos / Cupons
        if (!await db.Discounts.AnyAsync())
        {
            var discount = new Discount
            {
                Code = "PROMO10",
                Name = "Promoção Especial 10% OFF",
                Description = "Desconto comercial padrão para orçamentos e combos.",
                DiscountType = "percentage",
                DiscountValue = 10.0m,
                ValidFrom = DateTimeOffset.UtcNow.AddDays(-1),
                ValidUntil = DateTimeOffset.UtcNow.AddYears(1),
                PaymentMethods = ["pix", "credit_card", "boleto"],
                MaxUses = 1000,
                CurrentUses = 0,
                Active = true,
                Stackable = false
            };
            discount.Embedding = await embeddingService.GenerateEmbeddingAsync(EmbeddingService.BuildDiscountEmbeddingText(discount));
            db.Discounts.Add(discount);
            await db.SaveChangesAsync();
        }

        // 3. Seed de Categorias e Produtos a partir de product_catalog.json
        if (!await db.Products.AnyAsync())
        {
            var catalogPath = Path.Combine(env.ContentRootPath, "../../../infra/product_catalog.json");
            if (!File.Exists(catalogPath))
            {
                catalogPath = "product_catalog.json";
            }

            if (File.Exists(catalogPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(catalogPath);
                    var rawProducts = JsonSerializer.Deserialize<List<RawProductDto>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? [];

                    var categoryCache = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);

                    foreach (var raw in rawProducts)
                    {
                        Category? category = null;
                        if (!string.IsNullOrWhiteSpace(raw.Category))
                        {
                            if (!categoryCache.TryGetValue(raw.Category, out category))
                            {
                                category = new Category
                                {
                                    Name = raw.Category,
                                    Slug = raw.Category.ToLowerInvariant().Replace(" ", "-"),
                                    Active = true
                                };
                                db.Categories.Add(category);
                                categoryCache[raw.Category] = category;
                            }
                        }

                        var product = new Product
                        {
                            Sku = raw.Sku,
                            Name = raw.Name,
                            Description = raw.Description,
                            Price = raw.Price,
                            Currency = raw.Currency ?? "BRL",
                            InStock = raw.InStock,
                            StockQty = raw.StockQty,
                            Color = raw.Color,
                            Size = raw.Size,
                            Brand = raw.Brand,
                            Category = category,
                            Tags = raw.Tags ?? [],
                            CompatibleSkus = raw.CompatibleSkus ?? [],
                            ImageUrl = raw.ImageUrl,
                            Active = true
                        };

                        product.Embedding = await embeddingService.GenerateEmbeddingAsync(EmbeddingService.BuildProductEmbeddingText(product));
                        db.Products.Add(product);
                    }

                    await db.SaveChangesAsync();
                    Console.WriteLine($"[DbInitializer] Catálogo inicial semeado com {rawProducts.Count} produtos.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DbInitializer] Erro ao carregar product_catalog.json: {ex.Message}");
                }
            }
        }

        // 4. Seed de Clientes e Follow-Ups de exemplo
        if (!await db.FollowUps.AnyAsync())
        {
            var customer = await db.Customers.FirstOrDefaultAsync();
            if (customer is null)
            {
                customer = new Customer
                {
                    Name = "Carlos Eduardo Silva",
                    Email = "carlos.silva@exemplo.com",
                    Phone = "(11) 98765-4321",
                    DocumentNumber = "123.456.789-00",
                    DocumentType = "cpf",
                    CustomerType = "individual",
                    Active = true,
                    Addresses =
                    [
                        new CustomerAddress
                        {
                            Label = "Residencial",
                            Street = "Av. Paulista",
                            Number = "1000",
                            Complement = "Apto 42",
                            Neighborhood = "Bela Vista",
                            City = "São Paulo",
                            State = "SP",
                            ZipCode = "01310-100",
                            Country = "BR",
                            IsDefault = true
                        }
                    ]
                };
                db.Customers.Add(customer);
                await db.SaveChangesAsync();
            }

            db.FollowUps.AddRange(
            [
                new FollowUpRecord
                {
                    CustomerId = customer.Id,
                    Module = "sales",
                    FollowUpType = "quote_reminder",
                    ReferenceId = "ORC-1042",
                    ReferenceTitle = "Orçamento #1042 - Furadeira de Impacto Bosch",
                    ScheduledFor = DateTimeOffset.UtcNow.AddHours(2),
                    Channel = "WhatsApp",
                    MessageText = "Olá Carlos! Notamos que seu orçamento para a Furadeira Bosch ainda está em aberto. Conseguimos manter o cupom de 10% se fechar hoje!",
                    Status = "Pending"
                },
                new FollowUpRecord
                {
                    CustomerId = customer.Id,
                    Module = "vet",
                    FollowUpType = "foto_ferida",
                    ReferenceId = "PET-THOR-01",
                    ReferenceTitle = "Paciente: Thor (Golden Retriever) - Retirada de pontos",
                    ScheduledFor = DateTimeOffset.UtcNow.AddHours(24),
                    Channel = "WhatsApp",
                    MessageText = "Olá Carlos! Como está a cicatrização dos pontos do Thor hoje? Poderia nos enviar uma foto nítida do local para o Dr. verificar?",
                    Status = "Pending"
                }
            ]);
            await db.SaveChangesAsync();
            Console.WriteLine("[DbInitializer] Follow-ups de exemplo (Vendas e Vet) semeados com sucesso.");
        }
    }

    private sealed class RawProductDto
    {
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? Currency { get; set; }
        public string? ImageUrl { get; set; }
        public bool InStock { get; set; }
        public int StockQty { get; set; }
        public string? Color { get; set; }
        public string? Size { get; set; }
        public string? Brand { get; set; }
        public string? Category { get; set; }
        public List<string>? Tags { get; set; }
        public List<string>? CompatibleSkus { get; set; }
    }
}
