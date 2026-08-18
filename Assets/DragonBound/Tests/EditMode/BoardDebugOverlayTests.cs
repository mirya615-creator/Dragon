using DragonBound.Grid;
using DragonBound.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Tests.EditMode
{
    public sealed class BoardDebugOverlayTests
    {
        [Test]
        public void DebugOverlayDefaultsDisabledAndNeverBlocksRaycasts()
        {
            var screen = CreateRect("Screen", null, new Vector2(1080f, 1920f));
            var template = CreateRect("ART_CellTemplate", screen.transform, new Vector2(40f, 40f))
                .gameObject.AddComponent<GridCellView>();
            try
            {
                var canvas = FixedBoardCanvasView.Create(
                    screen,
                    BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01,
                    template);
                var overlay = canvas.DebugOverlay;

                Assert.IsNotNull(overlay);
                Assert.IsFalse(overlay.IsVisible);
                Assert.IsFalse(overlay.BlocksRaycasts);
                Assert.AreEqual(80, overlay.CellVisualCount);
                foreach (var graphic in overlay.GetComponentsInChildren<Graphic>(true))
                {
                    Assert.IsFalse(graphic.raycastTarget);
                }
            }
            finally
            {
                Object.DestroyImmediate(screen.gameObject);
            }
        }

        [Test]
        public void DebugOverlayRendersRoleCoordinatesOwnerAndOrderedPathFromLayout()
        {
            var screen = CreateRect("Screen", null, new Vector2(1080f, 1920f));
            var template = CreateRect("ART_CellTemplate", screen.transform, new Vector2(40f, 40f))
                .gameObject.AddComponent<GridCellView>();
            try
            {
                var canvas = FixedBoardCanvasView.Create(
                    screen,
                    BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01,
                    template);
                var overlay = canvas.DebugOverlay;
                overlay.SetVisible(true);
                overlay.SetOptions(
                    BoardDebugOverlayOptions.ShowCellRoles |
                    BoardDebugOverlayOptions.ShowCoordinates |
                    BoardDebugOverlayOptions.ShowOwner |
                    BoardDebugOverlayOptions.ShowPathOrder |
                    BoardDebugOverlayOptions.ShowCellBounds |
                    BoardDebugOverlayOptions.ShowPathProgress |
                    BoardDebugOverlayOptions.ShowAttackRange);
                overlay.SetPathProgress(DragonBound.Core.TeamSide.Player, 0.25f);
                overlay.SetPathProgress(DragonBound.Core.TeamSide.AI, 0.75f);

                Assert.IsTrue(overlay.TryGetCellLabel(new GridPosition(0, 0), out var playerSpawn));
                StringAssert.Contains("S", playerSpawn.text);
                StringAssert.Contains("R9 C0", playerSpawn.text);
                StringAssert.Contains("X0 Y0", playerSpawn.text);
                StringAssert.Contains("PLAYER", playerSpawn.text);
                StringAssert.Contains("P00", playerSpawn.text);
                Assert.IsTrue(overlay.TryGetCellBounds(new GridPosition(0, 0), out var bounds));
                Assert.IsTrue(bounds.gameObject.activeSelf);
                Assert.IsTrue(overlay.HasOption(BoardDebugOverlayOptions.ShowAttackRange));
            }
            finally
            {
                Object.DestroyImmediate(screen.gameObject);
            }
        }

        [Test]
        public void ArtContractProvidesIndependentMapAndCellReplacementAnchors()
        {
            var screen = CreateRect("Screen", null, new Vector2(1080f, 1920f));
            var template = CreateRect("ART_CellTemplate", screen.transform, new Vector2(40f, 40f))
                .gameObject.AddComponent<GridCellView>();
            try
            {
                var layout = BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01;
                var canvas = FixedBoardCanvasView.Create(screen, layout, template);

                foreach (var mapSlotId in FixedBoardArtContract.MapSlots)
                {
                    Assert.IsTrue(canvas.TryGetMapArtSlot(mapSlotId, out var mapSlot), mapSlotId);
                    Assert.AreEqual(mapSlotId, mapSlot.ArtSlotId);
                    foreach (var graphic in mapSlot.GetComponentsInChildren<Graphic>(true))
                    {
                        Assert.IsFalse(graphic.raycastTarget);
                    }
                }

                foreach (var definition in layout.CellDefinitions)
                {
                    Assert.IsTrue(canvas.TryGetArtSlot(definition.Coordinate, out var slot));
                    CollectionAssert.Contains(FixedBoardArtContract.CellSlots, slot.SurfaceArtSlotId);
                    Assert.IsNotNull(slot.transform.Find(FixedBoardArtContract.CellBorder));
                    if (definition.Role == FixedBoardCellRole.Deployment &&
                        definition.DeployState == FixedBoardDeployState.LockedUnlockable)
                    {
                        Assert.IsTrue(slot.HasLockMarker);
                        Assert.IsNotNull(slot.transform.Find(FixedBoardArtContract.LockMarker));
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(screen.gameObject);
            }
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return rect;
        }
    }
}
