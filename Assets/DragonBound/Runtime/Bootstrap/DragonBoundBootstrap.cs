using DragonBound.AI;
using DragonBound.Analytics;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Items;
using DragonBound.Presentation;
using DragonBound.Recruitment;
using DragonBound.Runes;
using GameShared.Random;
using UnityEngine;

namespace DragonBound.Bootstrap
{
    public sealed class DragonBoundBootstrap : MonoBehaviour
    {
        [SerializeField] private int runSeed = 20260801;
        [SerializeField] private string battlefieldLayoutId = BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01Id;
        [SerializeField] private bool enableHeroComponents = false;
        [SerializeField] private bool heroSliceMode = false;
        [SerializeField] private bool useTwentyWavePressureRuntime = false;
        [SerializeField] private bool enablePressureRunDiagnostics = false;
        [SerializeField] private bool enableAiSurvivalController = true;
        [SerializeField] private RecruitComponentPolicy recruitComponentPolicy = RecruitComponentPolicy.V3;
        [SerializeField, Min(20)] private int heroSliceStartingResources = 500;
        [SerializeField] private DragonBoundScreenView screenView;

        public MatchController Match { get; private set; }
        public RunSeed Seed { get; private set; }
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
        public LimitedComponentBag ComponentBag { get; private set; }
        public LimitedComponentBag AiComponentBag { get; private set; }
        public ThreeWaveSliceRuntime ThreeWave { get; private set; }
        public WaveSystem WaveSystem { get; private set; }
        public TwentyWavePressureRuntime TwentyWave { get; private set; }
        public PressureRunDiagnostics PressureDiagnostics { get; private set; }
        public BasicUnitAiController AiController { get; private set; }
        /// <summary>Meta-owned data. The current greybox creates an empty local profile;
        /// save/backend loading can replace it before the next Run starts.</summary>
        public RuneSaveData RuneSaveData { get; private set; }
        public IRuneProfileRepository RuneProfileRepository { get; private set; }
        public RuneFeatureGate RuneFeatureGate { get; private set; }
        public RuneLoadoutService PlayerRuneLoadout { get; private set; }
        public RuneRunRewardService PlayerRuneRewards { get; private set; }
        /// <summary>Optional V2 Rune lifecycle observer supplied by the integration owner.</summary>
        public RuneAnalyticsAdapterV2 RuneAnalyticsAdapter { get; set; }
        public GreyboxRunStatistics PlayerLayoutStatistics { get; private set; }
        public GreyboxRunStatistics AiLayoutStatistics { get; private set; }
        /// <summary>Injected by the matchmaking/bootstrap boundary before a Run. The greybox
        /// deliberately has no local inventory fallback that could fabricate a server loadout.</summary>
        public IItemRunSnapshotProvider ItemRunSnapshotProvider { get; set; }
        /// <summary>External account boundary for a validated player profile and AI snapshot.</summary>
        public IItemValidatedProfileSnapshotSource ItemProfileSnapshotSource { get; set; }

        public const float InitializationPromptSeconds = 1f;
        private float initializationRemaining;
        private PressureRunDiagnosticsPanel pressureDiagnosticsPanel;
        private DevelopmentGameplayTestPanel developmentGameplayTestPanel;
        private DevelopmentItemRunSnapshotProvider developmentItemSnapshotProvider;
        private int lastAiDiagnosticsWave;

        // Test-only injection keeps persistence tests away from a developer's real local profile.
        public static IRuneProfileRepository RuneProfileRepositoryOverrideForTests { get; set; }
        public static IItemRunSnapshotProvider ItemRunSnapshotProviderOverrideForTests { get; set; }
        public static IItemValidatedProfileSnapshotSource ItemProfileSnapshotSourceOverrideForTests { get; set; }
        public static RuneAnalyticsAdapterV2 RuneAnalyticsAdapterOverrideForTests { get; set; }

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

