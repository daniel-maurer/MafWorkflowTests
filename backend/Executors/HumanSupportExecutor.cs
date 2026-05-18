using Microsoft.Agents.AI.Workflows;

namespace SupportWorkflow;

/// <summary>
/// Executor responsible for the human-support exchange triggered when the Frequent
/// Problem Agent fails to match a known issue. Delegates the actual loop to
/// <see cref="HumanSupportSession"/> so the same orchestration can also be reused
/// when an automated resolution is rejected by the customer.
/// All interaction happens through the BFF / SignalR channel — there is no terminal
/// interaction.
/// </summary>
internal sealed class HumanSupportExecutor : Executor<FrequentProblemResult, ResolutionResult>
{
    private readonly IUserInteractor _userInteractor;

    public HumanSupportExecutor(IUserInteractor userInteractor) : base("HumanSupportExecutor")
    {
        _userInteractor = userInteractor ?? throw new ArgumentNullException(nameof(userInteractor));
    }

    public override async ValueTask<ResolutionResult> HandleAsync(FrequentProblemResult frequentProblemResult, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (frequentProblemResult is null)
        {
            throw new ArgumentNullException(nameof(frequentProblemResult));
        }

        Logger.LogInfo("Starting human support handling for complex/unknown issue");

        var resolutionResult = await HumanSupportSession.RunAsync(
            _userInteractor,
            handoffReason: "no KB match",
            cancellationToken);

        Logger.LogInfo($"Human support interaction completed - Issue resolved: {resolutionResult.IsResolved}");

        await context.YieldOutputAsync(resolutionResult, cancellationToken);
        return resolutionResult;
    }
}
