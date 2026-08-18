using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace DragonBound.Tests.EditMode
{
    public sealed class FixedBoardCanvasViewTests
    {
        [TestCase(720f, 1280f)]
        [TestCase(1080f, 1920f)]
        [TestCase(1080f, 2340f)]
        [TestCase(1170f, 2532f)]
        public void ReferenceMapBoardFitsPortraitScreenWithSquareCells(float width, float height)
        {
            var screen = CreateRect("Screen", null, new Vector2(width, height));
            var template = CreateRect("ART_CellTemplate", screen.transform, new Vector2(40f, 40f));
            var cellTemplate = template.gameObject.AddComponent<GridCellView>();
            try
            {
                var canvas = FixedBoardCanvasView.Create(
                    screen,
                    BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01,
                    cellTemplate);

                Assert.AreEqual(canvas.CellSize.x, canvas.CellSize.y, 0.001f);
                Assert.LessOrEqual(canvas.BoardRect.rect.width, screen.rect.width + 0.001f);
                Assert.LessOrEqual(canvas.BoardRect.rect.height, screen.rect.height + 0.001f);
                Assert.AreEqual(80, canvas.SemanticTileCount);
                Assert.AreEqual(48, canvas.CellViewCount);
            }
            finally
            {
                Object.DestroyImmediate(screen.gameObject);
            }
        }

        [Test]
        public void FixedCanvasBuildsFullSemanticMapAndOnlyDeploymentCellsAreInteractive()
        {
            var screen = CreateRect("Screen", null, new Vector2(1080f, 1920f));
            var template = CreateRect("ART_CellTemplate", screen.transform, new Vector2(40f, 40f));
            var cellTemplate = template.gameObject.AddComponent<GridCellView>();
            try
            {
                var layout = BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01;
                var canvas = FixedBoardCanvasView.Create(screen, layout, cellTemplate);

                Assert.AreEqual(48, canvas.CellViewCount);
                Assert.AreEqual(80, canvas.SemanticTileCount);
                Assert.IsNotNull(canvas.CenterDivider);
                Assert.AreEqual("ART_CenterDivider", canvas.CenterDivider.name);
                Assert.AreEqual(canvas.CellSize.x, canvas.CellSize.y, 0.001f);
                Assert.IsFalse(canvas.TryGetCellView(new GridPosition(0, 0), out _));
                Assert.IsTrue(canvas.TryGetVisualCell(new GridPosition(0, 0), out var spawn));
                Assert.IsNotNull(spawn.GetComponent<FixedBoardArtSlot>());
                Assert.IsTrue(canvas.TryGetArtSlot(new GridPosition(0, 0), out var spawnSlot));
                Assert.AreEqual(FixedBoardCellRole.Spawn, spawnSlot.Role);
                Assert.AreEqual("ART_PlayerSpawnGate", spawnSlot.ArtSlotId);
                Assert.IsTrue(canvas.TryGetArtSlot(new GridPosition(4, 3), out var laneSlot));
                Assert.AreEqual(FixedBoardCellRole.Lane, laneSlot.Role);
                Assert.AreEqual(
                    new GridPosition(2, 2),
                    canvas.GetDeploymentCell(new GridPosition(2, 2), TeamSide.Player).Position);
                Assert.AreEqual(
                    new GridPosition(3, 8),
                    canvas.GetDeploymentCell(new GridPosition(3, 8), TeamSide.AI).Position);
            }
            finally
            {
                Object.DestroyImmediate(screen.gameObject);
            }
        }

        [Test]
        public void FixedCanvasBindsEachLaneFromTheAuthoredRoadTemplate()
        {
            var screen = CreateRect("Screen", null, new Vector2(1080f, 1920f));
            var cellTemplate = CreateRect("ART_CellTemplate", screen.transform, new Vector2(40f, 40f))
                .gameObject.AddComponent<GridCellView>();
            var roadTemplate = CreateRect("ART_PathTemplate", screen.transform, new Vector2(20f, 20f));
            try
            {
                var layout = BattlefieldLayoutDefinitions.Fixed8x10HorizontalStart;
                var canvas = FixedBoardCanvasView.Create(screen, layout, cellTemplate);
                canvas.BindLaneArt(layout, TeamSide.Player, roadTemplate);
                canvas.BindLaneArt(layout, TeamSide.AI, roadTemplate);

                Assert.AreEqual(layout.PlayerLaneWaypoints.Count - 2, canvas.LaneArtCount(TeamSide.Player));
                Assert.AreEqual(layout.AiLaneWaypoints.Count - 2, canvas.LaneArtCount(TeamSide.AI));
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
