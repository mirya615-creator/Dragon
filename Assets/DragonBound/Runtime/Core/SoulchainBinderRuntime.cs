using System;
using System.Collections.Generic;
using DragonBound.Grid;
using DragonBound.Recruitment;
using GameShared.Random;

namespace DragonBound.Core
{
    public static class SoulchainBinderConfiguration
    {
        public const string BossId = "BOSS_SOULCHAIN_BINDER";
        public const string SkillId = "SOULCHAIN";
        // Retained name for compatibility with existing telemetry and diagnostics. This is now
        // the single shared Production W6 HP source of truth.
        public const float GreyboxMaxHitPoints = 600f;
        public const float BossMoveSpeedCellsPerSecond = 0.20f;
        public const float FirstCastDelaySeconds = 8f;
        public const float CastWindupSeconds = 0.5f;
        public const float EffectDurationSeconds = 2f;
        public const float CooldownSeconds = 15f;
        public const float FullCastCycleSeconds = 17.5f;
        public const int MaxAffectedBasic = 2;
        public const bool PausesSimulation = false;
        public const bool MoveDuringCast = true;
        public const bool FormalHitPointsPending = false;
    }

    public readonly struct SoulChainBasicCandidate
    {
        public SoulChainBasicCandidate(string runtimeId, GridPosition cell, bool isAlive = true)
        {
            RuntimeId = runtimeId ?? string.Empty;
            Cell = cell;
            IsAlive = isAlive;
        }

        public string RuntimeId { get; }
        public GridPosition Cell { get; }
        public bool IsAlive { get; }
    }

    public interface ISoulChainTargetProvider
    {
        IReadOnlyList<SoulChainBasicCandidate> GetBasicCandidates();
        bool SetAttackDisabled(string runtimeId, bool disabled);
    }

    public sealed class BoardSoulChainTargetProvider : ISoulChainTargetProvider
    {
        private readonly BoardRecruitDestination destination;

        public BoardSoulChainTargetProvider(BoardRecruitDestination destination)
        {
            this.destination = destination ?? throw new ArgumentNullException(nameof(destination));
        }

        public IReadOnlyList<SoulChainBasicCandidate> GetBasicCandidates()
        {
            var deployed = destination.GetDeployedUnits();
            var result = new List<SoulChainBasicCandidate>(deployed.Count);
            for (var index = 0; index < deployed.Count; index++)
            {
                var unit = deployed[index];
                result.Add(new SoulChainBasicCandidate(unit.Card.RuntimeId, unit.GridPosition, true));
            }

            return result;
        }

        public bool SetAttackDisabled(string runtimeId, bool disabled)
        {
            return destination.SetCombatSuspended(runtimeId, disabled);
        }

        /// <summary>
        /// Returns every legal contiguous 2x2 battlefield area that currently contains at
        /// least one deployed Basic. Bench cells are deliberately excluded.
        /// </summary>
        public IReadOnlyList<GridPosition> GetEligibleRegionAnchors()
        {
            var anchors = new List<GridPosition>();
            var deployed = GetBasicCandidates();
            for (var index = 0; index < deployed.Count; index++)
            {
                var cell = deployed[index].Cell;
                for (var offsetX = -1; offsetX <= 0; offsetX++)
                {
                    for (var offsetY = -1; offsetY <= 0; offsetY++)
                    {
                        var anchor = new GridPosition(cell.X + offsetX, cell.Y + offsetY);
                        if (!Contains(anchors, anchor) && IsLegalBattleRegion(anchor))
                        {
                            anchors.Add(anchor);
                        }
                    }
                }
            }

            anchors.Sort();
            return anchors;
        }

