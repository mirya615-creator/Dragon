using UnityEngine;
using DragonBound.Presentation;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class VisualStateToggleController : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private const float DragThresholdPixels = 8f;

    private Button toggleButton;
    private RectTransform track;
    private Image stateImage;
    private RectTransform handle;
    private bool value;
    private bool dragged;
    private Vector2 pointerDownPosition;

    protected virtual void Awake()
    {
        toggleButton = GetComponent<Button>();
        track = transform as RectTransform;

        Transform stateTransform = transform.FindUi("State");
        stateImage = stateTransform != null ? stateTransform.GetComponent<Image>() : null;
        if (stateImage == null)
        {
            Debug.LogError($"{name} requires a direct child named 'State' with an Image component.", this);
            enabled = false;
            return;
        }

        value = stateTransform.gameObject.activeSelf;
        stateTransform.gameObject.SetActive(true);
        stateImage.type = Image.Type.Filled;
        stateImage.fillMethod = Image.FillMethod.Horizontal;
        stateImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        stateImage.fillClockwise = true;

        if (stateTransform.childCount > 0)
        {
            handle = stateTransform.GetChild(0) as RectTransform;
        }

        RefreshVisuals();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanInteract()) return;
        pointerDownPosition = eventData.position;
        dragged = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!CanInteract()) return;
        if (!dragged && Vector2.Distance(pointerDownPosition, eventData.position) >= DragThresholdPixels)
        {
            dragged = true;
        }

        if (dragged) SetFromPointer(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!CanInteract()) return;

        if (dragged)
        {
            SetFromPointer(eventData);
        }
        else
        {
            SetValue(!value);
        }
    }

    private bool CanInteract()
    {
        return enabled && toggleButton != null && toggleButton.IsInteractable() && stateImage != null;
    }

    private void SetFromPointer(PointerEventData eventData)
    {
        if (track == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                track,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        SetValue(localPoint.x >= track.rect.center.x);
    }

    private void SetValue(bool newValue)
    {
        value = newValue;
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        if (stateImage != null)
        {
            stateImage.fillAmount = value ? 1f : 0f;
        }

        if (handle == null) return;

        float normalizedValue = value ? 1f : 0f;
        Vector2 anchorMin = handle.anchorMin;
        Vector2 anchorMax = handle.anchorMax;
        Vector2 anchoredPosition = handle.anchoredPosition;
        anchorMin.x = normalizedValue;
        anchorMax.x = normalizedValue;
        anchoredPosition.x = value
            ? -handle.rect.width * (1f - handle.pivot.x)
            : handle.rect.width * handle.pivot.x;
        handle.anchorMin = anchorMin;
        handle.anchorMax = anchorMax;
        handle.anchoredPosition = anchoredPosition;
    }
}
