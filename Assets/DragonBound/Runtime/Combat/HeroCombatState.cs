using System;
using System.Collections.Generic;
using System.Linq;
using DragonBound.Core;
using DragonBound.Recruitment;
using DragonBound.Runes;
using GameShared.Random;

namespace DragonBound.Combat
{
    public readonly struct HeroDamageResult : IDamageResultPort
    {
        public HeroDamageResult(
            AttackKind kind,
            EnemyRuntime target,
            float damage,
            bool killed,
            float effectDuration = 0f,
            float effectRadius = 0f,
            bool isRuneDerived = false,
            float shieldDamage = 0f,
            float healthDamage = 0f)
        {
            Kind = kind;
            Target = target;
            Damage = damage;
            Killed = killed;
            EffectDuration = effectDuration;
            EffectRadius = effectRadius;
            IsRuneDerived = isRuneDerived;
            ShieldDamage = shieldDamage;
            HealthDamage = healthDamage;
        }

        public AttackKind Kind { get; }
        public EnemyRuntime Target { get; }
        public string TargetRuntimeId => Target == null ? string.Empty : Target.RuntimeId;
        public float Damage { get; }
        public bool Killed { get; }
        public float EffectDuration { get; }
        public float EffectRadius { get; }
        public bool IsRuneDerived { get; }
        public float ShieldDamage { get; }
        public float HealthDamage { get; }
    }

    internal readonly struct AbyssHarpoonDirectionSelection
    {
        public AbyssHarpoonDirectionSelection(
            EnemyRuntime anchor,
            CombatPoint direction,
            IReadOnlyList<EnemyRuntime> targets)
        {
            Anchor = anchor;
            Direction = direction;
            Targets = targets;
        }

        public EnemyRuntime Anchor { get; }
        public CombatPoint Direction { get; }
        public IReadOnlyList<EnemyRuntime> Targets { get; }
    }

    public sealed class HeroProgressionState : IHeroProgression
    {
        private readonly HeroDefinition definition;

        public HeroProgressionState(string heroId)
        {
            definition = HeroSliceCatalog.Get(heroId);
            HeroId = heroId;
            Level = 1;
        }

        public string HeroId { get; }
        public int Experience { get; private set; }
        public int Level { get; private set; }

        public bool AddExperience(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            if (amount == 0 || Level >= definition.MaxLevel)
            {
                return false;
            }

            var previousLevel = Level;
            Experience = Math.Min(
                definition.GetLevelStats(definition.MaxLevel).RequiredExperience,
                checked(Experience + amount));
            Level = definition.GetLevelForExperience(Experience);
            return Level != previousLevel;
        }
    }

    public sealed class HeroCombatState
    {
        public const float FormationDurationSeconds = 0.6f;

        private readonly HeroDefinition definition;
        private readonly SkillDefinition skill;
        private readonly HeroProgressionState progression;
        private readonly TargetingSystem targeting = new TargetingSystem();
        private readonly GroundHazardSystem groundHazards = new GroundHazardSystem();
        private readonly TeamSide side;
        private readonly string sourceRuntimeId;
        private readonly string sourceRecipeId;
        private float formationElapsed;
        private float attackElapsed;
        private float skillElapsed;
        private int attackNumber;
        private int stoneBindAttackCount;
        private string duelMomentumTargetRuntimeId;
        private int duelMomentumStacks;
        private string huntMarkTargetRuntimeId;
        private string skyHuntTargetRuntimeId;
        private int skyHuntStacks;
        private float starfallTelegraphElapsed;
        private CombatPoint starfallTelegraphCenter;
        private bool starfallTelegraphActive;
        private bool shadowExecutionActive;
        private float shadowExecutionElapsed;
        private int shadowExecutionNextHit;
        private int shadowExecutionRetargetCount;
        private string shadowExecutionTargetRuntimeId;
        private bool abyssHarpoonWarningActive;
        private float abyssHarpoonWarningElapsed;
        private CombatPoint abyssHarpoonDirection;
        private string abyssHarpoonAnchorRuntimeId;
        private bool skyhunterRadianceActive;
        private float skyhunterRadianceElapsed;
        private string skyhunterRadiancePrimaryRuntimeId;
        private RuneDefinition runeDefinition;
        private float temporaryRuneAttackSpeedMultiplier = 1f;
        private float temporaryRuneAttackSpeedRemaining;
        private readonly List<EnemyRuntime> basicAttackSucceededTargets = new List<EnemyRuntime>();

        public HeroCombatState(string heroId, bool formationComplete = false)
            : this(
                heroId,
                new HeroProgressionState(heroId),
                formationComplete,
                TeamSide.Player,
                "hero." + heroId,
                heroId)
        {
        }

        public HeroCombatState(
            string heroId,
            HeroProgressionState progression,
            bool formationComplete = false)
            : this(
                heroId,
                progression,
                formationComplete,
                TeamSide.Player,
                "hero." + heroId,
                heroId)
        {
        }

