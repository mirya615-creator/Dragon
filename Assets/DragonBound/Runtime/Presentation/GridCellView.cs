using System;
using DragonBound.Grid;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    public sealed class GridCellView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private int gridX;
        [SerializeField] private int gridY;
        [SerializeField] private CellType cellType;
        [SerializeField] private Image artImage;
        [SerializeField] private RectTransform contentAnchor;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private Text debugRangeBandLabel;
        [Header("Development state art")]
        [SerializeField] private Image developmentStateImage;
        [SerializeField] private Sprite lockedSprite;
        [SerializeField] private Sprite unlockedSprite;

        private bool usesFixedBoardVisual;
        private FixedBoardCellRole fixedRole;
        private FixedBoardArtSlot fixedArtSlot;
        private static Sprite cachedLockedSprite;
        private static Sprite cachedUnlockedSprite;

        public GridPosition Position => new GridPosition(gridX, gridY);
        public CellType CellType => cellType;
        public RectTransform RectTransform => (RectTransform)transform;
        public RectTransform ContentAnchor => contentAnchor != null ? contentAnchor : RectTransform;
        public Image ArtImage => artImage;
        public event Action<GridPosition> Clicked;

        public void Configure(
            int x,
            int y,
            CellType type,
            Image art,
            RectTransform anchor)
        {
            gridX = x;
            gridY = y;
            cellType = type;
            artImage = art;
            contentAnchor = anchor;
            EnsureInputReceiver();
            if (lockOverlay == null)
            {
                var overlay = transform.Find("ART_LockOverlay");
                lockOverlay = overlay != null ? overlay.gameObject : null;
            }

            if (debugRangeBandLabel == null)
            {
                var label = transform.Find("DebugRangeBandLabel");
                debugRangeBandLabel = label != null ? label.GetComponent<Text>() : null;
            }

            ApplyRuntimeState(type, null, false);
        }

        public void ApplyFixedBoardDefinition(FixedBoardCellDefinition definition)
        {
            usesFixedBoardVisual = true;
            fixedRole = definition.Role;
            if (fixedArtSlot == null)
            {
                fixedArtSlot = GetComponent<FixedBoardArtSlot>();
                if (fixedArtSlot == null)
                {
                    fixedArtSlot = gameObject.AddComponent<FixedBoardArtSlot>();
                }
            }

            fixedArtSlot.Bind(definition);
            gridX = definition.Coordinate.X;
            gridY = definition.Coordinate.Y;
            EnsureInputReceiver();
            ConfigureFixedArt(definition);
            ApplyDevelopmentVisual(cellType);
        }

        /// <summary>Restores gameplay semantics for a serialized cell without restyling authored UI.</summary>
        public void BindAuthoredFixedBoardDefinition(FixedBoardCellDefinition definition)
        {
            usesFixedBoardVisual = true;
            fixedRole = definition.Role;
            fixedArtSlot = GetComponent<FixedBoardArtSlot>();
            gridX = definition.Coordinate.X;
            gridY = definition.Coordinate.Y;
            EnsureInputReceiver();
            if (lockOverlay == null)
            {
                var overlay = transform.Find("ART_LockOverlay");
                lockOverlay = overlay != null ? overlay.gameObject : null;
            }
            if (debugRangeBandLabel == null)
            {
                var label = transform.Find("DebugRangeBandLabel");
                debugRangeBandLabel = label != null ? label.GetComponent<Text>() : null;
            }

            ApplyDevelopmentVisual(cellType);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Left)
            {
                Clicked?.Invoke(Position);
            }
        }

        /// <summary>
        /// Adds stable, non-interactive art anchors to a cloned fixed-board tile. These anchors
        /// are presentation only; CellRoot, ContentAnchor, and the input receiver are untouched.
        /// </summary>
        public void ApplyFixedBoardArtContract(string surfaceArtSlotId)
        {
            if (!usesFixedBoardVisual || string.IsNullOrWhiteSpace(surfaceArtSlotId))
            {
                return;
            }

            var surface = artImage != null ? artImage.transform : transform.Find("ART_CellSurface");
            if (surface != null)
            {
                surface.name = surfaceArtSlotId;
            }

            EnsureBorder();
            var locked = fixedRole == FixedBoardCellRole.Deployment && cellType == CellType.Locked;
            var usesDevelopmentStateArt = ApplyDevelopmentVisual(cellType);
            // Keep the authored handoff anchor for compatibility, but the full-cell Lock/UnLock
            // artwork is now the visible state indicator.
            EnsureLockMarker(locked);
            if (usesDevelopmentStateArt)
            {
                var marker = transform.Find(FixedBoardArtContract.LockMarker);
                if (marker != null)
                {
                    marker.gameObject.SetActive(false);
                }
            }

            fixedArtSlot?.BindPresentationContract(surfaceArtSlotId, true, locked);
        }

        public void ApplyRuntimeState(
            CellType type,
            BattlefieldRangeBand? rangeBand,
            bool showRangeBand)
        {
            cellType = type;
            if (lockOverlay == null)
            {
                var overlay = transform.Find("ART_LockOverlay");
                lockOverlay = overlay != null ? overlay.gameObject : null;
            }

            if (lockOverlay != null)
            {
                lockOverlay.SetActive(type == CellType.Locked && !usesFixedBoardVisual);
            }

            var usesDevelopmentStateArt = ApplyDevelopmentVisual(type);
            if (usesFixedBoardVisual && artImage != null && fixedRole == FixedBoardCellRole.Deployment)
            {
                var marker = transform.Find(FixedBoardArtContract.LockMarker);
                if (marker != null)
                {
                    marker.gameObject.SetActive(type == CellType.Locked && !usesDevelopmentStateArt);
                }
            }

            if (debugRangeBandLabel != null)
            {
                debugRangeBandLabel.gameObject.SetActive(showRangeBand && rangeBand.HasValue && type != CellType.Bench);
                debugRangeBandLabel.text = rangeBand.HasValue
                    ? rangeBand.Value == BattlefieldRangeBand.Near
                        ? "N"
                        : rangeBand.Value == BattlefieldRangeBand.Middle ? "M" : "F"
                    : string.Empty;
            }
        }

        private void ConfigureFixedArt(FixedBoardCellDefinition definition)
        {
            if (artImage != null)
            {
                artImage.raycastTarget = false;
                artImage.color = GetFixedColor(definition);
            }

            if (lockOverlay != null)
            {
                // The legacy text overlay is deliberately disabled for fixed maps. The compact
                // ART_LockMarker is created by ApplyFixedBoardArtContract instead.
                var legacyLabel = lockOverlay.GetComponentInChildren<Text>(true);
                if (legacyLabel != null)
                {
                    legacyLabel.text = string.Empty;
                    legacyLabel.gameObject.SetActive(false);
                }

                lockOverlay.SetActive(false);
            }

            if (debugRangeBandLabel != null)
            {
                debugRangeBandLabel.gameObject.SetActive(false);
            }
        }

        private static Color GetFixedColor(FixedBoardCellDefinition definition)
        {
            switch (definition.Role)
            {
                case FixedBoardCellRole.Deployment:
                    return definition.DeployState == FixedBoardDeployState.Unlocked
                        ? definition.Owner == FixedBoardCellOwner.Player
                            ? new Color(0.79f, 0.82f, 0.78f, 1f)
                            : new Color(0.76f, 0.72f, 0.79f, 1f)
                        : new Color(0.26f, 0.23f, 0.29f, 1f);
                case FixedBoardCellRole.Lane:
                    return new Color(0.38f, 0.25f, 0.20f, 1f);
                case FixedBoardCellRole.Spawn:
                    return new Color(0.55f, 0.32f, 0.28f, 1f);
                case FixedBoardCellRole.Goal:
                    return new Color(0.42f, 0.16f, 0.20f, 1f);
                case FixedBoardCellRole.Separator:
                    return new Color(0.24f, 0.16f, 0.20f, 1f);
                case FixedBoardCellRole.PermanentTerrain:
                    return definition.Owner == FixedBoardCellOwner.Player
                        ? new Color(0.42f, 0.29f, 0.34f, 1f)
                        : new Color(0.32f, 0.25f, 0.36f, 1f);
                default:
                    return new Color(0.20f, 0.16f, 0.20f, 1f);
            }
        }

        private bool ApplyDevelopmentVisual(CellType type)
        {
            if (!usesFixedBoardVisual || fixedRole != FixedBoardCellRole.Deployment)
            {
                return false;
            }

            ResolveDevelopmentStateArt();
            var target = developmentStateImage != null ? developmentStateImage : artImage;
            var sprite = type == CellType.Locked ? lockedSprite : unlockedSprite;
            if (target == null || sprite == null)
            {
                return false;
            }

            target.sprite = sprite;
            target.color = Color.white;
            target.enabled = true;
            target.raycastTarget = false;

            if (lockOverlay != null)
            {
                lockOverlay.SetActive(false);
            }

            var marker = transform.Find(FixedBoardArtContract.LockMarker);
            if (marker != null)
            {
                marker.gameObject.SetActive(false);
            }

            return true;
        }

        private void ResolveDevelopmentStateArt()
        {
            if (developmentStateImage == null)
            {
                developmentStateImage = artImage;
            }

            if (lockedSprite == null)
            {
                if (cachedLockedSprite == null)
                {
                    cachedLockedSprite = Resources.Load<Sprite>("GameUI/Lock");
                }

                lockedSprite = cachedLockedSprite;
            }

            if (unlockedSprite == null)
            {
                if (cachedUnlockedSprite == null)
                {
                    cachedUnlockedSprite = Resources.Load<Sprite>("GameUI/UnLock");
                }

                unlockedSprite = cachedUnlockedSprite;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (artImage == null)
            {
                artImage = GetComponent<Image>();
            }

            fixedArtSlot = GetComponent<FixedBoardArtSlot>();
            if (fixedArtSlot == null || fixedArtSlot.Role != FixedBoardCellRole.Deployment)
            {
                return;
            }

            usesFixedBoardVisual = true;
            fixedRole = FixedBoardCellRole.Deployment;
            ApplyDevelopmentVisual(cellType);
        }
#endif

        private void EnsureBorder()
        {
            var existing = transform.Find(FixedBoardArtContract.CellBorder);
            if (existing != null)
            {
                return;
            }

            var border = new GameObject(
                FixedBoardArtContract.CellBorder,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));
            var rect = border.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(1f, 1f);
            rect.offsetMax = new Vector2(-1f, -1f);
            var image = border.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.01f);
            image.raycastTarget = false;
            var outline = border.GetComponent<Outline>();
            outline.effectColor = new Color(0.88f, 0.92f, 0.94f, 0.32f);
            outline.effectDistance = new Vector2(0.65f, 0.65f);
        }

        private void EnsureInputReceiver()
        {
            var receiver = transform.Find("InputReceiver");
            if (receiver != null)
            {
                return;
            }

            var receiverObject = new GameObject(
                "InputReceiver",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var rect = receiverObject.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = receiverObject.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            rect.SetAsFirstSibling();
        }

        private void EnsureLockMarker(bool visible)
        {
            var marker = transform.Find(FixedBoardArtContract.LockMarker);
            if (!visible)
            {
                if (marker != null)
                {
                    marker.gameObject.SetActive(false);
                }

                return;
            }

            if (marker == null)
            {
                var markerObject = new GameObject(
                    FixedBoardArtContract.LockMarker,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Outline));
                marker = markerObject.transform;
                marker.SetParent(transform, false);
                var markerRect = (RectTransform)marker;
                markerRect.anchorMin = new Vector2(0.37f, 0.33f);
                markerRect.anchorMax = new Vector2(0.63f, 0.57f);
                markerRect.offsetMin = Vector2.zero;
                markerRect.offsetMax = Vector2.zero;
                var image = markerObject.GetComponent<Image>();
                image.color = new Color(0.67f, 0.7f, 0.74f, 0.78f);
                image.raycastTarget = false;
                var outline = markerObject.GetComponent<Outline>();
                outline.effectColor = new Color(0.08f, 0.1f, 0.12f, 0.72f);
                outline.effectDistance = new Vector2(0.7f, 0.7f);

                var shackle = new GameObject(
                    "ART_LockMarker_Shackle",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Outline));
                var shackleRect = shackle.GetComponent<RectTransform>();
                shackleRect.SetParent(marker, false);
                shackleRect.anchorMin = new Vector2(0.2f, 0.72f);
                shackleRect.anchorMax = new Vector2(0.8f, 1.35f);
                shackleRect.offsetMin = Vector2.zero;
                shackleRect.offsetMax = Vector2.zero;
                var shackleImage = shackle.GetComponent<Image>();
                shackleImage.color = new Color(0.67f, 0.7f, 0.74f, 0.78f);
                shackleImage.raycastTarget = false;
                var shackleOutline = shackle.GetComponent<Outline>();
                shackleOutline.effectColor = new Color(0.08f, 0.1f, 0.12f, 0.72f);
                shackleOutline.effectDistance = new Vector2(0.7f, 0.7f);
            }

            marker.gameObject.SetActive(true);
        }

        internal void Configure(int x, int y, CellType type, RectTransform contentAnchor)
        {
            throw new NotImplementedException();
        }
    }
}
