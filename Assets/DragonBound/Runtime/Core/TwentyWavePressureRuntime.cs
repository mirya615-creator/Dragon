using System;
using System.Collections.Generic;
using DragonBound.Bosses.Contracts;
using DragonBound.Bosses.Runtime;
using DragonBound.Foundation.Contracts;
using DragonBound.Items;
using DragonBound.Recruitment;
using DragonBound.Runes;
using GameShared.Random;
using UnityEngine;

namespace DragonBound.Core
{
    /// <summary>
    /// Configuration-driven, overlapping twenty-wave pressure race. It schedules the shared
    /// side runtime and intentionally has no alternate enemy movement, combat, or reward code.
    /// </summary>
    public sealed class TwentyWavePressureRuntime : IWaveRuntime
    {
        private const string PlayerCompositionStream = "EnemyComposition.Player";
        private const string AiCompositionStream = "EnemyComposition.AI";
        private const string CompositionRngVersion = "PressureComposition.v1";

        private readonly MatchController match;
        private readonly int runSeed;
        private readonly PressureRaceSideRuntime player;
        private readonly PressureRaceSideRuntime ai;
        private readonly TwentyWavePressureConfiguration configuration;
        private readonly RuneRunRewardService playerRuneRewards;
        private readonly bool soulChainEnabled;
        private readonly IItemRunSnapshotProvider itemSnapshotProvider;
        private readonly float soulChainBossMaxHitPoints;
        private readonly float stormcallerBossMaxHitPoints;
        private readonly float bloodcrownBossMaxHitPoints;
        private readonly float worldeaterBossMaxHitPoints;
        private float waveElapsedTime;
        private float elapsedRunTime;
        private int currentWaveIndex;
        private bool started;
        private bool paused;
        private bool wavesExhausted;
        private ISoulChainSpellbreakerResolver playerSpellbreakerResolver;
        private ISoulChainSpellbreakerResolver aiSpellbreakerResolver;
        private SoulchainBinderRuntime playerW6Boss;
        private SoulchainBinderRuntime aiW6Boss;
        private StormcallerPriestRuntime playerW12Boss;
        private StormcallerPriestRuntime aiW12Boss;
        private BloodcrownTyrantRuntime playerW16Boss;
        private BloodcrownTyrantRuntime aiW16Boss;
        private BloodcrownIntegrationAdapter playerW16Policy;
        private BloodcrownIntegrationAdapter aiW16Policy;
        private WorldeaterWyrmRuntime playerW20Boss;
        private WorldeaterWyrmRuntime aiW20Boss;
        private WorldeaterIntegrationAdapter playerW20Policy;
        private WorldeaterIntegrationAdapter aiW20Policy;
        private ItemRunRuntime playerItems;
        private ItemRunRuntime aiItems;

        public TwentyWavePressureRuntime(
            MatchController match,
            BoardRecruitDestination playerDestination,
            BoardRecruitDestination aiDestination,
            int runSeed,
            TwentyWavePressureConfiguration suppliedConfiguration = null,
            RuneRunRewardService playerRuneRewards = null,
            bool soulChainEnabled = true,
            IItemRunSnapshotProvider itemSnapshotProvider = null,
            float soulChainBossMaxHitPoints = SoulchainBinderConfiguration.GreyboxMaxHitPoints,
            float stormcallerBossMaxHitPoints = StormcallerPriestConfiguration.GreyboxMaxHitPoints,
            float bloodcrownBossMaxHitPoints = BloodcrownTyrantConfiguration.GreyboxMaxHitPoints,
            float worldeaterBossMaxHitPoints = WorldeaterWyrmConfiguration.GreyboxMaxHitPoints)
        {
            this.match = match ?? throw new ArgumentNullException(nameof(match));
            this.runSeed = runSeed;
            this.playerRuneRewards = playerRuneRewards;
            this.soulChainEnabled = soulChainEnabled;
            this.itemSnapshotProvider = itemSnapshotProvider ?? new EmptyItemRunSnapshotProvider();
            if (soulChainBossMaxHitPoints <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(soulChainBossMaxHitPoints));
            }

