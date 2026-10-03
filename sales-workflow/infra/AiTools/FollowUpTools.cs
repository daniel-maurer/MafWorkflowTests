using System.ComponentModel;
using SalesWorkflow.Models;
using SalesWorkflow.Services;

namespace SalesWorkflow.AiTools;

public static class FollowUpTools
{
    public static SalesAdminClient? AdminClient { get; set; }
    public static ISalesUserInteractor? Interactor { get; set; }

    [Description("Agenda um retorno programado ou lembrete de atendimento para o cliente no banco de dados comercial.")]
    public static async Task<FollowUpResult> ScheduleFollowUp(
        [Description("Identificador do orçamento ou sessão")] string referenceId,
        [Description("Tipo: 'quote_reminder', 'cart_recovery', 'restock_notification'")] string followUpType,
        [Description("Horas a partir de agora para enviar o lembrete")] int hoursFromNow,
        [Description("Canal preferencial: 'whatsapp', 'email' ou 'sms'")] string channel = "whatsapp",
        CancellationToken cancellationToken = default)
    {
        var scheduledTime = DateTimeOffset.UtcNow.AddHours(hoursFromNow);
        var customerId = Interactor?.CurrentCustomer?.Id.ToString() ?? "lead_sales";
        var customerName = Interactor?.CurrentCustomer?.Name ?? "Cliente";

        string messageText = followUpType switch
        {
            "cart_recovery" => $"Olá {customerName}! Vimos que você deixou itens no carrinho. Deseja concluir seu pedido com as condições especiais?",
            "restock_notification" => $"Olá {customerName}! O produto que você consultou voltou ao estoque com condições exclusivas.",
            _ => $"Olá {customerName}! Seu orçamento #{referenceId} continua disponível. Deseja finalizar agora?"
        };

        if (AdminClient != null)
        {
            await AdminClient.CreateFollowUpAsync(
                customerId: customerId,
                module: "sales",
                followUpType: followUpType,
                referenceId: referenceId,
                referenceTitle: $"Orçamento/Sessão #{referenceId} ({customerName})",
                scheduledFor: scheduledTime,
                channel: channel,
                messageText: messageText,
                notes: $"Agendado pelo Sales Assistant Workflow via {channel}",
                ct: cancellationToken
            );
        }

        return new FollowUpResult
        {
            FollowUpScheduled = true,
            ScheduledAt = scheduledTime,
            FollowUpType = followUpType,
            Channel = channel,
            MessageForUser = $"Follow-up comercial cadastrado no sistema para {scheduledTime:dd/MM/yyyy HH:mm} via {channel}."
        };
    }
}
