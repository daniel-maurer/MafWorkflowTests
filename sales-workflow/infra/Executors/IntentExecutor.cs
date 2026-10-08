using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using SalesWorkflow.Models;
using SalesWorkflow.Services;

namespace SalesWorkflow.Executors;

internal sealed class IntentExecutor : Executor<string, IntentResult>
{
    private readonly AIAgent _intentAgent;
    private readonly IChatClient _chatClient;
    private readonly IUserInteractor _userInteractor;
    private readonly SalesAdminClient _salesAdminClient;

    public IntentExecutor(
        AIAgent intentAgent,
        IChatClient chatClient,
        IUserInteractor userInteractor,
        SalesAdminClient salesAdminClient) : base("IntentExecutor")
    {
        _intentAgent = intentAgent;
        _chatClient = chatClient;
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

        var isSystemStart = userMessage.StartsWith("__START_");

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
            }
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
                    $"Contexto do cliente identificado: Nome: {currentCustomer.Name} | Email: {currentCustomer.Email} | Telefone: {currentCustomer.Phone ?? "não informado"} | Endereços: {addressSummary} | Observações/Highlights: {currentCustomer.Notes ?? "nenhuma"}. Trate sempre o cliente pelo nome com gentileza e profissionalismo."));
            }
        }

        bool isCasualGreeting = IsCasualGreeting(userMessage);

        // Se o cliente mandou apenas uma saudação casual (ou mensagem de início) e temos highlights, respondemos quebrando o gelo e perguntando como ajudar
        if (isCasualGreeting || isSystemStart)
        {
            await _userInteractor.SetAgentTypingAsync("Preparando atendimento...", true, cancellationToken);
            var greetingMessage = string.Empty;
            
            if (currentCustomer != null && !string.IsNullOrWhiteSpace(currentCustomer.Notes))
            {
                var greetingPrompt = $@"Você é um(a) atendente simpático(a), atencioso(a) e acolhedor(a) na loja MAF.
O cliente {currentCustomer.Name} acabou de mandar: '{userMessage}'.
Contexto/assunto de conversas anteriores registrado nas observações do cliente:
'{currentCustomer.Notes}'

SUA MISSÃO:
Cumprimente o cliente pelo nome com simpatia e faça uma pergunta rápida e descontraída sobre o assunto ou ocasião citado nas observações (ex: se fala de praia, pergunte casualmente 'E aí, conseguiu curtir a praia?' ou 'Como foi o passeio na praia?').

DIRETRIZES FUNDAMENTAIS:
1. OBRIGATÓRIO: Faça uma pergunta rápida quebrando o gelo sobre o assunto das observações anteriores (ex: praia, lazer), demonstrando atenção e proximidade.
2. NUNCA soe robótico ou artificial. JAMAIS use frases invasivas ou duras como 'vi no meu sistema que...', 'sei que você gosta de...', 'que delícia saber que você curte...'. Fale com a espontaneidade de um atendente amigo que lembra do cliente.
3. Finalize perguntando como pode ajudar hoje com nossos produtos.
4. Extensão: exatamente 2 frases curtas e diretas.
   Exemplo ideal:
   'Oi {currentCustomer.Name}! Tudo bem? E aí, conseguiu curtir a praia? Como posso te ajudar hoje?'

Responda APENAS com a mensagem final que será enviada ao cliente.";
                try
                {
                    var greetingResponse = await _chatClient.GetResponseAsync(greetingPrompt, cancellationToken: cancellationToken);
                    greetingMessage = greetingResponse.Text?.Trim();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[IntentExecutor] Falha ao gerar saudação dinâmica: {ex.Message}");
                }
            }

            if (string.IsNullOrWhiteSpace(greetingMessage))
            {
                greetingMessage = $"Olá{(currentCustomer != null ? " " + currentCustomer.Name : "")}! Tudo bem? Como posso te ajudar com nossos produtos hoje?";
            }

            history.Add(new ChatMessage(ChatRole.User, userMessage));
            history.Add(new ChatMessage(ChatRole.Assistant, greetingMessage));
            await context.QueueStateUpdateAsync(Constants.InteractionHistoryKey, history, Constants.SalesStateScope);

            await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);
            userMessage = await _userInteractor.GetUserResponseAsync(
                greetingMessage,
                "intent",
                audience: MessageAudience.Both,
                cancellationToken: cancellationToken);
        }

        // Se o cliente responder à saudação com outra cortesia ou small talk (ex: "tudo bem e você?", "tudo certo e contigo?")
        int pleasantryAttempts = 0;
        while (IsSmallTalkOrPleasantry(userMessage) && pleasantryAttempts < 2)
        {
            pleasantryAttempts++;
            var customerNamePart = currentCustomer != null ? $" {currentCustomer.Name}" : "";
            var pleasantryReply = $"Tudo ótimo por aqui também{customerNamePart}, obrigado por perguntar! Como posso te ajudar hoje? Procura algum produto em especial?";

            history.Add(new ChatMessage(ChatRole.User, userMessage));
            history.Add(new ChatMessage(ChatRole.Assistant, pleasantryReply));
            await context.QueueStateUpdateAsync(Constants.InteractionHistoryKey, history, Constants.SalesStateScope);

            await _userInteractor.SetAgentTypingAsync(string.Empty, false, cancellationToken);
            userMessage = await _userInteractor.GetUserResponseAsync(
                pleasantryReply,
                "intent",
                audience: MessageAudience.Both,
                cancellationToken: cancellationToken);
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

        if (!history.Any(m => m.Role == ChatRole.User && m.Text == userMessage))
        {
            history.Add(new ChatMessage(ChatRole.User, userMessage));
        }

        IntentResult? intentResult = null;
        int attempts = 0;

        var categories = await _salesAdminClient.GetCategoriesAsync(active: true, ct: cancellationToken);
        var categoriesNames = categories.Count > 0
            ? string.Join(", ", categories.Select(c => c.Name))
            : "Celulares & Smartphones, Informática, Áudio & Vídeo, Periféricos, Vestuário, Calçados, Wearables";

        var activeCampaigns = await _salesAdminClient.GetActiveCampaignsAsync(cancellationToken);
        var campaignInfo = activeCampaigns.Count > 0
            ? string.Join("; ", activeCampaigns.Select(c => $"• {c.Name}: {c.Description}"))
            : "No momento não há campanhas ativas, mas temos ótimos preços em todo o catálogo.";

        while (attempts <= Constants.MaxClarificationAttempts)
        {
            var agentMessages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, $@"INFORMAÇÕES OFICIAIS DA LOJA MAF STORE:
- Departamentos e Categorias: {categoriesNames}.
- Campanhas e Promoções ATIVAS no momento:
{campaignInfo}

DIRETRIZES DE INTENÇÃO E ATENDIMENTO:
1. PERGUNTAS SOBRE PROMOÇÕES, CAMPANHAS OU DESCONTOS (ex: 'tem alguma promoção ou campanha?', 'tem promoção?', 'quais as promoções ativas?', 'tem desconto?'):
   - Marque SEMPRE is_understood = false, intent = 'price_inquiry', extracted_product_query = ''.
   - Em question_for_user, informe COM ENTUSIASMO as nossas campanhas ativas com todas as regras ({campaignInfo}) e pergunte que produto ou categoria o cliente gostaria de ver para aproveitar essas condições!
   - NUNCA dê respostas evasivas dizendo que promoções 'costumam acontecer ao longo do ano'. Cite as campanhas ativas reais!
2. PERGUNTAS SOBRE O QUE A LOJA VENDE OU CATEGORIAS (ex: 'que tipos de produtos vocês vendem?', 'quais tipos possuem?', 'o que vocês vendem?'):
   - Marque is_understood = false, intent = 'product_search', extracted_product_query = ''.
   - Em question_for_user, apresente de forma concisa e amigável nossos departamentos principais e pergunte o que ele gostaria de conhecer.
3. SAUDAÇÕES SIMPLES (ex: 'oi', 'tudo bem?', 'olá', 'boa tarde'):
   - Seja breve, cordial e acolhedor (ex: 'Olá! Tudo bem? Como posso te ajudar hoje?').
   - NUNCA despeje uma lista longa de categorias em saudações simples!
4. PEDIDOS DE SUGESTÃO GERAL, RECOMENDAÇÕES OU DESTAQUES (ex: 'quais são os produtos que você me recomenda?', 'me diga o que vocês têm de legal', 'o que está saindo bem', 'destaques', 'quero gastar'):
   - Marque is_understood = true, intent = 'product_search', e defina extracted_product_query = 'destaques mais vendidos' para que o catálogo busque os produtos em destaque.")
            };
            agentMessages.AddRange(history);

            var response = await _intentAgent.RunAsync(agentMessages, cancellationToken: cancellationToken);

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

            // Se o agente marcou como entendido mas não há produto para buscar no catálogo e não foi solicitada transferência humana
            if (intentResult.IsUnderstood && string.IsNullOrWhiteSpace(intentResult.ExtractedProductQuery) && !intentResult.RequiresHuman)
            {
                var recovered = RecoverProductFromHistory(history);
                if (!string.IsNullOrWhiteSpace(recovered))
                {
                    Logger.LogInfo($"[IntentExecutor] Produto recuperado do histórico da conversa: '{recovered}'");
                    intentResult.ExtractedProductQuery = recovered;
                    intentResult.Intent = "product_search";
                }
                else
                {
                    intentResult.IsUnderstood = false;
                    if (string.IsNullOrWhiteSpace(intentResult.QuestionForUser))
                    {
                        intentResult.QuestionForUser = $"Como posso te ajudar com nossos produtos hoje? Temos opções em {categoriesNames}. Você procura algo específico ou gostaria de sugestões em algum departamento?";
                    }
                }
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
            userMessage = userClarification;
            await _userInteractor.SetAgentTypingAsync("Refinando entendimento...", true, cancellationToken);
        }

        intentResult ??= new IntentResult { IsUnderstood = true, Intent = "product_search", Summary = userMessage };

        if (string.IsNullOrWhiteSpace(intentResult.ExtractedProductQuery) && !intentResult.RequiresHuman)
        {
            var recovered = RecoverProductFromHistory(history);
            if (!string.IsNullOrWhiteSpace(recovered))
            {
                Logger.LogInfo($"[IntentExecutor] Produto recuperado do histórico (pós-loop): '{recovered}'");
                intentResult.ExtractedProductQuery = recovered;
                intentResult.Intent = "product_search";
                intentResult.IsUnderstood = true;
            }
        }

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

    private static bool IsCasualGreeting(string msg)
    {
        return IsSmallTalkOrPleasantry(msg);
    }

    private static bool IsSmallTalkOrPleasantry(string msg)
    {
        if (string.IsNullOrWhiteSpace(msg)) return false;
        var clean = System.Text.RegularExpressions.Regex.Replace(msg.Trim().ToLowerInvariant(), @"[,\.;!\?]+", " ");
        clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\s+", " ").Trim();
        
        var exacts = new HashSet<string>
        {
            "oi", "ola", "olá", "opa", "fala", "e ai", "e aí", "hello", "hi",
            "bom dia", "boa tarde", "boa noite",
            "oi tudo bem", "ola tudo bem", "olá tudo bem", "opa tudo bem",
            "oi tudo bom", "ola tudo bom", "olá tudo bom", "opa tudo bom",
            "oi tudo certo", "ola tudo certo", "olá tudo certo",
            "oi como vai", "ola como vai", "olá como vai",
            "tudo bem", "tudo bom", "tudo certo", "tudo joia", "tudo jóia",
            "tudo otimo", "tudo ótimo", "tudo bem por aqui", "tudo em ordem",
            "tudo bem e voce", "tudo bem e você", "tudo bem e vc", "tudo bem e contigo",
            "tudo bem e com você", "tudo bem e com voce", "tudo bem contigo",
            "tudo certo e voce", "tudo certo e você", "tudo certo e vc", "tudo certo e contigo",
            "tudo certo e com você", "tudo certo e com voce", "tudo certo contigo",
            "tudo bom e você", "tudo bom e voce", "tudo bom e vc", "tudo bom e contigo",
            "tudo joia e você", "tudo joia e vc", "tudo jóia e você", "tudo joia e contigo",
            "como vai", "como vai você", "como vai voce", "como você está", "como voce esta", "como vc ta",
            "e você", "e voce", "e vc", "e contigo", "e com você", "e com voce",
            "beleza", "tranquilo", "suave", "show", "legal", "valeu", "obrigado", "obrigada"
        };

        if (exacts.Contains(clean)) return true;

        bool hasCommercialOrProductIntent = clean.Contains("quero") || clean.Contains("preciso") || clean.Contains("tem ") 
            || clean.Contains("camisa") || clean.Contains("camiseta") || clean.Contains("calca") || clean.Contains("calça") 
            || clean.Contains("bermuda") || clean.Contains("tenis") || clean.Contains("tênis") || clean.Contains("sapato") 
            || clean.Contains("comprar") || clean.Contains("preço") || clean.Contains("preco") || clean.Contains("valor") 
            || clean.Contains("estoque") || clean.Contains("tamanho") || clean.Contains("pedido") || clean.Contains("loja") 
            || clean.Contains("humano") || clean.Contains("atendente") || clean.Contains("vendedor") || clean.Contains("troca")
            || clean.Contains("devolu") || clean.Contains("reclama");

        if (hasCommercialOrProductIntent) return false;

        if (clean.StartsWith("tudo bem") || clean.StartsWith("tudo bom") || clean.StartsWith("tudo certo") 
            || clean.StartsWith("tudo joia") || clean.StartsWith("tudo jóia") || clean.StartsWith("tudo ótimo") 
            || clean.StartsWith("tudo otimo") || clean.StartsWith("como vai") || clean.StartsWith("como você") 
            || clean.StartsWith("como voce") || clean.StartsWith("oi ") || clean.StartsWith("ola ") 
            || clean.StartsWith("olá ") || clean.StartsWith("opa ") || clean.StartsWith("bom dia") 
            || clean.StartsWith("boa tarde") || clean.StartsWith("boa noite"))
        {
            return clean.Length <= 40;
        }

        return false;
    }

    private static string RecoverProductFromHistory(List<ChatMessage> history)
    {
        if (history == null || history.Count == 0) return string.Empty;

        var productKeywords = new[]
        {
            "tênis de corrida", "tenis de corrida", "tênis corrida", "tenis corrida", "tênis esportivo", "tenis esportivo",
            "tênis", "tenis", "sapatênis", "sapatenis", "sapato",
            "camisa polo", "camisa de praia", "camisa praia", "camisa social", "camisa",
            "camiseta", "bermuda", "calça", "calca", "short", "jaqueta", "casaco", "blusão", "blusao"
        };

        for (int i = history.Count - 1; i >= 0; i--)
        {
            var msg = history[i];
            if (msg.Role != ChatRole.User || string.IsNullOrWhiteSpace(msg.Text)) continue;

            var lower = msg.Text.ToLowerInvariant();
            foreach (var kw in productKeywords)
            {
                if (lower.Contains(kw))
                {
                    var clean = msg.Text.Trim();
                    var prefixes = new[] { "quero um ", "quero uma ", "quero ", "preciso de um ", "preciso de uma ", "preciso de ", "gostaria de ", "tem ", "busco um ", "busco uma " };
                    foreach (var p in prefixes)
                    {
                        var idx = clean.IndexOf(p, StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0)
                        {
                            var sub = clean[(idx + p.Length)..].Trim();
                            if (!string.IsNullOrWhiteSpace(sub) && sub.Length <= 40) return sub;
                        }
                    }
                    return kw;
                }
            }
        }

        return string.Empty;
    }
}
