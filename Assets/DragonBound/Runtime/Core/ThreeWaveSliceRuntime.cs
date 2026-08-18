using System;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Grid;
using DragonBound.Recruitment;
using UnityEngine;

namespace DragonBound.Core
{
    public enum ThreeWaveEnemyDurabilityProfile
    {
        BasicUnitBaseline,
        HeroSkillShowcase
    }

    public sealed class ThreeWaveSliceRuntime
    {
        public const int WaveCount = 3;
        public const float Wave1DurationSeconds = 24f;
        public const float Wave2DurationSeconds = 26f;
        public const float Wave3DurationSeconds = 28f;

        private static readonly WaveDefinition[] BasicUnitWaves =
        {
            new WaveDefinition(4, Wave1DurationSeconds, 30f, 30f),
            new WaveDefinition(5, Wave2DurationSeconds, 30f, 30f),
            new WaveDefinition(6, Wave3DurationSeconds, 30f, 30f)
        };

        // Hero skills need an enemy to survive across their first meaningful cadence:
        // Windclaw's five attacks and Dragon Rider's six-second dive cooldown.
        private static readonly WaveDefinition[] HeroSkillShowcaseWaves =
        {
            new WaveDefinition(4, Wave1DurationSeconds, 300f, 300f),
            new WaveDefinition(5, Wave2DurationSeconds, 340f, 340f),
            new WaveDefinition(6, Wave3DurationSeconds, 380f, 480f)
        };

        private readonly MatchController match;
        private readonly SideRuntime player;
        private readonly SideRuntime ai;
        private readonly WaveDefinition[] waves;
        private float waveElapsed;
        private int waveIndex = -1;
        private int lastElapsedLogSecond;
        private bool finalWaveEnded;

        public ThreeWaveSliceRuntime(
            MatchController match,
            BoardRecruitDestination playerDestination,
            BoardRecruitDestination aiDestination,
            ThreeWaveEnemyDurabilityProfile durabilityProfile = ThreeWaveEnemyDurabilityProfile.BasicUnitBaseline)
        {
            this.match = match ?? throw new ArgumentNullException(nameof(match));
            DurabilityProfile = durabilityProfile;
            waves = GetWaves(durabilityProfile);
            player = new SideRuntime(
                "Player",
                TeamSide.Player,
                match.Player,
                playerDestination,
                Emit,
                RaiseCombatEvent,
                durabilityProfile);
            ai = new SideRuntime(
                "AI",
                TeamSide.AI,
                match.AI,
                aiDestination,
                Emit,
                RaiseCombatEvent,
                durabilityProfile);
        }

        public bool IsComplete { get; private set; }
        public bool IsGameplayRunning => match.State == MatchState.Running;
        public ThreeWaveEnemyDurabilityProfile DurabilityProfile { get; }
        public int CurrentWave => waveIndex < 0 ? 0 : waveIndex + 1;
        public float WaveElapsedSeconds => waveElapsed;
        public float WaveDurationSeconds => waveIndex < 0 ? 0f : waves[waveIndex].DurationSeconds;
        public float WaveRemainingSeconds => Mathf.Max(0f, WaveDurationSeconds - waveElapsed);
        public string LastEvent { get; private set; } = "NONE";
        public int TotalGenerated => player.TotalGenerated + ai.TotalGenerated;
        public int TotalAttacks => player.TotalAttacks + ai.TotalAttacks;
        public int TotalKills => player.TotalKills + ai.TotalKills;
        public int TotalLeaked => player.TotalLeaked + ai.TotalLeaked;
        public int TotalResidual => player.TotalResidual + ai.TotalResidual;
        public EnemyRegistry PlayerEnemyRegistry => player.Registry;
        public EnemyRegistry AiEnemyRegistry => ai.Registry;
        public EnemyPath PlayerPath => player.Path;
        public EnemyPath AiPath => ai.Path;

        /// <summary>
        /// Returns the current three-wave greybox enemy durability. Enemy durability is
        /// deliberately owned by the wave slice rather than hero or basic-unit catalogs.
        /// </summary>
        public static float GetEnemyMaxHitPoints(int waveNumber, EnemyArchetype archetype)
        {
            return GetEnemyMaxHitPoints(
                ThreeWaveEnemyDurabilityProfile.BasicUnitBaseline,
                waveNumber,
                archetype);
        }

