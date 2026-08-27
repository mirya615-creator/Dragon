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

            Assert.IsNull(screen.transform.Find("Versus"));
            AssertLegacyVisualIsAbsentOrInactive(screen.PlayerBattlefieldView.transform, "ART_Background");
            AssertLegacyVisualIsAbsentOrInactive(screen.AiBattlefieldView.transform, "ART_Background");

            var locked = screen.FixedBoardCanvas.GetDeploymentCell(new GridPosition(1, 0), TeamSide.Player);
            var artSlot = locked.GetComponent<FixedBoardArtSlot>();
            Assert.IsNotNull(artSlot);
            Assert.AreEqual("ART_Cell_Locked", artSlot.ArtSlotId);
            Assert.AreEqual(FixedBoardCellRole.Deployment, artSlot.Role);
            Assert.AreEqual(FixedBoardDeployState.LockedUnlockable, artSlot.DeployState);
        }

        [UnityTest]
        public IEnumerator PausePanelContinuesOrSettlesTheRunAsDefeat()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            Assert.IsNotNull(bootstrap);
            Assert.IsNotNull(screen);
            var background = screen.transform.Find("ART_ScreenBackground");
            var openButton = background.Find("ART_PauseButton").GetComponent<Button>();
            var panel = background.Find("PausePanel").gameObject;
            var finishButton = panel.transform.Find("Bg/PauseBtn").GetComponent<Button>();
            var continueButton = panel.transform.Find("Bg/ContinueBtn").GetComponent<Button>();

            openButton.onClick.Invoke();
            Assert.AreEqual(MatchState.Paused, bootstrap.Match.State);
            Assert.AreEqual(0f, Time.timeScale, 0.001f);
            Assert.IsTrue(panel.activeSelf);

            continueButton.onClick.Invoke();
            Assert.AreNotEqual(MatchState.Paused, bootstrap.Match.State);
            Assert.AreEqual(1f, Time.timeScale, 0.001f);
            Assert.IsFalse(panel.activeSelf);

            openButton.onClick.Invoke();
            finishButton.onClick.Invoke();
            Assert.AreEqual(MatchState.Defeat, bootstrap.Match.State);
            Assert.AreEqual(1f, Time.timeScale, 0.001f);
            Assert.IsFalse(panel.activeSelf);
            var settlement = screen.transform.Find("SettlementPanel");
            Assert.IsTrue(settlement.gameObject.activeSelf);
            Assert.AreEqual("Defalt", ReadAuthoredText(settlement.Find("Text")));
        }

        [UnityTest]
        public IEnumerator AiDefeatShowsVictorySettlementPanel()
        {
            SceneManager.LoadScene("Greybox_Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindObjectOfType<DragonBoundBootstrap>();
            var screen = Object.FindObjectOfType<DragonBoundScreenView>();
            Assert.IsNotNull(bootstrap);
            Assert.IsNotNull(screen);

            Assert.IsTrue(bootstrap.Match.TryTransition(MatchState.Victory));
            var settlement = screen.transform.Find("SettlementPanel");
            Assert.IsTrue(settlement.gameObject.activeSelf);
            Assert.AreEqual("Victory", ReadAuthoredText(settlement.Find("Text")));
        }

        private static string ReadAuthoredText(Transform target)
        {
            var component = target.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(component);
            return (string)component.GetType().GetProperty("text").GetValue(component);
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

        private static void AssertLegacyVisualIsAbsentOrInactive(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            Assert.IsTrue(child == null || !child.gameObject.activeSelf);
        }

    }
}
