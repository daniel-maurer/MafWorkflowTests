using System.ComponentModel;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace SupportWorkflow;

/// <summary>
/// Tools for the frequent problem detection agent to access and search known issues.
/// </summary>
public static class FrequentProblemTools
{
    /// <summary>
    /// Retrieves known issues that match the provided keywords from the knowledge base.
    /// </summary>
    /// <param name="keyWords">List of keywords to search for known issues. Each keyword is matched against issue keywords.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of matching KnownIssue objects</returns>
    [Description("Get the known issues for a given set of keywords in pt-BR.")]
    public static async Task<List<KnownIssue>> GetKnownIssuesAsync(
        [Description("The keywords to search for known issues. Keyword is a unique word.")] List<string> keyWords,
        CancellationToken cancellationToken = default)
    {
        string knownIssuesPath = "know_issues.json";

        if (!File.Exists(knownIssuesPath))
        {
            throw new FileNotFoundException("File known_issues.json not found.", knownIssuesPath);
        }

        try
        {
            var jsonContent = await File.ReadAllTextAsync(knownIssuesPath, cancellationToken);

            var listKnownIssues = JsonSerializer.Deserialize<List<KnownIssue>>(jsonContent) ?? [];

            var result = listKnownIssues.Where(issue => 
                issue.Keywords.Any(kw => keyWords.Contains(kw, StringComparer.OrdinalIgnoreCase))).ToList();

            return result;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse known_issues.json: {ex.Message}", ex);
        }
    }
}