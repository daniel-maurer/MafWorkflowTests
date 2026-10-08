namespace SalesWorkflow.Models;

public sealed class SalesRecordInput
{
    public object? SourceData { get; set; }

    public static SalesRecordInput From(object? data) => new() { SourceData = data };
}