        public static float GetEnemyMaxHitPoints(
            ThreeWaveEnemyDurabilityProfile durabilityProfile,
            int waveNumber,
            EnemyArchetype archetype)
        {
            var profileWaves = GetWaves(durabilityProfile);
            if (waveNumber < 1 || waveNumber > profileWaves.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(waveNumber));
            }

            return profileWaves[waveNumber - 1].GetMaxHitPoints(archetype);
        }

        public event Action<CombatEvent> CombatEmitted;

        public void Tick(float deltaSeconds)
        {
            if (IsComplete || deltaSeconds <= 0f || match.State != MatchState.Running)
            {
                return;
            }

            if (waveIndex < 0)
            {
                StartWave(0);
            }

            waveElapsed += deltaSeconds;
            LogElapsedSeconds();
            player.Tick(deltaSeconds, CurrentWave);
            ai.Tick(deltaSeconds, CurrentWave);

            if (!finalWaveEnded && waveElapsed >= waves[waveIndex].DurationSeconds)
            {
                EndWave();
            }

            if (finalWaveEnded && player.Remaining == 0 && ai.Remaining == 0)
            {
                TrySettle();
            }
        }

        private void StartWave(int index)
        {
            waveIndex = index;
            waveElapsed = 0f;
            lastElapsedLogSecond = 0;
            player.StartWave(waves[index], CurrentWave);
            ai.StartWave(waves[index], CurrentWave);
            match.SetCurrentWave(CurrentWave);
            Emit(
                $"WaveStarted Wave={CurrentWave} DurationSeconds={waves[index].DurationSeconds:0} " +
                $"RemainingSeconds={waves[index].DurationSeconds:0}");
        }

        private void LogElapsedSeconds()
        {
            var elapsedSecond = Mathf.Min(
                Mathf.FloorToInt(waveElapsed),
                Mathf.CeilToInt(WaveDurationSeconds));
            while (lastElapsedLogSecond < elapsedSecond)
            {
                lastElapsedLogSecond++;
                Emit(
                    $"WaveElapsed Wave={CurrentWave} ElapsedSeconds={lastElapsedLogSecond} " +
                    $"RemainingSeconds={Mathf.Max(0, Mathf.CeilToInt(WaveDurationSeconds - lastElapsedLogSecond))}");
            }
        }

        private void EndWave()
        {
            player.RecordResidual(CurrentWave);
            ai.RecordResidual(CurrentWave);
            Emit(
                $"WaveFinished Wave={CurrentWave} ElapsedSeconds={Mathf.RoundToInt(waveElapsed)} " +
                $"RemainingSeconds=0 Residual={player.Remaining + ai.Remaining}");

            if (waveIndex >= waves.Length - 1)
            {
                finalWaveEnded = true;
                Emit("ThreeWave Wave=3 Event=FinalWaveEnded");
                return;
            }

            StartWave(waveIndex + 1);
        }

        private void TrySettle()
        {
            if (player.Remaining > 0 || ai.Remaining > 0)
            {
                return;
            }

            if (match.Player.HatchlingHealth <= 0 || match.AI.HatchlingHealth <= 0)
            {
                match.TryTransition(MatchState.Defeat);
                Emit("ThreeWave Result=Defeat");
            }
            else
            {
                match.TryTransition(MatchState.Victory);
                Emit("ThreeWave Result=Victory");
            }

            IsComplete = true;
        }

        private void RaiseCombatEvent(CombatEvent combatEvent)
        {
            CombatEmitted?.Invoke(combatEvent);
        }

        private void Emit(string message)
        {
            LastEvent = message;
            Debug.Log(message);
        }

        private static WaveDefinition[] GetWaves(ThreeWaveEnemyDurabilityProfile durabilityProfile)
        {
            switch (durabilityProfile)
            {
                case ThreeWaveEnemyDurabilityProfile.BasicUnitBaseline:
                    return BasicUnitWaves;
                case ThreeWaveEnemyDurabilityProfile.HeroSkillShowcase:
                    return HeroSkillShowcaseWaves;
                default:
                    throw new ArgumentOutOfRangeException(nameof(durabilityProfile));
            }
        }

