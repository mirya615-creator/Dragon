using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DragonBound.Bootstrap;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Presentation;
using DragonBound.Recruitment;
using DragonBound.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DragonBound.Tests.PlayMode
{
    public sealed class BootstrapPlayModeTests
    {
        [SetUp]
        public void UseDeterministicGameplayRun()
        {
            GameplayRunGatewayRegistry.Install(new FixedGameplayRunGateway(20260801));
        }

        [Test]
        public void HeroGallerySummaryUsesTwoLineComponentAndDescriptionFormat()
        {
            var recipe = HeroRecipeCatalog.Get(DragonBoundHeroIds.RuneboltMage);

            Assert.AreEqual(
                "Left: Rune Staff  Right: Rune Apprentice\n" +
                "A rune mage whose attacks pierce through enemies in a straight line.",
                CampPanelView.BuildHeroSummary(
                    recipe,
                    "Rune Staff",
                    "Rune Apprentice",
                    "A rune mage whose attacks pierce through enemies in a straight line."));
        }

        [UnityTest]
        public IEnumerator CampDeckPartInitializesFromCurrentScreenHierarchy()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            DragonBoundScreenView screen = FindScreen();
            Assert.IsNotNull(screen.CampPanelView);
            Assert.AreEqual(4, screen.CampPanelView.UnitEntryCount);
            Assert.AreEqual(18, screen.CampPanelView.ComponentEntryCount);
        }

        [UnityTest]
        public IEnumerator CampComponentSpritesPopulateDeckAndSelectedHeroRecipe()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            DragonBoundScreenView screen = FindScreen();
            var provider = new ResourcesCampComponentArtProvider();
            var componentContainer = screen.transform.Find(
                "campPanel/CampBg/DeckPart/ComponentContainer");
            Assert.IsNotNull(componentContainer);

            for (var index = 0; index < HeroComponentCatalog.Definitions.Count; index++)
            {
                var definition = HeroComponentCatalog.Definitions[index];
                Assert.IsTrue(provider.TryGetHeroComponentSprite(definition.Id, out var expected));
                Assert.AreSame(
                    expected,
                    componentContainer.GetChild(index).GetComponent<Image>().sprite,
                    definition.Id);
            }

            var collectionPart = screen.transform.Find("campPanel/CampBg/CollectionPart");
            Assert.IsNotNull(collectionPart);
            Assert.IsTrue(provider.TryGetHeroComponentSprite(
                DragonBoundComponentIds.SkyRanger,
                out var expectedTop));
            Assert.IsTrue(provider.TryGetHeroComponentSprite(
                DragonBoundComponentIds.ContractHatchling,
                out var expectedBottom));
            Assert.AreSame(expectedTop, collectionPart.Find("Img1").GetComponent<Image>().sprite);
            Assert.AreSame(expectedBottom, collectionPart.Find("Img2").GetComponent<Image>().sprite);
        }

        [UnityTest]
        public IEnumerator GreyboxMainInitializesIndependentPlayerAndAiBattlefields()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = FindBootstrap();
            Assert.AreEqual(1f, Time.timeScale, 0.0001f);
            while (bootstrap.Match.State == MatchState.Ready)
            {
                Assert.AreEqual(0, bootstrap.Match.Player.RemainingEnemyCount);
                Assert.AreEqual(0, bootstrap.Match.AI.RemainingEnemyCount);
                yield return null;
            }
            Assert.AreEqual(20260801, bootstrap.Seed.Value);
            Assert.AreEqual(MatchState.Running, bootstrap.Match.State);
            Assert.AreEqual(1, bootstrap.Match.CurrentWave);
            Assert.AreEqual(20, bootstrap.Match.Player.Resources);
            Assert.AreEqual(10, bootstrap.Match.AI.Resources);
            Assert.AreEqual(0, bootstrap.Match.Player.RemainingEnemyCount);
            Assert.AreEqual(0, bootstrap.Match.AI.RemainingEnemyCount);
            Assert.IsTrue(bootstrap.EnableHeroComponents);
            Assert.IsTrue(bootstrap.UseTwentyWavePressureRuntime);
            Assert.IsNotNull(bootstrap.TwentyWave);
            Assert.IsNull(bootstrap.ThreeWave);
            Assert.IsFalse(bootstrap.Recruitment.HasLastAttempt);
            Assert.AreEqual(1, bootstrap.AiRecruitment.CompletedRecruitments);
            Assert.AreEqual(
                bootstrap.AiRecruitDestination.TotalObjectCount,
                bootstrap.AiRecruitDestination.CampCount + bootstrap.AiRecruitDestination.DeployedCount);
            Assert.LessOrEqual(bootstrap.AiRecruitDestination.TotalObjectCount, 5);
            Assert.GreaterOrEqual(bootstrap.AiRecruitDestination.TotalObjectCount, 1);
            Assert.Greater(
                bootstrap.AiRecruitDestination.GetDeployedCards()
                    .Count(card => card.Kind == RecruitItemKind.BasicUnit),
                0,
                "AI V0 must deploy at least one ordinary unit before pressure begins.");

            AssertBoardModel(bootstrap.PlayerBoard, bootstrap.BattlefieldLayout);
            AssertBoardModel(bootstrap.AiBoard, bootstrap.BattlefieldLayout);
            Assert.AreNotSame(bootstrap.PlayerBoard, bootstrap.AiBoard);

            Assert.IsNotNull(bootstrap.BoardView);
            Assert.IsNotNull(bootstrap.AiBoardView);
            Assert.AreSame(bootstrap.PlayerBoard, bootstrap.BoardView.Board);
            Assert.AreSame(bootstrap.AiBoard, bootstrap.AiBoardView.Board);
            Assert.IsTrue(bootstrap.BoardView.AllowInteraction);
            Assert.IsFalse(bootstrap.AiBoardView.AllowInteraction);
            Assert.AreEqual(
                bootstrap.BattlefieldLayout.InitialUnlockedCellCount,
                bootstrap.BoardView.CellViews.Count(cell => cell.CellType == CellType.Battle));
            Assert.AreEqual(
                bootstrap.BattlefieldLayout.FormationCellCount - bootstrap.BattlefieldLayout.InitialUnlockedCellCount,
                bootstrap.BoardView.CellViews.Count(cell => cell.CellType == CellType.Locked));
            Assert.AreEqual(
                bootstrap.BattlefieldLayout.InitialUnlockedCellCount,
                bootstrap.AiBoardView.CellViews.Count(cell => cell.CellType == CellType.Battle));
            Assert.AreEqual(
                bootstrap.BattlefieldLayout.FormationCellCount - bootstrap.BattlefieldLayout.InitialUnlockedCellCount,
                bootstrap.AiBoardView.CellViews.Count(cell => cell.CellType == CellType.Locked));

            Assert.AreEqual(0, bootstrap.RecruitDestination.TotalObjectCount);
            Assert.IsFalse(bootstrap.PlayerBoard.TryGetPosition("greybox.player.axe", out _));
            Assert.IsFalse(bootstrap.AiBoard.TryGetPosition("greybox.ai.axe", out _));
        }

        private sealed class FixedGameplayRunGateway : IGameplayRunGateway
        {
            private readonly int seed;
            private readonly LocalGameplayRunGateway inner = new LocalGameplayRunGateway();

            public FixedGameplayRunGateway(int seed)
            {
                this.seed = seed;
            }

            public Task<StartGameplayRunResult> StartRunAsync(
                StartGameplayRunRequest request,
                CancellationToken cancellationToken)
            {
                request.UseDiagnosticSeed = true;
                request.DiagnosticSeed = seed;
                return inner.StartRunAsync(request, cancellationToken);
            }

            public Task<RecruitGameplayResult> RecruitAsync(
                RecruitGameplayRequest request,
                CancellationToken cancellationToken)
            {
                return inner.RecruitAsync(request, cancellationToken);
            }

            public Task<FinishGameplayRunResult> FinishRunAsync(
                FinishGameplayRunRequest request,
                CancellationToken cancellationToken)
            {
                return inner.FinishRunAsync(request, cancellationToken);
            }
        }

        [UnityTest]
        public IEnumerator InitializationCompletesBeforeWaveRuntimeStarts()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = FindBootstrap();
            Assert.AreEqual(1f, Time.timeScale, 0.0001f);
            Assert.That(bootstrap.Match.State, Is.EqualTo(MatchState.Ready).Or.EqualTo(MatchState.Running));
            while (bootstrap.Match.State == MatchState.Ready)
            {
                Assert.AreEqual(0, bootstrap.Match.CurrentWave);
                Assert.AreEqual(0, bootstrap.Match.Player.RemainingEnemyCount);
                Assert.AreEqual(0, bootstrap.Match.AI.RemainingEnemyCount);
                yield return null;
            }

            Assert.AreEqual(MatchState.Running, bootstrap.Match.State);
            yield return null;
            Assert.AreEqual(1, bootstrap.Match.CurrentWave);
            Assert.IsNotNull(bootstrap.TwentyWave);
            Assert.AreEqual(
                bootstrap.TwentyWave.Configuration.GetWave(1).WaveDurationSeconds,
                bootstrap.TwentyWave.WaveDurationSeconds);
            Assert.LessOrEqual(
                bootstrap.TwentyWave.WaveRemainingSeconds,
                bootstrap.TwentyWave.WaveDurationSeconds);
        }

        [UnityTest]
        public IEnumerator RecruitingFiveCardsThenRefreshingAFullBenchReplacesEveryCard()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = FindBootstrap();
            Assert.AreEqual(RecruitDestinationPlan.AddToEmptySlots, bootstrap.Recruitment.NextDestinationPlan);

            var first = bootstrap.Recruitment.TryRecruit();
            Assert.AreEqual(RecruitmentStatus.Success, first.Status);
            Assert.IsFalse(first.RefreshedBench);
            Assert.IsNotNull(first.Batch);
            Assert.AreEqual(RecruitmentService.CardsPerRecruitment, first.Batch.Cards.Count);
            Assert.IsTrue(first.Batch.Cards.Any(card => card.Kind == RecruitItemKind.BasicUnit));
            var firstIds = GetBenchOccupants(bootstrap.PlayerBoard);
            Assert.AreEqual(RecruitmentService.CardsPerRecruitment, firstIds.Count);
            Assert.AreEqual(RecruitmentService.CardsPerRecruitment, bootstrap.RecruitDestination.TotalObjectCount);
            Assert.AreEqual(RecruitDestinationPlan.RefreshBench, bootstrap.Recruitment.NextDestinationPlan);

            bootstrap.BoardView.RefreshUnits();
            yield return null;
            Assert.AreEqual(
                bootstrap.Recruitment.NextCost.ToString(),
                FindScreen().RecruitmentView.RecruitButtonLabel.text);
            foreach (var firstId in firstIds)
            {
                Assert.IsNotNull(FindUnitView(bootstrap.BoardView, firstId));
            }

            bootstrap.Match.Player.AddResources(bootstrap.Recruitment.NextCost - bootstrap.Match.Player.Resources);
            var second = default(RecruitmentAttempt);
            bootstrap.Recruitment.Attempted += attempt => second = attempt;
            FindScreen().RecruitmentView.RecruitButton.onClick.Invoke();
            Assert.AreEqual(RecruitmentStatus.Success, second.Status);
            Assert.AreEqual(2, second.Sequence);
            Assert.IsTrue(second.RefreshedBench);
            Assert.IsNotNull(second.Batch);
            Assert.AreEqual(RecruitmentService.CardsPerRecruitment, second.Batch.Cards.Count);

            var secondIds = GetBenchOccupants(bootstrap.PlayerBoard);
            Assert.AreEqual(RecruitmentService.CardsPerRecruitment, secondIds.Count);
            CollectionAssert.IsEmpty(firstIds.Intersect(secondIds).ToArray());
            foreach (var oldRuntimeId in firstIds)
            {
                Assert.IsFalse(bootstrap.RecruitDestination.TryGetCard(oldRuntimeId, out _));
            }

            foreach (var newRuntimeId in secondIds)
            {
                Assert.IsTrue(bootstrap.RecruitDestination.TryGetCard(newRuntimeId, out _));
            }

            Assert.AreEqual(2, bootstrap.Match.Player.RecruitmentCount);
            Assert.AreEqual(0, bootstrap.Match.Player.Resources);

            yield return null;
            foreach (var oldRuntimeId in firstIds)
            {
                Assert.IsNull(FindUnitView(bootstrap.BoardView, oldRuntimeId));
            }

            foreach (var newRuntimeId in secondIds)
            {
                Assert.IsNotNull(FindUnitView(bootstrap.BoardView, newRuntimeId));
            }
        }

        [UnityTest]
        public IEnumerator SelectedUnitShowsCircularRangeAndBothRoadMarkersMoveOnTheirRoutes()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = FindBootstrap();
            var playerView = bootstrap.BoardView;
            Assert.IsFalse(playerView.RangePreview.enabled);
            var recruit = bootstrap.Recruitment.TryRecruit();
            Assert.AreEqual(RecruitmentStatus.Success, recruit.Status);
            var basicCard = recruit.Batch.Cards.First(card => card.Kind == RecruitItemKind.BasicUnit);
            var bench = bootstrap.PlayerBoard.GetPositions(CellType.Bench);
            var battle = bootstrap.PlayerBoard.GetPositions(CellType.Battle)[0];
            Assert.IsTrue(bootstrap.PlayerBoard.TryGetPosition(basicCard.RuntimeId, out var basicOrigin));
            Assert.IsTrue(bootstrap.PlayerBoard.TryMove(basicOrigin, battle));
            playerView.RefreshUnits();
            playerView.SetUnitPresentation(
                basicCard.RuntimeId,
                "AXE 1",
                UnitRangeRules.GetRadius(BasicUnitArchetype.Axe),
                true);
            playerView.SelectUnit(basicCard.RuntimeId);
            var range = playerView.RangePreview;
            Assert.IsNotNull(range);
            Assert.IsTrue(range.enabled);
            Assert.IsNotNull(range.sprite);
            Assert.IsTrue(range.preserveAspect);
            Assert.LessOrEqual(range.color.a, 0.08f);
            var rangeOutline = range.transform.Find("ART_RangeOutline").GetComponent<Image>();
            Assert.IsNotNull(rangeOutline);
            Assert.That(rangeOutline.color.a, Is.InRange(0.5f, 0.7f));
            Assert.AreEqual(range.rectTransform.sizeDelta.x, range.rectTransform.sizeDelta.y, 0.01f);

            Assert.IsTrue(bootstrap.PlayerBoard.TryGetPosition(basicCard.RuntimeId, out var axePosition));
            var axeCell = playerView.GetCellView(axePosition);
            Assert.IsNotNull(axeCell);
            var cellSize = Mathf.Min(axeCell.RectTransform.rect.width, axeCell.RectTransform.rect.height);
            var expectedDiameter = cellSize * UnitRangeRules.GetRadius(BasicUnitArchetype.Axe) * 2f;
            Assert.AreEqual(expectedDiameter, range.rectTransform.sizeDelta.x, 0.01f);
            Assert.Less(Vector3.Distance(range.rectTransform.position, axeCell.ContentAnchor.position), 0.1f);
            Assert.IsTrue(playerView.BeginDrag(basicCard.RuntimeId));
            Assert.IsTrue(bootstrap.RecruitDestination.IsCombatSuspended(basicCard.RuntimeId));
            Assert.IsFalse(range.enabled, "Range must hide as soon as dragging starts.");
            playerView.CompleteDrag(basicCard.RuntimeId, new Vector2(-1000f, -1000f));
            Assert.IsFalse(bootstrap.RecruitDestination.IsCombatSuspended(basicCard.RuntimeId));
            Assert.IsFalse(range.enabled, "Range must remain hidden after a cancelled drag.");

            while (bootstrap.Match.State == MatchState.Ready)
            {
                yield return null;
            }

            Assert.AreEqual(MatchState.Running, bootstrap.Match.State);
            yield return null;

            var lanes = new[]
            {
                FindScreen().PlayerBattlefieldView.LaneView,
                FindScreen().AiBattlefieldView.LaneView
            };
            // The formal twenty-wave runtime deliberately gives the player a four-second
            // preparation window. Enemy presentation must respect that schedule instead of
            // forcing the first pair of views into the first few rendered frames.
            yield return new WaitForSecondsRealtime(
                TwentyWavePressureConfiguration.StartPreparationSeconds - 0.15f);
            Assert.IsTrue(lanes.All(lane => lane.EnemyViewCount == 0));
            yield return new WaitForSecondsRealtime(0.30f);
            for (var frame = 0; frame < 10 && lanes.Any(lane => lane.EnemyViewCount == 0); frame++)
            {
                yield return null;
            }

            Assert.IsTrue(lanes.All(lane => lane.EnemyViewCount > 0));
            var initialPositions = lanes.Select(lane => lane.EnemyMarker.position).ToArray();
            yield return new WaitForSecondsRealtime(0.15f);

            for (var index = 0; index < lanes.Length; index++)
            {
                Assert.Greater(
                    Vector3.Distance(initialPositions[index], lanes[index].EnemyMarker.position),
                    0.01f,
                    $"Road marker {index} did not move.");
                AssertMarkerIsOnAuthoredRoute(lanes[index]);
            }
        }

        [UnityTest]
        public IEnumerator ClickingAnEmptyBattleCellClearsRangePreview()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = FindBootstrap();
            var recruit = bootstrap.Recruitment.TryRecruit();
            Assert.AreEqual(RecruitmentStatus.Success, recruit.Status);
            var basicCard = recruit.Batch.Cards.First(card => card.Kind == RecruitItemKind.BasicUnit);
            var battle = bootstrap.PlayerBoard.GetPositions(CellType.Battle);
            Assert.IsTrue(bootstrap.PlayerBoard.TryGetPosition(basicCard.RuntimeId, out var basicOrigin));
            Assert.IsTrue(bootstrap.PlayerBoard.TryMove(basicOrigin, battle[0]));
            bootstrap.BoardView.RefreshUnits();
            bootstrap.BoardView.SetUnitPresentation(
                basicCard.RuntimeId,
                "Test Unit",
                UnitRangeRules.GetRadius(BasicUnitArchetype.Axe),
                true);
            bootstrap.BoardView.SelectUnit(basicCard.RuntimeId);
            Assert.IsTrue(bootstrap.BoardView.RangePreview.enabled);

            var emptyCell = bootstrap.BoardView.GetCellView(battle[1]);
            Assert.IsNotNull(emptyCell);
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left
            };
            emptyCell.OnPointerClick(pointer);

            Assert.IsFalse(bootstrap.BoardView.RangePreview.enabled);
        }

        [UnityTest]
        public IEnumerator ClickingBoardBackgroundClearsRangePreviewWithoutAnotherUnit()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = FindBootstrap();
            var recruit = bootstrap.Recruitment.TryRecruit();
            Assert.AreEqual(RecruitmentStatus.Success, recruit.Status);
            var basicCard = recruit.Batch.Cards.First(card => card.Kind == RecruitItemKind.BasicUnit);
            var battle = bootstrap.PlayerBoard.GetPositions(CellType.Battle);
            Assert.IsTrue(bootstrap.PlayerBoard.TryGetPosition(basicCard.RuntimeId, out var basicOrigin));
            Assert.IsTrue(bootstrap.PlayerBoard.TryMove(basicOrigin, battle[0]));
            bootstrap.BoardView.RefreshUnits();
            bootstrap.BoardView.SetUnitPresentation(
                basicCard.RuntimeId,
                "Test Unit",
                UnitRangeRules.GetRadius(BasicUnitArchetype.Axe),
                true);
            bootstrap.BoardView.SelectUnit(basicCard.RuntimeId);
            Assert.IsTrue(bootstrap.BoardView.RangePreview.enabled);

            var receiver = FindScreen().FixedBoardCanvas.transform
                .Find("ART_FixedBoardCellLayer/BoardBackgroundClickSurface")
                ?.GetComponent<BoardBackgroundClickReceiver>();
            Assert.IsNotNull(receiver, "Fixed board must expose a transparent background click surface.");
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left
            };
            receiver.OnPointerClick(pointer);

            Assert.IsFalse(bootstrap.BoardView.RangePreview.enabled);
        }

        [UnityTest]
        public IEnumerator ClickingScreenEmptySpaceClearsRangePreviewWithoutAnotherUnit()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = FindBootstrap();
            var recruit = bootstrap.Recruitment.TryRecruit();
            Assert.AreEqual(RecruitmentStatus.Success, recruit.Status);
            var basicCard = recruit.Batch.Cards.First(card => card.Kind == RecruitItemKind.BasicUnit);
            var battle = bootstrap.PlayerBoard.GetPositions(CellType.Battle);
            Assert.IsTrue(bootstrap.PlayerBoard.TryGetPosition(basicCard.RuntimeId, out var basicOrigin));
            Assert.IsTrue(bootstrap.PlayerBoard.TryMove(basicOrigin, battle[0]));
            bootstrap.BoardView.RefreshUnits();
            bootstrap.BoardView.SetUnitPresentation(
                basicCard.RuntimeId,
                "Test Unit",
                UnitRangeRules.GetRadius(BasicUnitArchetype.Axe),
                true);
            bootstrap.BoardView.SelectUnit(basicCard.RuntimeId);
            Assert.IsTrue(bootstrap.BoardView.RangePreview.enabled);

            var receiver = FindScreen().transform.Find("RangeDismissSurface")
                ?.GetComponent<BoardBackgroundClickReceiver>();
            Assert.IsNotNull(receiver, "Screen must expose a bottom-layer empty-space click surface.");
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left
            };
            receiver.OnPointerClick(pointer);

            Assert.IsFalse(bootstrap.BoardView.RangePreview.enabled);
        }

        [UnityTest]
        public IEnumerator DeployedUnitCanReturnToBenchAndIsRemovedByNextRecruitment()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = FindBootstrap();
            var first = bootstrap.Recruitment.TryRecruit();
            Assert.AreEqual(RecruitmentStatus.Success, first.Status);
            var unitId = first.Batch.Cards
                .First(card => card.Kind == RecruitItemKind.BasicUnit)
                .RuntimeId;
            var battle = bootstrap.PlayerBoard.GetPositions(CellType.Battle)[0];
            Assert.IsTrue(bootstrap.PlayerBoard.TryGetPosition(unitId, out var basicOrigin));
            Assert.IsTrue(bootstrap.PlayerBoard.TryMove(basicOrigin, battle));
            bootstrap.BoardView.RefreshUnits();
            yield return null;

            Assert.IsTrue(bootstrap.BoardView.BeginDrag(unitId));
            Assert.IsTrue(bootstrap.RecruitDestination.IsCombatSuspended(unitId));
            var benchCell = bootstrap.BoardView.GetCellView(basicOrigin);
            var benchScreenPosition = RectTransformUtility.WorldToScreenPoint(
                null,
                benchCell.ContentAnchor.position);
            bootstrap.BoardView.CompleteDrag(unitId, benchScreenPosition);

            Assert.IsFalse(bootstrap.RecruitDestination.IsCombatSuspended(unitId));
            Assert.IsTrue(bootstrap.PlayerBoard.TryGetPosition(unitId, out var returnedPosition));
            Assert.AreEqual(basicOrigin, returnedPosition);
            bootstrap.Match.Player.AddResources(
                bootstrap.Recruitment.NextCost - bootstrap.Match.Player.Resources);
            var second = bootstrap.Recruitment.TryRecruit();
            Assert.AreEqual(RecruitmentStatus.Success, second.Status);
            CollectionAssert.Contains(second.RefreshedUnitIds, unitId);
            Assert.IsFalse(bootstrap.RecruitDestination.TryGetCard(unitId, out _));
            Assert.IsFalse(bootstrap.PlayerBoard.TryGetPosition(unitId, out _));
            Assert.AreEqual(5, bootstrap.RecruitDestination.CampCount);
            Assert.AreEqual(0, bootstrap.RecruitDestination.DeployedCount);
        }

        [UnityTest]
        public IEnumerator BenchShovelCanBeDraggedOntoLockedCellToUnlockIt()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = FindBootstrap();
            var shovelId = "playmode-shovel";
            var batch = new RecruitBatch(901, new List<RecruitCard>
            {
                new RecruitCard(shovelId, RecruitItemKind.Shovel, ShovelRecruitmentConfig.ShovelConfigId, string.Empty),
                new RecruitCard("playmode-basic-a", RecruitItemKind.BasicUnit, "axe", string.Empty),
                new RecruitCard("playmode-basic-b", RecruitItemKind.BasicUnit, "axe", string.Empty),
                new RecruitCard("playmode-basic-c", RecruitItemKind.BasicUnit, "axe", string.Empty),
                new RecruitCard("playmode-basic-d", RecruitItemKind.BasicUnit, "axe", string.Empty)
            });
            bootstrap.RecruitDestination.Commit(bootstrap.RecruitDestination.Plan(5), batch);
            bootstrap.BoardView.RefreshUnits();
            yield return null;

            var target = bootstrap.PlayerBoard.GetPositions(CellType.Locked)[0];
            Assert.IsTrue(bootstrap.RecruitDestination.TryGetCard(shovelId, out _));
            Assert.AreEqual(1, bootstrap.RecruitDestination.GetBenchShovelCount());
            Assert.AreEqual(CellType.Locked, GetCellType(bootstrap.PlayerBoard, target));

            var targetCell = bootstrap.BoardView.GetCellView(target);
            Assert.IsNotNull(targetCell);
            var targetScreenPosition = RectTransformUtility.WorldToScreenPoint(
                null,
                targetCell.ContentAnchor.position);

            Assert.IsTrue(bootstrap.BoardView.BeginDrag(shovelId));
            bootstrap.BoardView.CompleteDrag(shovelId, targetScreenPosition);
            yield return null;

            Assert.AreEqual(CellType.Battle, GetCellType(bootstrap.PlayerBoard, target));
            Assert.AreEqual(0, bootstrap.RecruitDestination.GetBenchShovelCount());
            Assert.IsFalse(bootstrap.RecruitDestination.TryGetCard(shovelId, out _));
        }

        [UnityTest]
        public IEnumerator RuntimeUnitCardsRemainInsideTheirAssignedCells()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = FindBootstrap();
            var recruitment = bootstrap.Recruitment.TryRecruit();
            Assert.AreEqual(RecruitmentStatus.Success, recruitment.Status);
            bootstrap.BoardView.RefreshUnits();
            bootstrap.AiBoardView.RefreshUnits();
            yield return null;

            Assert.AreEqual(
                5,
                bootstrap.BoardView.UnitLayer.GetComponentsInChildren<DraggableUnitView>(true).Length,
                "Player must show exactly the five recruited cards; there is no free initial unit.");
            var aiUnitViewCount = bootstrap.AiBoardView.UnitLayer.GetComponentsInChildren<DraggableUnitView>(true).Length;
            Assert.AreEqual(
                bootstrap.AiRecruitDestination.GetDeployedCards().Count,
                aiUnitViewCount,
                "AI view must show every deployed object controlled by the AI survival controller.");
            Assert.GreaterOrEqual(
                bootstrap.AiRecruitDestination.GetBoardCards()
                    .Count(card => card.Kind == RecruitItemKind.BasicUnit),
                2,
                "AI V0 must still expose basic units for the placement bounds check.");
            Assert.AreEqual(
                5 + aiUnitViewCount,
                Object.FindObjectsOfType<DraggableUnitView>().Length,
                "The two battlefield views must expose five player cards plus the current AI board objects.");
            AssertUnitCardsInsideCells(bootstrap.BoardView, bootstrap.PlayerBoard);
            AssertUnitCardsInsideCells(bootstrap.AiBoardView, bootstrap.AiBoard);
        }

        private static DragonBoundBootstrap FindBootstrap()
        {
            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            Assert.IsNotNull(bootstrap, "Greybox_Main must contain DragonBoundBootstrap.");
            Assert.IsNotNull(bootstrap.Recruitment);
            return bootstrap;
        }

        private static DragonBoundScreenView FindScreen()
        {
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            Assert.IsNotNull(screen);
            return screen;
        }

        private static void AssertBoardModel(BoardGrid board, BattlefieldLayoutDefinition layout)
        {
            Assert.IsNotNull(board);
            Assert.IsNotNull(layout);
            Assert.AreEqual(layout.InitialUnlockedCellCount, board.GetPositions(CellType.Battle).Count);
            Assert.AreEqual(
                layout.FormationCellCount - layout.InitialUnlockedCellCount,
                board.GetPositions(CellType.Locked).Count);
            Assert.AreEqual(layout.BenchCapacity, board.GetPositions(CellType.Bench).Count);
            Assert.AreEqual(layout.FormationCellCount + layout.BenchCapacity, board.CellCount);
        }

        private static HashSet<string> GetBenchOccupants(BoardGrid board)
        {
            var ids = new HashSet<string>();
            foreach (var position in board.GetPositions(CellType.Bench))
            {
                if (board.TryGetOccupant(position, out var runtimeId))
                {
                    ids.Add(runtimeId);
                }
            }

            return ids;
        }

        private static CellType GetCellType(BoardGrid board, GridPosition position)
        {
            Assert.IsTrue(board.TryGetCellType(position, out var type));
            return type;
        }

        private static DraggableUnitView FindUnitView(GreyboxBoardView boardView, string unitId)
        {
            return boardView.UnitLayer
                .GetComponentsInChildren<DraggableUnitView>(true)
                .SingleOrDefault(view => view.name == $"Card_{unitId}");
        }

        private static void AssertMarkerIsOnAuthoredRoute(GreyboxLaneView lane)
        {
            Assert.IsNotNull(lane);
            Assert.IsNotNull(lane.EnemyMarker);
            Assert.GreaterOrEqual(lane.WaypointCount, 2);

            var marker = lane.EnemyMarker.position;
            var nearestDistance = float.MaxValue;
            for (var index = 0; index < lane.WaypointCount - 1; index++)
            {
                nearestDistance = Mathf.Min(
                    nearestDistance,
                    DistanceToSegment(marker, lane.Waypoints[index].position, lane.Waypoints[index + 1].position));
            }

            Assert.Less(nearestDistance, 0.1f, "Enemy marker left its authored road polyline.");
        }

        private static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            var segment = end - start;
            if (segment.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector3.Distance(point, start);
            }

            var t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / segment.sqrMagnitude);
            return Vector3.Distance(point, start + (segment * t));
        }

        private static void AssertUnitCardsInsideCells(GreyboxBoardView boardView, BoardGrid board)
        {
            var renderedOccupants = board.GetOccupants()
                .Where(occupant => boardView.GetCellView(occupant.Position) != null)
                .ToArray();
            var cards = boardView.UnitLayer.GetComponentsInChildren<DraggableUnitView>(true);
            Assert.AreEqual(renderedOccupants.Length, cards.Length);

            foreach (var occupant in renderedOccupants)
            {
                var card = cards.Single(view => view.name == $"Card_{occupant.UnitId}");
                var cell = boardView.GetCellView(occupant.Position);
                Assert.AreSame(boardView.UnitLayer, card.RectTransform.parent);
                Assert.AreEqual(card.RectTransform.anchorMin, card.RectTransform.anchorMax);
                AssertRectIsInside(card.RectTransform, cell.RectTransform, occupant.UnitId);
            }
        }

        private static void AssertRectIsInside(RectTransform inner, RectTransform outer, string unitId)
        {
            var innerCorners = new Vector3[4];
            var outerCorners = new Vector3[4];
            inner.GetWorldCorners(innerCorners);
            outer.GetWorldCorners(outerCorners);

            const float tolerance = 0.1f;
            Assert.GreaterOrEqual(innerCorners.Min(point => point.x), outerCorners.Min(point => point.x) - tolerance, unitId);
            Assert.LessOrEqual(innerCorners.Max(point => point.x), outerCorners.Max(point => point.x) + tolerance, unitId);
            Assert.GreaterOrEqual(innerCorners.Min(point => point.y), outerCorners.Min(point => point.y) - tolerance, unitId);
            Assert.LessOrEqual(innerCorners.Max(point => point.y), outerCorners.Max(point => point.y) + tolerance, unitId);
        }
    }
}
