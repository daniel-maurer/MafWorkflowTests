namespace MafWorkflow.Worker.Core;

public class WorkflowConfigurationBase
{
    public string AzureOpenAiEndpoint { get; set; } = string.Empty;
    public string AzureOpenAiDeploymentName { get; set; } = string.Empty;
    public string BffBaseUrl { get; set; } = string.Empty;
    public string WorkerId { get; set; } = string.Empty;

    public static void LoadBaseEnvironment(WorkflowConfigurationBase config, string defaultWorkerId = "maf-worker-01")
    {
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable is not set.");
        }

        config.AzureOpenAiEndpoint = endpoint;
        config.AzureOpenAiDeploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o-mini";
        config.BffBaseUrl = Environment.GetEnvironmentVariable("BFF_BASE_URL") ?? Environment.GetEnvironmentVariable("WORKFLOW_BFF_BASE_URL") ?? "http://localhost:5089/hubs/maf";
        config.WorkerId = Environment.GetEnvironmentVariable("WORKFLOW_WORKER_ID") ?? defaultWorkerId;
    }

    public virtual void Validate()
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