            if (stormcallerBossMaxHitPoints <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(stormcallerBossMaxHitPoints));
            }

            if (bloodcrownBossMaxHitPoints <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(bloodcrownBossMaxHitPoints));
            }

            if (worldeaterBossMaxHitPoints <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(worldeaterBossMaxHitPoints));
            }

            this.soulChainBossMaxHitPoints = soulChainBossMaxHitPoints;
            this.stormcallerBossMaxHitPoints = stormcallerBossMaxHitPoints;
            this.bloodcrownBossMaxHitPoints = bloodcrownBossMaxHitPoints;
            this.worldeaterBossMaxHitPoints = worldeaterBossMaxHitPoints;
            configuration = suppliedConfiguration ?? TwentyWavePressureConfiguration.CreateGreyboxV1();
            player = new PressureRaceSideRuntime(
                "Player", "TwentyWave", TeamSide.Player, match.Player, playerDestination, Emit, RaiseCombatEvent);
            ai = new PressureRaceSideRuntime(
                "AI", "TwentyWave", TeamSide.AI, match.AI, aiDestination, Emit, RaiseCombatEvent);
            player.EnemyLifecycleEmitted += value => PlayerEnemyLifecycleEmitted?.Invoke(value);
            ai.EnemyLifecycleEmitted += value => AiEnemyLifecycleEmitted?.Invoke(value);
            if (playerDestination != null)
            {
                playerDestination.BasicUnitMerged += value => playerW6Boss?.NotifyMerge(value.SourceUnitId, value.TargetUnitId);
            }

            if (aiDestination != null)
            {
                aiDestination.BasicUnitMerged += value => aiW6Boss?.NotifyMerge(value.SourceUnitId, value.TargetUnitId);
            }
        }

        public event Action<CombatEvent> CombatEmitted;
        public event Action<RuneReward> PlayerRuneRewardGranted;
        /// <summary>Development telemetry only; it forwards the shared enemy-runtime lifecycle.</summary>
        public event Action<EnemyLifecycleEvent> PlayerEnemyLifecycleEmitted;
        /// <summary>Development telemetry only; it forwards the shared enemy-runtime lifecycle.</summary>
        public event Action<EnemyLifecycleEvent> AiEnemyLifecycleEmitted;
        public event Action<TeamSide, SoulChainCastEvent> SoulChainCastEmitted;
        public event Action<TeamSide, StormcallerCastEvent> StormcallerCastEmitted;
        public event Action<TeamSide, BossSkillLifecycleEvent> BloodcrownLifecycleEmitted;
        public event Action<TeamSide, BossCastResult> BloodcrownCastEmitted;
        public event Action<TeamSide, WorldeaterCastEvent> WorldeaterCastEmitted;

        public TwentyWavePressureConfiguration Configuration => configuration;
        public string RngVersion => CompositionRngVersion;
        public bool IsStarted => started;
        public bool IsPaused => paused;
        public bool WavesExhausted => wavesExhausted;
        public bool RegularWaveScheduleCompleted => wavesExhausted;
        public bool SoulChainEnabled => soulChainEnabled;
        /// <summary>Calibration-only override; production defaults to the greybox value.</summary>
        public float SoulChainBossMaxHitPoints => soulChainBossMaxHitPoints;
        /// <summary>Calibration-only override; production defaults to the W12 greybox value.</summary>
        public float StormcallerBossMaxHitPoints => stormcallerBossMaxHitPoints;
        public bool IsComplete { get; private set; }
        public bool EmitLogs { get; set; } = true;
        public bool IsRunEnded => IsComplete;
        public int CurrentWaveIndex => currentWaveIndex;
        public bool IsGameplayRunning => started && !paused && match.State == MatchState.Running;
        public int CurrentWave => CurrentWaveIndex;
        public float WaveElapsedTime => waveElapsedTime;
        public float ElapsedRunTime => elapsedRunTime;
        public float WaveDuration => currentWaveIndex == 0 ? 0f : configuration.GetWave(currentWaveIndex).WaveDurationSeconds;
        public float WaveRemainingTime => Mathf.Max(0f, WaveDuration - waveElapsedTime);
        public float WaveDurationSeconds => WaveDuration;
        public float WaveRemainingSeconds => WaveRemainingTime;
        public int PlayerSpawnedThisWave => player.SpawnedThisWave;
        public int AiSpawnedThisWave => ai.SpawnedThisWave;
        public int SpawnedThisWave => player.SpawnedThisWave + ai.SpawnedThisWave;
        public int PlayerAliveEnemyCount => player.AliveEnemyCount;
        public int AiAliveEnemyCount => ai.AliveEnemyCount;
        public int AliveEnemyCount => player.AliveEnemyCount + ai.AliveEnemyCount;
        public int TotalSpawnedPerSide => player.TotalGenerated;
        public int PlayerTotalSpawned => player.TotalGenerated;
        public int AiTotalSpawned => ai.TotalGenerated;
        public int TotalKilledPerSide => player.TotalKills;
        public int PlayerTotalKilled => player.TotalKills;
        public int AiTotalKilled => ai.TotalKills;
        public int TotalReachedGoalPerSide => player.TotalLeaked;
        public int PlayerTotalReachedGoal => player.TotalLeaked;
        public int AiTotalReachedGoal => ai.TotalLeaked;
        /// <summary>Residual enemies recorded as the previous wave ended, before the next wave queues.</summary>
        public int PlayerLastEndedWaveResidual => player.LastRecordedResidual;
        /// <summary>Residual enemies recorded as the previous wave ended, before the next wave queues.</summary>
        public int AiLastEndedWaveResidual => ai.LastRecordedResidual;
        public EnemyRegistry PlayerEnemyRegistry => player.Registry;
        public EnemyRegistry AiEnemyRegistry => ai.Registry;
        public EnemyPath PlayerPath => player.Path;
        public EnemyPath AiPath => ai.Path;
        public SoulchainBinderRuntime PlayerW6BossRuntime => playerW6Boss;
        public SoulchainBinderRuntime AiW6BossRuntime => aiW6Boss;
        public EnemyRuntime PlayerW6Boss => playerW6Boss?.Boss;
        public EnemyRuntime AiW6Boss => aiW6Boss?.Boss;
        public StormcallerPriestRuntime PlayerW12BossRuntime => playerW12Boss;
        public StormcallerPriestRuntime AiW12BossRuntime => aiW12Boss;
        public EnemyRuntime PlayerW12Boss => playerW12Boss?.Boss;
        public EnemyRuntime AiW12Boss => aiW12Boss?.Boss;
        public BloodcrownTyrantRuntime PlayerW16BossRuntime => playerW16Boss;
        public BloodcrownTyrantRuntime AiW16BossRuntime => aiW16Boss;
        public EnemyRuntime PlayerW16Boss => playerW16Policy?.Boss;
        public EnemyRuntime AiW16Boss => aiW16Policy?.Boss;
        public WorldeaterWyrmRuntime PlayerW20BossRuntime => playerW20Boss;
        public WorldeaterWyrmRuntime AiW20BossRuntime => aiW20Boss;
        public EnemyRuntime PlayerW20Boss => playerW20Policy?.Boss;
        public EnemyRuntime AiW20Boss => aiW20Policy?.Boss;
        public ItemRunRuntime PlayerItems => playerItems;
        public ItemRunRuntime AiItems => aiItems;
        public string LastEvent { get; private set; } = "NONE";

        public void SetSpellbreakerResolver(TeamSide side, ISoulChainSpellbreakerResolver resolver)
        {
            if (started)
            {
                throw new InvalidOperationException("Spellbreaker resolver must be configured before the run starts.");
            }

            if (side == TeamSide.Player)
            {
                playerSpellbreakerResolver = resolver;
            }
            else
            {
                aiSpellbreakerResolver = resolver;
            }
        }

        public bool StartRun()
        {
            if (IsComplete || started)
            {
                return false;
            }

            if (!StartItemRuntimes())
            {
                return false;
            }

            if (!EnsureRunning())
            {
                return false;
            }

            started = true;
            BeginWave(1);
            return true;
        }

        /// <summary>Developer-only restart of pressure scheduling and enemy registries.</summary>
        public bool RestartRun()
        {
            if (match.State == MatchState.Victory || match.State == MatchState.Defeat)
            {
                return false;
            }

            player.Reset();
            ai.Reset();
            currentWaveIndex = 0;
            waveElapsedTime = 0f;
            elapsedRunTime = 0f;
            started = false;
            paused = false;
            wavesExhausted = false;
            IsComplete = false;
            playerW6Boss = null;
            aiW6Boss = null;
            playerW12Boss = null;
            aiW12Boss = null;
            playerW16Boss = null;
            aiW16Boss = null;
            playerW16Policy = null;
            aiW16Policy = null;
            playerW20Boss = null;
            aiW20Boss = null;
            playerW20Policy = null;
            aiW20Policy = null;
            player.SetBloodcrownBasicPolicy(null);
            ai.SetBloodcrownBasicPolicy(null);
            return StartRun();
        }

        /// <summary>Developer-only jump. Existing spawned enemies remain active; only pending spawns are replaced.</summary>
        public bool JumpToWave(int wave)
        {
            if (wave < 1 || wave > TwentyWavePressureConfiguration.WaveCount || IsComplete)
            {
                return false;
            }

            if (!started && !StartRun())
            {
                return false;
            }

            if (paused || !EnsureRunning())
            {
                return false;
            }

            BeginWave(wave);
            return true;
        }

        public bool PauseWave()
        {
            if (!started || paused || IsComplete || match.State != MatchState.Running)
            {
                return false;
            }

            if (!match.TryTransition(MatchState.Paused))
            {
                return false;
            }

            paused = true;
            Emit($"TwentyWave Event=Paused Wave={currentWaveIndex}");
            return true;
        }

        public bool ResumeWave()
        {
            if (!started || !paused || IsComplete || match.State != MatchState.Paused)
            {
                return false;
            }

            if (!match.TryTransition(MatchState.Running))
            {
                return false;
            }

            paused = false;
            Emit($"TwentyWave Event=Resumed Wave={currentWaveIndex}");
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (!started || paused || IsComplete || deltaSeconds <= 0f || match.State != MatchState.Running)
            {
                return;
            }

            waveElapsedTime += deltaSeconds;
            elapsedRunTime += deltaSeconds;
            player.Tick(deltaSeconds, currentWaveIndex);
            ai.Tick(deltaSeconds, currentWaveIndex);
            playerItems?.Tick(deltaSeconds);
            aiItems?.Tick(deltaSeconds);
            playerW6Boss?.Tick(deltaSeconds);
            aiW6Boss?.Tick(deltaSeconds);
            playerW12Boss?.Tick(deltaSeconds);
            aiW12Boss?.Tick(deltaSeconds);
            playerW16Boss?.Tick(deltaSeconds);
            aiW16Boss?.Tick(deltaSeconds);
            playerW20Boss?.Tick(deltaSeconds);
            aiW20Boss?.Tick(deltaSeconds);

            // Base death is the only gameplay settlement trigger. Schedule completion is
            // deliberately independent: pressure can continue to be observed after W20.
            if (match.Player.HatchlingHealth <= 0 || match.AI.HatchlingHealth <= 0 ||
                match.Player.IsInstantDefeated || match.AI.IsInstantDefeated)
            {
                SettleRun();
                return;
            }

            if (!wavesExhausted && waveElapsedTime >= WaveDuration)
            {
                EndCurrentWave();
            }
        }

        /// <summary>Developer-only stop. It does not transition MatchState or claim a winner.</summary>
        public bool StopRun()
        {
            if (!started || IsComplete)
            {
                return false;
            }

            IsComplete = true;
            Emit($"TwentyWave Event=DeveloperStopped Wave={currentWaveIndex}");
            return true;
        }

        /// <summary>Single formal Active Item command path for player and AI.</summary>
        public bool TryUseItem(TeamSide side, string itemId, out string reason)
        {
            var runtime = side == TeamSide.Player ? playerItems : aiItems;
            if (runtime == null)
            {
                reason = "RunNotStarted";
                return false;
            }

            return runtime.TryUse(itemId, out reason);
        }

        /// <summary>
        /// Produces one side's deterministic type plan. Player and AI use separate random
        /// objects and stream labels, deliberately initialized from the same canonical seed so
        /// that the pressure is symmetric while still isolated from every other random system.
        /// </summary>
        public IReadOnlyList<EnemyArchetype> GetWaveComposition(int wave, TeamSide side)
        {
            var definition = configuration.GetWave(wave);
            var random = new RunRandom(DeriveCompositionSeed(runSeed, wave));
            var stream = side == TeamSide.Player ? PlayerCompositionStream : AiCompositionStream;
            var composition = new EnemyArchetype[definition.EnemyCountPerSide];
            for (var index = 0; index < composition.Length; index++)
            {
                var roll = random.NextUnit(stream + ".W" + wave + "." + index) * definition.TotalWeight;
                composition[index] = roll < definition.NormalWeight
                    ? EnemyArchetype.Normal
                    : roll < definition.NormalWeight + definition.FastWeight
                        ? EnemyArchetype.Fast
                        : EnemyArchetype.Elite;
            }

            return composition;
        }

        public IReadOnlyList<PressureRaceEnemySpawn> GetWaveSpawnPlan(int wave, TeamSide side)
        {
            var definition = configuration.GetWave(wave);
            var composition = GetWaveComposition(wave, side);
            var spawns = new PressureRaceEnemySpawn[composition.Count];
            for (var index = 0; index < spawns.Length; index++)
            {
                spawns[index] = new PressureRaceEnemySpawn(
                    composition[index],
                    EnemyRuntime.DefaultMaxHitPoints * definition.HealthMultiplier,
                    definition.MoveSpeedMultiplier,
                    configuration.GetMoveSpeedCellsPerSecond(composition[index]));
            }

            return spawns;
        }

        private bool EnsureRunning()
        {
            if (match.State == MatchState.Running)
            {
                return true;
            }

            if (match.State == MatchState.Initializing)
            {
                match.TryTransition(MatchState.Ready);
            }

            if (match.State == MatchState.Paused)
            {
                return match.TryTransition(MatchState.Running);
            }

            return match.State == MatchState.Ready && match.TryTransition(MatchState.Running);
        }

        private bool StartItemRuntimes()
        {
            if (playerItems != null && aiItems != null)
            {
                return true;
            }

            if (!itemSnapshotProvider.TryGetValidatedSnapshots(
                    out var playerSnapshot,
                    out var aiSnapshot,
                    out var reason) ||
                playerSnapshot == null || aiSnapshot == null)
            {
                Emit("TwentyWave ItemSnapshotsRejected Reason=" + (reason ?? "Unknown"));
                return false;
            }

            playerItems = new ItemRunRuntime(
                playerSnapshot,
                match.Player,
                player.Registry,
                runSeed: runSeed,
                opposingTeam: match.AI,
                opposingRouteEnemies: ai.Registry);
            aiItems = new ItemRunRuntime(
                aiSnapshot,
                match.AI,
                ai.Registry,
                runSeed: runSeed,
                opposingTeam: match.Player,
                opposingRouteEnemies: player.Registry);
            if (!playerItems.StartRun(out reason) || !aiItems.StartRun(out reason))
            {
                Emit("TwentyWave ItemRuntimeStartRejected Reason=" + (reason ?? "Unknown"));
                playerItems = null;
                aiItems = null;
                return false;
            }

            return true;
        }

        private void BeginWave(int wave)
        {
            currentWaveIndex = wave;
            waveElapsedTime = 0f;
            wavesExhausted = false;
            var definition = configuration.GetWave(wave);
            player.QueueWave(
                wave,
                GetWaveSpawnPlan(wave, TeamSide.Player),
                definition.SpawnIntervalSeconds,
                definition.FirstSpawnDelaySeconds);
            ai.QueueWave(
                wave,
                GetWaveSpawnPlan(wave, TeamSide.AI),
                definition.SpawnIntervalSeconds,
                definition.FirstSpawnDelaySeconds);
            if (wave == 6 && playerW6Boss == null && aiW6Boss == null)
            {
                var playerBoss = player.SpawnBoss(
                    wave,
                    SoulchainBinderConfiguration.BossId,
                    soulChainBossMaxHitPoints,
                    SoulchainBinderConfiguration.BossMoveSpeedCellsPerSecond);
                var aiBoss = ai.SpawnBoss(
                    wave,
                    SoulchainBinderConfiguration.BossId,
                    soulChainBossMaxHitPoints,
                    SoulchainBinderConfiguration.BossMoveSpeedCellsPerSecond);
                playerW6Boss = new SoulchainBinderRuntime(
                    playerBoss,
                    TeamSide.Player,
                    player.Destination,
                    runSeed,
                    playerSpellbreakerResolver,
                    soulChainEnabled);
                aiW6Boss = new SoulchainBinderRuntime(
                    aiBoss,
                    TeamSide.AI,
                    ai.Destination,
                    runSeed,
                    aiSpellbreakerResolver,
                    soulChainEnabled);
                playerW6Boss.SoulChain.CastEvent += value => SoulChainCastEmitted?.Invoke(TeamSide.Player, value);
                aiW6Boss.SoulChain.CastEvent += value => SoulChainCastEmitted?.Invoke(TeamSide.AI, value);
                Emit(
                    $"TwentyWave Wave={wave} Event=BossSpawned BossId={SoulchainBinderConfiguration.BossId} " +
                    $"BossHP={soulChainBossMaxHitPoints:0.00} " +
                    $"MoveSpeed={SoulchainBinderConfiguration.BossMoveSpeedCellsPerSecond:0.00} " +
                    $"SoulChainEnabled={soulChainEnabled}");
            }
            else if (wave == 12 && playerW12Boss == null && aiW12Boss == null)
            {
                var playerBoss = player.SpawnBoss(
                    wave,
                    StormcallerPriestConfiguration.BossId,
                    stormcallerBossMaxHitPoints,
                    StormcallerPriestConfiguration.BossMoveSpeedCellsPerSecond);
                var aiBoss = ai.SpawnBoss(
                    wave,
                    StormcallerPriestConfiguration.BossId,
                    stormcallerBossMaxHitPoints,
                    StormcallerPriestConfiguration.BossMoveSpeedCellsPerSecond);
                playerW12Boss = new StormcallerPriestRuntime(
                    playerBoss,
                    TeamSide.Player,
                    player.Registry,
                    playerSpellbreakerResolver);
                aiW12Boss = new StormcallerPriestRuntime(
                    aiBoss,
                    TeamSide.AI,
                    ai.Registry,
                    aiSpellbreakerResolver);
                playerW12Boss.CastEvent += value => StormcallerCastEmitted?.Invoke(TeamSide.Player, value);
                aiW12Boss.CastEvent += value => StormcallerCastEmitted?.Invoke(TeamSide.AI, value);
                Emit(
                    $"TwentyWave Wave={wave} Event=BossSpawned BossId={StormcallerPriestConfiguration.BossId} " +
                    $"BossHP={stormcallerBossMaxHitPoints:0.00} " +
                    $"MoveSpeed={StormcallerPriestConfiguration.BossMoveSpeedCellsPerSecond:0.00}");
            }
            else if (wave == 16 && playerW16Boss == null && aiW16Boss == null)
            {
                var bossDefinition = new BossDefinition(
                    FixedBossIds.W16BloodcrownTyrant,
                    new WaveNumber(16),
                    bloodcrownBossMaxHitPoints,
                    BloodcrownTyrantConfiguration.BossMoveSpeedCellsPerSecond,
                    BossGoalEffect.InstantDefeat,
                    BloodcrownTyrantConfiguration.HeroXpReward);
                var playerBoss = player.SpawnBoss(
                    wave,
                    bossDefinition.BossId.Value,
                    bossDefinition.MaxHitPoints,
                    bossDefinition.MoveSpeed);
                var aiBoss = ai.SpawnBoss(
                    wave,
                    bossDefinition.BossId.Value,
                    bossDefinition.MaxHitPoints,
                    bossDefinition.MoveSpeed);
                playerW16Policy = new BloodcrownIntegrationAdapter(playerBoss, TeamSide.Player, playerSpellbreakerResolver);
                aiW16Policy = new BloodcrownIntegrationAdapter(aiBoss, TeamSide.AI, aiSpellbreakerResolver);
                player.SetBloodcrownBasicPolicy(playerW16Policy);
                ai.SetBloodcrownBasicPolicy(aiW16Policy);
                playerW16Boss = new BloodcrownTyrantRuntime(
                    bossDefinition, playerW16Policy, playerW16Policy, playerW16Policy);
                aiW16Boss = new BloodcrownTyrantRuntime(
                    bossDefinition, aiW16Policy, aiW16Policy, aiW16Policy);
                playerW16Boss.LifecycleEmitted += value => BloodcrownLifecycleEmitted?.Invoke(TeamSide.Player, value);
                aiW16Boss.LifecycleEmitted += value => BloodcrownLifecycleEmitted?.Invoke(TeamSide.AI, value);
                playerW16Boss.CastResultEmitted += value => BloodcrownCastEmitted?.Invoke(TeamSide.Player, value);
                aiW16Boss.CastResultEmitted += value => BloodcrownCastEmitted?.Invoke(TeamSide.AI, value);
                Emit(
                    $"TwentyWave Wave={wave} Event=BossSpawned BossId={bossDefinition.BossId.Value} " +
                    $"BossHP={bossDefinition.MaxHitPoints:0.00} MoveSpeed={bossDefinition.MoveSpeed:0.00} GreyboxHP=true");
            }
            else if (wave == 20 && playerW20Boss == null && aiW20Boss == null)
            {
                var bossDefinition = new BossDefinition(
                    FixedBossIds.W20WorldeaterWyrm,
                    new WaveNumber(20),
                    worldeaterBossMaxHitPoints,
                    WorldeaterWyrmConfiguration.BossMoveSpeedCellsPerSecond,
                    BossGoalEffect.InstantDefeat,
                    WorldeaterWyrmConfiguration.HeroXpReward);
                var playerBoss = player.SpawnBoss(
                    wave,
                    bossDefinition.BossId.Value,
                    bossDefinition.MaxHitPoints,
                    bossDefinition.MoveSpeed);
                var aiBoss = ai.SpawnBoss(
                    wave,
                    bossDefinition.BossId.Value,
                    bossDefinition.MaxHitPoints,
                    bossDefinition.MoveSpeed);
                playerW20Policy = new WorldeaterIntegrationAdapter(
                    playerBoss, player, player.Destination, TeamSide.Player, playerSpellbreakerResolver);
                aiW20Policy = new WorldeaterIntegrationAdapter(
                    aiBoss, ai, ai.Destination, TeamSide.AI, aiSpellbreakerResolver);
                playerW20Boss = new WorldeaterWyrmRuntime(
                    bossDefinition, playerW20Policy, playerW20Policy, playerW20Policy, playerW20Policy);
                aiW20Boss = new WorldeaterWyrmRuntime(
                    bossDefinition, aiW20Policy, aiW20Policy, aiW20Policy, aiW20Policy);
                playerW20Boss.CastEvent += value => WorldeaterCastEmitted?.Invoke(TeamSide.Player, value);
                aiW20Boss.CastEvent += value => WorldeaterCastEmitted?.Invoke(TeamSide.AI, value);
                Emit(
                    $"TwentyWave Wave={wave} Event=BossSpawned BossId={bossDefinition.BossId.Value} " +
                    $"BossHP={bossDefinition.MaxHitPoints:0.00} MoveSpeed={bossDefinition.MoveSpeed:0.00} GreyboxHP=true");
            }
            match.SetCurrentWave(wave);
            Emit(
                $"TwentyWave WaveStarted Wave={wave} CountPerSide={definition.EnemyCountPerSide} " +
                $"DurationSeconds={definition.WaveDurationSeconds:0.00} BossSlot={definition.HasBossSlot}");
            if (definition.HasBossSlot)
            {
                Emit($"TwentyWave Wave={wave} Event=BossSlotReserved BossSpawned={wave == 6 || wave == 12 || wave == 16 || wave == 20}");
            }
        }


        private void EndCurrentWave()
        {
            player.RecordResidual(currentWaveIndex);
            ai.RecordResidual(currentWaveIndex);
            Emit(
                $"TwentyWave WaveFinished Wave={currentWaveIndex} " +
                $"ResidualPlayer={player.Remaining} ResidualAI={ai.Remaining}");
            var runeReward = playerRuneRewards?.CompleteWave(currentWaveIndex);
            if (runeReward != null)
            {
                Emit(
                    $"RuneReward Wave={runeReward.Wave} RuneId={runeReward.RuneId} " +
                    $"Rarity={runeReward.Rarity} Complete={runeReward.IsComplete} Fragment={runeReward.IsFragment}");
                PlayerRuneRewardGranted?.Invoke(runeReward);
            }

            if (currentWaveIndex >= TwentyWavePressureConfiguration.WaveCount)
            {
                wavesExhausted = true;
                Emit("TwentyWave Event=FinalWaveEnded");
                return;
            }

            BeginWave(currentWaveIndex + 1);
        }

        private void SettleRun()
        {
            var playerDefeated = match.Player.HatchlingHealth <= 0 || match.Player.IsInstantDefeated;
            var aiDefeated = match.AI.HatchlingHealth <= 0 || match.AI.IsInstantDefeated;
            if (playerDefeated && !aiDefeated)
            {
                match.TryTransition(MatchState.Defeat);
                Emit("TwentyWave Result=Defeat");
            }
            else if (aiDefeated && !playerDefeated)
            {
                match.TryTransition(MatchState.Victory);
                Emit("TwentyWave Result=Victory");
            }
            else
            {
                match.TryTransition(MatchState.Defeat);
                Emit("TwentyWave Result=Defeat");
            }

            IsComplete = true;
        }

        private static int DeriveCompositionSeed(int sourceRunSeed, int wave)
        {
            unchecked
            {
                var hash = 2166136261u;
                hash = Mix(hash, sourceRunSeed);
                foreach (var character in CompositionRngVersion)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }

                hash = Mix(hash, wave);
                return (int)hash;
            }
        }

        private static uint Mix(uint hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                return hash * 16777619u;
            }
        }

        private void RaiseCombatEvent(CombatEvent combatEvent)
        {
            CombatEmitted?.Invoke(combatEvent);
        }

        private void Emit(string message)
        {
            LastEvent = message;
            if (EmitLogs)
            {
                Debug.Log(message);
            }
        }
    }
}
