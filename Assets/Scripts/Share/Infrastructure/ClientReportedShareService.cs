using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Temporary P1 bridge used before platform callbacks are available. It confirms the
/// client-side share step immediately; the authoritative reward request still goes to
/// the server and carries an auditable client_share_succeeded event.
/// </summary>
public sealed class ClientReportedShareService : IShareService
{
    public Task<ShareResult> ShareAsync(
        ShareRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request == null || string.IsNullOrWhiteSpace(request.PlacementId))
            return Task.FromResult(ShareResult.Failed);
        return Task.FromResult(ShareResult.Completed);
    }
}
