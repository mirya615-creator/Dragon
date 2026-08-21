using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.HandoffUi
{
    [DisallowMultipleComponent]
    public sealed class HandoffMerchantOfferView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text detailLabel;
        [SerializeField] private TMP_Text stateLabel;
        [SerializeField] private Button selectButton;
        [SerializeField] private Image itemImage;
        [SerializeField] private Image cardImage;
        [Header("Art hooks")]
        [SerializeField] private Sprite itemSprite;
        [SerializeField] private Material cardMaterial;
        [SerializeField] private RuntimeAnimatorController animatorController;
        [SerializeField] private GameObject claimVfxPrefab;
        [SerializeField] private AudioClip selectSfx;

        private Action<string> selected;
        public string OfferId { get; private set; }
        public MerchantOfferState State { get; private set; }
        public Sprite ItemSprite => itemSprite;
        public Material CardMaterial => cardMaterial;
        public RuntimeAnimatorController AnimatorController => animatorController;
        public GameObject ClaimVfxPrefab => claimVfxPrefab;
        public AudioClip SelectSfx => selectSfx;

        private void OnDestroy() => selectButton.onClick.RemoveListener(Select);
        public void Bind(MerchantOfferSnapshot snapshot, Action<string> selectAction)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            selectButton.onClick.RemoveListener(Select);
            selectButton.onClick.AddListener(Select);
            OfferId = snapshot.Id; State = snapshot.State; selected = selectAction;
            titleLabel.text = snapshot.Title; detailLabel.text = snapshot.Detail; stateLabel.text = LabelFor(snapshot.State);
            selectButton.interactable = snapshot.State == MerchantOfferState.Normal || snapshot.State == MerchantOfferState.Ad;
            if (itemImage != null)
            {
                itemImage.sprite = itemSprite;
                itemImage.enabled = itemSprite != null;
            }
            if (cardImage != null) cardImage.material = cardMaterial;
        }
        private void Select() => selected?.Invoke(OfferId);
        private static string LabelFor(MerchantOfferState state)
        {
            switch (state) { case MerchantOfferState.Ad: return "WATCH AD"; case MerchantOfferState.Claimed: return "CLAIMED"; case MerchantOfferState.Unavailable: return "UNAVAILABLE"; case MerchantOfferState.Expired: return "EXPIRED"; case MerchantOfferState.Loading: return "LOADING"; case MerchantOfferState.Error: return "RETRY LATER"; default: return "SELECT"; }
        }
    }
}
