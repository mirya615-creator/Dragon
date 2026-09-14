using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum LoadingPanelEntranceMode
{
    Vertical,
    Horizontal
}

/// <summary>
/// Animates the authored player/enemy loading content without changing its layout.
/// Every target receives the same side-specific offset and returns to its cached
/// anchored position, so the relative spacing authored in the prefab is preserved.
/// </summary>
[DisallowMultipleComponent]
public sealed class LoadingPanelEntranceAnimator : MonoBehaviour
{
    [SerializeField] private LoadingPanelEntranceMode entranceMode = LoadingPanelEntranceMode.Vertical;
    [SerializeField, Min(0.05f)] private float duration = 0.6f;
    [SerializeField, Min(0f)] private float horizontalPlayerDelay = 0.05f;
    [SerializeField] private bool playOnEnable = true;

    private readonly List<MotionTarget> enemyTargets = new List<MotionTarget>();
    private readonly List<MotionTarget> playerTargets = new List<MotionTarget>();
    private Coroutine entranceRoutine;
    private bool positionsCaptured;

    public LoadingPanelEntranceMode EntranceMode
    {
        get => entranceMode;
        set => entranceMode = value;
    }

    public bool IsEntranceComplete { get; private set; }

    private void OnEnable()
    {
        // This presentation belongs exclusively to the authored
        // Greybox_Main/Canvas/LoadingPanel node.
        if (transform.parent == null || transform.parent.name != "Canvas")
        {
            enabled = false;
            return;
        }

        if (playOnEnable) Play();
    }

    private void OnDisable()
    {
        if (entranceRoutine != null)
        {
            StopCoroutine(entranceRoutine);
            entranceRoutine = null;
        }

        RestoreFinalPositions();
        IsEntranceComplete = false;
    }

    public void Play()
    {
        if (!isActiveAndEnabled) return;

        if (entranceRoutine != null)
        {
            StopCoroutine(entranceRoutine);
            entranceRoutine = null;
        }

        RestoreFinalPositions();
        Canvas.ForceUpdateCanvases();
        CaptureTargets();

        if (enemyTargets.Count == 0 || playerTargets.Count == 0)
        {
            IsEntranceComplete = true;
            Debug.LogWarning(
                "LoadingPanel entrance requires EnemyPart/EnemyItem and MyPart/MyItem.",
                this);
            return;
        }

        Vector2 enemyOffset;
        Vector2 playerOffset;
        CalculateStartOffsets(out enemyOffset, out playerOffset);
        ApplyProgress(enemyTargets, enemyOffset, 0f);
        ApplyProgress(playerTargets, playerOffset, 0f);

        IsEntranceComplete = false;
        entranceRoutine = StartCoroutine(PlayRoutine(enemyOffset, playerOffset));
    }

    public void Replay(LoadingPanelEntranceMode mode)
    {
        entranceMode = mode;
        Play();
    }

    [ContextMenu("Preview Vertical Entrance")]
    private void PreviewVertical()
    {
        Replay(LoadingPanelEntranceMode.Vertical);
    }

    [ContextMenu("Preview Horizontal Entrance")]
    private void PreviewHorizontal()
    {
        Replay(LoadingPanelEntranceMode.Horizontal);
    }

    [ContextMenu("Reset Entrance Preview")]
    private void ResetPreview()
    {
        if (entranceRoutine != null)
        {
            StopCoroutine(entranceRoutine);
            entranceRoutine = null;
        }

        RestoreFinalPositions();
        IsEntranceComplete = true;
    }

    private IEnumerator PlayRoutine(Vector2 enemyOffset, Vector2 playerOffset)
    {
        // StartCoroutine advances immediately until the first yield. During scene
        // activation Time.unscaledDeltaTime can contain the entire loading stall,
        // which used to complete this 0.6s motion before the first rendered frame.
        // Keep the authored content at its off-screen start for one real frame first.
        yield return null;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.05f, duration);
        float playerDelay = entranceMode == LoadingPanelEntranceMode.Horizontal
            ? Mathf.Max(0f, horizontalPlayerDelay)
            : 0f;
        float totalDuration = safeDuration + playerDelay;

