using System;
using System.Collections.Generic;
using DragonBound.Analytics;
using GameShared.Random;

namespace DragonBound.Runes
{
    public sealed class RuneReward
    {
        public RuneReward(int wave, RuneRarity rarity, string runeId, bool complete, bool fragment)
        { Wave = wave; Rarity = rarity; RuneId = runeId; IsComplete = complete; IsFragment = fragment; }
        public int Wave { get; }
        public RuneRarity Rarity { get; }
        public string RuneId { get; }
        public bool IsComplete { get; }
        public bool IsFragment { get; }
    }

    public sealed class RuneRewardResolutionResult
    {
        public RuneRewardResolutionResult(RuneReward reward, bool granted, string reason)
        {
            Reward = reward;
            Granted = granted;
            Reason = reason ?? string.Empty;
        }

        public RuneReward Reward { get; }
        public bool Granted { get; }
        public string Reason { get; }
    }

    public sealed class RuneDropState
    {
        public int SuccessfulRewards { get; private set; }
        public bool IsCapped => SuccessfulRewards >= RuneDropRules.MaxSuccessfulRewardsPerRun;
        public bool RecordSuccess() { if (IsCapped) return false; SuccessfulRewards++; return true; }
    }

    public static class RuneDropRules
    {
        public const int MaxSuccessfulRewardsPerRun = 4;
        public const string RandomStream = "PlayerRuneReward";
        public const string ContentVersion = "RuneContent.V1";
        public const string AlgorithmVersion = RuneCombatDeterminism.AlgorithmVersion;
        public static float ChanceForWave(int wave) { if (wave < 3) return 0f; if (wave <= 6) return .12f; if (wave <= 12) return .18f; if (wave <= 16) return .28f; return .40f; }
        public static bool IsEligibleWave(int wave) { return wave >= 3; }
        public static RuneRarity RollRarity(int wave, IRunRandom random, string context)
        {
            var roll = random.NextUnit(context);
            if (wave <= 6) return roll < .75f ? RuneRarity.Common : RuneRarity.Excellent;
            if (wave <= 12) return roll < .45f ? RuneRarity.Common : roll < .75f ? RuneRarity.Excellent : roll < .90f ? RuneRarity.Epic : RuneRarity.Legendary;
            if (wave <= 16) return roll < .30f ? RuneRarity.Common : roll < .60f ? RuneRarity.Excellent : roll < .85f ? RuneRarity.Epic : RuneRarity.Legendary;
            return roll < .15f ? RuneRarity.Common : roll < .40f ? RuneRarity.Excellent : roll < .80f ? RuneRarity.Epic : RuneRarity.Legendary;
        }
        public static RuneReward TryRollCompletedWave(int runSeed, int wave, RuneDropState state)
        {
            if (state == null || state.IsCapped || !IsEligibleWave(wave)) return null;
            var random = new RunRandom(DeriveSeed(runSeed, wave));
            if (random.NextUnit(RandomStream + ".Chance") >= ChanceForWave(wave)) return null;
            var rarity = RollRarity(wave, random, RandomStream + ".Rarity");
            var pool = RuneCatalog.Pool(rarity); if (pool.Count == 0) return null;
            var definition = pool[random.NextInt(RandomStream + ".Pool", 0, pool.Count)];
            var complete = rarity == RuneRarity.Common || rarity == RuneRarity.Excellent;
            if (rarity == RuneRarity.Epic) complete = random.NextUnit(RandomStream + ".EpicForm") < .25f;
            state.RecordSuccess();
            return new RuneReward(wave, rarity, definition.RuneId, complete, !complete);
        }
        public static void GrantToInventory(RuneReward reward, RuneInventory inventory)
        {
            if (reward == null || inventory == null) return;
            if (reward.IsComplete) inventory.AddComplete(reward.RuneId); else inventory.AddFragment(reward.RuneId);
        }
        private static int DeriveSeed(int seed, int wave)
        {
            unchecked
            {
                var hash = 2166136261u;
                hash = (hash ^ (uint)seed) * 16777619u;
                hash = (hash ^ (uint)wave) * 16777619u;
                foreach (var character in ContentVersion + "." + AlgorithmVersion + "." + RandomStream)
                {
                    hash = (hash ^ character) * 16777619u;
                }

                return (int)hash;
            }
        }
    }

    /// <summary>Per-run Player reward sink. Call only after a wave has fully completed.</summary>
    public sealed class RuneRunRewardService
    {
        private readonly int runSeed;
        private readonly RuneDropState state = new RuneDropState();
        private readonly List<RuneReward> grantedRewards = new List<RuneReward>();
        private readonly RuneFeatureGate gate;
        private readonly Action onInventoryChanged;
        private readonly RuneAnalyticsAdapterV2 analytics;
        private int analyticsSequence;

        public RuneRunRewardService(int runSeed, RuneInventory inventory)
            : this(runSeed, inventory, null, null)
        {
        }

        public RuneRunRewardService(
            int runSeed,
            RuneInventory inventory,
            RuneFeatureGate gate,
            Action onInventoryChanged,
            RuneAnalyticsAdapterV2 analytics = null)
        {
            this.runSeed = runSeed;
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.gate = gate;
            this.onInventoryChanged = onInventoryChanged;
            this.analytics = analytics;
        }

        public RuneInventory Inventory { get; }
        public int SuccessfulRewards => state.SuccessfulRewards;
        public IReadOnlyList<RuneReward> GrantedRewards => grantedRewards;

        public RuneReward CompleteWave(int completedWave)
        {
            return CompleteWaveResult(completedWave).Reward;
        }

        public RuneRewardResolutionResult CompleteWaveResult(int completedWave)
        {
            var key = "wave-" + completedWave + "-" + (++analyticsSequence);
            if (analytics != null)
            {
                analytics.RecordRewardPending(
                    new RuneRewardPendingObservationV2(key + "-pending", completedWave), out _);
            }
            if (gate != null && !gate.IsUnlocked)
            {
                if (analytics != null)
                {
                    analytics.RecordGateRejection(
                        new RuneGateRejectionObservationV2(
                            key + "-gate", completedWave, "reward", gate.AccountDay,
                            "RuneSystemLockedUntilDay3"), out _);
                }
                return RecordResult(null, false, "RuneSystemLockedUntilDay3", key, completedWave);
            }

            var reward = RuneDropRules.TryRollCompletedWave(runSeed, completedWave, state);
            RuneDropRules.GrantToInventory(reward, Inventory);
            if (reward != null)
            {
                grantedRewards.Add(reward);
                onInventoryChanged?.Invoke();
            }
            var reason = reward == null
                ? (state.IsCapped ? "RewardCapReached" : "NoRewardRolled")
                : string.Empty;
            return RecordResult(reward, reward != null, reason, key, completedWave);
        }

        private RuneRewardResolutionResult RecordResult(
            RuneReward reward,
            bool granted,
            string reason,
            string key,
            int wave)
        {
            if (analytics != null)
            {
                analytics.RecordRewardResult(
                    new RuneRewardResultObservationV2(
                        key + "-result",
                        wave,
                        reward?.RuneId ?? string.Empty,
                        reward == null ? string.Empty : (reward.IsComplete ? "complete" : "fragment"),
                        granted,
                        reason), out _);
            }
            return new RuneRewardResolutionResult(reward, granted, reason);
        }
    }
}
