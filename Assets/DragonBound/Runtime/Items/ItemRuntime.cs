using System;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Core;

namespace DragonBound.Items
{
    /// <summary>Match-start contract. The backend/matchmaker validates inventory and loadouts
    /// before this provider is called; it is never consulted again during a Run.</summary>
    public interface IItemRunSnapshotProvider
    {
        bool TryGetValidatedSnapshots(
            out ItemRunSnapshot playerSnapshot,
            out ItemRunSnapshot aiSnapshot,
            out string reason);
    }

    /// <summary>
    /// Matchmaking/client account boundary. Implementations supply a server-validated player
    /// profile and an independently validated AI snapshot; this interface never computes account
    /// progress, DayKey, inventory or rewards.
    /// </summary>
    public interface IItemValidatedProfileSnapshotSource
    {
        bool TryGetValidatedPlayerProfile(
            out ItemProfile profile,
            out ItemRunSnapshot aiSnapshot,
            out string reason);
    }

    /// <summary>Converts an externally validated profile into the immutable player Run snapshot.</summary>
    public sealed class ItemProfileRunSnapshotProvider : IItemRunSnapshotProvider
    {
        private readonly IItemValidatedProfileSnapshotSource source;

        public ItemProfileRunSnapshotProvider(IItemValidatedProfileSnapshotSource source)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public bool TryGetValidatedSnapshots(
            out ItemRunSnapshot playerSnapshot,
            out ItemRunSnapshot aiSnapshot,
            out string reason)
        {
            playerSnapshot = null;
            aiSnapshot = null;
            reason = ItemOperationFailure.InvalidLoadout;
            if (!source.TryGetValidatedPlayerProfile(out var profile, out aiSnapshot, out reason) ||
                profile == null || aiSnapshot == null)
            {
                if (string.IsNullOrWhiteSpace(reason))
                {
                    reason = ItemOperationFailure.InvalidLoadout;
                }

                return false;
            }

            if (!profile.TryCreateRunSnapshot(out playerSnapshot, out reason))
            {
                aiSnapshot = null;
                return false;
            }

            return true;
        }
    }

    public sealed class EmptyItemRunSnapshotProvider : IItemRunSnapshotProvider
    {
        public bool TryGetValidatedSnapshots(
            out ItemRunSnapshot playerSnapshot,
            out ItemRunSnapshot aiSnapshot,
            out string reason)
        {
            playerSnapshot = ItemRunSnapshot.Empty;
            aiSnapshot = ItemRunSnapshot.Empty;
            reason = ItemOperationFailure.None;
            return true;
        }
    }

    public enum ItemCombatEventKind
    {
        EnemySpawned,
        EnemyKilled,
        EnemyLeaked,
        RecruitSucceeded,
        EnemyApproachingGoal,
        HeroFormed
    }

    public enum ItemCombatEventSource
    {
        Unknown,
        Basic,
        Hero,
        Item,
        RuneDerived,
        System
    }

    public readonly struct ItemCombatEvent
    {
        public ItemCombatEvent(
            ItemCombatEventKind kind,
            TeamSide side,
            string runtimeId = "",
            bool isLegalKill = false,
            ItemCombatEventSource source = ItemCombatEventSource.Unknown)
        {
            Kind = kind;
            Side = side;
            RuntimeId = runtimeId ?? string.Empty;
            IsLegalKill = isLegalKill;
            Source = source;
        }

        public ItemCombatEventKind Kind { get; }
        public TeamSide Side { get; }
        public string RuntimeId { get; }
        public bool IsLegalKill { get; }
        public ItemCombatEventSource Source { get; }
    }

    public readonly struct ItemEnemyDamageResult
    {
        public ItemEnemyDamageResult(
            bool applied,
            bool killed,
            float shieldDamage,
            float healthDamage)
        {
            Applied = applied;
            Killed = killed;
            ShieldDamage = shieldDamage;
            HealthDamage = healthDamage;
        }

        public bool Applied { get; }
        public bool Killed { get; }
        public float ShieldDamage { get; }
        public float HealthDamage { get; }
        public static ItemEnemyDamageResult Rejected =>
            new ItemEnemyDamageResult(false, false, 0f, 0f);
    }

    public interface IItemEnemyDamagePort
    {
        ItemEnemyDamageResult ApplyItemDamage(string itemId, string enemyRuntimeId, float damage);
    }

    internal sealed class DirectRegistryItemEnemyDamagePort : IItemEnemyDamagePort
    {
        private readonly EnemyRegistry registry;

