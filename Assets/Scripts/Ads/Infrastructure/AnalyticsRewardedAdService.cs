using System;
using System.Threading;
using System.Threading.Tasks;
using DragonBound.Analytics;

/// <summary>Records every rewarded-ad attempt at the service boundary, independent of its caller.</summary>
public sealed class AnalyticsRewardedAdService : IRewardedAdService
{
    private readonly IRewardedAdService inner;
    private readonly IAdAnalyticsObserverV2 observer;

    public AnalyticsRewardedAdService(
        IRewardedAdService inner,
        IAdAnalyticsObserverV2 observer = null)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.observer = observer ?? NoOpAdAnalyticsObserverV2.Instance;
    }

    public async Task<RewardedAdResult> ShowAsync(
        string placementId,
        CancellationToken cancellationToken)
    {
        var attemptId = Guid.NewGuid().ToString("N");
        observer.RecordRequest(attemptId, placementId);

        try
        {
            var result = await inner.ShowAsync(placementId, cancellationToken);
            observer.RecordResult(attemptId, placementId, ToWireValue(result), string.Empty);
            return result;
        }
        catch (OperationCanceledException)
        {
            observer.RecordResult(attemptId, placementId, "cancelled", "operation_cancelled");
            throw;
        }
        catch
        {
            observer.RecordResult(attemptId, placementId, "failed", "provider_exception");
            throw;
        }
    }

    public async Task<RewardedAdResult> ShowAsync(
        RewardedAdPlaybackRequest request,
        CancellationToken cancellationToken)
    {
        string placementId = request?.PlacementId ?? string.Empty;
        var attemptId = Guid.NewGuid().ToString("N");
        observer.RecordRequest(attemptId, placementId);
        try
        {
            var result = await inner.ShowAsync(request, cancellationToken);
            observer.RecordResult(attemptId, placementId, ToWireValue(result), string.Empty);
            return result;
        }
        catch (OperationCanceledException)
        {
            observer.RecordResult(attemptId, placementId, "cancelled", "operation_cancelled");
            throw;
        }
        catch
        {
            observer.RecordResult(attemptId, placementId, "failed", "provider_exception");
            throw;
        }
    }

    private static string ToWireValue(RewardedAdResult result)
    {
        switch (result)
        {
            case RewardedAdResult.Completed: return "completed";
            case RewardedAdResult.Skipped: return "skipped";
            default: return "failed";
        }
    }
}
