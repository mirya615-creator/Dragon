using System.Collections;
using DragonBound.Bootstrap;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DragonBound.Tests.PlayMode
{
    public sealed class FixedBoardCanvasPlayModeTests
    {
        [UnityTest]
        public IEnumerator PlayerEnemyTraversesAllPathNodesInOrder()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            Assert.IsNotNull(bootstrap);
            AssertEnemyTraversesOrderedPath(
                bootstrap.FixedBoardLayout.GetLane(TeamSide.Player),
                TeamSide.Player);
        }

        [UnityTest]
        public IEnumerator AiEnemyTraversesAllPathNodesInOrder()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            Assert.IsNotNull(bootstrap);
            AssertEnemyTraversesOrderedPath(
                bootstrap.FixedBoardLayout.GetLane(TeamSide.AI),
                TeamSide.AI);
        }

        [UnityTest]
        public IEnumerator FixedReferenceBoardRendersOneContinuousCanvasAndBindsBothSides()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            Assert.IsNotNull(bootstrap);
            Assert.IsNotNull(screen);
            Assert.AreEqual(BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01Id, bootstrap.BattlefieldLayout.LayoutId);
            Assert.IsNotNull(screen.FixedBoardCanvas);
            Assert.AreEqual(48, screen.FixedBoardCanvas.CellViewCount);
            Assert.AreEqual(80, screen.FixedBoardCanvas.SemanticTileCount);
            Assert.AreEqual(screen.FixedBoardCanvas.CellSize.x, screen.FixedBoardCanvas.CellSize.y, 0.1f);
            Assert.IsFalse(screen.FixedBoardCanvas.TryGetCellView(new GridPosition(0, 0), out _));
            Assert.IsTrue(screen.FixedBoardCanvas.TryGetArtSlot(new GridPosition(0, 0), out var spawnSlot));
            Assert.AreEqual(FixedBoardCellRole.Spawn, spawnSlot.Role);
            Assert.AreEqual("ART_PlayerSpawnGate", spawnSlot.ArtSlotId);

            var playerPosition = new GridPosition(2, 2);
            var aiPosition = bootstrap.FixedBoardLayout.GetFairCounterpart(playerPosition, TeamSide.Player);
            Assert.AreSame(
                screen.FixedBoardCanvas.GetDeploymentCell(playerPosition, TeamSide.Player),
                bootstrap.BoardView.GetCellView(playerPosition));
            Assert.AreSame(
                screen.FixedBoardCanvas.GetDeploymentCell(aiPosition, TeamSide.AI),
                bootstrap.AiBoardView.GetCellView(aiPosition));
            Assert.AreEqual(
                bootstrap.FixedBoardLayout.PlayerLaneWaypoints.Count - 2,
                screen.FixedBoardCanvas.LaneArtCount(TeamSide.Player));
            Assert.AreEqual(
                bootstrap.FixedBoardLayout.AiLaneWaypoints.Count - 2,
                screen.FixedBoardCanvas.LaneArtCount(TeamSide.AI));

            Assert.Greater(
                screen.FixedBoardCanvas.UnitLayer.GetSiblingIndex(),
                screen.FixedBoardCanvas.LaneLayer.GetSiblingIndex());
            Assert.Greater(
                screen.FixedBoardCanvas.CombatFxLayer.GetSiblingIndex(),
                screen.FixedBoardCanvas.UnitLayer.GetSiblingIndex());
            Assert.AreSame(screen.FixedBoardCanvas.UnitLayer, bootstrap.BoardView.UnitLayer.parent);
            Assert.AreSame(screen.FixedBoardCanvas.UnitLayer, bootstrap.AiBoardView.UnitLayer.parent);
            Assert.AreSame(
                screen.FixedBoardCanvas.CombatFxLayer,
                bootstrap.BoardView.RangePreview.rectTransform.parent);
            Assert.AreSame(
                screen.FixedBoardCanvas.CombatFxLayer,
                bootstrap.AiBoardView.RangePreview.rectTransform.parent);
            Assert.IsNull(bootstrap.BoardView.RangePreview.GetComponentInParent<RectMask2D>());
            Assert.IsNull(bootstrap.AiBoardView.RangePreview.GetComponentInParent<RectMask2D>());

            Assert.Greater(
                screen.FixedBoardCanvas.OverlayLayer.GetSiblingIndex(),
                bootstrap.BoardView.UnitLayer.GetSiblingIndex());
            var arrows = screen.FixedBoardCanvas.OverlayLayer
                .GetComponentsInChildren<DragArrowPreviewView>(true);
            Assert.GreaterOrEqual(arrows.Length, 2);
            foreach (var arrow in arrows)
            {
                Assert.AreSame(screen.FixedBoardCanvas.OverlayLayer, arrow.transform.parent);
                foreach (var graphic in arrow.GetComponentsInChildren<Graphic>(true))
                {
                    Assert.IsFalse(graphic.raycastTarget);
                }
            }

            Assert.IsFalse(screen.transform.Find("Versus").gameObject.activeSelf);
            Assert.IsFalse(screen.PlayerBattlefieldView.transform.Find("ART_Background").gameObject.activeSelf);
            Assert.IsFalse(screen.AiBattlefieldView.transform.Find("ART_Background").gameObject.activeSelf);

            var locked = screen.FixedBoardCanvas.GetDeploymentCell(new GridPosition(1, 0), TeamSide.Player);
            var artSlot = locked.GetComponent<FixedBoardArtSlot>();
            Assert.IsNotNull(artSlot);
            Assert.AreEqual("ART_Cell_Locked", artSlot.ArtSlotId);
            Assert.AreEqual(FixedBoardCellRole.Deployment, artSlot.Role);
            Assert.AreEqual(FixedBoardDeployState.LockedUnlockable, artSlot.DeployState);
        }

        private static void AssertEnemyTraversesOrderedPath(
            BattlefieldLaneDefinition lane,
            TeamSide side)
        {
            var path = new EnemyPath(lane.NodeNames, lane.CombatPoints);
            var enemy = new EnemyRuntime($"{side}.route", side);
            path.PlaceAtSpawn(enemy);
            Assert.IsTrue(enemy.CombatPosition.Equals(lane.CombatPoints[0]));

            var travelSeconds = lane.NodeNames.Count - 1f;
            for (var index = 1; index < lane.NodeNames.Count; index++)
            {
                var reachedGoal = path.Advance(enemy, 1f, travelSeconds);
                Assert.AreEqual(index, enemy.PathIndex, $"Node {index} was skipped.");
                Assert.AreEqual(index / travelSeconds, enemy.PathProgress, 0.0001f);
                Assert.IsTrue(enemy.CombatPosition.Equals(lane.CombatPoints[index]));
                Assert.AreEqual(index == lane.NodeNames.Count - 1, reachedGoal);
            }
        }

    }
}