        public DirectRegistryItemEnemyDamagePort(EnemyRegistry registry)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public ItemEnemyDamageResult ApplyItemDamage(
            string itemId,
            string enemyRuntimeId,
            float damage)
        {
            if (damage <= 0f ||
                !registry.TryGet(enemyRuntimeId ?? string.Empty, out var enemy) ||
                !enemy.IsAlive)
            {
                return ItemEnemyDamageResult.Rejected;
            }

            var result = enemy.ApplyDamage(damage);
            return new ItemEnemyDamageResult(
                true,
                result.Killed,
                result.ShieldDamage,
                result.HealthDamage);
        }
    }

    public interface IItemRunResourcePort
    {
        bool TryGrant(int amount, out string reason);
    }

    public sealed class TeamStateRunResourcePort : IItemRunResourcePort
    {
        private readonly TeamState team;

        public TeamStateRunResourcePort(TeamState team)
        {
            this.team = team ?? throw new ArgumentNullException(nameof(team));
        }

        public bool TryGrant(int amount, out string reason)
        {
            reason = ItemOperationFailure.None;
            if (amount <= 0)
            {
                reason = "InvalidResourceAmount";
                return false;
            }

            team.AddResources(amount);
            return true;
        }
    }

    public interface IItemFreeRecruitPort
    {
        bool TryGrantFreeRecruit(out string reason);
    }

    public interface IItemForgePickPort
    {
        ItemForgePickResult TryGrantForgePick(bool requiresAdvertisement);
    }

    public enum ItemForgePickResultKind
    {
        Granted,
        NoLockedCell,
        AdvertisementRequired,
        AuthorityUnavailable,
        Rejected
    }

    public readonly struct ItemForgePickResult
    {
        public ItemForgePickResult(ItemForgePickResultKind kind, string reason = "")
        {
            Kind = kind;
            Reason = reason ?? string.Empty;
        }

        public ItemForgePickResultKind Kind { get; }
        public string Reason { get; }
        public bool Granted => Kind == ItemForgePickResultKind.Granted;
    }

    public sealed class ItemRunContext
    {
        public ItemRunContext(
            TeamState ownTeam,
            EnemyRegistry ownRouteEnemies,
            ItemCombatUnitRegistry unitRegistry = null,
            int runSeed = 0,
            TeamState opposingTeam = null,
            EnemyRegistry opposingRouteEnemies = null,
            ItemCombatUnitRegistry opposingUnitRegistry = null,
            IItemBenchCapacityPort benchCapacity = null,
            IItemSpellbreakerPort spellbreaker = null,
            IItemRunResourcePort runResource = null,
            IItemFreeRecruitPort freeRecruit = null,
            IItemForgePickPort forgePick = null,
            IItemEnemyDamagePort itemEnemyDamage = null)
        {
            OwnTeam = ownTeam ?? throw new ArgumentNullException(nameof(ownTeam));
            OwnRouteEnemies = ownRouteEnemies ?? throw new ArgumentNullException(nameof(ownRouteEnemies));
            UnitRegistry = unitRegistry ?? new ItemCombatUnitRegistry();
            RunSeed = runSeed;
            OpposingTeam = opposingTeam;
            OpposingRouteEnemies = opposingRouteEnemies;
            OpposingUnitRegistry = opposingUnitRegistry ?? new ItemCombatUnitRegistry();
            BenchCapacity = benchCapacity ?? new ItemBenchCapacityState();
            Spellbreaker = spellbreaker;
            RunResource = runResource ?? new TeamStateRunResourcePort(OwnTeam);
            FreeRecruit = freeRecruit;
            ForgePick = forgePick;
            ItemEnemyDamage = itemEnemyDamage ??
                              new DirectRegistryItemEnemyDamagePort(OwnRouteEnemies);
        }

        public TeamState OwnTeam { get; }
        public EnemyRegistry OwnRouteEnemies { get; }
        public ItemCombatUnitRegistry UnitRegistry { get; }
        public int RunSeed { get; }
        public TeamState OpposingTeam { get; }
        public EnemyRegistry OpposingRouteEnemies { get; }
        public ItemCombatUnitRegistry OpposingUnitRegistry { get; }
        public IItemBenchCapacityPort BenchCapacity { get; }
        public IItemSpellbreakerPort Spellbreaker { get; }
        public IItemRunResourcePort RunResource { get; }
        public IItemFreeRecruitPort FreeRecruit { get; }
        public IItemForgePickPort ForgePick { get; }
        public IItemEnemyDamagePort ItemEnemyDamage { get; }
        public float ElapsedSeconds { get; internal set; }
        public string ActivationTargetId { get; internal set; }
        public bool HasActivationPoint { get; internal set; }
        public CombatPoint ActivationPoint { get; internal set; }
        public int NextActivationOrdinal { get; internal set; }

