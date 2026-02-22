using DotNetEnv;
using Azure.Identity;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;

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
            
            var workflow = WorkflowFactory.BuildWorkflow(chatClient);

            Console.WriteLine("Welcome to Support Workflow. Type your request below:");
            await using StreamingRun handle = await InProcessExecution.StreamAsync(workflow, "Como posso ajudar?");
            await foreach (WorkflowEvent evt in handle.WatchStreamAsync())
            {
                // Only print WorkflowOutputEvent if it contains a ResolutionResult or similar final result
                // Skip intermediate messages to avoid duplication
                if (evt is WorkflowOutputEvent outputEvent)
                {
                    var eventData = outputEvent.Data;
                    // Only print if this is a final result (ResolutionResult or HumanSupportResult)
                    // Skip intermediate JSON responses and intermediate outputs
                    if (eventData is ResolutionResult or FrequentProblemResult or TriageResult)
                    {
                        // These are already handled by the executors with Logger.OutputUser
                        continue;
                    }
                }
                
                switch (evt)
                {
                    case RequestInfoEvent requestInputEvt:
                        ExternalResponse response = HandleExternalRequest(requestInputEvt.Request);
                        await handle.SendResponseAsync(response);
                        continue;
                    case ExecutorCompletedEvent executorComplete:
                        //Console.WriteLine($"{executorComplete.ExecutorId}: {executorComplete.Data}");
                        break;
                    case WorkflowOutputEvent workflowOutput:
                        //Console.WriteLine($"Workflow '{workflowOutput.SourceId}' outputs: {workflowOutput.Data}");
                        break;
                }
            }
            
            Console.WriteLine("Thank you for using Support Workflow.");
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