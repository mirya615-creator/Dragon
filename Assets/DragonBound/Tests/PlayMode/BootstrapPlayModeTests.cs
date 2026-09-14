using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DragonBound.AI;
using DragonBound.Bootstrap;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Presentation;
using DragonBound.Recruitment;
using DragonBound.Services;
using DragonBound.UI;
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
                "Left: Rune Staff  Right: Storm Hat\n" +
                "A rune mage whose attacks pierce through enemies in a straight line.",
                CampPanelView.BuildHeroSummary(
                    recipe,
                    "Rune Staff",
                    "Storm Hat",
                    "A rune mage whose attacks pierce through enemies in a straight line."));
        }

        [Test]
        public void FormerVerticalHeroGallerySummaryUsesLeftAndRightComponents()
        {
            var recipe = HeroRecipeCatalog.Get(DragonBoundHeroIds.WindclawRanger);

            Assert.AreEqual(
                "Left: Sky Ranger  Right: Baby Dragon\nWindclaw skill",
                CampPanelView.BuildHeroSummary(
                    recipe,
                    "Sky Ranger",
                    "Baby Dragon",
                    "Windclaw skill"));
        }

        [UnityTest]
        public IEnumerator LoginToMainKeepsOneEventSystemAndDedicatedAnalyticsHost()
        {
            SceneManager.LoadScene("Login", LoadSceneMode.Single);

            var loginSystems = Object.FindObjectsOfType<EventSystem>(true)
                .Where(value => value != null && value.isActiveAndEnabled)
                .ToArray();
            Assert.AreEqual(1, loginSystems.Length);
            var analyticsHost = GameObject.Find("AnalyticsBootstrap");
            Assert.IsNotNull(analyticsHost);
            Assert.IsNull(analyticsHost.GetComponent<EventSystem>());
            Assert.IsNotNull(analyticsHost.GetComponent("FirebaseAnalyticsBootstrap"));

            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;

            var mainSystems = Object.FindObjectsOfType<EventSystem>(true)
                .Where(value => value != null && value.isActiveAndEnabled)
                .ToArray();
            Assert.AreEqual(1, mainSystems.Length);
            Assert.AreSame(analyticsHost, GameObject.Find("AnalyticsBootstrap"));
            Assert.AreEqual("DontDestroyOnLoad", analyticsHost.scene.name);
        }

        [UnityTest]
        public IEnumerator LoginConfirmationTipUsesSharedTipTextPrefabPresentation()
        {
            SceneManager.LoadScene("Login", LoadSceneMode.Single);
            yield return null;

            var canvas = GameObject.Find("Canvas");
            Assert.IsNotNull(canvas);
            var tip = canvas.transform.FindUi(
                "SafeArea/MainPanel/GoogleConfirmPanel/TipText");
            Assert.IsNotNull(tip);
            Assert.IsNotNull(tip.GetComponent("TipTextController"));
            Assert.IsNotNull(tip.GetComponent<Image>());
            Assert.IsNotNull(tip.FindUi("Text"));
        }

        [UnityTest]
        public IEnumerator MainTipsUseSharedPrefabAndDisappearAfterThreeSeconds()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;

            var legacyTips = Object.FindObjectsOfType<Transform>(true)
                .Where(value =>
                    value != null &&
                    value.gameObject.scene.name == "Main" &&
                    value.name == "TipText" &&
                    value.GetComponent<TipTextController>() == null)
                .ToArray();
            Assert.IsTrue(legacyTips.All(value => !value.gameObject.activeSelf));

            TipTextService.Show("Main tip presentation test");
            yield return null;

            var tipController = Object.FindObjectOfType<TipTextController>(true);
            Assert.IsNotNull(tipController);
            Assert.IsTrue(tipController.gameObject.activeSelf);
            Assert.IsNotNull(tipController.GetComponent<Image>());
            Assert.IsNotNull(tipController.transform.FindUi("Text"));

            yield return new WaitForSecondsRealtime(3.1f);
            Assert.IsFalse(tipController.gameObject.activeSelf);
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
        public IEnumerator GameplayHudUsesSharedTipPrefabWithoutBossTextNotifications()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var screen = FindScreen();
            var legacyTip = screen.transform.FindUi("TipText");
            Assert.IsNotNull(legacyTip);
            Assert.IsFalse(legacyTip.gameObject.activeSelf);

            var hudType = typeof(GreyboxHudView);
            const System.Reflection.BindingFlags privateInstance =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic;
            Assert.IsNull(hudType.GetMethod("ShowBossTip", privateInstance));
            Assert.IsNull(hudType.GetMethod("HandleSoulChainCast", privateInstance));
            Assert.IsNull(hudType.GetMethod("HandleStormcallerCast", privateInstance));
            Assert.IsNull(hudType.GetMethod("HandleBloodcrownLifecycle", privateInstance));
            Assert.IsNull(hudType.GetMethod("HandleWorldeaterCast", privateInstance));

            var showTip = hudType.GetMethod("ShowTip", privateInstance);
            Assert.IsNotNull(showTip);
            showTip.Invoke(screen.OverlayController, new object[] { "Gameplay tip test" });
            yield return null;

            var sharedTip = Object.FindObjectOfType<TipTextController>(true);
            Assert.IsNotNull(sharedTip);
            Assert.IsTrue(sharedTip.gameObject.activeSelf);
            TipTextService.Hide();
        }

        [UnityTest]
        public IEnumerator RiverTracksActualBattlefieldEdgesWithoutMovingMaps()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var screen = FindScreen();
            var safeAreaRoot = GameObject.Find("SafeAreaRoot")?.transform as RectTransform;
            var river = GameObject.Find("RIVER")?.transform as RectTransform;
            var topAnchor = screen.transform.FindUi(
                "ART_ScreenBackground/AiBattlefield/RiverTopAnchor") as RectTransform;
            var bottomAnchor = screen.transform.FindUi(
                "ART_ScreenBackground/PlayerBattlefield/RiverBottomAnchor") as RectTransform;

            Assert.IsNotNull(safeAreaRoot);
            Assert.IsNotNull(river);
            Assert.IsNotNull(topAnchor);
            Assert.IsNotNull(bottomAnchor);
            Assert.AreEqual(174f, river.rect.height, 0.1f);
            Assert.AreEqual(1080f, river.rect.width, 0.1f);

            var topBattlefield = topAnchor.parent as RectTransform;
            var bottomBattlefield = bottomAnchor.parent as RectTransform;
            Assert.IsNotNull(topBattlefield);
            Assert.IsNotNull(bottomBattlefield);
            var topPosition = topBattlefield.anchoredPosition;
            var bottomPosition = bottomBattlefield.anchoredPosition;
            var topScale = topBattlefield.localScale;
            var bottomScale = bottomBattlefield.localScale;
            river.gameObject.SendMessage("Refresh", SendMessageOptions.RequireReceiver);

            var riverY = safeAreaRoot.InverseTransformPoint(river.position).y;
            var corners = new Vector3[4];
            topBattlefield.GetWorldCorners(corners);
            var topBottomY = corners.Min(corner => safeAreaRoot.InverseTransformPoint(corner).y);
            bottomBattlefield.GetWorldCorners(corners);
            var bottomTopY = corners.Max(corner => safeAreaRoot.InverseTransformPoint(corner).y);
            Assert.AreEqual(((topBottomY + bottomTopY) * 0.5f) + 5f, riverY, 0.1f);
            Assert.AreEqual(topPosition, topBattlefield.anchoredPosition);
            Assert.AreEqual(bottomPosition, bottomBattlefield.anchoredPosition);
            Assert.AreEqual(topScale, topBattlefield.localScale);
            Assert.AreEqual(bottomScale, bottomBattlefield.localScale);
        }

        [UnityTest]
        public IEnumerator BeachDragShowsSourceTargetAndAuthoredRoadUntilDrop()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = FindBootstrap();
            var result = bootstrap.Recruitment.TryRecruit();
            Assert.AreEqual(RecruitmentStatus.Success, result.Status);
            bootstrap.BoardView.RefreshUnits();
            yield return null;

            var beachContainer = FindScreen().transform.FindUi("ART_ScreenBackground/BeachContainer");
            Assert.IsNotNull(beachContainer);
            var beachSelections = beachContainer
                .GetComponentsInChildren<Transform>(true)
                .Where(value =>
                    value.name == "Select" &&
                    value.parent != null &&
                    value.parent.name.StartsWith("ImgBg"))
                .ToArray();
            Assert.AreEqual(5, beachSelections.Length);
            Assert.IsTrue(beachSelections.All(value => !value.gameObject.activeSelf));

            var beachCard = result.Batch.Cards.First(card => card.Kind == RecruitItemKind.BasicUnit);
            Assert.IsTrue(bootstrap.PlayerBoard.TryGetPosition(beachCard.RuntimeId, out var origin));
            Assert.AreEqual(CellType.Bench, GetCellType(bootstrap.PlayerBoard, origin));
            var target = bootstrap.PlayerBoard.GetPositions(CellType.Battle)[0];
            var targetCell = bootstrap.BoardView.GetCellView(target);
            var targetScreenPosition = RectTransformUtility.WorldToScreenPoint(
                null,
                targetCell.ContentAnchor.position);

            Assert.IsTrue(bootstrap.BoardView.BeginDrag(beachCard.RuntimeId));
            Assert.AreEqual(1, bootstrap.BoardView.VisibleBeachSelectionCount);
            Assert.IsFalse(bootstrap.BoardView.IsBoardSelectVisible);
            var activeBeachSelection = beachSelections.Single(
                value => value.gameObject.activeSelf);
            Assert.AreSame(
                DragonBound.Presentation.UiAssets.Load<Sprite>("GameUI/BeachSelect"),
                activeBeachSelection.GetComponent<Image>().sprite);

            bootstrap.BoardView.UpdateDraggedUnit(beachCard.RuntimeId, targetScreenPosition);
            Assert.IsTrue(bootstrap.BoardView.IsBoardSelectVisible);
            var boardSelection = targetCell.ContentAnchor.FindUi("BoardSelect");
            Assert.IsNotNull(boardSelection);
            Assert.AreSame(
                DragonBound.Presentation.UiAssets.Load<Sprite>("GameUI/BoardSelect"),
                boardSelection.GetComponent<Image>().sprite);
            Assert.IsTrue(bootstrap.BoardView.IsDragArrowVisible);
            Assert.IsTrue(bootstrap.BoardView.RangePreview.gameObject.activeSelf);
            Assert.IsTrue(bootstrap.BoardView.RangePreview.enabled);
            Assert.AreSame(
                DragonBound.Presentation.UiAssets.Load<Sprite>("GameUI/SlectRoad"),
                bootstrap.BoardView.DragPathSprite);
            var expectedRange = BasicUnitCatalog
                .GetStats(beachCard.ConfigId, beachCard.Level)
                .RangeCells;
            var expectedDiameter = Mathf.Min(
                                       targetCell.RectTransform.rect.width,
                                       targetCell.RectTransform.rect.height) *
                                   expectedRange *
                                   2f;
            Assert.AreEqual(
                expectedDiameter,
                bootstrap.BoardView.RangePreview.rectTransform.sizeDelta.x,
                0.01f);
            Assert.Less(
                Vector3.Distance(
                    targetCell.ContentAnchor.position,
                    bootstrap.BoardView.RangePreview.rectTransform.position),
                0.01f);

            bootstrap.BoardView.UpdateDraggedUnit(
                beachCard.RuntimeId,
                new Vector2(-10000f, -10000f));
            Assert.IsTrue(
                bootstrap.BoardView.IsBoardSelectVisible,
                "Leaving valid cells must retain the last valid map selection.");
            Assert.AreSame(targetCell.ContentAnchor, boardSelection.parent);
            Assert.IsFalse(bootstrap.BoardView.IsDragArrowVisible);
            Assert.IsFalse(bootstrap.BoardView.RangePreview.gameObject.activeSelf);

            bootstrap.BoardView.CompleteDrag(beachCard.RuntimeId, targetScreenPosition);
            Assert.AreEqual(0, bootstrap.BoardView.VisibleBeachSelectionCount);
            Assert.IsFalse(bootstrap.BoardView.IsBoardSelectVisible);
            Assert.IsFalse(bootstrap.BoardView.IsDragArrowVisible);
            Assert.IsFalse(bootstrap.BoardView.RangePreview.gameObject.activeSelf);

            var cancelledCard = result.Batch.Cards
                .Where(card => card.Kind == RecruitItemKind.BasicUnit)
                .Skip(1)
                .First();
            Assert.IsTrue(bootstrap.BoardView.BeginDrag(cancelledCard.RuntimeId));
            bootstrap.BoardView.UpdateDraggedUnit(
                cancelledCard.RuntimeId,
                new Vector2(-10000f, -10000f));
            Assert.AreEqual(1, bootstrap.BoardView.VisibleBeachSelectionCount);
            Assert.IsFalse(bootstrap.BoardView.IsBoardSelectVisible);
            Assert.IsFalse(bootstrap.BoardView.IsDragArrowVisible);
            Assert.IsFalse(bootstrap.BoardView.RangePreview.gameObject.activeSelf);

            bootstrap.BoardView.CancelActiveDrag();
            Assert.AreEqual(0, bootstrap.BoardView.VisibleBeachSelectionCount);
            Assert.IsFalse(bootstrap.BoardView.IsBoardSelectVisible);
            Assert.IsFalse(bootstrap.BoardView.RangePreview.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator GoalHealthViewsHideLostHeartsAndCloneHealthBeyondThree()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = FindBootstrap();
            var screen = FindScreen();
            var playerView = screen.PlayerGoalHealthView;
            var aiView = screen.AiGoalHealthView;

            Assert.IsNotNull(playerView);
            Assert.IsNotNull(aiView);
            Assert.AreEqual(3, playerView.HeartCount);
            Assert.AreEqual(3, playerView.VisibleHeartCount);
            Assert.AreEqual(3, aiView.VisibleHeartCount);

            var playerLayout = playerView.GetComponent<GridLayoutGroup>();
            var aiLayout = aiView.GetComponent<GridLayoutGroup>();
            Assert.AreEqual(GridLayoutGroup.Constraint.FixedColumnCount, playerLayout.constraint);
            Assert.AreEqual(3, playerLayout.constraintCount);
            Assert.AreEqual(GridLayoutGroup.Constraint.FixedColumnCount, aiLayout.constraint);
            Assert.AreEqual(3, aiLayout.constraintCount);
            var playerHeartRoot = (RectTransform)playerView.transform;
            var initialHeight = playerHeartRoot.rect.height;
            var fixedBottom = playerHeartRoot.anchoredPosition.y -
                              (playerHeartRoot.rect.height * playerHeartRoot.pivot.y);

            bootstrap.Match.Player.ApplyHatchlingDamage(1);
            yield return null;
            Assert.AreEqual(2, playerView.VisibleHeartCount);
            Assert.AreEqual(3, aiView.VisibleHeartCount);

            bootstrap.Match.Player.ApplyHatchlingHealthBonus(3);
            yield return null;
            Assert.AreEqual(5, playerView.HeartCount);
            Assert.AreEqual(5, playerView.VisibleHeartCount);
            Assert.Greater(playerHeartRoot.rect.height, initialHeight);
            Assert.AreEqual(
                fixedBottom,
                playerHeartRoot.anchoredPosition.y -
                (playerHeartRoot.rect.height * playerHeartRoot.pivot.y),
                0.001f);

            bootstrap.Match.Player.ApplyHatchlingDamage(2);
            yield return null;
            Assert.AreEqual(5, playerView.HeartCount);
            Assert.AreEqual(3, playerView.VisibleHeartCount);
            Assert.AreEqual(initialHeight, playerHeartRoot.rect.height, 0.001f);
        }

        [UnityTest]
        public IEnumerator FirstRecruitButtonKeepsTheNormalRecruitmentBatch()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = FindBootstrap();
            var screen = FindScreen();
            Assert.IsNotNull(screen.RecruitmentButtonController);
            Assert.IsFalse(bootstrap.RecruitDestination.HasActiveHero(
                DragonBoundHeroIds.DragonRider));
            Assert.IsFalse(bootstrap.RecruitDestination.HasActiveHero(
                DragonBoundHeroIds.StarfallArchmage));

            screen.RecruitmentButtonController.RecruitButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(1, bootstrap.Recruitment.CompletedRecruitments);
            Assert.IsTrue(bootstrap.Recruitment.HasLastAttempt);
            Assert.IsNotNull(bootstrap.Recruitment.LastAttempt.Batch);
            CollectionAssert.AreEquivalent(
                bootstrap.Recruitment.LastAttempt.Batch.Cards.Select(card => card.RuntimeId),
                bootstrap.RecruitDestination.GetBoardCards().Select(card => card.RuntimeId));
            Assert.IsFalse(bootstrap.RecruitDestination.HasActiveHero(
                DragonBoundHeroIds.DragonRider));
            Assert.IsFalse(bootstrap.RecruitDestination.HasActiveHero(
                DragonBoundHeroIds.StarfallArchmage));
        }

        [UnityTest]
        public IEnumerator CampTabButtonsSwapSpritesAndScaleSelectedTabByTenPercent()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var screen = FindScreen();
            var campPanel = screen.transform.FindUi("campPanel");
            Assert.IsNotNull(campPanel);
            var deckPart = campPanel.FindUi("CampBg/DeckPart");
            var collectionPart = campPanel.FindUi("CampBg/CollectionPart");
            var deckButton = campPanel.FindUi("BtnImg/DeckBtn").GetComponent<Button>();
            var collectionButton = campPanel.FindUi("BtnImg/CollectionBtn").GetComponent<Button>();
            var deckImage = deckButton.GetComponent<Image>();
            var collectionImage = collectionButton.GetComponent<Image>();
            var deckSprite = DragonBound.Presentation.UiAssets.Load<Sprite>("GameUI/CampUI/Deck");
            var deckSelectedSprite = DragonBound.Presentation.UiAssets.Load<Sprite>("GameUI/CampUI/DeckClick");
            var collectionSprite = DragonBound.Presentation.UiAssets.Load<Sprite>("GameUI/CampUI/Collection");
            var collectionSelectedSprite = DragonBound.Presentation.UiAssets.Load<Sprite>("GameUI/CampUI/CollectionClick");

            Assert.IsTrue(deckPart.gameObject.activeSelf);
            Assert.IsFalse(collectionPart.gameObject.activeSelf);
            var deckPosition = deckButton.transform.localPosition;
            var collectionPosition = collectionButton.transform.localPosition;
            Assert.AreSame(collectionSelectedSprite, deckImage.sprite);
            Assert.AreSame(deckSprite, collectionImage.sprite);
            var selectedDeckScale = deckButton.transform.localScale;
            var normalCollectionScale = collectionButton.transform.localScale;

            collectionButton.onClick.Invoke();
            Assert.IsFalse(deckPart.gameObject.activeSelf);
            Assert.IsTrue(collectionPart.gameObject.activeSelf);
            Assert.AreSame(collectionSprite, deckImage.sprite);
            Assert.AreSame(deckSelectedSprite, collectionImage.sprite);
            Assert.AreEqual(deckPosition, deckButton.transform.localPosition);
            Assert.AreEqual(collectionPosition, collectionButton.transform.localPosition);
            Assert.Less(
                Vector3.Distance(selectedDeckScale / 1.10f, deckButton.transform.localScale),
                0.0001f);
            Assert.Less(
                Vector3.Distance(normalCollectionScale * 1.10f, collectionButton.transform.localScale),
                0.0001f);

            deckButton.onClick.Invoke();
            Assert.IsTrue(deckPart.gameObject.activeSelf);
            Assert.IsFalse(collectionPart.gameObject.activeSelf);
            Assert.AreSame(collectionSelectedSprite, deckImage.sprite);
            Assert.AreSame(deckSprite, collectionImage.sprite);
            Assert.AreEqual(deckPosition, deckButton.transform.localPosition);
            Assert.AreEqual(collectionPosition, collectionButton.transform.localPosition);
            Assert.Less(Vector3.Distance(selectedDeckScale, deckButton.transform.localScale), 0.0001f);
            Assert.Less(Vector3.Distance(normalCollectionScale, collectionButton.transform.localScale), 0.0001f);
        }

        [UnityTest]
        public IEnumerator CampComponentSpritesPopulateDeckAndSelectedHeroRecipe()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            DragonBoundScreenView screen = FindScreen();
            var provider = new ResourcesCampComponentArtProvider();
            var componentContainer = screen.transform.FindUi(
                "campPanel/CampBg/DeckPart/ComponentContainer");
            Assert.IsNotNull(componentContainer);

            for (var index = 0; index < HeroComponentCatalog.Definitions.Count; index++)
            {
                var definition = HeroComponentCatalog.Definitions[index];
                Assert.IsTrue(provider.TryGetHeroComponentSprite(definition.Id, out var expected));
                var slot = componentContainer.GetChild(index);
                var componentUi = slot
                    .GetComponentsInChildren<Image>(true)
                    .Single(image => image.transform.parent == slot);
                Assert.AreNotSame(slot.GetComponent<Image>(), componentUi, definition.Id + " layers");
                Assert.AreSame(
                    expected,
                    componentUi.sprite,
                    definition.Id);
            }

            var collectionPart = screen.transform.FindUi("campPanel/CampBg/CollectionPart");
            Assert.IsNotNull(collectionPart);
            Assert.IsTrue(provider.TryGetHeroComponentSprite(
                DragonBoundComponentIds.SkyRanger,
                out var expectedTop));
            Assert.IsTrue(provider.TryGetHeroComponentSprite(
                DragonBoundComponentIds.ContractHatchling,
                out var expectedBottom));
            Assert.AreSame(expectedTop, collectionPart.FindUi("Img1").GetComponent<Image>().sprite);
            Assert.AreSame(expectedBottom, collectionPart.FindUi("Img2").GetComponent<Image>().sprite);

            var visibleHeroes = HeroDefinitionCatalog.Definitions
                .Where(hero => HeroDefinitionCatalog.GetMetadata(hero.Id).GalleryVisible)
                .ToArray();
            var heroContainer = collectionPart.FindUi("HeroContainer");
            Assert.IsNotNull(heroContainer);
            Assert.AreEqual(visibleHeroes.Length, screen.CampPanelView.HeroEntryCount);
            for (var index = 0; index < visibleHeroes.Length; index++)
            {
                var hero = visibleHeroes[index];
                Assert.IsTrue(provider.TryGetHeroSprite(hero.Id, out var expectedHero), hero.Id);
                var slot = heroContainer.GetChild(index);
                var heroUi = slot
                    .GetComponentsInChildren<Image>(true)
                    .Single(image => image.transform.parent == slot);
                Assert.AreNotSame(slot.GetComponent<Image>(), heroUi, hero.Id + " layers");
                Assert.AreSame(expectedHero, heroUi.sprite, hero.Id);
                Assert.AreEqual(Color.white, heroUi.color, hero.Id);
            }
        }

        [UnityTest]
        public IEnumerator GreyboxMainInitializesIndependentPlayerAndAiBattlefields()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = FindBootstrap();
            Assert.AreEqual(1f, Time.timeScale, 0.0001f);
            var initializedScreen = FindScreen();
            Assert.IsNotNull(initializedScreen.PlayerBattlefieldView.CombatFxView);
            Assert.IsNotNull(initializedScreen.AiBattlefieldView.CombatFxView);
            Assert.AreEqual(
                TeamSide.Player,
                initializedScreen.PlayerBattlefieldView.CombatFxView.Side);
            Assert.AreEqual(
                TeamSide.AI,
                initializedScreen.AiBattlefieldView.CombatFxView.Side,
                "The authored AI CombatFxView must not retain the Player enum default.");
            while (bootstrap.Match.State == MatchState.Ready)
            {
                Assert.AreEqual(0, bootstrap.Match.Player.RemainingEnemyCount);
                Assert.AreEqual(0, bootstrap.Match.AI.RemainingEnemyCount);
                Assert.AreEqual(0, bootstrap.AiRecruitment.CompletedRecruitments,
                    "AI must not recruit during initialization/Ready.");
                Assert.AreEqual(0, bootstrap.AiRecruitDestination.TotalObjectCount,
                    "AI board must remain unchanged until the Run is active.");
                yield return null;
            }

            float aiDecisionDeadline = Time.realtimeSinceStartup + 3f;
            while (bootstrap.AiRecruitment.CompletedRecruitments == 0 &&
                   Time.realtimeSinceStartup < aiDecisionDeadline)
            {
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
            Assert.AreEqual(AiStrategyProfileId.Beginner, bootstrap.AiProfileId);
            Assert.IsNotNull(bootstrap.AiDecisionScheduler);
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
            var rangeOutline = range.transform.FindUi("ART_RangeOutline").GetComponent<Image>();
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
        public IEnumerator BenchBasicUnitTapShowsRangeAndCompactInformPanel()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = FindBootstrap();
            var recruit = bootstrap.Recruitment.TryRecruit();
            Assert.AreEqual(RecruitmentStatus.Success, recruit.Status);
            var basicCard = recruit.Batch.Cards.First(card => card.Kind == RecruitItemKind.BasicUnit);
            Assert.IsTrue(bootstrap.PlayerBoard.TryGetPosition(basicCard.RuntimeId, out var position));
            Assert.AreEqual(CellType.Bench, GetCellType(bootstrap.PlayerBoard, position));

            bootstrap.BoardView.RefreshUnits();
            var benchCell = bootstrap.BoardView.GetCellView(position);
            Assert.IsNotNull(benchCell);
            var beachItem = benchCell.transform.FindUi("BeachItem")?.GetComponent<DraggableUnitView>();
            Assert.IsNotNull(beachItem, "The occupied BeachContainer slot must expose its unit input view.");
            Assert.IsNotNull(beachItem.GetComponent<IPointerClickHandler>(),
                "BeachItem must consume PointerClick instead of passing it to its GridCellView.");
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, beachItem.RectTransform.position)
            };
            beachItem.OnPointerDown(pointer);
            beachItem.OnPointerUp(pointer);
            ExecuteEvents.Execute<IPointerClickHandler>(
                beachItem.gameObject,
                pointer,
                ExecuteEvents.pointerClickHandler);
            yield return null;

            Assert.IsTrue(bootstrap.BoardView.RangePreview.enabled);
            var inform = Object.FindObjectOfType<UnitInformController>(true);
            Assert.IsNotNull(inform);
            Assert.IsTrue(inform.gameObject.activeSelf);
            Assert.AreEqual(115f, ((RectTransform)inform.transform).rect.height, 0.01f);
            Assert.AreEqual(
                $"{BasicUnitCatalog.GetDisplayName(basicCard.ConfigId)} Lv{basicCard.Level} (Unit)",
                ReadTmpText(inform.transform.FindUi("Name")));
            Assert.AreEqual(
                $"Max Lv{BasicUnitCatalog.MaxLevel}",
                ReadTmpText(inform.transform.FindUi("MaxLv")));
            Assert.IsFalse(inform.transform.FindUi("EXP").gameObject.activeSelf);
            Assert.IsFalse(inform.transform.FindUi("device").gameObject.activeSelf);
            Assert.IsFalse(inform.transform.FindUi("Rune").gameObject.activeSelf);
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
            var inputReceiver = emptyCell.transform.FindUi("InputReceiver") as RectTransform;
            Assert.IsNotNull(inputReceiver);
            Assert.AreEqual(new Vector2(6f, 6f), inputReceiver.offsetMin);
            Assert.AreEqual(new Vector2(-6f, -6f), inputReceiver.offsetMax);
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
                .FindUi("ART_FixedBoardCellLayer/BoardBackgroundClickSurface")
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

            var receiver = FindScreen().transform.FindUi("RangeDismissSurface")
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
            Assert.AreEqual(0, bootstrap.BoardView.VisibleBeachSelectionCount);
            Assert.IsTrue(bootstrap.BoardView.IsSourceSelectVisible);
            var sourceCell = bootstrap.BoardView.GetCellView(battle);
            var sourceSelection = sourceCell.ContentAnchor.FindUi("BeachSourceSelect");
            Assert.IsNotNull(sourceSelection);
            Assert.AreSame(
                DragonBound.Presentation.UiAssets.Load<Sprite>("GameUI/BeachSelect"),
                sourceSelection.GetComponent<Image>().sprite);

            var mapTarget = bootstrap.PlayerBoard
                .GetPositions(CellType.Battle)
                .First(position => !position.Equals(battle));
            var mapTargetCell = bootstrap.BoardView.GetCellView(mapTarget);
            var mapTargetScreenPosition = RectTransformUtility.WorldToScreenPoint(
                null,
                mapTargetCell.ContentAnchor.position);
            bootstrap.BoardView.UpdateDraggedUnit(unitId, mapTargetScreenPosition);
            Assert.IsTrue(bootstrap.BoardView.IsSourceSelectVisible);
            Assert.IsTrue(bootstrap.BoardView.IsBoardSelectVisible);
            var mapTargetSelection = mapTargetCell.ContentAnchor.FindUi("BoardSelect");
            Assert.IsNotNull(mapTargetSelection);
            Assert.AreSame(
                DragonBound.Presentation.UiAssets.Load<Sprite>("GameUI/BoardSelect"),
                mapTargetSelection.GetComponent<Image>().sprite);

            var benchCell = bootstrap.BoardView.GetCellView(basicOrigin);
            var benchScreenPosition = RectTransformUtility.WorldToScreenPoint(
                null,
                benchCell.ContentAnchor.position);
            bootstrap.BoardView.UpdateDraggedUnit(unitId, benchScreenPosition);
            Assert.AreEqual(0, bootstrap.BoardView.VisibleBeachSelectionCount);
            Assert.IsTrue(bootstrap.BoardView.IsSourceSelectVisible);
            Assert.IsTrue(bootstrap.BoardView.IsBoardSelectVisible);
            var benchSelection = benchCell.ContentAnchor.FindUi("BoardSelect");
            Assert.IsNotNull(benchSelection);
            Assert.IsTrue(benchSelection.gameObject.activeSelf);
            Assert.AreSame(
                DragonBound.Presentation.UiAssets.Load<Sprite>("GameUI/BoardSelect"),
                benchSelection.GetComponent<Image>().sprite);

            bootstrap.BoardView.UpdateDraggedUnit(
                unitId,
                new Vector2(-10000f, -10000f));
            Assert.IsTrue(
                bootstrap.BoardView.IsBoardSelectVisible,
                "Leaving the recruit area must retain the last valid recruit selection.");

            bootstrap.BoardView.CompleteDrag(unitId, benchScreenPosition);

            Assert.IsFalse(bootstrap.RecruitDestination.IsCombatSuspended(unitId));
            Assert.AreEqual(0, bootstrap.BoardView.VisibleBeachSelectionCount);
            Assert.IsFalse(bootstrap.BoardView.IsSourceSelectVisible);
            Assert.IsFalse(bootstrap.BoardView.IsBoardSelectVisible);
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

            var expectedShovelSprite = DragonBound.Presentation.UiAssets.Load<Sprite>("ComponentUI/shovel");
            Assert.IsNotNull(expectedShovelSprite);
            var beachContainer = FindScreen().transform.FindUi("ART_ScreenBackground/BeachContainer");
            Assert.IsNotNull(beachContainer);
            var shovelView = beachContainer
                .GetComponentsInChildren<DraggableUnitView>(true)
                .Single(view => view.gameObject.activeSelf && view.ArtImage.sprite == expectedShovelSprite);
            Assert.AreSame(expectedShovelSprite, shovelView.ArtImage.sprite);
            Assert.AreEqual(Color.white, shovelView.ArtImage.color);

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
            Assert.IsTrue(
                bootstrap.BoardView.UnitLayer
                    .GetComponentsInChildren<DraggableUnitView>(true)
                    .All(view => !view.IsArtMirrored),
                "Player basic units and hero components must retain their authored orientation.");
            Assert.IsTrue(
                bootstrap.AiBoardView.UnitLayer
                    .GetComponentsInChildren<DraggableUnitView>(true)
                    .All(view => view.IsArtMirrored),
                "AI basic units and hero components must mirror the player art orientation.");
            Assert.IsTrue(
                bootstrap.AiBoardView.UnitLayer
                    .GetComponentsInChildren<DraggableUnitView>(true)
                    .All(view => Mathf.Approximately(
                        -45f,
                        view.ArtImage.rectTransform.anchoredPosition.x)),
                "Every AI ART_UnitPortrait must use anchored Pos X=-45.");
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

        private static string ReadTmpText(Transform target)
        {
            Assert.IsNotNull(target);
            var component = target.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(component);
            var property = component.GetType().GetProperty("text");
            Assert.IsNotNull(property);
            return property.GetValue(component) as string;
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
