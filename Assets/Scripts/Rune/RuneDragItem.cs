using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RuneDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public string RuneId { get; private set; }

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private RectTransform dragProxy;
    private Transform amountRoot;
    private TMP_Text amountText;
    private int availableCount;
    private float originalAlpha = 1f;
    private bool originalBlocksRaycasts = true;
    private bool consumed;

    public void Initialize(string runeId, int completeRuneCount)
    {
        RuneId = runeId;
        availableCount = Mathf.Max(0, completeRuneCount);
        rectTransform = transform as RectTransform;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        amountRoot = transform.Find("AcText");
        amountText = amountRoot != null
            ? amountRoot.Find("Text (TMP)")?.GetComponent<TMP_Text>()
            : null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(RuneId) || availableCount <= 0 || dragProxy != null) return;

        consumed = false;
        originalAlpha = canvasGroup.alpha;
        originalBlocksRaycasts = canvasGroup.blocksRaycasts;
        canvasGroup.blocksRaycasts = false;

        dragProxy = CreateVisualProxy();
        if (dragProxy != null) dragProxy.position = eventData.position;

        if (availableCount > 1)
        {
            // The original card stays in WeaponContainer and represents the remaining copies.
            if (amountRoot != null) amountRoot.gameObject.SetActive(true);
            if (amountText != null) amountText.text = (availableCount - 1).ToString();
        }
        else
        {
            // Keep the layout slot stable while its only copy follows the pointer.
            canvasGroup.alpha = 0f;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragProxy != null) dragProxy.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DestroyProxy();
        canvasGroup.blocksRaycasts = originalBlocksRaycasts;

        if (!consumed)
        {
            RestoreSourceVisual();
        }
    }

    private void OnDisable()
    {
        DestroyProxy();
        if (!consumed) RestoreSourceVisual();
    }

    public void Consume()
    {
        // The controller commits and rebuilds inventory on the next frame.
        consumed = true;
    }

    private RectTransform CreateVisualProxy()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform dragRoot = canvas != null ? canvas.rootCanvas.transform : transform.root;

        var proxyObject = new GameObject(
            $"DragProxy_{RuneId}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup));
        RectTransform proxyRect = (RectTransform)proxyObject.transform;
        proxyRect.SetParent(dragRoot, false);
        proxyRect.SetAsLastSibling();

        Vector2 size = rectTransform != null ? rectTransform.rect.size : Vector2.zero;
        if (size.x <= 0f || size.y <= 0f) size = new Vector2(240f, 140f);
        proxyRect.sizeDelta = size;

        Image proxyImage = proxyObject.GetComponent<Image>();
        Image sourceImage = GetComponent<Image>();
        if (sourceImage != null)
        {
            proxyImage.sprite = sourceImage.sprite;
            proxyImage.overrideSprite = sourceImage.overrideSprite;
            proxyImage.color = sourceImage.color;
            proxyImage.material = sourceImage.material;
            proxyImage.type = sourceImage.type;
            proxyImage.preserveAspect = sourceImage.preserveAspect;
        }
        else
        {
            proxyImage.color = new Color(1f, 1f, 1f, 0.95f);
        }
        proxyImage.raycastTarget = false;

        CloneVisualChild("Name", proxyRect);

        CanvasGroup proxyGroup = proxyObject.GetComponent<CanvasGroup>();
        proxyGroup.alpha = 0.92f;
        proxyGroup.blocksRaycasts = false;
        proxyGroup.interactable = false;
        return proxyRect;
    }

    private void CloneVisualChild(string childName, RectTransform proxyParent)
    {
        Transform source = transform.Find(childName);
        if (source == null) return;

        GameObject clone = Instantiate(source.gameObject, proxyParent, false);
        clone.name = source.name;
        Graphic[] graphics = clone.GetComponentsInChildren<Graphic>(true);
        for (int index = 0; index < graphics.Length; index++)
        {
            graphics[index].raycastTarget = false;
        }
    }

    private void RestoreSourceVisual()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = originalAlpha;
            canvasGroup.blocksRaycasts = originalBlocksRaycasts;
        }

        if (amountRoot != null) amountRoot.gameObject.SetActive(availableCount > 0);
        if (amountText != null) amountText.text = availableCount.ToString();
    }

    private void DestroyProxy()
    {
        if (dragProxy == null) return;
        Destroy(dragProxy.gameObject);
        dragProxy = null;
    }
}
