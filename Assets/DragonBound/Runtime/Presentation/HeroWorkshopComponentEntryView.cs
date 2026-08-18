using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    // This is intentionally a small data-binding view so card art and typography stay prefab-authored.
    public sealed class HeroWorkshopComponentEntryView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text countLabel;
        [SerializeField] private Text stateLabel;

        public string ComponentId { get; private set; }

        public void Configure(Image iconImage, Text name, Text count, Text state)
        {
            icon = iconImage;
            nameLabel = name;
            countLabel = count;
            stateLabel = state;
        }

        public void SetData(string componentId, string displayName, string count, string state, Color color)
        {
            ComponentId = componentId;
            if (icon != null)
            {
                icon.color = color;
            }

            if (nameLabel != null)
            {
                nameLabel.text = displayName;
            }

            if (countLabel != null)
            {
                countLabel.text = count;
            }

            if (stateLabel != null)
            {
                stateLabel.text = state;
            }
        }
    }
}
