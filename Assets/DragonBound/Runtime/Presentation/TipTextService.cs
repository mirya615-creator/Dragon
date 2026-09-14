using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DragonBound.UI
{
    /// <summary>
    /// 全局统一提示入口。首次调用懒加载 Resources/prefabs/TipText.prefab，
    /// 跨场景自动重建父 Canvas，单实例复用（后到覆盖先到，刷新计时）。
    /// 显示位置沿用预制体初始 RectTransform；可指定 anchoredPosition。
    /// </summary>
    public static class TipTextService
    {
        private const string PrefabPath = "prefabs/TipText";

        private static GameObject cachedPrefab;
        private static TipTextController activeInstance;

        /// <summary>显示一条提示（默认 3 秒）。空串或 null = 隐藏已有提示。</summary>
        public static void Show(string message) => Show(message, 3f, null);

        public static void Show(string message, float seconds, Vector2? anchoredPosition = null)
        {
            if (string.IsNullOrEmpty(message))
            {
                if (activeInstance != null) activeInstance.Hide();
                return;
            }

            EnsureInstance();
            if (activeInstance == null) return; // 资源缺失：降级静默，不打断业务

            activeInstance.Show(message, seconds, anchoredPosition);
        }

        public static void Hide()
        {
            if (activeInstance != null) activeInstance.Hide();
        }

        private static void EnsureInstance()
        {
            if (cachedPrefab == null)
                cachedPrefab = Resources.Load<GameObject>(PrefabPath);

            if (cachedPrefab == null)
            {
                Debug.LogError("[TipTextService] Cannot load prefab at " + PrefabPath);
                return;
            }

            if (activeInstance != null && activeInstance.gameObject != null &&
                activeInstance.gameObject.activeInHierarchy == false)
            {
                // 跨场景后实例可能失活，但还在内存；可直接复用，但需重挂父节点
            }

            if (activeInstance == null)
                activeInstance = Object.Instantiate(cachedPrefab).GetComponent<TipTextController>();

            Canvas canvas = ResolveActiveCanvas();
            if (canvas != null)
            {
                Transform tipTransform = activeInstance.transform;
                if (tipTransform.parent != canvas.transform)
                {
                    tipTransform.SetParent(canvas.transform, false);
                    tipTransform.SetAsLastSibling();
                }
            }
        }

        private static Canvas ResolveActiveCanvas()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return null;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Canvas canvas = roots[i].GetComponentInChildren<Canvas>(true);
                if (canvas != null && canvas.isActiveAndEnabled) return canvas;
            }
            return null;
        }
    }
}

