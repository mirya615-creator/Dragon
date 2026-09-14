using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Server-backed daily sign-in adapter for backendMode=GoUnary.</summary>
internal sealed class GoSignInGateway : ISignInGateway
{
    private readonly IUnaryTransport transport;
    private readonly GoApiContextFactory contexts;
    private readonly GoBootstrapClient bootstrap;
    private readonly IRuneProfileGateway runes;

    internal GoSignInGateway(
        IUnaryTransport transport,
        GoApiContextFactory contexts,
        GoBootstrapClient bootstrap,
        IRuneProfileGateway runes)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
        this.bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
        this.runes = runes ?? throw new ArgumentNullException(nameof(runes));
    }

    public async Task<SignInStatus> GetStatusAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        ValidatePlayerId(playerId);
        GoSigninStatusResponse response = await transport.SendAsync<object, GoSigninStatusResponse>(
            "GET", "/v1/signin/status", null, contexts.Create(), cancellationToken);
        ValidateDayKey(response?.day_key);

        int cycleDay = response.cycle_day;
        if (cycleDay < 1 || cycleDay > 7)
            cycleDay = LocalSignInGateway.ResolveCycleDay(
                Math.Max(0, response.total_signins), response.can_claim);

        SignInReward preview = FirstReward(response.preview_rewards);
        if (preview == null)
            preview = LocalSignInGateway.CreatePreview(cycleDay);

        return new SignInStatus
        {
            DayKey = response.day_key,
            CycleDay = cycleDay,
            TotalClaims = Math.Max(0, response.total_signins),
            CanClaim = response.can_claim,
            DoubleAvailable = response.double_available,
            PreviewReward = preview
        };
    }

    public async Task<SignInClaimResult> ClaimAsync(
        string playerId,
        string expectedDayKey,
        SignInClaimMode mode,
        CancellationToken cancellationToken)
    {
        ValidatePlayerId(playerId);
        ValidateDayKey(expectedDayKey);
        string wireMode = mode == SignInClaimMode.SharedDouble
            ? "share_double"
            : "standard";
        bool shareSucceeded = mode == SignInClaimMode.SharedDouble;
        string stableSuffix = StableToken(playerId + "|" + expectedDayKey);
        var request = new GoSigninClaimRequest
        {
            expected_day_key = expectedDayKey,
            claim_mode = wireMode,
            client_share_succeeded = shareSucceeded,
            client_share_event_id = shareSucceeded ? "signin.share." + stableSuffix : string.Empty,
            share_platform = shareSucceeded ? ResolvePlatform() : string.Empty
        };
        string idempotencyKey = "signin.claim:" + expectedDayKey + ":" + wireMode;
        GoSigninClaimResponse response = await transport.SendAsync<GoSigninClaimRequest, GoSigninClaimResponse>(
            "POST", "/v1/signin/claim", request, contexts.Create(idempotencyKey), cancellationToken);
        ValidateDayKey(response?.day_key);

        List<SignInReward> rewards = MapRewards(response);
        if (rewards.Count == 0)
            throw InvalidResponse("Sign-in claim did not return any rewards.");
        int cycleDay = response.cycle_day;
        if (cycleDay < 1 || cycleDay > 7)
            cycleDay = LocalSignInGateway.ResolveCycleDay(
                Math.Max(1, response.total_signins), false);

        // Reward application is authoritative on the server. Refresh local projections
        // after commit, but never turn a committed claim into a failed claim if refresh fails.
        await RefreshAuthoritativeStateAsync(playerId, cancellationToken);
        return new SignInClaimResult
        {
            ClaimId = response.claim_id.ToString(CultureInfo.InvariantCulture),
            Applied = response.applied || !response.replayed,
            Replayed = response.replayed,
            DayKey = response.day_key,
            CycleDay = cycleDay,
            TotalClaims = Math.Max(1, response.total_signins),
            ClaimMode = mode,
            Rewards = rewards
        };
    }

    private async Task RefreshAuthoritativeStateAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        try
        {
            GoBootstrapResponse latest = await bootstrap.GetAsync(cancellationToken);
            if (latest?.resources != null)
                PlayerGoldEvents.RaiseBalanceChanged(playerId, latest.resources.gold);
            await runes.GetProfileAsync(playerId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Sign-in was committed, but account refresh failed: " + exception.Message);
        }
    }

    private static List<SignInReward> MapRewards(GoSigninClaimResponse response)
    {
        var result = new List<SignInReward>();
        if (response?.rewards != null)
        {
            foreach (GoSigninReward reward in response.rewards)
            {
                SignInReward mapped = MapReward(reward);
                if (mapped != null) result.Add(mapped);
            }
        }
        if (result.Count == 0)
        {
            SignInReward legacy = MapReward(response?.reward);
            if (legacy != null) result.Add(legacy);
        }
        return result;
    }

    private static SignInReward FirstReward(IReadOnlyList<GoSigninReward> rewards)
    {
        if (rewards == null) return null;
        for (int index = 0; index < rewards.Count; index++)
        {
            SignInReward mapped = MapReward(rewards[index]);
            if (mapped != null) return mapped;
        }
        return null;
    }

    private static SignInReward MapReward(GoSigninReward value)
    {
        if (value == null) return null;
        if (value.amount <= 0 || value.amount > int.MaxValue)
            throw InvalidResponse("Sign-in reward amount is invalid.");

        string type = (value.type ?? string.Empty).Trim().ToLowerInvariant();
        if (type == "gold")
        {
            return new SignInReward
            {
                Type = SignInRewardType.Gold,
                Amount = (int)value.amount,
                DisplayName = "Gold"
            };
        }
        if (type != "complete_rune" && type != "rune")
            throw InvalidResponse("Unknown sign-in reward type: " + value.type);

        RuneDefinition definition = RuneCatalog.Find(value.id);
        RuneRarity rarity = definition != null
            ? definition.Rarity
            : ParseRarity(value.rarity);
        string displayName = definition != null
            ? definition.DisplayName
            : "Random Complete " + rarity + " Rune";
        return new SignInReward
        {
            Type = SignInRewardType.CompleteRune,
            Amount = (int)value.amount,
            RuneId = value.id ?? string.Empty,
            RuneRarity = rarity,
            DisplayName = displayName
        };
    }

    private static RuneRarity ParseRarity(string value)
    {
        if (string.Equals(value, "epic", StringComparison.OrdinalIgnoreCase))
            return RuneRarity.Epic;
        if (string.Equals(value, "legendary", StringComparison.OrdinalIgnoreCase))
            return RuneRarity.Legendary;
        throw InvalidResponse("Sign-in Rune rarity is invalid: " + value);
    }

    private static string ResolvePlatform()
    {
        switch (Application.platform)
        {
            case RuntimePlatform.Android: return "android";
            case RuntimePlatform.IPhonePlayer: return "ios";
            default: return "editor";
        }
    }

    private static string StableToken(string value)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            var builder = new StringBuilder(32);
            for (int index = 0; index < 16; index++)
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    private static void ValidatePlayerId(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("Player ID is required.", nameof(playerId));
    }

    private static void ValidateDayKey(string dayKey)
    {
        if (!DateTime.TryParseExact(
            dayKey,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _))
        {
            throw InvalidResponse("Sign-in DayKey is invalid.");
        }
    }

    private static ClientServiceException InvalidResponse(string message)
    {
        return new ClientServiceException("INVALID_RESPONSE", message);
    }
}