        private bool IsLegalBattleRegion(GridPosition anchor)
        {
            for (var x = 0; x <= 1; x++)
            {
                for (var y = 0; y <= 1; y++)
                {
                    if (!destination.Board.TryGetCellType(
                            new GridPosition(anchor.X + x, anchor.Y + y),
                            out var cellType) ||
                        cellType == CellType.Bench)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool Contains(List<GridPosition> positions, GridPosition candidate)
        {
            for (var index = 0; index < positions.Count; index++)
            {
                if (positions[index] == candidate)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class EmptySoulChainTargetProvider : ISoulChainTargetProvider
    {
        private static readonly IReadOnlyList<SoulChainBasicCandidate> Empty =
            new List<SoulChainBasicCandidate>();

        public IReadOnlyList<SoulChainBasicCandidate> GetBasicCandidates()
        {
            return Empty;
        }

        public bool SetAttackDisabled(string runtimeId, bool disabled)
        {
            return false;
        }
    }

    public readonly struct SoulChainBossCastContext
    {
        public SoulChainBossCastContext(string bossId, TeamSide side, int castNumber, float maxHitPoints)
        {
            BossId = bossId ?? string.Empty;
            Side = side;
            CastNumber = castNumber;
            MaxHitPoints = maxHitPoints;
        }

        public string BossId { get; }
        public TeamSide Side { get; }
        public int CastNumber { get; }
        public float MaxHitPoints { get; }
    }

    public interface ISoulChainSpellbreakerResolver
    {
        bool ShouldBlockCast(SoulChainBossCastContext context);
    }

    public enum SoulChainCastEventKind
    {
        CastStarted,
        WindupResolved,
        EffectApplied,
        EffectEnded,
        CastFailed,
        CooldownStarted,
        BossDeathCleared
    }

    public readonly struct SoulChainCastEvent
    {
        public SoulChainCastEvent(
            SoulChainCastEventKind kind,
            int castNumber,
            float elapsedSeconds,
            int affectedCount,
            float controlUnitSeconds,
            GridPosition regionAnchor,
            float reflectionDamage)
        {
            Kind = kind;
            CastNumber = castNumber;
            ElapsedSeconds = elapsedSeconds;
            AffectedCount = affectedCount;
            ControlUnitSeconds = controlUnitSeconds;
            RegionAnchor = regionAnchor;
            ReflectionDamage = reflectionDamage;
        }

        public SoulChainCastEventKind Kind { get; }
        public int CastNumber { get; }
        public float ElapsedSeconds { get; }
        public int AffectedCount { get; }
        public float ControlUnitSeconds { get; }
        public GridPosition RegionAnchor { get; }
        public float ReflectionDamage { get; }
    }

    public sealed class SoulChainController
    {
        private readonly EnemyRuntime boss;
        private readonly TeamSide side;
        private readonly ISoulChainTargetProvider targets;
        private readonly ISoulChainSpellbreakerResolver spellbreaker;
        private readonly IRunRandom random;
        private readonly Dictionary<string, float> activeRemaining =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private float elapsedSeconds;
        private float nextCastStart = SoulchainBinderConfiguration.FirstCastDelaySeconds;
        private float castStart;
        private float effectEnd;
        private float cooldownEnd;
        private bool castActive;
        private bool windupResolved;
        private bool effectActive;
        private bool bossDeathNotified;
        private GridPosition selectedRegionAnchor;

        public SoulChainController(
            EnemyRuntime boss,
            TeamSide side,
            ISoulChainTargetProvider targets,
            int runSeed,
            ISoulChainSpellbreakerResolver spellbreaker = null,
            bool enabled = true)
        {
            this.boss = boss ?? throw new ArgumentNullException(nameof(boss));
            this.side = side;
            this.targets = targets ?? throw new ArgumentNullException(nameof(targets));
            this.spellbreaker = spellbreaker;
            random = new RunRandom(DeriveSeed(runSeed, side));
            Enabled = enabled;
        }

        public bool Enabled { get; }
        public float ElapsedSeconds => elapsedSeconds;
        public bool IsCasting => castActive;
        public bool IsEffectActive => effectActive;
        public float CooldownRemainingSeconds => Math.Max(0f, cooldownEnd - elapsedSeconds);
        public int CastsStarted { get; private set; }
        public int CastsSucceeded { get; private set; }
        public int CastsFailed { get; private set; }
        public int LastAffectedCount { get; private set; }
        public float LastControlUnitSeconds { get; private set; }
        public GridPosition SelectedRegionAnchor => selectedRegionAnchor;
        public event Action<SoulChainCastEvent> CastEvent;

        public float GetRemainingControl(string runtimeId)
        {
            float remaining;
            return activeRemaining.TryGetValue(runtimeId, out remaining) ? remaining : 0f;
        }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || bossDeathNotified)
            {
                return;
            }

            if (!boss.IsAlive)
            {
                NotifyBossDeath();
                return;
            }

            // A disabled comparison run still advances time. It must not walk into the
            // first cast boundary, otherwise the boundary has no state transition and the
            // scheduler can repeatedly process a zero-length step.
            if (!Enabled)
            {
                TickActiveControl(deltaSeconds);
                elapsedSeconds += deltaSeconds;
                return;
            }

            var targetTime = elapsedSeconds + deltaSeconds;
            while (elapsedSeconds < targetTime - 0.0001f && !bossDeathNotified)
            {
                var nextBoundary = targetTime;
                if (!castActive)
                {
                    nextBoundary = Math.Min(nextBoundary, nextCastStart);
                }
                else if (!windupResolved)
                {
                    nextBoundary = Math.Min(nextBoundary, castStart + SoulchainBinderConfiguration.CastWindupSeconds);
                }
                else if (effectActive)
                {
                    nextBoundary = Math.Min(nextBoundary, effectEnd);
                }

                var step = Math.Max(0f, nextBoundary - elapsedSeconds);
                TickActiveControl(step);
                elapsedSeconds = nextBoundary;
                ProcessBoundary();
            }

            if (elapsedSeconds < targetTime - 0.0001f)
            {
                TickActiveControl(targetTime - elapsedSeconds);
                elapsedSeconds = targetTime;
                ProcessBoundary();
            }
        }

        public void NotifyMerge(string sourceRuntimeId, string targetRuntimeId)
        {
            if (string.IsNullOrWhiteSpace(sourceRuntimeId) || string.IsNullOrWhiteSpace(targetRuntimeId) ||
                string.Equals(sourceRuntimeId, targetRuntimeId, StringComparison.Ordinal))
            {
                return;
            }

            var sourceRemaining = GetRemainingControl(sourceRuntimeId);
            var targetRemaining = GetRemainingControl(targetRuntimeId);
            if (sourceRemaining <= 0f && targetRemaining <= 0f)
            {
                return;
            }

            targets.SetAttackDisabled(sourceRuntimeId, false);
            activeRemaining.Remove(sourceRuntimeId);
            var inherited = Math.Max(sourceRemaining, targetRemaining);
            activeRemaining[targetRuntimeId] = inherited;
            targets.SetAttackDisabled(targetRuntimeId, true);
        }

        public void NotifyBossDeath()
        {
            if (bossDeathNotified)
            {
                return;
            }

            bossDeathNotified = true;
            foreach (var runtimeId in new List<string>(activeRemaining.Keys))
            {
                targets.SetAttackDisabled(runtimeId, false);
            }

            activeRemaining.Clear();
            castActive = false;
            effectActive = false;
            Emit(SoulChainCastEventKind.BossDeathCleared, 0f);
        }

        private void ProcessBoundary()
        {
            if (!castActive && elapsedSeconds >= nextCastStart - 0.0001f)
            {
                BeginCast();
            }

            if (castActive && !windupResolved && elapsedSeconds >= castStart + SoulchainBinderConfiguration.CastWindupSeconds - 0.0001f)
            {
                ResolveWindup();
            }

            if (castActive && effectActive && elapsedSeconds >= effectEnd - 0.0001f)
            {
                EndEffect();
            }
        }

        private void BeginCast()
        {
            castActive = true;
            windupResolved = false;
            effectActive = false;
            castStart = elapsedSeconds;
            CastsStarted++;
            selectedRegionAnchor = SelectRegionAnchor();
            Emit(SoulChainCastEventKind.CastStarted, 0f);
        }

        private void ResolveWindup()
        {
            windupResolved = true;
            Emit(SoulChainCastEventKind.WindupResolved, 0f);
            var context = new SoulChainBossCastContext(
                SoulchainBinderConfiguration.BossId,
                side,
                CastsStarted,
                boss.MaxHitPoints);
            if (spellbreaker != null && spellbreaker.ShouldBlockCast(context))
            {
                var reflectionDamage = boss.MaxHitPoints * 0.10f;
                boss.ApplyDamage(reflectionDamage);
                CastsFailed++;
                LastAffectedCount = 0;
                LastControlUnitSeconds = 0f;
                Emit(SoulChainCastEventKind.CastFailed, reflectionDamage);
                StartCooldownFromFailure();
                if (!boss.IsAlive)
                {
                    NotifyBossDeath();
                }
                return;
            }

            var eligible = GetCandidatesInSelectedRegion();
            Shuffle(eligible);
            if (eligible.Count > SoulchainBinderConfiguration.MaxAffectedBasic)
            {
                eligible.RemoveRange(
                    SoulchainBinderConfiguration.MaxAffectedBasic,
                    eligible.Count - SoulchainBinderConfiguration.MaxAffectedBasic);
            }

            LastAffectedCount = 0;
            for (var index = 0; index < eligible.Count; index++)
            {
                if (targets.SetAttackDisabled(eligible[index].RuntimeId, true))
                {
                    activeRemaining[eligible[index].RuntimeId] = SoulchainBinderConfiguration.EffectDurationSeconds;
                    LastAffectedCount++;
                }
            }

            LastControlUnitSeconds = LastAffectedCount * SoulchainBinderConfiguration.EffectDurationSeconds;
            CastsSucceeded++;
            effectActive = true;
            effectEnd = elapsedSeconds + SoulchainBinderConfiguration.EffectDurationSeconds;
            Emit(SoulChainCastEventKind.EffectApplied, 0f);
        }

        private void EndEffect()
        {
            effectActive = false;
            castActive = false;
            ClearExpiredControl();
            Emit(SoulChainCastEventKind.EffectEnded, 0f);
            cooldownEnd = elapsedSeconds + SoulchainBinderConfiguration.CooldownSeconds;
            nextCastStart = cooldownEnd;
            Emit(SoulChainCastEventKind.CooldownStarted, 0f);
        }

        private void StartCooldownFromFailure()
        {
            castActive = false;
            cooldownEnd = elapsedSeconds + SoulchainBinderConfiguration.CooldownSeconds;
            nextCastStart = cooldownEnd;
            Emit(SoulChainCastEventKind.CooldownStarted, 0f);
        }

        private GridPosition SelectRegionAnchor()
        {
            if (targets is BoardSoulChainTargetProvider boardTargets)
            {
                var legalAnchors = boardTargets.GetEligibleRegionAnchors();
                return legalAnchors.Count == 0
                    ? default
                    : legalAnchors[random.NextInt("SoulChain.Region", 0, legalAnchors.Count)];
            }

            var candidates = targets.GetBasicCandidates();
            var anchors = new List<GridPosition>();
            for (var index = 0; index < candidates.Count; index++)
            {
                if (!candidates[index].IsAlive)
                {
                    continue;
                }

                // Non-board providers are used by deterministic diagnostics/tests. Generate
                // all four possible 2x2 anchors around each Basic instead of biasing the
                // selection toward areas whose upper-left cell happens to be occupied.
                for (var offsetX = -1; offsetX <= 0; offsetX++)
                {
                    for (var offsetY = -1; offsetY <= 0; offsetY++)
                    {
                        var anchor = new GridPosition(
                            candidates[index].Cell.X + offsetX,
                            candidates[index].Cell.Y + offsetY);
                        if (!Contains(anchors, anchor))
                        {
                            anchors.Add(anchor);
                        }
                    }
                }
            }

            anchors.Sort();
            if (anchors.Count == 0)
            {
                return default;
            }

            return anchors[random.NextInt("SoulChain.Region", 0, anchors.Count)];
        }

        private List<SoulChainBasicCandidate> GetCandidatesInSelectedRegion()
        {
            var result = new List<SoulChainBasicCandidate>();
            var candidates = targets.GetBasicCandidates();
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (candidate.IsAlive &&
                    candidate.Cell.X >= selectedRegionAnchor.X && candidate.Cell.X <= selectedRegionAnchor.X + 1 &&
                    candidate.Cell.Y >= selectedRegionAnchor.Y && candidate.Cell.Y <= selectedRegionAnchor.Y + 1)
                {
                    result.Add(candidate);
                }
            }

            return result;
        }

        private void Shuffle(List<SoulChainBasicCandidate> values)
        {
            for (var index = values.Count - 1; index > 0; index--)
            {
                var swap = random.NextInt("SoulChain.Target", 0, index + 1);
                var value = values[index];
                values[index] = values[swap];
                values[swap] = value;
            }
        }

        private void TickActiveControl(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || activeRemaining.Count == 0)
            {
                return;
            }

            var expired = new List<string>();
            // Dictionary values cannot be updated while enumerating the live collection.
            // Iterate a stable snapshot so expiry and merge callbacks remain deterministic.
            foreach (var entry in new List<KeyValuePair<string, float>>(activeRemaining))
            {
                var remaining = entry.Value - deltaSeconds;
                if (remaining <= 0.0001f)
                {
                    expired.Add(entry.Key);
                }
                else
                {
                    activeRemaining[entry.Key] = remaining;
                }
            }

            for (var index = 0; index < expired.Count; index++)
            {
                targets.SetAttackDisabled(expired[index], false);
                activeRemaining.Remove(expired[index]);
            }
        }

        private void ClearExpiredControl()
        {
            foreach (var runtimeId in new List<string>(activeRemaining.Keys))
            {
                targets.SetAttackDisabled(runtimeId, false);
            }

            activeRemaining.Clear();
        }

        private void Emit(SoulChainCastEventKind kind, float reflectionDamage)
        {
            CastEvent?.Invoke(new SoulChainCastEvent(
                kind,
                CastsStarted,
                elapsedSeconds,
                LastAffectedCount,
                LastControlUnitSeconds,
                selectedRegionAnchor,
                reflectionDamage));
        }

        private static bool Contains(List<GridPosition> positions, GridPosition candidate)
        {
            for (var index = 0; index < positions.Count; index++)
            {
                if (positions[index] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private static int DeriveSeed(int runSeed, TeamSide side)
        {
            unchecked
            {
                var value = runSeed * 397;
                return value ^ (side == TeamSide.Player ? 0x51A3 : 0xA315);
            }
        }
    }

    public sealed class SoulchainBinderRuntime
    {
        public SoulchainBinderRuntime(
            EnemyRuntime boss,
            TeamSide side,
            BoardRecruitDestination destination,
            int runSeed,
            ISoulChainSpellbreakerResolver spellbreaker = null,
            bool soulChainEnabled = true)
        {
            Boss = boss ?? throw new ArgumentNullException(nameof(boss));
            SoulChain = new SoulChainController(
                boss,
                side,
                destination == null
                    ? (ISoulChainTargetProvider)new EmptySoulChainTargetProvider()
                    : new BoardSoulChainTargetProvider(destination),
                runSeed,
                spellbreaker,
                soulChainEnabled);
        }

        public EnemyRuntime Boss { get; }
        public SoulChainController SoulChain { get; }

        public void Tick(float deltaSeconds)
        {
            SoulChain.Tick(deltaSeconds);
        }

        public void NotifyMerge(string sourceRuntimeId, string targetRuntimeId)
        {
            SoulChain.NotifyMerge(sourceRuntimeId, targetRuntimeId);
        }

        public void NotifyBossDeath()
        {
            SoulChain.NotifyBossDeath();
        }
    }
}
