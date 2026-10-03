using DotNetEnv;
using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;

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

            var bffClient = new BffWorkflowClient(
                configuration,
                chatClient,
                interactor => WorkflowFactory.BuildSalesWorkflow(chatClient, interactor));

            await bffClient.StartAsync();
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
