using System.Collections;
using System.Linq;
using DragonBound.Bootstrap;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Presentation;
using DragonBound.Recruitment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DragonBound.Tests.PlayMode
{
    public sealed class HeroSlicePlayModeTests
    {
        [UnityTest]
        public IEnumerator TapOnlySelectsUnit()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var batch = bootstrap.Recruitment.TryRecruit().Batch;
            var basic = batch.Cards.First(card => card.Kind == RecruitItemKind.BasicUnit);
            MoveDirect(bootstrap, basic.RuntimeId, bootstrap.PlayerBoard.GetPositions(CellType.Battle)[0]);
            bootstrap.BoardView.RefreshUnits();
            yield return null;

            var view = FindCard(bootstrap.BoardView, basic.RuntimeId);
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, view.RectTransform.position)
            };
            view.OnPointerDown(pointer);
            view.OnPointerUp(pointer);

            Assert.IsTrue(bootstrap.BoardView.RangePreview.enabled);
            Assert.IsFalse(bootstrap.BoardView.Drag.IsDragging);
            Assert.IsFalse(bootstrap.BoardView.HasDragGhost);
        }

        [UnityTest]
        public IEnumerator UnpairedHeroComponentNeverShowsRangePreview()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var batch = bootstrap.Recruitment.TryRecruit().Batch;
            var component = batch.Cards.First(card => card.Kind == RecruitItemKind.HeroComponent);
            MoveDirect(bootstrap, component.RuntimeId, bootstrap.PlayerBoard.GetPositions(CellType.Battle)[0]);
            bootstrap.BoardView.RefreshUnits();
            yield return null;

            bootstrap.BoardView.SelectUnit(component.RuntimeId);
            Assert.IsFalse(bootstrap.BoardView.RangePreview.enabled);

            Assert.IsTrue(bootstrap.BoardView.BeginDrag(component.RuntimeId));
            bootstrap.BoardView.UpdateDraggedUnit(component.RuntimeId, new Vector2(120f, 160f));
            Assert.IsFalse(bootstrap.BoardView.RangePreview.enabled);
            bootstrap.BoardView.CancelActiveDrag();
            Assert.IsFalse(bootstrap.BoardView.RangePreview.enabled);
        }

        [UnityTest]
        public IEnumerator DragArrowKeepsRuntimeAndOriginalViewInFixedSlotUntilPointerUp()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var batch = bootstrap.Recruitment.TryRecruit().Batch;
            var basic = batch.Cards.First(card => card.Kind == RecruitItemKind.BasicUnit);
            var battle = bootstrap.PlayerBoard.GetPositions(CellType.Battle)[0];
            var target = bootstrap.PlayerBoard.GetPositions(CellType.Battle)[1];
            MoveDirect(bootstrap, basic.RuntimeId, battle);
            bootstrap.BoardView.RefreshUnits();
            yield return null;

            var view = FindCard(bootstrap.BoardView, basic.RuntimeId);
            var targetCell = bootstrap.BoardView.CellViews.Single(cell => cell.Position == target);
            var targetScreenPosition = RectTransformUtility.WorldToScreenPoint(null, targetCell.ContentAnchor.position);
            var originalVisualPosition = view.RectTransform.anchoredPosition;
            Assert.IsTrue(bootstrap.BoardView.BeginDrag(basic.RuntimeId));
            Assert.IsFalse(bootstrap.BoardView.HasDragGhost);
            Assert.IsFalse(bootstrap.BoardView.IsDragGhostVisible);
            Assert.IsTrue(bootstrap.BoardView.HasDragArrowPreview);
            Assert.IsTrue(bootstrap.RecruitDestination.IsCombatSuspended(basic.RuntimeId));

            bootstrap.BoardView.UpdateDraggedUnit(basic.RuntimeId, targetScreenPosition);
            Assert.AreEqual(originalVisualPosition, view.RectTransform.anchoredPosition);
            Assert.IsTrue(bootstrap.PlayerBoard.TryGetPosition(basic.RuntimeId, out var duringDrag));
            Assert.AreEqual(battle, duringDrag);
            Assert.IsTrue(bootstrap.BoardView.IsDragArrowVisible);

            bootstrap.BoardView.CompleteDrag(basic.RuntimeId, targetScreenPosition);
            Assert.IsFalse(bootstrap.BoardView.HasDragGhost);
            Assert.IsFalse(bootstrap.BoardView.IsDragArrowVisible);
            Assert.IsTrue(bootstrap.PlayerBoard.TryGetPosition(basic.RuntimeId, out var afterDrop));
            Assert.AreEqual(target, afterDrop);
        }

        [UnityTest]
        public IEnumerator DragPreviewDoesNotRenderUnitBeforeDrop()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var batch = bootstrap.Recruitment.TryRecruit().Batch;
            var basic = batch.Cards.First(card => card.Kind == RecruitItemKind.BasicUnit);
            var battle = bootstrap.PlayerBoard.GetPositions(CellType.Battle)[0];
            MoveDirect(bootstrap, basic.RuntimeId, battle);
            bootstrap.BoardView.RefreshUnits();
            yield return null;

            Assert.IsTrue(bootstrap.BoardView.BeginDrag(basic.RuntimeId));
            bootstrap.BoardView.UpdateDraggedUnit(basic.RuntimeId, new Vector2(120f, 160f));
            Assert.IsFalse(bootstrap.BoardView.IsDragGhostVisible);

            bootstrap.BoardView.UpdateDraggedUnit(basic.RuntimeId, new Vector2(-10000f, -10000f));
            Assert.IsFalse(bootstrap.BoardView.IsDragGhostVisible);
            bootstrap.BoardView.CompleteDrag(basic.RuntimeId, new Vector2(-10000f, -10000f));
        }

        [UnityTest]
        public IEnumerator InterruptedDragCancelsTransactionAndResumesCombat()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var batch = bootstrap.Recruitment.TryRecruit().Batch;
            var basic = batch.Cards.First(card => card.Kind == RecruitItemKind.BasicUnit);
            var battle = bootstrap.PlayerBoard.GetPositions(CellType.Battle)[0];
            MoveDirect(bootstrap, basic.RuntimeId, battle);
            bootstrap.BoardView.RefreshUnits();

            Assert.IsTrue(bootstrap.BoardView.BeginDrag(basic.RuntimeId));
            Assert.IsTrue(bootstrap.RecruitDestination.IsCombatSuspended(basic.RuntimeId));
            bootstrap.BoardView.CancelActiveDrag();

            Assert.IsFalse(bootstrap.BoardView.Drag.IsDragging);
            Assert.IsFalse(bootstrap.BoardView.HasDragGhost);
            Assert.IsFalse(bootstrap.RecruitDestination.IsCombatSuspended(basic.RuntimeId));
            Assert.IsTrue(bootstrap.PlayerBoard.TryGetPosition(basic.RuntimeId, out var restored));
            Assert.AreEqual(battle, restored);
        }

        [UnityTest]
        public IEnumerator HeroSliceSceneUsesIndependentEnabledConfiguration()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap.EnableHeroComponents);
            Assert.IsTrue(bootstrap.HeroSliceMode);
            Assert.GreaterOrEqual(bootstrap.HeroSliceStartingResources, 200);
            Assert.AreEqual(bootstrap.HeroSliceStartingResources, bootstrap.Match.Player.Resources);
            Assert.AreEqual(
                bootstrap.HeroSliceStartingResources -
                RecruitmentPrice.GetCost(1) -
                RecruitmentPrice.GetCost(2) -
                RecruitmentPrice.GetCost(3),
                bootstrap.Match.AI.Resources);
            Assert.AreEqual(3, bootstrap.AiRecruitment.CompletedRecruitments);
            Assert.AreEqual(0, bootstrap.AiRecruitment.RemainingHeroComponents);
            Assert.AreEqual(2, bootstrap.AiRecruitDestination.ActivePairLinkCount);
            Assert.IsTrue(bootstrap.AiRecruitDestination.HasActiveHero(HeroSliceCatalog.WindclawRangerHeroId));
            Assert.IsTrue(bootstrap.AiRecruitDestination.HasActiveHero(HeroSliceCatalog.DragonRiderHeroId));
            Assert.IsTrue(bootstrap.AiRecruitDestination.GetActiveHeroPairs()
                .All(pair => pair.PairLink.CombatProxy != null));
            Assert.AreEqual(
                4,
                bootstrap.AiRecruitDestination.GetDeployedCards()
                    .Count(card => card.Kind == RecruitItemKind.HeroComponent));
        }

        [UnityTest]
        public IEnumerator DamageTargetPositionFallsBackToLastKnownLaneCoordinate()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            var lane = screen.PlayerBattlefieldView.LaneView;
            var enemy = new EnemyRuntime("test.damage.target", TeamSide.Player);
            enemy.SetTargetingState(4, 0.5f, new CombatPoint(0f, 0f));
            Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Register(enemy));

            Assert.IsTrue(lane.TryGetEnemyPosition(enemy.RuntimeId, out var expectedPosition));
            Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Remove(enemy.RuntimeId, out _));

            Assert.IsTrue(lane.TryGetEnemyPosition(enemy.RuntimeId, out var fallbackPosition));
            Assert.Less(Vector3.Distance(expectedPosition, fallbackPosition), 0.01f);
        }

        [UnityTest]
        public IEnumerator DamageNumberRemainsVisibleAtTheEnemyPosition()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            var lane = screen.PlayerBattlefieldView.LaneView;
            var enemy = new EnemyRuntime("test.damage.label", TeamSide.Player);
            enemy.SetTargetingState(4, 0.5f, new CombatPoint(0f, 0f));
            Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Register(enemy));
            Assert.IsTrue(lane.TryGetEnemyPosition(enemy.RuntimeId, out var targetPosition));

            var combatFx = screen.PlayerBattlefieldView.GetComponent<CombatFxView>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var handler = typeof(CombatFxView).GetMethod(
                "OnCombat",
                flags);
            Assert.IsNotNull(handler);
            handler.Invoke(
                combatFx,
                new object[]
                {
                    new CombatEvent(
                        TeamSide.Player,
                        AttackKind.Single,
                        string.Empty,
                        enemy.RuntimeId,
                        10f,
                        false,
                        false,
                        bootstrap.Match.Player.Resources)
                });
            yield return null;

            var label = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Text>(true)
                .Single(text => text.gameObject.activeInHierarchy && text.text == "-10");
            var expectedLabelPosition = targetPosition +
                (Vector3.up * combatFx.DamageNumberVerticalOffsetPixels);
            Assert.Less(Vector3.Distance(label.rectTransform.position, expectedLabelPosition), 0.01f);

            bootstrap.Match.TryTransition(MatchState.Running);
            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(label != null && label.gameObject.activeInHierarchy);
        }

        [UnityTest]
        public IEnumerator PlayerPairLinksKeepTwoComponentEntitiesAndPersistentEditableProxy()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var first = bootstrap.Recruitment.TryRecruit();
            var firstSigil = first.Batch.Cards.Single(card =>
                card.ConfigId == HeroSliceCatalog.DragonSigilComponentId);
            bootstrap.BoardView.RefreshUnits();
            yield return null;
            var sigilView = FindCard(bootstrap.BoardView, firstSigil.RuntimeId);
            Assert.IsNotNull(sigilView);
            Assert.AreEqual("Contract Dragonling", sigilView.GetComponentInChildren<Text>(true).text);
            MoveDirect(bootstrap, firstSigil.RuntimeId, new GridPosition(2, 1));

            var second = bootstrap.Recruitment.TryRecruit();
            var sky = second.Batch.Cards.Single(card =>
                card.ConfigId == HeroSliceCatalog.SkyRangerComponentId);
            var secondSigil = second.Batch.Cards.Single(card =>
                card.ConfigId == HeroSliceCatalog.DragonSigilComponentId);
            Assert.AreEqual(DragDropStatus.Moved, Drag(bootstrap, sky.RuntimeId, new GridPosition(2, 2)));
            var windclaw = bootstrap.RecruitDestination.GetActiveHeroPairs().Single();
            Assert.IsFalse(windclaw.PairLink.CombatProxy.IsFormationComplete);
            Assert.IsTrue(bootstrap.RecruitDestination.TryGetCard(firstSigil.RuntimeId, out _));
            Assert.IsTrue(bootstrap.RecruitDestination.TryGetCard(sky.RuntimeId, out _));
            MoveDirect(bootstrap, secondSigil.RuntimeId, new GridPosition(3, 1));

            var third = bootstrap.Recruitment.TryRecruit();
            var knight = third.Batch.Cards.Single(card =>
                card.ConfigId == HeroSliceCatalog.DragonKnightComponentId);
            Assert.AreEqual(DragDropStatus.Moved, Drag(bootstrap, knight.RuntimeId, new GridPosition(3, 2)));

            Assert.AreEqual(2, bootstrap.RecruitDestination.ActivePairLinkCount);
            foreach (var activePair in bootstrap.RecruitDestination.GetActiveHeroPairs())
            {
                Assert.IsTrue(bootstrap.PlayerBoard.TryGetOccupant(
                    activePair.ComponentA.CurrentCell,
                    out var firstRuntimeId));
                Assert.IsTrue(bootstrap.PlayerBoard.TryGetOccupant(
                    activePair.ComponentB.CurrentCell,
                    out var secondRuntimeId));
                Assert.AreEqual(activePair.ComponentA.ComponentId, firstRuntimeId);
                Assert.AreEqual(activePair.ComponentB.ComponentId, secondRuntimeId);
                Assert.AreNotEqual(firstRuntimeId, secondRuntimeId);
                Assert.IsFalse(bootstrap.PlayerBoard.TryGetPosition(activePair.PairLink.PairLinkId, out _));
            }

            bootstrap.BoardView.RefreshUnits();
            yield return null;
            foreach (var activePair in bootstrap.RecruitDestination.GetActiveHeroPairs())
            {
                Assert.IsTrue(FindCard(bootstrap.BoardView, activePair.ComponentA.ComponentId)
                    .IsPairedPresentationHidden);
                Assert.IsTrue(FindCard(bootstrap.BoardView, activePair.ComponentB.ComponentId)
                    .IsPairedPresentationHidden);
            }

            Assert.AreEqual(
                2,
                bootstrap.BoardView.UnitLayer.GetComponentsInChildren<HeroFormationView>(true).Length);

            while (bootstrap.Match.State == MatchState.Ready)
            {
                yield return null;
            }

            yield return new WaitForSecondsRealtime(0.7f);
            Assert.IsTrue(bootstrap.RecruitDestination.GetActiveHeroPairs()
                .All(pair => pair.PairLink.CombatProxy.IsFormationComplete));
            windclaw = bootstrap.RecruitDestination.GetActiveHeroPairs()
                .Single(pair => pair.PairLink.RecipeId == HeroSliceCatalog.WindclawRangerRecipeId);
            bootstrap.BoardView.SelectUnit(windclaw.ComponentA.ComponentId);
            Assert.IsTrue(bootstrap.BoardView.RangePreview.enabled);
            var primaryCell = bootstrap.BoardView.GetCellView(windclaw.ComponentA.CurrentCell);
            var secondaryCell = bootstrap.BoardView.GetCellView(windclaw.ComponentB.CurrentCell);
            var expectedCenter = (primaryCell.ContentAnchor.position + secondaryCell.ContentAnchor.position) * 0.5f;
            Assert.Less(
                Vector3.Distance(expectedCenter, bootstrap.BoardView.RangePreview.rectTransform.position),
                0.1f);
        }

        [UnityTest]
        public IEnumerator DraggingOnePairedComponentRestoresBothViewsAndMovesOnlySelectedComponent()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var first = bootstrap.Recruitment.TryRecruit();
            var sigil = first.Batch.Cards.Single(card =>
                card.ConfigId == HeroSliceCatalog.DragonSigilComponentId);
            MoveDirect(bootstrap, sigil.RuntimeId, new GridPosition(2, 1));
            var second = bootstrap.Recruitment.TryRecruit();
            var sky = second.Batch.Cards.Single(card =>
                card.ConfigId == HeroSliceCatalog.SkyRangerComponentId);
            Assert.AreEqual(DragDropStatus.Moved, Drag(bootstrap, sky.RuntimeId, new GridPosition(2, 2)));
            bootstrap.BoardView.RefreshUnits();
            yield return null;

            var drag = new DragPlacementController(
                bootstrap.PlayerBoard,
                bootstrap.RecruitDestination,
                true);
            Assert.IsTrue(drag.BeginDrag(sigil.RuntimeId));
            Assert.AreEqual(0, bootstrap.RecruitDestination.ActivePairLinkCount);
            Assert.IsFalse(FindCard(bootstrap.BoardView, sigil.RuntimeId).IsPairedPresentationHidden);
            Assert.IsFalse(FindCard(bootstrap.BoardView, sky.RuntimeId).IsPairedPresentationHidden);
            Assert.AreEqual(DragDropStatus.Moved, drag.Drop(new GridPosition(3, 2)));

            Assert.IsTrue(bootstrap.PlayerBoard.TryGetPosition(sigil.RuntimeId, out var moved));
            Assert.IsTrue(bootstrap.PlayerBoard.TryGetPosition(sky.RuntimeId, out var partner));
            Assert.AreEqual(new GridPosition(3, 2), moved);
            Assert.AreEqual(new GridPosition(2, 2), partner);
        }

        [UnityTest]
        public IEnumerator HeroSliceUsesHeroSkillShowcaseEnemyDurability()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            Assert.IsTrue(bootstrap.HeroSliceMode);
            Assert.AreEqual(
                ThreeWaveEnemyDurabilityProfile.HeroSkillShowcase,
                bootstrap.ThreeWave.DurabilityProfile);
        }

        private static void MoveDirect(DragonBoundBootstrap bootstrap, string runtimeId, GridPosition target)
        {
            Assert.IsTrue(bootstrap.PlayerBoard.TryGetPosition(runtimeId, out var origin));
            Assert.IsTrue(bootstrap.PlayerBoard.TryMove(origin, target));
            bootstrap.RecruitDestination.TryResolvePostDrop(runtimeId);
        }

        private static DragDropStatus Drag(
            DragonBoundBootstrap bootstrap,
            string runtimeId,
            GridPosition target)
        {
            var drag = new DragPlacementController(
                bootstrap.PlayerBoard,
                bootstrap.RecruitDestination,
                true);
            Assert.IsTrue(drag.BeginDrag(runtimeId));
            return drag.Drop(target);
        }

        private static DraggableUnitView FindCard(GreyboxBoardView board, string runtimeId)
        {
            return board.UnitLayer.GetComponentsInChildren<DraggableUnitView>(true)
                .SingleOrDefault(view => view.name == $"Card_{runtimeId}");
        }
    }
}
