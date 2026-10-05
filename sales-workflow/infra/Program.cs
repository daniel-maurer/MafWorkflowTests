using DotNetEnv;
using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using SalesWorkflow.Services;

namespace SalesWorkflow;

public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            Env.Load();

            var configuration = WorkflowConfiguration.FromEnvironment();
            configuration.Validate();

            var endpoint = new Uri(configuration.AzureOpenAiEndpoint);
            var deploymentName = configuration.AzureOpenAiDeploymentName;

            var chatClient = new AzureOpenAIClient(endpoint, new AzureCliCredential())
                .GetChatClient(deploymentName).AsIChatClient();

            var salesAdminClient = new SalesAdminClient(configuration.SalesAdminBaseUrl);

            var instructionCache = new InstructionCache(salesAdminClient);
            for (int i = 1; i <= 15; i++)
            {
                try
                {
                    await instructionCache.RefreshAsync();
                    break;
                }
                catch (Exception ex) when (i < 15)
                {
                    Console.WriteLine($"[Sales Workflow Worker] Aguardando Sales Admin API ({configuration.SalesAdminBaseUrl})... tentativa {i}/15: {ex.Message}");
                    await Task.Delay(2000);
                }
            }

            var bffClient = new BffWorkflowClient(
                configuration,
                chatClient,
                salesAdminClient,
                instructionCache,
                interactor => WorkflowFactory.BuildSalesWorkflow(chatClient, interactor, salesAdminClient, instructionCache));

            for (int i = 1; i <= 15; i++)
            {
                try
                {
                    await bffClient.StartAsync();
                    break;
                }
                catch (Exception ex) when (i < 15)
                {
                    Console.WriteLine($"[Sales Workflow Worker] Aguardando BFF Hub ({configuration.BffBaseUrl})... tentativa {i}/15: {ex.Message}");
                    await Task.Delay(2000);
                }
            }

            Console.WriteLine($"[Sales Workflow Worker] Conectado ao BFF em {configuration.BffBaseUrl}");
            Console.WriteLine($"[Sales Workflow Worker] Registrado para o workflow: 'sales-assistant'");
            await Task.Delay(Timeout.Infinite);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"Configuration Error: {ex.Message}");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal Error: {ex.GetType().Name}: {ex.Message}");
            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                Console.Error.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
            Environment.Exit(1);
        }
    }
}
