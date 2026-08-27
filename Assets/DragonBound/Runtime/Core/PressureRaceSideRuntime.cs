using System;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Bosses.Runtime;
using DragonBound.Grid;
using DragonBound.Items;
using DragonBound.Recruitment;

namespace DragonBound.Core
{
    /// <summary>One configured enemy spawn. Composition is selected by a wave runtime, not here.</summary>
    public readonly struct PressureRaceEnemySpawn
    {
        public PressureRaceEnemySpawn(
            EnemyArchetype archetype,
            float maxHitPoints,
            float moveSpeedMultiplier,
            float moveSpeedCellsPerSecond = 0f)
        {
            if (maxHitPoints <= 0f || moveSpeedMultiplier <= 0f || moveSpeedCellsPerSecond < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHitPoints));
            }

            Archetype = archetype;
            MaxHitPoints = maxHitPoints;
            MoveSpeedMultiplier = moveSpeedMultiplier;
            MoveSpeedCellsPerSecond = moveSpeedCellsPerSecond;
        }

        public EnemyArchetype Archetype { get; }
        public float MaxHitPoints { get; }
        public float MoveSpeedMultiplier { get; }
        /// <summary>
        /// When positive this is an absolute board-cell speed. It is converted once at spawn
        /// using the active path length so V2 does not change speed as waves advance.
        /// </summary>
        public float MoveSpeedCellsPerSecond { get; }
    }

    /// <summary>
    /// Shared per-side pressure-race combat runtime. Three-wave and twenty-wave scheduling both
    /// feed it spawn plans, so movement, targeting, rewards, experience, and leaks stay unified.
    /// </summary>
    public sealed class PressureRaceSideRuntime : IItemEnemyDamagePort
    {
        private const float EnemyTravelSeconds = 12f;

        private readonly string label;
        private readonly string eventPrefix;
        private readonly TeamSide side;
        private readonly TeamState team;
        private readonly BoardRecruitDestination destination;
        private readonly Action<string> emit;
        private readonly Action<CombatEvent> emitCombat;
        private readonly TargetingSystem targeting = new TargetingSystem();
        private readonly Dictionary<string, float> attackElapsedByUnit =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Queue<PressureRaceEnemySpawn> pendingSpawns =
            new Queue<PressureRaceEnemySpawn>();
        private readonly Dictionary<string, int> spawnWaveByEnemyId =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> approachingGoalNotified =
            new HashSet<string>(StringComparer.Ordinal);
        private IBloodcrownBasicPolicyPort bloodcrownBasicPolicy;
        private static readonly BloodcrownBasicModifierPipeline BloodcrownModifiers =
            new BloodcrownBasicModifierPipeline();
        private int nextEnemyNumber;
        private float spawnInterval;
        private float nextSpawnDelay;

        public PressureRaceSideRuntime(
            string label,
            string eventPrefix,
            TeamSide side,
            TeamState team,
            BoardRecruitDestination destination,
            Action<string> emit,
            Action<CombatEvent> emitCombat)
        {
            if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(eventPrefix))
            {
                throw new ArgumentException("A side label and event prefix are required.");
            }

            this.label = label;
            this.eventPrefix = eventPrefix;
            this.side = side;
            this.team = team ?? throw new ArgumentNullException(nameof(team));
            this.destination = destination;
            this.emit = emit ?? throw new ArgumentNullException(nameof(emit));
            this.emitCombat = emitCombat ?? throw new ArgumentNullException(nameof(emitCombat));
            if (destination != null)
            {
                destination.CombatRegistrationChanged += HandleCombatRegistrationChanged;
            }

            var layout = destination != null && destination.Board.Layout != null
                ? destination.Board.Layout
                : BattlefieldLayoutDefinitions.Default;
            var lane = layout.GetLane(side);
            Path = new EnemyPath(lane.NodeNames, lane.CombatPoints);
            PathDisplacement = new PathDisplacementSystem(Path);
        }

        public EnemyRegistry Registry { get; } = new EnemyRegistry();
        public EnemyPath Path { get; }
        public PathDisplacementSystem PathDisplacement { get; }
        public BoardRecruitDestination Destination => destination;
        public int Remaining => pendingSpawns.Count + Registry.Count;
        public int SpawnedThisWave { get; private set; }
        public int AliveEnemyCount => Registry.Count;
        public int TotalGenerated { get; private set; }
        public int TotalAttacks { get; private set; }
        public int TotalKills { get; private set; }
        public int TotalLeaked { get; private set; }
        public int TotalResidual { get; private set; }
        public int LastRecordedResidual { get; private set; }
        public event Action<EnemyLifecycleEvent> EnemyLifecycleEmitted;
        public event Action<string> EnemyApproachingGoal;

        public ItemEnemyDamageResult ApplyItemDamage(
            string itemId,
            string enemyRuntimeId,
            float damage)
        {
            if (string.IsNullOrWhiteSpace(itemId) || damage <= 0f ||
                !Registry.TryGet(enemyRuntimeId ?? string.Empty, out var enemy) ||
                !enemy.IsAlive)
            {
                return ItemEnemyDamageResult.Rejected;
            }

            enemy.RecordDamageOwner(new CombatDamageOwner(
                CombatDamageOwnerKind.Item,
                side,
                itemId));
            var damageResult = enemy.ApplyDamage(damage);
            TotalAttacks++;
            int heroXpAwarded = damageResult.Killed ? ResolveKill(enemy) : 0;
            emit(
                $"ItemCombatAttack Team={side} Item={itemId} Target={enemy.RuntimeId} " +
                $"Damage={damage:0.00} HP={enemy.HitPoints:0.00} Killed={damageResult.Killed}");
            emitCombat(new CombatEvent(
                side,
                AttackKind.Item,
                itemId,
                enemy.RuntimeId,
                damage,
                damageResult.Killed,
                false,
                team.Resources,
                damageOwnerKind: CombatDamageOwnerKind.Item,
                damageOwnerRuntimeId: itemId,
                experienceReward: enemy.ExperienceReward,
                heroXpAwarded: heroXpAwarded,
                shieldDamage: damageResult.ShieldDamage,
                healthDamage: damageResult.HealthDamage));
            return new ItemEnemyDamageResult(
                true,
                damageResult.Killed,
                damageResult.ShieldDamage,
                damageResult.HealthDamage);
        }

        public void SetBloodcrownBasicPolicy(IBloodcrownBasicPolicyPort policy)
        {
            bloodcrownBasicPolicy = policy;
            destination?.SetMergeBlockedProvider(() => bloodcrownBasicPolicy != null && bloodcrownBasicPolicy.IsMergeBlocked);
        }

        public void QueueWave(int waveNumber, float durationSeconds, IReadOnlyList<PressureRaceEnemySpawn> spawns)
        {
            if (waveNumber < 1 || durationSeconds <= 0f || spawns == null || spawns.Count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(spawns));
            }

            QueueWave(
                waveNumber,
                spawns,
                durationSeconds / (spawns.Count + 1f),
                firstSpawnDelaySeconds: 0f);
        }

        public void QueueWave(
            int waveNumber,
            IReadOnlyList<PressureRaceEnemySpawn> spawns,
            float spawnIntervalSeconds,
            float firstSpawnDelaySeconds)
        {
            if (waveNumber < 1 || spawns == null || spawns.Count < 1 ||
                spawnIntervalSeconds <= 0f || firstSpawnDelaySeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(spawns));
            }

            pendingSpawns.Clear();
            foreach (var spawn in spawns)
            {
                pendingSpawns.Enqueue(spawn);
            }

            SpawnedThisWave = 0;
            spawnInterval = spawnIntervalSeconds;
            nextSpawnDelay = firstSpawnDelaySeconds;
            team.SetRemainingEnemyCount(Registry.Count);
            emit(
                $"{eventPrefix} Wave={waveNumber} Side={label} Event=Queue " +
                $"Count={spawns.Count} Residual={Registry.Count}");

            // Waves after W1 begin immediately once the previous last-spawn gap ends.
            SpawnPending(0f, waveNumber);
        }

        public EnemyRuntime SpawnBoss(
            int waveNumber,
            string bossId,
            float maxHitPoints,
            float moveSpeedCellsPerSecond)
        {
            if (waveNumber < 1 || string.IsNullOrWhiteSpace(bossId) || maxHitPoints <= 0f ||
                moveSpeedCellsPerSecond <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(bossId));
            }

            var enemyNumber = nextEnemyNumber++;
            var runtimeId = $"{label.ToLowerInvariant()}.wave{waveNumber}.{bossId.ToLowerInvariant()}";
            var boss = new EnemyRuntime(
                runtimeId,
                side,
                maxHitPoints,
                EnemyArchetype.Boss,
                enemyNumber,
                bossId,
                waveNumber);
            var speedMultiplier = moveSpeedCellsPerSecond * EnemyTravelSeconds / Path.TotalDistance;
            boss.SetBaseMovementSpeedMultiplier(speedMultiplier);
            Path.PlaceAtSpawn(boss);
            Registry.Register(boss);
            spawnWaveByEnemyId[boss.RuntimeId] = waveNumber;
            TotalGenerated++;
            EnemyLifecycleEmitted?.Invoke(new EnemyLifecycleEvent(
                EnemyLifecycleEventKind.Spawned,
                waveNumber,
                boss.RuntimeId,
                boss.Archetype,
                boss.MaxHitPoints,
                boss.PathProgress));
            emit(
                $"EnemyBossSpawned RuntimeId={boss.RuntimeId} Team={boss.Team} " +
                $"BossId={bossId} Wave={waveNumber} HP={boss.MaxHitPoints:0.00} " +
                $"MoveSpeed={moveSpeedCellsPerSecond:0.00}");
            team.SetRemainingEnemyCount(Registry.Count);
            return boss;
        }

        public IReadOnlyList<EnemyRuntime> SpawnBossSummons(
            int waveNumber,
            string ownerBossId,
            string summonId,
            int count,
            float maxHitPoints,
            float moveSpeedCellsPerSecond,
            EnemyArchetype archetype = EnemyArchetype.Swarm)
        {
            if (waveNumber < 1 || string.IsNullOrWhiteSpace(ownerBossId) || string.IsNullOrWhiteSpace(summonId) ||
                count <= 0 || maxHitPoints <= 0f || moveSpeedCellsPerSecond <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(summonId));
            }

            var result = new List<EnemyRuntime>(count);
            for (var index = 0; index < count; index++)
            {
                var enemyNumber = nextEnemyNumber++;
                var runtimeId = $"{label.ToLowerInvariant()}.wave{waveNumber}.{summonId.ToLowerInvariant()}.{enemyNumber:00}";
                var summon = new EnemyRuntime(
                    runtimeId,
                    side,
                    maxHitPoints,
                    archetype,
                    enemyNumber,
                    summonId,
                    waveNumber);
                summon.SetBaseMovementSpeedMultiplier(moveSpeedCellsPerSecond * EnemyTravelSeconds / Path.TotalDistance);
                Path.PlaceAtSpawn(summon);
                Registry.Register(summon);
                spawnWaveByEnemyId[summon.RuntimeId] = waveNumber;
                TotalGenerated++;
                EnemyLifecycleEmitted?.Invoke(new EnemyLifecycleEvent(
                    EnemyLifecycleEventKind.Spawned,
                    waveNumber,
                    summon.RuntimeId,
                    summon.Archetype,
                    summon.MaxHitPoints,
                    summon.PathProgress));
                result.Add(summon);
            }

            team.SetRemainingEnemyCount(Registry.Count);
            emit($"EnemyBossSummonsSpawned Team={side} OwnerBossId={ownerBossId} SummonId={summonId} Count={count}");
            return result;
        }

        public bool RemoveEnemyWithoutRewards(string runtimeId)
        {
            if (string.IsNullOrWhiteSpace(runtimeId) || !Registry.Remove(runtimeId, out var enemy))
            {
                return false;
            }

            enemy.HasResolved = true;
            enemy.State = EnemyRuntimeState.Dead;
            team.SetRemainingEnemyCount(Registry.Count);
            return true;
        }

        public void Reset()
        {
            pendingSpawns.Clear();
            Registry.Clear();
            attackElapsedByUnit.Clear();
            nextEnemyNumber = 0;
            spawnInterval = 0f;
            nextSpawnDelay = 0f;
            SpawnedThisWave = 0;
            TotalGenerated = 0;
            TotalAttacks = 0;
            TotalKills = 0;
            TotalLeaked = 0;
            TotalResidual = 0;
            LastRecordedResidual = 0;
            spawnWaveByEnemyId.Clear();
            approachingGoalNotified.Clear();
            team.SetRemainingEnemyCount(0);
        }

        public void Tick(float deltaSeconds, int waveNumber)
        {
            destination?.TickPairLinks(deltaSeconds);
            SpawnPending(deltaSeconds, waveNumber);
            MoveEnemies(deltaSeconds);
            ResolveAttacks(deltaSeconds);
            team.SetRemainingEnemyCount(Registry.Count);
        }

        public void RecordResidual(int waveNumber)
        {
            LastRecordedResidual = Remaining;
            if (Remaining <= 0)
            {
                return;
            }

            TotalResidual += Remaining;
            emit(
                $"{eventPrefix} WaveResidual Wave={waveNumber} Side={label} Count={Remaining} " +
                $"RegistryCount={Registry.Count} Pending={pendingSpawns.Count}");
        }

        private void HandleCombatRegistrationChanged(CombatRegistrationChangedEvent changed)
        {
            attackElapsedByUnit.Remove(changed.UnitId);
        }

        private void SpawnPending(float deltaSeconds, int waveNumber)
        {
            nextSpawnDelay -= deltaSeconds;
            while (pendingSpawns.Count > 0 && nextSpawnDelay <= 0.0001f)
            {
                nextSpawnDelay += spawnInterval;
                var spawn = pendingSpawns.Dequeue();
                var enemyNumber = nextEnemyNumber++;
                var runtimeId = $"{label.ToLowerInvariant()}.wave{waveNumber}.enemy{enemyNumber:00}";
                var enemy = new EnemyRuntime(
                    runtimeId,
                    side,
                    spawn.MaxHitPoints,
                    spawn.Archetype,
                    enemyNumber,
                    spawnWaveIndex: waveNumber);
                var speedMultiplier = spawn.MoveSpeedCellsPerSecond > 0f
                    ? spawn.MoveSpeedCellsPerSecond * EnemyTravelSeconds / Path.TotalDistance
                    : spawn.MoveSpeedMultiplier;
                enemy.SetBaseMovementSpeedMultiplier(speedMultiplier);
                Path.PlaceAtSpawn(enemy);
                Registry.Register(enemy);
                spawnWaveByEnemyId[enemy.RuntimeId] = waveNumber;
                SpawnedThisWave++;
                TotalGenerated++;
                EnemyLifecycleEmitted?.Invoke(new EnemyLifecycleEvent(
                    EnemyLifecycleEventKind.Spawned,
                    waveNumber,
                    enemy.RuntimeId,
                    enemy.Archetype,
                    enemy.MaxHitPoints,
                    enemy.PathProgress));
                emit(
                    $"EnemySpawned RuntimeId={enemy.RuntimeId} Team={enemy.Team} " +
                    $"PathIndex={enemy.PathIndex} PathProgress={enemy.PathProgress:0.000} HP={enemy.HitPoints}");
            }
        }

        private void MoveEnemies(float deltaSeconds)
        {
            foreach (var enemy in Registry.Snapshot())
            {
                enemy.TickControl(deltaSeconds);
                if (enemy.IsStunned)
                {
                    continue;
                }

                if (Path.Advance(
                        enemy,
                        deltaSeconds * enemy.BaseMovementSpeedMultiplier * enemy.MovementSpeedMultiplier *
                        enemy.StormcallerMovementSpeedMultiplier,
                        EnemyTravelSeconds))
                {
                    ResolveLeak(enemy);
                }
                else if (enemy.PathProgress >= 0.80f &&
                         approachingGoalNotified.Add(enemy.RuntimeId))
                {
                    EnemyApproachingGoal?.Invoke(enemy.RuntimeId);
                }
            }
        }

        private void ResolveAttacks(float deltaSeconds)
        {
            var deployed = destination == null
                ? new List<DeployedBasicUnit>()
                : destination.GetDeployedUnits();
            if (deployed.Count == 0)
            {
                attackElapsedByUnit.Clear();
                ResolveHeroAttacks(deltaSeconds);
                return;
            }

            RemoveStaleAttackTimers(deployed);
            foreach (var deployedUnit in deployed)
            {
                var attacker = deployedUnit.Card;
                var stats = BasicUnitCatalog.GetStats(attacker.ConfigId, attacker.Level);
                if (bloodcrownBasicPolicy != null && bloodcrownBasicPolicy.IsDecreeActive)
                {
                    var levelOne = BasicUnitCatalog.GetStats(attacker.ConfigId, bloodcrownBasicPolicy.EffectiveCombatLevel);
                    var projected = BloodcrownBasicCombatPolicy.Apply(
                        new BloodcrownBasicCombatInput(
                            attacker.Level,
                            levelOne.Attack,
                            levelOne.AttackSpeed,
                            stats.RangeCells),
                        BloodcrownModifiers);
                    stats = new BasicUnitStats(
                        stats.Archetype,
                        stats.Level,
                        projected.Attack,
                        projected.AttackSpeed,
                        projected.Range,
                        stats.AttackKind);
                }
                attackElapsedByUnit.TryGetValue(attacker.RuntimeId, out var attackElapsed);
                if (deployedUnit.IsCombatSuspended)
                {
                    continue;
                }

                attackElapsed += deltaSeconds;
                var attackInterval = stats.AttackIntervalSeconds;
                while (attackElapsed + 0.0001f >= attackInterval)
                {
                    var targetCount = stats.AttackKind == AttackKind.Single ||
                                      stats.AttackKind == AttackKind.BowProjectile
                        ? 1
                        : 2;
                    var targets = targeting.SelectFrontmostInRange(
                        deployedUnit.CombatPosition,
                        stats.RangeCells,
                        Registry.Snapshot(),
                        targetCount);
                    if (targets.Count == 0)
                    {
                        attackElapsed = Math.Min(attackElapsed, attackInterval);
                        break;
                    }

                    attackElapsed -= attackInterval;
                    foreach (var selectedTarget in targets)
                    {
                        if (!Registry.TryGet(selectedTarget.RuntimeId, out var target) ||
                            !targeting.IsWithinRange(deployedUnit.CombatPosition, target, stats.RangeCells))
                        {
                            continue;
                        }

                        target.RecordDamageOwner(new CombatDamageOwner(
                            CombatDamageOwnerKind.BasicUnit,
                            side,
                            attacker.RuntimeId));
                        var damageResult = target.ApplyDamage(stats.Attack);
                        var killed = damageResult.Killed;
                        TotalAttacks++;
                        emit(
                            $"CombatAttack Team={side} Kind={stats.AttackKind} Attacker={attacker.RuntimeId} " +
                            $"Level={attacker.Level} Target={target.RuntimeId} Damage={stats.Attack:0.00} " +
                            $"HP={target.HitPoints:0.00} Killed={killed}");
                        var heroXpAwarded = killed ? ResolveKill(target) : 0;
                        emitCombat(new CombatEvent(
                            side,
                            stats.AttackKind,
                            attacker.RuntimeId,
                            target.RuntimeId,
                            stats.Attack,
                            killed,
                            false,
                            team.Resources,
                            damageOwnerKind: CombatDamageOwnerKind.BasicUnit,
                            damageOwnerRuntimeId: attacker.RuntimeId,
                            experienceReward: target.ExperienceReward,
                            heroXpAwarded: heroXpAwarded,
                            shieldDamage: damageResult.ShieldDamage,
                            healthDamage: damageResult.HealthDamage));
                    }
                }

                attackElapsedByUnit[attacker.RuntimeId] = attackElapsed;
            }

            ResolveHeroAttacks(deltaSeconds);
        }

        private void ResolveHeroAttacks(float deltaSeconds)
        {
            if (destination == null)
            {
                return;
            }

            foreach (var activePair in destination.GetActiveHeroPairs())
            {
                var pairLink = activePair.PairLink;
                var combat = pairLink.CombatProxy;
                var results = combat.TickCombat(deltaSeconds, activePair.CombatPosition, Registry, PathDisplacement);
                ApplyRuneWarcries(combat.DrainWarcries());
                for (var index = 0; index < results.Count; index++)
                {
                    var result = results[index];
                    result.Target.RecordDamageOwner(new CombatDamageOwner(
                        CombatDamageOwnerKind.Hero,
                        side,
                        pairLink.PairLinkId,
                        pairLink.HeroId));
                    TotalAttacks++;
                    emit(
                        $"HeroCombatAttack Team={side} Kind={result.Kind} Attacker={pairLink.PairLinkId} " +
                        $"RecipeId={pairLink.RecipeId} HeroId={pairLink.HeroId} Level={combat.Level} " +
                        $"Target={result.Target.RuntimeId} Damage={result.Damage:0.00} " +
                        $"HP={result.Target.HitPoints:0.00} Killed={result.Killed}");
                    var heroXpAwarded = result.Killed ? ResolveKill(result.Target) : 0;
                    if (result.Killed && result.Target.Archetype != EnemyArchetype.Swarm &&
                        !IsRewardlessBossSummon(result.Target))
                    {
                        results.AddRange(combat.NotifyHeroKill(result.Target, Registry, result.IsRuneDerived));
                    }
                    emitCombat(new CombatEvent(
                        side,
                        result.Kind,
                        pairLink.PairLinkId,
                        result.Target.RuntimeId,
                        result.Damage,
                        result.Killed,
                        false,
                        team.Resources,
                        result.EffectDuration,
                        result.EffectRadius,
                        CombatDamageOwnerKind.Hero,
                        pairLink.PairLinkId,
                        pairLink.HeroId,
                        result.Target.ExperienceReward,
                        heroXpAwarded,
                        combat.Level,
                        result.ShieldDamage,
                        result.HealthDamage));
                }
            }
        }

        private void ApplyRuneWarcries(IReadOnlyList<DragonBound.Runes.RuneDamageResult> warcries)
        {
            if (warcries == null || destination == null)
            {
                return;
            }

            foreach (var warcry in warcries)
            {
                if (!warcry.IsWarcry)
                {
                    continue;
                }

                var radiusSquared = warcry.EffectRadius * warcry.EffectRadius;
                foreach (var activePair in destination.GetActiveHeroPairs())
                {
                    if (activePair.CombatPosition.DistanceSquared(warcry.WarcryCenter) <= radiusSquared + 0.0001f)
                    {
                        activePair.PairLink.CombatProxy.ApplyRuneAttackSpeedBuff(
                            warcry.WarcryMultiplier,
                            warcry.WarcryDuration);
                    }
                }
            }
        }

        private void RemoveStaleAttackTimers(IReadOnlyList<DeployedBasicUnit> deployed)
        {
            var activeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var unit in deployed)
            {
                activeIds.Add(unit.Card.RuntimeId);
            }

            var staleIds = new List<string>();
            foreach (var runtimeId in attackElapsedByUnit.Keys)
            {
                if (!activeIds.Contains(runtimeId))
                {
                    staleIds.Add(runtimeId);
                }
            }

            foreach (var runtimeId in staleIds)
            {
                attackElapsedByUnit.Remove(runtimeId);
            }
        }

        private int ResolveKill(EnemyRuntime enemy)
        {
            if (enemy.HasResolved)
            {
                return 0;
            }

            enemy.HasResolved = true;
            enemy.State = EnemyRuntimeState.Dead;
            Registry.Remove(enemy.RuntimeId, out _);
            EmitEnemyLifecycle(EnemyLifecycleEventKind.Killed, enemy);
            TotalKills++;
            if (enemy.Archetype != EnemyArchetype.Swarm && !IsRewardlessBossSummon(enemy))
            {
                team.AddResources(1);
            }
            var heroXpAwarded = AwardHeroExperience(enemy);
            emit($"EnemyKilled RuntimeId={enemy.RuntimeId} Team={enemy.Team} ResourcesAfter={team.Resources} RegistryCount={Registry.Count}");
            return heroXpAwarded;
        }

        private static bool IsRewardlessBossSummon(EnemyRuntime enemy)
        {
            return enemy != null &&
                   string.Equals(
                       enemy.BossId,
                       WorldeaterWyrmConfiguration.SubBossId,
                       StringComparison.Ordinal);
        }

        private int AwardHeroExperience(EnemyRuntime enemy)
        {
            if (destination == null)
            {
                return 0;
            }

            var owner = enemy.LastDamageOwner;
            var amount = HeroXpSettlement.GetAwardedExperience(enemy);
            if (amount <= 0 || owner.Kind != CombatDamageOwnerKind.Hero ||
                owner.Side != side || !destination.TryGetPairLink(owner.SourceRuntimeId, out var ownedPair))
            {
                return 0;
            }

            var combat = ownedPair.CombatProxy;
            var previousLevel = combat.Level;
            var previousExperience = combat.Experience;
            combat.AddExperience(amount);
            var awarded = combat.Experience > previousExperience;
            emit(
                $"HeroExperience Team={side} PairLinkId={ownedPair.PairLinkId} RecipeId={ownedPair.RecipeId} " +
                $"HeroId={ownedPair.HeroId} Amount={(awarded ? amount : 0)} Experience={combat.Experience} Level={combat.Level}");
            if (combat.Level != previousLevel)
            {
                combat.NotifyHeroLevelUp();
                emit($"HeroLevelUp Team={side} PairLinkId={ownedPair.PairLinkId} HeroId={ownedPair.HeroId} Level={combat.Level}");
            }

            return awarded ? amount : 0;
        }

        private void ResolveLeak(EnemyRuntime enemy)
        {
            if (enemy.HasResolved)
            {
                return;
            }

            enemy.HasResolved = true;
            enemy.State = EnemyRuntimeState.Leaked;
            Registry.Remove(enemy.RuntimeId, out _);
            EmitEnemyLifecycle(EnemyLifecycleEventKind.Leaked, enemy);
            TotalLeaked++;
            if (enemy.Archetype == EnemyArchetype.Boss || enemy.Archetype == EnemyArchetype.Swarm)
            {
                team.ApplyBossGoalInstantDefeat();
            }
            else
            {
                team.ApplyHatchlingDamage(BattleSettlementDefinition.NormalGoalDamage);
            }
            emit(
                $"OnEnemyLeaked RuntimeId={enemy.RuntimeId} Team={enemy.Team} " +
                $"PathIndex={enemy.PathIndex} PathProgress={enemy.PathProgress:0.000} " +
                $"DragonGoal={(enemy.Archetype == EnemyArchetype.Boss || enemy.Archetype == EnemyArchetype.Swarm ? "InstantDefeat" : "HeartDamage")} " +
                $"Health={team.HatchlingHealth} RegistryCount={Registry.Count}");
            emitCombat(new CombatEvent(
                side,
                AttackKind.Single,
                string.Empty,
                enemy.RuntimeId,
                0,
                false,
                true,
                team.Resources));
        }

        private void EmitEnemyLifecycle(EnemyLifecycleEventKind kind, EnemyRuntime enemy)
        {
            if (enemy == null)
            {
                return;
            }

            var spawnWave = 0;
            if (spawnWaveByEnemyId.TryGetValue(enemy.RuntimeId, out var recordedWave))
            {
                spawnWave = recordedWave;
                spawnWaveByEnemyId.Remove(enemy.RuntimeId);
            }

            EnemyLifecycleEmitted?.Invoke(new EnemyLifecycleEvent(
                kind,
                spawnWave,
                enemy.RuntimeId,
                enemy.Archetype,
                enemy.MaxHitPoints,
                enemy.PathProgress));
        }
    }
}
