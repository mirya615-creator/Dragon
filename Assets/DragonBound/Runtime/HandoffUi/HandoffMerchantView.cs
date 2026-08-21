using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DragonBound.HandoffUi
{
    [DisallowMultipleComponent]
    public sealed class HandoffMerchantView : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Transform offerContainer;
        [SerializeField] private HandoffMerchantOfferView offerPrefab;
        private readonly List<HandoffMerchantOfferView> entries = new List<HandoffMerchantOfferView>();
        private HandoffUiCommands commands;
        public int EntryCount => entries.Count;
        public HandoffMerchantOfferView OfferPrefab => offerPrefab;

        public void Bind(MerchantSnapshot snapshot, HandoffUiCommands commandBoundary)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            commands = commandBoundary ?? throw new ArgumentNullException(nameof(commandBoundary));
            if (offerPrefab == null || offerContainer == null) throw new InvalidOperationException("Merchant requires a serialized offer prefab and container.");
            statusLabel.text = snapshot.Status;
            EnsureEntries(snapshot.Offers.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                var visible = i < snapshot.Offers.Count;
                entries[i].gameObject.SetActive(visible);
                if (visible) entries[i].Bind(snapshot.Offers[i], SelectOffer);
            }
        }
        private void EnsureEntries(int count)
        {
            while (entries.Count < count)
            {
                var entry = Instantiate(offerPrefab, offerContainer);
                entry.name = "MerchantOffer_" + entries.Count;
                entry.gameObject.SetActive(true);
                entries.Add(entry);
            }
        }
        private void SelectOffer(string offerId)
        {
            commands.RequestMerchantOffer(offerId);
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].OfferId != offerId) entries[i].Bind(new MerchantOfferSnapshot(entries[i].OfferId, "", "", MerchantOfferState.Unavailable), SelectOffer);
            }
        }
    }
}