        public void SetActivationTarget(string targetId)
        {
            ActivationTargetId = targetId;
        }

        public void SetActivationPoint(CombatPoint point)
        {
            ActivationPoint = point;
            HasActivationPoint = true;
        }
    }

    public interface IItemEffectRuntime
    {
        string ItemId { get; }
        void OnRunStart(ItemRunContext context);
        void Tick(ItemRunContext context, float deltaSeconds);
        bool TryActivate(ItemRunContext context, out string reason);
        void HandleCombatEvent(ItemRunContext context, ItemCombatEvent combatEvent);
    }

    public sealed class DrakeheartRelicEffect : IItemEffectRuntime
    {
        public const int HeartBonus = 3;

        public string ItemId => Items.ItemIds.DrakeheartRelic;
        public bool Applied { get; private set; }

        public void OnRunStart(ItemRunContext context)
        {
            if (!Applied)
            {
                context.OwnTeam.ApplyHatchlingHealthBonus(HeartBonus);
                Applied = true;
            }
        }

        public void Tick(ItemRunContext context, float deltaSeconds)
        {
        }

        public bool TryActivate(ItemRunContext context, out string reason)
        {
            reason = ItemOperationFailure.None;
            return false;
        }

        public void HandleCombatEvent(ItemRunContext context, ItemCombatEvent combatEvent)
        {
        }
    }

    public sealed class WinterveilRuneEffect : IItemEffectRuntime
    {
        public const float SlowFraction = 0.10f;
        public const float DurationSeconds = 5f;
        public const float CooldownSeconds = 30f;

        public string ItemId => Items.ItemIds.WinterveilRune;
        public float CooldownRemainingSeconds { get; private set; }
        public int LastAffectedEnemyCount { get; private set; }

        public void OnRunStart(ItemRunContext context)
        {
        }

        public void Tick(ItemRunContext context, float deltaSeconds)
        {
            if (deltaSeconds > 0f)
            {
                CooldownRemainingSeconds = Math.Max(0f, CooldownRemainingSeconds - deltaSeconds);
            }
        }

        public bool TryActivate(ItemRunContext context, out string reason)
        {
            reason = ItemOperationFailure.None;
            LastAffectedEnemyCount = 0;
            if (CooldownRemainingSeconds > 0.0001f)
            {
                reason = "Cooldown";
                return false;
            }

            var enemies = context.OwnRouteEnemies.Snapshot();
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy.Team != context.OwnTeam.Side || !enemy.IsAlive)
                {
                    continue;
                }

                if (enemy.ApplyMovementSlow(SlowFraction, DurationSeconds))
                {
                    LastAffectedEnemyCount++;
                }
            }

            if (LastAffectedEnemyCount == 0)
            {
                reason = "NoAliveTargets";
                return false;
            }

