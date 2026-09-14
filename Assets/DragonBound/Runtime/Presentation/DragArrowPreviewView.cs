using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    // The authored shaft and head can be replaced by UI artists without changing drag rules.
    public sealed class DragArrowPreviewView : MonoBehaviour
    {
        private const string DragPathSpriteResourcePath = "GameUI/SlectRoad";
        private const float DragPathRectHeight = 180f;

        [SerializeField] private Image shaft;
        [SerializeField] private Text headLabel;

        public bool IsVisible => gameObject.activeSelf;
        public Sprite PathSprite => shaft != null ? shaft.sprite : null;

        private void Awake()
        {
            ApplyAuthoredPathSprite();
            DisableAllGraphicRaycasts();
        }

        public void Configure(Image shaftImage, Text arrowHead)
        {
            shaft = shaftImage;
            headLabel = arrowHead;
            ApplyAuthoredPathSprite();
            DisableAllGraphicRaycasts();

            Hide();
        }

        public void Show(RectTransform parent, Vector3 sourceWorld, Vector3 targetWorld)
        {
            if (parent == null)
            {
                Hide();
                return;
            }

            var source = (Vector2)parent.InverseTransformPoint(sourceWorld);
            var target = (Vector2)parent.InverseTransformPoint(targetWorld);
            var delta = target - source;
            if (delta.sqrMagnitude < 0.01f)
            {
                Hide();
                return;
            }

            var rect = (RectTransform)transform;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = source;
            // SlectRoad has generous transparent padding around its centered line art.
            // A taller rect keeps the visible line readable without modifying the source asset.
            rect.sizeDelta = new Vector2(delta.magnitude, DragPathRectHeight);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            // Keep the authored arrow art above runtime unit cards without changing board state.
            transform.SetAsLastSibling();
            DisableAllGraphicRaycasts();
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void DisableAllGraphicRaycasts()
        {
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }

        private void ApplyAuthoredPathSprite()
        {
            if (shaft == null)
            {
                return;
            }

            var pathSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(DragPathSpriteResourcePath);
            if (pathSprite == null)
            {
                throw new System.InvalidOperationException(
                    $"Missing drag path sprite at Resources/{DragPathSpriteResourcePath}.");
            }

            shaft.sprite = pathSprite;
            shaft.type = Image.Type.Simple;
            shaft.preserveAspect = false;
            shaft.color = Color.white;
            if (headLabel != null)
            {
                headLabel.gameObject.SetActive(false);
            }
        }
    }
}
