using System;
using System.Collections.Generic;
using DragonBound.Combat;

namespace DragonBound.Core
{
    public enum EnemyRuntimeState
    {
        Spawned,
        Moving,
        Dead,
        Leaked
    }

    public enum AttackKind
    {
        Single,
        BowProjectile,
        SpearPierce,
        RiderSweep,
        WindclawShot,
        WindclawPowerShot,
        EmberShamanArea,
        EmberGround,
        DragonRiderArea,
        DragonRiderDive,
        DragonRiderFlame,
        RuneboltPierce,
        StonebinderShot,
        StoneBind,
        StarfallArea,
        StarfallTelegraph,
        StarfallImpact,
        CrownSwordStrike,
        CrownHunterShot,
        HuntMark,
        ThunderJarlChain,
        ThunderDominion,
        EmberExplosiveFireball,
        EmberExplosiveSplash,
        NightfangStrike,
        NightfangExecutionSlash,
        LeviathanHarpoon,
        AbyssHarpoonWarning,
        AbyssHarpoonStrike,
        SkyhunterShot,
        SkyhunterRadiancePrimary,
        SkyhunterRadianceSecondary
    }

    public enum EnemyArchetype
    {
        Normal,
        Fast,
        Swarm,
        Elite,
        Boss
    }

    public sealed class EnemyRuntime
    {
        public EnemyRuntime(
            string runtimeId,
            TeamSide team,
            float maxHitPoints = 30f,
            EnemyArchetype archetype = EnemyArchetype.Normal)
        {
            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                throw new ArgumentException("An enemy runtime id is required.", nameof(runtimeId));
            }

