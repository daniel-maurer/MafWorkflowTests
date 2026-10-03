using System.ComponentModel;
using SalesWorkflow.Models;
using SalesWorkflow.Services;

namespace SalesWorkflow.AiTools;

public sealed class CustomerTools
{
    private readonly SalesAdminClient _client;

    public CustomerTools(SalesAdminClient client)
    {
        _client = client;
    }

    [Description("Busca as informações e endereços cadastrados de um cliente através do seu ID, CPF, e-mail, telefone ou nome.")]
    public async Task<CustomerInfo?> FindCustomer(
        [Description("Identificador do cliente (ID, CPF, E-mail, Telefone ou Nome)")] string identifier,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL Customer] Buscando cliente por: {identifier}");
        return await _client.FindCustomerAsync(identifier, cancellationToken);
    }

    [Description("Cadastra um novo cliente ou atualiza os dados básicos de um cliente existente (nome, e-mail, telefone, CPF).")]
    public async Task<CustomerInfo?> RegisterOrUpdateCustomer(
        [Description("Nome do cliente")] string name,
        [Description("E-mail do cliente (opcional)")] string? email = null,
        [Description("Telefone ou WhatsApp do cliente (opcional)")] string? phone = null,
        [Description("Número do documento (CPF ou CNPJ)")] string? documentNumber = null,
        [Description("ID do cliente caso já exista no sistema (opcional)")] string? customerId = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(customerId))
        {
            Logger.LogInfo($"[TOOL Customer] Atualizando cliente existente ID: {customerId}");
            var updateReq = new UpdateCustomerRequest
            {
                Name = name,
                Email = email,
                Phone = phone,
                DocumentNumber = documentNumber
            };
            return await _client.UpdateCustomerAsync(customerId, updateReq, cancellationToken);
        }

        Logger.LogInfo($"[TOOL Customer] Cadastrando novo cliente no sistema: {name}");
        var createReq = new CreateCustomerRequest
        {
            Name = name,
            Email = email,
            Phone = phone,
            DocumentNumber = documentNumber
        };
        return await _client.CreateCustomerAsync(createReq, cancellationToken);
    }

    [Description("Adiciona um endereço de entrega ao cadastro do cliente no sistema.")]
    public async Task<CustomerAddressInfo?> AddCustomerAddress(
        [Description("ID do cliente")] string customerId,
        [Description("Logradouro (Rua, Avenida, etc.)")] string street,
        [Description("Número")] string number,
        [Description("Bairro")] string neighborhood,
        [Description("Cidade")] string city,
        [Description("Estado (sigla UF, ex: SP, RJ)")] string state,
        [Description("CEP (apenas números ou formatado)")] string zipCode,
        [Description("Complemento (Apto, Bloco, Casa, etc.)")] string? complement = null,
        [Description("Rótulo do endereço, ex: 'Residencial', 'Comercial'")] string label = "Entrega",
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL Customer] Adicionando endereço de entrega para cliente: {customerId} -> {street}, {number}");
        var addressReq = new CreateAddressRequest
        {
            Label = label,
            Street = street,
            Number = number,
            Neighborhood = neighborhood,
            City = city,
            State = state,
            ZipCode = zipCode,
            Complement = complement,
            IsDefault = true
        };
        return await _client.AddCustomerAddressAsync(customerId, addressReq, cancellationToken);
    }
}
