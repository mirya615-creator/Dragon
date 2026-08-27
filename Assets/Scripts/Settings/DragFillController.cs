using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class DragFillController : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public enum VolumeChannel
    {
        Auto,
        Music,
        Sfx,
        Custom
    }

    private const string MusicPrefsKey = "Settings.MusicVolume";
    private const string SfxPrefsKey = "Settings.SfxVolume";

    [Header("Value")]
    [SerializeField] private VolumeChannel channel = VolumeChannel.Auto;
    [SerializeField] private string customPrefsKey = string.Empty;
    [SerializeField, Range(0f, 1f)] private float defaultValue = 1f;
    [SerializeField] private bool saveValue = true;

    [Header("Optional bindings")]
    [SerializeField] private Image fillImage;
    [SerializeField] private RectTransform handle;
    [SerializeField] private UnityEvent<float> onValueChanged = new UnityEvent<float>();

    private RectTransform track;
    private RectTransform fillRect;
    private string prefsKey;
    private float value;
    private bool valueIsDirty;

    /// <summary>The current normalized value. Setting it updates the UI and persisted setting.</summary>
    public float Value
    {
        get => value;
        set => SetValue(value);
    }

    /// <summary>
    /// Runtime listeners can use this when an AudioMixer or audio service is created.
    /// The Inspector-facing equivalent is On Value Changed.
    /// </summary>
    public event System.Action<float> ValueChanged;

    private void Awake()
    {
        track = (RectTransform)transform;

        Transform fillTransform = transform.Find("FillImg");
        fillRect = fillTransform as RectTransform;
        if (fillRect == null)
        {
            Debug.LogError($"{name} requires a direct child named 'FillImg'.", this);
            enabled = false;
            return;
        }

        if (fillImage == null)
        {
            fillImage = fillTransform.GetComponent<Image>();
        }

        if (fillImage == null)
        {
            Debug.LogError($"{name}/FillImg requires an Image component.", this);
            enabled = false;
            return;
        }

        if (handle == null && fillTransform.childCount > 0)
        {
            handle = fillTransform.GetChild(0) as RectTransform;
        }

        // Filled images reveal the sprite without changing the RectTransform dimensions.
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillClockwise = true;

        prefsKey = ResolvePrefsKey();
        float initialValue = saveValue && !string.IsNullOrEmpty(prefsKey)
            ? PlayerPrefs.GetFloat(prefsKey, defaultValue)
            : defaultValue;
        SetValueWithoutNotify(initialValue);
        NotifyValueChanged();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        UpdateValueFromPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateValueFromPointer(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        SaveIfDirty();
    }

    public void SetValue(float newValue)
    {
        newValue = Mathf.Clamp01(newValue);
        if (Mathf.Approximately(value, newValue))
        {
            return;
        }

        value = newValue;
        RefreshVisuals();

        if (saveValue && !string.IsNullOrEmpty(prefsKey))
        {
            PlayerPrefs.SetFloat(prefsKey, value);
            valueIsDirty = true;
        }

        NotifyValueChanged();
    }

    public void SetValueWithoutNotify(float newValue)
    {
        value = Mathf.Clamp01(newValue);
        RefreshVisuals();
    }

    private void UpdateValueFromPointer(PointerEventData eventData)
    {
        if (fillImage == null)
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
        SetValue(amount);
    }

    private void RefreshVisuals()
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = value;
        }

        // The old implementation moved this child because FillImg itself became narrower.
        // With a fixed FillImg, move only the handle's anchors to the current value.
        if (handle != null)
        {
            Vector2 anchorMin = handle.anchorMin;
            Vector2 anchorMax = handle.anchorMax;
            anchorMin.x = value;
            anchorMax.x = value;
            handle.anchorMin = anchorMin;
            handle.anchorMax = anchorMax;
        }
    }

    private string ResolvePrefsKey()
    {
        VolumeChannel resolvedChannel = channel;
        if (resolvedChannel == VolumeChannel.Auto)
        {
            Transform part = transform.parent;
            if (part != null && part.name == "MusicPart")
            {
                resolvedChannel = VolumeChannel.Music;
            }
            else if (part != null && part.name == "SFXPart")
            {
                resolvedChannel = VolumeChannel.Sfx;
            }
        }

        switch (resolvedChannel)
        {
            case VolumeChannel.Music:
                return MusicPrefsKey;
            case VolumeChannel.Sfx:
                return SfxPrefsKey;
            case VolumeChannel.Custom:
                return customPrefsKey;
            default:
                return string.Empty;
        }
    }

    private void NotifyValueChanged()
    {
        onValueChanged?.Invoke(value);
        ValueChanged?.Invoke(value);
    }

    private void SaveIfDirty()
    {
        if (!valueIsDirty)
        {
            return;
        }

        PlayerPrefs.Save();
        valueIsDirty = false;
    }

    private void OnDisable()
    {
        SaveIfDirty();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveIfDirty();
        }
    }
}