        public HeroCombatState(
            string heroId,
            HeroProgressionState progression,
            bool formationComplete,
            TeamSide side,
            string sourceRuntimeId,
            string sourceRecipeId)
        {
            if (string.IsNullOrWhiteSpace(sourceRuntimeId) || string.IsNullOrWhiteSpace(sourceRecipeId))
            {
                throw new ArgumentException("Hero combat requires source runtime and recipe ids.");
            }

            definition = HeroSliceCatalog.Get(heroId);
            skill = FrozenHeroConfigurationCatalog.GetSkill(definition.SkillId);
            this.progression = progression ?? throw new ArgumentNullException(nameof(progression));
            if (!string.Equals(progression.HeroId, heroId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Hero combat and progression ids must match.", nameof(progression));
            }

            this.side = side;
            this.sourceRuntimeId = sourceRuntimeId;
            this.sourceRecipeId = sourceRecipeId;
            IsFormationComplete = formationComplete;
            formationElapsed = formationComplete ? FormationDurationSeconds : 0f;
        }

        public HeroDefinition Definition => definition;
        public bool IsFormationComplete { get; private set; }
        public bool IsCombatSuspended { get; private set; }
        public int Experience => progression.Experience;
        public int Level => progression.Level;
        public int AttackNumber => attackNumber;
        public int StoneBindAttackCount => stoneBindAttackCount;
        public int DuelMomentumStacks => duelMomentumStacks;
        public string HuntMarkTargetRuntimeId => huntMarkTargetRuntimeId;
        public int SkyHuntStacks => skyHuntStacks;
        public string SkyHuntTargetRuntimeId => skyHuntTargetRuntimeId;
        public string CurrentTargetRuntimeId { get; private set; }
        public float Attack
        {
            get
            {
                var input = new RuneModifierInput
                {
                    BaseAttackDamage = definition.BaseAttack,
                    HeroLevelMultiplier = definition.GetLevelStats(Level).AttackMultiplier
                };
                return RuneModifierPipeline.Evaluate(input, runeDefinition).AttackDamage;
            }
        }

        private float RuneModifiedBaseAttack
        {
            get
            {
                var input = new RuneModifierInput
                {
                    BaseAttackDamage = definition.BaseAttack,
                    HeroLevelMultiplier = 1f
                };
                return RuneModifierPipeline.Evaluate(input, runeDefinition).AttackDamage;
            }
        }
        public float AttackSpeed
        {
            get
            {
                var speed = definition.BaseAttackSpeed * definition.GetLevelStats(Level).AttackSpeedMultiplier;
                if (definition.Id == HeroSliceCatalog.SkyhunterValkyrieHeroId)
                {
                    speed *= 1f + (GetScalar(skill, "AttackSpeedBonusPerStack", 0.06f) * skyHuntStacks);
                }

                return speed * temporaryRuneAttackSpeedMultiplier;
            }
        }
        public float RangeCells => definition.RangeCells +
                                   (runeDefinition != null && runeDefinition.EffectType == RuneEffectType.AttackRangeFlat
                                       ? runeDefinition.GetParameter("RangeCells", runeDefinition.Parameter)
                                       : 0f);
        public IReadOnlyList<EnemyRuntime> BasicAttackSucceededTargets => basicAttackSucceededTargets;
        public int ActiveGroundHazardCount => groundHazards.ActiveCount;
        public IReadOnlyList<GroundHazardRuntime> ActiveGroundHazards => groundHazards.ActiveHazards;
        public float FormationProgress => Math.Min(1f, formationElapsed / FormationDurationSeconds);
        public bool IsSkillTelegraphActive => starfallTelegraphActive;
        public bool IsShadowExecutionActive => shadowExecutionActive;
        public bool IsAbyssHarpoonWarningActive => abyssHarpoonWarningActive;
        public bool IsSkyhunterRadianceActive => skyhunterRadianceActive;
        public float SkillTelegraphProgress
        {
            get
            {
                var warning = GetScalar(skill, "TelegraphDurationSeconds", 1f);
                return warning <= 0f ? 0f : Math.Min(1f, starfallTelegraphElapsed / warning);
            }
        }

        public float SkillCooldownRemaining
        {
            get
            {
                if (skill.Cooldown <= 0f)
                {
                    return 0f;
                }

                return Math.Max(0f, skill.Cooldown - skillElapsed);
            }
        }

        public void SetCombatSuspended(bool suspended)
        {
            IsCombatSuspended = suspended;
            if (suspended)
            {
                CurrentTargetRuntimeId = null;
                starfallTelegraphActive = false;
                starfallTelegraphElapsed = 0f;
                shadowExecutionActive = false;
                shadowExecutionElapsed = 0f;
                shadowExecutionNextHit = 0;
                shadowExecutionTargetRuntimeId = null;
                abyssHarpoonWarningActive = false;
                abyssHarpoonWarningElapsed = 0f;
                abyssHarpoonAnchorRuntimeId = null;
            }
        }

        public void SetRuneDefinition(RuneDefinition value)
        {
            runeDefinition = value;
        }

        public void ApplyRuneAttackSpeedBuff(float multiplier, float durationSeconds)
        {
            if (multiplier <= 1f || durationSeconds <= 0f)
            {
                return;
            }

            temporaryRuneAttackSpeedMultiplier = Math.Max(temporaryRuneAttackSpeedMultiplier, multiplier);
            temporaryRuneAttackSpeedRemaining = Math.Max(temporaryRuneAttackSpeedRemaining, durationSeconds);
        }

        public void ResetTargetingAfterRelocation()
        {
            CurrentTargetRuntimeId = null;
            if (definition.Id == HeroSliceCatalog.SkyhunterValkyrieHeroId)
            {
                skyHuntTargetRuntimeId = null;
                skyHuntStacks = 0;
                skyhunterRadiancePrimaryRuntimeId = null;
            }
        }

        // Terminal cleanup is intentionally separate from temporary PairLink suspension.
        public void StopAndReset()
        {
            IsCombatSuspended = true;
            IsFormationComplete = false;
            CurrentTargetRuntimeId = null;
            formationElapsed = 0f;
            attackElapsed = 0f;
            skillElapsed = 0f;
            attackNumber = 0;
            stoneBindAttackCount = 0;
            duelMomentumTargetRuntimeId = null;
            duelMomentumStacks = 0;
            huntMarkTargetRuntimeId = null;
            skyHuntTargetRuntimeId = null;
            skyHuntStacks = 0;
            starfallTelegraphElapsed = 0f;
            starfallTelegraphActive = false;
            shadowExecutionActive = false;
            shadowExecutionElapsed = 0f;
            shadowExecutionNextHit = 0;
            shadowExecutionRetargetCount = 0;
            shadowExecutionTargetRuntimeId = null;
            abyssHarpoonWarningActive = false;
            abyssHarpoonWarningElapsed = 0f;
            abyssHarpoonAnchorRuntimeId = null;
            skyhunterRadianceActive = false;
            skyhunterRadianceElapsed = 0f;
            skyhunterRadiancePrimaryRuntimeId = null;
            temporaryRuneAttackSpeedMultiplier = 1f;
            temporaryRuneAttackSpeedRemaining = 0f;
            basicAttackSucceededTargets.Clear();
            groundHazards.Clear();
        }

        public bool TickFormation(float deltaSeconds)
        {
            if (IsFormationComplete || deltaSeconds <= 0f || IsCombatSuspended)
            {
                return false;
            }

            formationElapsed += deltaSeconds;
            if (formationElapsed + 0.0001f < FormationDurationSeconds)
            {
                return false;
            }

            formationElapsed = FormationDurationSeconds;
            IsFormationComplete = true;
            return true;
        }

        public bool AddExperience(int amount)
        {
            return progression.AddExperience(amount);
        }

        public List<HeroDamageResult> TickCombat(
            float deltaSeconds,
            CombatPoint origin,
            EnemyRegistry registry,
            PathDisplacementSystem pathDisplacement = null)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            var results = new List<HeroDamageResult>();
            basicAttackSucceededTargets.Clear();
            if (deltaSeconds <= 0f || !IsFormationComplete || IsCombatSuspended)
            {
                return results;
            }

            temporaryRuneAttackSpeedRemaining = Math.Max(0f, temporaryRuneAttackSpeedRemaining - deltaSeconds);
            if (temporaryRuneAttackSpeedRemaining <= 0.0001f)
            {
                temporaryRuneAttackSpeedMultiplier = 1f;
            }

            var blocksNormalAttacks = false;
            if (definition.Id == HeroSliceCatalog.DragonRiderHeroId)
            {
                results.AddRange(groundHazards.Tick(deltaSeconds, registry, AttackKind.DragonRiderFlame));
                skillElapsed += deltaSeconds;
                ResolveReadyDives(origin, registry, results);
            }
            else if (definition.Id == HeroSliceCatalog.StarfallArchmageHeroId)
            {
                skillElapsed += deltaSeconds;
                ResolveReadyStarfall(origin, registry, results, deltaSeconds);
            }
            else if (definition.Id == HeroSliceCatalog.ThunderJarlHeroId)
            {
                skillElapsed += deltaSeconds;
                ResolveReadyThunderDominion(origin, registry, results);
            }
            else if (definition.Id == HeroSliceCatalog.NightfangAssassinHeroId)
            {
                blocksNormalAttacks = ResolveShadowExecution(origin, registry, results, deltaSeconds);
            }
            else if (definition.Id == HeroSliceCatalog.LeviathanHunterHeroId)
            {
                blocksNormalAttacks = ResolveAbyssHarpoon(origin, registry, results, deltaSeconds, pathDisplacement);
            }
            else if (definition.Id == HeroSliceCatalog.SkyhunterValkyrieHeroId)
            {
                ResolveSkyhunterRadiance(origin, registry, deltaSeconds);
            }

            if (blocksNormalAttacks)
            {
                return results;
            }

            attackElapsed += deltaSeconds;
            var interval = 1f / AttackSpeed;
            while (attackElapsed + 0.0001f >= interval)
            {
                if (!ResolveNormalAttack(origin, registry, results, pathDisplacement))
                {
                    // Keep a ready attack ready; it will resolve as soon as a target enters range.
                    attackElapsed = Math.Min(attackElapsed, interval);
                    break;
                }

                attackElapsed -= interval;
                if (!string.IsNullOrEmpty(CurrentTargetRuntimeId) &&
                    registry.TryGet(CurrentTargetRuntimeId, out var successfulTarget))
                {
                    basicAttackSucceededTargets.Add(successfulTarget);
                }
            }

            return results;
        }

        private bool ResolveNormalAttack(
            CombatPoint origin,
            EnemyRegistry registry,
            ICollection<HeroDamageResult> results,
            PathDisplacementSystem pathDisplacement)
        {
            if (definition.Id == HeroSliceCatalog.WindclawRangerHeroId)
            {
                return ResolveWindclawAttack(origin, registry, results);
            }

            if (definition.Id == HeroSliceCatalog.EmberShamanHeroId)
            {
                return ResolveEmberAttack(origin, registry, results);
            }

            if (definition.Id == HeroSliceCatalog.DragonRiderHeroId)
            {
                return ResolveDragonRiderAttack(origin, registry, results);
            }

            if (definition.Id == HeroSliceCatalog.RuneboltMageHeroId)
            {
                return ResolveRuneboltAttack(origin, registry, results);
            }

            if (definition.Id == HeroSliceCatalog.StonebinderHeroId)
            {
                return ResolveStonebinderAttack(origin, registry, results);
            }

            if (definition.Id == HeroSliceCatalog.StarfallArchmageHeroId)
            {
                return ResolveStarfallAttack(origin, registry, results);
            }

            if (definition.Id == HeroSliceCatalog.CrownSwordLeaderHeroId)
            {
                return ResolveCrownSwordAttack(origin, registry, results);
            }

            if (definition.Id == HeroSliceCatalog.CrownHunterLeaderHeroId)
            {
                return ResolveCrownHunterAttack(origin, registry, results);
            }

            if (definition.Id == HeroSliceCatalog.ThunderJarlHeroId)
            {
                return ResolveThunderJarlAttack(origin, registry, results);
            }

            if (definition.Id == HeroSliceCatalog.NightfangAssassinHeroId)
            {
                return ResolveNightfangAttack(origin, registry, results);
            }

            if (definition.Id == HeroSliceCatalog.LeviathanHunterHeroId)
            {
                return ResolveLeviathanAttack(origin, registry, results);
            }

            if (definition.Id == HeroSliceCatalog.SkyhunterValkyrieHeroId)
            {
                return ResolveSkyhunterAttack(origin, registry, results);
            }

            // HeroSliceCatalog only exposes implemented heroes. Keep this guard explicit so a
            // future catalog entry can never silently inherit another hero's attack.
            throw new InvalidOperationException($"Hero {definition.Id} has no combat executor.");
        }

