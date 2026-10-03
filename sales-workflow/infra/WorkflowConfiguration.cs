using MafWorkflow.Worker.Core;

namespace SalesWorkflow;

public class WorkflowConfiguration : WorkflowConfigurationBase
{
    public string ProductCatalogPath { get; set; } = string.Empty;
    public string SalesAdminBaseUrl { get; set; } = "http://localhost:5100/api";

    public static WorkflowConfiguration FromEnvironment()
    {
        var config = new WorkflowConfiguration();
        LoadBaseEnvironment(config, "maf-sales-worker-01");
        config.ProductCatalogPath = Environment.GetEnvironmentVariable("PRODUCT_CATALOG_PATH") ?? "product_catalog.json";
        config.SalesAdminBaseUrl = Environment.GetEnvironmentVariable("SALES_ADMIN_API_URL") ?? "http://localhost:5100/api";
        return config;
    }

    public override void Validate()
    {
        base.Validate();
        if (!string.IsNullOrWhiteSpace(SalesAdminBaseUrl) && !Uri.TryCreate(SalesAdminBaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException($"SalesAdminBaseUrl '{SalesAdminBaseUrl}' is not a valid URI.");
        }
    }
}
