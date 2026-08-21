using System.Linq;
using DragonBound.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Tests.EditMode
{
    public sealed class RuneLoadoutPrefabTests
    {
        private const string ModulePath = "Assets/DragonBound/UI/Prefabs/Modules/RuneLoadout.prefab";
        private const string ScreenPath = "Assets/DragonBound/UI/Prefabs/Screens/DragonBoundPortraitScreen.prefab";

        [Test]
        public void GreyboxLoadoutPrefab_ExposesRuneArtContractsAndScreenEntry()
        {
            var module = AssetDatabase.LoadAssetAtPath<GameObject>(ModulePath);
            Assert.IsNotNull(module, ModulePath);
            Assert.IsNotNull(module.GetComponent<RuneLoadoutView>());
            Assert.IsNotNull(module.transform.Find("ART_RuneLoadoutPanel"));
            Assert.IsNotNull(module.transform.Find("ART_RuneLoadoutPanel/ART_RuneFilters"));
            Assert.IsNotNull(module.transform.Find("ART_RuneLoadoutPanel/ART_RuneHeroGrid"));
            Assert.IsNotNull(module.transform.Find("ART_RuneLoadoutPanel/ART_RuneGrid"));
            foreach (var image in module.GetComponentsInChildren<Image>(true))
            {
                StringAssert.StartsWith("ART_", image.gameObject.name, image.name);
            }

            var screen = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath);
            var view = screen.GetComponent<DragonBoundScreenView>();
            Assert.IsNotNull(view.RuneLoadoutView);
            Assert.IsNotNull(view.RecruitmentView.RuneLoadoutButton);
            Assert.AreEqual(14, DragonBound.Runes.RuneCatalog.All.Count);
            Assert.IsTrue(DragonBound.Runes.RuneCatalog.All.All(rune =>
                !string.IsNullOrWhiteSpace(new DragonBound.Runes.RunePresentationCatalog().Get(rune.RuneId).ArtAssetKey)));
        }
    }
}