            CooldownRemainingSeconds = CooldownSeconds;
            return true;
        }

        public void HandleCombatEvent(ItemRunContext context, ItemCombatEvent combatEvent)
        {
        }
    }

    public static class ItemEffectRuntimeFactory
    {
        private static readonly IReadOnlyDictionary<ItemEffectKind, Func<IItemEffectRuntime>> factories =
            new Dictionary<ItemEffectKind, Func<IItemEffectRuntime>>
            {
                { ItemEffectKind.DrakeheartRelic, () => new DrakeheartRelicEffect() },
                { ItemEffectKind.WinterveilRune, () => new WinterveilRuneEffect() },
                { ItemEffectKind.WyrmfangSnare, () => new WyrmfangSnareEffect() },
                { ItemEffectKind.RuneburstMine, () => new RuneburstMineEffect() },
                { ItemEffectKind.FrenzyRune, () => new FrenzyRuneEffect() },
                { ItemEffectKind.RuneOfTempering, () => new RuneOfTemperingEffect() },
                { ItemEffectKind.WarforgeSigil, () => new WarforgeSigilEffect() },
                { ItemEffectKind.DragonfallJudgment, () => new DragonfallJudgmentEffect() },
                { ItemEffectKind.PactOfEndurance, () => new PactOfEnduranceEffect() },
                { ItemEffectKind.FarwatchCrest, () => new FarwatchCrestEffect() },
                { ItemEffectKind.FrostMire, () => new FrostMireEffect() },
                { ItemEffectKind.WarTempo, () => new WarTempoEffect() },
                { ItemEffectKind.VeteransMark, () => new VeteransMarkEffect() },
                { ItemEffectKind.QuartermastersSatchel, () => new QuartermasterSatchelEffect() },
                { ItemEffectKind.SpellbreakerSeal, () => new SpellbreakerSealEffect() },
                { ItemEffectKind.RivalryOath, () => new RivalryOathEffect() },
                { ItemEffectKind.DraconicPresence, () => new DraconicPresenceEffect() },
                { ItemEffectKind.ForgeTreasury, () => new ForgeTreasuryEffect() },
                { ItemEffectKind.BattlefieldCommand, () => new BattlefieldCommandEffect() },
                { ItemEffectKind.ForgekeepersGift, () => new ForgekeepersGiftEffect() }
            };

        public static IItemEffectRuntime Create(ItemDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            Func<IItemEffectRuntime> factory;
            return factories.TryGetValue(definition.EffectKind, out factory) ? factory() : null;
        }
    }

    public sealed class ItemRunRuntime
    {
        private readonly Dictionary<string, IItemEffectRuntime> effects =
            new Dictionary<string, IItemEffectRuntime>(StringComparer.Ordinal);
        private readonly ItemRunContext context;
        private bool effectsActivated;

        public ItemRunRuntime(
            ItemRunSnapshot snapshot,
            TeamState ownTeam,
            EnemyRegistry ownRouteEnemies,
            ItemCombatUnitRegistry unitRegistry = null,
            int runSeed = 0,
            TeamState opposingTeam = null,
            EnemyRegistry opposingRouteEnemies = null,
            ItemCombatUnitRegistry opposingUnitRegistry = null,
            IItemBenchCapacityPort benchCapacity = null,
            IItemSpellbreakerPort spellbreaker = null,
            IItemRunResourcePort runResource = null,
            IItemFreeRecruitPort freeRecruit = null,
            IItemForgePickPort forgePick = null,
            IItemEnemyDamagePort itemEnemyDamage = null,
            float initialCooldownSeconds = 0f)
        {
            if (initialCooldownSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCooldownSeconds));
            }
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            InitialCooldownDurationSeconds = initialCooldownSeconds;
            InitialCooldownRemainingSeconds = initialCooldownSeconds;
            context = new ItemRunContext(
                ownTeam,
                ownRouteEnemies,
                unitRegistry,
                runSeed,
                opposingTeam,
                opposingRouteEnemies,
                opposingUnitRegistry,
                benchCapacity,
                spellbreaker,
                runResource,
                freeRecruit,
                forgePick,
                itemEnemyDamage);
            BuildEffects();
        }

        public ItemRunSnapshot Snapshot { get; }
        public bool IsStarted { get; private set; }
        public float InitialCooldownDurationSeconds { get; }
        public float InitialCooldownRemainingSeconds { get; private set; }
        public bool IsInitialCooldownActive => IsStarted && InitialCooldownRemainingSeconds > 0.0001f;
        public bool AreEffectsActivated => effectsActivated;
        public float ElapsedSeconds => context.ElapsedSeconds;
        public ItemRunContext Context => context;
        public ItemCombatUnitRegistry UnitRegistry => context.UnitRegistry;
        public ItemCombatUnitRegistry OpposingUnitRegistry => context.OpposingUnitRegistry;

        public bool StartRun(out string reason)
        {
            reason = ItemOperationFailure.None;
            if (IsStarted)
            {
                return true;
            }

            IsStarted = true;
            if (InitialCooldownRemainingSeconds <= 0.0001f)
            {
                ActivateEffects();
            }
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsStarted || deltaSeconds <= 0f)
            {
                return;
            }

            context.ElapsedSeconds += deltaSeconds;
            if (InitialCooldownRemainingSeconds > 0.0001f)
            {
                var cooldownSlice = Math.Min(deltaSeconds, InitialCooldownRemainingSeconds);
                InitialCooldownRemainingSeconds = Math.Max(
                    0f,
                    InitialCooldownRemainingSeconds - deltaSeconds);
                deltaSeconds -= cooldownSlice;
                if (InitialCooldownRemainingSeconds > 0.0001f)
                {
                    return;
                }

                ActivateEffects();
            }

            if (!effectsActivated)
            {
                ActivateEffects();
            }
            if (deltaSeconds <= 0f)
            {
                return;
            }
            foreach (var effect in effects.Values)
            {
                effect.Tick(context, deltaSeconds);
            }
        }

        public bool TryUse(string itemId, out string reason)
        {
            return TryUse(itemId, null, out reason);
        }

        public bool TryUse(string itemId, string targetId, out string reason)
        {
            return TryUseInternal(itemId, targetId, false, default(CombatPoint), out reason);
        }

        public bool TryUseAtPoint(string itemId, CombatPoint activationPoint, out string reason)
        {
            return TryUseInternal(itemId, null, true, activationPoint, out reason);
        }

        private bool TryUseInternal(
            string itemId,
            string targetId,
            bool hasActivationPoint,
            CombatPoint activationPoint,
            out string reason)
        {
            reason = ItemOperationFailure.None;
            if (!IsStarted)
            {
                reason = "RunNotStarted";
                return false;
            }

            if (IsInitialCooldownActive)
            {
                reason = "InitialCooldown";
                return false;
            }

            if (!Snapshot.IsActive(itemId))
            {
                reason = "NotActiveInSnapshot";
                return false;
            }

            IItemEffectRuntime effect;
            if (!effects.TryGetValue(itemId, out effect))
            {
                reason = "EffectPending";
                return false;
            }

            context.ActivationTargetId = targetId;
            context.HasActivationPoint = hasActivationPoint;
            context.ActivationPoint = activationPoint;
            context.NextActivationOrdinal++;
            try
            {
                return effect.TryActivate(context, out reason);
            }
            finally
            {
                context.ActivationTargetId = null;
                context.HasActivationPoint = false;
                context.ActivationPoint = default(CombatPoint);
            }
        }

        public float GetCooldownRemainingSeconds(string itemId)
        {
            if (effects.TryGetValue(itemId, out var effect) && effect is ItemCooldownEffectBase cooldown)
            {
                return cooldown.CooldownRemainingSeconds;
            }

            if (effects.TryGetValue(itemId, out effect) && effect is WinterveilRuneEffect winterveil)
            {
                return winterveil.CooldownRemainingSeconds;
            }

            return 0f;
        }

        public float GetCooldownDurationSeconds(string itemId)
        {
            if (effects.TryGetValue(itemId, out var effect) && effect is ItemCooldownEffectBase cooldown)
            {
                return cooldown.CooldownSeconds;
            }

            if (effects.TryGetValue(itemId, out effect) && effect is WinterveilRuneEffect)
            {
                return WinterveilRuneEffect.CooldownSeconds;
            }

            return 0f;
        }

        public void HandleCombatEvent(ItemCombatEvent combatEvent)
        {
            if (!IsStarted || !effectsActivated)
            {
                return;
            }

            foreach (var effect in effects.Values)
            {
                effect.HandleCombatEvent(context, combatEvent);
            }
        }

        public bool TryGetEffect(string itemId, out IItemEffectRuntime effect)
        {
            return effects.TryGetValue(itemId, out effect);
        }

        public bool TryEvaluateBossCast(ItemBossCastAttempt attempt, out bool blocked, out string reason)
        {
            blocked = false;
            reason = ItemOperationFailure.None;
            if (!IsStarted)
            {
                reason = "RunNotStarted";
                return false;
            }

            if (IsInitialCooldownActive || !effectsActivated)
            {
                reason = "InitialCooldown";
                return false;
            }

            if (!effects.TryGetValue(ItemIds.SpellbreakerSeal, out var effect) ||
                !(effect is SpellbreakerSealEffect spellbreaker))
            {
                reason = "EffectPending";
                return false;
            }

            blocked = spellbreaker.TryBlockBossCast(context, attempt, out reason);
            return true;
        }

        private void BuildEffects()
        {
            AddEffects(Snapshot.ActiveItems);
            AddEffects(Snapshot.PassiveItems);
        }

        private void ActivateEffects()
        {
            if (effectsActivated)
            {
                return;
            }

            foreach (var effect in effects.Values)
            {
                effect.OnRunStart(context);
            }
            effectsActivated = true;
        }

        private void AddEffects(IReadOnlyList<string> itemIds)
        {
            for (var i = 0; i < itemIds.Count; i++)
            {
                var definition = ItemCatalog.Get(itemIds[i]);
                var effect = ItemEffectRuntimeFactory.Create(definition);
                if (effect != null)
                {
                    effects[itemIds[i]] = effect;
                }
            }
        }
    }
}
