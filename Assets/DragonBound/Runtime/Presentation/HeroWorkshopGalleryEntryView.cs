using System;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    public sealed class HeroWorkshopGalleryEntryView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text statusLabel;

        public string HeroId { get; private set; }

        public void Configure(Button entryButton, Image backgroundImage, Text name, Text status)
        {
            button = entryButton;
            background = backgroundImage;
            nameLabel = name;
            statusLabel = status;
        }

        public void SetData(
            string heroId,
            string displayName,
            string status,
            Color color,
            bool selected,
            Action<string> onSelected)
        {
            HeroId = heroId;
            if (nameLabel != null)
            {
                nameLabel.text = displayName;
            }

            if (statusLabel != null)
            {
                statusLabel.text = status;
            }

            if (background != null)
            {
                background.color = color;
                var outline = background.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = selected ? new Color(1f, 0.88f, 0.30f, 1f) : new Color(0f, 0f, 0f, 0.55f);
                }
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onSelected?.Invoke(heroId));
            }
        }
    }
}
