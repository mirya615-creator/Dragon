using System.Collections.Generic;
using System.Linq;
using DragonBound.Bootstrap;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DragonBound.Tests.EditMode
{
    public sealed class PortraitUiPrefabTests
    {
        private const string ScreenPath =
            "Assets/DragonBound/UI/Prefabs/Screens/DragonBoundPortraitScreen.prefab";
        private const string BattlefieldPath = "Assets/DragonBound/UI/Prefabs/Modules/Battlefield.prefab";
        private const string BenchPath = "Assets/DragonBound/UI/Prefabs/Modules/Bench.prefab";
        private const string RecruitmentPath = "Assets/DragonBound/UI/Prefabs/Modules/Recruitment.prefab";
        private const string UnitCardPath = "Assets/DragonBound/UI/Prefabs/Components/UnitCard.prefab";
        private const string HeroFormationPath = "Assets/DragonBound/UI/Prefabs/Components/HeroFormation.prefab";
        private const string RangeOutlinePath = "Assets/DragonBound/UI/Art/Range/RangeOutlineThin.png";
        private const string BoardCellPath = "Assets/DragonBound/UI/Prefabs/Components/BoardCell.prefab";
        private const string BenchSlotPath = "Assets/DragonBound/UI/Prefabs/Components/BenchSlot.prefab";
        private const string ScenePath = "Assets/Scenes/Greybox_Main.unity";

        [Test]
        public void PortraitBandsMatchFrozenDualBattlefieldLayout()
        {
            Assert.AreEqual(new Vector2(1080f, 1920f), PortraitLayoutMetrics.ReferenceResolution);
            var layout = PortraitLayoutMetrics.Calculate(
                new Rect(Vector2.zero, PortraitLayoutMetrics.ReferenceResolution));

            Assert.AreEqual(0.11f, layout.TopHud.height / layout.Bounds.height, 0.0001f);
            Assert.AreEqual(0.33f, layout.AiField.height / layout.Bounds.height, 0.0001f);
            Assert.AreEqual(0.33f, layout.PlayerField.height / layout.Bounds.height, 0.0001f);
            Assert.AreEqual(0.10f, layout.BenchBand.height / layout.Bounds.height, 0.0001f);
            Assert.AreEqual(0.10f, layout.CallToActionBand.height / layout.Bounds.height, 0.0001f);
            Assert.AreEqual(0.13f, layout.CallToActionBand.height / layout.Bounds.height, 0.0001f);
            Assert.AreEqual(layout.Bounds.yMin, layout.CallToActionBand.yMin, 0.01f);
            Assert.AreEqual(layout.CallToActionBand.yMax, layout.BenchBand.yMin, 0.01f);
            Assert.AreEqual(layout.BenchBand.yMax, layout.PlayerField.yMin, 0.01f);
            Assert.AreEqual(layout.PlayerField.yMax, layout.AiField.yMin, 0.01f);
            Assert.AreEqual(layout.AiField.yMax, layout.TopHud.yMin, 0.01f);
        }

        [TestCase(720f, 1280f)]
        [TestCase(1080f, 1920f)]
        [TestCase(1080f, 2280f)]
        public void FormationCellsAndRoadsRemainSeparatedAcrossPortraitSizes(float width, float height)
        {
            var layout = PortraitLayoutMetrics.Calculate(new Rect(0f, 0f, width, height));
            foreach (TeamSide side in System.Enum.GetValues(typeof(TeamSide)))
            {
                var cells = new List<Rect>();
                for (var y = 1; y <= 3; y++)
                {
                    for (var x = 0; x < 3; x++)
                    {
                        var current = layout.GetFormationCell(side, new GridPosition(x, y));
                        Assert.AreEqual(current.width, current.height, 0.01f);
                        foreach (var existing in cells)
                        {
                            Assert.IsFalse(existing.Overlaps(current), $"{side} cells overlap: {existing} / {current}");
                        }

                        Assert.IsFalse(layout.GetRoad(side, false).Overlaps(current));
                        Assert.IsFalse(layout.GetRoad(side, true).Overlaps(current));
                        cells.Add(current);
                    }
                }

                Assert.AreEqual(9, cells.Count);
            }

            Assert.Less(
                layout.GetFormationCell(TeamSide.Player, new GridPosition(1, 3)).center.y,
                layout.GetFormationCell(TeamSide.Player, new GridPosition(1, 1)).center.y);
            Assert.Greater(
                layout.GetFormationCell(TeamSide.AI, new GridPosition(1, 3)).center.y,
                layout.GetFormationCell(TeamSide.AI, new GridPosition(1, 1)).center.y);
        }

        [Test]
        public void FiveBenchSlotsAreEqualWidthAndDoNotOverlap()
        {
            var layout = PortraitLayoutMetrics.Calculate(
                new Rect(Vector2.zero, PortraitLayoutMetrics.ReferenceResolution));
            var first = layout.GetBenchSlot(0);
            var previous = first;
            for (var index = 1; index < 5; index++)
            {
                var current = layout.GetBenchSlot(index);
                Assert.AreEqual(first.width, current.width, 0.01f);
                Assert.Greater(current.xMin, previous.xMax);
                previous = current;
            }

            Assert.AreEqual(layout.Bounds.width * 0.15f, first.xMin, 0.01f);
            Assert.AreEqual(layout.Bounds.width * 0.85f, previous.xMax, 0.01f);
        }

        [Test]
        public void EditablePrefabAssetsExistAndAllImagesExposeArtNamedSlots()
        {
            var paths = new[]
            {
                ScreenPath,
                BattlefieldPath,
                BenchPath,
                RecruitmentPath,
                UnitCardPath,
                HeroFormationPath,
                BoardCellPath,
                BenchSlotPath
            };

            foreach (var path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, path);
                foreach (var image in prefab.GetComponentsInChildren<Image>(true))
                {
                    StringAssert.StartsWith("ART_", image.gameObject.name, $"{path}: {image.name}");
                }
            }
        }

        [Test]
        public void UnitCardRootUsesFixedAnchors()
        {
            var unitCard = AssetDatabase.LoadAssetAtPath<GameObject>(UnitCardPath);
            Assert.IsNotNull(unitCard);
            var rect = unitCard.GetComponent<RectTransform>();
            Assert.AreEqual(rect.anchorMin, rect.anchorMax, "UnitCard must not stretch across its unit layer.");
            Assert.Greater(rect.sizeDelta.x, 0f);
            Assert.Greater(rect.sizeDelta.y, 0f);

            var soulChainOverlay = unitCard.transform.Find("ART_SoulChainOverlay")?.GetComponent<Image>();
            Assert.IsNotNull(soulChainOverlay);
            Assert.IsFalse(soulChainOverlay.raycastTarget);
            Assert.AreEqual(0f, soulChainOverlay.color.a, 0.0001f);

        }

        [Test]
        public void HeroSlicePresentationUsesEditablePrefabHooks()
        {
            var unitCard = AssetDatabase.LoadAssetAtPath<GameObject>(UnitCardPath);
            var formation = AssetDatabase.LoadAssetAtPath<GameObject>(HeroFormationPath);
            Assert.IsNotNull(unitCard);
            Assert.IsNotNull(formation);

            var cardView = unitCard.GetComponent<DraggableUnitView>();
            Assert.IsNotNull(cardView.HeroLevelLabel);
            var formationView = formation.GetComponent<HeroFormationView>();
            Assert.IsNotNull(formationView);
            Assert.IsNotNull(formationView.RuneImage);
            Assert.AreEqual("RuneImg", formationView.RuneImage.name);
            Assert.IsFalse(formationView.RuneImage.raycastTarget);
            Assert.IsTrue(formationView.RuneImage.preserveAspect);
            Assert.IsFalse(formationView.RuneImage.gameObject.activeSelf);

            var screen = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath);
            var screenView = screen.GetComponent<DragonBoundScreenView>();
            foreach (var board in new[] { screenView.PlayerBoardView, screenView.AiBoardView })
            {
                Assert.IsNotNull(board.HeroPrefab);
                Assert.IsNotNull(board.HeroFormationEffectPrefab);
            }
        }

        [TestCase("Might", 1)]
        [TestCase("Farreach", 2)]
        [TestCase("Power", 3)]
        [TestCase("Longshot", 4)]
        [TestCase("Frostbite", 5)]
        [TestCase("Ricochet", 6)]
        [TestCase("Volley", 7)]
        [TestCase("BladeTempest", 8)]
        [TestCase("Ambush", 9)]
        [TestCase("Windhawk", 10)]
        [TestCase("Skybreaker", 11)]
        [TestCase("Wyrmguard", 12)]
        [TestCase("Dragonbloom", 13)]
        [TestCase("Warcry", 14)]
        public void RuntimeRuneResolvesToExpectedUiResourcePath(string runtimeRuneId, int resourceNumber)
        {
            Assert.AreEqual($"RuneUI/{resourceNumber}", RuneUiSpriteCatalog.GetResourcePath(runtimeRuneId));
        }

        [Test]
        public void FormationShowsEquippedRuneAndHidesEmptyRune()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeroFormationPath);
            var instance = Object.Instantiate(prefab);
            try
            {
                var view = instance.GetComponent<HeroFormationView>();
                view.SetRune("Ricochet");
                Assert.IsTrue(view.RuneImage.gameObject.activeSelf);
                Assert.AreEqual(RuneUiSpriteCatalog.Load("Ricochet"), view.RuneImage.sprite);

                view.SetRune(string.Empty);
                Assert.IsFalse(view.RuneImage.gameObject.activeSelf);
                Assert.IsNull(view.RuneImage.sprite);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ScreenPrefabSeparatesPlayerAndAiBoards()
        {
            var screen = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath);
            Assert.IsNotNull(screen);
            var view = screen.GetComponent<DragonBoundScreenView>();
            Assert.IsNotNull(view);
            Assert.IsNotNull(view.PlayerBattlefieldView);
            Assert.IsNotNull(view.AiBattlefieldView);
            Assert.AreEqual(TeamSide.Player, view.PlayerBattlefieldView.Side);
            Assert.AreEqual(TeamSide.AI, view.AiBattlefieldView.Side);
            Assert.AreEqual("AI", view.AiBattlefieldView.transform.Find("SideLabel").GetComponent<Text>().text);
            Assert.AreEqual("PLAYER", view.PlayerBattlefieldView.transform.Find("SideLabel").GetComponent<Text>().text);

            AssertBoard(view.AiBoardView, 9, 6, 3, 0, false);
            AssertBoard(view.PlayerBoardView, 14, 6, 3, 5, true);
            Assert.AreNotSame(view.PlayerBoardView, view.AiBoardView);
            Assert.AreNotSame(view.PlayerBoardView.UnitLayer, view.AiBoardView.UnitLayer);
        }

        [Test]
        public void BattlefieldPrefabContainsAuthoredGridExternalLaneAndStatus()
        {
            var battlefield = AssetDatabase.LoadAssetAtPath<GameObject>(BattlefieldPath);
            Assert.IsNotNull(battlefield);
            var cells = battlefield.GetComponentsInChildren<GridCellView>(true);
            Assert.AreEqual(9, cells.Length);
            Assert.AreEqual(6, cells.Count(cell => cell.CellType == CellType.Battle));
            Assert.AreEqual(3, cells.Count(cell => cell.CellType == CellType.Locked));

            Assert.IsNotNull(battlefield.transform.Find("ART_PathLeft"));
            Assert.IsNotNull(battlefield.transform.Find("ART_PathRight"));
            Assert.IsNotNull(battlefield.transform.Find("ART_PathTop"));
            Assert.IsNotNull(battlefield.transform.Find("ART_PathBottom"));
            Assert.IsNotNull(battlefield.transform.Find("ART_Spawn"));
            Assert.IsNotNull(battlefield.transform.Find("ART_Hatchling"));
            Assert.IsNotNull(battlefield.transform.Find("RouteWaypoints/DragonGoal"));
            Assert.IsNotNull(battlefield.transform.Find("ART_EnemyMarker/ART_EnemyHpTrack/ART_EnemyHpFill"));
            Assert.IsNotNull(battlefield.transform.Find("ART_EnemyMarker/EnemyRuntimeLabel"));
            Assert.IsNotNull(battlefield.transform.Find("ART_EnemyMarker").GetComponent<EnemyView>());
            Assert.IsNotNull(battlefield.GetComponent<CombatFxView>());
            Assert.IsNotNull(battlefield.transform.Find("ART_AttackLine"));
            Assert.IsNotNull(battlefield.transform.Find("ART_BowProjectile"));
            Assert.IsNotNull(battlefield.transform.Find("ART_SpearPierceLine"));
            Assert.IsNotNull(battlefield.transform.Find("ART_RiderSweepCircle"));
            var starfallWarning = battlefield.transform.Find("ART_StarfallWarning")?.GetComponent<Image>();
            Assert.IsNotNull(starfallWarning);
            Assert.IsFalse(starfallWarning.raycastTarget);
            Assert.IsNotNull(battlefield.transform.Find("DamageNumber"));
            Assert.IsNotNull(battlefield.transform.Find("SuppliesGain"));
            Assert.IsNotNull(battlefield.GetComponent<GreyboxBattlefieldSideView>());
            Assert.AreEqual(5, battlefield.GetComponent<GreyboxLaneView>().WaypointCount);
            Assert.AreEqual("DragonGoal", battlefield.GetComponent<GreyboxLaneView>().GoalNodeName);
        }

        [TestCase(1920f)]
        [TestCase(1760f)]
        public void AuthoredBattlefieldCellsAndRoadsRemainSeparatedWhenSafeAreaHeightChanges(float safeAreaHeight)
        {
            var battlefield = AssetDatabase.LoadAssetAtPath<GameObject>(BattlefieldPath);
            var parentSize = new Vector2(
                PortraitLayoutMetrics.ReferenceResolution.x,
                safeAreaHeight * 0.29f);
            var cellRects = battlefield.GetComponentsInChildren<GridCellView>(true)
                .Select(cell => ResolveRect(cell.RectTransform, parentSize))
                .ToArray();
            var roads = new[]
            {
                battlefield.transform.Find("ART_PathLeft").GetComponent<RectTransform>(),
                battlefield.transform.Find("ART_PathRight").GetComponent<RectTransform>(),
                battlefield.transform.Find("ART_PathTop").GetComponent<RectTransform>(),
                battlefield.transform.Find("ART_PathBottom").GetComponent<RectTransform>()
            };

            for (var first = 0; first < cellRects.Length; first++)
            {
                for (var second = first + 1; second < cellRects.Length; second++)
                {
                    Assert.IsFalse(
                        cellRects[first].Overlaps(cellRects[second]),
                        $"Formation cells overlap at safe area height {safeAreaHeight}: " +
                        $"{cellRects[first]} / {cellRects[second]}");
                }
            }

            foreach (var road in roads)
            {
                var roadRect = ResolveRect(road, parentSize);
                foreach (var cellRect in cellRects)
                {
                    Assert.IsFalse(roadRect.Overlaps(cellRect), $"Road crosses formation cell: {road.name}");
                }
            }
        }

        [Test]
        public void ModulesContainFixedControlsAndNestedPrefabDependencies()
        {
            var bench = AssetDatabase.LoadAssetAtPath<GameObject>(BenchPath);
            var recruitment = AssetDatabase.LoadAssetAtPath<GameObject>(RecruitmentPath);
            Assert.AreEqual(5, bench.GetComponentsInChildren<GridCellView>(true).Length);
            Assert.AreEqual(1, recruitment.GetComponentsInChildren<Button>(true).Length);
            CollectionAssert.Contains(AssetDatabase.GetDependencies(BattlefieldPath), BoardCellPath);
            CollectionAssert.Contains(AssetDatabase.GetDependencies(BenchPath), BenchSlotPath);
            var screenDependencies = AssetDatabase.GetDependencies(ScreenPath);
            CollectionAssert.Contains(screenDependencies, BattlefieldPath);
            CollectionAssert.Contains(screenDependencies, BenchPath);
            CollectionAssert.Contains(screenDependencies, RecruitmentPath);
            CollectionAssert.Contains(screenDependencies, UnitCardPath);
        }

        [Test]
        public void RangePreviewsUseSeparateEditableFillAndOutlineSprites()
        {
            var screen = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath);
            var view = screen.GetComponent<DragonBoundScreenView>();
            foreach (var board in new[] { view.PlayerBoardView, view.AiBoardView })
            {
                var fill = board.RangePreview;
                Assert.IsNotNull(fill);
                Assert.IsNotNull(fill.sprite);
                Assert.IsTrue(fill.preserveAspect);
                Assert.LessOrEqual(fill.color.a, 0.08f);
                Assert.IsNull(fill.GetComponent<Outline>());
                Assert.AreEqual(fill.rectTransform.sizeDelta.x, fill.rectTransform.sizeDelta.y, 0.01f);

                var outline = fill.transform.Find("ART_RangeOutline")?.GetComponent<Image>();
                Assert.IsNotNull(outline);
                Assert.IsNotNull(outline.sprite);
                Assert.AreEqual(RangeOutlinePath, AssetDatabase.GetAssetPath(outline.sprite));
                Assert.GreaterOrEqual(outline.color.a, 0.5f);
                Assert.LessOrEqual(outline.color.a, 0.7f);
                Assert.IsTrue(outline.preserveAspect);
                Assert.IsFalse(outline.raycastTarget);
            }
        }

        [Test]
        public void GreyboxSceneUsesPortraitCanvasSafeAreaAndConnectedScreenPrefab()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var closeWhenDone = !scene.IsValid() || !scene.isLoaded;
            if (closeWhenDone)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            try
            {
                var bootstrap = FindInScene<DragonBoundBootstrap>(scene);
                var canvas = FindInScene<Canvas>(scene);
                var safeArea = FindInScene<SafeAreaFitter>(scene);
                Assert.IsNotNull(bootstrap);
                Assert.IsNotNull(canvas);
                Assert.IsNotNull(safeArea);

                var scaler = canvas.GetComponent<CanvasScaler>();
                Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
                Assert.AreEqual(PortraitLayoutMetrics.ReferenceResolution, scaler.referenceResolution);
                Assert.AreEqual(0.5f, scaler.matchWidthOrHeight, 0.0001f);

                var serializedBootstrap = new SerializedObject(bootstrap);
                var screenView = serializedBootstrap.FindProperty("screenView").objectReferenceValue as DragonBoundScreenView;
                Assert.IsNotNull(screenView);
                Assert.AreEqual(ScreenPath, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(screenView.gameObject));
                Assert.AreSame(canvas, screenView.PlayerBoardView.Canvas);
                Assert.AreSame(canvas, screenView.AiBoardView.Canvas);
            }
            finally
            {
                if (closeWhenDone)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void AssertBoard(
            GreyboxBoardView board,
            int total,
            int battle,
            int locked,
            int bench,
            bool interactive)
        {
            Assert.IsNotNull(board);
            Assert.AreEqual(total, board.CellViews.Count);
            Assert.AreEqual(battle, board.CellViews.Count(cell => cell.CellType == CellType.Battle));
            Assert.AreEqual(locked, board.CellViews.Count(cell => cell.CellType == CellType.Locked));
            Assert.AreEqual(bench, board.CellViews.Count(cell => cell.CellType == CellType.Bench));
            Assert.AreEqual(interactive, board.AllowInteraction);
            Assert.IsNotNull(board.UnitLayer);
            Assert.IsNotNull(board.RangePreview);

            var coordinates = new HashSet<GridPosition>();
            foreach (var cell in board.CellViews)
            {
                Assert.IsNotNull(cell);
                Assert.IsTrue(coordinates.Add(cell.Position), $"Duplicate board coordinate {cell.Position}");
                Assert.IsNotNull(cell.ArtImage);
                Assert.IsNotNull(cell.ContentAnchor);
            }
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static Rect ResolveRect(RectTransform rectTransform, Vector2 parentSize)
        {
            var min = Vector2.Scale(rectTransform.anchorMin, parentSize) + rectTransform.offsetMin;
            var max = Vector2.Scale(rectTransform.anchorMax, parentSize) + rectTransform.offsetMax;
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