        private bool ResolveWindclawAttack(
            CombatPoint origin,
            EnemyRegistry registry,
            ICollection<HeroDamageResult> results)
        {
            var target = SelectEliteFirstInRange(origin, registry, RangeCells);
            if (target == null)
            {
                CurrentTargetRuntimeId = null;
                return false;
            }

            CurrentTargetRuntimeId = target.RuntimeId;
            var triggerCount = Math.Max(1, skill.TriggerCount);
            var powerShot = attackNumber + 1 >= triggerCount;
            attackNumber = powerShot ? 0 : attackNumber + 1;

            var skillMultiplier = definition.GetLevelStats(Level).SkillMultiplier;
            var damage = Attack * (powerShot ? skill.DamageMultiplier : 1f) * (powerShot ? skillMultiplier : 1f);
            if (powerShot && target.Archetype == EnemyArchetype.Elite)
            {
                damage *= GetScalar(skill, "EliteFinalDamageMultiplier", 1f);
            }

            ApplyDamage(
                target,
                damage,
                powerShot ? AttackKind.WindclawPowerShot : AttackKind.WindclawShot,
                results);
            return true;
        }

        private bool ResolveEmberAttack(
            CombatPoint origin,
            EnemyRegistry registry,
            ICollection<HeroDamageResult> results)
        {
            var center = SelectFrontmostInRange(origin, registry, RangeCells);
            if (center == null)
            {
                CurrentTargetRuntimeId = null;
                return false;
            }

            CurrentTargetRuntimeId = center.RuntimeId;
            var radius = skill.Radius > 0f ? skill.Radius : 0.90f;
            var maximumTargets = Math.Max(1, skill.MaxTargets > 0 ? skill.MaxTargets : 5);
            ApplyDamage(
                center,
                Attack * GetScalar(skill, "PrimaryDamageMultiplier", 1f),
                AttackKind.EmberExplosiveFireball,
                results,
                effectRadius: radius);

            foreach (var target in SelectBlastSecondaryTargets(
                         center,
                         radius,
                         registry.Snapshot(),
                         maximumTargets - 1))
            {
                ApplyDamage(
                    target,
                    Attack * GetScalar(skill, "SecondaryDamageMultiplier", 0.75f) *
                    definition.GetLevelStats(Level).SkillMultiplier,
                    AttackKind.EmberExplosiveSplash,
                    results,
                    effectRadius: radius);
            }

            return true;
        }

        private bool ResolveDragonRiderAttack(
            CombatPoint origin,
            EnemyRegistry registry,
            ICollection<HeroDamageResult> results)
        {
            var center = SelectFrontmostInRange(origin, registry, RangeCells);
            if (center == null)
            {
                CurrentTargetRuntimeId = null;
                return false;
            }

            CurrentTargetRuntimeId = center.RuntimeId;
            var radius = GetAttackParameter("AttackRadius", 0.65f);
            var maximumTargets = GetAttackParameterInt("AttackMaxTargets", 4);
            foreach (var target in SelectInRadius(center.CombatPosition, radius, registry.Snapshot(), maximumTargets))
            {
                ApplyDamage(target, Attack, AttackKind.DragonRiderArea, results);
            }

            return true;
        }

        private bool ResolveRuneboltAttack(
            CombatPoint origin,
            EnemyRegistry registry,
            ICollection<HeroDamageResult> results)
        {
            var frontmost = SelectFrontmostInRange(origin, registry, RangeCells);
            if (frontmost == null)
            {
                CurrentTargetRuntimeId = null;
                return false;
            }

            CurrentTargetRuntimeId = frontmost.RuntimeId;
            var direction = Normalize(
                frontmost.CombatPosition.X - origin.X,
                frontmost.CombatPosition.Y - origin.Y);
            var length = GetAttackParameter("PierceLength", skill.Length > 0f ? skill.Length : 5f);
            var width = GetAttackParameter("PierceWidth", skill.Width > 0f ? skill.Width : 0.35f);
            var maximumTargets = GetAttackSeriesParameterInt(
                "MaxTargetsByLevel",
                Level,
                4);
            var lineTargets = SelectLineTargets(
                origin,
                direction,
                length,
                width,
                registry.Snapshot(),
                maximumTargets);
            for (var index = 0; index < lineTargets.Count; index++)
            {
                ApplyDamage(
                    lineTargets[index],
                    Attack * (index == 0 ? 1f : definition.GetLevelStats(Level).SkillMultiplier),
                    AttackKind.RuneboltPierce,
                    results);
            }

            return true;
        }

        private bool ResolveStonebinderAttack(
            CombatPoint origin,
            EnemyRegistry registry,
            ICollection<HeroDamageResult> results)
        {
            var target = SelectFrontmostInRange(origin, registry, RangeCells);
            if (target == null)
            {
                CurrentTargetRuntimeId = null;
                return false;
            }

            CurrentTargetRuntimeId = target.RuntimeId;
            ApplyDamage(target, Attack, AttackKind.StonebinderShot, results);
            stoneBindAttackCount++;
            var triggerCount = Math.Max(1, skill.TriggerCount);
            if (stoneBindAttackCount < triggerCount)
            {
                return true;
            }

            stoneBindAttackCount = 0;
            var stunMultiplier = GetStunMultiplier(target.Archetype);
            var duration = skill.BaseStunDuration *
                           definition.GetLevelStats(Level).SkillMultiplier *
                           stunMultiplier;
            var immunity = target.Archetype == EnemyArchetype.Boss
                ? FrozenHeroConfigurationCatalog.Configuration.ControlRules.BossPostStunImmunitySeconds
                : 0f;
            if (target.ApplyStun(duration, immunity))
            {
                results.Add(new HeroDamageResult(
                    AttackKind.StoneBind,
                    target,
                    0f,
                    false,
                    duration));
            }

            return true;
        }

        private bool ResolveStarfallAttack(
            CombatPoint origin,
            EnemyRegistry registry,
            ICollection<HeroDamageResult> results)
        {
            var center = SelectHighestDensityCenter(
                origin,
                registry,
                RangeCells,
                GetAttackParameter("AttackRadius", skill.Radius > 0f ? skill.Radius : 0.80f));
            if (center == null)
            {
                CurrentTargetRuntimeId = null;
                return false;
            }

            CurrentTargetRuntimeId = center.RuntimeId;
            var radius = GetAttackParameter("AttackRadius", skill.Radius > 0f ? skill.Radius : 0.80f);
            var maximumTargets = GetAttackParameterInt("AttackMaxTargets", skill.MaxTargets > 0 ? skill.MaxTargets : 5);
            foreach (var target in SelectInRadius(center.CombatPosition, radius, registry.Snapshot(), maximumTargets))
            {
                ApplyDamage(target, Attack, AttackKind.StarfallArea, results);
            }

            return true;
        }

        private bool ResolveCrownSwordAttack(
            CombatPoint origin,
            EnemyRegistry registry,
            ICollection<HeroDamageResult> results)
        {
            var target = SelectFrontmostInRange(origin, registry, RangeCells);
            if (target == null)
            {
                CurrentTargetRuntimeId = null;
                duelMomentumTargetRuntimeId = null;
                duelMomentumStacks = 0;
                return false;
            }

            if (!string.Equals(duelMomentumTargetRuntimeId, target.RuntimeId, StringComparison.Ordinal))
            {
                duelMomentumTargetRuntimeId = target.RuntimeId;
                duelMomentumStacks = 0;
            }

            CurrentTargetRuntimeId = target.RuntimeId;
            var bonusPerStack = GetScalar(skill, "FinalDamageBonusPerStack", 0.08f) *
                                definition.GetLevelStats(Level).SkillMultiplier;
            var damage = Attack * (1f + (duelMomentumStacks * bonusPerStack));
            ApplyDamage(target, damage, AttackKind.CrownSwordStrike, results);

            var maximumStacks = GetSkillSeriesParameterInt("MaxStacksByLevel", Level, 5);
            duelMomentumStacks = Math.Min(maximumStacks, duelMomentumStacks + 1);
            return true;
        }

