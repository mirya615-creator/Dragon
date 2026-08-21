using System;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    /// <summary>Reusable greybox entry. ArtAssetKey stays a resource contract for final Rune art.</summary>
    public sealed class RuneLoadoutEntryView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text detailLabel;
        [SerializeField] private Text artKeyLabel;

        public string EntryId { get; private set; }
        public string ArtAssetKey { get; private set; }
        public Button Button => button;

        public void Configure(Button value, Image backing, Text title, Text detail, Text artKey)
        {
            button = value;
            background = backing;
            titleLabel = title;
            detailLabel = detail;
            artKeyLabel = artKey;
        }

        public void SetData(
            string id,
            string title,
            string detail,
            string artKey,
            Color color,
            bool selected,
            bool interactable,
            Action<string> selectedAction)
        {
            EntryId = id;
            ArtAssetKey = artKey ?? string.Empty;
            if (titleLabel != null) titleLabel.text = title ?? string.Empty;
            if (detailLabel != null) detailLabel.text = detail ?? string.Empty;
            if (artKeyLabel != null) artKeyLabel.text = ArtAssetKey;
            if (background != null)
            {
                background.color = selected
                    ? Color.Lerp(color, Color.white, 0.28f)
                    : color;
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => selectedAction?.Invoke(EntryId));
                button.interactable = interactable;
            }
        }
    }
}
