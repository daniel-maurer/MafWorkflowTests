using Microsoft.Agents.AI.Workflows;
using MafWorkflow.Shared;

namespace MafWorkflow.Worker.Core;

public class WorkflowSessionBase : IAsyncDisposable
{
    protected readonly string SessionId;
    protected readonly Workflow Workflow;
    protected readonly BffWorkflowClientBase Parent;
    protected readonly SessionWorkflowInteractorBase UserInteractor;
    protected readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    protected readonly Task RunnerTask;

    public WorkflowSessionBase(
        string sessionId,
        Workflow workflow,
        BffWorkflowClientBase parent,
        SessionWorkflowInteractorBase userInteractor)
    {
        SessionId = sessionId;
        Workflow = workflow;
        Parent = parent;
        UserInteractor = userInteractor;
        RunnerTask = Task.Run(RunAsync);
    }

    public virtual async Task StartAsync(string initialMessage)
    {
        if (!string.IsNullOrWhiteSpace(initialMessage))
        {
            await EnqueueMessageAsync(initialMessage);
        }

        Started.TrySetResult();
    }

    public virtual async Task EnqueueMessageAsync(string message)
    {
        await UserInteractor.EnqueueMessageAsync(message);
    }

    protected virtual async Task RunAsync()
    {
        await Started.Task;

        try
        {
            await using var handle = await InProcessExecution.StreamAsync(Workflow, string.Empty);
            await foreach (var evt in handle.WatchStreamAsync())
            {
                switch (evt)
                {
                    case RequestInfoEvent requestInfoEvent:
                        var incomingMessage = await UserInteractor.ReadNextMessageAsync();
                        var response = requestInfoEvent.Request.CreateResponse(incomingMessage);
                        await handle.SendResponseAsync(response);
                        break;
                    case WorkflowOutputEvent workflowOutput:
                        await Parent.OnWorkflowOutputAsync(SessionId, workflowOutput.Data);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Session {SessionId} failed: {ex.Message}");
            await Parent.PublishTraceAsync(SessionId, $"Session error: {ex.Message}", "error");
        }
    }

    public virtual async ValueTask DisposeAsync()
    {
        UserInteractor.Complete();
        await RunnerTask;
    }
}