        private bool ResolveCrownHunterAttack(
            CombatPoint origin,
            EnemyRegistry registry,
            ICollection<HeroDamageResult> results)
        {
            var marked = FindAliveEnemy(registry, huntMarkTargetRuntimeId);
            if (marked == null || marked.Team != side ||
                !targeting.IsWithinRange(origin, marked, RangeCells))
            {
                huntMarkTargetRuntimeId = null;
                marked = null;
            }

            var target = marked ?? SelectHighestHealthInRange(origin, registry, RangeCells);
            if (target == null)
            {
                CurrentTargetRuntimeId = null;
                huntMarkTargetRuntimeId = null;
                return false;
            }

            CurrentTargetRuntimeId = target.RuntimeId;
            if (string.IsNullOrEmpty(huntMarkTargetRuntimeId))
            {
                // A mark persists until its target leaves range or is no longer alive.
                huntMarkTargetRuntimeId = target.RuntimeId;
            }
            ApplyDamage(
                target,
                Attack * GetScalar(skill, "MarkedAttackDamageMultiplier", skill.DamageMultiplier) *
                definition.GetLevelStats(Level).SkillMultiplier,
                AttackKind.CrownHunterShot,
                results);
            return true;
        }

        private bool ResolveThunderJarlAttack(
            CombatPoint origin,
            EnemyRegistry registry,
            ICollection<HeroDamageResult> results)
        {
            var first = SelectFrontmostInRange(origin, registry, RangeCells);
            if (first == null)
            {
                CurrentTargetRuntimeId = null;
                return false;
            }

            CurrentTargetRuntimeId = first.RuntimeId;
            var targets = new List<EnemyRuntime> { first };
            var previous = first;
            var maximumTargets = GetAttackParameterInt("AttackMaxTargets", 3);
            while (targets.Count < maximumTargets)
            {
                var next = SelectNextChainTarget(previous, targets, registry, skill.Radius > 0f ? skill.Radius : 1f);
                if (next == null)
                {
                    break;
                }

                targets.Add(next);
                previous = next;
            }

            for (var index = 0; index < targets.Count; index++)
            {
                var multiplier = GetAttackSeriesParameter(
                    "ChainDamageMultipliers",
                    index,
                    index == 0 ? 1f : index == 1 ? 0.75f : 0.55f);
                ApplyDamage(
                    targets[index],
                    Attack * multiplier,
                    AttackKind.ThunderJarlChain,
                    results);
            }

            return true;
        }

        private void ResolveReadyThunderDominion(
            CombatPoint origin,
            EnemyRegistry registry,
            ICollection<HeroDamageResult> results)
        {
            var cooldown = skill.Cooldown;
            if (cooldown <= 0f)
            {
                return;
            }

            while (skillElapsed + 0.0001f >= cooldown)
            {
                var targets = SelectFrontmostInRange(
                    origin,
                    registry,
                    RangeCells,
                    int.MaxValue);
                if (targets.Count == 0)
                {
                    // Keep the ability ready without consuming cooldown while no
                    // enemy is legal for this battlefield.
                    skillElapsed = cooldown;
                    return;
                }

                var levelMultiplier = definition.GetLevelStats(Level).SkillMultiplier;
                foreach (var target in targets)
                {
                    var duration = skill.BaseStunDuration * levelMultiplier * GetStunMultiplier(target.Archetype);
                    ApplyDamage(
                        target,
                        Attack * skill.DamageMultiplier,
                        AttackKind.ThunderDominion,
                        results,
                        duration);
                    target.ApplyStun(
                        duration,
                        target.Archetype == EnemyArchetype.Boss
                            ? FrozenHeroConfigurationCatalog.Configuration.ControlRules.BossPostStunImmunitySeconds
                            : 0f);
                }

                skillElapsed -= cooldown;
            }
        }

        private bool ResolveNightfangAttack(
            CombatPoint origin,
            EnemyRegistry registry,
            ICollection<HeroDamageResult> results)
        {
            var target = SelectBossEliteFrontmostInRange(origin, registry, RangeCells);
            if (target == null)
            {
                CurrentTargetRuntimeId = null;
                return false;
            }

            CurrentTargetRuntimeId = target.RuntimeId;
            ApplyDamage(target, GetNightfangDamage(target, 1f, false), AttackKind.NightfangStrike, results);
            return true;
        }

        private bool ResolveLeviathanAttack(
            CombatPoint origin,
            EnemyRegistry registry,
            ICollection<HeroDamageResult> results)
        {
            var frontmost = SelectFrontmostInRange(origin, registry, RangeCells);
            if (frontmost == null)
            {
                CurrentTargetRuntimeId = null;
                return false;
            }

            CurrentTargetRuntimeId = frontmost.RuntimeId;
            var direction = Normalize(
                frontmost.CombatPosition.X - origin.X,
                frontmost.CombatPosition.Y - origin.Y);
            var length = skill.Length > 0f ? skill.Length : 7f;
            var width = skill.Width > 0f ? skill.Width : 0.40f;
            var maximumTargets = Math.Max(1, skill.MaxTargets > 0 ? skill.MaxTargets : 6);
            var targets = SelectLineTargetsByDistance(
                origin,
                direction,
                length,
                width,
                registry.Snapshot(),
                maximumTargets);
            for (var index = 0; index < targets.Count; index++)
            {
                var multiplier = GetAttackSeriesParameter("DamageMultiplierByHit", index, 1f);
                ApplyDamage(
                    targets[index],
                    Attack * multiplier,
                    AttackKind.LeviathanHarpoon,
                    results);
            }

            return targets.Count > 0;
        }

        private bool ResolveSkyhunterAttack(
            CombatPoint origin,
            EnemyRegistry registry,
            ICollection<HeroDamageResult> results)
        {
            var target = skyhunterRadianceActive
                ? FindAliveEnemy(registry, skyhunterRadiancePrimaryRuntimeId)
                : null;
            var targetRange = skyhunterRadianceActive
                ? GetScalar(skill, "SkillTargetRange", RangeCells)
                : RangeCells;
            if (target == null || !targeting.IsWithinRange(origin, target, targetRange))
            {
                target = SelectFrontmostInRange(origin, registry, targetRange);
            }
            if (target == null)
            {
                CurrentTargetRuntimeId = null;
                skyHuntTargetRuntimeId = null;
                skyHuntStacks = 0;
                return false;
            }

            if (skyhunterRadianceActive)
            {
                skyhunterRadiancePrimaryRuntimeId = target.RuntimeId;
            }

            if (!string.Equals(skyHuntTargetRuntimeId, target.RuntimeId, StringComparison.Ordinal))
            {
                skyHuntTargetRuntimeId = target.RuntimeId;
                skyHuntStacks = 0;
            }

            CurrentTargetRuntimeId = target.RuntimeId;
            ApplyDamage(
                target,
                Attack * (skyhunterRadianceActive
                    ? definition.GetLevelStats(Level).SkillMultiplier
                    : 1f),
                skyhunterRadianceActive ? AttackKind.SkyhunterRadiancePrimary : AttackKind.SkyhunterShot,
                results);
            var maximumStacks = GetSkillSeriesParameterInt("MaxStacksByLevel", Level, 5);
            skyHuntStacks = Math.Min(maximumStacks, skyHuntStacks + 1);

            if (skyhunterRadianceActive)
            {
                var secondaryDamage = Attack * GetScalar(skill, "SecondaryDamageMultiplier", 0.40f) *
                                      definition.GetLevelStats(Level).SkillMultiplier;
                foreach (var secondary in SelectSkyhunterSecondaryTargets(
                             origin,
                             target,
                             registry,
                             Math.Max(0, skill.MaxTargets - 1)))
                {
                    ApplyDamage(
                        secondary,
                        secondaryDamage,
                        AttackKind.SkyhunterRadianceSecondary,
                        results);
                }
            }

            return true;
        }

        private float GetNightfangDamage(
            EnemyRuntime target,
            float attackMultiplier,
            bool isExecutionFinalHit)
        {
            var damage = Attack * attackMultiplier;
            if (target == null)
            {
                return damage;
            }

            if (target.Archetype == EnemyArchetype.Elite || target.Archetype == EnemyArchetype.Boss)
            {
                damage *= 1f + GetScalar(skill, "EliteAndBossFinalDamageBonus", 0.60f);
            }

            var healthRatio = target.MaxHitPoints <= 0f ? 1f : target.HitPoints / target.MaxHitPoints;
            if (healthRatio < GetScalar(skill, "ExecuteHealthThreshold", 0.20f))
            {
                damage *= 1f + GetScalar(skill, "ExecuteFinalDamageBonus", 0.30f);
            }

            if (isExecutionFinalHit && target.Archetype == EnemyArchetype.Boss &&
                healthRatio <= GetScalar(skill, "ExecutionHealthThreshold", 0.10f))
            {
                damage *= GetScalar(skill, "BossLowHealthFinalDamageMultiplier", 1.50f);
            }

            return damage;
        }

