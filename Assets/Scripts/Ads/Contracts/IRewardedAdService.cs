using System.Threading;
using System.Threading.Tasks;

public enum RewardedAdResult
{
    Completed,
    Skipped,
    Failed
}

public sealed class RewardedAdPlaybackRequest
{
    public string PlacementId;
    public string CustomData;
    public string ClaimId;
}

/// <summary>
/// Rewarded-ad boundary. A production SDK adapter can replace the local implementation.
/// </summary>
public interface IRewardedAdService
{
    Task<RewardedAdResult> ShowAsync(
        string placementId,
        CancellationToken cancellationToken);

    Task<RewardedAdResult> ShowAsync(
        RewardedAdPlaybackRequest request,
        CancellationToken cancellationToken);
}
