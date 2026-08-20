using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class RuneProfileMutationResult
{
    public bool Succeeded;
    public RuneProfile Profile;
}

/// <summary>
/// Rune inventory boundary used by Main/MainPanel/WeaponPanel.
/// Fragment rewards are promoted to complete runes during settlement; there is no player-driven
/// compose operation. A future Go unary adapter can implement this asynchronous contract.
/// </summary>
public interface IRuneProfileGateway
{
    Task<RuneProfile> SettleRunAsync(
        string playerId,
        string runId,
        IReadOnlyList<RuneReward> rewards,
        CancellationToken cancellationToken);

    Task<RuneProfile> GetProfileAsync(
        string playerId,
        CancellationToken cancellationToken);

    Task<RuneProfileMutationResult> EquipRuneAsync(
        string playerId,
        string heroId,
        string runeId,
        CancellationToken cancellationToken);

    Task<RuneProfileMutationResult> UnequipRuneAsync(
        string playerId,
        string heroId,
        CancellationToken cancellationToken);
}
