using DotNetEnv;
using Azure.Identity;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.SignalR.Client;
using System.Threading.Tasks;

namespace SupportWorkflow;
public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            Env.Load();
            
            // To disable logging, uncomment the line below:
            // Logger.DisableLogging();
            
            var configuration = WorkflowConfiguration.FromEnvironment();
            configuration.Validate();

            var endpoint = new Uri(configuration.AzureOpenAiEndpoint);
            var deploymentName = configuration.AzureOpenAiDeploymentName;
            
            var chatClient = new AzureOpenAIClient(endpoint, new AzureCliCredential())
                .GetChatClient(deploymentName).AsIChatClient();
            
            var bffClient = new BffWorkflowClient(configuration, chatClient, interactor => WorkflowFactory.BuildWorkflow(chatClient, interactor));
            await bffClient.StartAsync();
            Console.WriteLine($"Connected to BFF at {configuration.BffBaseUrl}");
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
    private static ExternalResponse HandleExternalRequest(ExternalRequest request)
    {
        string prompt = request.DataAs<string>() ?? "Please enter your request:";
        string input = ReadMessageFromConsole(prompt);
        return request.CreateResponse(input);
    }
    private static string ReadMessageFromConsole(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            string? input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input))
            {
                return input;
            }
            Console.WriteLine("Invalid input. Please enter a valid request.");
        }
    }
}