        [ContextMenu("DEV/Spawn Player Dragon Rider")]
        private void SpawnPlayerDragonRider()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.Player, DragonBoundHeroIds.DragonRider);
        }

        [ContextMenu("DEV/Spawn Player Runebolt Mage")]
        private void SpawnPlayerRuneboltMage()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.Player, DragonBoundHeroIds.RuneboltMage);
        }

        [ContextMenu("DEV/Spawn Player Stonebinder")]
        private void SpawnPlayerStonebinder()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.Player, DragonBoundHeroIds.Stonebinder);
        }

        [ContextMenu("DEV/Spawn Player Starfall Archmage")]
        private void SpawnPlayerStarfallArchmage()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.Player, DragonBoundHeroIds.StarfallArchmage);
        }

        [ContextMenu("DEV/Spawn Player Crown Sword Leader")]
        private void SpawnPlayerCrownSwordLeader()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.Player, DragonBoundHeroIds.CrownSwordLeader);
        }

        [ContextMenu("DEV/Spawn Player Crown Hunter Leader")]
        private void SpawnPlayerCrownHunterLeader()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.Player, DragonBoundHeroIds.CrownHunterLeader);
        }

        [ContextMenu("DEV/Spawn Player Thunder Jarl")]
        private void SpawnPlayerThunderJarl()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.Player, DragonBoundHeroIds.ThunderJarl);
        }

        [ContextMenu("DEV/Spawn Player Nightfang Assassin")]
        private void SpawnPlayerNightfangAssassin()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.Player, DragonBoundHeroIds.NightfangAssassin);
        }

        [ContextMenu("DEV/Spawn Player Leviathan Hunter")]
        private void SpawnPlayerLeviathanHunter()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.Player, DragonBoundHeroIds.LeviathanHunter);
        }

        [ContextMenu("DEV/Spawn Player Skyhunter Valkyrie")]
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

        [ContextMenu("DEV/Spawn AI Dragon Rider")]
        private void SpawnAiDragonRider()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.AI, DragonBoundHeroIds.DragonRider);
        }

        [ContextMenu("DEV/Spawn AI Runebolt Mage")]
        private void SpawnAiRuneboltMage()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.AI, DragonBoundHeroIds.RuneboltMage);
        }

        [ContextMenu("DEV/Spawn AI Stonebinder")]
        private void SpawnAiStonebinder()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.AI, DragonBoundHeroIds.Stonebinder);
        }

        [ContextMenu("DEV/Spawn AI Starfall Archmage")]
        private void SpawnAiStarfallArchmage()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.AI, DragonBoundHeroIds.StarfallArchmage);
        }

        [ContextMenu("DEV/Spawn AI Crown Sword Leader")]
        private void SpawnAiCrownSwordLeader()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.AI, DragonBoundHeroIds.CrownSwordLeader);
        }

        [ContextMenu("DEV/Spawn AI Crown Hunter Leader")]
        private void SpawnAiCrownHunterLeader()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.AI, DragonBoundHeroIds.CrownHunterLeader);
        }

        [ContextMenu("DEV/Spawn AI Thunder Jarl")]
        private void SpawnAiThunderJarl()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.AI, DragonBoundHeroIds.ThunderJarl);
        }

        [ContextMenu("DEV/Spawn AI Nightfang Assassin")]
        private void SpawnAiNightfangAssassin()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.AI, DragonBoundHeroIds.NightfangAssassin);
        }

        [ContextMenu("DEV/Spawn AI Leviathan Hunter")]
        private void SpawnAiLeviathanHunter()
        {
            TryDebugSpawnDragonRouteHero(TeamSide.AI, DragonBoundHeroIds.LeviathanHunter);
        }

        [ContextMenu("DEV/Spawn AI Skyhunter Valkyrie")]
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
            Time.timeScale = 1f;
            Debug.Log("TimeScaleInitialized Time.timeScale=1");
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
                developmentItemSnapshotProvider = new DevelopmentItemRunSnapshotProvider();
                ItemRunSnapshotProvider = developmentItemSnapshotProvider;
            }