        private readonly struct WaveDefinition
        {
            public WaveDefinition(
                int enemyCount,
                float durationSeconds,
                float normalAndFastHitPoints,
                float eliteHitPoints)
            {
                if (normalAndFastHitPoints <= 0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(normalAndFastHitPoints));
                }

                if (eliteHitPoints <= 0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(eliteHitPoints));
                }

                EnemyCount = enemyCount;
                DurationSeconds = durationSeconds;
                NormalAndFastHitPoints = normalAndFastHitPoints;
                EliteHitPoints = eliteHitPoints;
            }

            public int EnemyCount { get; }
            public float DurationSeconds { get; }
            public float NormalAndFastHitPoints { get; }
            public float EliteHitPoints { get; }

            public float GetMaxHitPoints(EnemyArchetype archetype)
            {
                return archetype == EnemyArchetype.Elite
                    ? EliteHitPoints
                    : NormalAndFastHitPoints;
            }
        }

        private sealed class SideRuntime
        {
            private const float EnemyTravelSeconds = 12f;

            private readonly string label;
            private readonly TeamSide side;
            private readonly TeamState team;
            private readonly BoardRecruitDestination destination;
            private readonly Action<string> emit;
            private readonly Action<CombatEvent> emitCombat;
            private readonly ThreeWaveEnemyDurabilityProfile durabilityProfile;
            private readonly TargetingSystem targeting = new TargetingSystem();
            private readonly Dictionary<string, float> attackElapsedByUnit =
                new Dictionary<string, float>(StringComparer.Ordinal);
            private int pending;
            private int nextEnemyNumber;
            private float spawnElapsed;
            private float spawnInterval;

            public SideRuntime(
                string label,
                TeamSide side,
                TeamState team,
                BoardRecruitDestination destination,
                Action<string> emit,
                Action<CombatEvent> emitCombat,
                ThreeWaveEnemyDurabilityProfile durabilityProfile)
            {
                this.label = label;
                this.side = side;
                this.team = team;
                this.destination = destination;
                this.emit = emit;
                this.emitCombat = emitCombat;
                this.durabilityProfile = durabilityProfile;
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

            private void HandleCombatRegistrationChanged(CombatRegistrationChangedEvent changed)
            {
                attackElapsedByUnit.Remove(changed.UnitId);
            }

            public EnemyRegistry Registry { get; } = new EnemyRegistry();
            public EnemyPath Path { get; }
            public PathDisplacementSystem PathDisplacement { get; }
            public int Remaining => pending + Registry.Count;
            public int TotalGenerated { get; private set; }
            public int TotalAttacks { get; private set; }
            public int TotalKills { get; private set; }
            public int TotalLeaked { get; private set; }
            public int TotalResidual { get; private set; }

            public void StartWave(WaveDefinition definition, int waveNumber)
            {
                pending += definition.EnemyCount;
                spawnInterval = definition.DurationSeconds / (definition.EnemyCount + 1f);
                spawnElapsed = spawnInterval;
                team.SetRemainingEnemyCount(Registry.Count);
                emit(
                    $"ThreeWave Wave={waveNumber} Side={label} Event=Queue " +
                    $"Count={definition.EnemyCount} Residual={Registry.Count + pending - definition.EnemyCount}");
            }

            public void Tick(float deltaSeconds, int waveNumber)
            {
                destination?.TickPairLinks(deltaSeconds);
                SpawnPending(deltaSeconds, waveNumber);
                MoveEnemies(deltaSeconds);
                ResolveAttacks(deltaSeconds);
                team.SetRemainingEnemyCount(Registry.Count);
            }

            private void SpawnPending(float deltaSeconds, int waveNumber)
            {
                spawnElapsed += deltaSeconds;
                while (pending > 0 && spawnElapsed >= spawnInterval)
                {
                    spawnElapsed -= spawnInterval;
                    pending--;
                    var enemyNumber = nextEnemyNumber++;
                    var runtimeId = $"{label.ToLowerInvariant()}.wave{waveNumber}.enemy{enemyNumber:00}";
                    var archetype = waveNumber == 3 && enemyNumber % 2 == 0
                        ? EnemyArchetype.Elite
                        : waveNumber == 2
                            ? EnemyArchetype.Fast
                            : EnemyArchetype.Normal;
                    var enemy = new EnemyRuntime(
                        runtimeId,
                        side,
                        ThreeWaveSliceRuntime.GetEnemyMaxHitPoints(
                            durabilityProfile,
                            waveNumber,
                            archetype),
                        archetype);
                    Path.PlaceAtSpawn(enemy);
                    Registry.Register(enemy);
                    TotalGenerated++;
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
                            deltaSeconds * enemy.MovementSpeedMultiplier,
                            EnemyTravelSeconds))
                    {
                        ResolveLeak(enemy);
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
                                !targeting.IsWithinRange(
                                    deployedUnit.CombatPosition,
                                    target,
                                    stats.RangeCells))
                            {
                                continue;
                            }

                            target.HitPoints = Math.Max(0f, target.HitPoints - stats.Attack);
                            var killed = target.HitPoints <= 0.0001f;
                            TotalAttacks++;
                            emit(
                                $"CombatAttack Team={side} Kind={stats.AttackKind} Attacker={attacker.RuntimeId} " +
                                $"Level={attacker.Level} Target={target.RuntimeId} " +
                                $"Damage={stats.Attack:0.00} HP={target.HitPoints:0.00} Killed={killed}");
                            emitCombat(new CombatEvent(
                                side,
                                stats.AttackKind,
                                attacker.RuntimeId,
                                target.RuntimeId,
                                stats.Attack,
                                killed,
                                false,
                                team.Resources));
                            if (killed)
                            {
                                ResolveKill(target);
                            }
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
                    var results = combat.TickCombat(
                        deltaSeconds,
                        activePair.CombatPosition,
                        Registry,
                        PathDisplacement);
                    foreach (var result in results)
                    {
                        TotalAttacks++;
                        emit(
                            $"HeroCombatAttack Team={side} Kind={result.Kind} " +
                            $"Attacker={pairLink.PairLinkId} RecipeId={pairLink.RecipeId} HeroId={pairLink.HeroId} Level={combat.Level} " +
                            $"Target={result.Target.RuntimeId} Damage={result.Damage:0.00} " +
                            $"HP={result.Target.HitPoints:0.00} Killed={result.Killed}");
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
                            result.EffectRadius));
                        if (result.Killed)
                        {
                            ResolveKill(result.Target);
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

            private void ResolveKill(EnemyRuntime enemy)
            {
                if (enemy.HasResolved)
                {
                    return;
                }

                enemy.HasResolved = true;
                enemy.State = EnemyRuntimeState.Dead;
                Registry.Remove(enemy.RuntimeId, out _);
                TotalKills++;
                team.AddResources(1);
                AwardHeroExperience(enemy.ExperienceReward);
                emit(
                    $"EnemyKilled RuntimeId={enemy.RuntimeId} Team={enemy.Team} " +
                    $"ResourcesAfter={team.Resources} RegistryCount={Registry.Count}");
            }

            private void AwardHeroExperience(int amount)
            {
                if (destination == null)
                {
                    return;
                }

                foreach (var activePair in destination.GetActiveHeroPairs())
                {
                    var pairLink = activePair.PairLink;
                    var combat = pairLink.CombatProxy;
                    var previousLevel = combat.Level;
                    combat.AddExperience(amount);
                    emit(
                        $"HeroExperience Team={side} PairLinkId={pairLink.PairLinkId} RecipeId={pairLink.RecipeId} HeroId={pairLink.HeroId} " +
                        $"Amount={amount} Experience={combat.Experience} Level={combat.Level}");
                    if (combat.Level != previousLevel)
                    {
                        emit(
                            $"HeroLevelUp Team={side} PairLinkId={pairLink.PairLinkId} " +
                            $"HeroId={pairLink.HeroId} Level={combat.Level}");
                    }
                }
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
                TotalLeaked++;
                team.ApplyHatchlingDamage(1);
                emit(
                    $"OnEnemyLeaked RuntimeId={enemy.RuntimeId} Team={enemy.Team} " +
                    $"PathIndex={enemy.PathIndex} PathProgress={enemy.PathProgress:0.000} " +
                    $"DragonGoal=DragonGoal Health={team.HatchlingHealth} RegistryCount={Registry.Count}");
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

            public void RecordResidual(int waveNumber)
            {
                if (Remaining <= 0)
                {
                    return;
                }

                TotalResidual += Remaining;
                emit(
                    $"WaveResidual Wave={waveNumber} Side={label} Count={Remaining} " +
                    $"RegistryCount={Registry.Count} Pending={pending}");
            }

        }
    }
}
