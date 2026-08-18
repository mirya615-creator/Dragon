using DragonBound.AI;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Presentation;
using DragonBound.Recruitment;
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
        public int HeroSliceStartingResources => Mathf.Max(MatchController.StartingResources, heroSliceStartingResources);
        public GreyboxBoardView BoardView => screenView != null ? screenView.BoardView : null;
        public GreyboxBoardView AiBoardView => screenView != null ? screenView.AiBoardView : null;
        public BoardRecruitDestination RecruitDestination { get; private set; }
        public RecruitmentService Recruitment { get; private set; }
        public BoardRecruitDestination AiRecruitDestination { get; private set; }
        public RecruitmentService AiRecruitment { get; private set; }
        public LimitedComponentBag ComponentBag { get; private set; }
        public LimitedComponentBag AiComponentBag { get; private set; }
        public ThreeWaveSliceRuntime ThreeWave { get; private set; }
        public WaveSystem WaveSystem { get; private set; }
        public BasicUnitAiController AiController { get; private set; }
        public GreyboxRunStatistics PlayerLayoutStatistics { get; private set; }
        public GreyboxRunStatistics AiLayoutStatistics { get; private set; }

        public const float InitializationPromptSeconds = 1f;
        private float initializationRemaining;

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
            if (enableHeroComponents)
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
            var deck = CreateRecruitDeck(catalog, "player", ComponentBag, 0x13579BDF);
            var aiDeck = CreateRecruitDeck(catalog, "ai", AiComponentBag, 0x2468ACE0);
            RecruitDestination = new BoardRecruitDestination(PlayerBoard);
            Recruitment = new RecruitmentService(Match.Player, deck, RecruitDestination);
            AiRecruitDestination = new BoardRecruitDestination(AiBoard);
            AiRecruitment = new RecruitmentService(Match.AI, aiDeck, AiRecruitDestination);
            AiController = new BasicUnitAiController(AiBoard, AiRecruitDestination, AiRecruitment);
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
            AiBoardView.RefreshUnits();
            WaveSystem = new WaveSystem(
                Match,
                RecruitDestination,
                AiRecruitDestination,
                heroSliceMode
                    ? ThreeWaveEnemyDurabilityProfile.HeroSkillShowcase
                    : ThreeWaveEnemyDurabilityProfile.BasicUnitBaseline);
            ThreeWave = WaveSystem.Runtime;
            screenView.BindWaveRuntime(ThreeWave);
            Match.TryTransition(MatchState.Ready);
            initializationRemaining = InitializationPromptSeconds;
            Debug.Log("MatchStateChanged State=Ready InitializationComplete=true");
        }

        private RecruitDeck CreateRecruitDeck(
            RecruitmentCatalog catalog,
            string runtimePrefix,
            LimitedComponentBag componentBag,
            int sideSeedSalt)
        {
            if (enableHeroComponents && !heroSliceMode)
            {
                return new RecruitDeck(
                    catalog,
                    unchecked(runSeed ^ sideSeedSalt),
                    runtimePrefix,
                    componentBag);
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
                initializationRemaining -= Time.deltaTime;
                if (initializationRemaining <= 0f)
                {
                    Match.TryTransition(MatchState.Running);
                    Debug.Log("MatchStateChanged State=Running InitializationPromptHidden=true");
                }

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
        }

        private void HandleMatchStateChanged(MatchState state)
        {
            if (state != MatchState.Victory && state != MatchState.Defeat)
            {
                return;
            }

            Debug.Log($"GreyboxLayoutStatistics Side=Player {PlayerLayoutStatistics}");
            Debug.Log($"GreyboxLayoutStatistics Side=AI {AiLayoutStatistics}");
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
