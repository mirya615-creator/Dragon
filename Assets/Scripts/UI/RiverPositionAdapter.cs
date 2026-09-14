using System.Collections;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public sealed class RiverPositionAdapter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform topAnchor;
    [SerializeField] private RectTransform bottomAnchor;

    [Header("Adjustment")]
    [SerializeField] private float yOffset = 5f;

    private RectTransform river;
    private RectTransform layoutRoot;
    private Vector2Int lastScreenSize = new Vector2Int(-1, -1);
    private Rect lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
    private Vector2 lastRootSize = new Vector2(-1f, -1f);
    private float lastTopBattlefieldEdge = float.NaN;
    private float lastBottomBattlefieldEdge = float.NaN;
    private readonly Vector3[] worldCorners = new Vector3[4];
    private bool layoutDirty = true;
    private bool applying;

    private void OnEnable()
    {
        ResolveReferences();
        layoutDirty = true;

        if (Application.isPlaying)
        {
            StartCoroutine(RefreshAfterLayout());
        }
        else
        {
            Refresh();
        }
    }

    private IEnumerator RefreshAfterLayout()
    {
        // Safe area and CanvasScaler can settle after the first rendered layout pass.
        yield return null;
        Canvas.ForceUpdateCanvases();
        Refresh();
        yield return null;
        Canvas.ForceUpdateCanvases();
        Refresh();
    }

    private void LateUpdate()
    {
        ResolveReferences();
        if (river == null || layoutRoot == null)
        {
            return;
        }

        var screenSize = new Vector2Int(Screen.width, Screen.height);
        var safeArea = Screen.safeArea;
        var rootSize = layoutRoot.rect.size;
        var hasBattlefieldEdges = TryGetBattlefieldEdges(out var topBattlefieldEdge, out var bottomBattlefieldEdge);
        if (screenSize != lastScreenSize ||
            safeArea != lastSafeArea ||
            rootSize != lastRootSize ||
            (hasBattlefieldEdges &&
             (!Mathf.Approximately(topBattlefieldEdge, lastTopBattlefieldEdge) ||
              !Mathf.Approximately(bottomBattlefieldEdge, lastBottomBattlefieldEdge))))
        {
            layoutDirty = true;
        }

        if (layoutDirty)
        {
            Refresh();
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!applying)
        {
            layoutDirty = true;
        }
    }

    public void Refresh()
    {
        ResolveReferences();
        if (applying ||
            river == null ||
            layoutRoot == null ||
            topAnchor == null ||
            bottomAnchor == null)
        {
            return;
        }

        applying = true;
        try
        {
            if (!TryGetBattlefieldEdges(out var topBattlefieldEdge, out var bottomBattlefieldEdge))
            {
                return;
            }

            var targetY = ((topBattlefieldEdge + bottomBattlefieldEdge) * 0.5f) + yOffset;

            var localPosition = river.localPosition;
            localPosition.y = targetY;
            river.localPosition = localPosition;

            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            lastSafeArea = Screen.safeArea;
            lastRootSize = layoutRoot.rect.size;
            lastTopBattlefieldEdge = topBattlefieldEdge;
            lastBottomBattlefieldEdge = bottomBattlefieldEdge;
            layoutDirty = false;
        }
        finally
        {
            applying = false;
        }
    }

    private bool TryGetBattlefieldEdges(out float topBattlefieldBottom, out float bottomBattlefieldTop)
    {
        topBattlefieldBottom = 0f;
        bottomBattlefieldTop = 0f;
        if (layoutRoot == null || topAnchor == null || bottomAnchor == null)
        {
            return false;
        }

        // The serialized anchors remain as stable scene references, but their 100x100 rects
        // are presentation helpers and their pivots are not the actual map seams. Their
        // parents are the authored AI/player battlefield rectangles, whose nearest edges are
        // the only reliable river boundaries across aspect ratios and safe-area changes.
        var topBattlefield = topAnchor.parent as RectTransform;
        var bottomBattlefield = bottomAnchor.parent as RectTransform;
        if (topBattlefield == null || bottomBattlefield == null)
        {
            return false;
        }

        topBattlefield.GetWorldCorners(worldCorners);
        topBattlefieldBottom = float.PositiveInfinity;
        for (var index = 0; index < worldCorners.Length; index++)
        {
            topBattlefieldBottom = Mathf.Min(
                topBattlefieldBottom,
                layoutRoot.InverseTransformPoint(worldCorners[index]).y);
        }

        bottomBattlefield.GetWorldCorners(worldCorners);
        bottomBattlefieldTop = float.NegativeInfinity;
        for (var index = 0; index < worldCorners.Length; index++)
        {
            bottomBattlefieldTop = Mathf.Max(
                bottomBattlefieldTop,
                layoutRoot.InverseTransformPoint(worldCorners[index]).y);
        }

        return !float.IsInfinity(topBattlefieldBottom) &&
               !float.IsInfinity(bottomBattlefieldTop);
    }

    private void ResolveReferences()
    {
        if (river == null)
        {
            river = GetComponent<RectTransform>();
        }

        if (layoutRoot == null && river != null)
        {
            layoutRoot = river.parent as RectTransform;
        }
    }
}
