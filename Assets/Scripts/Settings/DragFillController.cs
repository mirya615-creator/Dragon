using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class DragFillController : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private RectTransform track;
    private RectTransform fill;

    private void Awake()
    {
        track = (RectTransform)transform;

        Transform fillTransform = transform.Find("FillImg");
        fill = fillTransform as RectTransform;
        if (fill == null)
        {
            Debug.LogError($"{name} requires a direct child named 'FillImg'.", this);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        UpdateFill(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateFill(eventData);
    }

    private void UpdateFill(PointerEventData eventData)
    {
        if (fill == null)
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                track,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        Rect rect = track.rect;
        float amount = Mathf.Clamp01(Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x));

        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(amount, 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
    }
}