#endif
            RuneProfileRepository = RuneProfileRepositoryOverrideForTests ?? new LocalRuneProfileRepository();
            var runeProfileResult = RuneProfileRepository.Load();
            RuneSaveData = runeProfileResult.Data ?? new RuneSaveData();
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
                unchecked(runSeed ^ 0x2468ACE0),
                LimitedComponentBag.DefaultContentVersion,
                catalog);
            PlayerShovelState = new ShovelRecruitmentState(
                () => PlayerBoard.GetPositions(CellType.Locked).Count);
            AiShovelState = new ShovelRecruitmentState(
                () => AiBoard.GetPositions(CellType.Locked).Count);
            var deck = CreateRecruitDeck(catalog, "player", ComponentBag, 0x13579BDF, PlayerShovelState);
            var aiDeck = CreateRecruitDeck(catalog, "ai", AiComponentBag, 0x2468ACE0, AiShovelState);
            RecruitDestination = new BoardRecruitDestination(
                PlayerBoard,
                runeRunSeed: runSeed);
            Recruitment = new RecruitmentService(Match.Player, deck, RecruitDestination);
            AiRecruitDestination = new BoardRecruitDestination(AiBoard, runeRunSeed: unchecked(runSeed ^ 0x2468ACE0));
            AiRecruitment = new RecruitmentService(Match.AI, aiDeck, AiRecruitDestination);
            PlayerShovelUnlocks = new ShovelUnlockService(PlayerBoard, RecruitDestination);
            AiShovelUnlocks = new ShovelUnlockService(AiBoard, AiRecruitDestination);
            AiController = new BasicUnitAiController(
                AiBoard,
                AiRecruitDestination,
                AiRecruitment,
                AiShovelUnlocks,
                Match.AI);
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
                AiRecruitDestination,
                PlayerRuneLoadout,
                () => Match != null && Match.State == MatchState.Ready,
                developmentItemSnapshotProvider);
            BoardView.BindShovelUnlockService(PlayerShovelUnlocks);
            AiBoardView.BindShovelUnlockService(AiShovelUnlocks);

            if (useTwentyWavePressureRuntime)
            {
                TwentyWave = new TwentyWavePressureRuntime(
                    Match,
                    RecruitDestination,
                    AiRecruitDestination,
                    runSeed,
                    playerRuneRewards: PlayerRuneRewards,
                    itemSnapshotProvider: ItemRunSnapshotProvider);
                screenView.BindWaveRuntime(TwentyWave);
                screenView.HudView.BindItemRuntime(TwentyWave);
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!Application.isBatchMode && developmentItemSnapshotProvider != null)
            {
                developmentGameplayTestPanel = DevelopmentGameplayTestPanel.Create(
                    this,
                    developmentItemSnapshotProvider);
            }