            if (maxHitPoints <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHitPoints));
            }

            RuntimeId = runtimeId;
            Team = team;
            Archetype = archetype;
            MaxHitPoints = maxHitPoints;
            HitPoints = maxHitPoints;
            State = EnemyRuntimeState.Spawned;
        }

        public string RuntimeId { get; }
        public TeamSide Team { get; }
        public EnemyArchetype Archetype { get; }
        public int ExperienceReward => Archetype == EnemyArchetype.Elite ? 3 : 1;
        public int PathIndex { get; internal set; }
        /// <summary>
        /// Normalized cumulative distance over the active ordered enemy path. Zero is
        /// the spawn tile and one is the goal tile; targeting must never infer order
        /// from world X/Y coordinates.
        /// </summary>
        public float PathProgress { get; internal set; }
        public float SegmentProgress { get; internal set; }
        public float MaxHitPoints { get; }
        public float HitPoints { get; internal set; }
        public CombatPoint CombatPosition { get; private set; }
        public EnemyRuntimeState State { get; internal set; }
        public bool HasResolved { get; internal set; }
        public bool IsAlive => !HasResolved && HitPoints > 0;
        public float StunRemainingSeconds { get; private set; }
        public float StunImmunityRemainingSeconds { get; private set; }
        public bool IsStunned => IsAlive && StunRemainingSeconds > 0.0001f;
        public float MovementSpeedMultiplier { get; private set; } = 1f;
        public float MovementSlowRemainingSeconds { get; private set; }
        private float pendingPostStunImmunitySeconds;

        public void SetCombatPosition(CombatPoint position)
        {
            CombatPosition = position;
        }

        public void SetTargetingState(int pathIndex, float pathProgress, CombatPoint position)
        {
            if (pathIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pathIndex));
            }

            if (pathProgress < 0f || pathProgress > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(pathProgress));
            }

            PathIndex = pathIndex;
            PathProgress = pathProgress;
            SegmentProgress = pathProgress;
            CombatPosition = position;
        }

        public bool ApplyStun(float durationSeconds, float postStunImmunitySeconds = 0f)
        {
            if (!IsAlive || durationSeconds <= 0f || StunImmunityRemainingSeconds > 0.0001f)
            {
                return false;
            }

            StunRemainingSeconds = Math.Max(StunRemainingSeconds, durationSeconds);
            pendingPostStunImmunitySeconds = Math.Max(
                pendingPostStunImmunitySeconds,
                postStunImmunitySeconds);

            return true;
        }

        public bool ApplyMovementSlow(float slowFraction, float durationSeconds)
        {
            if (!IsAlive || slowFraction <= 0f || durationSeconds <= 0f)
            {
                return false;
            }

            var multiplier = Math.Max(0f, 1f - Math.Min(1f, slowFraction));
            MovementSpeedMultiplier = Math.Min(MovementSpeedMultiplier, multiplier);
            MovementSlowRemainingSeconds = Math.Max(MovementSlowRemainingSeconds, durationSeconds);
            return true;
        }

        public void TickControl(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
            {
                return;
            }

            var wasStunned = StunRemainingSeconds > 0.0001f;
            StunRemainingSeconds = Math.Max(0f, StunRemainingSeconds - deltaSeconds);
            if (wasStunned && StunRemainingSeconds <= 0.0001f && pendingPostStunImmunitySeconds > 0f)
            {
                StunImmunityRemainingSeconds = Math.Max(
                    StunImmunityRemainingSeconds,
                    pendingPostStunImmunitySeconds);
                pendingPostStunImmunitySeconds = 0f;
                // Control state resolves at the end of a simulation tick. Start the full
                // post-stun window on the following tick instead of charging it for the
                // frame that released the stun.
                return;
            }
            StunImmunityRemainingSeconds = Math.Max(
                0f,
                StunImmunityRemainingSeconds - deltaSeconds);
            MovementSlowRemainingSeconds = Math.Max(0f, MovementSlowRemainingSeconds - deltaSeconds);
            if (MovementSlowRemainingSeconds <= 0.0001f)
            {
                MovementSpeedMultiplier = 1f;
            }
        }

        internal void SetPathState(
            int pathIndex,
            float segmentProgress,
            float normalizedPathProgress,
            int goalIndex,
            CombatPoint position)
        {
            if (pathIndex < 0 || goalIndex < 1 || pathIndex > goalIndex ||
                segmentProgress < 0f || segmentProgress > 1f ||
                normalizedPathProgress < 0f || normalizedPathProgress > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(pathIndex));
            }

            PathIndex = pathIndex;
            SegmentProgress = segmentProgress;
            PathProgress = normalizedPathProgress;
            CombatPosition = position;
        }
    }

    public readonly struct CombatEvent
    {
        public CombatEvent(
            TeamSide team,
            AttackKind kind,
            string attackerRuntimeId,
            string targetRuntimeId,
            float damage,
            bool killed,
            bool leaked,
            int resourcesAfter,
            float effectDuration = 0f,
            float effectRadius = 0f)
        {
            Team = team;
            Kind = kind;
            AttackerRuntimeId = attackerRuntimeId;
            TargetRuntimeId = targetRuntimeId;
            Damage = damage;
            Killed = killed;
            Leaked = leaked;
            ResourcesAfter = resourcesAfter;
            EffectDuration = effectDuration;
            EffectRadius = effectRadius;
        }

        public TeamSide Team { get; }
        public AttackKind Kind { get; }
        public string AttackerRuntimeId { get; }
        public string TargetRuntimeId { get; }
        public float Damage { get; }
        public bool Killed { get; }
        public bool Leaked { get; }
        public int ResourcesAfter { get; }
        public float EffectDuration { get; }
        public float EffectRadius { get; }
    }

    public sealed class EnemyRegistry
    {
        private readonly Dictionary<string, EnemyRuntime> enemies =
            new Dictionary<string, EnemyRuntime>(StringComparer.Ordinal);

        public int Count => enemies.Count;
        public IReadOnlyCollection<EnemyRuntime> Enemies => enemies.Values;

        public bool Register(EnemyRuntime enemy)
        {
            if (enemy == null || enemies.ContainsKey(enemy.RuntimeId))
            {
                return false;
            }

            enemies.Add(enemy.RuntimeId, enemy);
            return true;
        }

        public bool TryGet(string runtimeId, out EnemyRuntime enemy)
        {
            return enemies.TryGetValue(runtimeId, out enemy);
        }

        public bool Remove(string runtimeId, out EnemyRuntime enemy)
        {
            if (!enemies.TryGetValue(runtimeId, out enemy))
            {
                return false;
            }

            enemies.Remove(runtimeId);
            return true;
        }

        public List<EnemyRuntime> Snapshot()
        {
            return new List<EnemyRuntime>(enemies.Values);
        }

        public void Clear()
        {
            enemies.Clear();
        }
    }
}
