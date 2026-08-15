using System;
using System.Threading;
using System.Threading.Tasks;

[Serializable]
public sealed class PlayerEnergyState
{
    public int Current;
    public int Maximum;
}

public sealed class EnergyConsumeResult
{
    public bool Succeeded;
    public PlayerEnergyState State;
}

/// <summary>
/// Player energy boundary. A server-backed unary-call implementation can replace the local gateway.
/// </summary>
public interface IPlayerEnergyGateway
{
    Task<PlayerEnergyState> GetEnergyAsync(string playerId, CancellationToken cancellationToken);

    Task<EnergyConsumeResult> ConsumeEnergyAsync(
        string playerId,
        int amount,
        string requestId,
        CancellationToken cancellationToken);
}
