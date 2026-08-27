using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Carries the authoritative rewards already resolved at completed-wave boundaries into the
/// account settlement gateway. Settlement never rolls rewards a second time.
/// </summary>
public static class GameRuneDropSession
{
    private const int MaxRewardsPerRun = 4;
    private static string pendingRunId;
    private static List<RuneReward> pendingRewards;

    public static void Begin(string runId)
    {
        pendingRunId = runId;
        pendingRewards = new List<RuneReward>(MaxRewardsPerRun);
    }

    public static void RecordCompletedWaveReward(DragonBound.Runes.RuneReward runtimeReward)
    {
        if (runtimeReward == null || pendingRewards == null ||
            pendingRewards.Count >= MaxRewardsPerRun) return;

        string profileRuneId = RuneGameplayLoadoutAdapter.ResolveProfileRuneId(
            runtimeReward.RuneId);
        RuneDefinition definition = RuneCatalog.Find(profileRuneId);
        if (definition == null)
        {
            Debug.LogError($"Runtime Rune reward '{runtimeReward.RuneId}' cannot be settled.");
            return;
        }

        pendingRewards.Add(new RuneReward
        {
            RuneId = definition.RuneId,
            DisplayName = definition.DisplayName,
            Rarity = definition.Rarity,
            RewardKind = runtimeReward.IsComplete
                ? RuneRewardKind.CompleteRune
                : RuneRewardKind.Fragment,
            Amount = 1
        });
        Debug.Log(
            $"Rune reward recorded: Wave={runtimeReward.Wave}, Rune={definition.RuneId}, " +
            $"Kind={(runtimeReward.IsComplete ? "Complete" : "Fragment")}.");
    }

    public static async Task<RuneProfile> SettleAsync(
        string playerId,
        string runId,
        CancellationToken cancellationToken)
    {
        if (pendingRewards == null || pendingRunId != runId)
        {
            pendingRunId = runId;
            pendingRewards = new List<RuneReward>(MaxRewardsPerRun);
        }

        RuneProfile profile = await ClientCompositionRoot.Current.Runes.SettleRunAsync(
            playerId,
            runId,
            pendingRewards,
            cancellationToken);
        pendingRunId = null;
        pendingRewards = null;
        return profile;
    }
}
