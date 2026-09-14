using DragonBound.Presentation;
using DragonBound.Recruitment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Tests.EditMode
{
    public sealed class RecruitItemColorTests
    {
        [Test]
        public void RecruitAndHeroColorsFollowConfiguredPalette()
        {
            var root = new GameObject("BoardView", typeof(RectTransform), typeof(GreyboxBoardView));
            try
            {
                var view = root.GetComponent<GreyboxBoardView>();
                var basic = Color.white;
                var component = new Color(0.2f, 0.5f, 0.9f, 1f);
                var purple = new Color(0.6f, 0.2f, 0.9f, 1f);
                var gold = new Color(1f, 0.7f, 0.1f, 1f);
                view.ConfigureRecruitItemColors(basic, component, purple, gold);

                Assert.AreEqual(basic, view.GetRecruitItemColor(RecruitItemKind.BasicUnit));
                Assert.AreEqual(basic, view.GetRecruitItemColor(RecruitItemKind.Shovel));
                Assert.AreEqual(component, view.GetRecruitItemColor(RecruitItemKind.HeroComponent));
                Assert.AreEqual(purple, view.GetHeroRarityColor(HeroRecipeRarity.Purple));
                Assert.AreEqual(gold, view.GetHeroRarityColor(HeroRecipeRarity.Gold));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BeachItemAppliesSpriteAndTintOnlyToItsChildArtImage()
        {
            var root = new GameObject(
                "BeachItem",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(DraggableUnitView));
            try
            {
                var rootImage = root.GetComponent<Image>();
                rootImage.color = new Color(1f, 1f, 1f, 0f);
                var artObject = new GameObject(
                    "Image",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                artObject.transform.SetParent(root.transform, false);
                var artImage = artObject.GetComponent<Image>();
                var view = root.GetComponent<DraggableUnitView>();
                view.ConfigureBeach(artImage, null, null, root.GetComponent<CanvasGroup>());
                var expected = new Color(0.3f, 0.6f, 1f, 1f);

                view.SetCardColor(expected);

                Assert.AreEqual(expected, artImage.color);
                Assert.AreEqual(new Color(1f, 1f, 1f, 0f), rootImage.color);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisablingBeachItemDoesNotCreateSoulChainOverlayDuringLifecycleChange()
        {
            var root = new GameObject(
                "BeachItem",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(DraggableUnitView));
            try
            {
                var view = root.GetComponent<DraggableUnitView>();
                view.ConfigureBeach(
                    root.GetComponent<Image>(),
                    null,
                    null,
                    root.GetComponent<CanvasGroup>());

                root.SetActive(false);

                Assert.IsNull(root.transform.FindUi("ART_SoulChainOverlay"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
