using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DragonBound.Bootstrap;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Presentation;
using DragonBound.Recruitment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DragonBound.Tests.PlayMode
{
    public sealed class BootstrapPlayModeTests
    {
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
            Assert.AreEqual(0, bootstrap.Match.CurrentWave);
            Assert.AreEqual(20, bootstrap.Match.Player.Resources);
            Assert.AreEqual(10, bootstrap.Match.AI.Resources);
            Assert.AreEqual(0, bootstrap.Match.Player.RemainingEnemyCount);
            Assert.AreEqual(0, bootstrap.Match.AI.RemainingEnemyCount);
            Assert.IsFalse(bootstrap.EnableHeroComponents);
            Assert.IsFalse(bootstrap.Recruitment.HasLastAttempt);
            Assert.AreEqual(1, bootstrap.AiRecruitment.CompletedRecruitments);
            Assert.AreEqual(4, bootstrap.AiRecruitDestination.TotalObjectCount);
            Assert.AreEqual(2, bootstrap.AiRecruitDestination.CampCount);
            Assert.AreEqual(2, bootstrap.AiRecruitDestination.DeployedCount);
            Assert.IsTrue(bootstrap.AiRecruitDestination.GetDeployedCards().Any(card => card.Level == 2));

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
            Assert.AreEqual(ThreeWaveSliceRuntime.Wave1DurationSeconds, bootstrap.ThreeWave.WaveDurationSeconds);
            Assert.LessOrEqual(bootstrap.ThreeWave.WaveRemainingSeconds, ThreeWaveSliceRuntime.Wave1DurationSeconds);
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
            Assert.IsTrue(first.Batch.Cards.All(card => card.Kind == RecruitItemKind.BasicUnit));
            var firstIds = GetBenchOccupants(bootstrap.PlayerBoard);
            Assert.AreEqual(RecruitmentService.CardsPerRecruitment, firstIds.Count);
            Assert.AreEqual(RecruitmentService.CardsPerRecruitment, bootstrap.RecruitDestination.TotalObjectCount);
            Assert.AreEqual(RecruitDestinationPlan.RefreshBench, bootstrap.Recruitment.NextDestinationPlan);

            bootstrap.BoardView.RefreshUnits();
            yield return null;
            Assert.AreEqual(
                "REFRESH 5 LEFT\nCOST 12",
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
            var bench = bootstrap.PlayerBoard.GetPositions(CellType.Bench);
            var battle = bootstrap.PlayerBoard.GetPositions(CellType.Battle)[0];
            Assert.IsTrue(bootstrap.PlayerBoard.TryMove(bench[0], battle));
            playerView.RefreshUnits();
            playerView.SetUnitPresentation(
                recruit.Batch.Cards[0].RuntimeId,
                "AXE 1",
                UnitRangeRules.GetRadius(BasicUnitArchetype.Axe),
                true);
            playerView.SelectUnit(recruit.Batch.Cards[0].RuntimeId);
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

            Assert.IsTrue(bootstrap.PlayerBoard.TryGetPosition(recruit.Batch.Cards[0].RuntimeId, out var axePosition));
            var axeCell = playerView.GetCellView(axePosition);
            Assert.IsNotNull(axeCell);
            var cellSize = Mathf.Min(axeCell.RectTransform.rect.width, axeCell.RectTransform.rect.height);
            var expectedDiameter = cellSize * UnitRangeRules.GetRadius(BasicUnitArchetype.Axe) * 2f;
            Assert.AreEqual(expectedDiameter, range.rectTransform.sizeDelta.x, 0.01f);
            Assert.Less(Vector3.Distance(range.rectTransform.position, axeCell.ContentAnchor.position), 0.1f);
            Assert.IsTrue(playerView.BeginDrag(recruit.Batch.Cards[0].RuntimeId));
            Assert.IsTrue(bootstrap.RecruitDestination.IsCombatSuspended(recruit.Batch.Cards[0].RuntimeId));
            Assert.IsFalse(range.enabled, "Range must hide as soon as dragging starts.");
            playerView.CompleteDrag(recruit.Batch.Cards[0].RuntimeId, new Vector2(-1000f, -1000f));
            Assert.IsFalse(bootstrap.RecruitDestination.IsCombatSuspended(recruit.Batch.Cards[0].RuntimeId));
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
        public IEnumerator DeployedUnitCanReturnToBenchAndIsRemovedByNextRecruitment()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = FindBootstrap();
            var first = bootstrap.Recruitment.TryRecruit();
            Assert.AreEqual(RecruitmentStatus.Success, first.Status);
            var unitId = first.Batch.Cards[0].RuntimeId;
            var bench = bootstrap.PlayerBoard.GetPositions(CellType.Bench);
            var battle = bootstrap.PlayerBoard.GetPositions(CellType.Battle)[0];
            Assert.IsTrue(bootstrap.PlayerBoard.TryMove(bench[0], battle));
            bootstrap.BoardView.RefreshUnits();
            yield return null;

            Assert.IsTrue(bootstrap.BoardView.BeginDrag(unitId));
            Assert.IsTrue(bootstrap.RecruitDestination.IsCombatSuspended(unitId));
            var benchCell = bootstrap.BoardView.GetCellView(bench[0]);
            var benchScreenPosition = RectTransformUtility.WorldToScreenPoint(
                null,
                benchCell.ContentAnchor.position);
            bootstrap.BoardView.CompleteDrag(unitId, benchScreenPosition);

            Assert.IsFalse(bootstrap.RecruitDestination.IsCombatSuspended(unitId));
            Assert.IsTrue(bootstrap.PlayerBoard.TryGetPosition(unitId, out var returnedPosition));
            Assert.AreEqual(bench[0], returnedPosition);
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
        public IEnumerator RuntimeUnitCardsRemainInsideTheirAssignedCells()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = FindBootstrap();
            var recruitment = bootstrap.Recruitment.TryRecruit();
            Assert.AreEqual(RecruitmentStatus.Success, recruitment.Status);
            bootstrap.BoardView.RefreshUnits();
            yield return null;

            Assert.AreEqual(
                5,
                bootstrap.BoardView.UnitLayer.GetComponentsInChildren<DraggableUnitView>(true).Length,
                "Player must show exactly the five recruited cards; there is no free initial unit.");
            Assert.AreEqual(
                2,
                bootstrap.AiBoardView.UnitLayer.GetComponentsInChildren<DraggableUnitView>(true).Length,
                "AI must show the two deployed units remaining after its deterministic opening merge.");
            Assert.AreEqual(
                7,
                Object.FindObjectsOfType<DraggableUnitView>().Length,
                "The two battlefield views must expose five player cards and two deployed AI cards.");
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
