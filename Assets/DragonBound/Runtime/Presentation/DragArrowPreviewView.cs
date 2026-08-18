using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    // The authored shaft and head can be replaced by UI artists without changing drag rules.
    public sealed class DragArrowPreviewView : MonoBehaviour
    {
        [SerializeField] private Image shaft;
        [SerializeField] private Text headLabel;

        public bool IsVisible => gameObject.activeSelf;

        private void Awake()
        {
            DisableAllGraphicRaycasts();
        }

        public void Configure(Image shaftImage, Text arrowHead)
        {
            shaft = shaftImage;
            headLabel = arrowHead;
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
            rect.sizeDelta = new Vector2(delta.magnitude, rect.sizeDelta.y);
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
    }
}
