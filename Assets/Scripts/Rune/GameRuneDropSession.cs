using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Development-only run drop simulator. It deliberately skips the account-day gate.
/// </summary>
public static class GameRuneDropSession
{
    private static string pendingRunId;
    private static List<RuneReward> pendingRewards;

    public static void Begin(string runId)
    {
        pendingRunId = runId;
        pendingRewards = GenerateRewards();
        Debug.Log($"Simulated {pendingRewards.Count} rune reward(s) for run {runId}.");
    }

    public static RuneProfile Settle(string playerId, string runId)
    {
        if (pendingRewards == null || pendingRunId != runId)
        {
            pendingRunId = runId;
            pendingRewards = GenerateRewards();
        }

        RuneProfile profile = new LocalRuneRewardService().SettleRun(
            playerId,
            runId,
            pendingRewards);
        pendingRunId = null;
        pendingRewards = null;
        return profile;
    }

    private static List<RuneReward> GenerateRewards()
    {
        int rewardCount = Random.Range(1, 5);
        var rewards = new List<RuneReward>(rewardCount);
        IReadOnlyList<RuneDefinition> catalog = RuneCatalog.All;

        for (int index = 0; index < rewardCount; index++)
        {
            RuneDefinition definition = catalog[Random.Range(0, catalog.Count)];
            RuneRewardKind kind = GetRewardKind(definition.Rarity);
            rewards.Add(new RuneReward
            {
                RuneId = definition.RuneId,
                DisplayName = definition.DisplayName,
                Rarity = definition.Rarity,
                RewardKind = kind,
                Amount = 1
            });
        }

        return rewards;
    }

    private static RuneRewardKind GetRewardKind(RuneRarity rarity)
    {
        if (rarity == RuneRarity.Legendary) return RuneRewardKind.Fragment;
        if (rarity == RuneRarity.Epic)
        {
            return Random.value < 0.25f
                ? RuneRewardKind.CompleteRune
                : RuneRewardKind.Fragment;
        }
        return RuneRewardKind.CompleteRune;
    }
}
