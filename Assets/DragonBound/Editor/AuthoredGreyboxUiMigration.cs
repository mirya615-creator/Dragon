using System;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Editor
{
    /// <summary>
    /// One-time conversion of the formal fixed-board preview from a Play-mode generated tree
    /// into editable prefab content. Once authored, this tool never rebuilds or overwrites it.
    /// </summary>
    [InitializeOnLoad] // Compilation in Edit mode schedules the one-time asset conversion.
    public static class AuthoredGreyboxUiMigration
    {
        private const string ScreenPrefabPath =
            "Assets/DragonBound/UI/Prefabs/Screens/DragonBoundPortraitScreen.prefab";
        private const string AuthoredBoardName = "ART_FixedBoardCanvas";

        static AuthoredGreyboxUiMigration()
        {
            EditorApplication.delayCall += TryAutomaticMigration;
        }

        [MenuItem("DragonBound/UI/Migrate Greybox To Authored Fixed Board")]
        public static void MigrateFromMenu()
        {
            MigrateIfRequired(true);
        }

        [MenuItem("DragonBound/UI/Reset Authored Board To Reference Bounds")]
        public static void ApplyReferenceBoundsFromMenu()
        {
            var root = PrefabUtility.LoadPrefabContents(ScreenPrefabPath);
            try
            {
                var screen = root.GetComponent<DragonBoundScreenView>();
                if (screen?.FixedBoardCanvas == null)
                {
                    throw new InvalidOperationException("The authored fixed board is missing.");
                }

                ApplyReferenceBounds(screen.FixedBoardCanvas);
                EditorUtility.SetDirty(root);
                PrefabUtility.SaveAsPrefabAsset(root, ScreenPrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void TryAutomaticMigration()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += TryAutomaticMigration;
                return;
            }

            MigrateIfRequired(false);
        }

        private static void MigrateIfRequired(bool logWhenCurrent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPrefabPath);
            if (prefab == null) return;
            var existing = prefab.transform.Find(AuthoredBoardName);
            if (existing != null && existing.GetComponent<FixedBoardCanvasView>()?.IsAuthoredLayout == true)
            {
                var existingCanvas = existing.GetComponent<FixedBoardCanvasView>();
                if (!existingCanvas.AuthoredRiverLayoutApplied)
                {
                    ApplyRiverLayoutToAuthoredPrefab();
                }
                if (logWhenCurrent) Debug.Log("Greybox fixed board is already authored; no assets were overwritten.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(ScreenPrefabPath);
            try
            {
                var screen = root.GetComponent<DragonBoundScreenView>();
                if (screen == null) throw new InvalidOperationException("DragonBoundScreenView is missing.");

                // The old screen is composed from nested module Prefabs. Their children cannot
                // be moved into the authored board until those nested instances become editable
                // content owned by this screen Prefab.
                UnpackNestedPrefabInstances(root);

                var template = FindCellTemplate(screen.PlayerBoardView);
                var layout = BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01;
                var canvas = FixedBoardCanvasView.Create((RectTransform)root.transform, layout, template);
                canvas.gameObject.name = AuthoredBoardName;

                // Run the old generation path once in the Editor, then persist its result.
                screen.AiBattlefieldView.ConfigureFixedBoardCanvas(canvas);
                screen.PlayerBattlefieldView.ConfigureFixedBoardCanvas(canvas);
                screen.AiBattlefieldView.LaneView.ConfigureLayout(layout, TeamSide.AI);
                screen.PlayerBattlefieldView.LaneView.ConfigureLayout(layout, TeamSide.Player);
                ApplyReferenceBounds(canvas);

                screen.OverlayController?.SetDebugOverlayVisible(false);

                var rangeDismiss = CreateRangeDismissSurface(root.transform);
                canvas.MarkAsAuthored();
                canvas.ApplyAuthoredRiverLayout(layout);
                screen.ConfigureAuthoredUi(canvas, rangeDismiss);

                EditorUtility.SetDirty(root);
                PrefabUtility.SaveAsPrefabAsset(root, ScreenPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log(
                    "Greybox UI migration complete: the fixed board and click surfaces are now editable prefab content.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GridCellView FindCellTemplate(GreyboxBoardView board)
        {
            if (board != null)
            {
                foreach (var candidate in board.CellViews)
                {
                    if (candidate != null && candidate.CellType != CellType.Bench) return candidate;
                }
            }

            throw new InvalidOperationException("The Player board has no authored GridCellView template.");
        }

        private static void ApplyReferenceBounds(FixedBoardCanvasView canvas)
        {
            const float arenaCenterY = 0.56f;
            var layout = BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01;
            canvas.ApplyAuthoredRiverLayout(layout);
            var size = new Vector2(
                canvas.VisualCellSize * layout.Columns,
                canvas.VisualCellSize * layout.Rows + canvas.CenterRiverGap);
            var center = new Vector2(0.5f, arenaCenterY);
            ApplyRect(canvas.BoardRect, center, size);
            ApplyRect(canvas.OverlayLayer, center, size);
        }

        private static void ApplyRiverLayoutToAuthoredPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(ScreenPrefabPath);
            try
            {
                var screen = root.GetComponent<DragonBoundScreenView>();
                var canvas = screen?.FixedBoardCanvas;
                if (canvas == null) return;
                canvas.ApplyAuthoredRiverLayout(BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01);
                EditorUtility.SetDirty(root);
                PrefabUtility.SaveAsPrefabAsset(root, ScreenPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Authored fixed board updated to the two-half river layout.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ApplyRect(RectTransform rect, Vector2 center, Vector2 size)
        {
            rect.anchorMin = center;
            rect.anchorMax = center;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private static void UnpackNestedPrefabInstances(GameObject root)
        {
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform == root.transform ||
                        !PrefabUtility.IsAnyPrefabInstanceRoot(transform.gameObject)) continue;

                    PrefabUtility.UnpackPrefabInstance(
                        transform.gameObject,
                        PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                    changed = true;
                    break;
                }
            }
        }

        private static BoardBackgroundClickReceiver CreateRangeDismissSurface(Transform parent)
        {
            var existing = parent.Find("RangeDismissSurface");
            if (existing != null)
            {
                return existing.GetComponent<BoardBackgroundClickReceiver>();
            }

            var surface = new GameObject(
                "RangeDismissSurface",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(BoardBackgroundClickReceiver));
            var rect = surface.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();
            var image = surface.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            return surface.GetComponent<BoardBackgroundClickReceiver>();
        }

    }
}
