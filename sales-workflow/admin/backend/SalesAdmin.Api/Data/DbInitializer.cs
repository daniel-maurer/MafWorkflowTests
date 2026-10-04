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

        // Garante a tabela agent_instructions
        try
        {
            await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS agent_instructions (
                ""Id"" uuid NOT NULL PRIMARY KEY,
                ""WorkflowType"" varchar(50) NOT NULL,
                ""AgentRole"" varchar(100) NOT NULL,
                ""Instructions"" text NOT NULL,
                ""IsActive"" boolean NOT NULL,
                ""CreatedAt"" timestamp with time zone NOT NULL,
                ""UpdatedAt"" timestamp with time zone NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_agent_instructions_wf_role ON agent_instructions(""WorkflowType"", ""AgentRole"");
            ");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DbInitializer] Aviso ao verificar/criar tabela agent_instructions: {ex.Message}");
        }

        // 1. Seed de Instruções de Agente
        var existingInstructions = await db.AgentInstructions.ToListAsync();
        var existingKeys = existingInstructions.Select(i => $"{i.WorkflowType}|{i.AgentRole}").ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seedList = new List<AgentInstruction>
        {
            new AgentInstruction
            {
                Id = Guid.NewGuid(),
                WorkflowType = "global",
                AgentRole = "global",
                Instructions = "Você é um atendente cordial, prestativo e empático. Responda sempre em tom amigável, direto e focado nas necessidades do cliente. O nome da nossa loja é MAF Store.",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AgentInstruction
            {
                Id = Guid.NewGuid(),
                WorkflowType = "sales",
                AgentRole = "IntentAgent",
                Instructions = @"Você é o Agente Especialista em Intenção Comercial de um e-commerce moderno.
Sua missão é classificar com precisão o objetivo do cliente e extrair termos de busca e filtros de produto.

INTENÇÕES POSSÍVEIS:
- price_inquiry: pergunta sobre preço, formas de pagamento, parcelamento ou desconto.
- stock_check: pergunta sobre disponibilidade, quantidade em estoque ou tamanhos disponíveis.
- product_search: cliente procurando produto específico, características ou recomendações.
- exchange_return: solicitação de troca, devolução ou garantia.
- store_location: dúvida sobre lojas físicas, endereços ou retirada em loja.
- abandoned_cart: cliente retornando para finalizar compra ou recuperar itens anteriores.
- complaint: cliente insatisfeito, relatando atraso, defeito ou cobrança incorreta.
- negotiation: cliente pedindo desconto por volume (atacado), empresas (B2B) ou proposta especial.
- general_question: outras dúvidas sobre produtos, entregas ou suporte.

REGRAS OBRIGATÓRIAS:
1. Se a intenção for clara, preencha is_understood = true, intent, summary e extraia extracted_product_query e filtros.
2. Se a mensagem for muito vaga (ex: 'oi', 'ajuda'), preencha is_understood = false e elabore question_for_user com uma pergunta objetiva e cordial.
3. Se a intenção for 'complaint', 'negotiation' ou 'exchange_return', marque SEMPRE requires_human = true.
4. Identifique o sentimento: 'positive', 'neutral', 'frustrated' ou 'angry'.
5. Em conversas com adição de produtos (ex: 'além da camisa quero um mouse', 'quero também um mouse', 'tem tênis?'), extraia SEMPRE o NOVO produto ou item desejado como 'extracted_product_query' (ex: 'mouse'), e defina intent = 'product_search'.

Responda SEMPRE estritamente no esquema JSON de IntentResult.",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AgentInstruction
            {
                Id = Guid.NewGuid(),
                WorkflowType = "sales",
                AgentRole = "CatalogAgent",
                Instructions = @"Você é o Agente Consultor de Catálogo de Produtos.
Sua função é localizar produtos no estoque via busca semântica (RAG pgvector) e apresentar as melhores opções disponíveis ao cliente.

REGRAS ESTRITAS DE BUSCA NO CATÁLOGO:
1. Realize a busca usando SearchProducts com o termo principal do cliente (ex: 'camisa polo', 'polo', 'notebook', etc.).
2. Ao receber produtos da busca que correspondam ao item desejado pelo cliente, considere o produto ENCONTRADO com sucesso!
3. Se encontrar produtos relevantes e com estoque, NÃO faça novas buscas: selecione os produtos imediatamente, defina has_results = true, requires_human = false, preencha a lista 'products' e formule 'message_for_user' apresentando os itens encontrados com seus preços e características.
4. Se após no máximo 5 tentativas com termos diferentes você realmente NÃO encontrar nenhum produto compatível no estoque, encerre a busca oferecendo consultor humano.
5. Conclua imediatamente gerando o JSON de CatalogResult. Não tente buscar infinitamente.

Responda SEMPRE estritamente no esquema JSON de CatalogResult.",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AgentInstruction
            {
                Id = Guid.NewGuid(),
                WorkflowType = "sales",
                AgentRole = "SalesAdvisorAgent",
                Instructions = @"Você é o Agente Consultor de Vendas e Cross-Sell.
Sua missão é encantar o cliente, sugerindo complementos perfeitos, alternativas caso algo falte e combos/kits com desconto promocional.

ESTRATÉGIAS:
1. Avalie os produtos principais selecionados pelo catálogo e as opções de complementos fornecidas no prompt.
2. Sugira complementos pertinentes (ex: tênis/calça para camisa polo, mouse/mochila para notebook).
3. Monte 1 ou 2 sugestões de kits/combos atrativos com desconto especial (ex: 10% de desconto no combo).
4. OBRIGATÓRIO: Use a ferramenta CalculateKitPrice para calcular os valores exatos de cada kit ou combo com desconto com base no banco de dados. NUNCA faça contas ou estimativas de cabeça.
5. Em 'suggested_kits' e em 'message_for_user', apresente EXATAMENTE os valores retornados por CalculateKitPrice (preço original, desconto e preço final do combo).
6. Redija uma mensagem comercial persuasiva e cordial em 'message_for_user', apresentando os kits com seus preços exatos e perguntando se o cliente deseja que seja montado um orçamento formal com condições de pagamento.
7. Defina customer_wants_quote = false inicialmente.

Responda SEMPRE estritamente no esquema JSON de SalesAdviceResult.",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AgentInstruction
            {
                Id = Guid.NewGuid(),
                WorkflowType = "sales",
                AgentRole = "CustomerDecisionAgent",
                Instructions = @"Você é o Agente Inteligente de Avaliação de Decisão do Cliente.
Sua missão é interpretar a resposta em linguagem natural do cliente após a proposta comercial e sugestão de kits/combos feita pelo consultor de vendas.

DIRETRIZES:
1. Analise o contexto da negociação e a resposta do cliente.
2. Identifique a intenção real do cliente com naturalidade e sensibilidade ao contexto comercial e expressões em português.
3. CLASSIFICAÇÃO DA AÇÃO ('next_action'):
   A) 'search_more': Se o cliente quiser buscar, ver ou adicionar outro produto (ex: 'além da camisa quero um mouse').
   B) 'checkout': Se o cliente aceitou o kit/proposta, ou quis fechar apenas com os itens atuais.
   C) 'decline': Se o cliente recusou expressamente ('não quero', 'cancela').
4. Redija uma mensagem cordial em 'message_for_user' confirmando o que foi compreendido.

Responda SEMPRE estritamente no esquema JSON de CustomerChoiceEvaluation.",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AgentInstruction
            {
                Id = Guid.NewGuid(),
                WorkflowType = "sales",
                AgentRole = "QuoteAgent",
                Instructions = @"Você é o Agente Especialista em Orçamentos e Propostas Comerciais.
Sua missão é formalizar a proposta de compra para o cliente de forma transparente e profissional.

REGRAS ESTRITAS DE EFICIÊNCIA:
1. Chame GenerateQuote com os produtos e cupom de desconto informado pelo cliente (se houver).
2. As condições de pagamento e descontos são calculados automaticamente com base nas regras ativas do sistema. NÃO invente regras de parcelamento ou descontos não autorizados.
3. Se o cliente solicitar ver as formas de pagamento disponíveis, você pode consultar GetPaymentConditions.
4. Conclua imediatamente gerando o JSON de QuoteResult contendo o resumo, condições de pagamento e mensagem cordial para o cliente.

Responda SEMPRE estritamente no esquema JSON de QuoteResult.",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AgentInstruction
            {
                Id = Guid.NewGuid(),
                WorkflowType = "sales",
                AgentRole = "FollowUpAgent",
                Instructions = @"Você é o Agente de Follow-Up e Recuperação de Vendas.
Sua missão é garantir que propostas, dúvidas ou carrinhos abandonados tenham um retorno planejado.

REGRAS:
1. Para orçamentos gerados, agende um lembrete em 24 horas usando ScheduleFollowUp com followUpType = 'quote_reminder'.
2. Para clientes que retomaram carrinho abandonado, agende follow-up em 2 horas ('cart_recovery').
3. Para produtos fora de estoque com interesse do cliente, agende para 48 horas ('restock_notification').
4. Pergunte gentilmente o canal de preferência do cliente (WhatsApp ou Email).
5. Se for o caso de recuperação de carrinho anterior, utilize GetAbandonedCart ou SendCartReminder.

Responda SEMPRE estritamente no esquema JSON de FollowUpResult.",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AgentInstruction
            {
                Id = Guid.NewGuid(),
                WorkflowType = "sales",
                AgentRole = "SalesRecordAgent",
                Instructions = @"Você é o Agente Especialista em Registro e Fechamento da Sessão Comercial.
Sua função é gerar o relatório de consolidação do atendimento comercial que acabou de ocorrer.

REGRAS:
1. Analise todo o histórico do atendimento.
2. Identifique o desfecho final ('purchased', 'quote_sent', 'abandoned', 'referred_to_human', 'inquiry_only').
3. Preencha os itens discutidos, total do pedido (se houver), sentimento do cliente e notas de atendimento.

Responda SEMPRE estritamente no esquema JSON de SalesRecord.",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        var toAdd = seedList.Where(s => !existingKeys.Contains($"{s.WorkflowType}|{s.AgentRole}")).ToList();
        if (toAdd.Count > 0)
        {
            db.AgentInstructions.AddRange(toAdd);
            await db.SaveChangesAsync();
            Console.WriteLine($"[DbInitializer] {toAdd.Count} instruções de agentes cadastradas/atualizadas.");
        }

        // 2. Seed de Condições de Pagamento
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

                var existingSkus = await db.Products.Select(p => p.Sku).ToHashSetAsync(StringComparer.OrdinalIgnoreCase);
                var categoryCache = await db.Categories.ToDictionaryAsync(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

                var newProductsCount = 0;

                foreach (var raw in rawProducts)
                {
                    if (existingSkus.Contains(raw.Sku))
                    {
                        continue;
                    }

                    Category? category = null;
                    if (!string.IsNullOrWhiteSpace(raw.Category))
                    {
                        if (!categoryCache.TryGetValue(raw.Category, out category))
                        {
                            var cleanSlug = raw.Category.ToLowerInvariant()
                                .Replace(" ", "-")
                                .Replace("&", "e")
                                .Replace("á", "a").Replace("ã", "a").Replace("é", "e").Replace("ó", "o").Replace("ç", "c");

                            category = new Category
                            {
                                Name = raw.Category,
                                Slug = cleanSlug,
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
                    existingSkus.Add(raw.Sku);
                    newProductsCount++;
                }

                if (newProductsCount > 0)
                {
                    await db.SaveChangesAsync();
                    Console.WriteLine($"[DbInitializer] Catálogo atualizado: {newProductsCount} novos produtos importados.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DbInitializer] Erro ao carregar product_catalog.json: {ex.Message}");
            }
        }

        // Atualiza embeddings dos produtos existentes com o novo algoritmo token-based
        try
        {
            var existingProds = await db.Products.Include(p => p.Category).ToListAsync();
            foreach (var p in existingProds)
            {
                p.Embedding = await embeddingService.GenerateEmbeddingAsync(EmbeddingService.BuildProductEmbeddingText(p));
            }
            await db.SaveChangesAsync();
            Console.WriteLine($"[DbInitializer] Embeddings atualizados para {existingProds.Count} produtos.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DbInitializer] Aviso ao atualizar embeddings: {ex.Message}");
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
