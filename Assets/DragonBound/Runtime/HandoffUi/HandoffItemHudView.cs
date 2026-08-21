using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.HandoffUi
{
    [DisallowMultipleComponent]
    public sealed class HandoffItemHudView : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text detailLabel;
        [SerializeField] private Button actionButton;
        [SerializeField] private Image iconImage;
        [Header("Art hooks")]
        [SerializeField] private Sprite iconSprite;
        [SerializeField] private Material iconMaterial;
        [SerializeField] private RuntimeAnimatorController animatorController;
        [SerializeField] private AudioClip selectSfx;
        [SerializeField] private GameObject useVfxPrefab;

        private HandoffUiCommands commands;
        public ItemHudSnapshot Snapshot { get; private set; }
        public Sprite IconSprite => iconSprite;
        public Material IconMaterial => iconMaterial;
        public RuntimeAnimatorController AnimatorController => animatorController;
        public AudioClip SelectSfx => selectSfx;
        public GameObject UseVfxPrefab => useVfxPrefab;

        public void Bind(ItemHudSnapshot snapshot, HandoffUiCommands commandBoundary)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            commands = commandBoundary ?? throw new ArgumentNullException(nameof(commandBoundary));
            titleLabel.text = Snapshot.Title;
            detailLabel.text = Snapshot.Detail;
            actionButton.interactable = Snapshot.State == ItemHudState.Available || Snapshot.State == ItemHudState.Selected;
            if (iconImage != null)
            {
                iconImage.sprite = iconSprite;
                iconImage.material = iconMaterial;
                iconImage.enabled = iconSprite != null;
            }
        }

        private void Awake() => actionButton.onClick.AddListener(RequestItem);
        private void OnDestroy() => actionButton.onClick.RemoveListener(RequestItem);
        private void RequestItem() => commands?.RequestItem();
    }
}
