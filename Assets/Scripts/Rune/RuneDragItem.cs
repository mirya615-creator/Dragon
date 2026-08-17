using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class RuneDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public string RuneId { get; private set; }

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Transform originalParent;
    private int originalSiblingIndex;
    private bool consumed;

    public void Initialize(string runeId)
    {
        RuneId = runeId;
        rectTransform = transform as RectTransform;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(RuneId)) return;

        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) transform.SetParent(canvas.rootCanvas.transform, true);
        canvasGroup.blocksRaycasts = false;
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (rectTransform != null) rectTransform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
        if (consumed || originalParent == null) return;

        transform.SetParent(originalParent, false);
        transform.SetSiblingIndex(originalSiblingIndex);
    }

    public void Consume()
    {
        consumed = true;
        Destroy(gameObject);
    }
}
