using System.Collections;
using DragonBound.HandoffUi;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DragonBound.Tests.PlayMode
{
    public sealed class HandoffUiPlayModeTests
    {
        private const string PreviewScenePath = "Assets/DragonBound/Scenes/UI_Handoff.unity";

        [UnityTest]
        public IEnumerator PreviewScene_LoadsWithSerializedPrefabReferences()
        {
            var operation = LoadPreviewScene();
            Assert.IsNotNull(operation);
            yield return operation; yield return null;
            var presenter = Object.FindObjectOfType<HandoffPreviewPresenter>();
            Assert.IsNotNull(presenter); Assert.IsNotNull(presenter.ItemHudView); Assert.IsNotNull(presenter.MerchantView); Assert.IsNotNull(presenter.MerchantView.OfferPrefab); Assert.AreEqual(3, presenter.MerchantView.EntryCount);
        }

        [UnityTest]
        public IEnumerator ResponsiveContainer_HandlesPhoneAndTabletAspectRanges()
        {
            var operation = LoadPreviewScene();
            Assert.IsNotNull(operation);
            yield return operation; yield return null;
            var layout = Object.FindObjectOfType<HandoffResponsiveLayout>();
            Assert.IsNotNull(layout); Assert.Greater(layout.PhoneMaxWidth, 0f); Assert.GreaterOrEqual(layout.TabletMaxWidth, layout.PhoneMaxWidth);
        }

        private static AsyncOperation LoadPreviewScene()
        {
            return EditorSceneManager.LoadSceneAsyncInPlayMode(
                PreviewScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
        }
    }
}