        private bool ResolveShadowExecution(
            CombatPoint origin,
            EnemyRegistry registry,
            ICollection<HeroDamageResult> results,
            float deltaSeconds)
        {
            if (!shadowExecutionActive)
            {
                skillElapsed += deltaSeconds;
                if (skill.Cooldown <= 0f || skillElapsed + 0.0001f < skill.Cooldown)
                {
                    return false;
                }

                var selected = SelectBossEliteFrontmostInRange(
                    origin,
                    registry,
                    GetScalar(skill, "SkillTargetRange", RangeCells));
                if (selected == null)
                {
                    // An automatic cooldown ability remains ready until it has a legal target.
                    skillElapsed = skill.Cooldown;
                    return false;
                }

                shadowExecutionActive = true;
                shadowExecutionElapsed = 0f;
                shadowExecutionNextHit = 0;
                shadowExecutionRetargetCount = 0;
                shadowExecutionTargetRuntimeId = selected.RuntimeId;
                CurrentTargetRuntimeId = selected.RuntimeId;
                skillElapsed = 0f;
            }
            else
            {
                shadowExecutionElapsed += deltaSeconds;
            }

            var duration = Math.Max(0.0001f, skill.Duration);
            var hitInterval = duration / 2f;
            while (shadowExecutionActive && shadowExecutionNextHit < 3 &&
                   shadowExecutionElapsed + 0.0001f >= shadowExecutionNextHit * hitInterval)
            {
                var target = FindAliveEnemy(registry, shadowExecutionTargetRuntimeId);
                var targetRange = GetScalar(skill, "SkillTargetRange", RangeCells);
                if (target == null || target.Team != side || !targeting.IsWithinRange(origin, target, targetRange))
                {
                    var maximumRetargets = Math.Max(0, (int)Math.Round(
                        GetScalar(skill, "MaximumRetargets", 1f)));
                    if (shadowExecutionRetargetCount >= maximumRetargets)
                    {
                        shadowExecutionActive = false;
                        shadowExecutionTargetRuntimeId = null;
                        return true;
                    }

                    target = SelectBossEliteFrontmostInRange(origin, registry, targetRange);
                    shadowExecutionRetargetCount++;
                    if (target == null)
                    {
                        shadowExecutionActive = false;
                        shadowExecutionTargetRuntimeId = null;
                        CurrentTargetRuntimeId = null;
                        return true;
                    }

                    shadowExecutionTargetRuntimeId = target.RuntimeId;
                }

                CurrentTargetRuntimeId = target.RuntimeId;
                var hitIndex = shadowExecutionNextHit;
                var isFinalHit = hitIndex == 2;
                var executeThreshold = GetScalar(skill, "ExecutionHealthThreshold", 0.10f);
                var executeTarget = isFinalHit && target.Archetype != EnemyArchetype.Boss &&
                    target.HitPoints <= (target.MaxHitPoints * executeThreshold);
                var multiplier = GetSkillSeriesParameter("DamageMultiplierByHit", hitIndex, 1f);
                ApplyDamage(
                    target,
                    executeTarget
                        ? target.HitPoints
                        : GetNightfangDamage(
                            target,
                            multiplier * definition.GetLevelStats(Level).SkillMultiplier,
                            isFinalHit),
                    AttackKind.NightfangExecutionSlash,
                    results);

                shadowExecutionNextHit++;
                if (shadowExecutionNextHit >= 3)
                {
                    shadowExecutionActive = false;
                    shadowExecutionTargetRuntimeId = null;
                    return true;
                }
            }

            return true;
        }

        private bool ResolveAbyssHarpoon(
            CombatPoint origin,
            EnemyRegistry registry,
            ICollection<HeroDamageResult> results,
            float deltaSeconds,
            PathDisplacementSystem pathDisplacement)
        {
            if (abyssHarpoonWarningActive)
            {
                abyssHarpoonWarningElapsed += deltaSeconds;
                if (abyssHarpoonWarningElapsed + 0.0001f < skill.Duration)
                {
                    return true;
                }

                abyssHarpoonWarningActive = false;
                abyssHarpoonWarningElapsed = 0f;
                var length = skill.Length > 0f ? skill.Length : 7f;
                var width = skill.Width > 0f ? skill.Width : 0.40f;
                var targets = SelectLineTargetsByDistance(
                    origin,
                    abyssHarpoonDirection,
                    length,
                    width,
                    registry.Snapshot(),
                    Math.Max(1, skill.MaxTargets));
                for (var index = 0; index < targets.Count; index++)
                {
                    var target = targets[index];
                    ApplyDamage(
                        target,
                        Attack * GetSkillSeriesParameter("DamageMultiplierByHit", index, 1f) *
                        definition.GetLevelStats(Level).SkillMultiplier,
                        AttackKind.AbyssHarpoonStrike,
                        results);
                    ApplyAbyssHarpoonDisplacement(target, pathDisplacement);
                }

                abyssHarpoonAnchorRuntimeId = null;
                return true;
            }

            skillElapsed += deltaSeconds;
            if (skill.Cooldown <= 0f || skillElapsed + 0.0001f < skill.Cooldown)
            {
                return false;
            }

            var direction = SelectBestAbyssHarpoonDirection(origin, registry);
            if (!direction.HasValue)
            {
                skillElapsed = skill.Cooldown;
                return false;
            }

            abyssHarpoonDirection = direction.Value.Direction;
            abyssHarpoonAnchorRuntimeId = direction.Value.Anchor.RuntimeId;
            abyssHarpoonWarningElapsed = 0f;
            abyssHarpoonWarningActive = true;
            skillElapsed = 0f;
            CurrentTargetRuntimeId = abyssHarpoonAnchorRuntimeId;
            results.Add(new HeroDamageResult(
                AttackKind.AbyssHarpoonWarning,
                direction.Value.Anchor,
                0f,
                false,
                skill.Duration));
            return true;
        }

        private void ApplyAbyssHarpoonDisplacement(
            EnemyRuntime target,
            PathDisplacementSystem pathDisplacement)
        {
            if (target == null || !target.IsAlive || pathDisplacement == null)
            {
                return;
            }

            if (pathDisplacement.IsDisplacementImmune(target))
            {
                pathDisplacement.ApplyMovementSlow(
                    target,
                    GetScalar(skill, "BossSlowFraction", 0.25f),
                    GetScalar(skill, "BossSlowDurationSeconds", 1.50f));
                return;
            }

            var distance = target.Archetype == EnemyArchetype.Elite
                ? GetScalar(skill, "ElitePullDistance", 0.50f)
                : GetScalar(skill, "NormalPullDistance", 1f);
            pathDisplacement.MoveBackwardByPathDistance(target, distance);
        }

        private void ResolveSkyhunterRadiance(
            CombatPoint origin,
            EnemyRegistry registry,
            float deltaSeconds)
        {
            if (skyhunterRadianceActive)
            {
                skyhunterRadianceElapsed += deltaSeconds;
                if (skyhunterRadianceElapsed + 0.0001f >= skill.Duration)
                {
                    skyhunterRadianceActive = false;
                    skyhunterRadianceElapsed = 0f;
                    skyhunterRadiancePrimaryRuntimeId = null;
                }

                return;
            }

            skillElapsed += deltaSeconds;
            if (skill.Cooldown <= 0f || skillElapsed + 0.0001f < skill.Cooldown)
            {
                return;
            }

            var target = SelectFrontmostInRange(
                origin,
                registry,
                GetScalar(skill, "SkillTargetRange", RangeCells));
            if (target == null)
            {
                skillElapsed = skill.Cooldown;
                return;
            }

            skyhunterRadianceActive = true;
            skyhunterRadianceElapsed = 0f;
            skyhunterRadiancePrimaryRuntimeId = target.RuntimeId;
            CurrentTargetRuntimeId = target.RuntimeId;
            skillElapsed = 0f;
        }

        private void ResolveReadyStarfall(
            CombatPoint origin,
            EnemyRegistry registry,
            ICollection<HeroDamageResult> results,
            float deltaSeconds)
        {
            if (starfallTelegraphActive)
            {
                starfallTelegraphElapsed += deltaSeconds;
                var warning = GetScalar(skill, "TelegraphDurationSeconds", 1f);
                if (starfallTelegraphElapsed + 0.0001f >= warning)
                {
                    starfallTelegraphActive = false;
                    starfallTelegraphElapsed = 0f;
                    var radius = skill.Radius > 0f ? skill.Radius : 1.50f;
                    var maximumTargets = Math.Max(1, skill.MaxTargets);
                    foreach (var target in SelectInRadius(
                                 starfallTelegraphCenter,
                                 radius,
                                 registry.Snapshot(),
                                 maximumTargets))
                    {
                        ApplyDamage(
                            target,
                            Attack * skill.DamageMultiplier * definition.GetLevelStats(Level).SkillMultiplier,
                            AttackKind.StarfallImpact,
                            results);
                    }
                }

                return;
            }

            if (skill.Cooldown <= 0f || skillElapsed + 0.0001f < skill.Cooldown)
            {
                return;
            }

            var center = SelectHighestDensityCenter(
                origin,
                registry,
                RangeCells,
                skill.Radius > 0f ? skill.Radius : 1.50f);
            if (center == null)
            {
                // Keep the ability ready without consuming cooldown while no target is legal.
                skillElapsed = skill.Cooldown;
                return;
            }

            CurrentTargetRuntimeId = center.RuntimeId;
            starfallTelegraphCenter = center.CombatPosition;
            starfallTelegraphElapsed = 0f;
            starfallTelegraphActive = true;
            skillElapsed = 0f;
            results.Add(new HeroDamageResult(
                AttackKind.StarfallTelegraph,
                center,
                0f,
                false,
                GetScalar(skill, "TelegraphDurationSeconds", 1f),
                skill.Radius > 0f ? skill.Radius : 1.50f));
        }

