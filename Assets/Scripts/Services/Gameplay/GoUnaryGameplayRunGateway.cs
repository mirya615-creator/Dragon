using System;
using System.Threading;
using System.Threading.Tasks;
using DragonBound.Services;

public sealed class GoUnaryGameplayRunGateway : IGameplayRunGateway
{
    public const string StartPath = "/v1/gameplay/run/start";
    public const string RecruitPath = "/v1/gameplay/run/recruit";
    public const string FinishPath = "/v1/gameplay/run/finish";

    private readonly IUnaryTransport transport;
    private readonly Func<UnaryRequestContext> contextFactory;

    public GoUnaryGameplayRunGateway(
        IUnaryTransport transport,
        Func<UnaryRequestContext> contextFactory)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public Task<StartGameplayRunResult> StartRunAsync(
        StartGameplayRunRequest request,
        CancellationToken cancellationToken)
    {
        return transport.SendAsync<StartGameplayRunRequest, StartGameplayRunResult>(
            "POST",
            StartPath,
            request,
            contextFactory(),
            cancellationToken);
    }

    public Task<RecruitGameplayResult> RecruitAsync(
        RecruitGameplayRequest request,
        CancellationToken cancellationToken)
    {
        return transport.SendAsync<RecruitGameplayRequest, RecruitGameplayResult>(
            "POST",
            RecruitPath,
            request,
            contextFactory(),
            cancellationToken);
    }

    public Task<FinishGameplayRunResult> FinishRunAsync(
        FinishGameplayRunRequest request,
        CancellationToken cancellationToken)
    {
        return transport.SendAsync<FinishGameplayRunRequest, FinishGameplayRunResult>(
            "POST",
            FinishPath,
            request,
            contextFactory(),
            cancellationToken);
    }
}
