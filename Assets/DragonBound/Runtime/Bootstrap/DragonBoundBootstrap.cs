using DragonBound.AI;
using DragonBound.Analytics;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Items;
using DragonBound.Presentation;
using DragonBound.Recruitment;
using DragonBound.Runes;
using DragonBound.Services;
using GameShared.Random;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DragonBound.Bootstrap
{
    public sealed class ExternalRuneLoadoutAssignment
    {
        public string HeroId;
        public string RuneId;
    }

    public sealed class DragonBoundBootstrap : MonoBehaviour
    {
        [SerializeField] private int runSeed = 20260801;
        private int waveRandomSeed;
        [SerializeField] private bool deferInitializationUntilItemSnapshotReady;
        [SerializeField] private bool useFixedSeedForDiagnostics;
        [SerializeField] private string battlefieldLayoutId = BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01Id;
        [SerializeField] private bool enableHeroComponents = false;
        [SerializeField] private bool heroSliceMode = false;
        [SerializeField] private bool useTwentyWavePressureRuntime = false;
        [SerializeField] private bool enablePressureRunDiagnostics = false;
        [SerializeField] private bool enableAiSurvivalController = true;
        [SerializeField, Range(1, 10)] private int localPlayerRankLevel = 1;
        [SerializeField] private RecruitComponentPolicy recruitComponentPolicy = RecruitComponentPolicy.V3;
        [SerializeField, Min(20)] private int heroSliceStartingResources = 500;
        [SerializeField, Min(0.05f)] private float aiBoardActionIntervalSeconds = 0.2f;
        [SerializeField] private DragonBoundScreenView screenView;

        public MatchController Match { get; private set; }
        public RunSeed Seed { get; private set; }
        public string GameplayRunId { get; private set; }
        public string GameplayRulesVersion { get; private set; }
        public AnalyticsRunSessionV2 AnalyticsSession { get; private set; }
        public DrakeforgeAnalyticsAdapterV1 GameplayAnalyticsAdapter { get; private set; }
        public int ReconnectGraceSeconds { get; private set; } = 90;
        public int AfkTimeoutSeconds { get; private set; } = 180;
        public BoardGrid Board => PlayerBoard;
        public BoardGrid PlayerBoard { get; private set; }
        public BoardGrid AiBoard { get; private set; }
        public BattlefieldLayoutDefinition BattlefieldLayout { get; private set; }
        public FixedBoardLayoutDefinition FixedBoardLayout => BattlefieldLayout as FixedBoardLayoutDefinition;
        public bool EnableHeroComponents => enableHeroComponents;
        public bool HeroSliceMode => heroSliceMode;
        public bool UseTwentyWavePressureRuntime => useTwentyWavePressureRuntime;
        public bool EnablePressureRunDiagnostics => enablePressureRunDiagnostics;
        public RecruitComponentPolicy RecruitComponentPolicy => recruitComponentPolicy;
        public int HeroSliceStartingResources => Mathf.Max(MatchController.StartingResources, heroSliceStartingResources);
        public GreyboxBoardView BoardView => screenView != null ? screenView.BoardView : null;
        public GreyboxBoardView AiBoardView => screenView != null ? screenView.AiBoardView : null;
        public BoardRecruitDestination RecruitDestination { get; private set; }
        public RecruitmentService Recruitment { get; private set; }
        public BoardRecruitDestination AiRecruitDestination { get; private set; }
        public RecruitmentService AiRecruitment { get; private set; }
        public ShovelRecruitmentState PlayerShovelState { get; private set; }
        public ShovelRecruitmentState AiShovelState { get; private set; }
        public ShovelUnlockService PlayerShovelUnlocks { get; private set; }
        public ShovelUnlockService AiShovelUnlocks { get; private set; }
        public ItemForgePickPortRouter PlayerForgePickPort { get; } = new ItemForgePickPortRouter();
        public LimitedComponentBag ComponentBag { get; private set; }
        public LimitedComponentBag AiComponentBag { get; private set; }
        public ThreeWaveSliceRuntime ThreeWave { get; private set; }
        public WaveSystem WaveSystem { get; private set; }
        public TwentyWavePressureRuntime TwentyWave { get; private set; }
        public PressureRunDiagnostics PressureDiagnostics { get; private set; }
        public BasicUnitAiController AiController { get; private set; }
        public AiStrategyProfileId AiProfileId { get; private set; } = AiStrategyProfileId.Beginner;
        public AiStrategyProfile AiProfile { get; private set; }
        public AiDecisionScheduler AiDecisionScheduler { get; private set; }
        public bool IsAiRecoveryMatch { get; private set; }
        public string AiAlgorithmVersion { get; private set; }
        /// <summary>Meta-owned data. The current greybox creates an empty local profile;
        /// save/backend loading can replace it before the next Run starts.</summary>
        public RuneSaveData RuneSaveData { get; private set; }
        public IRuneProfileRepository RuneProfileRepository { get; private set; }
        public RuneFeatureGate RuneFeatureGate { get; private set; }
        public RuneLoadoutService PlayerRuneLoadout { get; private set; }
        public RuneRunRewardService PlayerRuneRewards { get; private set; }
        /// <summary>Run-local AI rewards used only by the pause result UI; never persisted to the player profile.</summary>
        public RuneRunRewardService AiRuneRewards { get; private set; }
        /// <summary>Optional V2 Rune lifecycle observer supplied by the integration owner.</summary>
        public RuneAnalyticsAdapterV2 RuneAnalyticsAdapter { get; set; }
        public GreyboxRunStatistics PlayerLayoutStatistics { get; private set; }
        public GreyboxRunStatistics AiLayoutStatistics { get; private set; }
        /// <summary>Injected by the matchmaking/bootstrap boundary before a Run. The greybox
        /// deliberately has no local inventory fallback that could fabricate a server loadout.</summary>
        public IItemRunSnapshotProvider ItemRunSnapshotProvider { get; set; }
        /// <summary>External account boundary for a validated player profile and AI snapshot.</summary>
        public IItemValidatedProfileSnapshotSource ItemProfileSnapshotSource { get; set; }
        /// <summary>Account-owned, immutable Rune assignments supplied before initialization.
        /// When present, this snapshot replaces the legacy Greybox-local Rune profile for the Run.</summary>
        private RuneLoadoutSnapshot externalPlayerRuneLoadoutSnapshot;
        private int? externalPlayerRuneAccountDay;
        public bool IsInitialized { get; private set; }
        public bool InitializationFailed { get; private set; }
        public System.Exception InitializationException { get; private set; }
        public event System.Action Initialized;
        public event System.Action<System.Exception> InitializationFailedEvent;
        private bool initializationInProgress;

        public const float InitializationPromptSeconds = 1f;
        private float initializationRemaining;
        private PressureRunDiagnosticsPanel pressureDiagnosticsPanel;
        private int lastAiDiagnosticsWave;
        private int playerRecruitSeed;
        private int aiRecruitSeed;
        private int combatSeed;
        private int aiDecisionSeed;
        private int debugSpawnSequence;
        private float aiBoardActionRemaining;

        // Test-only injection keeps persistence tests away from a developer's real local profile.
        public static IRuneProfileRepository RuneProfileRepositoryOverrideForTests { get; set; }
        public static IItemRunSnapshotProvider ItemRunSnapshotProviderOverrideForTests { get; set; }
        public static IItemValidatedProfileSnapshotSource ItemProfileSnapshotSourceOverrideForTests { get; set; }
        public static RuneAnalyticsAdapterV2 RuneAnalyticsAdapterOverrideForTests { get; set; }
        public static IAnalyticsSinkV2 AnalyticsSinkOverrideForTests { get; set; }

        public void Configure(DragonBoundScreenView view)
        {
            screenView = view;
        }

        public bool TryDebugUnlockPlayerCell(int x, int y)
        {
            return PlayerBoard != null && PlayerBoard.TryDebugUnlockCell(x, y);
        }

        public bool TryDebugUnlockAiCell(int x, int y)
        {
            return AiBoard != null && AiBoard.TryDebugUnlockCell(x, y);
        }

        // Future rewarded-ad integration enters through this same inventory and unlock path.
        public void GrantShovel(int count)
        {
            PlayerShovelUnlocks?.GrantShovel(count);
        }

        public void GrantShovel(TeamSide side, int count)
        {
            var service = side == TeamSide.Player ? PlayerShovelUnlocks : AiShovelUnlocks;
            service?.GrantShovel(count);
        }

        public void BindPlayerForgePickPort(IItemForgePickPort provider)
        {
            PlayerForgePickPort.Bind(provider);
        }

        public void UnbindPlayerForgePickPort(IItemForgePickPort provider)
        {
            PlayerForgePickPort.Unbind(provider);
        }

        public bool TryDebugSpawnDragonRouteHero(TeamSide side, string heroId)
        {
            var destination = side == TeamSide.Player ? RecruitDestination : AiRecruitDestination;
            var prefix = $"dev.{side.ToString().ToLowerInvariant()}.{heroId}.{runSeed}";
            if (!DragonRouteHeroDevelopmentFactory.TrySpawnPair(destination, heroId, prefix, out var pairLink))
            {
                Debug.LogWarning($"DebugDragonRouteSpawnRejected Team={side} HeroId={heroId}");
                return false;
            }

            Debug.Log(
                $"DebugDragonRouteSpawned Team={side} HeroId={pairLink.HeroId} " +
                $"RecipeId={pairLink.RecipeId} PairLinkId={pairLink.PairLinkId}");
            return true;
        }

        public bool TryDebugSpawnDragonRouteHeroDirect(TeamSide side, string heroId)
        {
            var destination = side == TeamSide.Player ? RecruitDestination : AiRecruitDestination;
            var prefix =
                $"dev.console.{side.ToString().ToLowerInvariant()}.{heroId}.{runSeed}.{++debugSpawnSequence}";
            if (!DragonRouteHeroDevelopmentFactory.TrySpawnPairDirect(
                    destination,
                    heroId,
                    prefix,
                    out var pairLink))
            {
                Debug.LogWarning($"DebugDirectHeroSpawnRejected Team={side} HeroId={heroId}");
                return false;
            }

            Debug.Log(
                $"DebugDirectHeroSpawned Team={side} HeroId={pairLink.HeroId} " +
                $"RecipeId={pairLink.RecipeId} PairLinkId={pairLink.PairLinkId}");
            return true;
        }

        public bool TryDebugSpawnBasicUnit(TeamSide side, string configId)
        {
            if ((!Application.isEditor && !Debug.isDebugBuild) || string.IsNullOrWhiteSpace(configId))
            {
                return false;
            }

            var destination = side == TeamSide.Player ? RecruitDestination : AiRecruitDestination;
            if (destination == null)
            {
                return false;
            }

            var runtimeId =
                $"dev.{side.ToString().ToLowerInvariant()}.basic.{++debugSpawnSequence}.{configId}";
            var card = new RecruitCard(
                runtimeId,
                RecruitItemKind.BasicUnit,
                configId,
                string.Empty);
            foreach (var position in destination.Board.GetPositions(CellType.Battle))
            {
                if (destination.TryDebugPlaceCard(card, position))
                {
                    Debug.Log(
                        $"DebugBasicUnitSpawned Team={side} ConfigId={configId} " +
                        $"RuntimeId={runtimeId} Position={position}");
                    return true;
                }
            }

            Debug.LogWarning($"DebugBasicUnitSpawnRejected Team={side} ConfigId={configId}");
            return false;
        }

        public bool TryDebugSetCurrentWave(int wave)
        {
            if (TwentyWave == null || !TwentyWave.JumpToWave(wave))
            {
                return false;
            }

            return !TwentyWave.IsBossWarningPending || TwentyWave.ConfirmBossWarning();
        }

        public bool TryDebugSpawnEnemy(TeamSide defendingSide, int appearanceIndex)
        {
            return TwentyWave != null && TwentyWave.TryDebugSpawnEnemy(defendingSide, appearanceIndex);
        }

        public bool TryDebugSpawnBoss(int bossWave)
        {
            return TwentyWave != null && TwentyWave.TryDebugSpawnBoss(bossWave);
        }

        [ContextMenu("DEV/Spawn Player Windclaw Ranger")]
        private void SpawnPlayerWindclawRanger()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.Player, DragonBoundHeroIds.WindclawRanger);
        }

        [ContextMenu("DEV/Spawn Player Ember Shaman")]
        private void SpawnPlayerEmberShaman()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.Player, DragonBoundHeroIds.EmberShaman);
        }

        [ContextMenu("DEV/Spawn Player Flame Drake Rider")]
        private void SpawnPlayerDragonRider()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.Player, DragonBoundHeroIds.DragonRider);
        }

        [ContextMenu("DEV/Spawn Player Runebolt Mage")]
        private void SpawnPlayerRuneboltMage()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.Player, DragonBoundHeroIds.RuneboltMage);
        }

        [ContextMenu("DEV/Spawn Player Stonebound Warlock")]
        private void SpawnPlayerStonebinder()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.Player, DragonBoundHeroIds.Stonebinder);
        }

        [ContextMenu("DEV/Spawn Player Starfall Archmage")]
        private void SpawnPlayerStarfallArchmage()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.Player, DragonBoundHeroIds.StarfallArchmage);
        }

        [ContextMenu("DEV/Spawn Player Oathcrown Blademaster")]
        private void SpawnPlayerCrownSwordLeader()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.Player, DragonBoundHeroIds.CrownSwordLeader);
        }

        [ContextMenu("DEV/Spawn Player Frostcrown Hunter")]
        private void SpawnPlayerCrownHunterLeader()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.Player, DragonBoundHeroIds.CrownHunterLeader);
        }

        [ContextMenu("DEV/Spawn Player Thunderlord")]
        private void SpawnPlayerThunderJarl()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.Player, DragonBoundHeroIds.ThunderJarl);
        }

        [ContextMenu("DEV/Spawn Player Nightfang Assassin")]
        private void SpawnPlayerNightfangAssassin()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.Player, DragonBoundHeroIds.NightfangAssassin);
        }

        [ContextMenu("DEV/Spawn Player Abyssal Harpooner")]
        private void SpawnPlayerLeviathanHunter()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.Player, DragonBoundHeroIds.LeviathanHunter);
        }

        [ContextMenu("DEV/Spawn Player Skyborne Valkyrie")]
        private void SpawnPlayerSkyhunterValkyrie()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.Player, DragonBoundHeroIds.SkyhunterValkyrie);
        }

        [ContextMenu("DEV/Spawn AI Windclaw Ranger")]
        private void SpawnAiWindclawRanger()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.AI, DragonBoundHeroIds.WindclawRanger);
        }

        [ContextMenu("DEV/Spawn AI Ember Shaman")]
        private void SpawnAiEmberShaman()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.AI, DragonBoundHeroIds.EmberShaman);
        }

        [ContextMenu("DEV/Spawn AI Flame Drake Rider")]
        private void SpawnAiDragonRider()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.AI, DragonBoundHeroIds.DragonRider);
        }

        [ContextMenu("DEV/Spawn AI Runebolt Mage")]
        private void SpawnAiRuneboltMage()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.AI, DragonBoundHeroIds.RuneboltMage);
        }

        [ContextMenu("DEV/Spawn AI Stonebound Warlock")]
        private void SpawnAiStonebinder()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.AI, DragonBoundHeroIds.Stonebinder);
        }

        [ContextMenu("DEV/Spawn AI Starfall Archmage")]
        private void SpawnAiStarfallArchmage()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.AI, DragonBoundHeroIds.StarfallArchmage);
        }

        [ContextMenu("DEV/Spawn AI Oathcrown Blademaster")]
        private void SpawnAiCrownSwordLeader()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.AI, DragonBoundHeroIds.CrownSwordLeader);
        }

        [ContextMenu("DEV/Spawn AI Frostcrown Hunter")]
        private void SpawnAiCrownHunterLeader()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.AI, DragonBoundHeroIds.CrownHunterLeader);
        }

        [ContextMenu("DEV/Spawn AI Thunderlord")]
        private void SpawnAiThunderJarl()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.AI, DragonBoundHeroIds.ThunderJarl);
        }

        [ContextMenu("DEV/Spawn AI Nightfang Assassin")]
        private void SpawnAiNightfangAssassin()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.AI, DragonBoundHeroIds.NightfangAssassin);
        }

        [ContextMenu("DEV/Spawn AI Abyssal Harpooner")]
        private void SpawnAiLeviathanHunter()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.AI, DragonBoundHeroIds.LeviathanHunter);
        }

        [ContextMenu("DEV/Spawn AI Skyborne Valkyrie")]
        private void SpawnAiSkyhunterValkyrie()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.AI, DragonBoundHeroIds.SkyhunterValkyrie);
        }

        public bool TryDebugUnlockNextFairCells()
        {
            if (PlayerBoard == null || AiBoard == null)
            {
                return false;
            }

            var playerLocked = PlayerBoard.GetPositions(CellType.Locked);
            foreach (var position in playerLocked)
            {
                var aiPosition = BattlefieldLayout.GetFairCounterpart(position, TeamSide.Player);
                if (AiBoard.TryGetCellType(aiPosition, out var aiCellType) &&
                    aiCellType == CellType.Locked &&
                    PlayerBoard.TryDebugUnlockCell(position) &&
                    AiBoard.TryDebugUnlockCell(aiPosition))
                {
                    Debug.Log(
                        $"DebugUnlockCell LayoutId={BattlefieldLayout.LayoutId} " +
                        $"PlayerPosition={position} AiPosition={aiPosition} BothSides=true");
                    return true;
                }
            }

            return false;
        }

        [ContextMenu("DEV/Log 20-Wave Pressure Diagnostics")]
        private void LogTwentyWavePressureDiagnostics()
        {
            Debug.Log(TwentyWavePressureDiagnostics.CreateReport(
                TwentyWavePressureConfiguration.CreateGreyboxV1()));
        }

        [ContextMenu("DEV/Stop 20-Wave Pressure And Report")]
        private void StopTwentyWavePressureAndReport()
        {
            if (TwentyWave == null || PressureDiagnostics == null)
            {
                Debug.LogWarning("Pressure diagnostics require Use Twenty Wave Pressure Runtime and Enable Pressure Run Diagnostics.");
                return;
            }

            TwentyWave.StopRun();
            Debug.Log(PressureDiagnostics.StopAndReport());
        }

        [ContextMenu("DEV/Validate All 12 Hero Recipes")]
        private void ValidateAllHeroRecipes()
        {
            if (!HeroRecipeValidation.IsAvailable)
            {
                Debug.LogWarning("Hero recipe validation is development-only.");
                return;
            }

            var results = HeroRecipeValidation.ValidateAll();
            var passed = 0;
            foreach (var result in results)
            {
                if (result.Passed)
                {
                    passed++;
                }

                Debug.Log(
                    $"RecipeValidation HeroId={result.HeroId} ComponentA={result.ComponentA} " +
                    $"ComponentB={result.ComponentB} Direction={result.Direction} " +
                    $"RecipeId={result.RecipeId} Executor={result.Executor} " +
                    $"Registered={result.Registered} PairLink={result.PairLinkTest} " +
                    $"WrongDirectionRejected={result.WrongDirectionRejected} " +
                    $"MissingComponentRejected={result.MissingComponentRejected} " +
                    $"BreakReform={result.PairBreaksAndReforms}");
            }

            var coverage = HeroRecipeValidation.AuditNormalRunSeeds(runSeed, 100);
            Debug.Log(
                $"RecipeValidationSummary Passed={passed}/{results.Count} " +
                $"NormalRunSeeds={coverage.SampleCount} FullyCovered={coverage.FullyCoveredCount} " +
                $"Incomplete={coverage.IncompleteCount}");
        }

        [ContextMenu("DEV/Run 100 AI Survival Seeds")]
        private void RunAiSurvivalSample()
        {
            Debug.Log(AiSurvivalSimulation.Run(runSeed, 100).CreateReport());
        }

        private void Awake()
        {
            if (!deferInitializationUntilItemSnapshotReady)
            {
                InitializeRuntime();
            }
        }

        public void InitializeWithItemSnapshotProvider(IItemRunSnapshotProvider provider)
        {
            if (provider == null)
            {
                throw new System.ArgumentNullException(nameof(provider));
            }

            if (IsInitialized) return;
            ItemRunSnapshotProvider = provider;
            InitializeRuntime();
        }

        public bool TrySetPlayerRuneLoadoutSnapshot(
            IEnumerable<ExternalRuneLoadoutAssignment> source,
            out string error)
        {
            if (IsInitialized)
            {
                error = "RuntimeAlreadyInitialized";
                return false;
            }

            var assignments = new List<RuneLoadoutAssignment>();
            if (source != null)
            {
                foreach (var value in source)
                {
                    assignments.Add(value == null
                        ? null
                        : new RuneLoadoutAssignment { HeroId = value.HeroId, RuneId = value.RuneId });
                }
            }

            return RuneLoadoutSnapshot.TryCreate(
                assignments,
                out externalPlayerRuneLoadoutSnapshot,
                out error);
        }

        public bool TrySetPlayerRuneAccountDay(int accountDay, out string error)
        {
            if (IsInitialized)
            {
                error = "RuntimeAlreadyInitialized";
                return false;
            }

            if (accountDay < 1)
            {
                error = "InvalidAccountDay";
                return false;
            }

            externalPlayerRuneAccountDay = accountDay;
            error = string.Empty;
            return true;
        }

        private async void InitializeRuntime()
        {
            if (IsInitialized || initializationInProgress) return;
            initializationInProgress = true;
            try
            {
                InitializationFailed = false;
                InitializationException = null;
                Time.timeScale = 1f;
                Debug.Log("TimeScaleInitialized Time.timeScale=1");
                await BeginGameplayRunAsync();
            InitializeAnalyticsSession();
            if (heroSliceMode && !enableHeroComponents)
            {
                Debug.LogError("HeroSliceMode requires EnableHeroComponents=true.");
                heroSliceMode = false;
            }

            Seed = new RunSeed(runSeed);
            Match = new MatchController(runSeed);
            ItemProfileSnapshotSource = ItemProfileSnapshotSourceOverrideForTests ?? ItemProfileSnapshotSource;
            RuneAnalyticsAdapter = RuneAnalyticsAdapterOverrideForTests ?? RuneAnalyticsAdapter;
            ItemRunSnapshotProvider = ItemRunSnapshotProviderOverrideForTests ?? ItemRunSnapshotProvider;
            if (ItemRunSnapshotProvider == null && ItemProfileSnapshotSource != null)
            {
                ItemRunSnapshotProvider = new ItemProfileRunSnapshotProvider(ItemProfileSnapshotSource);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!Application.isBatchMode && useTwentyWavePressureRuntime && ItemRunSnapshotProvider == null)
            {
                ItemRunSnapshotProvider = new DevelopmentItemRunSnapshotProvider();
            }
#endif
            RuneProfileRepository = RuneProfileRepositoryOverrideForTests ?? new LocalRuneProfileRepository();
            var runeProfileResult = RuneProfileRepository.Load();
            RuneSaveData = runeProfileResult.Data ?? new RuneSaveData();
            if (externalPlayerRuneAccountDay.HasValue)
            {
                RuneSaveData.AccountDay = Mathf.Max(1, externalPlayerRuneAccountDay.Value);
            }
            RuneFeatureGate = new RuneFeatureGate(new RuneProfileProgressionProvider(RuneSaveData));
            PlayerRuneLoadout = new RuneLoadoutService(
                RuneSaveData,
                RuneFeatureGate,
                PersistRuneProfile,
                RuneAnalyticsAdapter);
            PlayerRuneRewards = new RuneRunRewardService(
                runSeed,
                RuneSaveData.Inventory,
                RuneFeatureGate,
                () => PersistRuneProfile(),
                RuneAnalyticsAdapter);
            AiRuneRewards = new RuneRunRewardService(
                unchecked(runSeed ^ (int)0x6D2B79F5),
                new RuneInventory(),
                RuneFeatureGate,
                null);
            Debug.Log(
                $"RuneProfileLoaded Status={runeProfileResult.Status} Day={RuneFeatureGate.AccountDay} " +
                $"Unlocked={RuneFeatureGate.IsUnlocked}");
            // The large starting pool belongs only to the focused HeroSlice showcase.
            // The formal 20-wave Greybox run retains the normal match economy.
            if (heroSliceMode)
            {
                var supplement = HeroSliceStartingResources - MatchController.StartingResources;
                Match.Player.AddResources(supplement);
                Match.AI.AddResources(supplement);
                Debug.Log(
                    $"HeroComponentTestEconomy StartingResources={HeroSliceStartingResources} " +
                    $"PlayerResources={Match.Player.Resources} " +
                    $"AIResources={Match.AI.Resources}");
            }
            Match.SetCurrentWave(0);
            BattlefieldLayout = ResolveBattlefieldLayout();
            PlayerBoard = DragonBoundBoardLayout.Create(BattlefieldLayout, TeamSide.Player);
            AiBoard = DragonBoundBoardLayout.Create(BattlefieldLayout, TeamSide.AI);
            Match.Player.SetRemainingEnemyCount(0);
            Match.AI.SetRemainingEnemyCount(0);

            var catalog = GreyboxRecruitmentCatalog.Create();
            ComponentBag = LimitedComponentBag.CreateBag(
                runSeed,
                LimitedComponentBag.DefaultContentVersion,
                catalog);
            AiComponentBag = LimitedComponentBag.CreateBag(
                aiRecruitSeed,
                LimitedComponentBag.DefaultContentVersion,
                catalog);
            PlayerShovelState = new ShovelRecruitmentState(
                () => PlayerBoard.GetPositions(CellType.Locked).Count);
            AiShovelState = new ShovelRecruitmentState(
                () => AiBoard.GetPositions(CellType.Locked).Count);
            var deck = CreateRecruitDeck(
                catalog,
                "player",
                ComponentBag,
                playerRecruitSeed,
                PlayerShovelState);
            var aiDeck = CreateRecruitDeck(
                catalog,
                "ai",
                AiComponentBag,
                aiRecruitSeed,
                AiShovelState);
            RecruitDestination = new BoardRecruitDestination(
                PlayerBoard,
                runeRunSeed: combatSeed);
            Recruitment = new RecruitmentService(
                Match.Player,
                deck,
                RecruitDestination,
                protectHeroComponentsOnRefresh: false);
            AiRecruitDestination = new BoardRecruitDestination(AiBoard, runeRunSeed: aiRecruitSeed);
            AiRecruitment = new RecruitmentService(Match.AI, aiDeck, AiRecruitDestination);
            PlayerShovelUnlocks = new ShovelUnlockService(PlayerBoard, RecruitDestination);
            AiShovelUnlocks = new ShovelUnlockService(AiBoard, AiRecruitDestination);
            AiController = new BasicUnitAiController(
                AiBoard,
                AiRecruitDestination,
                AiRecruitment,
                AiShovelUnlocks,
                Match.AI);
            AiProfile = AiStrategyProfile.Get(AiProfileId);
            AiDecisionScheduler = new AiDecisionScheduler(AiProfile, aiDecisionSeed);
            AiController.ConfigureStrategy(AiProfile, aiDecisionSeed);
            PlayerLayoutStatistics = new GreyboxRunStatistics(
                BattlefieldLayout.LayoutId,
                PlayerBoard,
                RecruitDestination,
                Recruitment);
            AiLayoutStatistics = new GreyboxRunStatistics(
                BattlefieldLayout.LayoutId,
                AiBoard,
                AiRecruitDestination,
                AiRecruitment);
            Match.StateChanged += HandleMatchStateChanged;

            if (screenView == null)
            {
                screenView = FindObjectOfType<DragonBoundScreenView>();
            }

            if (screenView == null)
            {
                Debug.LogError("DragonBoundScreenView must be assigned in Greybox_Main.", this);
                return;
            }

            screenView.Initialize(
                Match,
                PlayerBoard,
                AiBoard,
                Recruitment,
                AiRecruitment,
                RecruitDestination,
                AiRecruitDestination);
            screenView.OverlayController.BindRuneRewardServices(PlayerRuneRewards, AiRuneRewards);
            BoardView.BindShovelUnlockService(PlayerShovelUnlocks);
            AiBoardView.BindShovelUnlockService(AiShovelUnlocks);

            if (useTwentyWavePressureRuntime)
            {
                TwentyWave = new TwentyWavePressureRuntime(
                    Match,
                    RecruitDestination,
                    AiRecruitDestination,
                    waveRandomSeed,
                    playerRuneRewards: PlayerRuneRewards,
                    itemSnapshotProvider: ItemRunSnapshotProvider,
                    playerForgePick: PlayerForgePickPort,
                    playerFreeRecruit: Recruitment,
                    aiFreeRecruit: AiRecruitment,
                    aiRuneRewards: AiRuneRewards);
                screenView.BindWaveRuntime(TwentyWave);
                screenView.BindItemRuntime(TwentyWave);
            }
            else
            {
                WaveSystem = new WaveSystem(
                    Match,
                    RecruitDestination,
                    AiRecruitDestination,
                    heroSliceMode
                        ? ThreeWaveEnemyDurabilityProfile.HeroSkillShowcase
                        : ThreeWaveEnemyDurabilityProfile.BasicUnitBaseline);
                ThreeWave = WaveSystem.Runtime;
                screenView.BindWaveRuntime(ThreeWave);
            }

            GameplayAnalyticsAdapter = new DrakeforgeAnalyticsAdapterV1(AnalyticsSession);
            if (TwentyWave != null)
            {
                GameplayAnalyticsAdapter.Attach(
                    TwentyWave,
                    RecruitDestination,
                    AiRecruitDestination,
                    Recruitment,
                    AiRecruitment);
            }
            else
            {
                GameplayAnalyticsAdapter.Attach(
                    ThreeWave,
                    RecruitDestination,
                    AiRecruitDestination,
                    Recruitment,
                    AiRecruitment);
            }

            if (enablePressureRunDiagnostics && TwentyWave != null)
            {
                PressureDiagnostics = new PressureRunDiagnostics(
                    Match,
                    TwentyWave,
                    Recruitment,
                    AiRecruitment,
                    RecruitDestination,
                    AiRecruitDestination,
                    PlayerShovelUnlocks,
                    AiShovelUnlocks);
                PressureDiagnostics.AttachDragControllers(BoardView?.Drag, AiBoardView?.Drag);
                if (Application.isEditor || Debug.isDebugBuild)
                {
                    pressureDiagnosticsPanel = PressureRunDiagnosticsPanel.Create(PressureDiagnostics);
                }
            }

            // Twenty-wave AI must not mutate the board during initialization/Ready. Its first
            // decision is released by the scheduler only after MatchState.Running.
            if (!useTwentyWavePressureRuntime)
            {
                var aiRecruitments = heroSliceMode ? 3 : 1;
                if (!AiController.BeginOpeningSequence(aiRecruitments, heroSliceMode ? 2 : 3))
                {
                    Debug.LogError("AI opening deployment sequence could not be started.");
                }
                aiBoardActionRemaining = 0f;
            }

            AiBoardView.RefreshUnits();
            Match.TryTransition(MatchState.Ready);
            initializationRemaining = InitializationPromptSeconds;
            IsInitialized = true;
            GreyboxGameplayAnimationTestConsole.Create(this);
            Initialized?.Invoke();
            Debug.Log("MatchStateChanged State=Ready InitializationComplete=true");
            }
            catch (System.Exception exception)
            {
                IsInitialized = false;
                InitializationFailed = true;
                InitializationException = exception;
                Debug.LogException(exception, this);
                InitializationFailedEvent?.Invoke(exception);
            }
            finally
            {
                initializationInProgress = false;
            }
        }

        private RecruitDeck CreateRecruitDeck(
            RecruitmentCatalog catalog,
            string runtimePrefix,
            LimitedComponentBag componentBag,
            int recruitmentSeed,
            ShovelRecruitmentState shovelState)
        {
            if (enableHeroComponents && !heroSliceMode)
            {
                return new RecruitDeck(
                    catalog,
                    recruitmentSeed,
                    runtimePrefix,
                    componentBag,
                    shovelState: shovelState,
                    componentPolicy: recruitComponentPolicy,
                    currentWaveProvider: () => Match != null ? Match.CurrentWave : 1);
            }

            return new RecruitDeck(
                catalog,
                new RunRandom(recruitmentSeed),
                runtimePrefix,
                enableHeroComponents,
                heroSliceMode);
        }

        private async Task BeginGameplayRunAsync()
        {
            bool hasLaunchContext = GameplayLaunchContext.TryGet(
                out string launchPlayerId,
                out string launchNonce,
                out int launchPlayerRankLevel);
            StartGameplayRunResult result;
            if (GameplayLaunchContext.IsDirectDevelopmentSceneEntry)
            {
                var staleLaunchContextCleared = hasLaunchContext;
                if (hasLaunchContext)
                {
                    GameplayLaunchContext.Complete(launchNonce);
                }

                launchPlayerId = string.Empty;
                launchNonce = System.Guid.NewGuid().ToString("N");
                launchPlayerRankLevel = Mathf.Clamp(localPlayerRankLevel, 1, 10);
                var request = new StartGameplayRunRequest
                {
                    PlayerId = launchPlayerId,
                    GameMode = useTwentyWavePressureRuntime ? "TwentyWave" : "Greybox",
                    ClientRunNonce = launchNonce,
                    UseDiagnosticSeed = true,
                    DiagnosticSeed = runSeed,
                    PlayerRankLevel = launchPlayerRankLevel
                };
                result = await new LocalGameplayRunGateway()
                    .StartRunAsync(request, CancellationToken.None);
                hasLaunchContext = false;
                Debug.Log(
                    $"GreyboxDirectDevelopmentLaunch Seed={runSeed} " +
                    $"StaleLaunchContextCleared={staleLaunchContextCleared}");
            }
            else if (hasLaunchContext)
            {
                if (!GameplayLaunchContext.TryTakePrepared(launchNonce, out result))
                {
                    throw new System.InvalidOperationException(
                        "Gameplay launch ticket is missing. Return to Main and start a new run.");
                }
            }
            else
            {
                // Direct scene entry remains available for local diagnostics and existing PlayMode tests.
                launchPlayerId = string.Empty;
                launchNonce = System.Guid.NewGuid().ToString("N");
                launchPlayerRankLevel = Mathf.Clamp(localPlayerRankLevel, 1, 10);
                var request = new StartGameplayRunRequest
                {
                    PlayerId = launchPlayerId,
                    GameMode = useTwentyWavePressureRuntime ? "TwentyWave" : "Greybox",
                    ClientRunNonce = launchNonce,
                    UseDiagnosticSeed = useFixedSeedForDiagnostics,
                    DiagnosticSeed = runSeed,
                    PlayerRankLevel = launchPlayerRankLevel
                };
                result = await GameplayRunGatewayRegistry.Current
                    .StartRunAsync(request, CancellationToken.None);
            }
            if (result == null)
            {
                throw new System.InvalidOperationException("Gameplay gateway returned no run configuration.");
            }

            GameplayRunId = result.RunId;
            GameplayRulesVersion = string.IsNullOrWhiteSpace(result.RulesVersion)
                ? LocalGameplayRunGateway.LocalRulesVersion
                : result.RulesVersion;
            runSeed = result.RunSeed;
            waveRandomSeed = string.IsNullOrWhiteSpace(result.RandomProtocolVersion)
                ? result.RunSeed
                : result.WaveRandomSeed;
            playerRecruitSeed = result.PlayerRecruitSeed;
            aiRecruitSeed = result.AiRecruitSeed;
            combatSeed = result.CombatSeed;
            int resolvedRankLevel = result.PlayerRankLevel > 0
                ? Mathf.Clamp(result.PlayerRankLevel, 1, 10)
                : Mathf.Clamp(localPlayerRankLevel, 1, 10);
            localPlayerRankLevel = resolvedRankLevel;
            AiProfileId = AiRankProfileMapping.TryParseWireValue(result.AiProfile, out var profile)
                ? profile
                : AiRankProfileMapping.FromRankLevel(resolvedRankLevel);
            aiDecisionSeed = result.AiDecisionSeed != 0
                ? result.AiDecisionSeed
                : LocalGameplayRunGateway.DeriveSeed(runSeed, "ai.decision");
            IsAiRecoveryMatch = result.IsRecoveryMatch;
            AiAlgorithmVersion = string.IsNullOrWhiteSpace(result.AiAlgorithmVersion)
                ? LocalGameplayRunGateway.LocalAiAlgorithmVersion
                : result.AiAlgorithmVersion;
            ReconnectGraceSeconds = result.ReconnectGraceSeconds > 0
                ? result.ReconnectGraceSeconds
                : 90;
            AfkTimeoutSeconds = result.AfkTimeoutSeconds > 0
                ? result.AfkTimeoutSeconds
                : 180;
            if (hasLaunchContext) GameplayLaunchContext.Complete(launchNonce);
            Debug.Log(
                $"GameplayRunStarted RunId={GameplayRunId} Rules={result.RulesVersion} " +
                $"DiagnosticSeed={useFixedSeedForDiagnostics} Rank={resolvedRankLevel} " +
                $"AIProfile={AiProfileId} Recovery={IsAiRecoveryMatch} " +
                $"AIAlgorithm={AiAlgorithmVersion}");
        }

        private void InitializeAnalyticsSession()
        {
            IAnalyticsSinkV2 sink = AnalyticsSinkOverrideForTests ??
                new BufferedAnalyticsSinkV2(
                    new FirebaseAnalyticsSinkV2(),
                    canBuffer: () => FirebaseAnalyticsRuntimeState.CanCollect);
            var context = new DrakeforgeAnalyticsRunContext(
                GameplayRunId,
                runSeed,
                ResolveAnalyticsExecutionContext(),
                GameplayRulesVersion,
                string.IsNullOrWhiteSpace(Application.version) ? "development" : Application.version,
                ResolveAnalyticsRankTier(localPlayerRankLevel),
                ResolveAnalyticsAiDifficulty(AiProfileId),
                AiProfileId.ToString().ToLowerInvariant(),
                AiAlgorithmVersion,
                aiDecisionSeed,
                IsAiRecoveryMatch,
                localPlayerRankLevel,
                AnalyticsBuildLaneResolverV2.Resolve());
            AnalyticsSession = new AnalyticsRunSessionV2(new AnalyticsRecorderV2(sink), context);
            AnalyticsRuntimeRegistryV2.SetCurrent(AnalyticsSession);
            if (RuneAnalyticsAdapterOverrideForTests == null && RuneAnalyticsAdapter == null)
            {
                RuneAnalyticsAdapter = new RuneAnalyticsAdapterV2(AnalyticsSession, AnalyticsSides.Player);
            }
        }

        private string ResolveAnalyticsExecutionContext()
        {
            if (useFixedSeedForDiagnostics) return AnalyticsExecutionContexts.DiagnosticAiVsAi;
            if (heroSliceMode) return AnalyticsExecutionContexts.HeroSliceShowcase;
            return AnalyticsExecutionContexts.LivePlayerVsAi;
        }

        private static string ResolveAnalyticsRankTier(int rankLevel)
        {
            if (rankLevel <= 0) return AnalyticsRankTiers.Unranked;
            if (rankLevel <= 2) return AnalyticsRankTiers.Bronze;
            if (rankLevel <= 4) return AnalyticsRankTiers.Silver;
            if (rankLevel <= 6) return AnalyticsRankTiers.Gold;
            if (rankLevel <= 8) return AnalyticsRankTiers.Platinum;
            return AnalyticsRankTiers.Diamond;
        }

        private static string ResolveAnalyticsAiDifficulty(AiStrategyProfileId profile)
        {
            switch (profile)
            {
                case AiStrategyProfileId.Beginner: return AnalyticsAiDifficulties.Easy;
                case AiStrategyProfileId.Veteran: return AnalyticsAiDifficulties.Standard;
                case AiStrategyProfileId.Elite: return AnalyticsAiDifficulties.Hard;
                case AiStrategyProfileId.Master: return AnalyticsAiDifficulties.Elite;
                default: return AnalyticsAiDifficulties.None;
            }
        }

        private void Update()
        {
            if (Match == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.U))
            {
                TryDebugUnlockNextFairCells();
            }

            if (Match.State == MatchState.Ready)
            {
                RecruitDestination?.TickPairLinks(Time.deltaTime);
                AiRecruitDestination?.TickPairLinks(Time.deltaTime);
                if (ProcessAiStepwiseSequence(Time.deltaTime))
                {
                    return;
                }

                initializationRemaining -= Time.deltaTime;
                if (initializationRemaining <= 0f)
                {
                    LockRuneLoadoutAtRunStart();
                    if (TwentyWave != null && !TwentyWave.StartRun())
                    {
                        Debug.LogError("TwentyWaveStartRejected: validated Item snapshots are required.");
                        return;
                    }

                    if (TwentyWave == null)
                    {
                        Match.TryTransition(MatchState.Running);
                    }
                    Debug.Log("MatchStateChanged State=Running InitializationPromptHidden=true");
                }

                return;
            }

            if (TwentyWave != null)
            {
                if (!TwentyWave.IsStarted)
                {
                    TwentyWave.StartRun();
                }

                if (enableAiSurvivalController && Match.State == MatchState.Running)
                {
                    ProcessAiStepwiseSequence(Time.deltaTime);
                    bool canDecide = !AiController.IsStepwiseCycleActive &&
                                     AiDecisionScheduler != null &&
                                     AiDecisionScheduler.Tick(Time.deltaTime, true);
                    if (canDecide)
                    {
                        if (AiController.BeginStepwiseCycle(TwentyWave.CurrentWaveIndex))
                        {
                            aiBoardActionRemaining = 0f;
                            ProcessAiStepwiseSequence(0f);
                        }
                    }
                }

                TwentyWave.Tick(Time.deltaTime);
                CaptureAiWaveDiagnostics();
                PressureDiagnostics?.Tick(Time.deltaTime);
                return;
            }

            WaveSystem?.Tick(Time.deltaTime);
        }

        private bool ProcessAiStepwiseSequence(float deltaTime)
        {
            if (AiController == null || !AiController.IsStepwiseCycleActive)
            {
                return false;
            }

            aiBoardActionRemaining -= Mathf.Max(0f, deltaTime);
            if (aiBoardActionRemaining > 0f)
            {
                return true;
            }

            if (AiController.TryExecuteStepwiseCycleStep(out var action))
            {
                var interval = Mathf.Max(0.05f, aiBoardActionIntervalSeconds);
                AiBoardView?.RefreshUnits(action, interval * 0.85f);
                aiBoardActionRemaining = interval;
            }
            else
            {
                aiBoardActionRemaining = 0f;
            }

            return AiController.IsStepwiseCycleActive;
        }

        private void OnDestroy()
        {
            if (Match != null)
            {
                Match.StateChanged -= HandleMatchStateChanged;
            }

            PressureDiagnostics?.Dispose();
            GameplayAnalyticsAdapter?.Detach();
            if (AnalyticsSession != null)
            {
                AnalyticsRuntimeRegistryV2.Clear(AnalyticsSession);
            }
            PersistRuneProfile();
            if (pressureDiagnosticsPanel != null)
            {
                Destroy(pressureDiagnosticsPanel.gameObject);
            }
        }

        private bool PersistRuneProfile()
        {
            if (RuneProfileRepository == null || RuneSaveData == null)
            {
                return false;
            }

            if (RuneProfileRepository.Save(RuneSaveData, out var error))
            {
                return true;
            }

            Debug.LogError("RuneProfileSaveFailed " + error);
            return false;
        }

        private void LockRuneLoadoutAtRunStart()
        {
            if (externalPlayerRuneLoadoutSnapshot != null)
            {
                if (RecruitDestination != null &&
                    !RecruitDestination.TrySetRuneLoadoutSnapshot(externalPlayerRuneLoadoutSnapshot))
                {
                    Debug.LogError("ExternalRuneLoadoutSnapshotRejectedAfterHeroFormation");
                }

                RecruitDestination?.SealRuneLoadoutSnapshot();
                RecordRuneLoadoutSnapshotLocked(externalPlayerRuneLoadoutSnapshot);
                return;
            }

            if (PlayerRuneLoadout == null)
            {
                return;
            }

            if (!PlayerRuneLoadout.LockForRunStart(out var error))
            {
                Debug.LogError("RuneLoadoutLockFailed " + error);
                RecruitDestination?.TrySetRuneLoadoutSnapshot(RuneLoadoutSnapshot.Empty);
                RecruitDestination?.SealRuneLoadoutSnapshot();
                RecordRuneLoadoutSnapshotLocked(RuneLoadoutSnapshot.Empty);
                return;
            }

            var snapshot = RuneSaveData.Loadout.RunStartSnapshot ?? RuneLoadoutSnapshot.Empty;
            if (RecruitDestination != null && !RecruitDestination.TrySetRuneLoadoutSnapshot(snapshot))
            {
                Debug.LogError("RuneLoadoutSnapshotRejectedAfterHeroFormation");
            }
            RecruitDestination?.SealRuneLoadoutSnapshot();
            RecordRuneLoadoutSnapshotLocked(snapshot);
        }

        private void RecordRuneLoadoutSnapshotLocked(RuneLoadoutSnapshot snapshot)
        {
            GameplayAnalyticsAdapter?.RecordRuneLoadoutSnapshotLocked(
                TeamSide.Player,
                0,
                snapshot ?? RuneLoadoutSnapshot.Empty);
        }

        private void HandleMatchStateChanged(MatchState state)
        {
            if (state == MatchState.Running)
            {
                if (GameplayAnalyticsAdapter != null && GameplayAnalyticsAdapter.RecordRunStart())
                {
                    GameplayAnalyticsAdapter.RecordFormationSnapshots(0, "run_start", "run:start");
                }
                return;
            }

            if (state != MatchState.Victory && state != MatchState.Defeat)
            {
                return;
            }

            int analyticsFinishWave = Match != null ? Match.CurrentWave : 0;
            GameplayAnalyticsAdapter?.RecordFormationSnapshots(
                analyticsFinishWave,
                "match_finish",
                "match:finish");
            GameplayAnalyticsAdapter?.RecordMatchFinished(
                analyticsFinishWave,
                state == MatchState.Victory ? "victory" : "defeat",
                ResolveAnalyticsFinishReason(state),
                ResolveAnalyticsElapsedSeconds());

            Debug.Log($"GreyboxLayoutStatistics Side=Player {PlayerLayoutStatistics}");
            Debug.Log($"GreyboxLayoutStatistics Side=AI {AiLayoutStatistics}");
            PressureDiagnostics?.StopAndReport(state.ToString());
            if (TwentyWave != null && AiController?.Diagnostics != null)
            {
                AiController.RecordRunEnd(
                    TwentyWave.CurrentWaveIndex,
                    TwentyWave.AiTotalKilled,
                    TwentyWave.AiTotalReachedGoal);
                Debug.Log(AiController.Diagnostics.CreateSummary());
            }
        }

        private string ResolveAnalyticsFinishReason(MatchState state)
        {
            if (state == MatchState.Victory) return "opponent_defeated";
            if (Match == null) return "unknown";
            bool playerDefeated = Match.Player.HatchlingHealth <= 0 || Match.Player.IsInstantDefeated;
            bool aiDefeated = Match.AI.HatchlingHealth <= 0 || Match.AI.IsInstantDefeated;
            if (playerDefeated && aiDefeated) return "simultaneous_defeat";
            if (playerDefeated) return "player_defeated";
            return "run_rule_defeat";
        }

        private float ResolveAnalyticsElapsedSeconds()
        {
            if (TwentyWave != null) return TwentyWave.ElapsedRunTime;
            return ThreeWave != null ? ThreeWave.ElapsedRunTime : 0f;
        }

        private void CaptureAiWaveDiagnostics()
        {
            if (AiController?.Diagnostics == null || TwentyWave == null)
            {
                return;
            }

            var currentWave = TwentyWave.CurrentWaveIndex;
            if (currentWave < 1)
            {
                return;
            }

            if (lastAiDiagnosticsWave > 0 && currentWave != lastAiDiagnosticsWave)
            {
                AiController.RecordWaveEnd(
                    lastAiDiagnosticsWave,
                    TwentyWave.AiTotalKilled,
                    TwentyWave.AiTotalReachedGoal);
            }

            lastAiDiagnosticsWave = currentWave;
        }

        private void ConfigureBatchPresentations(GreyboxBoardView view, RecruitBatch batch)
        {
            if (view == null || batch == null)
            {
                return;
            }

            foreach (var card in batch.Cards)
            {
                view.SetUnitPresentation(
                    card.RuntimeId,
                    GetCardLabel(card),
                    card.Kind == RecruitItemKind.BasicUnit
                        ? UnitRangeRules.GetRadiusForConfig(card.ConfigId)
                        : 0f,
                    card.Kind == RecruitItemKind.BasicUnit);
            }
        }

        private static string GetCardLabel(RecruitCard card)
        {
            if (card.Kind == RecruitItemKind.Shovel)
            {
                return "SHOVEL";
            }

            if (card.Kind != RecruitItemKind.BasicUnit)
            {
                return HeroSliceCatalog.GetComponentDisplayNameEn(card.ConfigId);
            }

            return $"{BasicUnitCatalog.GetDisplayName(card.ConfigId)} {card.Level}";
        }

        private BattlefieldLayoutDefinition ResolveBattlefieldLayout()
        {
            if (BattlefieldLayoutDefinitions.TryGet(battlefieldLayoutId, out var layout))
            {
                return layout;
            }

            Debug.LogError(
                $"Unknown battlefield layout '{battlefieldLayoutId}'. Falling back to " +
                $"{BattlefieldLayoutDefinitions.Default.LayoutId}.",
                this);
            return BattlefieldLayoutDefinitions.Default;
        }
    }
}
