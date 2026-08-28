using System;
using GameShared.Random;

namespace DragonBound.AI
{
    public enum AiStrategyProfileId
    {
        Beginner = 0,
        Veteran = 1,
        Elite = 2,
        Master = 3
    }

    /// <summary>
    /// Decision-quality configuration only. None of these values may alter resources,
    /// unit statistics, recruit odds, enemy statistics, or board rules.
    /// </summary>
    public sealed class AiStrategyProfile
    {
        private AiStrategyProfile(
            AiStrategyProfileId id,
            float decisionIntervalSeconds,
            float scoreError,
            int planningDepth,
            int resourceReserve,
            int spaceReserve)
        {
            Id = id;
            DecisionIntervalSeconds = decisionIntervalSeconds;
            ScoreError = scoreError;
            PlanningDepth = planningDepth;
            ResourceReserve = resourceReserve;
            SpaceReserve = spaceReserve;
        }

        public AiStrategyProfileId Id { get; }
        public float DecisionIntervalSeconds { get; }
        public float ScoreError { get; }
        public int PlanningDepth { get; }
        public int ResourceReserve { get; }
        public int SpaceReserve { get; }
        public string AnalyticsId => Id.ToString().ToLowerInvariant();

        public static AiStrategyProfile Get(AiStrategyProfileId id)
        {
            switch (id)
            {
                case AiStrategyProfileId.Beginner:
                    return new AiStrategyProfile(id, 1.2f, 0.35f, 0, 0, 0);
                case AiStrategyProfileId.Veteran:
                    return new AiStrategyProfile(id, 0.7f, 0.22f, 1, 1, 0);
                case AiStrategyProfileId.Elite:
                    return new AiStrategyProfile(id, 0.4f, 0.12f, 2, 2, 1);
                case AiStrategyProfileId.Master:
                    return new AiStrategyProfile(id, 0.2f, 0.05f, 3, 2, 2);
                default:
                    throw new ArgumentOutOfRangeException(nameof(id), id, null);
            }
        }
    }

    public static class AiRankProfileMapping
    {
        public static AiStrategyProfileId FromRankLevel(int rankLevel)
        {
            int safeRank = Math.Max(1, Math.Min(10, rankLevel));
            if (safeRank <= 2) return AiStrategyProfileId.Beginner;
            if (safeRank <= 5) return AiStrategyProfileId.Veteran;
            if (safeRank <= 8) return AiStrategyProfileId.Elite;
            return AiStrategyProfileId.Master;
        }

        public static AiStrategyProfileId OneStepEasier(AiStrategyProfileId profile)
        {
            return profile == AiStrategyProfileId.Beginner
                ? AiStrategyProfileId.Beginner
                : (AiStrategyProfileId)((int)profile - 1);
        }

        public static bool TryParseWireValue(string value, out AiStrategyProfileId profile)
        {
            if (Enum.TryParse(value, true, out profile)) return true;
            profile = AiStrategyProfileId.Beginner;
            return false;
        }
    }

    /// <summary>Deterministic, pause-aware gate for AI decisions.</summary>
    public sealed class AiDecisionScheduler
    {
        private readonly AiStrategyProfile profile;
        private readonly IRunRandom random;
        private float remainingSeconds;
        private int intervalOrdinal;

        public AiDecisionScheduler(AiStrategyProfile profile, int decisionSeed)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            random = new RunRandom(decisionSeed);
            Reset();
        }

        public int DecisionCount { get; private set; }
        public float CurrentIntervalSeconds { get; private set; }

        public bool Tick(float deltaTime, bool canDecide)
        {
            if (!canDecide || deltaTime <= 0f) return false;
            remainingSeconds -= deltaTime;
            if (remainingSeconds > 0f) return false;
            DecisionCount++;
            ScheduleNext();
            return true;
        }

        public void Reset()
        {
            DecisionCount = 0;
            intervalOrdinal = 0;
            ScheduleNext();
        }

        private void ScheduleNext()
        {
            float unit = random.NextUnit("ai.decision.interval." + intervalOrdinal++);
            float multiplier = 0.85f + (unit * 0.30f);
            CurrentIntervalSeconds = profile.DecisionIntervalSeconds * multiplier;
            remainingSeconds = CurrentIntervalSeconds;
        }
    }

    public enum AiActionKind
    {
        Recruit,
        DeployBasic,
        MergeBasic,
        FormHero,
        PlaceComponent,
        ProtectComponent,
        MoveForBoardSpace,
        UseForgePick,
        UseActiveItem,
        ReserveResource,
        ReserveSpace
    }

    public readonly struct AiActionCandidate
    {
        public AiActionCandidate(AiActionKind kind, string targetId, float baseScore)
        {
            Kind = kind;
            TargetId = targetId ?? string.Empty;
            BaseScore = baseScore;
        }

        public AiActionKind Kind { get; }
        public string TargetId { get; }
        public float BaseScore { get; }
    }

    /// <summary>
    /// Shared deterministic scoring seam. New deploy/item/rune planners should submit legal
    /// candidates here; this layer never executes actions or changes gameplay values.
    /// </summary>
    public static class AiActionScoring
    {
        public static float ApplyProfileError(
            AiActionCandidate candidate,
            AiStrategyProfile profile,
            IRunRandom random,
            int decisionOrdinal)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (random == null) throw new ArgumentNullException(nameof(random));
            float unit = random.NextUnit(
                "ai.action-score." + decisionOrdinal + "." + candidate.Kind + "." + candidate.TargetId);
            float signedError = ((unit * 2f) - 1f) * profile.ScoreError;
            return candidate.BaseScore * (1f + signedError);
        }
    }
}
