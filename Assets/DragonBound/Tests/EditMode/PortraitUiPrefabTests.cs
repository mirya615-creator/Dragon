using System.Collections.Generic;
using System.Linq;
using DragonBound.Bootstrap;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Presentation;
using DragonBound.Recruitment;
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
        private static readonly string ScreenPath = UiVariantProjectPaths.V1Ui("Prefabs/Screens/DragonBoundPortraitScreen.prefab");
        private static readonly string BattlefieldPath = UiVariantProjectPaths.V1Ui("Prefabs/Modules/Battlefield.prefab");
        private static readonly string BenchPath = UiVariantProjectPaths.V1Ui("Prefabs/Modules/Bench.prefab");
        private static readonly string RecruitmentPath = UiVariantProjectPaths.V1Ui("Prefabs/Modules/Recruitment.prefab");
        private static readonly string UnitCardPath = UiVariantProjectPaths.V1Ui("Prefabs/Components/UnitCard.prefab");
        private static readonly string EnemyCardPath = UiVariantProjectPaths.V1Ui("Prefabs/Components/EnemyCard.prefab");
        private static readonly string HeroFormationPath = UiVariantProjectPaths.V1Ui("Prefabs/Components/HeroFormation.prefab");
        private static readonly string WeaponPanelHeroPath = UiVariantProjectPaths.V1Resource("prefabs/Hero.prefab");
        private static readonly string RangeOutlinePath = UiVariantProjectPaths.V1Ui("Art/Range/RangeOutlineThin.png");
        private static readonly string BoardCellPath = UiVariantProjectPaths.V1Ui("Prefabs/Components/BoardCell.prefab");
        private static readonly string BenchSlotPath = UiVariantProjectPaths.V1Ui("Prefabs/Components/BenchSlot.prefab");
        private static readonly string ScenePath = UiVariantProjectPaths.V1Scene("Greybox_Main");

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

            var soulChainOverlay = unitCard.transform.FindUi("ART_SoulChainOverlay")?.GetComponent<Image>();
            Assert.IsNotNull(soulChainOverlay);
            Assert.IsFalse(soulChainOverlay.raycastTarget);
            Assert.AreEqual(0f, soulChainOverlay.color.a, 0.0001f);

        }

        [TestCase(6, "BossW06")]
        [TestCase(12, "BossW12")]
        [TestCase(16, "BossW16")]
        [TestCase(20, "BossW20")]
        public void EnemyCardBindsTheAuthoredBossAnimationForItsSpawnWave(
            int spawnWave,
            string expectedControllerName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyCardPath);
            var instance = Object.Instantiate(prefab);
            try
            {
                var view = instance.GetComponent<EnemyView>();
                var animationImage = instance.transform
                    .FindUi("ART_EnemyAnimation/Image")
                    ?.GetComponent<Image>();
                Assert.IsNotNull(view);
                Assert.IsNotNull(animationImage);
                var animator = animationImage.GetComponent<Animator>();
                Assert.IsNotNull(animator);

                view.Bind(new EnemyRuntime(
                    "test.boss." + spawnWave,
                    TeamSide.Player,
                    100f,
                    EnemyArchetype.Boss,
                    1,
                    "TEST_BOSS_" + spawnWave,
                    spawnWave));

                Assert.IsTrue(animationImage.enabled);
                Assert.IsTrue(animator.enabled);
                Assert.IsNotNull(animator.runtimeAnimatorController);
                Assert.AreEqual(expectedControllerName, animator.runtimeAnimatorController.name);
                Assert.IsNotEmpty(animator.runtimeAnimatorController.animationClips);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [TestCase("basic.axe_raider", "AXE")]
        [TestCase("basic.twinaxe_berserker", "BERSERKER")]
        [TestCase("basic.longbow_hunter", "BOW")]
        [TestCase("basic.spear_raider", "SPEAR")]
        public void UnitCardUsesOneShotPortraitAnimationForEachBasicUnit(
            string configId,
            string expectedControllerName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnitCardPath);
            var instance = Object.Instantiate(prefab);
            try
            {
                var portrait = instance.transform.FindUi("ART_UnitPortrait");
                var view = instance.GetComponent<DraggableUnitView>();
                Assert.IsNotNull(portrait);
                Assert.IsNotNull(view);
                Assert.AreSame(portrait.GetComponent<Animator>(), view.BasicAttackAnimator);

                view.ConfigureBasicAttackAnimation(configId);

                var controller = view.BasicAttackAnimator.runtimeAnimatorController;
                Assert.IsNotNull(controller, configId);
                Assert.AreEqual(expectedControllerName, controller.name);
                Assert.IsNotEmpty(controller.animationClips);
                Assert.IsTrue(controller.animationClips.All(clip => clip != null && !clip.isLooping));
                Assert.AreEqual(0f, view.BasicAttackAnimator.speed, 0.0001f);
                Assert.IsTrue(view.PlayBasicAttackAnimation());
                Assert.AreEqual(1f, view.BasicAttackAnimator.speed, 0.0001f);
                Assert.IsFalse(view.PlayBasicAttackAnimation(), "One attack must not restart twice in one frame.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void BowAnimationReleasesAuthoredSwordProjectileAfterFrameSeventeen()
        {
            Assert.IsNotNull(DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Unit/road"));
            var controller = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>("Animation/UnitAni/BOW");
            Assert.IsNotNull(controller);
            var clip = controller.animationClips.Single(value => value.name == "BOW");
            var releaseEvent = AnimationUtility.GetAnimationEvents(clip)
                .Single(value => value.functionName == "OnBowProjectileRelease");
            Assert.AreEqual(17f / 60f, releaseEvent.time, 0.0001f);
            Assert.IsFalse(clip.isLooping);
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
            Assert.IsNotNull(formationView.HeroAttackAnimator);
            Assert.AreEqual("ART_ComponentConnector", formationView.HeroAttackAnimator.name);
            var heroAnimationImage = formationView.HeroAttackAnimator.GetComponent<Image>();
            Assert.IsNotNull(heroAnimationImage);
            Assert.IsFalse(heroAnimationImage.raycastTarget);
            Assert.IsTrue(heroAnimationImage.preserveAspect);

            var screen = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath);
            var screenView = screen.GetComponent<DragonBoundScreenView>();
            foreach (var board in new[] { screenView.PlayerBoardView, screenView.AiBoardView })
            {
                Assert.IsNotNull(board.HeroPrefab);
                Assert.IsNotNull(board.HeroFormationEffectPrefab);
            }
        }

        [Test]
        public void HeroFormationCanApplyAndRestoreAiHeroArtPosition()
        {
            var formation = AssetDatabase.LoadAssetAtPath<GameObject>(HeroFormationPath);
            Assert.IsNotNull(formation);
            var instance = Object.Instantiate(formation);
            try
            {
                var formationView = instance.GetComponent<HeroFormationView>();
                var heroArt = formationView.HeroAttackAnimator.GetComponent<RectTransform>();
                var authoredPosition = heroArt.anchoredPosition;

                formationView.SetHeroArtAnchoredPositionX(13f);
                Assert.AreEqual(13f, heroArt.anchoredPosition.x, 0.001f);
                Assert.AreEqual(authoredPosition.y, heroArt.anchoredPosition.y, 0.001f);

                formationView.SetHeroArtAnchoredPositionX(null);
                Assert.AreEqual(authoredPosition.x, heroArt.anchoredPosition.x, 0.001f);
                Assert.AreEqual(authoredPosition.y, heroArt.anchoredPosition.y, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(instance);
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
        public void WeaponPanelHeroAuthorsHiddenRuneImage()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPanelHeroPath);
            Assert.IsNotNull(prefab);

            Transform weapon = prefab.transform.FindUi("weapon");
            Assert.IsNotNull(weapon);
            Assert.IsFalse(weapon.gameObject.activeSelf);
            Assert.IsNotNull(weapon.GetComponent<Image>());
        }

        [TestCase(DragonBoundHeroIds.WindclawRanger, "Animation/Windclaw Ranger")]
        [TestCase(DragonBoundHeroIds.EmberShaman, "Animation/Ember Shaman")]
        [TestCase(DragonBoundHeroIds.RuneboltMage, "Animation/Runebolt Mage")]
        [TestCase(DragonBoundHeroIds.Stonebinder, "Animation/Stonebound Warlock")]
        [TestCase(DragonBoundHeroIds.CrownSwordLeader, "Animation/Oathcrown Blademaster")]
        [TestCase(DragonBoundHeroIds.CrownHunterLeader, "Animation/Frostcrown Hunter")]
        [TestCase(DragonBoundHeroIds.DragonRider, "Animation/Flame Drake Rider")]
        [TestCase(DragonBoundHeroIds.StarfallArchmage, "Animation/Starfall Archmage")]
        [TestCase(DragonBoundHeroIds.ThunderJarl, "Animation/Thunderlord")]
        [TestCase(DragonBoundHeroIds.NightfangAssassin, "Animation/Nightfang Assassin")]
        [TestCase(DragonBoundHeroIds.LeviathanHunter, "Animation/Abyssal Harpooner")]
        [TestCase(DragonBoundHeroIds.SkyhunterValkyrie, "Animation/Skyborne Valkyrie")]
        public void HeroAnimationControllerIsAuthoredAndNonLooping(string heroId, string resourcePath)
        {
            Assert.AreEqual(resourcePath, HeroAnimationControllerCatalog.GetResourcePath(heroId));
            var controller = HeroAnimationControllerCatalog.Load(heroId);
            Assert.IsNotNull(controller, resourcePath);
            Assert.IsNotEmpty(controller.animationClips, resourcePath);
            Assert.IsTrue(controller.animationClips.All(clip => !clip.isLooping), resourcePath);
        }

        [Test]
        public void FormationAnimationWaitsForAnAttackAndThenRestarts()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeroFormationPath);
            var instance = Object.Instantiate(prefab);
            try
            {
                var view = instance.GetComponent<HeroFormationView>();
                view.SetHeroAnimation(DragonBoundHeroIds.RuneboltMage);
                view.ObserveAttackSequence(0);
                Assert.IsNotNull(view.HeroAttackAnimator.runtimeAnimatorController);
                Assert.AreEqual(0f, view.HeroAttackAnimator.speed, 0.0001f);

                view.ObserveAttackSequence(1);
                Assert.AreEqual(1f, view.HeroAttackAnimator.speed, 0.0001f);

                view.SetHeroArtVisible(false);
                Assert.IsFalse(view.HeroAttackAnimator.gameObject.activeSelf);
                view.SetHeroArtVisible(true);
                Assert.IsTrue(view.HeroAttackAnimator.gameObject.activeSelf);
                Assert.AreEqual(0f, view.HeroAttackAnimator.speed, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void WindclawPowerShotKeepsSkillControllerWhenAttackSequenceRefreshesSameFrame()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeroFormationPath);
            var instance = Object.Instantiate(prefab);
            try
            {
                var view = instance.GetComponent<HeroFormationView>();
                view.SetHeroAnimation(DragonBoundHeroIds.WindclawRanger);
                view.ObserveAttackSequence(0);

                Assert.IsTrue(view.PlayAttackAnimation(true));
                var skillController = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                    "Animation/Windclaw Ranger s");
                Assert.AreSame(skillController, view.HeroAttackAnimator.runtimeAnimatorController);

                view.ObserveAttackSequence(1);
                Assert.AreSame(
                    skillController,
                    view.HeroAttackAnimator.runtimeAnimatorController,
                    "LateUpdate attack observation must not overwrite the power-shot animation.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void WindclawSkillAnimationExposesTenthFrameReleaseEvent()
        {
            Assert.IsNotNull(DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Windclaw Ranger/road"));
            var controller = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                "Animation/Windclaw Ranger s");
            Assert.IsNotNull(controller);
            var clip = controller.animationClips.Single(value => value.name == "Windclaw Ranger s");
            var releaseEvent = AnimationUtility.GetAnimationEvents(clip)
                .Single(value => value.functionName == "OnWindclawSkillRelease");
            Assert.AreEqual(10f / 60f, releaseEvent.time, 0.0001f);
            Assert.IsFalse(clip.isLooping);
        }

        [Test]
        public void EmberShamanAttackExposesThirteenthFrameFireballEvent()
        {
            Assert.IsNotNull(DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Ember Shaman/road"));
            var controller = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                "Animation/Ember Shaman");
            Assert.IsNotNull(controller);
            var clip = controller.animationClips.Single(value => value.name == "Ember Shaman");
            var releaseEvent = AnimationUtility.GetAnimationEvents(clip)
                .Single(value => value.functionName == "OnEmberShamanFireballRelease");
            Assert.AreEqual(13f / 60f, releaseEvent.time, 0.0001f);
            Assert.IsFalse(clip.isLooping);
        }

        [Test]
        public void RuneboltMageAttackExposesFourteenthFrameBoltEvent()
        {
            var attackController = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                "Animation/Runebolt Mage");
            var boltController = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                "Animation/Runebolt MageBoom");
            Assert.IsNotNull(attackController);
            Assert.IsNotNull(boltController);
            var attackClip = attackController.animationClips
                .Single(value => value.name == "Runebolt Mage");
            var releaseEvent = AnimationUtility.GetAnimationEvents(attackClip)
                .Single(value => value.functionName == "OnRuneboltMageBoltRelease");
            Assert.AreEqual(14f / 60f, releaseEvent.time, 0.0001f);
            Assert.IsFalse(attackClip.isLooping);
            Assert.IsTrue(boltController.animationClips.Any(value =>
                value != null && value.name == "Runebolt MageBoom"));
        }

        [Test]
        public void StoneboundWarlockAttackExposesFourteenthFrameRockEvent()
        {
            Assert.IsNotNull(DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Stonebound Warlock/rood"));
            Assert.IsNotNull(DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Stonebound Warlock/roodS"));
            var controller = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                "Animation/Stonebound Warlock");
            Assert.IsNotNull(controller);
            var clip = controller.animationClips
                .Single(value => value.name == "Stonebound Warlock");
            var releaseEvent = AnimationUtility.GetAnimationEvents(clip)
                .Single(value =>
                    value.functionName == "OnStoneboundWarlockRockRelease");
            Assert.AreEqual(14f / 60f, releaseEvent.time, 0.0001f);
            Assert.IsFalse(clip.isLooping);
        }

        [Test]
        public void ThunderlordAttackExposesTenthFrameChainEventAndThreeSprites()
        {
            Assert.IsNotNull(DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Thunderlord/road/Main"));
            Assert.IsNotNull(DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Thunderlord/road/Froad"));
            Assert.IsNotNull(DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Thunderlord/road/Sroad"));
            var controller = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                "Animation/Thunderlord");
            Assert.IsNotNull(controller);
            var clip = controller.animationClips.Single(value => value.name == "Thunderlord");
            var releaseEvent = AnimationUtility.GetAnimationEvents(clip)
                .Single(value => value.functionName == "OnThunderlordChainRelease");
            Assert.AreEqual(10f / 60f, releaseEvent.time, 0.0001f);
            Assert.IsFalse(clip.isLooping);
        }

        [Test]
        public void ThunderlordSkillExposesTenthFrameReleaseAndAuthoredExplosion()
        {
            var skillController = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                "Animation/ThunderlordStartUp");
            Assert.IsNotNull(skillController);
            var skillClip = skillController.animationClips
                .Single(value => value.name == "ThunderlordStartUp");
            var releaseEvent = AnimationUtility.GetAnimationEvents(skillClip)
                .Single(value => value.functionName == "OnThunderlordSkillRelease");
            Assert.AreEqual(10f / 60f, releaseEvent.time, 0.0001f);
            Assert.IsFalse(skillClip.isLooping);

            var explosionController = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                "Animation/ThunderlordBoom");
            Assert.IsNotNull(explosionController);
            Assert.IsTrue(explosionController.animationClips
                .Any(value => value != null && value.name == "ThunderlordBoom"));
        }

        [Test]
        public void AbyssalHarpoonerAttackExposesSixteenthFrameHarpoonEventAndSprites()
        {
            Assert.IsNotNull(DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Abyssal Harpooner/road"));
            Assert.IsNotNull(DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Abyssal Harpooner/boom"));

            var controller = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                "Animation/Abyssal Harpooner");
            Assert.IsNotNull(controller);
            var clip = controller.animationClips
                .Single(value => value.name == "Abyssal Harpooner");
            var releaseEvent = AnimationUtility.GetAnimationEvents(clip)
                .Single(value => value.functionName == "OnAbyssalHarpoonRelease");
            Assert.AreEqual(16f / 60f, releaseEvent.time, 0.0001f);
            Assert.IsFalse(clip.isLooping);
        }

        [Test]
        public void AbyssalHarpoonerSkillUsesOwnControllerAndReleasesAfterFifteenthFrame()
        {
            Assert.AreEqual(
                "Animation/Abyssal HarpoonerStartUp",
                HeroAnimationControllerCatalog.GetSkillResourcePath(
                    DragonBoundHeroIds.LeviathanHunter));
            Assert.IsNotNull(DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Abyssal Harpooner/start"));

            var controller = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                "Animation/Abyssal HarpoonerStartUp");
            Assert.IsNotNull(controller);
            var clip = controller.animationClips
                .Single(value => value.name == "AbyssalHarpoonerStartUp");
            var releaseEvent = AnimationUtility.GetAnimationEvents(clip)
                .Single(value => value.functionName == "OnAbyssalHarpoonSkillRelease");
            Assert.AreEqual(15f / 60f, releaseEvent.time, 0.0001f);
            Assert.IsFalse(clip.isLooping);
        }

        [Test]
        public void FlameDrakeRiderDiveAnimationIsAuthoredAsOneShot()
        {
            var controller = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>("Animation/Flame Drake Rider S");
            Assert.IsNotNull(controller);
            Assert.IsNotEmpty(controller.animationClips);
            Assert.IsTrue(controller.animationClips.All(clip => clip != null && !clip.isLooping));
        }

        [Test]
        public void FlameDrakeFireballAndExplosionAnimationsExposeAuthoredTimingEvents()
        {
            Assert.IsNotNull(DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Flame Drake Rider/Sroad"));
            Assert.IsNotNull(DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Flame Drake Rider/road"));

            var attackController = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                "Animation/Flame Drake Rider");
            Assert.IsNotNull(attackController);
            var attackClip = attackController.animationClips
                .Single(clip => clip.name == "Flame Drake Rider");
            var releaseEvent = AnimationUtility.GetAnimationEvents(attackClip)
                .Single(animationEvent =>
                    animationEvent.functionName == "OnFlameDrakeFireballRelease");
            Assert.AreEqual(0.25f, releaseEvent.time, 0.0001f);

            var skillAttackController = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                "Animation/Flame Drake Rider S");
            Assert.IsNotNull(skillAttackController);
            var skillAttackClip = skillAttackController.animationClips
                .Single(clip => clip.name == "Flame Drake Rider S");
            Assert.AreEqual(
                0.25f,
                AnimationUtility.GetAnimationEvents(skillAttackClip)
                    .Single(animationEvent =>
                        animationEvent.functionName == "OnFlameDrakeFireballRelease").time,
                0.0001f);

            var explosionController = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                "Animation/Flame Drake Rider Boom");
            Assert.IsNotNull(explosionController);
            var explosionClip = explosionController.animationClips
                .Single(clip => clip.name == "Flame Drake Rider Boom");
            var explosionEvents = AnimationUtility.GetAnimationEvents(explosionClip);
            CollectionAssert.AreEquivalent(
                new[] { "OnDamageFrame", "OnAnimationEnd" },
                explosionEvents.Select(animationEvent => animationEvent.functionName).ToArray());
            Assert.IsFalse(explosionClip.isLooping);

            var skillExplosionController = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                "Animation/FlameDrakeRiderBoomS");
            Assert.IsNotNull(skillExplosionController);
            var skillExplosionClip = skillExplosionController.animationClips
                .Single(clip => clip.name == "FlameDrakeRiderBoomS");
            CollectionAssert.AreEquivalent(
                new[] { "OnDamageFrame", "OnAnimationEnd" },
                AnimationUtility.GetAnimationEvents(skillExplosionClip)
                    .Select(animationEvent => animationEvent.functionName).ToArray());
            Assert.IsFalse(skillExplosionClip.isLooping);

            var burningGroundController = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>("Animation/boomBoard");
            Assert.IsNotNull(burningGroundController);
            Assert.IsTrue(burningGroundController.animationClips.All(clip => clip.isLooping));
        }

        [Test]
        public void SkyborneValkyrieArrowAndExplosionExposeAuthoredTimingEvents()
        {
            Assert.IsNotNull(DragonBound.Presentation.UiAssets.Load<Sprite>("VFX/Skyborne Valkyrie/road"));

            var attackController = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                "Animation/Skyborne Valkyrie");
            Assert.IsNotNull(attackController);
            var attackClip = attackController.animationClips
                .Single(clip => clip.name == "Skyborne Valkyrie");
            var releaseEvent = AnimationUtility.GetAnimationEvents(attackClip)
                .Single(animationEvent =>
                    animationEvent.functionName == "OnSkyborneValkyrieArrowRelease");
            Assert.AreEqual(13f / 60f, releaseEvent.time, 0.0001f);
            Assert.IsFalse(attackClip.isLooping);

            var explosionController = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                "Animation/SkyborneValkyrieBoom");
            Assert.IsNotNull(explosionController);
            var explosionClip = explosionController.animationClips
                .Single(clip => clip.name == "SkyborneValkyrieBoom");
            CollectionAssert.AreEquivalent(
                new[] { "OnDamageFrame", "OnAnimationEnd" },
                AnimationUtility.GetAnimationEvents(explosionClip)
                    .Select(animationEvent => animationEvent.functionName).ToArray());
            Assert.IsFalse(explosionClip.isLooping);
        }

        [Test]
        public void StarfallArchmageGemAndSkillExplosionExposeAuthoredTimingEvents()
        {
            Assert.IsNotNull(DragonBound.Presentation.UiAssets.Load<Sprite>(
                "VFX/Starfall Archmage/road/微信图片_20260831170133_175_101"));

            var normalController = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                "Animation/Starfall Archmage");
            var skillController = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                "Animation/StarfallArchmageStartUP");
            Assert.IsNotNull(normalController);
            Assert.IsNotNull(skillController);
            foreach (var clip in normalController.animationClips.Concat(skillController.animationClips))
            {
                var releaseEvent = AnimationUtility.GetAnimationEvents(clip)
                    .Single(animationEvent =>
                        animationEvent.functionName == "OnStarfallArchmageGemRelease");
                Assert.AreEqual(0.25f, releaseEvent.time, 0.0001f);
                Assert.IsFalse(clip.isLooping);
            }

            var explosionController = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                "Animation/StarfallArchmageBoom");
            Assert.IsNotNull(explosionController);
            var explosionClip = explosionController.animationClips
                .Single(clip => clip.name == "StarfallArchmageBoom");
            CollectionAssert.AreEquivalent(
                new[] { "OnDamageFrame", "OnAnimationEnd" },
                AnimationUtility.GetAnimationEvents(explosionClip)
                    .Select(animationEvent => animationEvent.functionName).ToArray());
            Assert.IsFalse(explosionClip.isLooping);
        }

        [Test]
        public void NightfangSkillStartupAndExplosionExposeSynchronizedTimingEvents()
        {
            var startupController = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                "Animation/NightfangAssassinStartUp");
            Assert.IsNotNull(startupController);
            var startupClip = startupController.animationClips
                .Single(clip => clip.name == "NightfangAssassinStartUp");
            var startupEnd = AnimationUtility.GetAnimationEvents(startupClip)
                .Single(animationEvent =>
                    animationEvent.functionName == "OnNightfangSkillAnimationEnd");
            Assert.AreEqual(23f / 60f, startupEnd.time, 0.0001f);
            Assert.IsFalse(startupClip.isLooping);

            var explosionController = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                "Animation/NightfangAssassinBoom");
            Assert.IsNotNull(explosionController);
            var explosionClip = explosionController.animationClips
                .Single(clip => clip.name == "NightfangAssassinBoom");
            var explosionEvents = AnimationUtility.GetAnimationEvents(explosionClip);
            CollectionAssert.AreEquivalent(
                new[] { "OnDamageFrame", "OnAnimationEnd" },
                explosionEvents.Select(animationEvent => animationEvent.functionName).ToArray());
            Assert.AreEqual(
                0.2f,
                explosionEvents.Single(animationEvent =>
                    animationEvent.functionName == "OnDamageFrame").time,
                0.0001f);
            Assert.IsFalse(explosionClip.isLooping);
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
            Assert.AreEqual("AI", view.AiBattlefieldView.transform.FindUi("SideLabel").GetComponent<Text>().text);
            Assert.AreEqual("PLAYER", view.PlayerBattlefieldView.transform.FindUi("SideLabel").GetComponent<Text>().text);

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

            Assert.IsNotNull(battlefield.transform.FindUi("ART_PathLeft"));
            Assert.IsNotNull(battlefield.transform.FindUi("ART_PathRight"));
            Assert.IsNotNull(battlefield.transform.FindUi("ART_PathTop"));
            Assert.IsNotNull(battlefield.transform.FindUi("ART_PathBottom"));
            Assert.IsNotNull(battlefield.transform.FindUi("ART_Spawn"));
            Assert.IsNotNull(battlefield.transform.FindUi("ART_Hatchling"));
            Assert.IsNotNull(battlefield.transform.FindUi("RouteWaypoints/DragonGoal"));
            Assert.IsNotNull(battlefield.transform.FindUi("ART_EnemyMarker/ART_EnemyHpTrack/ART_EnemyHpFill"));
            Assert.IsNotNull(battlefield.transform.FindUi("ART_EnemyMarker/EnemyRuntimeLabel"));
            Assert.IsNotNull(battlefield.transform.FindUi("ART_EnemyMarker").GetComponent<EnemyView>());
            Assert.IsNotNull(battlefield.GetComponent<CombatFxView>());
            Assert.IsNotNull(battlefield.transform.FindUi("ART_AttackLine"));
            Assert.IsNotNull(battlefield.transform.FindUi("ART_BowProjectile"));
            Assert.IsNotNull(battlefield.transform.FindUi("ART_SpearPierceLine"));
            Assert.IsNotNull(battlefield.transform.FindUi("ART_RiderSweepCircle"));
            var starfallWarning = battlefield.transform.FindUi("ART_StarfallWarning")?.GetComponent<Image>();
            Assert.IsNotNull(starfallWarning);
            Assert.IsFalse(starfallWarning.raycastTarget);
            Assert.IsNotNull(battlefield.transform.FindUi("DamageNumber"));
            Assert.IsNotNull(battlefield.transform.FindUi("SuppliesGain"));
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
                battlefield.transform.FindUi("ART_PathLeft").GetComponent<RectTransform>(),
                battlefield.transform.FindUi("ART_PathRight").GetComponent<RectTransform>(),
                battlefield.transform.FindUi("ART_PathTop").GetComponent<RectTransform>(),
                battlefield.transform.FindUi("ART_PathBottom").GetComponent<RectTransform>()
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

                var outline = fill.transform.FindUi("ART_RangeOutline")?.GetComponent<Image>();
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
                foreach (var battlefield in new[]
                         {
                             screenView.PlayerBattlefieldView,
                             screenView.AiBattlefieldView
                         })
                {
                    var combatFx = battlefield.GetComponent<CombatFxView>();
                    Assert.IsNotNull(combatFx);
                    var serializedCombatFx = new SerializedObject(combatFx);
                    var diveTemplate = serializedCombatFx
                        .FindProperty("ART_FlameDrakeRiderDive")
                        .objectReferenceValue as Image;
                    Assert.IsNotNull(diveTemplate);
                    Assert.AreEqual("Flame Drake Rider S", diveTemplate.name);
                    Assert.IsFalse(diveTemplate.gameObject.activeSelf);
                    Assert.IsFalse(diveTemplate.raycastTarget);
                    Assert.IsTrue(diveTemplate.preserveAspect);
                    Assert.IsNotNull(diveTemplate.GetComponent<Animator>()?.runtimeAnimatorController);
                }
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
