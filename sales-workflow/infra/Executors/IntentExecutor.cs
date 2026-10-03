using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using SalesWorkflow.Models;
using SalesWorkflow.Services;

namespace SalesWorkflow.Executors;

internal sealed class IntentExecutor : Executor<string, IntentResult>
{
    private readonly AIAgent _intentAgent;
    private readonly IUserInteractor _userInteractor;
    private readonly SalesAdminClient _salesAdminClient;

    public IntentExecutor(
        AIAgent intentAgent,
        IUserInteractor userInteractor,
        SalesAdminClient salesAdminClient) : base("IntentExecutor")
    {
        _intentAgent = intentAgent;
        _userInteractor = userInteractor;
        _salesAdminClient = salesAdminClient;
    }

    public override async ValueTask<IntentResult> HandleAsync(
        string userMessage,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[IntentExecutor] Recebida mensagem do usuário: {userMessage}");

        // 1. Identificação / Resolução progressiva do cliente
        CustomerInfo? currentCustomer = null;
        if (_userInteractor is ISalesUserInteractor salesUi)
        {
            currentCustomer = salesUi.CurrentCustomer;
        }

        if (currentCustomer == null)
        {
            currentCustomer = await context.ReadStateAsync<CustomerInfo>(Constants.CustomerDataKey, Constants.SalesStateScope);
        }

        var history = await context.ReadStateAsync<List<ChatMessage>>(
            Constants.InteractionHistoryKey,
            Constants.SalesStateScope) ?? [];

        // Se o cliente ainda não foi identificado, solicita o nome cordialmente para iniciar o cadastro mínimo
        if (currentCustomer == null)
        {
            await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);
            var askName = "Olá! Seja muito bem-vindo à nossa loja! Para começarmos o seu atendimento personalizado, como posso te chamar?";
            var nameAnswer = await _userInteractor.GetUserResponseAsync(
                askName,
                "intent",
                audience: MessageAudience.Both,
                cancellationToken: cancellationToken);

            if (!string.IsNullOrWhiteSpace(nameAnswer))
            {
                var extractedName = ExtractCustomerName(nameAnswer);
                var created = await _salesAdminClient.CreateCustomerAsync(new CreateCustomerRequest
                {
                    Name = extractedName
                }, cancellationToken);

                currentCustomer = created ?? new CustomerInfo { Name = extractedName };
                if (_userInteractor is ISalesUserInteractor si)
                {
                    si.CurrentCustomer = currentCustomer;
                }

                await context.QueueStateUpdateAsync(Constants.CustomerIdKey, currentCustomer.Id, Constants.SalesStateScope);
                await context.QueueStateUpdateAsync(Constants.CustomerDataKey, currentCustomer, Constants.SalesStateScope);
                await _userInteractor.PublishTraceAsync($"Cliente registrado no sistema: {currentCustomer.Name} (ID: {currentCustomer.Id})", "success", cancellationToken);

                var isStartToken = userMessage.StartsWith("__START_");
                var cleanInitial = userMessage.Trim().ToLowerInvariant();
                var isInitialGreeting = cleanInitial is "oi" or "olá" or "ola" or "bom dia" or "boa tarde" or "boa noite" or "opa" or "salve" || cleanInitial.StartsWith("oi ") || cleanInitial.StartsWith("olá ");

                if (isStartToken || isInitialGreeting)
                {
                    var nextPrompt = $"Prazer em te conhecer, {currentCustomer.Name}! Como posso te ajudar hoje? Qual produto ou categoria você procura?";
                    var productAnswer = await _userInteractor.GetUserResponseAsync(
                        nextPrompt,
                        "intent",
                        audience: MessageAudience.Both,
                        cancellationToken: cancellationToken);

                    userMessage = string.IsNullOrWhiteSpace(productAnswer) ? nameAnswer : productAnswer;
                }
                else
                {
                    await _userInteractor.SendUserResponseAsync(
                        $"Prazer em te conhecer, {currentCustomer.Name}! Já estou analisando as melhores opções sobre: \"{userMessage}\"...",
                        "intent",
                        audience: MessageAudience.Both,
                        cancellationToken: cancellationToken);
                }
            }
        }
        else if (userMessage.StartsWith("__START_"))
        {
            await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);
            var welcomeBack = $"Olá, {currentCustomer.Name}! Que bom ter você de volta à nossa loja. Como posso te ajudar hoje? Qual produto você procura?";
            var productAnswer = await _userInteractor.GetUserResponseAsync(
                welcomeBack,
                "intent",
                audience: MessageAudience.Both,
                cancellationToken: cancellationToken);

