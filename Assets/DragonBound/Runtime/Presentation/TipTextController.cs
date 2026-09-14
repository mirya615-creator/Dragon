using System.Collections;
using DragonBound.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在 Assets/Resources/prefabs/TipText.prefab 根节点。
/// 显示设定的文字，默认 3 秒后自动隐藏；重复触发时先取消上一次计时。
/// 背景条尺寸随文案自适应：宽 clamp(自然宽 + 左右内边距, MinTipWidth, MaxTipWidth)，
/// 高 = 行数 × RowHeightPerLine。
/// </summary>
[DisallowMultipleComponent]
public sealed class TipTextController : MonoBehaviour
{
    private const float DefaultShowSeconds = 3f;

    // ---- 尺寸自适应常量（样式调整只改这里）----
    private const float MaxTipWidth = 580f;  // 最大宽：超过则折行
    private const float MinTipWidth = 120f;  // 最小宽防过窄；设为 0 则完全贴合文本
    private const float HorizontalPadding = 10f; // 背景左右各保留 10px
    private const float RowHeightPerLine = 80f;   // 每行高：1 行=80，N 行=N×80
    // -------------------------------------------------

    private TMP_Text label;
    private Coroutine hideRoutine;

    /// <summary>显示一条提示，默认 3 秒后消失。</summary>
    public void Show(string message) => Show(message, DefaultShowSeconds, null);

    public void Hide()
    {
        if (this == null) return;
        ShowInternal(string.Empty, null, null);
    }

    /// <summary>显示一条提示，指定时长；position 为 null 时沿用预制体初始位置。</summary>
    public void Show(string message, float seconds, Vector2? anchoredPosition = null)
    {
        if (this == null) return;
        ShowInternal(message, Mathf.Max(0f, seconds), anchoredPosition);
    }

    private void ShowInternal(
        string message,
        float? seconds,
        Vector2? anchoredPosition)
    {
        // A scene owner can still hold Unity's managed wrapper while the native
        // component is already gone. Do not resolve children in that state.
        if (this == null) return;

        if (label == null) Resolve();

        if (label == null)
        {
            Debug.LogError("TipText.prefab requires a TextMeshProUGUI label.", this);
            gameObject.SetActive(false);
            return;
        }

        if (hideRoutine != null)          // 先取消上次计时（替换式语义）
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (anchoredPosition.HasValue)
            (transform as RectTransform).anchoredPosition = anchoredPosition.Value;

        if (string.IsNullOrEmpty(message))   // 空串 = 隐藏（兼容旧调用点语义）
        {
            label.text = string.Empty;
            gameObject.SetActive(false);
            return;
        }

        label.text = message;
        gameObject.SetActive(true);      // ① 先激活：inactive 时 ForceMeshUpdate 不产 mesh
        ResizeToText();                  // ② 再量尺寸：与 SetActive 同帧完成，不闪跳
        transform.SetAsLastSibling();    // ③ 置顶，避免被其他面板遮挡
        if (seconds.HasValue)
        {
            hideRoutine = StartCoroutine(HideAfterDelay(seconds.Value));
        }
    }

    private void ResizeToText()
    {
        RectTransform root = (RectTransform)transform;

        // 1) 量「不换行自然宽」：临时关折行，避免被当前根宽/上一条的宽度干扰
        bool wasWrap = label.enableWordWrapping;
        label.enableWordWrapping = false;
        float naturalWidth = label.GetPreferredValues(label.text).x;
        label.enableWordWrapping = wasWrap;

        // 2) 根背景宽度包含左右各 10px 的可见边框；最大值仍为整体最大宽度
        float targetWidth = Mathf.Clamp(
            naturalWidth + HorizontalPadding * 2f,
            MinTipWidth,
            MaxTipWidth);

        // 3) 设宽后重排文本，ForceMeshUpdate 后再按真实行数定高（含显式 \n）
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
        label.ForceMeshUpdate();
        int lines = Mathf.Max(1, label.textInfo.lineCount);
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, lines * RowHeightPerLine);
    }

    private IEnumerator HideAfterDelay(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);   // 暂停/时间缩放不影响时长
        hideRoutine = null;
        if (label == null) yield break;
        label.text = string.Empty;
        gameObject.SetActive(false);
    }

    private void Resolve()
    {
        label = GetComponent<TextMeshProUGUI>();
        if (label == null && transform.FindUi("Text") != null)
            label = transform.FindUi("Text").GetComponent<TextMeshProUGUI>();
    }

    private void OnDisable()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }
    }
}
