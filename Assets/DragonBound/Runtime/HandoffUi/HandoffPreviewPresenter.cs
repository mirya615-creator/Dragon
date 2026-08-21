using System.Collections.Generic;
using UnityEngine;

namespace DragonBound.HandoffUi
{
    [DisallowMultipleComponent]
    public sealed class HandoffPreviewPresenter : MonoBehaviour
    {
        [SerializeField] private HandoffItemHudView itemHudView;
        [SerializeField] private HandoffMerchantView merchantView;
        [SerializeField] private ItemHudState initialItemState = ItemHudState.Available;
        [SerializeField] private MerchantOfferState initialSecondOfferState = MerchantOfferState.Ad;
        private HandoffUiCommands commands;
        public HandoffItemHudView ItemHudView => itemHudView;
        public HandoffMerchantView MerchantView => merchantView;

        private void Start()
        {
            Show(initialItemState, initialSecondOfferState);
        }
        private void EnsureCommands()
        {
            if (commands != null) return;
            commands = new HandoffUiCommands();
            commands.ItemRequested += HandleItemRequested;
            commands.MerchantOfferRequested += HandleMerchantRequested;
        }
        private void OnDestroy()
        {
            if (commands == null) return;
            commands.ItemRequested -= HandleItemRequested;
            commands.MerchantOfferRequested -= HandleMerchantRequested;
        }
        public void Show(ItemHudState itemState, MerchantOfferState secondOfferState)
        {
            EnsureCommands();
            itemHudView.Bind(new ItemHudSnapshot(itemState, "RUN ITEM", ItemDetail(itemState)), commands);
            merchantView.Bind(new MerchantSnapshot(new List<MerchantOfferSnapshot>
            {
                new MerchantOfferSnapshot("pulse", "PULSE CORE", "Mock item offer", MerchantOfferState.Normal),
                new MerchantOfferSnapshot("ward", "WARD PLATE", "Mock ad offer", secondOfferState),
                new MerchantOfferSnapshot("flare", "FLARE KIT", "Mock item offer", MerchantOfferState.Normal)
            }, "MERCHANT | CHOOSE ONE"), commands);
        }
        private void HandleItemRequested() => itemHudView.Bind(new ItemHudSnapshot(ItemHudState.Selected, "RUN ITEM", "SELECTED | MOCK ONLY"), commands);
        private void HandleMerchantRequested(string id) { }
        private static string ItemDetail(ItemHudState state)
        {
            switch (state) { case ItemHudState.Locked: return "LOCKED | FIRST 4 RUNS"; case ItemHudState.UnlockNotice: return "UNLOCKED AFTER RUN 5"; case ItemHudState.Empty: return "NO ITEM EQUIPPED"; case ItemHudState.Cooldown: return "COOLDOWN"; case ItemHudState.Disabled: return "DISABLED"; case ItemHudState.Selected: return "SELECTED"; default: return "AVAILABLE"; }
        }
    }
}
