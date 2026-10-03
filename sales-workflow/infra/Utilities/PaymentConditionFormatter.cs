using SalesWorkflow.Models;

namespace SalesWorkflow.Utilities;

public static class PaymentConditionFormatter
{
    public static string Format(IEnumerable<PaymentConditionInfo> conditions, decimal total)
    {
        var active = conditions.Where(c => c.Active).ToList();
        if (active.Count == 0)
        {
            return "Consulte nossas condições de pagamento com o vendedor.";
        }

        var parts = new List<string>();

        foreach (var c in active)
        {
            switch (c.PaymentMethod.ToLowerInvariant())
            {
                case "pix":
                    if (c.AdditionalDiscount > 0)
                    {
                        var discounted = Math.Round(total * (1 - c.AdditionalDiscount / 100m), 2);
                        parts.Add($"Pix à vista com {c.AdditionalDiscount:0.#}% de desconto (R$ {discounted:N2})");
                    }
                    else
                    {
                        parts.Add($"Pix à vista (R$ {total:N2})");
                    }
                    break;

                case "credit_card":
                    if (c.MaxInstallments > 1)
                    {
                        var installmentVal = Math.Round(total / c.MaxInstallments, 2);
                        var interestText = c.InterestFree ? "sem juros" : $"com juros de {c.InterestRate:0.#}% a.m.";
                        parts.Add($"Cartão de crédito em até {c.MaxInstallments}x de R$ {installmentVal:N2} {interestText}");
                    }
                    else
                    {
                        parts.Add("Cartão de crédito à vista");
                    }
                    break;

                case "boleto":
                    parts.Add("Boleto bancário");
                    break;

                default:
                    parts.Add(c.Name);
                    break;
            }
        }

        return string.Join(" ou ", parts) + ".";
    }
}
