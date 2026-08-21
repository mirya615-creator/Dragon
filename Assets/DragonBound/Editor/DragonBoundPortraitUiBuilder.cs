using System;
using System.Collections.Generic;
using System.IO;
using DragonBound.Bootstrap;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DragonBound.Editor
{
    public static class DragonBoundPortraitUiBuilder
    {
        public const string ScreenPrefabPath =
            "Assets/DragonBound/UI/Prefabs/Screens/DragonBoundPortraitScreen.prefab";
        public const string HudPrefabPath =
            "Assets/DragonBound/UI/Prefabs/Modules/HUD.prefab";
        public const string BattlefieldPrefabPath =
            "Assets/DragonBound/UI/Prefabs/Modules/Battlefield.prefab";
        public const string VersusPrefabPath =
            "Assets/DragonBound/UI/Prefabs/Modules/Versus.prefab";
        public const string BenchPrefabPath =
            "Assets/DragonBound/UI/Prefabs/Modules/Bench.prefab";
        public const string RecruitmentPrefabPath =
            "Assets/DragonBound/UI/Prefabs/Modules/Recruitment.prefab";
        public const string HeroWorkshopPrefabPath =
            "Assets/DragonBound/UI/Prefabs/Modules/HeroWorkshop.prefab";
        public const string RuneLoadoutPrefabPath =
            "Assets/DragonBound/UI/Prefabs/Modules/RuneLoadout.prefab";
        public const string BoardCellPrefabPath =
            "Assets/DragonBound/UI/Prefabs/Components/BoardCell.prefab";
        public const string BenchSlotPrefabPath =
            "Assets/DragonBound/UI/Prefabs/Components/BenchSlot.prefab";
        public const string UnitCardPrefabPath =
            "Assets/DragonBound/UI/Prefabs/Components/UnitCard.prefab";
        public const string HeroFormationPrefabPath =
            "Assets/DragonBound/UI/Prefabs/Components/HeroFormation.prefab";
        public const string RangeOutlineSpritePath =
            "Assets/DragonBound/UI/Art/Range/RangeOutlineThin.png";
        public const string ScenePath = "Assets/DragonBound/Scenes/Greybox_Main.unity";
        public const string HeroSliceScenePath = "Assets/DragonBound/Scenes/HeroSlice_Main.unity";

        private static readonly Color ScreenColor = new Color(0.055f, 0.065f, 0.07f, 1f);
        private static readonly Color HudColor = new Color(0.10f, 0.11f, 0.13f, 0.98f);
        private static readonly Color AiFieldColor = new Color(0.14f, 0.18f, 0.19f, 1f);
        private static readonly Color PlayerFieldColor = new Color(0.10f, 0.16f, 0.16f, 1f);
        private static readonly Color CellColor = new Color(0.48f, 0.55f, 0.55f, 0.96f);
        private static readonly Color LockedColor = new Color(0.16f, 0.17f, 0.18f, 0.98f);
        private static readonly Color BenchColor = new Color(0.38f, 0.42f, 0.42f, 0.98f);
        private static readonly Color RoadColor = new Color(0.36f, 0.31f, 0.28f, 0.96f);
        private static readonly Color AccentColor = new Color(0.78f, 0.25f, 0.18f, 1f);
        private static readonly Color RangeFillColor = new Color(0.86f, 0.96f, 0.94f, 0.055f);
        private static readonly Color RangeOutlineColor = new Color(0.88f, 1f, 0.96f, 0.62f);

        [MenuItem("DragonBound/UI/Create Editable Portrait UI")]
        public static void BuildAll()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPrefabPath) != null)
            {
                Debug.LogWarning(
                    "DragonBound editable UI already exists. Use the explicit Rebuild command only when replacing prefab edits is intended.");
                return;
            }

            RebuildAll();
        }

        [MenuItem("DragonBound/UI/Rebuild Editable Portrait UI")]
        public static void RebuildAll()
        {
            EnsureFolders();
            PrepareRangeOutlineSprite();

            var unitCard = BuildUnitCardPrefab();
            var heroFormation = BuildHeroFormationPrefab();
            var boardCell = BuildCellPrefab(BoardCellPrefabPath, "BoardCell", CellColor);
            var benchSlot = BuildCellPrefab(BenchSlotPrefabPath, "BenchSlot", BenchColor);
            var hud = BuildHudPrefab();
            var battlefield = BuildBattlefieldPrefab(boardCell, unitCard, heroFormation);
            var versus = BuildVersusPrefab();
            var bench = BuildBenchPrefab(benchSlot);
            var recruitment = BuildRecruitmentPrefab();
            var workshop = BuildHeroWorkshopPrefab();
            var runeLoadout = BuildRuneLoadoutPrefab();
            var screen = BuildScreenPrefab(unitCard, hud, battlefield, versus, bench, recruitment, workshop, runeLoadout);
            BuildScene(screen, unitCard);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("DragonBound editable dual-battlefield UI prefabs and Greybox_Main were rebuilt.");
        }

        [MenuItem("DragonBound/UI/Upgrade Hero Workshop Only")]
        public static void UpgradeHeroWorkshopOnly()
        {
            EnsureFolders();
            var recruitment = BuildRecruitmentPrefab();
            var workshop = BuildHeroWorkshopPrefab();
            UpgradeDragArrowInBattlefieldPrefab();
            var root = PrefabUtility.LoadPrefabContents(ScreenPrefabPath);
            try
            {
                var existing = root.transform.Find("ART_HeroWorkshop");
                if (existing != null)
                {
                    UnityEngine.Object.DestroyImmediate(existing.gameObject);
                }

                var workshopObject = InstantiateNestedPrefab(workshop, root.transform);
                workshopObject.name = "ART_HeroWorkshop";
                SetStretch(workshopObject.GetComponent<RectTransform>());
                workshopObject.SetActive(false);
                workshopObject.transform.SetAsLastSibling();

                var screen = root.GetComponent<DragonBoundScreenView>();
                var unitCard = AssetDatabase.LoadAssetAtPath<GameObject>(UnitCardPrefabPath)
                    ?.GetComponent<DraggableUnitView>();
                var aiArrow = EnsureDragArrow(screen.AiBattlefieldView.transform);
                var playerArrow = EnsureDragArrow(screen.PlayerBattlefieldView.transform);
                if (unitCard == null || aiArrow == null || playerArrow == null)
                {
                    throw new InvalidOperationException("Editable screen drag-arrow references are incomplete.");
                }

                screen.AiBoardView.Configure(
                    null,
                    CopyCells(screen.AiBoardView.CellViews),
                    screen.AiBoardView.UnitLayer,
                    unitCard,
                    screen.AiBoardView.RangePreview,
                    false,
                    aiArrow);
                screen.PlayerBoardView.Configure(
                    null,
                    CopyCells(screen.PlayerBoardView.CellViews),
                    screen.PlayerBoardView.UnitLayer,
                    unitCard,
                    screen.PlayerBoardView.RangePreview,
                    true,
                    playerArrow);
                screen.Configure(
                    screen.AiBattlefieldView,
                    screen.PlayerBattlefieldView,
                    screen.HudView,
                    root.transform.Find("Recruitment").GetComponent<GreyboxRecruitmentPanel>(),
                    workshopObject.GetComponent<HeroWorkshopView>(),
                    screen.RuneLoadoutView);
                PrefabUtility.SaveAsPrefabAsset(root, ScreenPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            UpgradeScenePresentationReferences(ScenePath);
            UpgradeScenePresentationReferences(HeroSliceScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("DragonBound hero workshop upgraded without rebuilding scene layout.");
        }

        [MenuItem("DragonBound/UI/Upgrade Rune Loadout Only")]
        public static void UpgradeRuneLoadoutOnly()
        {
            EnsureFolders();
            BuildRecruitmentPrefab();
            var runeLoadout = BuildRuneLoadoutPrefab();
            var root = PrefabUtility.LoadPrefabContents(ScreenPrefabPath);
            try
            {
                var existing = root.transform.Find("ART_RuneLoadout");
                if (existing != null)
                {
                    UnityEngine.Object.DestroyImmediate(existing.gameObject);
                }

                var loadoutObject = InstantiateNestedPrefab(runeLoadout, root.transform);
                loadoutObject.name = "ART_RuneLoadout";
                SetStretch(loadoutObject.GetComponent<RectTransform>());
                loadoutObject.SetActive(false);
                loadoutObject.transform.SetAsLastSibling();
                var screen = root.GetComponent<DragonBoundScreenView>();
                screen.Configure(
                    screen.AiBattlefieldView,
                    screen.PlayerBattlefieldView,
                    screen.HudView,
                    screen.RecruitmentView,
                    screen.HeroWorkshopView,
                    loadoutObject.GetComponent<RuneLoadoutView>());
                PrefabUtility.SaveAsPrefabAsset(root, ScreenPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            UpgradeScenePresentationReferences(ScenePath);
            UpgradeScenePresentationReferences(HeroSliceScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("DragonBound Rune loadout greybox upgraded without rebuilding scene layout.");
        }

        [MenuItem("DragonBound/UI/Upgrade Combat FX Only")]
        public static void UpgradeCombatFxOnly()
        {
            var root = PrefabUtility.LoadPrefabContents(BattlefieldPrefabPath);
            try
            {
                var combatFx = root.GetComponent<CombatFxView>();
                if (combatFx == null)
                {
                    throw new InvalidOperationException("Battlefield prefab is missing CombatFxView.");
                }

                var warningTransform = root.transform.Find("ART_StarfallWarning");
                var warning = warningTransform != null
                    ? warningTransform.GetComponent<Image>()
                    : CreateCircleImage(
                        "ART_StarfallWarning",
                        root.transform,
                        new Color(0.72f, 0.78f, 0.94f, 0.20f));
                if (warning == null)
                {
                    throw new InvalidOperationException("Unable to create ART_StarfallWarning.");
                }

                SetCentered(warning.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(180f, 180f));
                warning.raycastTarget = false;
                combatFx.ConfigureStarfallWarning(warning);
                PrefabUtility.SaveAsPrefabAsset(root, BattlefieldPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("DragonBound combat FX prefab upgraded with editable ART_StarfallWarning.");
        }

        [MenuItem("DragonBound/UI/Upgrade Range Previews Only")]
        public static void UpgradeRangePreviewsOnly()
        {
            AssetDatabase.Refresh();
            PrepareRangeOutlineSprite();
            var outlineSprite = LoadRangeOutlineSprite();
            UpgradeRangePreviews(
                BattlefieldPrefabPath,
                outlineSprite,
                "LocalUnitLayer/ART_LocalRangePreview");
            UpgradeRangePreviews(
                ScreenPrefabPath,
                outlineSprite,
                "AiUnitLayer/ART_AiRangePreview",
                "PlayerUnitLayer/ART_PlayerRangePreview");
            AssetDatabase.SaveAssets();
            Debug.Log("DragonBound range previews upgraded without rebuilding other UI prefabs.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/DragonBound/UI");
            EnsureFolder("Assets/DragonBound/UI/Prefabs");
            EnsureFolder("Assets/DragonBound/UI/Prefabs/Screens");
            EnsureFolder("Assets/DragonBound/UI/Prefabs/Modules");
            EnsureFolder("Assets/DragonBound/UI/Prefabs/Components");
            EnsureFolder("Assets/DragonBound/Scenes");
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var name = Path.GetFileName(assetPath);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException($"Invalid asset folder path: {assetPath}");
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static GameObject BuildUnitCardPrefab()
        {
            var root = CreateRect("UnitCard", null);
            SetCentered(root.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(124f, 124f));
            var group = root.AddComponent<CanvasGroup>();

            var art = CreateImage("ART_UnitPortrait", root.transform, CellColor);
            SetStretch(art.rectTransform);
            art.raycastTarget = true;

            var border = CreateImage("ART_UnitBorder", root.transform, new Color(1f, 1f, 1f, 0.22f));
            SetStretch(border.rectTransform, new Vector2(-4f, -4f), new Vector2(4f, 4f));
            border.raycastTarget = false;

            var label = CreateText(
                "UnitLabel",
                root.transform,
                "AXE 1",
                new Vector2(0.05f, 0.05f),
                new Vector2(0.95f, 0.95f),
                28,
                TextAnchor.MiddleCenter);
            label.raycastTarget = false;

            var heroBorder = CreateImage("ART_HeroRarityBorder", root.transform, new Color(1f, 1f, 1f, 0.04f));
            SetStretch(heroBorder.rectTransform, new Vector2(-5f, -5f), new Vector2(5f, 5f));
            heroBorder.raycastTarget = false;
            var heroOutline = heroBorder.gameObject.AddComponent<Outline>();
            heroOutline.effectDistance = new Vector2(4f, -4f);
            heroOutline.effectColor = new Color(0.67f, 0.35f, 1f, 0.92f);
            heroOutline.useGraphicAlpha = false;

            var heroLevel = CreateText(
                "HeroLevelLabel",
                root.transform,
                "Lv1",
                new Vector2(0.04f, 0.70f),
                new Vector2(0.30f, 0.96f),
                19,
                TextAnchor.UpperLeft);
            heroLevel.raycastTarget = false;

            var heroExperienceFill = CreateImage(
                "ART_HeroExperienceFill",
                root.transform,
                new Color(0.45f, 0.92f, 0.88f, 0.88f));
            SetAnchors(heroExperienceFill.rectTransform, new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.12f));
            heroExperienceFill.type = Image.Type.Filled;
            heroExperienceFill.fillMethod = Image.FillMethod.Horizontal;
            heroExperienceFill.fillOrigin = 0;
            heroExperienceFill.fillAmount = 0f;
            heroExperienceFill.raycastTarget = false;

            var heroExperience = CreateText(
                "HeroExperienceLabel",
                root.transform,
                "XP 0/20",
                new Vector2(0.24f, 0.02f),
                new Vector2(0.96f, 0.22f),
                15,
                TextAnchor.LowerRight);
            heroExperience.raycastTarget = false;

            var view = root.AddComponent<DraggableUnitView>();
            view.Configure(art, label, group);
            view.ConfigureHeroPresentation(heroBorder, heroLevel, heroExperience, heroExperienceFill);
            heroBorder.gameObject.SetActive(false);
            heroLevel.gameObject.SetActive(false);
            heroExperience.gameObject.SetActive(false);
            heroExperienceFill.gameObject.SetActive(false);
            return SavePrefab(root, UnitCardPrefabPath);
        }

        private static GameObject BuildHeroFormationPrefab()
        {
            var root = CreateRect("HeroFormation", null);
            SetCentered(root.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(260f, 140f));
            var group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var border = CreateImage("ART_DoubleCellBorder", root.transform, new Color(0.67f, 0.35f, 1f, 0.06f));
            SetStretch(border.rectTransform);
            border.raycastTarget = false;
            var outline = border.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(5f, -5f);
            outline.effectColor = new Color(0.67f, 0.35f, 1f, 0.92f);
            outline.useGraphicAlpha = false;

            var connector = CreateImage("ART_ComponentConnector", root.transform, Color.white);
            SetCentered(connector.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(100f, 7f));
            connector.raycastTarget = false;

            var primaryFlash = CreateCircleImage("ART_PrimaryFlash", root.transform, new Color(1f, 1f, 1f, 0f));
            SetCentered(primaryFlash.rectTransform, new Vector2(0.25f, 0.5f), new Vector2(100f, 100f));
            primaryFlash.raycastTarget = false;
            var secondaryFlash = CreateCircleImage("ART_SecondaryFlash", root.transform, new Color(1f, 1f, 1f, 0f));
            SetCentered(secondaryFlash.rectTransform, new Vector2(0.75f, 0.5f), new Vector2(100f, 100f));
            secondaryFlash.raycastTarget = false;

            var heroName = CreateText(
                "HeroNameLabel",
                root.transform,
                "WINDCLAW RANGER",
                new Vector2(0.04f, 0.35f),
                new Vector2(0.96f, 0.72f),
                24,
                TextAnchor.MiddleCenter);
            heroName.raycastTarget = false;

            var view = root.AddComponent<HeroFormationView>();
            view.Configure(group, connector, primaryFlash, secondaryFlash, border, heroName);
            return SavePrefab(root, HeroFormationPrefabPath);
        }

        private static GameObject BuildCellPrefab(string path, string rootName, Color color)
        {
            var root = CreateRect(rootName, null);
            SetCentered(root.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(128f, 128f));

            var art = CreateImage("ART_CellSurface", root.transform, color);
            SetStretch(art.rectTransform);
            art.raycastTarget = false;

            var lockOverlay = CreateImage("ART_LockOverlay", root.transform, new Color(0.04f, 0.04f, 0.04f, 0.48f));
            SetStretch(lockOverlay.rectTransform);
            lockOverlay.raycastTarget = false;
            var lockLabel = CreateText(
                "LockLabel",
                lockOverlay.transform,
                "LOCK",
                new Vector2(0.08f, 0.08f),
                new Vector2(0.92f, 0.92f),
                24,
                TextAnchor.MiddleCenter);
            lockLabel.raycastTarget = false;

            var highlight = CreateImage("ART_Highlight", root.transform, new Color(0.30f, 0.92f, 0.42f, 0.72f));
            SetStretch(highlight.rectTransform, new Vector2(-5f, -5f), new Vector2(5f, 5f));
            highlight.raycastTarget = false;

            var anchor = CreateRect("ContentAnchor", root.transform).GetComponent<RectTransform>();
            SetStretch(anchor);

            var view = root.AddComponent<GridCellView>();
            view.Configure(0, 0, CellType.Locked, art, highlight, anchor);
            return SavePrefab(root, path);
        }

        private static GameObject BuildHudPrefab()
        {
            var root = CreateRect("HUD", null);
            SetStretch(root.GetComponent<RectTransform>());
            var hud = root.AddComponent<GreyboxHudView>();

            var backdrop = CreateImage("ART_HudBackdrop", root.transform, HudColor);
            SetStretch(backdrop.rectTransform);
            backdrop.raycastTarget = false;

            var pauseImage = CreateImage("ART_PauseButton", root.transform, new Color(0.25f, 0.28f, 0.29f, 1f));
            SetAnchors(pauseImage.rectTransform, new Vector2(0.025f, 0.18f), new Vector2(0.125f, 0.82f));
            pauseImage.raycastTarget = true;
            var pauseButton = pauseImage.gameObject.AddComponent<Button>();
            pauseButton.targetGraphic = pauseImage;
            var pauseLabel = CreateText("PauseLabel", pauseImage.transform, "II", Vector2.zero, Vector2.one, 30, TextAnchor.MiddleCenter);
            pauseLabel.raycastTarget = false;

            var resourceIcon = CreateCircleImage("ART_ResourceIcon", root.transform, new Color(0.92f, 0.82f, 0.52f, 1f));
            SetAnchors(resourceIcon.rectTransform, new Vector2(0.15f, 0.28f), new Vector2(0.21f, 0.72f));
            resourceIcon.raycastTarget = false;
            var resources = CreateText("ResourceLabel", root.transform, "20", new Vector2(0.215f, 0.20f), new Vector2(0.32f, 0.80f), 38, TextAnchor.MiddleLeft);

            var wave = CreateText("WaveLabel", root.transform, "INITIALIZING...", new Vector2(0.35f, 0.10f), new Vector2(0.65f, 0.90f), 34, TextAnchor.MiddleCenter);
            var recruitments = CreateText("RecruitmentLabel", root.transform, "Recruits: 0", new Vector2(0.69f, 0.20f), new Vector2(0.84f, 0.80f), 26, TextAnchor.MiddleCenter);
            var debug = CreateText(
                "DebugLabel",
                root.transform,
                "AI Supplies: 20   AI Recruit Count: 0\nAI Camp Count: 0   AI Deployed Count: 0\nAI Last Recruit Result: NONE\nPlayer Last Recruit Result: NONE",
                new Vector2(0.02f, 0.01f),
                new Vector2(0.98f, 0.23f),
                12,
                TextAnchor.UpperLeft);
            debug.raycastTarget = false;

            var enemyDebug = CreateText(
                "EnemyDebugLabel",
                root.transform,
                "AI ENEMY DEBUG\nNONE\nPLAYER ENEMY DEBUG\nNONE",
                new Vector2(0.52f, 0.01f),
                new Vector2(0.98f, 0.23f),
                10,
                TextAnchor.UpperLeft);
            enemyDebug.raycastTarget = false;

            var menu = CreateImage("ART_MenuButton", root.transform, new Color(0.25f, 0.28f, 0.29f, 1f));
            SetAnchors(menu.rectTransform, new Vector2(0.86f, 0.18f), new Vector2(0.96f, 0.82f));
            menu.raycastTarget = true;
            var menuButton = menu.gameObject.AddComponent<Button>();
            menuButton.targetGraphic = menu;
            var menuLabel = CreateText("MenuLabel", menu.transform, "...", Vector2.zero, Vector2.one, 32, TextAnchor.MiddleCenter);
            menuLabel.raycastTarget = false;

            hud.Configure(pauseButton, pauseLabel, resources, wave, recruitments, null, null, debug, enemyDebug);
            return SavePrefab(root, HudPrefabPath);
        }

        private static GameObject BuildBattlefieldPrefab(
            GameObject boardCellPrefab,
            GameObject unitCardPrefab,
            GameObject heroFormationPrefab)
        {
            var root = CreateRect("Battlefield", null);
            SetStretch(root.GetComponent<RectTransform>());
            var boardView = root.AddComponent<GreyboxBoardView>();
            var laneView = root.AddComponent<GreyboxLaneView>();
            var sideView = root.AddComponent<GreyboxBattlefieldSideView>();
            var combatFxView = root.AddComponent<CombatFxView>();

            var backdrop = CreateImage("ART_Background", root.transform, PlayerFieldColor);
            SetStretch(backdrop.rectTransform);
            backdrop.raycastTarget = false;

            CreateRoadSegment(root.transform, "ART_PathLeft", new Vector2(0.035f, 0.08f), new Vector2(0.14f, 0.86f), 90f);
            CreateRoadSegment(root.transform, "ART_PathRight", new Vector2(0.86f, 0.08f), new Vector2(0.965f, 0.86f), 270f);
            CreateRoadSegment(root.transform, "ART_PathTop", new Vector2(0.035f, 0.86f), new Vector2(0.965f, 0.89f), 0f);
            CreateRoadSegment(root.transform, "ART_PathBottom", new Vector2(0.035f, 0.04f), new Vector2(0.965f, 0.16f), 180f);

            var spawn = CreateImage("ART_Spawn", root.transform, new Color(0.46f, 0.50f, 0.44f, 1f));
            SetCentered(spawn.rectTransform, new Vector2(0.91f, 0.86f), new Vector2(94f, 70f));
            spawn.raycastTarget = false;
            CreateText("SpawnLabel", spawn.transform, "SPAWN", Vector2.zero, Vector2.one, 18, TextAnchor.MiddleCenter).raycastTarget = false;

            var hatchling = CreateImage("ART_Hatchling", root.transform, new Color(0.82f, 0.36f, 0.25f, 1f));
            SetCentered(hatchling.rectTransform, new Vector2(0.09f, 0.10f), new Vector2(100f, 74f));
            hatchling.raycastTarget = false;
            CreateText("HatchlingMarkerLabel", hatchling.transform, "DRAGON", Vector2.zero, Vector2.one, 16, TextAnchor.MiddleCenter).raycastTarget = false;

            var sideLabel = CreateText("SideLabel", root.transform, "PLAYER", new Vector2(0.16f, 0.90f), new Vector2(0.34f, 0.99f), 22, TextAnchor.MiddleLeft);
            var healthLabel = CreateText("HatchlingLabel", root.transform, "HATCHLING", new Vector2(0.35f, 0.90f), new Vector2(0.62f, 0.99f), 22, TextAnchor.MiddleCenter);
            var enemyProgress = CreateText("EnemyProgressLabel", root.transform, "ENEMIES 0", new Vector2(0.64f, 0.90f), new Vector2(0.84f, 0.99f), 22, TextAnchor.MiddleRight);

            var bossTrack = CreateImage("ART_BossTrack", root.transform, new Color(0.10f, 0.10f, 0.10f, 0.88f));
            SetAnchors(bossTrack.rectTransform, new Vector2(0.31f, 0.855f), new Vector2(0.69f, 0.88f));
            bossTrack.raycastTarget = false;
            var bossFill = CreateImage("ART_BossFill", bossTrack.transform, AccentColor);
            SetStretch(bossFill.rectTransform);
            bossFill.type = Image.Type.Filled;
            bossFill.fillMethod = Image.FillMethod.Horizontal;
            bossFill.fillOrigin = 0;
            bossFill.fillAmount = 0.6f;
            bossFill.raycastTarget = false;
            bossTrack.gameObject.SetActive(false);

            var cells = new List<GridCellView>();
            for (var y = 1; y <= 3; y++)
            {
                for (var x = 0; x < 3; x++)
                {
                    var type = y == 3 ? CellType.Locked : CellType.Battle;
                    cells.Add(CreateBattlefieldCell(boardCellPrefab, root.transform, x, y, type, TeamSide.Player));
                }
            }

            // This is an authored UI node. Artists can replace the shaft/head artwork in the saved prefab.
            var dragArrowRoot = CreateRect("ART_DragArrow", root.transform);
            SetCentered(dragArrowRoot.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(120f, 10f));
            var dragArrowShaft = CreateImage("ART_DragArrowShaft", dragArrowRoot.transform, new Color(0.96f, 0.84f, 0.28f, 0.94f));
            SetStretch(dragArrowShaft.rectTransform);
            dragArrowShaft.raycastTarget = false;
            var dragArrowHead = CreateText(
                "ART_DragArrowHead",
                dragArrowRoot.transform,
                ">",
                new Vector2(0.86f, 0f),
                Vector2.one,
                34,
                TextAnchor.MiddleRight);
            dragArrowHead.raycastTarget = false;
            var dragArrow = dragArrowRoot.AddComponent<DragArrowPreviewView>();
            dragArrow.Configure(dragArrowShaft, dragArrowHead);

            var localUnitLayer = CreateRect("LocalUnitLayer", root.transform).GetComponent<RectTransform>();
            SetStretch(localUnitLayer);
            var localRange = CreateRangePreview("ART_LocalRangePreview", localUnitLayer);
            SetCentered(localRange.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(200f, 200f));

            var routeRoot = CreateRect("RouteWaypoints", root.transform);
            SetStretch(routeRoot.GetComponent<RectTransform>());
            var waypointAnchors = new[]
            {
                new Vector2(0.91f, 0.875f),
                new Vector2(0.09f, 0.875f),
                new Vector2(0.09f, 0.55f),
                new Vector2(0.91f, 0.10f),
                new Vector2(0.09f, 0.10f)
            };
            var waypointNames = new[]
            {
                "Spawn",
                "PathPoint_1",
                "PathPoint_2",
                "PathPoint_3",
                "DragonGoal"
            };
            var waypoints = new RectTransform[waypointAnchors.Length];
            for (var index = 0; index < waypointAnchors.Length; index++)
            {
                waypoints[index] = CreateRect(waypointNames[index], routeRoot.transform).GetComponent<RectTransform>();
                SetCentered(waypoints[index], waypointAnchors[index], Vector2.zero);
            }

            var enemyMarker = CreateImage("ART_EnemyMarker", root.transform, new Color(0.72f, 0.20f, 0.16f, 1f));
            SetCentered(enemyMarker.rectTransform, waypointAnchors[0], new Vector2(66f, 66f));
            enemyMarker.raycastTarget = false;
            var enemyHpTrack = CreateImage("ART_EnemyHpTrack", enemyMarker.transform, new Color(0.08f, 0.08f, 0.08f, 0.92f));
            SetAnchors(enemyHpTrack.rectTransform, new Vector2(0.02f, 0.90f), new Vector2(0.98f, 1.08f));
            enemyHpTrack.raycastTarget = false;
            var enemyHpFill = CreateImage("ART_EnemyHpFill", enemyHpTrack.transform, new Color(0.26f, 0.84f, 0.35f, 1f));
            SetStretch(enemyHpFill.rectTransform);
            enemyHpFill.type = Image.Type.Filled;
            enemyHpFill.fillMethod = Image.FillMethod.Horizontal;
            enemyHpFill.fillOrigin = 0;
            enemyHpFill.fillAmount = 1f;
            enemyHpFill.raycastTarget = false;
            var enemyRuntimeLabel = CreateText("EnemyRuntimeLabel", enemyMarker.transform, "E", new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.90f), 24, TextAnchor.MiddleCenter);
            enemyRuntimeLabel.raycastTarget = false;
            var enemyView = enemyMarker.gameObject.AddComponent<EnemyView>();
            enemyView.Configure(enemyMarker, enemyHpFill, enemyRuntimeLabel);
            enemyMarker.gameObject.SetActive(false);

            var attackLine = CreateImage("ART_AttackLine", root.transform, new Color(1f, 0.86f, 0.30f, 0.92f));
            SetCentered(attackLine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(120f, 7f));
            attackLine.raycastTarget = false;
            var bowProjectile = CreateImage("ART_BowProjectile", root.transform, new Color(0.95f, 0.90f, 0.45f, 1f));
            SetCentered(bowProjectile.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(22f, 22f));
            bowProjectile.raycastTarget = false;
            var spearPierce = CreateImage("ART_SpearPierceLine", root.transform, new Color(0.42f, 0.82f, 1f, 0.92f));
            SetCentered(spearPierce.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(120f, 7f));
            spearPierce.raycastTarget = false;
            var riderSweep = CreateCircleImage("ART_RiderSweepCircle", root.transform, new Color(0.90f, 0.45f, 0.23f, 0.28f));
            SetCentered(riderSweep.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(132f, 132f));
            riderSweep.raycastTarget = false;
            var starfallWarning = CreateCircleImage("ART_StarfallWarning", root.transform, new Color(0.72f, 0.78f, 0.94f, 0.20f));
            SetCentered(starfallWarning.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(180f, 180f));
            starfallWarning.raycastTarget = false;
            var damageNumber = CreateText("DamageNumber", root.transform, "-1", Vector2.zero, Vector2.one, 24, TextAnchor.MiddleCenter);
            SetCentered(damageNumber.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(160f, 40f));
            damageNumber.color = new Color(1f, 0.38f, 0.28f, 1f);
            damageNumber.raycastTarget = false;
            var suppliesGain = CreateText("SuppliesGain", root.transform, "+1 Supplies", Vector2.zero, Vector2.one, 18, TextAnchor.MiddleCenter);
            SetCentered(suppliesGain.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(180f, 34f));
            suppliesGain.color = new Color(0.45f, 1f, 0.58f, 1f);
            suppliesGain.raycastTarget = false;
            combatFxView.Configure(
                laneView,
                boardView,
                attackLine,
                bowProjectile,
                spearPierce,
                riderSweep,
                damageNumber,
                suppliesGain,
                starfallWarning);

            laneView.Configure(enemyMarker.rectTransform, waypoints, 12f, false);
            boardView.Configure(
                null,
                cells.ToArray(),
                localUnitLayer,
                unitCardPrefab.GetComponent<DraggableUnitView>(),
                localRange,
                true,
                dragArrow);
            boardView.ConfigureHeroPresentation(
                unitCardPrefab.GetComponent<DraggableUnitView>(),
                heroFormationPrefab.GetComponent<HeroFormationView>());
            sideView.Configure(
                TeamSide.Player,
                boardView,
                laneView,
                sideLabel,
                healthLabel,
                enemyProgress,
                bossFill,
                combatFxView);
            return SavePrefab(root, BattlefieldPrefabPath);
        }

        private static GridCellView CreateBattlefieldCell(
            GameObject prefab,
            Transform parent,
            int x,
            int y,
            CellType type,
            TeamSide side)
        {
            var instance = InstantiateNestedPrefab(prefab, parent);
            instance.name = type == CellType.Locked ? $"LockedCell_{x}_{y}" : $"BattleCell_{x}_{y}";
            var view = instance.GetComponent<GridCellView>();
            ConfigureCellInstance(view, x, y, type, side);
            RecordInstanceOverrides(instance);
            return view;
        }

        private static void ConfigureCellInstance(GridCellView view, int x, int y, CellType type, TeamSide side)
        {
            var centerX = 0.50f + ((x - 1) * PortraitLayoutMetrics.FormationColumnStep);
            var rowOffset = (y - 1) * PortraitLayoutMetrics.FormationRowStep;
            var centerY = side == TeamSide.Player
                ? PortraitLayoutMetrics.PlayerFirstRowY - rowOffset
                : PortraitLayoutMetrics.AiFirstRowY + rowOffset;
            var size = Vector2.one * PortraitLayoutMetrics.FormationCellReferenceSize;
            SetCentered(view.RectTransform, new Vector2(centerX, centerY), size);

            var lockOverlay = view.transform.Find("ART_LockOverlay");
            if (lockOverlay != null)
            {
                lockOverlay.gameObject.SetActive(type == CellType.Locked);
            }

            view.ArtImage.color = type == CellType.Locked ? LockedColor : CellColor;
            view.Configure(x, y, type, view.ArtImage, view.HighlightImage, view.ContentAnchor);
        }

        private static void ConfigureBattlefieldInstance(GameObject instance, TeamSide side)
        {
            instance.name = side == TeamSide.Player ? "PlayerBattlefield" : "AiBattlefield";
            var background = instance.transform.Find("ART_Background").GetComponent<Image>();
            background.color = side == TeamSide.Player ? PlayerFieldColor : AiFieldColor;

            foreach (var cell in instance.GetComponentsInChildren<GridCellView>(true))
            {
                ConfigureCellInstance(cell, cell.Position.X, cell.Position.Y, cell.CellType, side);
            }

            var lane = instance.GetComponent<GreyboxLaneView>();
            var combatFx = instance.GetComponent<CombatFxView>();
            var points = instance.transform.Find("RouteWaypoints").GetComponentsInChildren<RectTransform>(true);
            var route = new List<RectTransform>();
            foreach (var point in points)
            {
                if (string.Equals(point.name, "Spawn", StringComparison.Ordinal) ||
                    string.Equals(point.name, "DragonGoal", StringComparison.Ordinal) ||
                    point.name.StartsWith("PathPoint_", StringComparison.Ordinal))
                {
                    route.Add(point);
                }
            }

            route.Sort((first, second) =>
            {
                if (string.Equals(first.name, "Spawn", StringComparison.Ordinal))
                {
                    return -1;
                }

                if (string.Equals(second.name, "Spawn", StringComparison.Ordinal))
                {
                    return 1;
                }

                if (string.Equals(first.name, "DragonGoal", StringComparison.Ordinal))
                {
                    return 1;
                }

                if (string.Equals(second.name, "DragonGoal", StringComparison.Ordinal))
                {
                    return -1;
                }

                return string.CompareOrdinal(first.name, second.name);
            });
            var marker = instance.transform.Find("ART_EnemyMarker").GetComponent<RectTransform>();
            lane.Configure(marker, route.ToArray(), 12f, false);

            var board = instance.GetComponent<GreyboxBoardView>();
            var sideView = instance.GetComponent<GreyboxBattlefieldSideView>();
            var sideLabel = instance.transform.Find("SideLabel").GetComponent<Text>();
            sideLabel.text = side == TeamSide.Player ? "PLAYER" : "AI";
            sideView.Configure(
                side,
                board,
                lane,
                sideLabel,
                instance.transform.Find("HatchlingLabel").GetComponent<Text>(),
                instance.transform.Find("EnemyProgressLabel").GetComponent<Text>(),
                instance.transform.Find("ART_BossTrack/ART_BossFill").GetComponent<Image>(),
                combatFx);
            RecordInstanceOverrides(instance);
        }

        private static void CreateRoadSegment(Transform parent, string name, Vector2 min, Vector2 max, float arrowRotation)
        {
            var road = CreateImage(name, parent, RoadColor);
            SetAnchors(road.rectTransform, min, max);
            road.raycastTarget = false;
            var arrow = CreateText(name + "_Arrow", road.transform, ">", Vector2.zero, Vector2.one, 24, TextAnchor.MiddleCenter);
            arrow.raycastTarget = false;
            arrow.color = new Color(1f, 1f, 1f, 0.76f);
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, arrowRotation);
        }

        private static GameObject BuildVersusPrefab()
        {
            var root = CreateRect("Versus", null);
            SetStretch(root.GetComponent<RectTransform>());
            var backdrop = CreateImage("ART_VersusBackdrop", root.transform, new Color(0.08f, 0.09f, 0.10f, 1f));
            SetStretch(backdrop.rectTransform);
            backdrop.raycastTarget = false;

            CreateText("AiHealthLabel", root.transform, "AI \u2665\u2665\u2665", new Vector2(0.04f, 0.15f), new Vector2(0.34f, 0.85f), 28, TextAnchor.MiddleLeft);
            CreateText("VersusLabel", root.transform, "VS", new Vector2(0.42f, 0.10f), new Vector2(0.58f, 0.90f), 38, TextAnchor.MiddleCenter);
            CreateText("PlayerHealthLabel", root.transform, "PLAYER \u2665\u2665\u2665", new Vector2(0.66f, 0.15f), new Vector2(0.96f, 0.85f), 28, TextAnchor.MiddleRight);
            return SavePrefab(root, VersusPrefabPath);
        }

        private static GameObject BuildBenchPrefab(GameObject benchSlotPrefab)
        {
            var root = CreateRect("Bench", null);
            SetStretch(root.GetComponent<RectTransform>());
            var backdrop = CreateImage("ART_BenchBackdrop", root.transform, new Color(0.08f, 0.10f, 0.10f, 1f));
            SetStretch(backdrop.rectTransform);
            backdrop.raycastTarget = false;

            var benchBadge = CreateImage("ART_BenchBadge", root.transform, new Color(0.22f, 0.24f, 0.24f, 1f));
            SetAnchors(benchBadge.rectTransform, new Vector2(0.02f, 0.15f), new Vector2(0.13f, 0.85f));
            benchBadge.raycastTarget = false;
            CreateText("BenchLabel", benchBadge.transform, "BENCH", Vector2.zero, Vector2.one, 18, TextAnchor.MiddleCenter).raycastTarget = false;

            const float rowMin = 0.15f;
            const float rowMax = 0.85f;
            const float gap = 0.006f;
            var width = (rowMax - rowMin - (gap * 4f)) / 5f;
            for (var x = 0; x < 5; x++)
            {
                var instance = InstantiateNestedPrefab(benchSlotPrefab, root.transform);
                instance.name = $"BenchSlot_{x}";
                var minX = rowMin + (x * (width + gap));
                SetAnchors(instance.GetComponent<RectTransform>(), new Vector2(minX, 0.14f), new Vector2(minX + width, 0.86f));
                var view = instance.GetComponent<GridCellView>();
                var lockOverlay = instance.transform.Find("ART_LockOverlay");
                if (lockOverlay != null)
                {
                    lockOverlay.gameObject.SetActive(false);
                }

                view.Configure(x, 0, CellType.Bench, view.ArtImage, view.HighlightImage, view.ContentAnchor);
                RecordInstanceOverrides(instance);
            }

            return SavePrefab(root, BenchPrefabPath);
        }

        private static GameObject BuildRecruitmentPrefab()
        {
            var root = CreateRect("Recruitment", null);
            SetStretch(root.GetComponent<RectTransform>());
            var panel = root.AddComponent<GreyboxRecruitmentPanel>();
            var backdrop = CreateImage("ART_RecruitmentBackdrop", root.transform, ScreenColor);
            SetStretch(backdrop.rectTransform);
            backdrop.raycastTarget = false;

            var status = CreateText("StatusLabel", root.transform, string.Empty, new Vector2(0.20f, 0.73f), new Vector2(0.80f, 0.98f), 22, TextAnchor.MiddleCenter);
            status.raycastTarget = false;

            var recruitImage = CreateImage("ART_RecruitButton", root.transform, AccentColor);
            SetAnchors(recruitImage.rectTransform, new Vector2(0.315f, 0.08f), new Vector2(0.685f, 0.72f));
            recruitImage.raycastTarget = true;
            var button = recruitImage.gameObject.AddComponent<Button>();
            button.targetGraphic = recruitImage;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.92f, 0.85f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.42f, 0.42f, 0.42f, 0.72f);
            button.colors = colors;

            var resourceIcon = CreateCircleImage("ART_ResourceIcon", recruitImage.transform, new Color(0.92f, 0.82f, 0.52f, 1f));
            SetAnchors(resourceIcon.rectTransform, new Vector2(0.16f, 0.28f), new Vector2(0.31f, 0.72f));
            resourceIcon.raycastTarget = false;
            var buttonLabel = CreateText("RecruitButtonLabel", recruitImage.transform, "RECRUIT\n10 Supplies", new Vector2(0.30f, 0.04f), new Vector2(0.96f, 0.96f), 30, TextAnchor.MiddleCenter);
            buttonLabel.raycastTarget = false;

            var workshopImage = CreateImage("ART_WorkshopButton", root.transform, new Color(0.20f, 0.39f, 0.43f, 1f));
            SetAnchors(workshopImage.rectTransform, new Vector2(0.05f, 0.14f), new Vector2(0.28f, 0.66f));
            workshopImage.raycastTarget = true;
            var workshopButton = workshopImage.gameObject.AddComponent<Button>();
            workshopButton.targetGraphic = workshopImage;
            var workshopLabel = CreateText("WorkshopLabel", workshopImage.transform, "作坊", Vector2.zero, Vector2.one, 22, TextAnchor.MiddleCenter);
            workshopLabel.raycastTarget = false;

            var runeImage = CreateImage("ART_RuneLoadoutButton", root.transform, new Color(0.35f, 0.28f, 0.58f, 1f));
            SetAnchors(runeImage.rectTransform, new Vector2(0.72f, 0.14f), new Vector2(0.95f, 0.66f));
            runeImage.raycastTarget = true;
            var runeButton = runeImage.gameObject.AddComponent<Button>();
            runeButton.targetGraphic = runeImage;
            var runeLabel = CreateText("RuneLoadoutLabel", runeImage.transform, "RUNES", Vector2.zero, Vector2.one, 20, TextAnchor.MiddleCenter);
            runeLabel.raycastTarget = false;

            panel.Configure(button, buttonLabel, status, workshopButton, runeButton);
            return SavePrefab(root, RecruitmentPrefabPath);
        }

        private static GameObject BuildHeroWorkshopPrefab()
        {
            var root = CreateRect("HeroWorkshop", null);
            SetStretch(root.GetComponent<RectTransform>());
            var view = root.AddComponent<HeroWorkshopView>();

            var dimmer = CreateImage("ART_WorkshopDim", root.transform, new Color(0f, 0f, 0f, 0.68f));
            SetStretch(dimmer.rectTransform);
            dimmer.raycastTarget = true;

            var panel = CreateImage("ART_WorkshopPanel", root.transform, new Color(0.09f, 0.13f, 0.15f, 0.98f));
            // Keep the workshop compact so the board remains the primary screen signal.
            SetAnchors(panel.rectTransform, new Vector2(0.12f, 0.20f), new Vector2(0.88f, 0.80f));

            var bookPage = CreateImage("ART_WorkshopBookPage", panel.transform, new Color(0.91f, 0.85f, 0.68f, 1f));
            SetAnchors(bookPage.rectTransform, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.82f));
            bookPage.raycastTarget = false;

            var closeImage = CreateImage("ART_WorkshopClose", panel.transform, new Color(0.39f, 0.20f, 0.20f, 1f));
            SetAnchors(closeImage.rectTransform, new Vector2(0.88f, 0.84f), new Vector2(0.98f, 0.96f));
            closeImage.raycastTarget = true;
            var closeButton = closeImage.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;
            var closeLabel = CreateText("CloseLabel", closeImage.transform, "X", Vector2.zero, Vector2.one, 28, TextAnchor.MiddleCenter);
            closeLabel.raycastTarget = false;

            var componentsTabImage = CreateImage("ART_ComponentsTab", panel.transform, new Color(0.19f, 0.45f, 0.49f, 1f));
            SetAnchors(componentsTabImage.rectTransform, new Vector2(0.23f, 0.85f), new Vector2(0.43f, 0.93f));
            componentsTabImage.raycastTarget = true;
            var componentsTab = componentsTabImage.gameObject.AddComponent<Button>();
            componentsTab.targetGraphic = componentsTabImage;
            var componentsTabLabel = CreateText("ComponentsTabLabel", componentsTabImage.transform, "组件库", Vector2.zero, Vector2.one, 24, TextAnchor.MiddleCenter);
            SetAnchors(componentsTabLabel.rectTransform, new Vector2(0.34f, 0f), new Vector2(0.94f, 1f));
            componentsTabLabel.fontSize = 18;
            componentsTabLabel.raycastTarget = false;
            var componentsTabIcon = CreateImage("ART_ComponentsTabIcon", componentsTabImage.transform, new Color(0.93f, 0.84f, 0.54f, 1f));
            SetAnchors(componentsTabIcon.rectTransform, new Vector2(0.09f, 0.20f), new Vector2(0.29f, 0.80f));
            componentsTabIcon.raycastTarget = false;

            var galleryTabImage = CreateImage("ART_GalleryTab", panel.transform, new Color(0.31f, 0.28f, 0.47f, 1f));
            SetAnchors(galleryTabImage.rectTransform, new Vector2(0.57f, 0.85f), new Vector2(0.77f, 0.93f));
            galleryTabImage.raycastTarget = true;
            var galleryTab = galleryTabImage.gameObject.AddComponent<Button>();
            galleryTab.targetGraphic = galleryTabImage;
            var galleryTabLabel = CreateText("GalleryTabLabel", galleryTabImage.transform, "英雄图鉴", Vector2.zero, Vector2.one, 24, TextAnchor.MiddleCenter);
            SetAnchors(galleryTabLabel.rectTransform, new Vector2(0.34f, 0f), new Vector2(0.94f, 1f));
            galleryTabLabel.fontSize = 18;
            galleryTabLabel.raycastTarget = false;
            var galleryTabIcon = CreateImage("ART_GalleryTabIcon", galleryTabImage.transform, new Color(0.93f, 0.84f, 0.54f, 1f));
            SetAnchors(galleryTabIcon.rectTransform, new Vector2(0.09f, 0.20f), new Vector2(0.29f, 0.80f));
            galleryTabIcon.raycastTarget = false;

            var runtimeStats = CreateText(
                "WorkshopBagStatsLabel",
                panel.transform,
                "配置 24  |  牌袋 24/24  |  已抽 0  |  已丢弃 0",
                new Vector2(0.12f, 0.78f),
                new Vector2(0.88f, 0.83f),
                12,
                TextAnchor.MiddleCenter);
            runtimeStats.raycastTarget = false;

            var componentsPage = CreateImage("ART_ComponentLibraryPage", panel.transform, new Color(0.96f, 0.91f, 0.75f, 0.97f));
            SetAnchors(componentsPage.rectTransform, new Vector2(0.10f, 0.07f), new Vector2(0.90f, 0.77f));
            componentsPage.raycastTarget = false;
            var componentsGrid = CreateRect("ART_ComponentGrid", componentsPage.transform);
            SetStretch(componentsGrid.GetComponent<RectTransform>());
            var componentLayout = componentsGrid.AddComponent<GridLayoutGroup>();
            componentLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            componentLayout.constraintCount = 4;
            componentLayout.cellSize = new Vector2(140f, 140f);
            componentLayout.spacing = new Vector2(8f, 8f);
            componentLayout.padding = new RectOffset(4, 4, 4, 4);
            componentLayout.childAlignment = TextAnchor.UpperCenter;
            var componentTemplate = CreateComponentEntryTemplate(componentsGrid.transform);

            var galleryPage = CreateImage("ART_HeroGalleryPage", panel.transform, new Color(0.96f, 0.91f, 0.75f, 0.97f));
            SetAnchors(galleryPage.rectTransform, new Vector2(0.10f, 0.07f), new Vector2(0.90f, 0.77f));
            galleryPage.raycastTarget = false;

            var detail = CreateImage("ART_HeroDetail", galleryPage.transform, new Color(0.83f, 0.74f, 0.52f, 0.98f));
            SetAnchors(detail.rectTransform, new Vector2(0.07f, 0.77f), new Vector2(0.93f, 0.94f));
            var detailPortrait = CreateImage("ART_HeroDetailPortrait", detail.transform, new Color(0.21f, 0.28f, 0.30f, 0.96f));
            SetAnchors(detailPortrait.rectTransform, new Vector2(0.05f, 0.16f), new Vector2(0.19f, 0.84f));
            detailPortrait.raycastTarget = false;
            var detailInfo = CreateImage("ART_HeroDetailInfo", detail.transform, new Color(0.94f, 0.89f, 0.73f, 0.72f));
            SetAnchors(detailInfo.rectTransform, new Vector2(0.23f, 0.12f), new Vector2(0.95f, 0.88f));
            detailInfo.raycastTarget = false;
            var detailName = CreateText("DetailNameLabel", detail.transform, "风爪游侠", new Vector2(0.26f, 0.58f), new Vector2(0.55f, 0.90f), 17, TextAnchor.MiddleCenter);
            var detailRarity = CreateText("DetailRarityLabel", detail.transform, "紫色", new Vector2(0.58f, 0.58f), new Vector2(0.92f, 0.90f), 15, TextAnchor.MiddleCenter);
            var detailFormation = CreateText("DetailFormationLabel", detail.transform, string.Empty, new Vector2(0.26f, 0.30f), new Vector2(0.92f, 0.56f), 12, TextAnchor.MiddleCenter);
            var detailSkill = CreateText("DetailSkillLabel", detail.transform, string.Empty, new Vector2(0.26f, 0.07f), new Vector2(0.92f, 0.29f), 12, TextAnchor.MiddleCenter);
            detailName.raycastTarget = false;
            detailRarity.raycastTarget = false;
            detailFormation.raycastTarget = false;
            detailSkill.raycastTarget = false;

            var heroGrid = CreateRect("ART_HeroGrid", galleryPage.transform);
            SetAnchors(heroGrid.GetComponent<RectTransform>(), new Vector2(0.10f, 0.07f), new Vector2(0.90f, 0.72f));
            var heroLayout = heroGrid.AddComponent<GridLayoutGroup>();
            heroLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            heroLayout.constraintCount = 3;
            heroLayout.cellSize = new Vector2(160f, 125f);
            heroLayout.spacing = new Vector2(8f, 8f);
            heroLayout.padding = new RectOffset(4, 4, 4, 4);
            heroLayout.childAlignment = TextAnchor.UpperCenter;
            var heroTemplate = CreateHeroEntryTemplate(heroGrid.transform);

            view.Configure(
                closeButton,
                componentsTab,
                galleryTab,
                componentsPage.gameObject,
                galleryPage.gameObject,
                runtimeStats,
                null,
                detailName,
                detailRarity,
                detailFormation,
                detailSkill,
                componentsGrid.transform,
                heroGrid.transform,
                componentTemplate,
                heroTemplate);
            root.SetActive(false);
            return SavePrefab(root, HeroWorkshopPrefabPath);
        }

        private static GameObject BuildRuneLoadoutPrefab()
        {
            var root = CreateRect("RuneLoadout", null);
            SetStretch(root.GetComponent<RectTransform>());
            var view = root.AddComponent<RuneLoadoutView>();

            var dimmer = CreateImage("ART_RuneLoadoutDim", root.transform, new Color(0f, 0f, 0f, 0.72f));
            SetStretch(dimmer.rectTransform);
            dimmer.raycastTarget = true;
            var panel = CreateImage("ART_RuneLoadoutPanel", root.transform, new Color(0.075f, 0.11f, 0.15f, 0.99f));
            SetAnchors(panel.rectTransform, new Vector2(0.04f, 0.12f), new Vector2(0.96f, 0.88f));

            var title = CreateText("RuneLoadoutTitle", panel.transform, "RUNE LOADOUT", new Vector2(0.06f, 0.90f), new Vector2(0.60f, 0.98f), 27, TextAnchor.MiddleLeft);
            title.raycastTarget = false;
            var gate = CreateText("RuneGateLabel", panel.transform, "LOCKED | UNLOCKS ON DAY 3", new Vector2(0.06f, 0.84f), new Vector2(0.78f, 0.90f), 14, TextAnchor.MiddleLeft);
            gate.raycastTarget = false;
            var validation = CreateText("RuneValidationLabel", panel.transform, string.Empty, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.10f), 14, TextAnchor.MiddleCenter);
            validation.raycastTarget = false;

            var closeImage = CreateImage("ART_RuneLoadoutClose", panel.transform, new Color(0.46f, 0.20f, 0.20f, 1f));
            SetAnchors(closeImage.rectTransform, new Vector2(0.90f, 0.90f), new Vector2(0.98f, 0.98f));
            var close = closeImage.gameObject.AddComponent<Button>();
            close.targetGraphic = closeImage;
            var closeLabel = CreateText("CloseLabel", closeImage.transform, "X", Vector2.zero, Vector2.one, 24, TextAnchor.MiddleCenter);
            closeLabel.raycastTarget = false;

            var filters = CreateRect("ART_RuneFilters", panel.transform);
            SetAnchors(filters.GetComponent<RectTransform>(), new Vector2(0.45f, 0.76f), new Vector2(0.95f, 0.84f));
            var filterLayout = filters.AddComponent<GridLayoutGroup>();
            filterLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            filterLayout.constraintCount = 5;
            filterLayout.cellSize = new Vector2(88f, 42f);
            filterLayout.spacing = new Vector2(4f, 0f);
            filterLayout.childAlignment = TextAnchor.MiddleCenter;
            var allFilter = CreateRuneButton("ART_RuneFilterAll", filters.transform, "ALL", new Color(0.23f, 0.36f, 0.42f, 1f));
            var commonFilter = CreateRuneButton("ART_RuneFilterCommon", filters.transform, "COMMON", new Color(0.18f, 0.45f, 0.28f, 1f));
            var excellentFilter = CreateRuneButton("ART_RuneFilterExcellent", filters.transform, "EXCELLENT", new Color(0.20f, 0.36f, 0.65f, 1f));
            var epicFilter = CreateRuneButton("ART_RuneFilterEpic", filters.transform, "EPIC", new Color(0.43f, 0.24f, 0.60f, 1f));
            var legendaryFilter = CreateRuneButton("ART_RuneFilterLegendary", filters.transform, "LEGEND", new Color(0.65f, 0.48f, 0.15f, 1f));

            var heroLabel = CreateText("SelectedHeroLabel", panel.transform, "HERO", new Vector2(0.06f, 0.76f), new Vector2(0.42f, 0.82f), 15, TextAnchor.MiddleLeft);
            var runeLabel = CreateText("SelectedRuneLabel", panel.transform, "SELECT A RUNE TO EQUIP", new Vector2(0.45f, 0.69f), new Vector2(0.94f, 0.75f), 14, TextAnchor.MiddleCenter);
            heroLabel.raycastTarget = false;
            runeLabel.raycastTarget = false;

            var unequip = CreateRuneButton("ART_RuneUnequip", panel.transform, "UNEQUIP", new Color(0.40f, 0.24f, 0.24f, 1f));
            SetAnchors(unequip.GetComponent<RectTransform>(), new Vector2(0.08f, 0.11f), new Vector2(0.28f, 0.17f));
            var craft = CreateRuneButton("ART_RuneCraft", panel.transform, "CRAFT", new Color(0.31f, 0.40f, 0.18f, 1f));
            SetAnchors(craft.GetComponent<RectTransform>(), new Vector2(0.66f, 0.11f), new Vector2(0.86f, 0.17f));

            var heroGrid = CreateRect("ART_RuneHeroGrid", panel.transform);
            SetAnchors(heroGrid.GetComponent<RectTransform>(), new Vector2(0.06f, 0.20f), new Vector2(0.41f, 0.74f));
            var heroLayout = heroGrid.AddComponent<GridLayoutGroup>();
            heroLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            heroLayout.constraintCount = 2;
            heroLayout.cellSize = new Vector2(160f, 90f);
            heroLayout.spacing = new Vector2(6f, 6f);
            heroLayout.childAlignment = TextAnchor.UpperCenter;
            var heroTemplate = CreateRuneLoadoutEntryTemplate("ART_RuneHeroEntryTemplate", heroGrid.transform);

            var runeGrid = CreateRect("ART_RuneGrid", panel.transform);
            SetAnchors(runeGrid.GetComponent<RectTransform>(), new Vector2(0.45f, 0.20f), new Vector2(0.94f, 0.67f));
            var runeLayout = runeGrid.AddComponent<GridLayoutGroup>();
            runeLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            runeLayout.constraintCount = 3;
            runeLayout.cellSize = new Vector2(150f, 94f);
            runeLayout.spacing = new Vector2(6f, 6f);
            runeLayout.childAlignment = TextAnchor.UpperCenter;
            var runeTemplate = CreateRuneLoadoutEntryTemplate("ART_RuneEntryTemplate", runeGrid.transform);

            view.Configure(
                close,
                title,
                gate,
                heroLabel,
                runeLabel,
                validation,
                unequip,
                craft,
                allFilter,
                commonFilter,
                excellentFilter,
                epicFilter,
                legendaryFilter,
                heroGrid.transform,
                runeGrid.transform,
                heroTemplate,
                runeTemplate);
            root.SetActive(false);
            return SavePrefab(root, RuneLoadoutPrefabPath);
        }

        private static Button CreateRuneButton(string name, Transform parent, string label, Color color)
        {
            var image = CreateImage(name, parent, color);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = CreateText("Label", image.transform, label, Vector2.zero, Vector2.one, 14, TextAnchor.MiddleCenter);
            text.raycastTarget = false;
            return button;
        }

        private static RuneLoadoutEntryView CreateRuneLoadoutEntryTemplate(string name, Transform parent)
        {
            var background = CreateImage(name, parent, new Color(0.18f, 0.25f, 0.29f, 0.98f));
            background.raycastTarget = true;
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var title = CreateText("Title", background.transform, "RUNE", new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.94f), 13, TextAnchor.MiddleCenter);
            var detail = CreateText("Detail", background.transform, string.Empty, new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.56f), 11, TextAnchor.MiddleCenter);
            var artKey = CreateText("ArtKey", background.transform, string.Empty, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.17f), 8, TextAnchor.MiddleCenter);
            title.raycastTarget = false;
            detail.raycastTarget = false;
            artKey.raycastTarget = false;
            var entry = background.gameObject.AddComponent<RuneLoadoutEntryView>();
            entry.Configure(button, background, title, detail, artKey);
            background.gameObject.SetActive(false);
            return entry;
        }

        private static HeroWorkshopComponentEntryView CreateComponentEntryTemplate(Transform parent)
        {
            var background = CreateImage("ART_ComponentEntryTemplate", parent, new Color(0.25f, 0.31f, 0.31f, 0.98f));
            var icon = CreateImage("ART_ComponentIcon", background.transform, new Color(0.30f, 0.60f, 0.71f, 1f));
            SetAnchors(icon.rectTransform, new Vector2(0.22f, 0.32f), new Vector2(0.78f, 0.80f));
            icon.raycastTarget = false;
            var name = CreateText("ComponentName", background.transform, "组件", new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.26f), 15, TextAnchor.MiddleCenter);
            var count = CreateText("ComponentCount", background.transform, string.Empty, new Vector2(0.64f, 0.78f), new Vector2(0.94f, 0.96f), 12, TextAnchor.MiddleRight);
            var state = CreateText("ComponentState", background.transform, "尚未出现", new Vector2(0.08f, 0.80f), new Vector2(0.58f, 0.96f), 10, TextAnchor.MiddleLeft);
            name.raycastTarget = false;
            count.raycastTarget = false;
            state.raycastTarget = false;
            state.gameObject.SetActive(false);
            var entry = background.gameObject.AddComponent<HeroWorkshopComponentEntryView>();
            entry.Configure(icon, name, count, state);
            background.gameObject.SetActive(false);
            return entry;
        }

        private static HeroWorkshopGalleryEntryView CreateHeroEntryTemplate(Transform parent)
        {
            var background = CreateImage("ART_HeroEntryTemplate", parent, new Color(0.20f, 0.22f, 0.24f, 0.98f));
            background.raycastTarget = true;
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var outline = background.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
            outline.effectDistance = new Vector2(2f, -2f);
            var portrait = CreateImage("ART_HeroPortrait", background.transform, new Color(0.88f, 0.79f, 0.53f, 0.96f));
            SetAnchors(portrait.rectTransform, new Vector2(0.09f, 0.44f), new Vector2(0.43f, 0.88f));
            portrait.raycastTarget = false;
            var partner = CreateImage("ART_HeroRecipePartner", background.transform, new Color(0.88f, 0.79f, 0.53f, 0.96f));
            SetAnchors(partner.rectTransform, new Vector2(0.57f, 0.44f), new Vector2(0.91f, 0.88f));
            partner.raycastTarget = false;
            var join = CreateText("ART_HeroRecipeJoin", background.transform, "+", new Vector2(0.43f, 0.44f), new Vector2(0.57f, 0.88f), 18, TextAnchor.MiddleCenter);
            join.raycastTarget = false;
            var name = CreateText("HeroName", background.transform, "英雄", new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.32f), 15, TextAnchor.MiddleCenter);
            var state = CreateText("HeroState", background.transform, "未合成", new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.98f), 10, TextAnchor.MiddleCenter);
            name.raycastTarget = false;
            state.raycastTarget = false;
            state.gameObject.SetActive(false);
            var entry = background.gameObject.AddComponent<HeroWorkshopGalleryEntryView>();
            entry.Configure(button, background, name, state);
            background.gameObject.SetActive(false);
            return entry;
        }

        private static GameObject BuildScreenPrefab(
            GameObject unitCardPrefab,
            GameObject hudPrefab,
            GameObject battlefieldPrefab,
            GameObject versusPrefab,
            GameObject benchPrefab,
            GameObject recruitmentPrefab,
            GameObject workshopPrefab,
            GameObject runeLoadoutPrefab)
        {
            var root = CreateRect("DragonBoundPortraitScreen", null);
            SetStretch(root.GetComponent<RectTransform>());
            var screenBackground = CreateImage("ART_ScreenBackground", root.transform, ScreenColor);
            SetStretch(screenBackground.rectTransform);
            screenBackground.raycastTarget = false;

            var hudObject = InstantiateNestedPrefab(hudPrefab, root.transform);
            hudObject.name = "HUD";
            SetAnchors(hudObject.GetComponent<RectTransform>(), new Vector2(0f, 0.89f), Vector2.one);

            var aiBattlefieldObject = InstantiateNestedPrefab(battlefieldPrefab, root.transform);
            SetAnchors(aiBattlefieldObject.GetComponent<RectTransform>(), new Vector2(0f, 0.60f), new Vector2(1f, 0.89f));
            ConfigureBattlefieldInstance(aiBattlefieldObject, TeamSide.AI);

            var versusObject = InstantiateNestedPrefab(versusPrefab, root.transform);
            versusObject.name = "Versus";
            SetAnchors(versusObject.GetComponent<RectTransform>(), new Vector2(0f, 0.52f), new Vector2(1f, 0.60f));

            var playerBattlefieldObject = InstantiateNestedPrefab(battlefieldPrefab, root.transform);
            SetAnchors(playerBattlefieldObject.GetComponent<RectTransform>(), new Vector2(0f, 0.23f), new Vector2(1f, 0.52f));
            ConfigureBattlefieldInstance(playerBattlefieldObject, TeamSide.Player);

            var benchObject = InstantiateNestedPrefab(benchPrefab, root.transform);
            benchObject.name = "Bench";
            SetAnchors(benchObject.GetComponent<RectTransform>(), new Vector2(0f, 0.13f), new Vector2(1f, 0.23f));

            var recruitmentObject = InstantiateNestedPrefab(recruitmentPrefab, root.transform);
            recruitmentObject.name = "Recruitment";
            SetAnchors(recruitmentObject.GetComponent<RectTransform>(), new Vector2(0f, 0.03f), new Vector2(1f, 0.13f));

            var bottomGuard = CreateImage("ART_BottomGuard", root.transform, ScreenColor);
            SetAnchors(bottomGuard.rectTransform, Vector2.zero, new Vector2(1f, 0.03f));
            bottomGuard.raycastTarget = false;

            var aiUnitLayer = CreateRect("AiUnitLayer", root.transform).GetComponent<RectTransform>();
            SetStretch(aiUnitLayer);
            var aiRange = CreateRangePreview("ART_AiRangePreview", aiUnitLayer);
            SetCentered(aiRange.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(200f, 200f));

            var playerUnitLayer = CreateRect("PlayerUnitLayer", root.transform).GetComponent<RectTransform>();
            SetStretch(playerUnitLayer);
            var playerRange = CreateRangePreview("ART_PlayerRangePreview", playerUnitLayer);
            SetCentered(playerRange.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(200f, 200f));

            var workshopObject = InstantiateNestedPrefab(workshopPrefab, root.transform);
            workshopObject.name = "ART_HeroWorkshop";
            SetStretch(workshopObject.GetComponent<RectTransform>());
            workshopObject.SetActive(false);
            workshopObject.transform.SetAsLastSibling();

            var runeLoadoutObject = InstantiateNestedPrefab(runeLoadoutPrefab, root.transform);
            runeLoadoutObject.name = "ART_RuneLoadout";
            SetStretch(runeLoadoutObject.GetComponent<RectTransform>());
            runeLoadoutObject.SetActive(false);
            runeLoadoutObject.transform.SetAsLastSibling();

            var aiSide = aiBattlefieldObject.GetComponent<GreyboxBattlefieldSideView>();
            var playerSide = playerBattlefieldObject.GetComponent<GreyboxBattlefieldSideView>();
            var aiBoard = aiSide.BoardView;
            var playerBoard = playerSide.BoardView;
            var aiCells = aiBattlefieldObject.GetComponentsInChildren<GridCellView>(true);
            var playerCells = new List<GridCellView>();
            playerCells.AddRange(playerBattlefieldObject.GetComponentsInChildren<GridCellView>(true));
            playerCells.AddRange(benchObject.GetComponentsInChildren<GridCellView>(true));
            var aiDragArrow = aiBattlefieldObject.transform.Find("ART_DragArrow").GetComponent<DragArrowPreviewView>();
            var playerDragArrow = playerBattlefieldObject.transform.Find("ART_DragArrow").GetComponent<DragArrowPreviewView>();
            aiBoard.Configure(null, aiCells, aiUnitLayer, unitCardPrefab.GetComponent<DraggableUnitView>(), aiRange, false, aiDragArrow);
            playerBoard.Configure(null, playerCells.ToArray(), playerUnitLayer, unitCardPrefab.GetComponent<DraggableUnitView>(), playerRange, true, playerDragArrow);

            var hud = hudObject.GetComponent<GreyboxHudView>();
            hud.Configure(
                hudObject.transform.Find("ART_PauseButton").GetComponent<Button>(),
                hudObject.transform.Find("ART_PauseButton/PauseLabel").GetComponent<Text>(),
                hudObject.transform.Find("ResourceLabel").GetComponent<Text>(),
                hudObject.transform.Find("WaveLabel").GetComponent<Text>(),
                hudObject.transform.Find("RecruitmentLabel").GetComponent<Text>(),
                versusObject.transform.Find("PlayerHealthLabel").GetComponent<Text>(),
                versusObject.transform.Find("AiHealthLabel").GetComponent<Text>(),
                hudObject.transform.Find("DebugLabel").GetComponent<Text>(),
                hudObject.transform.Find("EnemyDebugLabel").GetComponent<Text>());

            var screenView = root.AddComponent<DragonBoundScreenView>();
            screenView.Configure(
                aiSide,
                playerSide,
                hud,
                recruitmentObject.GetComponent<GreyboxRecruitmentPanel>(),
                workshopObject.GetComponent<HeroWorkshopView>(),
                runeLoadoutObject.GetComponent<RuneLoadoutView>());

            RecordInstanceOverrides(hudObject);
            RecordInstanceOverrides(aiBattlefieldObject);
            RecordInstanceOverrides(versusObject);
            RecordInstanceOverrides(playerBattlefieldObject);
            RecordInstanceOverrides(benchObject);
            RecordInstanceOverrides(recruitmentObject);
            return SavePrefab(root, ScreenPrefabPath);
        }

        private static void BuildScene(GameObject screenPrefab, GameObject unitCardPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var systems = new GameObject("Systems");
            var bootstrap = systems.AddComponent<DragonBoundBootstrap>();

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ScreenColor;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var ui = new GameObject("UI");
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(ui.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = PortraitLayoutMetrics.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var safeArea = CreateRect("SafeAreaRoot", canvasObject.transform);
            SetStretch(safeArea.GetComponent<RectTransform>());
            safeArea.AddComponent<SafeAreaFitter>();

            var screenObject = InstantiateNestedPrefab(screenPrefab, safeArea.transform);
            SetStretch(screenObject.GetComponent<RectTransform>());
            var screenView = screenObject.GetComponent<DragonBoundScreenView>();
            var aiBoard = screenView.AiBoardView;
            var playerBoard = screenView.PlayerBoardView;
            aiBoard.Configure(
                canvas,
                CopyCells(aiBoard.CellViews),
                screenObject.transform.Find("AiUnitLayer").GetComponent<RectTransform>(),
                unitCardPrefab.GetComponent<DraggableUnitView>(),
                screenObject.transform.Find("AiUnitLayer/ART_AiRangePreview").GetComponent<Image>(),
                false,
                screenView.AiBattlefieldView.transform.Find("ART_DragArrow").GetComponent<DragArrowPreviewView>());
            playerBoard.Configure(
                canvas,
                CopyCells(playerBoard.CellViews),
                screenObject.transform.Find("PlayerUnitLayer").GetComponent<RectTransform>(),
                unitCardPrefab.GetComponent<DraggableUnitView>(),
                screenObject.transform.Find("PlayerUnitLayer/ART_PlayerRangePreview").GetComponent<Image>(),
                true,
                screenView.PlayerBattlefieldView.transform.Find("ART_DragArrow").GetComponent<DragArrowPreviewView>());
            bootstrap.Configure(screenView);
            RecordInstanceOverrides(screenObject);

            var overlay = CreateRect("OverlayRoot", canvasObject.transform);
            SetStretch(overlay.GetComponent<RectTransform>());
            overlay.transform.SetAsLastSibling();
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"Unable to save {ScenePath}.");
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void UpgradeScenePresentationReferences(string scenePath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            DragonBoundScreenView screen = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                screen = root.GetComponentInChildren<DragonBoundScreenView>(true);
                if (screen != null)
                {
                    break;
                }
            }

            if (screen == null)
            {
                throw new InvalidOperationException($"Screen view is missing from {scenePath}.");
            }

            var canvas = screen.GetComponentInParent<Canvas>();
            var unitCard = AssetDatabase.LoadAssetAtPath<GameObject>(UnitCardPrefabPath)
                ?.GetComponent<DraggableUnitView>();
            var aiArrow = EnsureDragArrow(screen.AiBattlefieldView.transform);
            var playerArrow = EnsureDragArrow(screen.PlayerBattlefieldView.transform);
            var workshop = EnsureHeroWorkshop(screen);
            var runeLoadout = EnsureRuneLoadout(screen);
            if (canvas == null || unitCard == null || aiArrow == null || playerArrow == null || workshop == null || runeLoadout == null)
            {
                throw new InvalidOperationException($"Editable presentation references are incomplete in {scenePath}.");
            }

            var aiBoard = screen.AiBoardView;
            var playerBoard = screen.PlayerBoardView;
            aiBoard.Configure(
                canvas,
                CopyCells(aiBoard.CellViews),
                aiBoard.UnitLayer,
                unitCard,
                aiBoard.RangePreview,
                false,
                aiArrow);
            playerBoard.Configure(
                canvas,
                CopyCells(playerBoard.CellViews),
                playerBoard.UnitLayer,
                unitCard,
                playerBoard.RangePreview,
                true,
                playerArrow);
            screen.Configure(
                screen.AiBattlefieldView,
                screen.PlayerBattlefieldView,
                screen.HudView,
                screen.RecruitmentView,
                workshop,
                runeLoadout);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException($"Unable to save presentation references for {scenePath}.");
            }
        }

        private static void UpgradeDragArrowInBattlefieldPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(BattlefieldPrefabPath);
            try
            {
                EnsureDragArrow(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, BattlefieldPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static DragArrowPreviewView EnsureDragArrow(Transform battlefield)
        {
            var existing = battlefield.Find("ART_DragArrow");
            if (existing != null)
            {
                var existingView = existing.GetComponent<DragArrowPreviewView>();
                if (existingView != null)
                {
                    return existingView;
                }
            }

            var root = CreateRect("ART_DragArrow", battlefield);
            SetCentered(root.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(120f, 10f));
            var shaft = CreateImage("ART_DragArrowShaft", root.transform, new Color(0.96f, 0.84f, 0.28f, 0.94f));
            SetStretch(shaft.rectTransform);
            shaft.raycastTarget = false;
            var head = CreateText(
                "ART_DragArrowHead",
                root.transform,
                ">",
                new Vector2(0.86f, 0f),
                Vector2.one,
                34,
                TextAnchor.MiddleRight);
            head.raycastTarget = false;
            var view = root.AddComponent<DragArrowPreviewView>();
            view.Configure(shaft, head);
            return view;
        }

        private static HeroWorkshopView EnsureHeroWorkshop(DragonBoundScreenView screen)
        {
            var existing = screen.transform.Find("ART_HeroWorkshop");
            if (existing != null)
            {
                return existing.GetComponent<HeroWorkshopView>();
            }

            var workshopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeroWorkshopPrefabPath);
            if (workshopPrefab == null)
            {
                return null;
            }

            var workshop = InstantiateNestedPrefab(workshopPrefab, screen.transform);
            workshop.name = "ART_HeroWorkshop";
            SetStretch(workshop.GetComponent<RectTransform>());
            workshop.SetActive(false);
            workshop.transform.SetAsLastSibling();
            return workshop.GetComponent<HeroWorkshopView>();
        }

        private static RuneLoadoutView EnsureRuneLoadout(DragonBoundScreenView screen)
        {
            var existing = screen.transform.Find("ART_RuneLoadout");
            if (existing != null)
            {
                return existing.GetComponent<RuneLoadoutView>();
            }

            var loadoutPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuneLoadoutPrefabPath);
            if (loadoutPrefab == null)
            {
                return null;
            }

            var loadout = InstantiateNestedPrefab(loadoutPrefab, screen.transform);
            loadout.name = "ART_RuneLoadout";
            SetStretch(loadout.GetComponent<RectTransform>());
            loadout.SetActive(false);
            loadout.transform.SetAsLastSibling();
            return loadout.GetComponent<RuneLoadoutView>();
        }

        private static GridCellView[] CopyCells(IReadOnlyList<GridCellView> source)
        {
            var result = new GridCellView[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                result[index] = source[index];
            }

            return result;
        }

        private static GameObject InstantiateNestedPrefab(GameObject prefab, Transform parent)
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException($"Unable to instantiate prefab {prefab.name}.");
            }

            instance.transform.SetParent(parent, false);
            return instance;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Unable to save prefab at {path}.");
            }

            return prefab;
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            return gameObject;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var gameObject = CreateRect(name, parent);
            var image = gameObject.AddComponent<Image>();
            image.color = color;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            return image;
        }

        private static Image CreateCircleImage(string name, Transform parent, Color color)
        {
            var image = CreateImage(name, parent, color);
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            image.preserveAspect = true;
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(color.r, color.g, color.b, 0.86f);
            outline.effectDistance = new Vector2(2f, 2f);
            outline.useGraphicAlpha = true;
            return image;
        }

        private static Image CreateRangePreview(string name, Transform parent)
        {
            var fill = CreateImage(name, parent, RangeFillColor);
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            fill.preserveAspect = true;
            fill.raycastTarget = false;

            var outline = CreateImage("ART_RangeOutline", fill.transform, RangeOutlineColor);
            outline.sprite = LoadRangeOutlineSprite();
            outline.preserveAspect = true;
            outline.raycastTarget = false;
            SetStretch(outline.rectTransform);

            fill.enabled = false;
            fill.gameObject.SetActive(false);
            return fill;
        }

        private static Sprite LoadRangeOutlineSprite()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RangeOutlineSpritePath);
            if (sprite == null)
            {
                throw new InvalidOperationException($"Range outline sprite is missing: {RangeOutlineSpritePath}");
            }

            return sprite;
        }

        private static void PrepareRangeOutlineSprite()
        {
            var importer = AssetImporter.GetAtPath(RangeOutlineSpritePath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Range outline texture is missing: {RangeOutlineSpritePath}");
            }

            if (importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Single &&
                importer.alphaIsTransparency &&
                !importer.mipmapEnabled)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        private static void UpgradeRangePreviews(string prefabPath, Sprite outlineSprite, params string[] paths)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (var path in paths)
                {
                    var target = root.transform.Find(path);
                    if (target == null)
                    {
                        throw new InvalidOperationException($"Range preview was not found in {prefabPath}: {path}");
                    }

                    var fill = target.GetComponent<Image>();
                    if (fill == null)
                    {
                        throw new InvalidOperationException($"Range preview has no Image component: {path}");
                    }

                    fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                    fill.color = RangeFillColor;
                    fill.preserveAspect = true;
                    fill.raycastTarget = false;
                    foreach (var oldOutline in target.GetComponents<Outline>())
                    {
                        UnityEngine.Object.DestroyImmediate(oldOutline);
                    }

                    var outlineTransform = target.Find("ART_RangeOutline");
                    var outline = outlineTransform != null
                        ? outlineTransform.GetComponent<Image>()
                        : CreateImage("ART_RangeOutline", target, RangeOutlineColor);
                    if (outline == null)
                    {
                        outline = outlineTransform.gameObject.AddComponent<Image>();
                    }

                    outline.sprite = outlineSprite;
                    outline.color = RangeOutlineColor;
                    outline.preserveAspect = true;
                    outline.raycastTarget = false;
                    outline.enabled = true;
                    SetStretch(outline.rectTransform);
                    fill.enabled = false;
                    fill.gameObject.SetActive(false);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string value,
            Vector2 anchorMin,
            Vector2 anchorMax,
            int fontSize,
            TextAnchor alignment)
        {
            var gameObject = CreateRect(name, parent);
            var text = gameObject.AddComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            SetAnchors(text.rectTransform, anchorMin, anchorMax);
            return text;
        }

        private static void SetStretch(RectTransform rect, Vector2? offsetMin = null, Vector2? offsetMax = null)
        {
            SetAnchors(rect, Vector2.zero, Vector2.one);
            rect.offsetMin = offsetMin ?? Vector2.zero;
            rect.offsetMax = offsetMax ?? Vector2.zero;
        }

        private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        private static void SetCentered(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private static void RecordInstanceOverrides(GameObject instance)
        {
            foreach (var component in instance.GetComponentsInChildren<Component>(true))
            {
                if (component != null)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
            }
        }
    }
}
