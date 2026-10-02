namespace VetWorkflow;

public class WorkflowConfiguration
{
    public string AzureOpenAiEndpoint { get; set; } = string.Empty;
    public string AzureOpenAiDeploymentName { get; set; } = string.Empty;
    public string BffBaseUrl { get; set; } = string.Empty;
    public string WorkerId { get; set; } = string.Empty;

    public static WorkflowConfiguration FromEnvironment()
    {
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable is not set.");
        }

        return new()
        {
            AzureOpenAiEndpoint = endpoint,
            AzureOpenAiDeploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o-mini",
            BffBaseUrl = Environment.GetEnvironmentVariable("BFF_BASE_URL") ?? Environment.GetEnvironmentVariable("WORKFLOW_BFF_BASE_URL") ?? "http://localhost:5089/hubs/maf",
            WorkerId = Environment.GetEnvironmentVariable("WORKFLOW_WORKER_ID") ?? "maf-vet-worker-01"
        };
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AzureOpenAiEndpoint))
        {
            throw new InvalidOperationException("AzureOpenAiEndpoint is not configured.");
        }

        if (!Uri.TryCreate(AzureOpenAiEndpoint, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException($"AzureOpenAiEndpoint '{AzureOpenAiEndpoint}' is not a valid URI.");
        }

        if (!string.IsNullOrWhiteSpace(BffBaseUrl) && !Uri.TryCreate(BffBaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException($"BffBaseUrl '{BffBaseUrl}' is not a valid URI.");
        }
    }
}
