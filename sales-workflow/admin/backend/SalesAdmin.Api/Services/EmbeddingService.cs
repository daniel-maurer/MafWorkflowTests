using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using Pgvector;
using SalesAdmin.Api.Entities;

namespace SalesAdmin.Api.Services;

public sealed class EmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>>? _generator;
    private const int EmbeddingDimensions = 1536;

    public EmbeddingService(IEmbeddingGenerator<string, Embedding<float>>? generator = null)
    {
        _generator = generator;
    }

    public async Task<Vector> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new Vector(new float[EmbeddingDimensions]);
        }

        if (_generator is not null)
        {
            try
            {
                var embeddings = await _generator.GenerateAsync([text], cancellationToken: cancellationToken);
                var first = embeddings.FirstOrDefault();
                if (first is not null)
                {
                    return new Vector(first.Vector.ToArray());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EmbeddingService] Falha ao gerar embedding via Azure OpenAI: {ex.Message}. Utilizando gerador local determinístico.");
            }
        }

        // Fallback local determinístico: gera vetor normalizado de 1536 dimensões via SHA256 do texto
        return GenerateDeterministicVector(text);
    }

    public static string BuildProductEmbeddingText(Product p)
    {
        var categoryName = p.Category?.Name ?? string.Empty;
        var tags = p.Tags.Count > 0 ? string.Join(" ", p.Tags) : string.Empty;
        return $"{p.Name} {p.Description} {p.Brand} {categoryName} {tags} {p.Color} {p.Size}".Trim();
    }

    public static string BuildDiscountEmbeddingText(Discount d)
    {
        var payments = d.PaymentMethods.Count > 0 ? string.Join(" ", d.PaymentMethods) : string.Empty;
        return $"{d.Name} {d.Description} {d.Code} {d.DiscountType} {d.DiscountValue} {payments}".Trim();
    }

    public static string BuildCustomerEmbeddingText(Customer c)
    {
        return $"{c.Name} {c.Email} {c.Phone} {c.DocumentNumber} {c.CustomerType}".Trim();
    }

    private static Vector GenerateDeterministicVector(string text)
    {
        var vector = new float[EmbeddingDimensions];
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        var seed = BitConverter.ToInt32(hash, 0);
        var random = new Random(seed);

        double sumSquares = 0;
        for (int i = 0; i < EmbeddingDimensions; i++)
        {
            var val = (float)(random.NextDouble() * 2 - 1);
            vector[i] = val;
            sumSquares += val * val;
        }

        var norm = (float)Math.Sqrt(sumSquares);
        if (norm > 0)
        {
            for (int i = 0; i < EmbeddingDimensions; i++)
            {
                vector[i] /= norm;
            }
        }

        return new Vector(vector);
    }
}
