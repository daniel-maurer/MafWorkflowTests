using System.ComponentModel;
using System.Text.Json;
using SalesWorkflow.Models;

namespace SalesWorkflow.AiTools;

public sealed class StockInfo
{
    public string Sku { get; set; } = string.Empty;
    public bool InStock { get; set; }
    public int Quantity { get; set; }
    public string EstimatedDelivery { get; set; } = string.Empty;
}

public sealed class PriceInfo
{
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal PixPrice { get; set; }
    public string Installments { get; set; } = string.Empty;
}

public sealed class ProductImageInfo
{
    public string Sku { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
}

public static class CatalogTools
{
    private static List<ProductInfo>? _catalogCache;
    private static readonly object _lock = new();
    private static int _searchAttempts = 0;
    private static readonly HashSet<string> _searchedQueries = new(StringComparer.OrdinalIgnoreCase);

    public static void ResetSearchCounter()
    {
        lock (_lock)
        {
            _searchAttempts = 0;
            _searchedQueries.Clear();
        }
    }

    public static List<ProductInfo> LoadCatalog(string path = "product_catalog.json")
    {
        lock (_lock)
        {
            if (_catalogCache is not null)
            {
                return _catalogCache;
            }

            if (!File.Exists(path))
            {
                Logger.LogWarning($"Catalog file not found at '{path}', using default empty catalog.");
                _catalogCache = [];
                return _catalogCache;
            }

            try
            {
                var json = File.ReadAllText(path);
                _catalogCache = JsonSerializer.Deserialize<List<ProductInfo>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? [];
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load product catalog: {ex.Message}");
                _catalogCache = [];
            }

            return _catalogCache;
        }
    }

    [Description("Pesquisa produtos no catálogo por palavras-chave, nome, SKU ou filtros. Limite de no máximo 5 tentativas com termos diferentes.")]
    public static async Task<List<ProductInfo>> SearchProducts(
        [Description("Termo de busca com palavras-chave diferentes")] string query,
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
                Logger.LogWarning($"[TOOL] Limite de 5 tentativas de busca atingido para a query: '{trimmedQuery}'. Encerrando buscas no catálogo.");
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

        Logger.LogInfo($"[TOOL] Pesquisando catálogo ({_searchAttempts}/5): '{trimmedQuery}' (cor={color}, tam={size}, marca={brand})");
        await Task.Delay(100, cancellationToken);

        var catalog = LoadCatalog();
        var results = catalog.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            results = results.Where(p =>
                terms.Any(term =>
                    p.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || p.Sku.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (p.Brand != null && p.Brand.Contains(term, StringComparison.OrdinalIgnoreCase))
                    || (p.Category != null && p.Category.Contains(term, StringComparison.OrdinalIgnoreCase))
                    || p.Tags.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase))));
        }

        if (!string.IsNullOrWhiteSpace(color))
        {
            results = results.Where(p => string.Equals(p.Color, color, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(size))
        {
            results = results.Where(p => string.Equals(p.Size, size, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(brand))
        {
            results = results.Where(p => string.Equals(p.Brand, brand, StringComparison.OrdinalIgnoreCase));
        }

        if (minPrice.HasValue)
        {
            results = results.Where(p => p.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            results = results.Where(p => p.Price <= maxPrice.Value);
        }

        return results.Take(5).ToList();
    }

    [Description("Verifica a disponibilidade de estoque de um produto pelo SKU.")]
    public static async Task<StockInfo> CheckStock(
        [Description("SKU do produto")] string sku,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Verificando estoque: {sku}");
        await Task.Delay(80, cancellationToken);

        var catalog = LoadCatalog();
        var product = catalog.FirstOrDefault(p => string.Equals(p.Sku, sku, StringComparison.OrdinalIgnoreCase));

        if (product is null)
        {
            return new StockInfo
            {
                Sku = sku,
                InStock = false,
                Quantity = 0,
                EstimatedDelivery = "Produto não cadastrado"
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

    [Description("Obtém o preço atual e condições promocionais de um produto pelo SKU.")]
    public static async Task<PriceInfo> GetPrice(
        [Description("SKU do produto")] string sku,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Consultando preço: {sku}");
        await Task.Delay(80, cancellationToken);

        var catalog = LoadCatalog();
        var product = catalog.FirstOrDefault(p => string.Equals(p.Sku, sku, StringComparison.OrdinalIgnoreCase));

        if (product is null)
        {
            return new PriceInfo { Sku = sku, Price = 0, OriginalPrice = 0, PixPrice = 0, Installments = "N/A" };
        }

        var original = Math.Round(product.Price * 1.15m, 2);
        var pix = Math.Round(product.Price * 0.95m, 2);
        var installmentValue = Math.Round(product.Price / 10m, 2);

        return new PriceInfo
        {
            Sku = product.Sku,
            Price = product.Price,
            OriginalPrice = original,
            PixPrice = pix,
            Installments = $"10x de R$ {installmentValue:N2} sem juros"
        };
    }

    [Description("Obtém a URL da imagem principal de um produto pelo SKU.")]
    public static async Task<ProductImageInfo> GetProductImage(
        [Description("SKU do produto")] string sku,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Obtendo imagem: {sku}");
        await Task.Delay(50, cancellationToken);

        var catalog = LoadCatalog();
        var product = catalog.FirstOrDefault(p => string.Equals(p.Sku, sku, StringComparison.OrdinalIgnoreCase));

        var imageUrl = product?.ImageUrl ?? $"/api/products/{sku}/image";
        return new ProductImageInfo { Sku = sku, ImageUrl = imageUrl };
    }

    [Description("Lista produtos compatíveis ou complementares a um SKU informado.")]
    public static async Task<List<ProductInfo>> GetCompatibleProducts(
        [Description("SKU do produto base")] string sku,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Buscando compatíveis para: {sku}");
        await Task.Delay(100, cancellationToken);

        var catalog = LoadCatalog();
        var baseProduct = catalog.FirstOrDefault(p => string.Equals(p.Sku, sku, StringComparison.OrdinalIgnoreCase));

        if (baseProduct is null || baseProduct.CompatibleSkus.Count == 0)
        {
            return catalog.Where(p => !string.Equals(p.Sku, sku, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
        }

        var matches = catalog.Where(p => baseProduct.CompatibleSkus.Contains(p.Sku, StringComparer.OrdinalIgnoreCase)).ToList();
        return matches;
    }

    [Description("Calcula os valores exatos de um kit ou combo de produtos aplicando o percentual de desconto comercial.")]
    public static KitPriceCalculation CalculateKitPrice(
        [Description("Lista de SKUs dos produtos incluídos no kit")] List<string> productSkus,
        [Description("Percentual de desconto comercial a aplicar (ex: 10 para 10%)")] decimal discountPercent)
    {
        Logger.LogInfo($"[TOOL] Calculando preço do kit para {productSkus?.Count ?? 0} SKUs com {discountPercent}% de desconto.");
        var catalog = LoadCatalog();
        var items = catalog.Where(p => productSkus != null && productSkus.Contains(p.Sku, StringComparer.OrdinalIgnoreCase)).ToList();
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
