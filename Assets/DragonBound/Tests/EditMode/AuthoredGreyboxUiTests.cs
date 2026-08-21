using System.Collections.Generic;
using DragonBound.Grid;
using DragonBound.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DragonBound.Tests.EditMode
{
    public sealed class AuthoredGreyboxUiTests
    {
        private const string ScreenPath =
            "Assets/DragonBound/UI/Prefabs/Screens/DragonBoundPortraitScreen.prefab";

        [Test]
        public void FixedBoardAndDevelopmentUiAreAuthoredPrefabContent()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath);
            Assert.IsNotNull(prefab);
            var screen = prefab.GetComponent<DragonBoundScreenView>();
            Assert.IsNotNull(screen);
            Assert.IsNotNull(screen.FixedBoardCanvas);
            Assert.IsTrue(screen.FixedBoardCanvas.IsAuthoredLayout);
            Assert.AreEqual("ART_FixedBoardCanvas", screen.FixedBoardCanvas.name);
            Assert.AreEqual(80, screen.FixedBoardCanvas.GetComponentsInChildren<GridCellView>(true).Length);
            Assert.IsNotNull(prefab.transform.Find("RangeDismissSurface"));
            Assert.IsNotNull(prefab.transform.Find("ItemEntryButton"));
            Assert.IsNotNull(prefab.transform.Find("ART_ItemLoadout"));
            Assert.IsFalse(prefab.transform.Find("Versus").gameObject.activeSelf);
        }

        [Test]
        public void RuntimeBindingDoesNotMoveOrResizeAuthoredFixedUi()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath);
            var instance = Object.Instantiate(prefab);
            try
            {
                var screen = instance.GetComponent<DragonBoundScreenView>();
                var canvas = screen.FixedBoardCanvas;
                var rects = canvas.GetComponentsInChildren<RectTransform>(true);
                var snapshots = new Dictionary<RectTransform, Snapshot>();
                foreach (var rect in rects) snapshots.Add(rect, new Snapshot(rect));

                canvas.BindAuthored(
                    (RectTransform)screen.transform,
                    BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01);

                Assert.AreEqual(80, canvas.SemanticTileCount);
                Assert.AreEqual(48, canvas.CellViewCount);
                foreach (var pair in snapshots) pair.Value.AssertUnchanged(pair.Key);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private readonly struct Snapshot
        {
            private readonly Vector2 anchorMin;
            private readonly Vector2 anchorMax;
            private readonly Vector2 anchoredPosition;
            private readonly Vector2 sizeDelta;
            private readonly Vector3 scale;
            private readonly int siblingIndex;

            public Snapshot(RectTransform rect)
            {
                anchorMin = rect.anchorMin;
                anchorMax = rect.anchorMax;
                anchoredPosition = rect.anchoredPosition;
                sizeDelta = rect.sizeDelta;
                scale = rect.localScale;
                siblingIndex = rect.GetSiblingIndex();
            }

            public void AssertUnchanged(RectTransform rect)
            {
                Assert.AreEqual(anchorMin, rect.anchorMin, rect.name);
                Assert.AreEqual(anchorMax, rect.anchorMax, rect.name);
                Assert.AreEqual(anchoredPosition, rect.anchoredPosition, rect.name);
                Assert.AreEqual(sizeDelta, rect.sizeDelta, rect.name);
                Assert.AreEqual(scale, rect.localScale, rect.name);
                Assert.AreEqual(siblingIndex, rect.GetSiblingIndex(), rect.name);
            }
        }
    }
}
