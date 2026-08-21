using System;
using System.Collections.Generic;
using DragonBound.Combat;

namespace DragonBound.Core
{
    public readonly struct EnemyDamageApplication
    {
        public EnemyDamageApplication(float requested, float shieldDamage, float healthDamage, bool killed)
        {
            Requested = requested;
            ShieldDamage = shieldDamage;
            HealthDamage = healthDamage;
            Killed = killed;
        }

        public float Requested { get; }
        public float ShieldDamage { get; }
        public float HealthDamage { get; }
        public bool Killed { get; }
    }

    public sealed class EnemyRuntime : IEnemyLifecycle, IPathProgress, ICombatTarget
    {
        public const float DefaultMaxHitPoints = 30f;

        public EnemyRuntime(
            string runtimeId,
            TeamSide team,
            float maxHitPoints = DefaultMaxHitPoints,
            EnemyArchetype archetype = EnemyArchetype.Normal,
            int spawnSequence = 0,
            string bossId = "")
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
            BossId = bossId ?? string.Empty;
            SpawnSequence = spawnSequence;
            MaxHitPoints = maxHitPoints;
            HitPoints = maxHitPoints;
            State = EnemyRuntimeState.Spawned;
        }

        public string RuntimeId { get; }
        public TeamSide Team { get; }
        public EnemyArchetype Archetype { get; }
        public int SpawnSequence { get; }
        public string BossId { get; }
        public int ExperienceReward => Archetype == EnemyArchetype.Boss
            ? BossExperienceRewards.Get(BossId)
            : Archetype == EnemyArchetype.Swarm ? 0
            : Archetype == EnemyArchetype.Elite ? 3 : 1;
        public int PathIndex { get; internal set; }
        /// <summary>
        /// Normalized cumulative distance over the active ordered enemy path. Zero is
        /// the spawn tile and one is the goal tile; targeting must never infer order
        /// from world X/Y coordinates.
        /// </summary>
        public float PathProgress { get; internal set; }
        public float SegmentProgress { get; internal set; }
        public float MaxHitPoints { get; private set; }
        public float HitPoints { get; internal set; }
        public CombatPoint CombatPosition { get; private set; }
        public EnemyRuntimeState State { get; internal set; }
        public bool HasResolved { get; internal set; }
        public CombatDamageOwner LastDamageOwner { get; private set; } = CombatDamageOwner.None;
        public bool IsAlive => !HasResolved && HitPoints > 0;
        public float StunRemainingSeconds { get; private set; }
        public float StunImmunityRemainingSeconds { get; private set; }
        public bool IsStunned => IsAlive && StunRemainingSeconds > 0.0001f;
        public float MovementSpeedMultiplier { get; private set; } = 1f;
        public float StormcallerShieldHitPoints { get; private set; }
        public float StormcallerMovementSpeedMultiplier { get; private set; } = 1f;
        public float StormcallerSpeedBuffRemainingSeconds { get; private set; }
        public float BaseMovementSpeedMultiplier { get; private set; } = 1f;
        public float MovementSlowRemainingSeconds { get; private set; }
        private float pendingPostStunImmunitySeconds;

        public void SetCombatPosition(CombatPoint position)
        {
            CombatPosition = position;
        }

        public void RecordDamageOwner(CombatDamageOwner owner)
        {
            // Hero skill runtimes apply damage before returning their result to the shared
            // settlement point. A zero-HP target still needs its final owner recorded until
            // ResolveKill marks it resolved.
            if (!HasResolved && owner.IsValid)
            {
                LastDamageOwner = owner;
            }
        }

        public EnemyDamageApplication ApplyDamage(float damage)
        {
            if (damage <= 0f || !IsAlive)
            {
                return new EnemyDamageApplication(Math.Max(0f, damage), 0f, 0f, !IsAlive);
            }

            var remaining = damage;
            var shieldDamage = Math.Min(StormcallerShieldHitPoints, remaining);
            StormcallerShieldHitPoints -= shieldDamage;
            remaining -= shieldDamage;
            var healthDamage = Math.Min(HitPoints, remaining);
            HitPoints = Math.Max(0f, HitPoints - healthDamage);
            return new EnemyDamageApplication(damage, shieldDamage, healthDamage, HitPoints <= 0.0001f);
        }

        public void IncreaseMaxHitPoints(float amount)
        {
            if (amount < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            MaxHitPoints += amount;
            HitPoints += amount;
        }

        public void ApplyStormcallerShield(float shieldValue)
        {
            if (shieldValue <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(shieldValue));
            }

            StormcallerShieldHitPoints = shieldValue;
        }

        public void ApplyStormcallerSpeedBuff(float multiplier, float durationSeconds)
        {
            if (multiplier <= 0f || durationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier));
            }

            StormcallerMovementSpeedMultiplier = multiplier;
            StormcallerSpeedBuffRemainingSeconds = Math.Max(
                StormcallerSpeedBuffRemainingSeconds,
                durationSeconds);
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

        public void SetBaseMovementSpeedMultiplier(float multiplier)
        {
            if (multiplier <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier));
            }

            BaseMovementSpeedMultiplier = multiplier;
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
            StormcallerSpeedBuffRemainingSeconds = Math.Max(0f, StormcallerSpeedBuffRemainingSeconds - deltaSeconds);
            if (StormcallerSpeedBuffRemainingSeconds <= 0.0001f)
            {
                StormcallerMovementSpeedMultiplier = 1f;
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
