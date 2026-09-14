using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public enum SignInClaimMode
{
    Standard,
    SharedDouble
}

public enum SignInRewardType
{
    Gold,
    CompleteRune
}

[Serializable]
public sealed class SignInReward
{
    public SignInRewardType Type;
    public int Amount;
    public string RuneId;
    public string DisplayName;
    public RuneRarity RuneRarity;
}

[Serializable]
public sealed class SignInStatus
{
    public string DayKey;
    public int CycleDay;
    public int TotalClaims;
    public bool CanClaim;
    public bool DoubleAvailable;
    public SignInReward PreviewReward;
}

[Serializable]
public sealed class SignInClaimResult
{
    public string ClaimId;
    public bool Applied;
    public bool Replayed;
    public string DayKey;
    public int CycleDay;
    public int TotalClaims;
    public SignInClaimMode ClaimMode;
    public List<SignInReward> Rewards = new List<SignInReward>();
}

/// <summary>
/// Daily sign-in boundary. The local implementation can be replaced by the server
/// status/claim adapter without changing MainSignInController.
/// </summary>
public interface ISignInGateway
{
    Task<SignInStatus> GetStatusAsync(
        string playerId,
        CancellationToken cancellationToken);

    Task<SignInClaimResult> ClaimAsync(
        string playerId,
        string expectedDayKey,
        SignInClaimMode mode,
        CancellationToken cancellationToken);
}