        while (elapsed < totalDuration)
        {
            // Do not let a scene-loading hitch skip the visible entrance. A 50ms
            // cap still follows genuinely slow rendered frames without consuming
            // an arbitrarily large activation-frame delta in one update.
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float enemyProgress = EaseOutCubic(Mathf.Clamp01(elapsed / safeDuration));
            float playerProgress = EaseOutCubic(
                Mathf.Clamp01((elapsed - playerDelay) / safeDuration));
            ApplyProgress(enemyTargets, enemyOffset, enemyProgress);
            ApplyProgress(playerTargets, playerOffset, playerProgress);
            yield return null;
        }

        RestoreFinalPositions();
        entranceRoutine = null;
        IsEntranceComplete = true;
    }

    private void CaptureTargets()
    {
        enemyTargets.Clear();
        playerTargets.Clear();

        Transform enemyPart = transform.Find("BG/EnemyPart");
        Transform playerPart = transform.Find("BG/MyPart");
        AddPartTargets(enemyPart, "EnemyItem", enemyTargets);
        AddPartTargets(playerPart, "MyItem", playerTargets);
        positionsCaptured = true;
    }

    private static void AddPartTargets(
        Transform part,
        string itemName,
        List<MotionTarget> targets)
    {
        if (part == null) return;

        Transform item = part.Find(itemName);
        AddTarget(item, targets);

        // Only add a direct sibling named Image. Images nested under the item move
        // with their parent and must not receive the offset a second time.
        Transform image = part.Find("Image");
        if (image != null && (item == null || !image.IsChildOf(item)))
        {
            AddTarget(image, targets);
        }
    }

    private static void AddTarget(Transform target, List<MotionTarget> targets)
    {
        RectTransform rect = target as RectTransform;
        if (rect == null) return;
        targets.Add(new MotionTarget(rect, rect.anchoredPosition));
    }

    private void CalculateStartOffsets(out Vector2 enemyOffset, out Vector2 playerOffset)
    {
        RectTransform panelRect = transform as RectTransform;
        float panelWidth = panelRect != null ? panelRect.rect.width : Screen.width;
        float panelHeight = panelRect != null ? panelRect.rect.height : Screen.height;
        panelWidth = Mathf.Max(panelWidth, Screen.width, 1f);
        panelHeight = Mathf.Max(panelHeight, Screen.height, 1f);

        if (entranceMode == LoadingPanelEntranceMode.Horizontal)
        {
            float distance = panelWidth + Mathf.Max(
                GetLargestExtent(enemyTargets, false),
                GetLargestExtent(playerTargets, false));
            enemyOffset = Vector2.left * distance;
            playerOffset = Vector2.right * distance;
            return;
        }

        float verticalDistance = panelHeight + Mathf.Max(
            GetLargestExtent(enemyTargets, true),
            GetLargestExtent(playerTargets, true));
        enemyOffset = Vector2.up * verticalDistance;
        playerOffset = Vector2.down * verticalDistance;
    }

    private static float GetLargestExtent(List<MotionTarget> targets, bool vertical)
    {
        float result = 0f;
        for (int i = 0; i < targets.Count; i++)
        {
            Rect rect = targets[i].Rect.rect;
            result = Mathf.Max(result, vertical ? rect.height : rect.width);
        }
        return result;
    }

    private static void ApplyProgress(
        List<MotionTarget> targets,
        Vector2 startOffset,
        float progress)
    {
        Vector2 remainingOffset = Vector2.LerpUnclamped(startOffset, Vector2.zero, progress);
        for (int i = 0; i < targets.Count; i++)
        {
            targets[i].Rect.anchoredPosition = targets[i].FinalPosition + remainingOffset;
        }
    }

    private void RestoreFinalPositions()
    {
        if (!positionsCaptured) return;
        RestoreTargets(enemyTargets);
        RestoreTargets(playerTargets);
    }

    private static void RestoreTargets(List<MotionTarget> targets)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i].Rect != null)
            {
                targets[i].Rect.anchoredPosition = targets[i].FinalPosition;
            }
        }
    }

    private static float EaseOutCubic(float value)
    {
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }

    private sealed class MotionTarget
    {
        public MotionTarget(RectTransform rect, Vector2 finalPosition)
        {
            Rect = rect;
            FinalPosition = finalPosition;
        }

        public RectTransform Rect { get; }
        public Vector2 FinalPosition { get; }
    }
}
