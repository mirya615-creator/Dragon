using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Temporary bridge used before an ad SDK is integrated. A valid placement is
/// reported as completed immediately; the reward gateway still asks the server
/// to validate limits and grant the authoritative reward.
/// </summary>
public sealed class ClientReportedRewardedAdService : IRewardedAdService
{
    public Task<RewardedAdResult> ShowAsync(
        string placementId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(string.IsNullOrWhiteSpace(placementId)
            ? RewardedAdResult.Failed
            : RewardedAdResult.Completed);
    }

    public Task<RewardedAdResult> ShowAsync(
        RewardedAdPlaybackRequest request,
        CancellationToken cancellationToken)
    {
        return ShowAsync(request?.PlacementId, cancellationToken);
    }
}
