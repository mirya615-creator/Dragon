using System.Threading;
using System.Threading.Tasks;

public enum RewardedAdResult
{
    Completed,
    Skipped,
    Failed
}

/// <summary>
/// Rewarded-ad boundary. A production SDK adapter can replace the local implementation.
/// </summary>
public interface IRewardedAdService
{
    Task<RewardedAdResult> ShowAsync(
        string placementId,
        CancellationToken cancellationToken);
}
