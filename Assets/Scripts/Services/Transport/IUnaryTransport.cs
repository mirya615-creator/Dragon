using System.Threading;
using System.Threading.Tasks;

public interface IUnaryTransport
{
    Task<TResponse> SendAsync<TRequest, TResponse>(
        string method,
        string path,
        TRequest request,
        UnaryRequestContext context,
        CancellationToken cancellationToken);
}