        private void ResolveReadyDives(
            CombatPoint origin,
            EnemyRegistry registry,
            ICollection<HeroDamageResult> results)
        {
            var cooldown = skill.Cooldown;
            if (cooldown <= 0f)
            {
                return;
            }

            while (skillElapsed + 0.0001f >= cooldown)
            {
                if (!ResolveDive(origin, registry, results))
                {
                    // A missing target does not consume the cooldown and remains ready.
                    skillElapsed = cooldown;
                    return;
                }

                skillElapsed -= cooldown;
            }
        }

        private bool ResolveDive(
            CombatPoint origin,
            EnemyRegistry registry,
            ICollection<HeroDamageResult> results)
        {
            var frontmost = SelectFrontmostInRange(origin, registry, skill.Length);
            if (frontmost == null)
            {
                CurrentTargetRuntimeId = null;
                return false;
            }

            CurrentTargetRuntimeId = frontmost.RuntimeId;
            var direction = Normalize(frontmost.CombatPosition.X - origin.X, frontmost.CombatPosition.Y - origin.Y);
            var length = skill.Length;
            var width = skill.Width;
            foreach (var target in registry.Snapshot())
            {
                if (target == null || target.Team != side || !target.IsAlive ||
                    !IsInsideLine(origin, direction, length, width, target.CombatPosition))
                {
                    continue;
                }

                ApplyDamage(
                    target,
                    Attack * skill.DamageMultiplier * definition.GetLevelStats(Level).SkillMultiplier,
                    AttackKind.DragonRiderDive,
                    results);
            }

            var tickMultiplier = GetScalar(skill, "FlameTickBaseAttackMultiplier", 0.25f);
            groundHazards.CreateOrRefresh(new GroundHazardDefinition(
                side,
                sourceRuntimeId,
                sourceRecipeId,
                origin,
                GroundHazardShape.Line,
                0f,
                skill.Duration,
                skill.TickInterval,
                RuneModifiedBaseAttack * tickMultiplier * definition.GetLevelStats(Level).SkillMultiplier,
                GetAttackParameterInt("AttackMaxTargets", 4),
                direction,
                length,
                width));
            return true;
        }

        private EnemyRuntime SelectFrontmostInRange(
            CombatPoint origin,
            EnemyRegistry registry,
            float range)
        {
            var candidates = new List<EnemyRuntime>();
            foreach (var enemy in registry.Snapshot())
            {
                if (enemy != null && enemy.Team == side && targeting.IsWithinRange(origin, enemy, range))
                {
                    candidates.Add(enemy);
                }
            }

            candidates.Sort(CompareFrontmost);
            return candidates.Count == 0 ? null : candidates[0];
        }

        private List<EnemyRuntime> SelectFrontmostInRange(
            CombatPoint origin,
            EnemyRegistry registry,
            float range,
            int maximumTargets)
        {
            var candidates = new List<EnemyRuntime>();
            foreach (var enemy in registry.Snapshot())
            {
                if (enemy != null && enemy.Team == side && targeting.IsWithinRange(origin, enemy, range))
                {
                    candidates.Add(enemy);
                }
            }

            candidates.Sort(CompareFrontmost);
            if (candidates.Count > maximumTargets)
            {
                candidates.RemoveRange(maximumTargets, candidates.Count - maximumTargets);
            }

            return candidates;
        }

        private EnemyRuntime SelectEliteFirstInRange(
            CombatPoint origin,
            EnemyRegistry registry,
            float range)
        {
            var candidates = new List<EnemyRuntime>();
            foreach (var enemy in registry.Snapshot())
            {
                if (enemy != null && enemy.Team == side && targeting.IsWithinRange(origin, enemy, range))
                {
                    candidates.Add(enemy);
                }
            }

            candidates.Sort((first, second) =>
            {
                var elite = (second.Archetype == EnemyArchetype.Elite).CompareTo(
                    first.Archetype == EnemyArchetype.Elite);
                return elite != 0 ? elite : CompareFrontmost(first, second);
            });
            return candidates.Count == 0 ? null : candidates[0];
        }

        private EnemyRuntime SelectBossEliteFrontmostInRange(
            CombatPoint origin,
            EnemyRegistry registry,
            float range)
        {
            var candidates = new List<EnemyRuntime>();
            foreach (var enemy in registry.Snapshot())
            {
                if (enemy != null && enemy.Team == side && targeting.IsWithinRange(origin, enemy, range))
                {
                    candidates.Add(enemy);
                }
            }

            candidates.Sort((first, second) =>
            {
                var boss = (second.Archetype == EnemyArchetype.Boss).CompareTo(
                    first.Archetype == EnemyArchetype.Boss);
                if (boss != 0)
                {
                    return boss;
                }

                var elite = (second.Archetype == EnemyArchetype.Elite).CompareTo(
                    first.Archetype == EnemyArchetype.Elite);
                return elite != 0 ? elite : CompareFrontmost(first, second);
            });
            return candidates.Count == 0 ? null : candidates[0];
        }

        private EnemyRuntime SelectHighestHealthInRange(
            CombatPoint origin,
            EnemyRegistry registry,
            float range)
        {
            var candidates = new List<EnemyRuntime>();
            foreach (var enemy in registry.Snapshot())
            {
                if (enemy != null && enemy.Team == side && targeting.IsWithinRange(origin, enemy, range))
                {
                    candidates.Add(enemy);
                }
            }

            candidates.Sort((first, second) =>
            {
                var health = second.HitPoints.CompareTo(first.HitPoints);
                return health != 0 ? health : CompareFrontmost(first, second);
            });
            return candidates.Count == 0 ? null : candidates[0];
        }

        private EnemyRuntime SelectNextChainTarget(
            EnemyRuntime previous,
            IReadOnlyCollection<EnemyRuntime> selected,
            EnemyRegistry registry,
            float jumpRange)
        {
            var candidates = new List<EnemyRuntime>();
            foreach (var enemy in registry.Snapshot())
            {
                if (enemy == null || enemy.Team != side || !enemy.IsAlive || selected.Contains(enemy) ||
                    !targeting.IsWithinRange(previous.CombatPosition, enemy, jumpRange))
                {
                    continue;
                }

                candidates.Add(enemy);
            }

            candidates.Sort(CompareFrontmost);
            return candidates.Count == 0 ? null : candidates[0];
        }

        private List<EnemyRuntime> SelectInRadius(
            CombatPoint center,
            float radius,
            IEnumerable<EnemyRuntime> enemies,
            int maximumTargets)
        {
            var values = new List<EnemyRuntime>();
            foreach (var enemy in enemies)
            {
                if (enemy != null && enemy.Team == side && enemy.IsAlive &&
                    center.DistanceSquared(enemy.CombatPosition) <= (radius * radius) + 0.0001f)
                {
                    values.Add(enemy);
                }
            }

            values.Sort(CompareFrontmost);
            if (values.Count > maximumTargets)
            {
                values.RemoveRange(maximumTargets, values.Count - maximumTargets);
            }

            return values;
        }

        private List<EnemyRuntime> SelectBlastSecondaryTargets(
            EnemyRuntime center,
            float radius,
            IEnumerable<EnemyRuntime> enemies,
            int maximumTargets)
        {
            var values = new List<EnemyRuntime>();
            if (center == null || maximumTargets <= 0)
            {
                return values;
            }

            var radiusSquared = radius * radius;
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy == center || enemy.Team != side || !enemy.IsAlive ||
                    center.CombatPosition.DistanceSquared(enemy.CombatPosition) > radiusSquared + 0.0001f)
                {
                    continue;
                }

                values.Add(enemy);
            }

            values.Sort((first, second) =>
            {
                var firstDistance = center.CombatPosition.DistanceSquared(first.CombatPosition);
                var secondDistance = center.CombatPosition.DistanceSquared(second.CombatPosition);
                var distance = firstDistance.CompareTo(secondDistance);
                return distance != 0 ? distance : string.CompareOrdinal(first.RuntimeId, second.RuntimeId);
            });
            if (values.Count > maximumTargets)
            {
                values.RemoveRange(maximumTargets, values.Count - maximumTargets);
            }