#endif

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

            if (useTwentyWavePressureRuntime && enableAiSurvivalController)
            {
                AiController.Tick();
            }
            else
            {
                var aiRecruitments = heroSliceMode ? 3 : 1;
                for (var index = 0; index < aiRecruitments; index++)
                {
                    var aiOpening = AiController.RecruitOrRefresh();
                    if (aiOpening.Status != RecruitmentStatus.Success)
                    {
                        Debug.LogError("AI opening recruitment failed despite using the same starting resources.");
                        break;
                    }
                }

                AiController.DeployOpeningUnits(heroSliceMode ? 2 : 3);
            }

            AiBoardView.RefreshUnits();
            Match.TryTransition(MatchState.Ready);
            initializationRemaining = InitializationPromptSeconds;
            Debug.Log("MatchStateChanged State=Ready InitializationComplete=true");
        }

        private RecruitDeck CreateRecruitDeck(
            RecruitmentCatalog catalog,
            string runtimePrefix,
            LimitedComponentBag componentBag,
            int sideSeedSalt,
            ShovelRecruitmentState shovelState)
        {
            if (enableHeroComponents && !heroSliceMode)
            {
                return new RecruitDeck(
                    catalog,
                    unchecked(runSeed ^ sideSeedSalt),
                    runtimePrefix,
                    componentBag,
                    shovelState: shovelState,
                    componentPolicy: recruitComponentPolicy,
                    currentWaveProvider: () => Match != null ? Match.CurrentWave : 1);
            }

            return new RecruitDeck(
                catalog,
                new RunRandom(unchecked(runSeed ^ sideSeedSalt)),
                runtimePrefix,
                enableHeroComponents,
                heroSliceMode);
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
                var loadoutUiOpen = (screenView?.IsRuneLoadoutOpen ?? false) ||
                                    (developmentGameplayTestPanel?.IsOpen ?? false);
                if (!loadoutUiOpen)
                {
                    initializationRemaining -= Time.deltaTime;
                }
                if (initializationRemaining <= 0f && !loadoutUiOpen)
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

                if (enableAiSurvivalController)
                {
                    AiController.Tick();
                    if (AiController.LastCycleChanged)
                    {
                        AiBoardView.RefreshUnits();
                    }
                }

                TwentyWave.Tick(Time.deltaTime);
                CaptureAiWaveDiagnostics();
                PressureDiagnostics?.Tick(Time.deltaTime);
                return;
            }

            WaveSystem?.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (Match != null)
            {
                Match.StateChanged -= HandleMatchStateChanged;
            }

            PressureDiagnostics?.Dispose();
            PersistRuneProfile();
            if (pressureDiagnosticsPanel != null)
            {
                Destroy(pressureDiagnosticsPanel.gameObject);
            }
        }

        internal bool TryPrepareDevelopmentRuneProfile(out string reason)
        {
            if (Match == null || Match.State != MatchState.Ready || RuneSaveData == null)
            {
                reason = "RunAlreadyStarted";
                return false;
            }

            RuneSaveData.AccountDay = RuneFeatureGate.UnlockAccountDay;
            RuneSaveData.Loadout.UnlockForLoadoutEditing();
            foreach (var rune in RuneCatalog.All)
            {
                var missingCopies = HeroDefinitionCatalog.Definitions.Count -
                                    RuneSaveData.Inventory.OwnedCount(rune.RuneId);
                if (missingCopies > 0)
                {
                    RuneSaveData.Inventory.AddComplete(rune.RuneId, missingCopies);
                }
            }

            if (!PersistRuneProfile())
            {
                reason = "RuneProfileSaveFailed";
                return false;
            }

            screenView?.RuneLoadoutView?.Refresh();
            reason = string.Empty;
            return true;
        }

        internal void OpenDevelopmentRuneLoadout()
        {
            if (Match != null && Match.State == MatchState.Ready)
            {
                screenView?.RuneLoadoutView?.Open();
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
            if (PlayerRuneLoadout == null)
            {
                return;
            }

            if (!PlayerRuneLoadout.LockForRunStart(out var error))
            {
                Debug.LogError("RuneLoadoutLockFailed " + error);
                RecruitDestination?.TrySetRuneLoadoutSnapshot(RuneLoadoutSnapshot.Empty);
                RecruitDestination?.SealRuneLoadoutSnapshot();
                return;
            }

            var snapshot = RuneSaveData.Loadout.RunStartSnapshot ?? RuneLoadoutSnapshot.Empty;
            if (RecruitDestination != null && !RecruitDestination.TrySetRuneLoadoutSnapshot(snapshot))
            {
                Debug.LogError("RuneLoadoutSnapshotRejectedAfterHeroFormation");
            }
            RecruitDestination?.SealRuneLoadoutSnapshot();
        }

        private void HandleMatchStateChanged(MatchState state)
        {
            if (state != MatchState.Victory && state != MatchState.Defeat)
            {
                return;
            }

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
                return "铲子";
            }

            if (card.Kind != RecruitItemKind.BasicUnit)
            {
                return HeroSliceCatalog.GetComponentDisplayName(card.ConfigId);
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
