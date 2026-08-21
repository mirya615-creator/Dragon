using System;
using GameShared.Random;

namespace DragonBound.Recruitment
{
    public enum ShovelNotEligibleReason
    {
        None,
        NoLockedCells,
        InvalidRecruit
    }

    /// <summary>
    /// Balance-only values for the greybox shovel result. These values deliberately live apart
    /// from the component-bag configuration so component ordering remains unchanged.
    /// </summary>
    public static class ShovelRecruitmentConfig
    {
        public const string ShovelConfigId = "ITEM_SHOVEL";
        public const float MissZeroChance = 0.20f;
        public const float MissOneChance = 0.35f;
        public const float MissTwoChance = 0.50f;
        public const int ConsecutiveEligibleBatchesBeforeGuaranteed = 3;
        public const string RandomStreamId = "RecruitShovel";
        public const string RandomContext = "RecruitShovel.v2";

        public static float GetChance(int consecutiveEligibleMisses)
        {
            if (consecutiveEligibleMisses < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(consecutiveEligibleMisses));
            }

            if (consecutiveEligibleMisses == 0)
            {
                return MissZeroChance;
            }

            return consecutiveEligibleMisses == 1
                ? MissOneChance
                : MissTwoChance;
        }
    }

    [Serializable]
    public sealed class ShovelRecruitmentStateData
    {
        public int ConsecutiveEligibleBatchesWithoutShovel;
        public int GuaranteedShovelCount;
        public int LongestEligibleNoShovelInterval;
        public int LockedCellCountAtCapture;
    }

    public readonly struct ShovelSpawnDecision
    {
        public ShovelSpawnDecision(
            int recruitmentNumber,
            bool isEligible,
            bool shouldSpawn,
            bool isGuaranteed,
            bool rollAttempted,
            float chance,
            int consecutiveEligibleMisses,
            ShovelNotEligibleReason notEligibleReason)
        {
            RecruitmentNumber = recruitmentNumber;
            IsEligible = isEligible;
            ShouldSpawn = shouldSpawn;
            IsGuaranteed = isGuaranteed;
            RollAttempted = rollAttempted;
            Chance = chance;
            ConsecutiveEligibleMisses = consecutiveEligibleMisses;
            NotEligibleReason = notEligibleReason;
        }

        public int RecruitmentNumber { get; }
        public bool IsEligible { get; }
        public bool ShouldSpawn { get; }
        public bool IsGuaranteed { get; }
        public bool RollAttempted { get; }
        public float Chance { get; }
        public int ConsecutiveEligibleMisses { get; }
        public ShovelNotEligibleReason NotEligibleReason { get; }
    }

    /// <summary>
    /// Per-side shovel probability state. Preview never mutates this state, allowing the existing
    /// recruitment transaction to reject a batch without consuming a pity step or random result.
    /// </summary>
    public sealed class ShovelRecruitmentState
    {
        private Func<int> lockedCellCountProvider;

        public ShovelRecruitmentState(Func<int> lockedCellCountProvider = null)
        {
            this.lockedCellCountProvider = lockedCellCountProvider ?? (() => int.MaxValue);
        }

        public int ConsecutiveEligibleBatchesWithoutShovel { get; private set; }
        public int GuaranteedShovelCount { get; private set; }
        public int LongestEligibleNoShovelInterval { get; private set; }
        public bool HasCommittedDecision { get; private set; }
        public ShovelSpawnDecision LastCommittedDecision { get; private set; }

        public void SetLockedCellCountProvider(Func<int> provider)
        {
            lockedCellCountProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public ShovelSpawnDecision PreviewDecision(
            int recruitmentNumber,
            IRunRandom random)
        {
            if (GetLockedCellCount() <= 0)
            {
                return new ShovelSpawnDecision(
                    recruitmentNumber,
                    false,
                    false,
                    false,
                    false,
                    0f,
                    0,
                    ShovelNotEligibleReason.NoLockedCells);
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            var isGuaranteed = ConsecutiveEligibleBatchesWithoutShovel >=
                               ShovelRecruitmentConfig.ConsecutiveEligibleBatchesBeforeGuaranteed;
            var chance = ShovelRecruitmentConfig.GetChance(ConsecutiveEligibleBatchesWithoutShovel);
            var rollAttempted = !isGuaranteed;
            var shouldSpawn = isGuaranteed ||
                              random.NextUnit(ShovelRecruitmentConfig.RandomContext) < chance;
            return new ShovelSpawnDecision(
                recruitmentNumber,
                true,
                shouldSpawn,
                isGuaranteed,
                rollAttempted,
                chance,
                ConsecutiveEligibleBatchesWithoutShovel,
                ShovelNotEligibleReason.None);
        }

        public void Commit(ShovelSpawnDecision decision)
        {
            LastCommittedDecision = decision;
            HasCommittedDecision = true;
            if (!decision.IsEligible)
            {
                return;
            }

            if (decision.ShouldSpawn)
            {
                if (decision.IsGuaranteed)
                {
                    GuaranteedShovelCount++;
                }

                ConsecutiveEligibleBatchesWithoutShovel = 0;
                return;
            }

            ConsecutiveEligibleBatchesWithoutShovel++;
            if (ConsecutiveEligibleBatchesWithoutShovel > LongestEligibleNoShovelInterval)
            {
                LongestEligibleNoShovelInterval = ConsecutiveEligibleBatchesWithoutShovel;
            }
        }

        public ShovelRecruitmentStateData CaptureState()
        {
            return new ShovelRecruitmentStateData
            {
                ConsecutiveEligibleBatchesWithoutShovel = ConsecutiveEligibleBatchesWithoutShovel,
                GuaranteedShovelCount = GuaranteedShovelCount,
                LongestEligibleNoShovelInterval = LongestEligibleNoShovelInterval,
                LockedCellCountAtCapture = GetLockedCellCount()
            };
        }

        public void RestoreState(ShovelRecruitmentStateData state)
        {
            if (state == null ||
                state.ConsecutiveEligibleBatchesWithoutShovel < 0 ||
                state.GuaranteedShovelCount < 0 ||
                state.LongestEligibleNoShovelInterval < 0 ||
                state.LockedCellCountAtCapture < 0)
            {
                throw new ArgumentException("A valid shovel recruitment state is required.", nameof(state));
            }

            ConsecutiveEligibleBatchesWithoutShovel = state.ConsecutiveEligibleBatchesWithoutShovel;
            GuaranteedShovelCount = state.GuaranteedShovelCount;
            LongestEligibleNoShovelInterval = state.LongestEligibleNoShovelInterval;
        }

        private int GetLockedCellCount()
        {
            return Math.Max(0, lockedCellCountProvider());
        }
    }
}
