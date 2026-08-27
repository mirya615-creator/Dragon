using System;
using System.Collections.Generic;
using DragonBound.Core;

namespace DragonBound.Combat
{
    public readonly struct CombatPoint : IEquatable<CombatPoint>
    {
        public CombatPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }

        public float DistanceSquared(CombatPoint other)
        {
            var x = X - other.X;
            var y = Y - other.Y;
            return (x * x) + (y * y);
        }

        public bool Equals(CombatPoint other)
        {
            return Math.Abs(X - other.X) <= 0.0001f && Math.Abs(Y - other.Y) <= 0.0001f;
        }

        public override bool Equals(object obj) => obj is CombatPoint other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public static CombatPoint Lerp(CombatPoint from, CombatPoint to, float progress)
        {
            var t = Math.Max(0f, Math.Min(1f, progress));
            return new CombatPoint(from.X + ((to.X - from.X) * t), from.Y + ((to.Y - from.Y) * t));
        }
    }

    public readonly struct RuneDamageApplication
    {
        public RuneDamageApplication(float requested, float shieldDamage, float healthDamage, bool killed)
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

    /// <summary>Target seam for Rune combat. Implementations remain owned by Combat/Enemies.</summary>
    public interface IRuneCombatTarget
    {
        string RuntimeId { get; }
        bool IsAlive { get; }
        bool IsBoss { get; }
        float PathProgress { get; }
        CombatPoint CombatPosition { get; }
        RuneDamageApplication ApplyRuneDamage(float damage);
        bool TryApplyRuneSlow(float slowFraction, float durationSeconds);
    }

    public interface IRuneCombatTargetRegistry
    {
        IReadOnlyList<IRuneCombatTarget> Snapshot();
    }
}

namespace DragonBound.Core
{
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
        SkyhunterRadianceSecondary,
        RuneRicochet,
        RuneLongshot,
        RuneVolleyBolt,
        RuneBladeTempest,
        RuneAmbush,
        RuneWindhawk,
        RuneSkybreakerPrimary,
        RuneSkybreakerSecondary,
        RuneWyrmguardSpirit,
        RuneDragonbloom,
        Item
    }

    public enum CombatDamageOwnerKind
    {
        None,
        BasicUnit,
        Hero,
        Item
    }

    public readonly struct CombatDamageOwner
    {
        public CombatDamageOwner(
            CombatDamageOwnerKind kind,
            TeamSide side,
            string sourceRuntimeId,
            string heroId = "")
        {
            Kind = kind;
            Side = side;
            SourceRuntimeId = sourceRuntimeId ?? string.Empty;
            HeroId = heroId ?? string.Empty;
        }

        public CombatDamageOwnerKind Kind { get; }
        public TeamSide Side { get; }
        public string SourceRuntimeId { get; }
        public string HeroId { get; }
        public bool IsValid => Kind != CombatDamageOwnerKind.None && !string.IsNullOrEmpty(SourceRuntimeId);
        public static CombatDamageOwner None => new CombatDamageOwner(CombatDamageOwnerKind.None, TeamSide.Player, string.Empty);
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
            float effectRadius = 0f,
            CombatDamageOwnerKind damageOwnerKind = CombatDamageOwnerKind.None,
            string damageOwnerRuntimeId = "",
            string damageOwnerHeroId = "",
            int experienceReward = 0,
            int heroXpAwarded = 0,
            int damageOwnerHeroLevel = 0,
            float shieldDamage = 0f,
            float healthDamage = 0f)
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
            DamageOwnerKind = damageOwnerKind;
            DamageOwnerRuntimeId = damageOwnerRuntimeId ?? string.Empty;
            DamageOwnerHeroId = damageOwnerHeroId ?? string.Empty;
            ExperienceReward = experienceReward;
            HeroXpAwarded = heroXpAwarded;
            DamageOwnerHeroLevel = damageOwnerHeroLevel;
            ShieldDamage = shieldDamage;
            HealthDamage = healthDamage;
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
        public CombatDamageOwnerKind DamageOwnerKind { get; }
        public string DamageOwnerRuntimeId { get; }
        public string DamageOwnerHeroId { get; }
        public int ExperienceReward { get; }
        public int HeroXpAwarded { get; }
        public int DamageOwnerHeroLevel { get; }
        public float ShieldDamage { get; }
        public float HealthDamage { get; }
    }

    public interface ICombatTarget
    {
        string RuntimeId { get; }
        TeamSide Team { get; }
        float MaxHitPoints { get; }
        float HitPoints { get; }
        bool IsAlive { get; }
        DragonBound.Combat.CombatPoint CombatPosition { get; }
    }

    public interface IDamageResultPort
    {
        AttackKind Kind { get; }
        string TargetRuntimeId { get; }
        float Damage { get; }
        bool Killed { get; }
    }

    public interface ICombatEventSink
    {
        void Emit(CombatEvent value);
    }
}
