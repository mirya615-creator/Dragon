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
        public IEnumerator HeroLevelUpVfxUsesAuthoredPlacementAndPlaybackSpeed()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var template = bootstrap.BoardView.HeroFormationEffectPrefab;
            var instance = Object.Instantiate(template, bootstrap.BoardView.UnitLayer);

            Assert.IsTrue(instance.PlayLevelUpAnimation());
            Assert.IsTrue(instance.IsLevelUpVfxVisible);
            Assert.IsNotNull(instance.LevelUpVfxImage);
            Assert.IsNotNull(instance.LevelUpVfxAnimator);
            Assert.AreEqual(new Vector2(150f, 100f), instance.LevelUpVfxImage.rectTransform.sizeDelta);
            Assert.AreEqual(new Vector2(3.4f, -13f), instance.LevelUpVfxImage.rectTransform.anchoredPosition);
            Assert.AreEqual(0.1f, instance.LevelUpPlaybackSpeed, 0.001f);
            Assert.AreEqual(1f, instance.LevelUpVfxAnimator.speed, 0.001f);
            Assert.IsFalse(instance.LevelUpVfxImage.preserveAspect);
            Assert.IsFalse(instance.LevelUpVfxImage.raycastTarget);

            instance.SetHeroLevel(3);
            Assert.AreEqual("LV 3", instance.HeroNameLabel.text);

            Object.Destroy(instance.gameObject);
        }

        [UnityTest]
        public IEnumerator HeroSynthesisVfxUsesRarityControllerAndHalfSpeed()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var template = bootstrap.BoardView.HeroFormationEffectPrefab;
            var instance = Object.Instantiate(template, bootstrap.BoardView.UnitLayer);

            Assert.IsTrue(instance.PlaySynthesisAnimation(HeroRecipeRarity.Purple));
            Assert.IsTrue(instance.IsSynthesisVfxVisible);
            Assert.AreEqual("synthesisP", instance.SynthesisVfxAnimator.runtimeAnimatorController.name);
            Assert.AreEqual(0.5f, instance.SynthesisPlaybackSpeed, 0.001f);
            Assert.AreEqual(1f, instance.SynthesisVfxAnimator.speed, 0.001f);
            Assert.IsTrue(instance.SynthesisVfxImage.preserveAspect);
            Assert.IsFalse(instance.SynthesisVfxImage.raycastTarget);

            Assert.IsTrue(instance.PlaySynthesisAnimation(HeroRecipeRarity.Gold));
            Assert.AreEqual("synthesisG", instance.SynthesisVfxAnimator.runtimeAnimatorController.name);

            yield return new WaitForSecondsRealtime(0.13f);
            Assert.IsTrue(instance.HeroAttackAnimator.gameObject.activeSelf);
            var revealGroup = instance.HeroAttackAnimator.GetComponent<CanvasGroup>();
            Assert.IsNotNull(revealGroup);
            Assert.Greater(revealGroup.alpha, 0f);
            Assert.Less(revealGroup.alpha, 1f);

            yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreEqual(1f, revealGroup.alpha, 0.001f);
            Assert.IsFalse(instance.IsSynthesisVfxVisible);

            Object.Destroy(instance.gameObject);
        }

        [UnityTest]
        public IEnumerator FrostcrownHunterMarkFollowsEnemyViewAndDisappearsWhenCleared()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            var enemy = new EnemyRuntime("test.frostcrown.mark", TeamSide.Player);
            enemy.SetTargetingState(4, 0.5f, new CombatPoint(0f, 0f));
            Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Register(enemy));
            yield return null;

            var lane = screen.PlayerBattlefieldView.LaneView;
            lane.SetFrostcrownMarkedEnemies(new[] { enemy.RuntimeId });
            var enemyView = screen.PlayerBattlefieldView
                .GetComponentsInChildren<EnemyView>(true)
                .Single(view => view.RuntimeId == enemy.RuntimeId);
            Assert.AreEqual(
                enemyView.transform.FindUi("ART_EnemyAnimation/Image").position,
                enemyView.VisualImpactPosition,
                "Projectile impacts must use the authored enemy-art centre instead of the card root.");
            Assert.IsTrue(lane.TryGetEnemyVisualImpactPosition(enemy.RuntimeId, out var impactPosition));
            Assert.AreEqual(enemyView.VisualImpactPosition, impactPosition);
            Assert.IsTrue(enemyView.IsFrostcrownMarked);
            Assert.IsNotNull(enemyView.FrostcrownMarkImage);
            Assert.AreSame(
                DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Frostcrown Hunter/road"),
                enemyView.FrostcrownMarkImage.sprite);
            Assert.AreEqual(
                new Vector2(22f, 33f),
                enemyView.FrostcrownMarkImage.rectTransform.sizeDelta);
            Assert.AreEqual(
                new Vector2(0f, 58f),
                enemyView.FrostcrownMarkImage.rectTransform.anchoredPosition);
            Assert.IsTrue(enemyView.FrostcrownMarkImage.preserveAspect);
            Assert.IsFalse(enemyView.FrostcrownMarkImage.raycastTarget);

            lane.SetFrostcrownMarkedEnemies(null);
            Assert.IsFalse(enemyView.IsFrostcrownMarked);
        }

        [UnityTest]
        public IEnumerator FrostMireMarkAppearsAboveHealthBarAndSharesTheRowWithFrostcrown()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            var enemy = new EnemyRuntime("test.frost.mire.mark", TeamSide.Player);
            enemy.SetTargetingState(4, 0.5f, new CombatPoint(0f, 0f));
            Assert.IsTrue(enemy.ApplyFrostMireSlow(0.1f, 10f));
            Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Register(enemy));
            yield return null;

            var lane = screen.PlayerBattlefieldView.LaneView;
            var enemyView = screen.PlayerBattlefieldView
                .GetComponentsInChildren<EnemyView>(true)
                .Single(view => view.RuntimeId == enemy.RuntimeId);
            Assert.IsTrue(enemyView.IsFrostMireMarked);
            Assert.AreSame(
                DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Item/微信图片_20260909164612_605_101"),
                enemyView.FrostMireMarkImage.sprite);
            Assert.AreEqual(new Vector2(22f, 33f), enemyView.FrostMireMarkImage.rectTransform.sizeDelta);
            Assert.AreEqual(new Vector2(0f, 58f), enemyView.FrostMireMarkImage.rectTransform.anchoredPosition);

            lane.SetFrostcrownMarkedEnemies(new[] { enemy.RuntimeId });
            Assert.AreEqual(new Vector2(-12f, 58f), enemyView.FrostcrownMarkImage.rectTransform.anchoredPosition);
            Assert.AreEqual(new Vector2(12f, 58f), enemyView.FrostMireMarkImage.rectTransform.anchoredPosition);

            lane.SetFrostcrownMarkedEnemies(null);
            Assert.AreEqual(new Vector2(0f, 58f), enemyView.FrostMireMarkImage.rectTransform.anchoredPosition);

            enemy.TickControl(11f);
            yield return null;
            Assert.IsFalse(enemyView.IsFrostMireMarked);
        }

        [UnityTest]
        public IEnumerator WinterveilMarkExpiresIndependentlyAndThreeMarksStayCentered()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            var enemy = new EnemyRuntime("test.winterveil.mark", TeamSide.Player);
            enemy.SetTargetingState(4, 0.5f, new CombatPoint(0f, 0f));
            Assert.IsTrue(enemy.ApplyFrostMireSlow(0.1f, 10f));
            Assert.IsTrue(enemy.ApplyWinterveilSlow(0.1f, 5f));
            Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Register(enemy));
            yield return null;

            var lane = screen.PlayerBattlefieldView.LaneView;
            var enemyView = screen.PlayerBattlefieldView
                .GetComponentsInChildren<EnemyView>(true)
                .Single(view => view.RuntimeId == enemy.RuntimeId);
            Assert.IsTrue(enemyView.IsWinterveilMarked);
            Assert.AreSame(
                DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Item/微信图片_20260909164615_606_101"),
                enemyView.WinterveilMarkImage.sprite);
            Assert.AreEqual(new Vector2(22f, 33f), enemyView.WinterveilMarkImage.rectTransform.sizeDelta);
            Assert.AreEqual(new Vector2(-12f, 58f), enemyView.FrostMireMarkImage.rectTransform.anchoredPosition);
            Assert.AreEqual(new Vector2(12f, 58f), enemyView.WinterveilMarkImage.rectTransform.anchoredPosition);

            lane.SetFrostcrownMarkedEnemies(new[] { enemy.RuntimeId });
            Assert.AreEqual(new Vector2(-24f, 58f), enemyView.FrostcrownMarkImage.rectTransform.anchoredPosition);
            Assert.AreEqual(new Vector2(0f, 58f), enemyView.FrostMireMarkImage.rectTransform.anchoredPosition);
            Assert.AreEqual(new Vector2(24f, 58f), enemyView.WinterveilMarkImage.rectTransform.anchoredPosition);

            enemy.TickControl(6f);
            yield return null;
            Assert.IsFalse(enemyView.IsWinterveilMarked);
            Assert.IsTrue(enemyView.IsFrostMireMarked);
            Assert.AreEqual(new Vector2(-12f, 58f), enemyView.FrostcrownMarkImage.rectTransform.anchoredPosition);
            Assert.AreEqual(new Vector2(12f, 58f), enemyView.FrostMireMarkImage.rectTransform.anchoredPosition);

            lane.SetFrostcrownMarkedEnemies(null);
            Assert.AreEqual(new Vector2(0f, 58f), enemyView.FrostMireMarkImage.rectTransform.anchoredPosition);
        }

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
            Assert.IsNotNull(view.InputReceiver);
            Assert.IsTrue(view.InputReceiver.raycastTarget);
            Assert.AreEqual(new Vector2(100f, 100f), view.InputReceiver.rectTransform.rect.size);
            Assert.IsFalse(view.ArtImage.raycastTarget,
                "Oversized portrait artwork must not expand the unit touch target into adjacent cells.");
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, view.RectTransform.position)
            };
            view.OnPointerDown(pointer);
            view.OnPointerUp(pointer);
            view.OnPointerClick(pointer);

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
            Assert.IsTrue(
                bootstrap.RecruitDestination.IsCombatSuspended(basic.RuntimeId),
                "A deployed basic unit must remain combat-suspended while its landing visual is running.");
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            var deploymentGhost = screen.FixedBoardCanvas.DeploymentFxLayer
                .FindUi($"DeploymentGhost_{basic.RuntimeId}")
                ?.GetComponent<DraggableUnitView>();
            Assert.IsNotNull(deploymentGhost);
            Assert.IsFalse(
                deploymentGhost.BasicAttackAnimator.enabled,
                "The flight proxy must not start the attack controller's default state.");

            yield return new WaitForSecondsRealtime(0.7f);
            Assert.IsFalse(bootstrap.RecruitDestination.IsCombatSuspended(basic.RuntimeId));
            Assert.IsNull(
                screen.FixedBoardCanvas.DeploymentFxLayer
                    .FindUi($"DeploymentGhost_{basic.RuntimeId}"));

            var returnBench = bootstrap.PlayerBoard.GetPositions(CellType.Bench)
                .First(position => !bootstrap.PlayerBoard.IsOccupied(position));
            var returnCell = bootstrap.BoardView.CellViews.Single(cell => cell.Position == returnBench);
            var returnScreenPosition = RectTransformUtility.WorldToScreenPoint(
                null,
                returnCell.ContentAnchor.position);
            Assert.IsTrue(bootstrap.BoardView.BeginDrag(basic.RuntimeId));
            bootstrap.BoardView.CompleteDrag(basic.RuntimeId, returnScreenPosition);
            Assert.IsTrue(bootstrap.PlayerBoard.TryGetPosition(basic.RuntimeId, out var afterReturn));
            Assert.AreEqual(returnBench, afterReturn);
            Assert.IsNotNull(
                screen.FixedBoardCanvas.DeploymentFxLayer
                    .FindUi($"DeploymentGhost_{basic.RuntimeId}"),
                "A basic unit returning from the battlefield to the bench must use the deployment flight.");

            yield return new WaitForSecondsRealtime(0.7f);
            Assert.IsNull(
                screen.FixedBoardCanvas.DeploymentFxLayer
                    .FindUi($"DeploymentGhost_{basic.RuntimeId}"));
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
            bootstrap.AiBoardView.RefreshUnits();
            yield return null;

            var aiComponentViews = bootstrap.AiBoardView.UnitLayer
                .GetComponentsInChildren<DraggableUnitView>(true);
            Assert.AreEqual(4, aiComponentViews.Length);
            Assert.IsTrue(
                aiComponentViews.All(view => view.IsArtMirrored),
                "Every AI hero component art layer must mirror its player-side orientation.");
            Assert.IsTrue(
                aiComponentViews.All(view => Mathf.Approximately(
                    -45f,
                    view.ArtImage.rectTransform.anchoredPosition.x)),
                "Every AI hero component ART_UnitPortrait must use anchored Pos X=-45.");

            var aiHeroViews = bootstrap.AiBoardView.UnitLayer
                .GetComponentsInChildren<HeroFormationView>(true);
            Assert.AreEqual(2, aiHeroViews.Length);
            Assert.IsTrue(
                aiHeroViews.All(view => view.IsArtMirrored),
                "Every completed AI hero art layer must mirror its player-side orientation.");
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
        public IEnumerator FlameDrakeRiderSkillUsesLargeRoadFireballOnceForMultiTargetDamageEvents()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            var enemy = new EnemyRuntime("test.dragon.dive", TeamSide.Player);
            enemy.SetTargetingState(4, 0.5f, new CombatPoint(0f, 0f));
            Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Register(enemy));

            var combatFx = screen.PlayerBattlefieldView.GetComponent<CombatFxView>();
            var handler = typeof(CombatFxView).GetMethod(
                "OnCombat",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(handler);
            var diveEvent = new CombatEvent(
                TeamSide.Player,
                AttackKind.DragonRiderDive,
                "pair.test.dragon",
                enemy.RuntimeId,
                20f,
                false,
                false,
                bootstrap.Match.Player.Resources);

            handler.Invoke(combatFx, new object[] { diveEvent });
            handler.Invoke(combatFx, new object[] { diveEvent });

            var releaseHandler = typeof(CombatFxView).GetMethod(
                "HandleFlameDrakeFireballReleased",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(releaseHandler);
            releaseHandler.Invoke(combatFx, new object[] { "pair.test.dragon" });

            var skillFireball = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Image>(true)
                .Single(image => image.gameObject.activeInHierarchy &&
                                 image.gameObject.name == "Flame Drake Rider Fireball");
            Assert.AreSame(
                DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Flame Drake Rider/Sroad"),
                skillFireball.sprite);
            Assert.AreEqual(new Vector2(92f, 58f), skillFireball.rectTransform.sizeDelta);
            var lane = screen.PlayerBattlefieldView.LaneView;
            Assert.IsTrue(lane.TryGetEnemyPosition(enemy.RuntimeId, out var targetPosition));
            var directionToTarget =
                (targetPosition - skillFireball.rectTransform.position).normalized;
            Assert.Greater(
                Vector3.Dot(skillFireball.rectTransform.right, directionToTarget),
                0f,
                "Flame Drake Rider skill fireball must face its target.");

            bootstrap.Match.TryTransition(MatchState.Running);
            yield return new WaitForSeconds(0.38f);
            var skillExplosion = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Animator>(true)
                .Single(animator => animator.gameObject.activeInHierarchy &&
                                    animator.gameObject.name == "Flame Drake Rider Skill Boom");
            Assert.AreSame(
                DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>("Animation/FlameDrakeRiderBoomS"),
                skillExplosion.runtimeAnimatorController);
            Assert.AreEqual(
                new Vector2(145f, 145f),
                ((RectTransform)skillExplosion.transform).sizeDelta);

            skillExplosion.gameObject.SendMessage("OnAnimationEnd");
            yield return null;
            var burningGround = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Animator>(true)
                .Where(animator => animator.gameObject.activeInHierarchy &&
                                   animator.gameObject.name.StartsWith(
                                       "Flame Drake Rider Burning Ground",
                                       System.StringComparison.Ordinal))
                .ToArray();
            Assert.AreEqual(3, burningGround.Length);
            Assert.IsTrue(burningGround.All(animator =>
                animator.runtimeAnimatorController ==
                DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>("Animation/boomBoard")));
            Assert.IsTrue(burningGround.All(animator =>
                ((RectTransform)animator.transform).sizeDelta == new Vector2(110f, 110f)));
            var laneWaypoints = screen.PlayerBattlefieldView.LaneView.Waypoints;
            Assert.IsTrue(screen.PlayerBattlefieldView.LaneView.TryGetEnemyPathTileIndex(
                enemy.RuntimeId,
                out var targetTileIndex));
            var expectedBurningPositions = Enumerable.Range(-1, 3)
                .Select(offset => targetTileIndex + offset)
                .Where(index => index > 0 && index < laneWaypoints.Count - 1)
                .Select(index => laneWaypoints[index].position)
                .ToArray();
            CollectionAssert.AreEquivalent(
                expectedBurningPositions,
                burningGround.Select(animator => animator.transform.position).ToArray());
            Assert.IsTrue(burningGround.All(animator =>
                Vector3.Distance(animator.transform.position, laneWaypoints[0].position) > 0.01f &&
                Vector3.Distance(
                    animator.transform.position,
                    laneWaypoints[laneWaypoints.Count - 1].position) > 0.01f));

            yield return new WaitForSeconds(3.1f);
            Assert.AreEqual(
                0,
                screen.FixedBoardCanvas.CombatFxLayer
                    .GetComponentsInChildren<Animator>(true)
                    .Count(animator => animator.gameObject.activeInHierarchy &&
                                       animator.gameObject.name.StartsWith(
                                           "Flame Drake Rider Burning Ground",
                                           System.StringComparison.Ordinal)));
        }

        [UnityTest]
        public IEnumerator FlameDrakeFireballWaitsForReleaseFrameThenExplodesBeforeDamageFeedback()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            var enemy = new EnemyRuntime("test.dragon.fireball", TeamSide.Player);
            enemy.SetTargetingState(4, 0.5f, new CombatPoint(0f, 0f));
            Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Register(enemy));

            var combatFx = screen.PlayerBattlefieldView.GetComponent<CombatFxView>();
            var flags = System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic;
            var combatHandler = typeof(CombatFxView).GetMethod("OnCombat", flags);
            var releaseHandler = typeof(CombatFxView).GetMethod(
                "HandleFlameDrakeFireballReleased",
                flags);
            Assert.IsNotNull(combatHandler);
            Assert.IsNotNull(releaseHandler);
            var fireballEvent = new CombatEvent(
                TeamSide.Player,
                AttackKind.DragonRiderArea,
                "pair.test.dragon.fireball",
                enemy.RuntimeId,
                20f,
                false,
                false,
                bootstrap.Match.Player.Resources);

            combatHandler.Invoke(combatFx, new object[] { fireballEvent });
            combatHandler.Invoke(combatFx, new object[] { fireballEvent });
            Assert.AreEqual(
                0,
                screen.FixedBoardCanvas.CombatFxLayer
                    .GetComponentsInChildren<Image>(true)
                    .Count(image => image.gameObject.activeInHierarchy &&
                                    image.gameObject.name == "Flame Drake Rider Fireball"));

            releaseHandler.Invoke(
                combatFx,
                new object[] { "pair.test.dragon.fireball" });
            var fireball = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Image>(true)
                .Single(image => image.gameObject.activeInHierarchy &&
                                 image.gameObject.name == "Flame Drake Rider Fireball");
            Assert.AreSame(
                DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Flame Drake Rider/road"),
                fireball.sprite);
            Assert.AreEqual(new Vector2(68f, 42f), fireball.rectTransform.sizeDelta);
            Assert.IsFalse(fireball.raycastTarget);

            bootstrap.Match.TryTransition(MatchState.Running);
            yield return new WaitForSeconds(0.38f);
            var normalExplosion = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Animator>(true)
                .Single(animator => animator.gameObject.activeInHierarchy &&
                                    animator.gameObject.name == "Flame Drake Rider Boom");
            Assert.AreEqual(
                new Vector2(105f, 105f),
                ((RectTransform)normalExplosion.transform).sizeDelta);
            Assert.AreEqual(
                0,
                screen.FixedBoardCanvas.CombatFxLayer
                    .GetComponentsInChildren<Text>(true)
                    .Count(text => text.gameObject.activeInHierarchy && text.text == "-20"));

            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(
                1,
                screen.FixedBoardCanvas.CombatFxLayer
                    .GetComponentsInChildren<Text>(true)
                    .Count(text => text.gameObject.activeInHierarchy && text.text == "-20"));
        }

        [UnityTest]
        public IEnumerator EmberShamanSplashAttackReleasesOneFireballPerTargetWithSmallerSplash()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            var primary = new EnemyRuntime("test.ember.primary", TeamSide.Player);
            primary.SetTargetingState(4, 0.5f, new CombatPoint(0f, 0f));
            var splash = new EnemyRuntime("test.ember.splash", TeamSide.Player);
            splash.SetTargetingState(5, 0.35f, new CombatPoint(0f, 0f));
            Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Register(primary));
            Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Register(splash));
            yield return null;

            var combatFx = screen.PlayerBattlefieldView.GetComponent<CombatFxView>();
            var flags = System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic;
            var combatHandler = typeof(CombatFxView).GetMethod("OnCombat", flags);
            var releaseHandler = typeof(CombatFxView).GetMethod(
                "HandleEmberShamanFireballReleased",
                flags);
            Assert.IsNotNull(combatHandler);
            Assert.IsNotNull(releaseHandler);

            combatHandler.Invoke(combatFx, new object[]
            {
                new CombatEvent(
                    TeamSide.Player,
                    AttackKind.EmberExplosiveFireball,
                    "pair.test.ember",
                    primary.RuntimeId,
                    20f,
                    false,
                    false,
                    bootstrap.Match.Player.Resources)
            });
            combatHandler.Invoke(combatFx, new object[]
            {
                new CombatEvent(
                    TeamSide.Player,
                    AttackKind.EmberExplosiveSplash,
                    "pair.test.ember",
                    splash.RuntimeId,
                    10f,
                    false,
                    false,
                    bootstrap.Match.Player.Resources)
            });

            Assert.AreEqual(
                0,
                screen.FixedBoardCanvas.CombatFxLayer
                    .GetComponentsInChildren<Image>(true)
                    .Count(image => image.gameObject.activeInHierarchy &&
                                    image.gameObject.name == "Ember Shaman Fireball"));

            releaseHandler.Invoke(combatFx, new object[] { "pair.test.ember" });
            var fireballs = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Image>(true)
                .Where(image => image.gameObject.activeInHierarchy &&
                                image.gameObject.name == "Ember Shaman Fireball")
                .ToArray();
            Assert.AreEqual(2, fireballs.Length);
            Assert.IsTrue(fireballs.All(fireball =>
                fireball.sprite == DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Ember Shaman/road")));
            Assert.AreEqual(
                1,
                fireballs.Count(fireball =>
                    fireball.rectTransform.sizeDelta == new Vector2(72f, 32f)));
            Assert.AreEqual(
                1,
                fireballs.Count(fireball =>
                    fireball.rectTransform.sizeDelta == new Vector2(54f, 24f)));
            Assert.IsTrue(fireballs.All(fireball => fireball.preserveAspect));
            Assert.IsTrue(fireballs.All(fireball => !fireball.raycastTarget));
        }

        [UnityTest]
        public IEnumerator RuneboltMagePierceWaitsForReleaseAndUsesOneContinuousAnimatedPath()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            var first = new EnemyRuntime("test.runebolt.first", TeamSide.Player);
            first.SetTargetingState(4, 0.5f, new CombatPoint(0f, 0f));
            var second = new EnemyRuntime("test.runebolt.second", TeamSide.Player);
            second.SetTargetingState(5, 0.35f, new CombatPoint(0f, 0f));
            Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Register(first));
            Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Register(second));
            yield return null;

            var combatFx = screen.PlayerBattlefieldView.GetComponent<CombatFxView>();
            var flags = System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic;
            var combatHandler = typeof(CombatFxView).GetMethod("OnCombat", flags);
            var releaseHandler = typeof(CombatFxView).GetMethod(
                "HandleRuneboltMageBoltReleased",
                flags);
            Assert.IsNotNull(combatHandler);
            Assert.IsNotNull(releaseHandler);

            foreach (var enemy in new[] { first, second })
            {
                combatHandler.Invoke(combatFx, new object[]
                {
                    new CombatEvent(
                        TeamSide.Player,
                        AttackKind.RuneboltPierce,
                        "pair.test.runebolt",
                        enemy.RuntimeId,
                        8f,
                        false,
                        false,
                        bootstrap.Match.Player.Resources)
                });
            }

            Assert.AreEqual(
                0,
                screen.FixedBoardCanvas.CombatFxLayer
                    .GetComponentsInChildren<Animator>(true)
                    .Count(animator => animator.gameObject.activeInHierarchy &&
                                       animator.gameObject.name == "Runebolt Mage Path"));

            releaseHandler.Invoke(combatFx, new object[] { "pair.test.runebolt" });
            var bolts = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Animator>(true)
                .Where(animator => animator.gameObject.activeInHierarchy &&
                                   animator.gameObject.name == "Runebolt Mage Path")
                .ToArray();
            Assert.AreEqual(1, bolts.Length);
            Assert.AreSame(
                DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>("Animation/Runebolt MageBoom"),
                bolts[0].runtimeAnimatorController);
            var boltImage = bolts[0].GetComponent<Image>();
            var enemyPositions = screen.PlayerBattlefieldView
                .GetComponentsInChildren<EnemyView>(true)
                .Where(view => view.RuntimeId == first.RuntimeId ||
                               view.RuntimeId == second.RuntimeId)
                .Select(view => view.transform.position)
                .ToArray();
            Assert.AreEqual(2, enemyPositions.Length);
            var attackerPosition = combatFx.transform.position;
            var nearestTarget = enemyPositions
                .OrderBy(position => Vector3.SqrMagnitude(position - attackerPosition))
                .First();
            var pathDirection = (nearestTarget - attackerPosition).normalized;
            var expectedStart = attackerPosition + (pathDirection * 24f);
            var expectedLength = Mathf.Clamp(
                enemyPositions.Max(position =>
                    Vector3.Dot(position - expectedStart, pathDirection)),
                1f,
                550f);
            Assert.AreEqual(expectedLength, boltImage.rectTransform.sizeDelta.x, 0.01f);
            Assert.LessOrEqual(
                boltImage.rectTransform.sizeDelta.x,
                550f + 0.01f,
                "The visual path must not exceed the five-cell pierce range.");
            Assert.AreEqual(300f, boltImage.rectTransform.sizeDelta.y, 0.001f);
            Assert.AreEqual(new Vector2(0f, 0.5f), boltImage.rectTransform.pivot);
            Assert.IsFalse(boltImage.preserveAspect);
            Assert.IsFalse(boltImage.raycastTarget);

            var pathStart = boltImage.rectTransform.position;
            yield return new WaitForSeconds(0.05f);
            Assert.Less(
                Vector3.Distance(pathStart, boltImage.rectTransform.position),
                0.01f,
                "The path must stay anchored at the hero instead of flying like an arrow.");

            yield return new WaitForSeconds(0.20f);
            var enemyViews = screen.PlayerBattlefieldView
                .GetComponentsInChildren<EnemyView>(true)
                .Where(view => view.RuntimeId == first.RuntimeId ||
                               view.RuntimeId == second.RuntimeId)
                .ToArray();
            Assert.AreEqual(2, enemyViews.Length);
            Assert.IsTrue(enemyViews.All(view => !view.IsHealthVisualHeld));
        }

        [UnityTest]
        public IEnumerator BowWaitsForFrameSeventeenThenSwordHitsBeforeDamageFeedback()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            var enemy = new EnemyRuntime("test.bow.sword", TeamSide.Player);
            enemy.SetTargetingState(4, 0.5f, new CombatPoint(0f, 0f));
            Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Register(enemy));

            var combatFx = screen.PlayerBattlefieldView.GetComponent<CombatFxView>();
            var flags = System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic;
            var combatHandler = typeof(CombatFxView).GetMethod("OnCombat", flags);
            var releaseHandler = typeof(CombatFxView).GetMethod(
                "HandleBowProjectileReleased",
                flags);
            Assert.IsNotNull(combatHandler);
            Assert.IsNotNull(releaseHandler);

            var bowEvent = new CombatEvent(
                TeamSide.Player,
                AttackKind.BowProjectile,
                "unit.test.bow",
                enemy.RuntimeId,
                8f,
                false,
                false,
                bootstrap.Match.Player.Resources,
                damageOwnerKind: CombatDamageOwnerKind.BasicUnit,
                damageOwnerRuntimeId: "unit.test.bow");
            combatHandler.Invoke(combatFx, new object[] { bowEvent });

            Assert.AreEqual(
                0,
                screen.FixedBoardCanvas.CombatFxLayer
                    .GetComponentsInChildren<Image>(true)
                    .Count(image => image.gameObject.activeInHierarchy &&
                                    image.gameObject.name == "Bow Sword Projectile"));

            releaseHandler.Invoke(combatFx, new object[] { "unit.test.bow" });
            var sword = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Image>(true)
                .Single(image => image.gameObject.activeInHierarchy &&
                                 image.gameObject.name == "Bow Sword Projectile");
            Assert.AreSame(DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Unit/road"), sword.sprite);
            Assert.AreEqual(new Vector2(48f, 41f), sword.rectTransform.sizeDelta);
            Assert.IsTrue(sword.preserveAspect);
            Assert.IsFalse(sword.raycastTarget);
            Assert.AreEqual(
                0,
                screen.FixedBoardCanvas.CombatFxLayer
                    .GetComponentsInChildren<Text>(true)
                    .Count(text => text.gameObject.activeInHierarchy && text.text == "-8"));

            bootstrap.Match.TryTransition(MatchState.Running);
            yield return new WaitForSeconds(0.35f);
            Assert.IsTrue(sword == null);
            Assert.AreEqual(
                1,
                screen.FixedBoardCanvas.CombatFxLayer
                    .GetComponentsInChildren<Text>(true)
                    .Count(text => text.gameObject.activeInHierarchy && text.text == "-8"));
        }

        [UnityTest]
        public IEnumerator StoneboundWarlockUsesSmallNormalRockAndLargeSkillRockAfterFrameFourteen()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            var enemy = new EnemyRuntime("test.stonebound.rock", TeamSide.Player);
            enemy.SetTargetingState(4, 0.5f, new CombatPoint(0f, 0f));
            Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Register(enemy));
            yield return null;

            var combatFx = screen.PlayerBattlefieldView.GetComponent<CombatFxView>();
            var flags = System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic;
            var combatHandler = typeof(CombatFxView).GetMethod("OnCombat", flags);
            var releaseHandler = typeof(CombatFxView).GetMethod(
                "HandleStoneboundWarlockRockReleased",
                flags);
            Assert.IsNotNull(combatHandler);
            Assert.IsNotNull(releaseHandler);

            var attackerId = "pair.test.stonebound";
            combatHandler.Invoke(combatFx, new object[]
            {
                new CombatEvent(
                    TeamSide.Player,
                    AttackKind.StonebinderShot,
                    attackerId,
                    enemy.RuntimeId,
                    10f,
                    false,
                    false,
                    bootstrap.Match.Player.Resources)
            });
            Assert.AreEqual(
                0,
                screen.FixedBoardCanvas.CombatFxLayer
                    .GetComponentsInChildren<Image>(true)
                    .Count(image => image.gameObject.activeInHierarchy &&
                                    image.gameObject.name.StartsWith(
                                        "Stonebound Warlock",
                                        System.StringComparison.Ordinal)));

            releaseHandler.Invoke(combatFx, new object[] { attackerId });
            var normalRock = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Image>(true)
                .Single(image => image.gameObject.activeInHierarchy &&
                                 image.gameObject.name ==
                                 "Stonebound Warlock Normal Rock");
            Assert.AreSame(
                DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Stonebound Warlock/rood"),
                normalRock.sprite);
            Assert.AreEqual(new Vector2(48f, 48f), normalRock.rectTransform.sizeDelta);
            Assert.IsTrue(normalRock.preserveAspect);

            bootstrap.Match.TryTransition(MatchState.Running);
            yield return new WaitForSeconds(0.40f);
            Assert.IsTrue(normalRock == null);

            combatHandler.Invoke(combatFx, new object[]
            {
                new CombatEvent(
                    TeamSide.Player,
                    AttackKind.StonebinderShot,
                    attackerId,
                    enemy.RuntimeId,
                    10f,
                    false,
                    false,
                    bootstrap.Match.Player.Resources)
            });
            combatHandler.Invoke(combatFx, new object[]
            {
                new CombatEvent(
                    TeamSide.Player,
                    AttackKind.StoneBind,
                    attackerId,
                    enemy.RuntimeId,
                    0f,
                    false,
                    false,
                    bootstrap.Match.Player.Resources)
            });
            releaseHandler.Invoke(combatFx, new object[] { attackerId });

            var skillRock = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Image>(true)
                .Single(image => image.gameObject.activeInHierarchy &&
                                 image.gameObject.name ==
                                 "Stonebound Warlock Skill Rock");
            Assert.AreSame(
                DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Stonebound Warlock/roodS"),
                skillRock.sprite);
            Assert.AreEqual(new Vector2(88f, 88f), skillRock.rectTransform.sizeDelta);
            Assert.IsTrue(skillRock.preserveAspect);
            Assert.IsFalse(skillRock.raycastTarget);
        }

        [UnityTest]
        public IEnumerator ThunderlordReleasesThreeOrderedChainSegmentsAfterFrameTen()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            var enemies = new[]
            {
                new EnemyRuntime("test.thunderlord.main", TeamSide.Player),
                new EnemyRuntime("test.thunderlord.first", TeamSide.Player),
                new EnemyRuntime("test.thunderlord.second", TeamSide.Player)
            };
            for (var index = 0; index < enemies.Length; index++)
            {
                enemies[index].SetTargetingState(
                    4 + index,
                    0.30f + (index * 0.10f),
                    new CombatPoint(0f, 0f));
                Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Register(enemies[index]));
            }
            yield return null;

            var combatFx = screen.PlayerBattlefieldView.GetComponent<CombatFxView>();
            var flags = System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic;
            var combatHandler = typeof(CombatFxView).GetMethod("OnCombat", flags);
            var releaseHandler = typeof(CombatFxView).GetMethod(
                "HandleThunderlordChainReleased",
                flags);
            Assert.IsNotNull(combatHandler);
            Assert.IsNotNull(releaseHandler);

            for (var index = 0; index < enemies.Length; index++)
            {
                combatHandler.Invoke(combatFx, new object[]
                {
                    new CombatEvent(
                        TeamSide.Player,
                        AttackKind.ThunderJarlChain,
                        "pair.test.thunderlord",
                        enemies[index].RuntimeId,
                        index == 0 ? 11f : index == 1 ? 8.25f : 6.05f,
                        false,
                        false,
                        bootstrap.Match.Player.Resources)
                });
            }

            var enemyViews = screen.PlayerBattlefieldView
                .GetComponentsInChildren<EnemyView>(true)
                .Where(view => enemies.Any(enemy => enemy.RuntimeId == view.RuntimeId))
                .ToArray();
            Assert.AreEqual(3, enemyViews.Length);
            Assert.IsTrue(enemyViews.All(view => view.IsHealthVisualHeld));
            Assert.AreEqual(
                0,
                screen.FixedBoardCanvas.CombatFxLayer
                    .GetComponentsInChildren<Image>(true)
                    .Count(image => image.gameObject.activeInHierarchy &&
                                    image.gameObject.name.StartsWith(
                                        "Thunderlord",
                                        System.StringComparison.Ordinal)));

            releaseHandler.Invoke(combatFx, new object[] { "pair.test.thunderlord" });
            var main = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Image>(true)
                .Single(image => image.gameObject.activeInHierarchy &&
                                 image.gameObject.name == "Thunderlord Main Chain");
            Assert.AreSame(DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Thunderlord/road/Main"), main.sprite);
            Assert.AreEqual(48f, main.rectTransform.sizeDelta.y, 0.001f);
            Assert.IsFalse(main.preserveAspect);
            Assert.IsFalse(main.raycastTarget);
            Assert.IsFalse(enemyViews.Single(view =>
                view.RuntimeId == enemies[0].RuntimeId).IsHealthVisualHeld);
            Assert.IsTrue(enemyViews.Single(view =>
                view.RuntimeId == enemies[1].RuntimeId).IsHealthVisualHeld);
            Assert.IsTrue(enemyViews.Single(view =>
                view.RuntimeId == enemies[2].RuntimeId).IsHealthVisualHeld);

            yield return new WaitForSeconds(0.05f);
            var firstJump = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Image>(true)
                .Single(image => image.gameObject.activeInHierarchy &&
                                 image.gameObject.name == "Thunderlord First Jump Chain");
            Assert.AreSame(DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Thunderlord/road/Froad"), firstJump.sprite);
            Assert.AreEqual(38f, firstJump.rectTransform.sizeDelta.y, 0.001f);
            Assert.IsFalse(enemyViews.Single(view =>
                view.RuntimeId == enemies[1].RuntimeId).IsHealthVisualHeld);
            Assert.IsTrue(enemyViews.Single(view =>
                view.RuntimeId == enemies[2].RuntimeId).IsHealthVisualHeld);

            yield return new WaitForSeconds(0.05f);
            var secondJump = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Image>(true)
                .Single(image => image.gameObject.activeInHierarchy &&
                                 image.gameObject.name == "Thunderlord Second Jump Chain");
            Assert.AreSame(DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Thunderlord/road/Sroad"), secondJump.sprite);
            Assert.AreEqual(30f, secondJump.rectTransform.sizeDelta.y, 0.001f);
            Assert.IsTrue(enemyViews.All(view => !view.IsHealthVisualHeld));
        }

        [UnityTest]
        public IEnumerator ThunderlordSkillReleasesOneNineTenthsSecondAreaStormAfterFrameTen()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            var enemies = new[]
            {
                new EnemyRuntime("test.thunderlord.skill.a", TeamSide.Player),
                new EnemyRuntime("test.thunderlord.skill.b", TeamSide.Player)
            };
            for (var index = 0; index < enemies.Length; index++)
            {
                enemies[index].SetTargetingState(
                    4 + index,
                    0.35f + (index * 0.10f),
                    new CombatPoint(0f, 0f));
                Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Register(enemies[index]));
            }
            yield return null;

            var combatFx = screen.PlayerBattlefieldView.GetComponent<CombatFxView>();
            var flags = System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic;
            var combatHandler = typeof(CombatFxView).GetMethod("OnCombat", flags);
            var releaseHandler = typeof(CombatFxView).GetMethod(
                "HandleThunderlordSkillReleased",
                flags);
            Assert.IsNotNull(combatHandler);
            Assert.IsNotNull(releaseHandler);

            foreach (var enemy in enemies)
            {
                combatHandler.Invoke(combatFx, new object[]
                {
                    new CombatEvent(
                        TeamSide.Player,
                        AttackKind.ThunderDominion,
                        "pair.test.thunderlord.skill",
                        enemy.RuntimeId,
                        6.6f,
                        false,
                        false,
                        bootstrap.Match.Player.Resources,
                        0.9f)
                });
            }

            var enemyViews = screen.PlayerBattlefieldView
                .GetComponentsInChildren<EnemyView>(true)
                .Where(view => enemies.Any(enemy => enemy.RuntimeId == view.RuntimeId))
                .ToArray();
            Assert.AreEqual(2, enemyViews.Length);
            Assert.IsTrue(enemyViews.All(view => view.IsHealthVisualHeld));

            releaseHandler.Invoke(combatFx, new object[] { "pair.test.thunderlord.skill" });
            var boom = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Image>(true)
                .Single(image => image.gameObject.activeInHierarchy &&
                                 image.gameObject.name == "Thunderlord Skill Boom");
            Assert.AreEqual(new Vector2(550f, 550f), boom.rectTransform.sizeDelta);
            Assert.IsTrue(boom.preserveAspect);
            Assert.IsFalse(boom.raycastTarget);
            Assert.AreSame(
                DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>("Animation/ThunderlordBoom"),
                boom.GetComponent<Animator>().runtimeAnimatorController);
            Assert.IsTrue(enemyViews.All(view => !view.IsHealthVisualHeld));

            yield return new WaitForSeconds(0.45f);
            Assert.IsTrue(boom != null);
            yield return new WaitForSeconds(0.50f);
            Assert.IsTrue(boom == null);
        }

        [UnityTest]
        public IEnumerator AbyssalHarpoonerReleasesOneSixCellTiledHarpoonAfterFrameSixteen()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            var enemies = new[]
            {
                new EnemyRuntime("test.abyss.normal.a", TeamSide.Player),
                new EnemyRuntime("test.abyss.normal.b", TeamSide.Player)
            };
            for (var index = 0; index < enemies.Length; index++)
            {
                enemies[index].SetTargetingState(
                    3 + index,
                    0.35f + (index * 0.10f),
                    new CombatPoint(0f, 0f));
                Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Register(enemies[index]));
            }
            yield return null;

            var combatFx = screen.PlayerBattlefieldView.GetComponent<CombatFxView>();
            var flags = System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic;
            var combatHandler = typeof(CombatFxView).GetMethod("OnCombat", flags);
            var releaseHandler = typeof(CombatFxView).GetMethod(
                "HandleAbyssalHarpoonReleased",
                flags);
            Assert.IsNotNull(combatHandler);
            Assert.IsNotNull(releaseHandler);

            foreach (var enemy in enemies)
            {
                combatHandler.Invoke(combatFx, new object[]
                {
                    new CombatEvent(
                        TeamSide.Player,
                        AttackKind.LeviathanHarpoon,
                        "pair.test.abyss.normal",
                        enemy.RuntimeId,
                        15f,
                        false,
                        false,
                        bootstrap.Match.Player.Resources)
                });
            }

            var enemyViews = screen.PlayerBattlefieldView
                .GetComponentsInChildren<EnemyView>(true)
                .Where(view => enemies.Any(enemy => enemy.RuntimeId == view.RuntimeId))
                .ToArray();
            Assert.AreEqual(2, enemyViews.Length);
            Assert.IsTrue(enemyViews.All(view => view.IsHealthVisualHeld));

            releaseHandler.Invoke(combatFx, new object[] { "pair.test.abyss.normal" });
            var chain = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Image>(true)
                .Single(image => image.gameObject.activeInHierarchy &&
                                 image.gameObject.name == "Abyssal Harpooner Chain");
            var hook = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Image>(true)
                .Single(image => image.gameObject.name == "Abyssal Harpooner Hook");
            Assert.AreSame(DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Abyssal Harpooner/road"), chain.sprite);
            Assert.AreEqual(Image.Type.Tiled, chain.type);
            Assert.AreEqual(16f, chain.rectTransform.sizeDelta.y, 0.001f);
            Assert.AreEqual(
                chain.sprite.rect.height / 16f,
                chain.pixelsPerUnitMultiplier,
                0.001f);
            Assert.AreSame(DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Abyssal Harpooner/boom"), hook.sprite);
            Assert.AreEqual(new Vector2(56f, 33.25f), hook.rectTransform.sizeDelta);
            Assert.IsTrue(hook.preserveAspect);
            Assert.IsFalse(chain.raycastTarget);
            Assert.IsFalse(hook.raycastTarget);

            yield return new WaitForSeconds(0.24f);
            Assert.Greater(chain.rectTransform.sizeDelta.x, 1f);
            Assert.LessOrEqual(chain.rectTransform.sizeDelta.x, 660f);
            Assert.AreEqual(
                chain.rectTransform.sizeDelta.x,
                hook.rectTransform.anchoredPosition.x,
                1f);
            Assert.IsTrue(enemyViews.All(view => !view.IsHealthVisualHeld));

            yield return new WaitForSeconds(0.25f);
            Assert.IsTrue(chain == null);
            Assert.IsTrue(hook == null);
        }

        [UnityTest]
        public IEnumerator SkyborneValkyrieSkillReleasesThreeAuthoredArrowsAfterAnimationEvent()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            var enemies = new[]
            {
                new EnemyRuntime("test.valkyrie.primary", TeamSide.Player),
                new EnemyRuntime("test.valkyrie.secondary.a", TeamSide.Player),
                new EnemyRuntime("test.valkyrie.secondary.b", TeamSide.Player)
            };
            for (var index = 0; index < enemies.Length; index++)
            {
                enemies[index].SetTargetingState(
                    4,
                    0.4f + (index * 0.1f),
                    new CombatPoint(0f, 0f));
                Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Register(enemies[index]));
            }

            var combatFx = screen.PlayerBattlefieldView.GetComponent<CombatFxView>();
            var flags = System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic;
            var rotationOffsetField = typeof(CombatFxView).GetField(
                "skyborneValkyrieArrowRotationOffset",
                flags);
            Assert.IsNotNull(rotationOffsetField);
            Assert.AreEqual(180f, (float)rotationOffsetField.GetValue(combatFx), 0.001f);
            var combatHandler = typeof(CombatFxView).GetMethod("OnCombat", flags);
            var releaseHandler = typeof(CombatFxView).GetMethod(
                "HandleSkyborneValkyrieArrowReleased",
                flags);
            Assert.IsNotNull(combatHandler);
            Assert.IsNotNull(releaseHandler);

            for (var index = 0; index < enemies.Length; index++)
            {
                var kind = index == 0
                    ? AttackKind.SkyhunterRadiancePrimary
                    : AttackKind.SkyhunterRadianceSecondary;
                combatHandler.Invoke(combatFx, new object[]
                {
                    new CombatEvent(
                        TeamSide.Player,
                        kind,
                        "pair.test.valkyrie",
                        enemies[index].RuntimeId,
                        12f,
                        false,
                        false,
                        bootstrap.Match.Player.Resources)
                });
            }

            Assert.AreEqual(
                0,
                screen.FixedBoardCanvas.CombatFxLayer
                    .GetComponentsInChildren<Image>(true)
                    .Count(image => image.gameObject.activeInHierarchy &&
                                    image.gameObject.name == "Skyborne Valkyrie Arrow"));

            releaseHandler.Invoke(combatFx, new object[] { "pair.test.valkyrie" });
            var arrows = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Image>(true)
                .Where(image => image.gameObject.activeInHierarchy &&
                                image.gameObject.name == "Skyborne Valkyrie Arrow")
                .ToArray();
            Assert.AreEqual(3, arrows.Length);
            Assert.IsTrue(arrows.All(arrow =>
                arrow.sprite == DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Skyborne Valkyrie/road")));
            Assert.IsTrue(arrows.All(arrow =>
                arrow.rectTransform.sizeDelta == new Vector2(68f, 29f)));
            Assert.IsTrue(arrows.All(arrow => arrow.preserveAspect && !arrow.raycastTarget));
        }

        [UnityTest]
        public IEnumerator StarfallNormalAttackReleasesOneGemForMultiTargetDamageEvents()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            var enemy = new EnemyRuntime("test.starfall.normal", TeamSide.Player);
            enemy.SetTargetingState(4, 0.5f, new CombatPoint(0f, 0f));
            Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Register(enemy));

            var combatFx = screen.PlayerBattlefieldView.GetComponent<CombatFxView>();
            var rotationOffsetField = typeof(CombatFxView).GetField(
                "starfallGemRotationOffset",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(rotationOffsetField);
            Assert.AreEqual(180f, (float)rotationOffsetField.GetValue(combatFx), 0.001f);
            var handler = typeof(CombatFxView).GetMethod(
                "OnCombat",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(handler);
            var starfallEvent = new CombatEvent(
                TeamSide.Player,
                AttackKind.StarfallArea,
                "pair.test.starfall",
                enemy.RuntimeId,
                20f,
                false,
                false,
                bootstrap.Match.Player.Resources);

            handler.Invoke(combatFx, new object[] { starfallEvent });
            handler.Invoke(combatFx, new object[] { starfallEvent });
            var release = typeof(CombatFxView).GetMethod(
                "HandleStarfallArchmageGemReleased",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(release);
            release.Invoke(combatFx, new object[] { "pair.test.starfall" });

            var activeStars = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Image>(true)
                .Where(image => image.gameObject.activeInHierarchy &&
                                image.gameObject.name == "Starfall Archmage Gem")
                .ToArray();
            Assert.AreEqual(1, activeStars.Length);
            Assert.AreSame(
                DragonBound.Presentation.UiAssets.Load<Sprite>(
                    "VFX/Starfall Archmage/road/微信图片_20260831170133_175_101"),
                activeStars[0].sprite);
            Assert.AreEqual(18f, activeStars[0].rectTransform.sizeDelta.y, 0.001f);
            Assert.AreEqual(
                activeStars[0].sprite.rect.width / activeStars[0].sprite.rect.height,
                activeStars[0].rectTransform.sizeDelta.x / activeStars[0].rectTransform.sizeDelta.y,
                0.001f);
            Assert.IsTrue(activeStars[0].preserveAspect);
            Assert.IsFalse(activeStars[0].raycastTarget);
            Assert.AreEqual(
                1,
                screen.FixedBoardCanvas.CombatFxLayer
                    .GetComponentsInChildren<Graphic>(true)
                    .Count(graphic => graphic.gameObject.activeInHierarchy &&
                                      graphic.gameObject.name == "Starfall Archmage Gem Ribbon"));
        }

        [UnityTest]
        public IEnumerator StarfallSkillReleasesGemThenSpawnsAuthoredExplosion()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            var enemy = new EnemyRuntime("test.starfall.impact", TeamSide.Player);
            enemy.SetTargetingState(4, 0.5f, new CombatPoint(0f, 0f));
            Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Register(enemy));

            var combatFx = screen.PlayerBattlefieldView.GetComponent<CombatFxView>();
            var handler = typeof(CombatFxView).GetMethod(
                "OnCombat",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(handler);
            handler.Invoke(
                combatFx,
                new object[]
                {
                    new CombatEvent(
                        TeamSide.Player,
                        AttackKind.StarfallImpact,
                        "pair.test.starfall",
                        enemy.RuntimeId,
                        40f,
                        false,
                        false,
                        bootstrap.Match.Player.Resources)
                });

            var release = typeof(CombatFxView).GetMethod(
                "HandleStarfallArchmageGemReleased",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(release);
            release.Invoke(combatFx, new object[] { "pair.test.starfall" });
            Assert.AreEqual(
                1,
                screen.FixedBoardCanvas.CombatFxLayer
                    .GetComponentsInChildren<Image>(true)
                    .Count(image => image.gameObject.activeInHierarchy &&
                                    image.gameObject.name == "Starfall Archmage Gem"));

            yield return new WaitForSeconds(0.34f);

            var impact = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Animator>(true)
                .Single(animator => animator.gameObject.activeInHierarchy &&
                                    animator.gameObject.name == "Starfall Archmage Boom");
            Assert.AreSame(
                DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>("Animation/StarfallArchmageBoom"),
                impact.runtimeAnimatorController);
            Assert.AreEqual(new Vector2(145f, 145f), impact.GetComponent<RectTransform>().sizeDelta);

            var yellowFallbackLines = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Image>(true)
                .Count(image => image.gameObject.activeInHierarchy &&
                                image.gameObject.name == "ART_AttackLine(Clone)");
            Assert.AreEqual(0, yellowFallbackLines);
        }

        [UnityTest]
        public IEnumerator NightfangSkillWaitsForStartupThenSpawnsTargetExplosionWithoutProjectile()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            var enemy = new EnemyRuntime("test.nightfang.impact", TeamSide.Player);
            enemy.SetTargetingState(4, 0.5f, new CombatPoint(0f, 0f));
            Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Register(enemy));

            var combatFx = screen.PlayerBattlefieldView.GetComponent<CombatFxView>();
            var handler = typeof(CombatFxView).GetMethod(
                "OnCombat",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(handler);
            handler.Invoke(
                combatFx,
                new object[]
                {
                    new CombatEvent(
                        TeamSide.Player,
                        AttackKind.NightfangExecutionSlash,
                        "pair.test.nightfang",
                        enemy.RuntimeId,
                        40f,
                        false,
                        false,
                        bootstrap.Match.Player.Resources)
                });

            Assert.AreEqual(
                0,
                screen.FixedBoardCanvas.CombatFxLayer
                    .GetComponentsInChildren<Animator>(true)
                    .Count(animator => animator.gameObject.activeInHierarchy &&
                                       animator.gameObject.name == "Nightfang Assassin Boom"));
            var release = typeof(CombatFxView).GetMethod(
                "HandleNightfangSkillAnimationCompleted",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(release);
            release.Invoke(combatFx, new object[] { "pair.test.nightfang" });

            var boom = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Animator>(true)
                .Single(animator => animator.gameObject.activeInHierarchy &&
                                    animator.gameObject.name == "Nightfang Assassin Boom");
            Assert.AreSame(
                DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>("Animation/NightfangAssassinBoom"),
                boom.runtimeAnimatorController);
            Assert.AreEqual(new Vector2(100f, 100f), boom.GetComponent<RectTransform>().sizeDelta);
            Assert.AreEqual(
                0,
                screen.FixedBoardCanvas.CombatFxLayer
                    .GetComponentsInChildren<Image>(true)
                    .Count(image => image.gameObject.activeInHierarchy &&
                                    (image.gameObject.name == "ART_ExecutionSlash(Clone)" ||
                                     image.gameObject.name == "ART_AttackLine(Clone)")));
        }

        [UnityTest]
        public IEnumerator WindclawPowerShotFallbackFinishesImpactWhenAnimationCannotRelease()
        {
            SceneManager.LoadScene("HeroSlice_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            var enemy = new EnemyRuntime("test.windclaw.impact", TeamSide.Player);
            enemy.SetTargetingState(4, 0.5f, new CombatPoint(0f, 0f));
            Assert.IsTrue(bootstrap.ThreeWave.PlayerEnemyRegistry.Register(enemy));
            yield return null;
            var enemyView = screen.PlayerBattlefieldView
                .GetComponentsInChildren<EnemyView>(true)
                .Single(view => view.RuntimeId == enemy.RuntimeId);

            var combatFx = screen.PlayerBattlefieldView.GetComponent<CombatFxView>();
            var handler = typeof(CombatFxView).GetMethod(
                "OnCombat",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(handler);
            handler.Invoke(
                combatFx,
                new object[]
                {
                    new CombatEvent(
                        TeamSide.Player,
                        AttackKind.WindclawPowerShot,
                        "pair.test.windclaw",
                        enemy.RuntimeId,
                        40f,
                        false,
                        false,
                        bootstrap.Match.Player.Resources)
                });
            Assert.IsTrue(enemyView.IsHealthVisualHeld);

            // No matching formation exists for this synthetic attacker, so the animation event
            // cannot fire. CombatFx must still release the held target after its fallback delay.
            yield return new WaitForSeconds(0.34f);
            Assert.IsFalse(enemyView.IsHealthVisualHeld);

            var impact = screen.FixedBoardCanvas.CombatFxLayer
                .GetComponentsInChildren<Image>(true)
                .Single(image => image.gameObject.activeInHierarchy &&
                                 image.gameObject.name == "Windclaw Ranger Impact");
            Assert.AreSame(DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Windclaw Ranger/road"), impact.sprite);
            Assert.AreEqual(new Vector2(110f, 110f), impact.rectTransform.sizeDelta);
            Assert.IsTrue(impact.preserveAspect);
            Assert.IsFalse(impact.raycastTarget);
        }

        [UnityTest]
        public IEnumerator OverlappingProjectileHoldsStillPublishEachCompletedHealthChange()
        {
            var root = new GameObject("Overlapping Health Test", typeof(RectTransform));
            var bodyObject = new GameObject(
                "Body",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            bodyObject.transform.SetParent(root.transform, false);
            var fillObject = new GameObject(
                "Health Fill",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            fillObject.transform.SetParent(root.transform, false);

            var view = root.AddComponent<EnemyView>();
            var fill = fillObject.GetComponent<Image>();
            view.Configure(bodyObject.GetComponent<Image>(), fill, null);
            var enemy = new EnemyRuntime(
                "test.overlapping.health",
                TeamSide.Player,
                100f);
            view.Bind(enemy);
            Assert.AreEqual(1f, fill.fillAmount, 0.001f);

            view.HoldHealthVisual();
            view.HoldHealthVisual();
            enemy.ApplyDamage(25f);
            view.Bind(enemy);
            view.ReleaseHealthVisual();

            Assert.IsTrue(view.IsHealthVisualHeld);
            yield return new WaitForSeconds(0.05f);
            Assert.Less(fill.fillAmount, 1f);

            view.ReleaseHealthVisual();
            Assert.IsFalse(view.IsHealthVisualHeld);
            Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator DeathPresentationCannotBeHeldByALateProjectile()
        {
            var root = new GameObject("Lethal Health Hold Test", typeof(RectTransform));
            var bodyObject = new GameObject(
                "Body",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            bodyObject.transform.SetParent(root.transform, false);
            var fillObject = new GameObject(
                "Health Fill",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            fillObject.transform.SetParent(root.transform, false);

            var view = root.AddComponent<EnemyView>();
            view.Configure(bodyObject.GetComponent<Image>(), fillObject.GetComponent<Image>(), null);
            var enemy = new EnemyRuntime(
                "test.lethal.health.hold",
                TeamSide.Player,
                100f);
            view.Bind(enemy);

            view.HoldHealthVisual();
            Assert.IsTrue(view.IsHealthVisualHeld);
            view.ShowDeathFlash();
            view.HoldHealthVisual();

            Assert.IsFalse(
                view.IsHealthVisualHeld,
                "A projectile queued after lethal damage must not retain the dead enemy view.");
            yield return null;
            Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator StormcallerShieldUsesAnimatedOverlayWithoutTintingEnemyBody()
        {
            var root = new GameObject("Storm Shield Test", typeof(RectTransform));
            var bodyObject = new GameObject(
                "Body",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            bodyObject.transform.SetParent(root.transform, false);
            var healthTrack = new GameObject(
                "ART_EnemyHpTrack",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            healthTrack.transform.SetParent(root.transform, false);
            var fillObject = new GameObject(
                "ART_EnemyHpFill",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            fillObject.transform.SetParent(healthTrack.transform, false);

            var body = bodyObject.GetComponent<Image>();
            var authoredColor = new Color(0.8f, 0.35f, 0.2f, 1f);
            body.color = authoredColor;
            var view = root.AddComponent<EnemyView>();
            view.Configure(body, fillObject.GetComponent<Image>(), null);
            var enemy = new EnemyRuntime("test.storm.shield", TeamSide.Player, 100f);
            enemy.ApplyStormcallerShield(60f);

            view.Bind(enemy);
            yield return null;

            var shield = root.transform.FindUi("ART_StormShieldVFX");
            Assert.IsNotNull(shield);
            Assert.IsTrue(view.IsStormShieldVisible);
            Assert.AreEqual(root.transform.childCount - 1, shield.GetSiblingIndex());
            Assert.AreEqual(authoredColor, body.color);
            Assert.IsFalse(shield.GetComponent<Image>().raycastTarget);
            Assert.AreSame(
                DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>("Animation/ShieldShieldW"),
                shield.GetComponent<Animator>().runtimeAnimatorController);

            enemy.ApplyDamage(60f);
            view.Bind(enemy);
            Assert.IsFalse(view.IsStormShieldVisible);
            Assert.AreEqual(100f, enemy.HitPoints, 0.001f);

            Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator EnemyDeathDrainsHealthPlaysDieBoomAndThenDestroysView()
        {
            var root = new GameObject("Enemy Death Test", typeof(RectTransform));
            var bodyObject = new GameObject(
                "Body",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            bodyObject.transform.SetParent(root.transform, false);
            var fillObject = new GameObject(
                "Health Fill",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            fillObject.transform.SetParent(root.transform, false);

            var view = root.AddComponent<EnemyView>();
            var body = bodyObject.GetComponent<Image>();
            var fill = fillObject.GetComponent<Image>();
            view.Configure(body, fill, null);
            view.Bind(new EnemyRuntime("test.death.vfx", TeamSide.Player, 100f));

            view.ShowDeathFlash();

            Assert.IsFalse(body.enabled);
            var deathVfx = root.transform.FindUi("ART_EnemyDeathVFX");
            Assert.IsNotNull(deathVfx);
            Assert.AreEqual(new Vector2(110f, 110f),
                ((RectTransform)deathVfx).sizeDelta);
            Assert.IsFalse(deathVfx.GetComponent<Image>().raycastTarget);
            Assert.AreSame(
                DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>("Animation/DieBoom"),
                deathVfx.GetComponent<Animator>().runtimeAnimatorController);

            yield return new WaitForSecondsRealtime(0.13f);
            Assert.AreEqual(0f, fill.fillAmount, 0.001f);
            Assert.IsTrue(root != null);

            yield return new WaitForSecondsRealtime(0.08f);
            Assert.IsTrue(root == null);
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
            Assert.AreEqual("Baby Dragon", sigilView.GetComponentInChildren<Text>(true).text);
            MoveDirect(bootstrap, firstSigil.RuntimeId, new GridPosition(3, 1));

            var second = bootstrap.Recruitment.TryRecruit();
            var sky = second.Batch.Cards.Single(card =>
                card.ConfigId == HeroSliceCatalog.SkyRangerComponentId);
            var secondSigil = second.Batch.Cards.Single(card =>
                card.ConfigId == HeroSliceCatalog.DragonSigilComponentId);
            Assert.AreEqual(DragDropStatus.Moved, Drag(bootstrap, sky.RuntimeId, new GridPosition(2, 1)));
            var windclaw = bootstrap.RecruitDestination.GetActiveHeroPairs().Single();
            Assert.IsFalse(windclaw.PairLink.CombatProxy.IsFormationComplete);
            Assert.IsTrue(bootstrap.RecruitDestination.TryGetCard(firstSigil.RuntimeId, out _));
            Assert.IsTrue(bootstrap.RecruitDestination.TryGetCard(sky.RuntimeId, out _));
            MoveDirect(bootstrap, secondSigil.RuntimeId, new GridPosition(3, 2));

            var third = bootstrap.Recruitment.TryRecruit();
            var knight = third.Batch.Cards.Single(card =>
                card.ConfigId == HeroSliceCatalog.DragonKnightComponentId);
            Assert.AreEqual(DragDropStatus.Moved, Drag(bootstrap, knight.RuntimeId, new GridPosition(2, 2)));

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
                var firstView = FindCard(bootstrap.BoardView, activePair.ComponentA.ComponentId);
                var secondView = FindCard(bootstrap.BoardView, activePair.ComponentB.ComponentId);
                Assert.IsTrue(firstView.IsPairedPresentationHidden);
                Assert.IsTrue(secondView.IsPairedPresentationHidden);
                Assert.AreEqual(0f, firstView.GetComponent<CanvasGroup>().alpha, 0.001f);
                Assert.AreEqual(0f, secondView.GetComponent<CanvasGroup>().alpha, 0.001f);
                Assert.IsTrue(
                    firstView.GetComponent<CanvasGroup>().blocksRaycasts,
                    "The first transparent component cell must remain draggable after synthesis.");
                Assert.IsTrue(
                    secondView.GetComponent<CanvasGroup>().blocksRaycasts,
                    "The second transparent component cell must remain draggable after synthesis.");
            }

            bootstrap.BoardView.RefreshUnits();
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
            MoveDirect(bootstrap, sigil.RuntimeId, new GridPosition(3, 1));
            var second = bootstrap.Recruitment.TryRecruit();
            var sky = second.Batch.Cards.Single(card =>
                card.ConfigId == HeroSliceCatalog.SkyRangerComponentId);
            Assert.AreEqual(DragDropStatus.Moved, Drag(bootstrap, sky.RuntimeId, new GridPosition(2, 1)));
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
            Assert.AreEqual(new GridPosition(2, 1), partner);
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

        [UnityTest]
        public IEnumerator UnitAndHeroFacingTransitionsOnlyMirrorTheirArtHorizontally()
        {
            var unitRoot = new GameObject(
                "FacingUnitTest",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(DraggableUnitView));
            var unitArtObject = new GameObject(
                "Art",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            unitArtObject.transform.SetParent(unitRoot.transform, false);

            var heroRoot = new GameObject(
                "FacingHeroTest",
                typeof(RectTransform),
                typeof(HeroFormationView));
            var heroArtObject = new GameObject(
                "HeroArt",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Animator));
            heroArtObject.transform.SetParent(heroRoot.transform, false);

            try
            {
                var unitView = unitRoot.GetComponent<DraggableUnitView>();
                var unitArt = unitArtObject.GetComponent<Image>();
                var unitRect = unitArt.rectTransform;
                unitRect.localScale = new Vector3(1f, 1.25f, 1f);
                unitRect.anchoredPosition = new Vector2(7f, 9f);
                unitView.Configure(
                    unitArt,
                    null,
                    unitRoot.GetComponent<CanvasGroup>());
                unitView.InitializeArtFacing(false, -38f);
                unitView.FaceArtTowards(true, -38f);

                var heroView = heroRoot.GetComponent<HeroFormationView>();
                var heroAnimator = heroArtObject.GetComponent<Animator>();
                var heroRect = heroArtObject.GetComponent<RectTransform>();
                heroRect.localScale = new Vector3(1.5f, 0.8f, 1f);
                heroRect.anchoredPosition = new Vector2(-4f, 6f);
                heroView.Configure(null, null, null, null, null, heroAnimator);
                heroView.InitializeArtFacing(false, 13f);
                heroView.FaceArtTowards(true, 13f);

                yield return new WaitForSecondsRealtime(0.2f);

                Assert.IsTrue(unitView.IsArtMirrored);
                Assert.AreEqual(-1f, unitRect.localScale.x, 0.001f);
                Assert.AreEqual(1.25f, unitRect.localScale.y, 0.001f);
                Assert.AreEqual(-38f, unitRect.anchoredPosition.x, 0.001f);
                Assert.AreEqual(9f, unitRect.anchoredPosition.y, 0.001f);

                Assert.IsTrue(heroView.IsArtMirrored);
                Assert.AreEqual(-1.5f, heroRect.localScale.x, 0.001f);
                Assert.AreEqual(0.8f, heroRect.localScale.y, 0.001f);
                Assert.AreEqual(13f, heroRect.anchoredPosition.x, 0.001f);
                Assert.AreEqual(6f, heroRect.anchoredPosition.y, 0.001f);
            }
            finally
            {
                Object.Destroy(unitRoot);
                Object.Destroy(heroRoot);
            }
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
