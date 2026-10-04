using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SalesWorkflow.Services;

public class InstructionCache
{
    private readonly SalesAdminClient _adminClient;
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(1);
    private DateTime _lastFetch = DateTime.MinValue;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public InstructionCache(SalesAdminClient adminClient)
    {
        _adminClient = adminClient;
    }

    public async Task RefreshAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (DateTime.UtcNow - _lastFetch < _ttl)
                return;

            var instructions = await _adminClient.GetAgentInstructionsAsync();
            
            _cache.Clear();
            foreach (var inst in instructions.Where(i => i.IsActive))
            {
                var key = $"{inst.WorkflowType}|{inst.AgentRole}";
                _cache[key] = inst.Instructions;
            }

            _lastFetch = DateTime.UtcNow;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public string GetInstruction(string workflowType, string role, string defaultInstruction)
    {
        // Fire and forget refresh se expirou
        if (DateTime.UtcNow - _lastFetch >= _ttl)
        {
            _ = RefreshAsync();
        }

        var globalKey = $"global|global";
        var roleKey = $"{workflowType}|{role}";

        var sb = new System.Text.StringBuilder();

        if (_cache.TryGetValue(globalKey, out var globalInst))
        {
            sb.AppendLine(globalInst);
            sb.AppendLine("---");
        }

        if (_cache.TryGetValue(roleKey, out var roleInst))
        {
            sb.AppendLine(roleInst);
        }
        else
        {
            sb.AppendLine(defaultInstruction);
        }

        return sb.ToString();
    }
}
