using System.Text.RegularExpressions;
using SalesWorkflow.Models;

namespace SalesWorkflow.Utilities;

public static class AddressParser
{
    private static readonly Regex CepRegex = new(@"\b\d{5}-?\d{3}\b", RegexOptions.Compiled);
    private static readonly Regex StateRegex = new(@"\b(AC|AL|AP|AM|BA|CE|DF|ES|GO|MA|MT|MS|MG|PA|PB|PR|PE|PI|RJ|RN|RS|RO|RR|SC|SP|SE|TO)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NumberRegex = new(@"\b(?:nº|n[oº]?|numero)?\s*(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static CreateAddressRequest Parse(string rawAddress)
    {
        if (string.IsNullOrWhiteSpace(rawAddress))
        {
            return new CreateAddressRequest
            {
                Street = "Endereço a confirmar",
                Number = "S/N",
                City = "São Paulo",
                State = "SP",
                ZipCode = "01000-000"
            };
        }

        var text = rawAddress.Trim();

        // 1. Extrair CEP
        string zipCode = "01000-000";
        var cepMatch = CepRegex.Match(text);
        if (cepMatch.Success)
        {
            zipCode = cepMatch.Value.Replace("-", "");
            if (zipCode.Length == 8)
            {
                zipCode = $"{zipCode[..5]}-{zipCode[5..]}";
            }
            text = text.Replace(cepMatch.Value, "").Trim();
        }

        // 2. Extrair Estado (UF)
        string state = "SP";
        var stateMatch = StateRegex.Match(text);
        if (stateMatch.Success)
        {
            state = stateMatch.Value.ToUpperInvariant();
        }

        // 3. Separar por vírgula ou traço se houver
        var parts = text.Split(new[] { ',', ';', '-' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        string street = parts.Length > 0 ? parts[0] : text;
        string number = "S/N";
        string neighborhood = "Centro";
        string city = "São Paulo";
        string? complement = null;

        // Tentar extrair número da rua se estiver nela
        var numMatch = NumberRegex.Match(street);
        if (numMatch.Success && parts.Length <= 2)
        {
            number = numMatch.Groups[1].Value;
            street = street.Replace(numMatch.Value, "").Trim();
        }
        else if (parts.Length > 1 && int.TryParse(Regex.Match(parts[1], @"\d+").Value, out _))
        {
            number = Regex.Match(parts[1], @"\d+").Value;
        }

        if (parts.Length > 2)
        {
            neighborhood = parts[2];
        }

        if (parts.Length > 3)
        {
            city = parts[3];
            // Remove sigla do estado se estiver na cidade
            var ufMatchInCity = StateRegex.Match(city);
            if (ufMatchInCity.Success)
            {
                city = city.Replace(ufMatchInCity.Value, "").Trim('/', ' ', '-');
            }
        }

        if (parts.Length > 4)
        {
            complement = parts[4];
        }

        if (string.IsNullOrWhiteSpace(city)) city = "São Paulo";
        if (string.IsNullOrWhiteSpace(street)) street = "Rua informada";

        return new CreateAddressRequest
        {
            Label = "Entrega",
            Street = street,
            Number = number,
            Neighborhood = neighborhood,
            City = city,
            State = state,
            ZipCode = zipCode,
            Complement = complement,
            IsDefault = true
        };
    }
}