            userMessage = string.IsNullOrWhiteSpace(productAnswer) ? "produtos" : productAnswer;
        }

        if (currentCustomer != null)
        {
            await context.QueueStateUpdateAsync(Constants.CustomerIdKey, currentCustomer.Id, Constants.SalesStateScope);
            await context.QueueStateUpdateAsync(Constants.CustomerDataKey, currentCustomer, Constants.SalesStateScope);

            if (history.Count == 0 || !history.Any(m => m.Role == ChatRole.System))
            {
                var addressSummary = currentCustomer.Addresses.Count > 0
                    ? string.Join("; ", currentCustomer.Addresses.Select(a => $"{a.Street}, {a.Number} - {a.City}/{a.State}"))
                    : "Nenhum endereço cadastrado";

                history.Insert(0, new ChatMessage(
                    ChatRole.System,
                    $"Contexto do cliente identificado: Nome: {currentCustomer.Name} | Email: {currentCustomer.Email} | Telefone: {currentCustomer.Phone ?? "não informado"} | Endereços: {addressSummary}. Trate sempre o cliente pelo nome com gentileza e profissionalismo."));
            }
        }

        await _userInteractor.SetAgentTypingAsync("Analisando sua solicitação comercial...", true, cancellationToken);
        await _userInteractor.PublishAgentStateAsync("intent", "active", "Running", cancellationToken);
        await _userInteractor.PublishContextAsync(
            "analyzing-intent",
            "Assistente Comercial",
            currentCustomer != null ? $"Atendendo: {currentCustomer.Name}" : "Identificando intenção e termos do produto...",
            "intent",
            false,
            cancellationToken);

        if (!userMessage.StartsWith("__START_") && !history.Any(m => m.Role == ChatRole.User && m.Text == userMessage))
        {
            history.Add(new ChatMessage(ChatRole.User, userMessage));
        }

        IntentResult? intentResult = null;
        int attempts = 0;

        while (attempts <= Constants.MaxClarificationAttempts)
        {
            var response = await _intentAgent.RunAsync(history, cancellationToken: cancellationToken);

            if (!AgentResponseParser.TryDeserializeAgentResponse(response.Text, out intentResult) || intentResult is null)
            {
                Logger.LogWarning($"[IntentExecutor] Falha na desserialização de IntentResult. Tentativa {attempts + 1}");
                intentResult = new IntentResult
                {
                    IsUnderstood = true,
                    Intent = "product_search",
                    Summary = userMessage,
                    ExtractedProductQuery = userMessage,
                    RequiresHuman = false
                };
                break;
            }

            if (intentResult.IsUnderstood)
            {
                break;
            }

            attempts++;
            if (attempts > Constants.MaxClarificationAttempts)
            {
                intentResult.IsUnderstood = true;
                break;
            }

            // Precisa de clarificação
            var clarificationPrompt = string.IsNullOrWhiteSpace(intentResult.QuestionForUser)
                ? "Como posso te ajudar com nossos produtos hoje?"
                : intentResult.QuestionForUser;

            await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);
            var userClarification = await _userInteractor.GetUserResponseAsync(
                clarificationPrompt,
                "intent",
                audience: MessageAudience.Both,
                cancellationToken: cancellationToken);

            history.Add(new ChatMessage(ChatRole.Assistant, clarificationPrompt));
            history.Add(new ChatMessage(ChatRole.User, userClarification));
            await _userInteractor.SetAgentTypingAsync("Refinando entendimento...", true, cancellationToken);
        }

        intentResult ??= new IntentResult { IsUnderstood = true, Intent = "product_search", Summary = userMessage };

        await context.QueueStateUpdateAsync(Constants.InteractionHistoryKey, history, Constants.SalesStateScope);
        await context.QueueStateUpdateAsync(Constants.IntentKey, intentResult.Intent, Constants.SalesStateScope);

        await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);
        await _userInteractor.PublishAgentStateAsync("intent", "done", "Done", cancellationToken);
        await _userInteractor.PublishTraceAsync(
            $"Intenção identificada: {intentResult.Intent} (Sentimento: {intentResult.CustomerSentiment})",
            "info",
            cancellationToken);

        // Feedback no painel de atendimento/telemetria
        await _userInteractor.SendUserResponseAsync(
            $"[Triagem Comercial] Intenção: {intentResult.Intent} | Busca: '{intentResult.ExtractedProductQuery}'",
            "intent",
            audience: MessageAudience.Attendant,
            cancellationToken: cancellationToken);

        await context.YieldOutputAsync(intentResult, cancellationToken);
        return intentResult;
    }

    private static string ExtractCustomerName(string input)
    {
        var text = input.Trim().TrimEnd('.', '!', '?');
        var prefixes = new[]
        {
            "meu nome é", "meu nome e", "sou o", "sou a", "me chamo",
            "pode me chamar de", "aqui é o", "aqui é a", "aqui e o", "aqui e a"
        };
        foreach (var prefix in prefixes)
        {
            var idx = text.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var remainder = text[(idx + prefix.Length)..].Trim();
                if (!string.IsNullOrWhiteSpace(remainder))
                {
                    return char.ToUpper(remainder[0]) + remainder[1..];
                }
            }
        }
        return char.ToUpper(text[0]) + text[1..];
    }
}
