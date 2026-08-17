using System;
using System.Threading;
using System.Threading.Tasks;

public enum ShareResult
{
    Completed,
    Cancelled,
    Failed
}

[Serializable]
public sealed class ShareRequest
{
    public string PlacementId;
    public string Message;
}

/// <summary>
/// External share boundary. A platform SDK adapter can replace the local implementation.
/// </summary>
public interface IShareService
{
    Task<ShareResult> ShareAsync(
        ShareRequest request,
        CancellationToken cancellationToken);
}