            return values;
        }

        private List<EnemyRuntime> SelectSkyhunterSecondaryTargets(
            CombatPoint origin,
            EnemyRuntime primary,
            EnemyRegistry registry,
            int maximumTargets)
        {
            var values = new List<EnemyRuntime>();
            if (primary == null || registry == null || maximumTargets <= 0)
            {
                return values;
            }

            foreach (var enemy in registry.Snapshot())
            {
                if (enemy == null || enemy == primary || enemy.Team != side || !enemy.IsAlive ||
                    !targeting.IsWithinRange(
                        origin,
                        enemy,
                        GetScalar(skill, "SkillTargetRange", RangeCells)))
                {
                    continue;
                }

                values.Add(enemy);
            }

            values.Sort((first, second) =>
            {
                var progress = second.PathProgress.CompareTo(first.PathProgress);
                if (progress != 0)
                {
                    return progress;
                }

                var firstDistance = origin.DistanceSquared(first.CombatPosition);
                var secondDistance = origin.DistanceSquared(second.CombatPosition);
                var distance = firstDistance.CompareTo(secondDistance);
                return distance != 0 ? distance : string.CompareOrdinal(first.RuntimeId, second.RuntimeId);
            });
            if (values.Count > maximumTargets)
            {
                values.RemoveRange(maximumTargets, values.Count - maximumTargets);
            }

            return values;
        }

        private AbyssHarpoonDirectionSelection? SelectBestAbyssHarpoonDirection(
            CombatPoint origin,
            EnemyRegistry registry)
        {
            var candidates = new List<EnemyRuntime>();
            var targetRange = GetScalar(skill, "SkillTargetRange", RangeCells);
            var length = skill.Length > 0f ? skill.Length : 7f;
            var width = skill.Width > 0f ? skill.Width : 0.40f;
            foreach (var enemy in registry.Snapshot())
            {
                if (enemy != null && enemy.Team == side && enemy.IsAlive &&
                    targeting.IsWithinRange(origin, enemy, targetRange))
                {
                    candidates.Add(enemy);
                }
            }

            candidates.Sort((first, second) => string.CompareOrdinal(first.RuntimeId, second.RuntimeId));
            AbyssHarpoonDirectionSelection? best = null;
            foreach (var candidate in candidates)
            {
                var direction = Normalize(
                    candidate.CombatPosition.X - origin.X,
                    candidate.CombatPosition.Y - origin.Y);
                var targets = SelectLineTargetsByDistance(
                    origin,
                    direction,
                    length,
                    width,
                    registry.Snapshot(),
                    Math.Max(1, skill.MaxTargets));
                if (targets.Count == 0)
                {
                    continue;
                }

                var selection = new AbyssHarpoonDirectionSelection(candidate, direction, targets);
                if (!best.HasValue || IsBetterAbyssHarpoonSelection(origin, selection, best.Value))
                {
                    best = selection;
                }
            }

            return best;
        }

        private static bool IsBetterAbyssHarpoonSelection(
            CombatPoint origin,
            AbyssHarpoonDirectionSelection candidate,
            AbyssHarpoonDirectionSelection current)
        {
            var hitCount = candidate.Targets.Count.CompareTo(current.Targets.Count);
            if (hitCount != 0)
            {
                return hitCount > 0;
            }

            var candidateFrontmost = candidate.Targets.Max(target => target.PathProgress);
            var currentFrontmost = current.Targets.Max(target => target.PathProgress);
            var progress = candidateFrontmost.CompareTo(currentFrontmost);
            if (progress != 0)
            {
                return progress > 0;
            }

            var candidateDistance = ProjectionDistance(origin, candidate.Direction, candidate.Targets[0].CombatPosition);
            var currentDistance = ProjectionDistance(origin, current.Direction, current.Targets[0].CombatPosition);
            var distance = candidateDistance.CompareTo(currentDistance);
            if (distance != 0)
            {
                return distance < 0;
            }

            return string.CompareOrdinal(candidate.Anchor.RuntimeId, current.Anchor.RuntimeId) < 0;
        }

        private List<EnemyRuntime> SelectLineTargets(
            CombatPoint origin,
            CombatPoint direction,
            float length,
            float width,
            IEnumerable<EnemyRuntime> enemies,
            int maximumTargets)
        {
            var values = new List<EnemyRuntime>();
            foreach (var enemy in enemies)
            {
                if (enemy != null && enemy.Team == side && enemy.IsAlive &&
                    IsInsideLine(origin, direction, length, width, enemy.CombatPosition))
                {
                    values.Add(enemy);
                }
            }

            values.Sort(CompareFrontmost);
            if (values.Count > maximumTargets)
            {
                values.RemoveRange(maximumTargets, values.Count - maximumTargets);
            }

            return values;
        }

        private List<EnemyRuntime> SelectLineTargetsByDistance(
            CombatPoint origin,
            CombatPoint direction,
            float length,
            float width,
            IEnumerable<EnemyRuntime> enemies,
            int maximumTargets)
        {
            var values = new List<EnemyRuntime>();
            foreach (var enemy in enemies)
            {
                if (enemy != null && enemy.Team == side && enemy.IsAlive &&
                    IsInsideLine(origin, direction, length, width, enemy.CombatPosition))
                {
                    values.Add(enemy);
                }
            }

            values.Sort((first, second) =>
            {
                var firstDistance = ProjectionDistance(origin, direction, first.CombatPosition);
                var secondDistance = ProjectionDistance(origin, direction, second.CombatPosition);
                var distance = firstDistance.CompareTo(secondDistance);
                return distance != 0 ? distance : CompareFrontmost(first, second);
            });
            if (values.Count > maximumTargets)
            {
                values.RemoveRange(maximumTargets, values.Count - maximumTargets);
            }

            return values;
        }

        private EnemyRuntime SelectHighestDensityCenter(
            CombatPoint origin,
            EnemyRegistry registry,
            float range,
            float radius)
        {
            var candidates = new List<EnemyRuntime>();
            foreach (var enemy in registry.Snapshot())
            {
                if (enemy != null && enemy.Team == side && enemy.IsAlive &&
                    targeting.IsWithinRange(origin, enemy, range))
                {
                    candidates.Add(enemy);
                }
            }

            candidates.Sort((first, second) =>
            {
                var firstDensity = CountInRadius(first.CombatPosition, radius, registry.Snapshot());
                var secondDensity = CountInRadius(second.CombatPosition, radius, registry.Snapshot());
                var density = secondDensity.CompareTo(firstDensity);
                return density != 0 ? density : CompareFrontmost(first, second);
            });
            return candidates.Count == 0 ? null : candidates[0];
        }

        private int CountInRadius(
            CombatPoint center,
            float radius,
            IEnumerable<EnemyRuntime> enemies)
        {
            var count = 0;
            foreach (var enemy in enemies)
            {
                if (enemy != null && enemy.Team == side && enemy.IsAlive &&
                    center.DistanceSquared(enemy.CombatPosition) <= (radius * radius) + 0.0001f)
                {
                    count++;
                }
            }

            return count;
        }

        private float GetAttackParameter(string key, float fallback)
        {
            return definition.AttackParameters.TryGetValue(key, out var value) && value > 0f ? value : fallback;
        }

        private int GetAttackParameterInt(string key, int fallback)
        {
            return Math.Max(1, (int)Math.Round(GetAttackParameter(key, fallback)));
        }

        private int GetAttackSeriesParameterInt(string key, int level, int fallback)
        {
            if (!definition.AttackSeriesParameters.TryGetValue(key, out var values) ||
                values == null || values.Length == 0)
            {
                return Math.Max(1, fallback);
            }

            var index = Math.Max(0, Math.Min(values.Length - 1, level - 1));
            return Math.Max(1, (int)Math.Round(values[index]));
        }

        private float GetAttackSeriesParameter(string key, int index, float fallback)
        {
            if (!definition.AttackSeriesParameters.TryGetValue(key, out var values) ||
                values == null || values.Length == 0 || index < 0 || index >= values.Length)
            {
                return fallback;
            }

            return values[index];
        }

        private int GetSkillSeriesParameterInt(string key, int level, int fallback)
        {
            if (!skill.SeriesParameters.TryGetValue(key, out var values) ||
                values == null || values.Length == 0)
            {
                return Math.Max(1, fallback);
            }

            var index = Math.Max(0, Math.Min(values.Length - 1, level - 1));
            return Math.Max(1, (int)Math.Round(values[index]));
        }

        private float GetSkillSeriesParameter(string key, int index, float fallback)
        {
            if (!skill.SeriesParameters.TryGetValue(key, out var values) ||
                values == null || values.Length == 0 || index < 0 || index >= values.Length)
            {
                return fallback;
            }

            return values[index];
        }

        private static EnemyRuntime FindAliveEnemy(EnemyRegistry registry, string runtimeId)
        {
            if (registry == null || string.IsNullOrWhiteSpace(runtimeId) ||
                !registry.TryGet(runtimeId, out var enemy) || enemy == null || !enemy.IsAlive)
            {
                return null;
            }

            return enemy;
        }

        private static float GetStunMultiplier(EnemyArchetype archetype)
        {
            var rules = FrozenHeroConfigurationCatalog.Configuration.ControlRules;
            switch (archetype)
            {
                case EnemyArchetype.Elite:
                    return rules.EliteStunMultiplier;
                case EnemyArchetype.Boss:
                    return rules.BossStunMultiplier;
                default:
                    return rules.NormalStunMultiplier;
            }
        }

        private static float GetScalar(SkillDefinition value, string key, float fallback)
        {
            return value.ScalarParameters.TryGetValue(key, out var scalar) ? scalar : fallback;
        }

        private static void ApplyDamage(
            EnemyRuntime target,
            float damage,
            AttackKind kind,
            ICollection<HeroDamageResult> results,
            float effectDuration = 0f,
            float effectRadius = 0f)
        {
            if (target == null || !target.IsAlive || damage < 0f)
            {
                return;
            }

            var application = target.ApplyDamage(damage);
            results.Add(new HeroDamageResult(
                kind,
                target,
                damage,
                target.HitPoints <= 0.0001f,
                effectDuration,
                effectRadius,
                false,
                application.ShieldDamage,
                application.HealthDamage));
        }

        private static int CompareFrontmost(EnemyRuntime first, EnemyRuntime second)
        {
            var progress = second.PathProgress.CompareTo(first.PathProgress);
            return progress != 0 ? progress : string.CompareOrdinal(first.RuntimeId, second.RuntimeId);
        }

        private static bool IsInsideLine(
            CombatPoint origin,
            CombatPoint direction,
            float length,
            float width,
            CombatPoint point)
        {
            var relativeX = point.X - origin.X;
            var relativeY = point.Y - origin.Y;
            var forward = (relativeX * direction.X) + (relativeY * direction.Y);
            if (forward < 0f || forward > length)
            {
                return false;
            }

            var lateral = Math.Abs((relativeX * -direction.Y) + (relativeY * direction.X));
            return lateral <= width * 0.5f + 0.0001f;
        }

        private static float ProjectionDistance(
            CombatPoint origin,
            CombatPoint direction,
            CombatPoint point)
        {
            return ((point.X - origin.X) * direction.X) +
                   ((point.Y - origin.Y) * direction.Y);
        }

        private static CombatPoint Normalize(float x, float y)
        {
            var magnitude = (float)Math.Sqrt((x * x) + (y * y));
            return magnitude <= 0.0001f
                ? new CombatPoint(1f, 0f)
                : new CombatPoint(x / magnitude, y / magnitude);
        }
    }

    public sealed class HeroPairCombatProxy
    {
        private readonly HeroCombatState state;
        private readonly string sourceRuntimeId;
        private readonly List<RuneDamageResult> pendingWarcries = new List<RuneDamageResult>();
        private RuneEffectExecutor runeEffects;
        private CombatPoint lastCombatOrigin;

        public HeroPairCombatProxy(string heroId, HeroProgressionState progression)
            : this(heroId, progression, TeamSide.Player, "hero." + heroId, heroId)
        {
        }

        public HeroPairCombatProxy(
            string heroId,
            HeroProgressionState progression,
            TeamSide side,
            string sourceRuntimeId,
            string sourceRecipeId)
        {
            this.sourceRuntimeId = sourceRuntimeId;
            state = new HeroCombatState(
                heroId,
                progression,
                false,
                side,
                sourceRuntimeId,
                sourceRecipeId);
        }

        public HeroDefinition Definition => state.Definition;
        public bool IsFormationComplete => state.IsFormationComplete;
        public bool IsCombatSuspended => state.IsCombatSuspended;
        public int Experience => state.Experience;
        public int Level => state.Level;
        public int AttackNumber => state.AttackNumber;
        public int DuelMomentumStacks => state.DuelMomentumStacks;
        public string HuntMarkTargetRuntimeId => state.HuntMarkTargetRuntimeId;
        public int SkyHuntStacks => state.SkyHuntStacks;
        public string SkyHuntTargetRuntimeId => state.SkyHuntTargetRuntimeId;
        public float Attack => state.Attack;
        public float AttackSpeed => state.AttackSpeed;
        public float RangeCells => state.RangeCells;
        public string RuneId => runeEffects == null ? string.Empty : runeEffects.Definition.RuneId;
        public float FormationProgress => state.FormationProgress;
        public string CurrentTargetRuntimeId => state.CurrentTargetRuntimeId;
        public int ActiveGroundHazardCount => state.ActiveGroundHazardCount;
        public IReadOnlyList<GroundHazardRuntime> ActiveGroundHazards => state.ActiveGroundHazards;

        public bool TickFormation(float deltaSeconds)
        {
            return state.TickFormation(deltaSeconds);
        }

        public bool AddExperience(int amount)
        {
            return state.AddExperience(amount);
        }

        public void ConfigureRune(RuneDefinition rune, int runSeed)
        {
            state.SetRuneDefinition(rune);
            runeEffects = rune == null
                ? null
                : new RuneEffectExecutor(
                    rune,
                    new RunRandom(DeriveRuneSeed(runSeed, sourceRuntimeId, rune.RuneId)),
                    sourceRuntimeId);
        }

        public void NotifyHeroLevelUp()
        {
            runeEffects?.OnHeroLevelUp();
        }

        public List<HeroDamageResult> NotifyHeroKill(EnemyRuntime killedEnemy, EnemyRegistry registry, bool wasRuneDerived)
        {
            var results = new List<HeroDamageResult>();
            if (runeEffects == null)
            {
                return results;
            }

            AddRuneResults(
                results,
                runeEffects.OnHeroKill(
                    new RuneCombatContext(lastCombatOrigin, Attack, RangeCells, registry),
                    killedEnemy,
                    wasRuneDerived));
            return results;
        }

        public List<RuneDamageResult> DrainWarcries()
        {
            var result = new List<RuneDamageResult>(pendingWarcries);
            pendingWarcries.Clear();
            return result;
        }

        public void ApplyRuneAttackSpeedBuff(float multiplier, float durationSeconds)
        {
            state.ApplyRuneAttackSpeedBuff(multiplier, durationSeconds);
        }

        public List<HeroDamageResult> TickCombat(
            float deltaSeconds,
            CombatPoint origin,
            EnemyRegistry registry,
            PathDisplacementSystem pathDisplacement = null)
        {
            lastCombatOrigin = origin;
            var results = state.TickCombat(deltaSeconds, origin, registry, pathDisplacement);
            if (runeEffects == null)
            {
                return results;
            }

            var context = new RuneCombatContext(origin, Attack, RangeCells, registry);
            AddRuneResults(results, runeEffects.Tick(context, deltaSeconds));
            foreach (var target in state.BasicAttackSucceededTargets)
            {
                AddRuneResults(results, runeEffects.OnBasicAttackSucceeded(context, target));
            }

            return results;
        }

        public void SetCombatSuspended(bool suspended)
        {
            state.SetCombatSuspended(suspended);
        }

        public void ResetTargetingAfterRelocation()
        {
            state.ResetTargetingAfterRelocation();
        }

        public void StopAndReset()
        {
            state.StopAndReset();
            pendingWarcries.Clear();
        }

        private void AddRuneResults(ICollection<HeroDamageResult> destination, IReadOnlyList<RuneDamageResult> runeResults)
        {
            if (runeResults == null)
            {
                return;
            }

            foreach (var result in runeResults)
            {
                if (result.IsWarcry)
                {
                    pendingWarcries.Add(result);
                    continue;
                }

                if (result.Target != null)
                {
                    destination.Add(new HeroDamageResult(
                        result.Kind,
                        result.Target,
                        result.Damage,
                        result.Killed,
                        effectRadius: result.EffectRadius,
                        isRuneDerived: true));
                }
            }
        }

        private static int DeriveRuneSeed(int runSeed, string runtimeId, string runeId)
        {
            unchecked
            {
                var hash = 2166136261u;
                hash = (hash ^ (uint)runSeed) * 16777619u;
                foreach (var character in RuneDropRules.AlgorithmVersion)
                {
                    hash = (hash ^ character) * 16777619u;
                }

                foreach (var character in runeId ?? string.Empty)
                {
                    hash = (hash ^ character) * 16777619u;
                }

                foreach (var character in runtimeId ?? string.Empty)
                {
                    hash = (hash ^ character) * 16777619u;
                }

                return (int)hash;
            }
        }
    }
}
