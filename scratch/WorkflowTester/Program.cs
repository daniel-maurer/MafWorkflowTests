using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace WorkflowTester;

public class Program
{
    private static readonly string BffUrl = "http://localhost:5089";
    private static readonly string Token = "mock-token:tester";

    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Workflow Integration Tester: Vale Transporte Scenarios ===");

        // Scenario 1: User asks about vale transporte, direct/escalated human help, payment on 25/05
        Console.WriteLine("\n=======================================================");
        Console.WriteLine("RUNNING SCENARIO 1: Payment on 25/05 (Direct or Escalated Human)");
        Console.WriteLine("=======================================================");
        bool success1 = await RunTestScenarioAsync(
            "Scenario 1 (25/05)",
            "Não recebi meu vale transporte esse mês.",
            "Verifiquei no sistema. O vale transporte será pago no dia 25/05."
        );

        // Scenario 2: User asks generally, matches vale refeição 25/04, rejected -> human support, payment on 25/04
        Console.WriteLine("\n=======================================================");
        Console.WriteLine("RUNNING SCENARIO 2: Rejection of Meal Voucher match -> Human support resolves as Transport Voucher on 25/04");
        Console.WriteLine("=======================================================");
        bool success2 = await RunTestScenarioAsync(
            "Scenario 2 (25/04)",
            "Não recebi meu vale este mês, quando vai ser pago?",
            "Na verdade, o seu vale transporte será pago no dia 25/04."
        );

        // Scenario 3: User asks about childcare voucher, matches known issue, resolves automatically with "Será pago dia 24/05."
        Console.WriteLine("\n=======================================================");
        Console.WriteLine("RUNNING SCENARIO 3: Childcare Voucher (Creche) auto-resolution");
        Console.WriteLine("=======================================================");
        bool success3 = await RunTestScenarioAsync(
            "Scenario 3 (Creche)",
            "Nao recebi meu vale creche",
            "mock-reply-not-needed",
            autoApproveResolution: true
        );

        Console.WriteLine("\n=======================================================");
        if (success1 && success2 && success3)
        {
            Console.WriteLine("[SUCCESS] All test scenarios completed successfully!");
        }
        else
        {
            Console.Error.WriteLine("[ERROR] One or more scenarios failed!");
            Environment.Exit(1);
        }
    }

    private static async Task<bool> RunTestScenarioAsync(string scenarioName, string initialMessage, string humanReplyMessage, bool autoApproveResolution = false)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("MockBearer", Token);

        Console.WriteLine($"[{scenarioName}] Creating support session...");
        var requestBody = new
        {
            workflowId = "support",
            initialMessage = initialMessage
        };
        var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        
        var response = await httpClient.PostAsync($"{BffUrl}/api/workflow-sessions", jsonContent);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            Console.Error.WriteLine($"[{scenarioName}] Failed to create session: {response.StatusCode} - {err}");
            return false;
        }

        var responseString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseString);
        var sessionId = doc.RootElement.GetProperty("sessionId").GetString()!;
        
        Console.WriteLine($"[{scenarioName}] Session ID: {sessionId}");

        // Setup SignalR client to join and listen
        var hubConnection = new HubConnectionBuilder()
            .WithUrl($"{BffUrl}/hubs/workflow", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(Token);
            })
            .WithAutomaticReconnect()
            .Build();

        var tcs = new TaskCompletionSource<bool>();
        bool splitModeTriggered = false;
        bool repliedToTriage = false;
        bool repliedToResolution = false;

        hubConnection.On<string, object>("message", async (sid, payload) =>
        {
            var json = JsonSerializer.Serialize(payload);
            
            try
            {
                using var mdoc = JsonDocument.Parse(json);
                if (mdoc.RootElement.TryGetProperty("text", out var textProp) && mdoc.RootElement.TryGetProperty("senderName", out var senderProp))
                {
                    var sender = senderProp.GetString();
                    var text = textProp.GetString() ?? string.Empty;

                    Console.WriteLine($"[{scenarioName} - SignalR Message] {sender}: {text}");

                    // If Triage Agent asks a clarifying question (contains '?')
                    if (sender == "Triage Agent" && text.Contains("?") && !repliedToTriage)
                    {
                        repliedToTriage = true;
                        Console.WriteLine($"[{scenarioName}] => Triage Agent asked for clarification. Replying...");
                        await Task.Delay(1000);
                        await hubConnection.InvokeAsync("SendUserMessage", sessionId, "Não recebi meu vale transporte este mês.");
                    }
                    
                    // If Resolution Agent asks if the issue was resolved (contains 'resolvido?')
                    if (sender == "Resolution Agent" && text.Contains("resolvido", StringComparison.OrdinalIgnoreCase) && !repliedToResolution)
                    {
                        repliedToResolution = true;
                        string answer = autoApproveResolution ? "sim" : "não";
                        Console.WriteLine($"[{scenarioName}] => Resolution Agent asked if resolved. Replying '{answer}'...");
                        await Task.Delay(1000);
                        await hubConnection.InvokeAsync("SendUserMessage", sessionId, answer);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{scenarioName}] Error processing message callback: {ex.Message}");
            }
        });

        hubConnection.On<string, object>("trace", (sid, payload) =>
        {
            using var tdoc = JsonDocument.Parse(JsonSerializer.Serialize(payload));
            if (tdoc.RootElement.TryGetProperty("title", out var titleProp))
            {
                Console.WriteLine($"[{scenarioName} - SignalR Trace] {titleProp.GetString()}");
            }
        });

        hubConnection.On<string, object>("context", (sid, payload) =>
        {
            using var cdoc = JsonDocument.Parse(JsonSerializer.Serialize(payload));
            if (cdoc.RootElement.TryGetProperty("status", out var statusProp))
            {
                var status = statusProp.GetString();
                Console.WriteLine($"[{scenarioName} - SignalR Context] Status = {status}");
                if (status == "resolved")
                {
                    tcs.TrySetResult(true);
                }
            }
        });

        hubConnection.On<string, bool>("splitMode", async (sid, active) =>
        {
            Console.WriteLine($"[{scenarioName} - SignalR splitMode] Active={active}");
            if (active && !splitModeTriggered)
            {
                splitModeTriggered = true;
                Console.WriteLine($"[{scenarioName}] => Human support is active. Simulating human agent...");
                await Task.Delay(2000);
                
                Console.WriteLine($"[{scenarioName}] => Human agent sending message: '{humanReplyMessage}'");
                await hubConnection.InvokeAsync("SendHumanMessage", sessionId, humanReplyMessage);
                
                await Task.Delay(2000);
                Console.WriteLine($"[{scenarioName}] => Marking solved...");
                try
                {
                    await hubConnection.InvokeAsync("MarkSolved", sessionId);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[{scenarioName}] Error sending MarkSolved: {ex.Message}");
                }
            }
        });

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinSession", sessionId);

        // Wait with a timeout of 60 seconds
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(60));
        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

        await hubConnection.StopAsync();

        if (completedTask == timeoutTask)
        {
            Console.Error.WriteLine($"[{scenarioName}] [ERROR] Run timed out!");
            return false;
        }
        
        Console.WriteLine($"[{scenarioName}] [SUCCESS] Completed successfully!");
        return true;
    }
}
