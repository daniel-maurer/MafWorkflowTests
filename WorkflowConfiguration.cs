namespace SupportWorkflow;

/// <summary>
/// Configuration for the support workflow system.
/// Loads settings from environment variables or uses defaults.
/// </summary>
public class WorkflowConfiguration
{
    public string AzureOpenAiEndpoint { get; set; } = string.Empty;
    public string AzureOpenAiDeploymentName { get; set; } = string.Empty;
    public string KnownIssuesPath { get; set; } = string.Empty;

    /// <summary>
    /// Loads configuration from environment variables.
    /// </summary>
    /// <returns>Configured WorkflowConfiguration instance</returns>
    /// <throws>InvalidOperationException if required configuration is missing</throws>
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
            KnownIssuesPath = Environment.GetEnvironmentVariable("KNOWN_ISSUES_PATH") ?? "know_issues.json"
        };
    }

    /// <summary>
    /// Validates that the configuration is valid.
    /// </summary>
    /// <throws>InvalidOperationException if configuration is invalid</throws>
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
    }
}
