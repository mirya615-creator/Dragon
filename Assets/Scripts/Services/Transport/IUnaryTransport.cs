using System.Threading;
using System.Threading.Tasks;

public interface IUnaryRawJsonRequest
{
    string ToJson();
}

public interface IUnaryRawJsonResponse
{
    void ReadJson(string json);
}

public interface IUnaryTransport
{
    Task<TResponse> SendAsync<TRequest, TResponse>(
        string method,
        string path,
        TRequest request,
        UnaryRequestContext context,
        CancellationToken cancellationToken);
}
