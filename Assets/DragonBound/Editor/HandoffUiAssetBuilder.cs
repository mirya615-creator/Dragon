using System;
using System.IO;
using DragonBound.HandoffUi;
using DragonBound.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DragonBound.Editor
{
    /// <summary>Creates only the isolated handoff assets. It deliberately never touches existing UI or scenes.</summary>
    public static class HandoffUiAssetBuilder
    {
        public const string Root = "Assets/DragonBound/UI/Handoff";
        public const string OfferPrefabPath = Root + "/Prefabs/HandoffMerchantOffer.prefab";
        public const string ScreenPrefabPath = Root + "/Prefabs/UI_HandoffScreen.prefab";
        public const string ScenePath = "Assets/DragonBound/Scenes/UI_Handoff.unity";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private static DateTime tmpImportStartedAt;
        private static bool tmpImportPending;

        [MenuItem("DragonBound/Handoff/Create UI Handoff Assets")]
        public static void BuildIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPrefabPath) != null ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.Log("UI_Handoff assets already exist. They are intentionally not rebuilt by this command.");
                return;
            }

            EnsureFolders();
            var offer = BuildOfferPrefab();
            var screen = BuildScreenPrefab(offer);
            BuildScene(screen);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Created isolated UI_Handoff prefabs and preview scene.");
        }

        public static void BuildForAutomation()
        {
            EnsureTmpEssentialResources();
            BuildIfMissing();
        }

        public static void ImportTmpEssentialResourcesForAutomation()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath) != null)
            {
                Debug.Log("TextMeshPro Essential Resources are already available.");
                EditorApplication.Exit(0);
                return;
            }

            tmpImportStartedAt = DateTime.UtcNow;
            tmpImportPending = true;
            AssetDatabase.importPackageCompleted += OnTmpImportCompleted;
            AssetDatabase.importPackageFailed += OnTmpImportFailed;
            AssetDatabase.importPackageCancelled += OnTmpImportCancelled;
            EditorApplication.update += MonitorTmpImport;
            TMP_PackageResourceImporter.ImportResources(true, false, false);
        }

        private static void EnsureTmpEssentialResources()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath) != null)
            {
                return;
            }

            throw new InvalidOperationException(
                "TextMeshPro Essential Resources are missing. Run ImportTmpEssentialResourcesForAutomation first.");
        }

        private static void OnTmpImportCompleted(string packageName)
        {
            if (!tmpImportPending || packageName != "TMP Essential Resources") return;

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var imported = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath) != null;
            FinishTmpImport(imported ? 0 : 1,
                imported
                    ? "TextMeshPro Essential Resources import completed."
                    : "TextMeshPro import completed without creating TMP Settings.");
        }

        private static void OnTmpImportFailed(string packageName, string errorMessage)
        {
            if (!tmpImportPending || packageName != "TMP Essential Resources") return;
            FinishTmpImport(1, "TextMeshPro Essential Resources import failed: " + errorMessage);
        }

        private static void OnTmpImportCancelled(string packageName)
        {
            if (!tmpImportPending || packageName != "TMP Essential Resources") return;
            FinishTmpImport(1, "TextMeshPro Essential Resources import was cancelled.");
        }

        private static void MonitorTmpImport()
        {
            if (!tmpImportPending || (DateTime.UtcNow - tmpImportStartedAt).TotalMinutes < 3d) return;
            FinishTmpImport(1, "TextMeshPro Essential Resources import timed out after three minutes.");
        }

        private static void FinishTmpImport(int exitCode, string message)
        {
            tmpImportPending = false;
            AssetDatabase.importPackageCompleted -= OnTmpImportCompleted;
            AssetDatabase.importPackageFailed -= OnTmpImportFailed;
            AssetDatabase.importPackageCancelled -= OnTmpImportCancelled;
            EditorApplication.update -= MonitorTmpImport;
            if (exitCode == 0) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.Exit(exitCode);
        }

        private static void EnsureFolders()
        {
            CreateFolder("Assets/DragonBound/UI", "Handoff");
            CreateFolder(Root, "Prefabs");
        }

        private static void CreateFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        private static GameObject BuildOfferPrefab()
        {
            var root = Panel("MerchantOffer", new Color(0.16f, 0.18f, 0.19f, 1f));
            SetSize(root.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            var layout = root.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 20, 20);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            root.AddComponent<LayoutElement>().preferredHeight = 250f;
            var title = Text("Title", root.transform, 32, TextAlignmentOptions.Left, new Color(0.93f, 0.94f, 0.92f));
            var detail = Text("Detail", root.transform, 22, TextAlignmentOptions.Left, new Color(0.73f, 0.77f, 0.76f));
            var state = Text("State", root.transform, 21, TextAlignmentOptions.Left, new Color(0.89f, 0.68f, 0.38f));
            var button = Button("SelectButton", root.transform, "SELECT");
            var icon = ArtImage("ItemIcon", root.transform, 72f);
            var view = root.AddComponent<HandoffMerchantOfferView>();
            Set(view, "titleLabel", title); Set(view, "detailLabel", detail); Set(view, "stateLabel", state); Set(view, "selectButton", button); Set(view, "itemImage", icon); Set(view, "cardImage", root.GetComponent<Image>());
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, OfferPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildScreenPrefab(GameObject offerPrefab)
        {
            var root = new GameObject("UI_HandoffScreen", typeof(RectTransform));
            var rootRect = root.GetComponent<RectTransform>(); SetStretch(rootRect);
            var safe = new GameObject("SafeAreaRoot", typeof(RectTransform), typeof(SafeAreaFitter)); safe.transform.SetParent(root.transform, false); SetStretch(safe.GetComponent<RectTransform>());
            var responsive = new GameObject("ResponsiveContainer", typeof(RectTransform), typeof(HandoffResponsiveLayout)); responsive.transform.SetParent(safe.transform, false); SetStretch(responsive.GetComponent<RectTransform>());
            var backdrop = Panel("Backdrop", new Color(0.075f, 0.09f, 0.095f, 1f)); backdrop.transform.SetParent(responsive.transform, false); SetStretch(backdrop.GetComponent<RectTransform>());
            var content = new GameObject("Content", typeof(RectTransform)); content.transform.SetParent(responsive.transform, false); SetStretch(content.GetComponent<RectTransform>());
            var contentLayout = content.AddComponent<VerticalLayoutGroup>(); contentLayout.padding = new RectOffset(54, 54, 74, 74); contentLayout.spacing = 26f; contentLayout.childControlWidth = true; contentLayout.childForceExpandWidth = true; contentLayout.childControlHeight = true; contentLayout.childForceExpandHeight = false;
            var header = Text("Header", content.transform, 42, TextAlignmentOptions.Center, new Color(0.95f, 0.96f, 0.93f)); header.text = "DRAKEFORGE | UI HANDOFF"; header.gameObject.AddComponent<LayoutElement>().preferredHeight = 74f;
            var item = BuildItemHud(content.transform);
            var merchant = BuildMerchant(content.transform, offerPrefab.GetComponent<HandoffMerchantOfferView>());
            var presenter = root.AddComponent<HandoffPreviewPresenter>(); Set(presenter, "itemHudView", item); Set(presenter, "merchantView", merchant);
            var responsiveLayout = responsive.GetComponent<HandoffResponsiveLayout>(); Set(responsiveLayout, "fixedFormatContent", content.GetComponent<RectTransform>()); SetVector2(responsiveLayout, "fixedFormatAspect", new Vector2(9f, 16f));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ScreenPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static HandoffItemHudView BuildItemHud(Transform parent)
        {
            var root = Panel("ItemHud", new Color(0.12f, 0.15f, 0.16f, 1f)); root.transform.SetParent(parent, false); root.AddComponent<LayoutElement>().preferredHeight = 260f;
            var layout = root.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(28, 28, 24, 24); layout.spacing = 10f; layout.childControlWidth = true; layout.childForceExpandWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            var heading = Text("SectionTitle", root.transform, 20, TextAlignmentOptions.Left, new Color(0.52f, 0.78f, 0.74f)); heading.text = "ITEM HUD";
            var title = Text("Title", root.transform, 34, TextAlignmentOptions.Left, Color.white);
            var detail = Text("Detail", root.transform, 23, TextAlignmentOptions.Left, new Color(0.75f, 0.79f, 0.78f));
            var button = Button("UseButton", root.transform, "USE ITEM");
            var icon = ArtImage("ItemIcon", root.transform, 72f);
            var view = root.AddComponent<HandoffItemHudView>(); Set(view, "titleLabel", title); Set(view, "detailLabel", detail); Set(view, "actionButton", button); Set(view, "iconImage", icon);
            return view;
        }

        private static HandoffMerchantView BuildMerchant(Transform parent, HandoffMerchantOfferView offerPrefab)
        {
            var root = Panel("Merchant", new Color(0.12f, 0.14f, 0.15f, 1f)); root.transform.SetParent(parent, false); root.AddComponent<LayoutElement>().flexibleHeight = 1f;
            var layout = root.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(28, 28, 24, 24); layout.spacing = 14f; layout.childControlWidth = true; layout.childForceExpandWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            var heading = Text("SectionTitle", root.transform, 20, TextAlignmentOptions.Left, new Color(0.89f, 0.68f, 0.38f)); heading.text = "MERCHANT";
            var status = Text("Status", root.transform, 24, TextAlignmentOptions.Left, Color.white); status.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;
            var offers = new GameObject("OfferContainer", typeof(RectTransform), typeof(VerticalLayoutGroup)); offers.transform.SetParent(root.transform, false); var offerLayout = offers.GetComponent<VerticalLayoutGroup>(); offerLayout.spacing = 12f; offerLayout.childControlWidth = true; offerLayout.childForceExpandWidth = true; offerLayout.childControlHeight = true; offerLayout.childForceExpandHeight = false; offers.AddComponent<LayoutElement>().flexibleHeight = 1f;
            var view = root.AddComponent<HandoffMerchantView>(); Set(view, "statusLabel", status); Set(view, "offerContainer", offers.transform); Set(view, "offerPrefab", offerPrefab);
            return view;
        }

        private static void BuildScene(GameObject screenPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("PreviewCamera", typeof(Camera)).GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.04f, 0.05f, 0.055f); camera.orthographic = true;
            var canvas = new GameObject("UI_HandoffCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceCamera; canvas.GetComponent<Canvas>().worldCamera = camera; canvas.GetComponent<Canvas>().planeDistance = 1f;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080f, 1920f); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 0.5f;
            var screen = PrefabUtility.InstantiatePrefab(screenPrefab, canvas.transform) as GameObject; screen.name = "UI_HandoffScreen"; SetStretch(screen.GetComponent<RectTransform>());
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static GameObject Panel(string name, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); item.GetComponent<Image>().color = color; return item;
        }
        private static TextMeshProUGUI Text(string name, Transform parent, float size, TextAlignmentOptions alignment, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); item.transform.SetParent(parent, false); var text = item.GetComponent<TextMeshProUGUI>(); text.font = TMP_Settings.defaultFontAsset; text.fontSize = size; text.alignment = alignment; text.color = color; text.enableWordWrapping = true; text.text = name; return text;
        }
        private static Button Button(string name, Transform parent, string label)
        {
            var item = Panel(name, new Color(0.20f, 0.37f, 0.36f, 1f)); item.transform.SetParent(parent, false); item.AddComponent<LayoutElement>().preferredHeight = 54f; var button = item.AddComponent<Button>(); button.targetGraphic = item.GetComponent<Image>(); var text = Text("Label", item.transform, 22, TextAlignmentOptions.Center, Color.white); SetStretch(text.rectTransform); text.text = label; return button;
        }
        private static Image ArtImage(string name, Transform parent, float size)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(1f, 1f); rect.anchoredPosition = new Vector2(-20f, -20f); rect.sizeDelta = new Vector2(size, size);
            item.GetComponent<LayoutElement>().ignoreLayout = true;
            var image = item.GetComponent<Image>(); image.enabled = false; image.preserveAspect = true;
            return image;
        }
        private static void SetStretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
        private static void SetSize(RectTransform rect, float left, float right, float top, float bottom) { rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(right, top); }
        private static void Set(UnityEngine.Object target, string property, UnityEngine.Object value) { var serialized = new SerializedObject(target); serialized.FindProperty(property).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetVector2(UnityEngine.Object target, string property, Vector2 value) { var serialized = new SerializedObject(target); serialized.FindProperty(property).vector2Value = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
