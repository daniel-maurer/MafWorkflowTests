using System.ComponentModel;
using SalesWorkflow.Models;

namespace SalesWorkflow.AiTools;

public static class QuoteTools
{
    private static readonly Dictionary<string, QuoteResult> _quotes = new();
    private static readonly object _lock = new();

    [Description("Gera um orçamento formal com base nos SKUs e quantidades solicitados.")]
    public static async Task<QuoteResult> GenerateQuote(
        [Description("Lista de SKUs dos produtos")] List<string> productSkus,
        [Description("Quantidades correspondentes a cada SKU")] List<int> quantities,
        [Description("Cupom de desconto opcional")] string? couponCode = null,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Gerando orçamento formal para {productSkus?.Count ?? 0} SKUs (Cupom: {couponCode ?? "nenhum"})");
        await Task.Delay(120, cancellationToken);

        var catalog = CatalogTools.LoadCatalog();
        var items = new List<QuoteItem>();
        decimal subtotal = 0;

        if (productSkus != null)
        {
            for (int i = 0; i < productSkus.Count; i++)
            {
                var sku = productSkus[i];
                var qty = (quantities != null && i < quantities.Count && quantities[i] > 0) ? quantities[i] : 1;

                var product = catalog.FirstOrDefault(p => string.Equals(p.Sku, sku, StringComparison.OrdinalIgnoreCase));
                var unitPrice = product?.Price ?? 99.90m;
                var total = unitPrice * qty;

                items.Add(new QuoteItem
                {
                    Sku = sku,
                    Name = product?.Name ?? $"Item {sku}",
                    Quantity = qty,
                    UnitPrice = unitPrice,
                    Total = total
                });

                subtotal += total;
            }
        }

        decimal discount = 0;
        if (!string.IsNullOrWhiteSpace(couponCode) && couponCode.Contains("PROMO", StringComparison.OrdinalIgnoreCase))
        {
            discount = Math.Round(subtotal * 0.10m, 2);
        }

        var totalFinal = Math.Max(0, subtotal - discount);
        var quoteId = $"QT-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

        var result = new QuoteResult
        {
            QuoteId = quoteId,
            Items = items,
            Subtotal = subtotal,
            Discount = discount,
            Total = totalFinal,
            Currency = "BRL",
            ValidUntil = DateTimeOffset.UtcNow.AddDays(7),
            PaymentConditions = $"Pix à vista com 5% adicional (R$ {totalFinal * 0.95m:N2}) ou até 10x sem juros no cartão.",
            MessageForUser = $"Orçamento {quoteId} gerado com sucesso! Total: R$ {totalFinal:N2} com validade de 7 dias.",
            CustomerAccepted = false
        };

        lock (_lock)
        {
            _quotes[quoteId] = result;
        }

        return result;
    }

    [Description("Aplica um percentual de desconto comercial a um orçamento existente.")]
    public static async Task<QuoteResult> ApplyDiscount(
        [Description("Identificador do orçamento gerado")] string quoteId,
        [Description("Percentual de desconto (ex: 5 para 5%, 10 para 10%)")] decimal discountPercent,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Aplicando {discountPercent}% de desconto ao orçamento {quoteId}");
        await Task.Delay(80, cancellationToken);

        lock (_lock)
        {
            if (_quotes.TryGetValue(quoteId, out var existing))
            {
                var discountAmount = Math.Round(existing.Subtotal * (discountPercent / 100m), 2);
                existing.Discount = discountAmount;
                existing.Total = Math.Max(0, existing.Subtotal - discountAmount);
                existing.PaymentConditions = $"Pix à vista com 5% adicional (R$ {existing.Total * 0.95m:N2}) ou até 10x sem juros no cartão.";
                existing.MessageForUser = $"Desconto especial de {discountPercent}% aplicado! Novo total: R$ {existing.Total:N2}.";
                return existing;
            }
        }

        return new QuoteResult
        {
            QuoteId = quoteId,
            MessageForUser = $"Orçamento {quoteId} atualizado com desconto de {discountPercent}%."
        };
    }

    [Description("Despacha a proposta comercial formal por email ou WhatsApp para o cliente.")]
    public static async Task<bool> SendQuote(
        [Description("Identificador do orçamento gerado")] string quoteId,
        [Description("Canal de envio: 'email' ou 'whatsapp'")] string channel,
        [Description("Destinatário (email ou número de telefone)")] string recipient,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Despachando orçamento {quoteId} via {channel} para {recipient}");
        await Task.Delay(100, cancellationToken);
        return true;
    }
}
