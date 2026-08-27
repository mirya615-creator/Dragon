using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DragonBound.Services;
using UnityEngine;

public sealed class GameSettlementPreparation
{
    public FinishGameplayRunResult Result;
    public MatchOutcome GoldOutcome;
    public bool CanClaimGold;
}

/// <summary>
/// Single client settlement boundary. The local implementation resumes each idempotent
/// stage from PlayerPrefs; the Go unary finish call can later make the same stages atomic.
/// </summary>
public sealed class GameSettlementCoordinator
{
    private const string LedgerKeyPrefix = "dragonbound.settlement-ledger.";

    [Serializable]
    private sealed class SettlementLedger
    {
        public bool FinishAccepted;
        public FinishGameplayRunResult FinishResult;
        public bool RankApplied;
        public bool RunesApplied;
        public bool CompletedRunApplied;
        public bool GoldApplied;
    }

    private readonly IClientServices services;

    public GameSettlementCoordinator(IClientServices services)
    {
        this.services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public async Task<GameSettlementPreparation> PrepareAsync(
        string playerId,
        string runId,
        ServerMatchResult proposedResult,
        GameplayTerminationReason reason,
        GameplayFaultAttribution attribution,
        int reachedWave,
        int finalResources,
        int recruitmentCount,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(playerId, runId);
        SettlementLedger ledger = Load(runId);
        if (!ledger.FinishAccepted || ledger.FinishResult == null)
        {
            FinishGameplayRunResult finish = await services.Gameplay.FinishRunAsync(
                new FinishGameplayRunRequest
                {
                    RunId = runId,
                    PlayerId = playerId,
                    ProposedResult = proposedResult,
                    SettlementType = GameplaySettlementType.Normal,
                    TerminationReason = reason,
                    FaultAttribution = attribution,
                    ReachedWave = Math.Max(0, reachedWave),
                    FinalResources = Math.Max(0, finalResources),
                    RecruitmentCount = Math.Max(0, recruitmentCount),
                    IdempotencyKey = runId + ":finish"
                },
                cancellationToken);
            if (finish == null || !finish.Accepted)
                throw new InvalidOperationException("Gameplay settlement was not accepted.");
            ledger.FinishAccepted = true;
            ledger.FinishResult = finish;
            Save(runId, ledger);
        }

        FinishGameplayRunResult result = ledger.FinishResult;
        bool resolved = result.Result == ServerMatchResult.Victory ||
                        result.Result == ServerMatchResult.Defeat;
        if (!resolved)
        {
            return CreatePreparation(result);
        }

        if (result.ApplyRank && !ledger.RankApplied)
        {
            RankProgressResult rank = result.Result == ServerMatchResult.Victory
                ? await services.Rank.RecordVictoryAsync(playerId, runId, cancellationToken)
                : await services.Rank.RecordDefeatAsync(playerId, runId, cancellationToken);
            if (result.Result == ServerMatchResult.Victory)
                RankPromotionStore.Set(playerId, rank);
            ledger.RankApplied = true;
            Save(runId, ledger);
        }

        if (result.GrantRewards && !ledger.RunesApplied)
        {
            await GameRuneDropSession.SettleAsync(playerId, runId, cancellationToken);
            ledger.RunesApplied = true;
            Save(runId, ledger);
        }

        if (result.CountCompletedRun && !ledger.CompletedRunApplied)
        {
            MerchantRunResult merchant = await services.Merchant.RecordCompletedRunAsync(
                playerId, runId, cancellationToken);
            if (merchant.Offer != null) MerchantPresentationStore.MarkPending(playerId);
            ledger.CompletedRunApplied = true;
            Save(runId, ledger);
        }

        return CreatePreparation(result);
    }

    public async Task ClaimGoldAsync(
        string playerId,
        string runId,
        GoldClaimType claimType,
        string adVerificationId,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(playerId, runId);
        SettlementLedger ledger = Load(runId);
        if (!ledger.FinishAccepted || ledger.FinishResult == null)
            throw new InvalidOperationException("Settlement must be prepared before claiming gold.");
        if (!ledger.FinishResult.GrantRewards ||
            ledger.FinishResult.Result == ServerMatchResult.NoContest ||
            ledger.FinishResult.Result == ServerMatchResult.Pending)
            return;
        if (ledger.GoldApplied) return;

        MatchOutcome outcome = ledger.FinishResult.Result == ServerMatchResult.Victory
            ? MatchOutcome.Victory
            : MatchOutcome.Defeat;
        await services.Gold.SettleMatchAsync(
            playerId, runId, outcome, claimType, adVerificationId, cancellationToken);
        ledger.GoldApplied = true;
        Save(runId, ledger);
    }

    private static GameSettlementPreparation CreatePreparation(FinishGameplayRunResult result)
    {
        return new GameSettlementPreparation
        {
            Result = result,
            GoldOutcome = result.Result == ServerMatchResult.Victory
                ? MatchOutcome.Victory
                : MatchOutcome.Defeat,
            CanClaimGold = result.GrantRewards &&
                           (result.Result == ServerMatchResult.Victory ||
                            result.Result == ServerMatchResult.Defeat)
        };
    }

    private static SettlementLedger Load(string runId)
    {
        string json = PlayerPrefs.GetString(GetKey(runId), string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return new SettlementLedger();
        try
        {
            return JsonUtility.FromJson<SettlementLedger>(json) ?? new SettlementLedger();
        }
        catch (Exception)
        {
            return new SettlementLedger();
        }
    }

    private static void Save(string runId, SettlementLedger ledger)
    {
        PlayerPrefs.SetString(GetKey(runId), JsonUtility.ToJson(ledger));
        PlayerPrefs.Save();
    }

    private static string GetKey(string runId)
    {
        using (SHA256 hash = SHA256.Create())
        {
            byte[] digest = hash.ComputeHash(Encoding.UTF8.GetBytes(runId));
            return LedgerKeyPrefix + Convert.ToBase64String(digest)
                .Replace('/', '_').Replace('+', '-').TrimEnd('=');
        }
    }

    private static void ValidateIdentity(string playerId, string runId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("Player ID is required.", nameof(playerId));
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("Run ID is required.", nameof(runId));
    }
}
