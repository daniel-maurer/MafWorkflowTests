using System.ComponentModel;
using SalesWorkflow.Models;
using SalesWorkflow.Services;
using SalesWorkflow.Utilities;

namespace SalesWorkflow.AiTools;

public sealed class StockInfo
{
    public string Sku { get; set; } = string.Empty;
    public bool InStock { get; set; }
    public int Quantity { get; set; }
    public string EstimatedDelivery { get; set; } = string.Empty;
}

public sealed class ProductImageInfo
{
    public string Sku { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
}

public sealed class CatalogTools
{
    private readonly SalesAdminClient _client;
    private int _searchAttempts = 0;
    private readonly HashSet<string> _searchedQueries = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public CatalogTools(SalesAdminClient client)
    {
        _client = client;
    }

    public void ResetSearchCounter()
    {
        lock (_lock)
        {
            _searchAttempts = 0;
            _searchedQueries.Clear();
        }
    }

    [Description("Pesquisa produtos no catálogo via busca semântica (RAG pgvector) e palavras-chave. Limite de 5 tentativas com termos diferentes.")]
    public async Task<List<ProductInfo>> SearchProducts(
        [Description("Termo de busca com palavras-chave ou descrição do produto desejado")] string query,
        [Description("Cor desejada (opcional)")] string? color = null,
        [Description("Tamanho desejado (opcional)")] string? size = null,
        [Description("Marca (opcional)")] string? brand = null,
        [Description("Preço mínimo (opcional)")] decimal? minPrice = null,
        [Description("Preço máximo (opcional)")] decimal? maxPrice = null,
        CancellationToken cancellationToken = default)
    {
        var trimmedQuery = query?.Trim() ?? string.Empty;

        lock (_lock)
        {
            if (_searchAttempts >= 5)
            {
                Logger.LogWarning($"[TOOL] Limite de 5 tentativas de busca atingido para a query: '{trimmedQuery}'. Encerrando buscas.");
                return [];
            }

            if (_searchedQueries.Contains(trimmedQuery))
            {
                Logger.LogInfo($"[TOOL] Termo repetido ignorado: '{trimmedQuery}'. Tentativa não contada.");
                return [];
            }

            _searchAttempts++;
            _searchedQueries.Add(trimmedQuery);
        }

        Logger.LogInfo($"[TOOL] Pesquisando produtos via RAG ({_searchAttempts}/5): '{trimmedQuery}' (cor={color}, tam={size}, marca={brand})");

        // 1. Busca Semântica RAG no PostgreSQL (pgvector)
        var results = await _client.SearchProductsSemanticAsync(trimmedQuery, top: 10, cancellationToken);

        // 2. Filtros em memória adicionais se fornecidos
        var filtered = results.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(color))
        {
            filtered = filtered.Where(p => string.Equals(p.Color, color, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(size))
        {
            filtered = filtered.Where(p => string.Equals(p.Size, size, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(brand))
        {
            filtered = filtered.Where(p => string.Equals(p.Brand, brand, StringComparison.OrdinalIgnoreCase));
        }

        if (minPrice.HasValue)
        {
            filtered = filtered.Where(p => p.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            filtered = filtered.Where(p => p.Price <= maxPrice.Value);
        }

        return filtered.Take(5).ToList();
    }

    [Description("Verifica a disponibilidade de estoque real de um produto no banco pelo SKU.")]
    public async Task<StockInfo> CheckStock(
        [Description("SKU do produto")] string sku,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Verificando estoque no banco: {sku}");
        var product = await _client.GetProductBySkuAsync(sku, cancellationToken);

        if (product is null)
        {
            return new StockInfo
            {
                Sku = sku,
                InStock = false,
                Quantity = 0,
                EstimatedDelivery = "Produto não encontrado no cadastro"
            };
        }

        return new StockInfo
        {
            Sku = product.Sku,
            InStock = product.InStock,
            Quantity = product.StockQty,
            EstimatedDelivery = product.InStock ? "2 a 5 dias úteis" : "Sem previsão de reposição imediata"
        };
    }

    [Description("Obtém o preço atualizado do banco e condições de pagamento ativas de um produto pelo SKU.")]
    public async Task<PriceInfo> GetPrice(
        [Description("SKU do produto")] string sku,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Consultando preço e condições para: {sku}");
        var product = await _client.GetProductBySkuAsync(sku, cancellationToken);
        var conditions = await _client.GetPaymentConditionsAsync(cancellationToken);

        if (product is null)
        {
            return new PriceInfo { Sku = sku, Price = 0, OriginalPrice = 0, PixPrice = 0, Installments = "N/A" };
        }

        var pixCondition = conditions.FirstOrDefault(c => c.PaymentMethod.Equals("pix", StringComparison.OrdinalIgnoreCase));
        var cardCondition = conditions.FirstOrDefault(c => c.PaymentMethod.Equals("credit_card", StringComparison.OrdinalIgnoreCase));

        var pixDiscount = pixCondition?.AdditionalDiscount ?? 5m;
        var maxInstallments = cardCondition?.MaxInstallments ?? 10;
        var pixPrice = Math.Round(product.Price * (1 - pixDiscount / 100m), 2);
        var installmentValue = Math.Round(product.Price / Math.Max(1, maxInstallments), 2);

        return new PriceInfo
        {
            Sku = product.Sku,
            Price = product.Price,
            OriginalPrice = product.Price,
            PixPrice = pixPrice,
            Installments = $"{maxInstallments}x de R$ {installmentValue:N2}{(cardCondition?.InterestFree == true ? " sem juros" : "")}"
        };
    }

    [Description("Obtém a URL da imagem cadastrada de um produto pelo SKU.")]
    public async Task<ProductImageInfo> GetProductImage(
        [Description("SKU do produto")] string sku,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Obtendo imagem: {sku}");
        var product = await _client.GetProductBySkuAsync(sku, cancellationToken);
        var imageUrl = !string.IsNullOrWhiteSpace(product?.ImageUrl) ? product.ImageUrl : $"/api/products/{sku}/image";
        return new ProductImageInfo { Sku = sku, ImageUrl = imageUrl };
    }

    [Description("Lista produtos compatíveis ou complementares a um SKU informado.")]
    public async Task<List<ProductInfo>> GetCompatibleProducts(
        [Description("SKU do produto base")] string sku,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Buscando compatíveis para: {sku}");
        var baseProduct = await _client.GetProductBySkuAsync(sku, cancellationToken);

        if (baseProduct is not null && baseProduct.CompatibleSkus.Count > 0)
        {
            var matches = await _client.GetProductsBySkusAsync(baseProduct.CompatibleSkus, cancellationToken);
            if (matches.Count > 0) return matches;
        }

        // Fallback: busca semântica de produtos similares na mesma categoria ou marca
        var term = $"{baseProduct?.Category} {baseProduct?.Brand}".Trim();
        if (string.IsNullOrWhiteSpace(term)) term = "acessórios complementares";
        var similar = await _client.SearchProductsSemanticAsync(term, top: 3, cancellationToken);
        return similar.Where(p => !string.Equals(p.Sku, sku, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
    }

    [Description("Calcula os valores exatos de um kit de produtos aplicando o percentual de desconto comercial com base nos preços reais.")]
    public async Task<KitPriceCalculation> CalculateKitPrice(
        [Description("Lista de SKUs dos produtos incluídos no kit")] List<string> productSkus,
        [Description("Percentual de desconto comercial a aplicar (ex: 10 para 10%)")] decimal discountPercent,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Calculando preço do kit para {productSkus?.Count ?? 0} SKUs com {discountPercent}% de desconto.");
        var items = await _client.GetProductsBySkusAsync(productSkus ?? [], cancellationToken);
        var originalPrice = items.Sum(p => p.Price);
        var discountAmount = Math.Round(originalPrice * (discountPercent / 100m), 2);
        var finalPrice = Math.Max(0, originalPrice - discountAmount);

        return new KitPriceCalculation
        {
            ProductSkus = productSkus ?? [],
            OriginalPrice = originalPrice,
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            FinalPrice = finalPrice
        };
    }
}
