using System.Collections.Generic;
using DragonBound.HandoffUi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Tests.EditMode
{
    public sealed class HandoffUiEditModeTests
    {
        private const string OfferPrefabPath = "Assets/DragonBound/UI/Handoff/Prefabs/HandoffMerchantOffer.prefab";
        private const string ScreenPrefabPath = "Assets/DragonBound/UI/Handoff/Prefabs/UI_HandoffScreen.prefab";

        [Test]
        public void HandoffPrefabs_UseSerializedOfferPrefabAndTmpViews()
        {
            var offer = AssetDatabase.LoadAssetAtPath<GameObject>(OfferPrefabPath);
            var screen = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPrefabPath);
            Assert.IsNotNull(offer); Assert.IsNotNull(offer.GetComponent<HandoffMerchantOfferView>());
            Assert.IsNotNull(screen); Assert.IsNotNull(screen.GetComponent<HandoffPreviewPresenter>());
            Assert.IsNotNull(screen.GetComponentInChildren<HandoffResponsiveLayout>(true));
            Assert.IsNotNull(screen.GetComponentInChildren<HandoffItemHudView>(true));
            Assert.IsNotNull(screen.GetComponentInChildren<HandoffMerchantView>(true).OfferPrefab);
        }

        [Test]
        public void StateSnapshots_KeepInputValuesAndCommandsPublishWithoutMutation()
        {
            var snapshot = new ItemHudSnapshot(ItemHudState.Cooldown, "ITEM", "WAIT", 7);
            Assert.AreEqual(ItemHudState.Cooldown, snapshot.State); Assert.AreEqual(7, snapshot.CooldownSeconds);
            var commands = new HandoffUiCommands(); var requested = string.Empty; commands.MerchantOfferRequested += id => requested = id;
            commands.RequestMerchantOffer("ward"); Assert.AreEqual("ward", requested);
        }

        [Test]
        public void MerchantSelection_DisablesTheOtherTwoOffers()
        {
            var root = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPrefabPath)) as GameObject;
            try
            {
                var view = root.GetComponentInChildren<HandoffMerchantView>(true);
                view.Bind(new MerchantSnapshot(new List<MerchantOfferSnapshot>
                {
                    new MerchantOfferSnapshot("a", "A", "", MerchantOfferState.Normal), new MerchantOfferSnapshot("b", "B", "", MerchantOfferState.Ad), new MerchantOfferSnapshot("c", "C", "", MerchantOfferState.Normal)
                }, ""), new HandoffUiCommands());
                var offer = view.GetComponentsInChildren<HandoffMerchantOfferView>(true)[1]; offer.GetComponentInChildren<Button>(true).onClick.Invoke();
                var entries = view.GetComponentsInChildren<HandoffMerchantOfferView>(true);
                Assert.AreEqual(MerchantOfferState.Unavailable, entries[0].State); Assert.AreEqual(MerchantOfferState.Ad, entries[1].State); Assert.AreEqual(MerchantOfferState.Unavailable, entries[2].State);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MerchantEmptySnapshot_CreatesNoOfferEntries()
        {
            var root = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPrefabPath)) as GameObject;
            try { var view = root.GetComponentInChildren<HandoffMerchantView>(true); view.Bind(new MerchantSnapshot(new List<MerchantOfferSnapshot>(), "EMPTY"), new HandoffUiCommands()); Assert.AreEqual(0, view.EntryCount); }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
