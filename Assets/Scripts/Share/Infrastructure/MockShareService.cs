using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Development share simulation. Production builds should use a platform share adapter.
/// </summary>
public sealed class MockShareService : IShareService
{
    private static readonly TimeSpan SimulatedShareDelay = TimeSpan.FromMilliseconds(500);
    private bool isSharing;

    public async Task<ShareResult> ShareAsync(
        ShareRequest request,
        CancellationToken cancellationToken)
    {
        if (isSharing || request == null || string.IsNullOrWhiteSpace(request.PlacementId))
        {
            return ShareResult.Failed;
        }

        isSharing = true;
        try
        {
            await Task.Delay(SimulatedShareDelay, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return ShareResult.Completed;
        }
        finally
        {
            isSharing = false;
        }
    }
}
