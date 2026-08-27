using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class UiImageFadeLoop : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float fadeInDuration = 0.5f;
    [SerializeField, Min(0f)] private float visibleDuration = 1f;
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.5f;
    [SerializeField] private bool useUnscaledTime = true;

    private CanvasGroup canvasGroup;
    private float elapsed;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
    }

    private void OnEnable()
    {
        elapsed = 0f;
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 0f;
    }

    private void Update()
    {
        var cycleDuration = fadeInDuration + visibleDuration + fadeOutDuration;
        if (cycleDuration <= 0f)
        {
            canvasGroup.alpha = 1f;
            return;
        }

        elapsed = Mathf.Repeat(
            elapsed + (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime),
            cycleDuration);

        if (elapsed < fadeInDuration)
        {
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            return;
        }

        var fadeOutStart = fadeInDuration + visibleDuration;
        if (elapsed < fadeOutStart)
        {
            canvasGroup.alpha = 1f;
            return;
        }

        canvasGroup.alpha = 1f - Mathf.Clamp01((elapsed - fadeOutStart) / fadeOutDuration);
    }
}

public static class UiImageFadeLoopInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        InstallInScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallInScene(scene);
    }

    private static void InstallInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        var roots = scene.GetRootGameObjects();
        for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            var canvases = roots[rootIndex].GetComponentsInChildren<Canvas>(true);
            for (var canvasIndex = 0; canvasIndex < canvases.Length; canvasIndex++)
            {
                if (!string.Equals(canvases[canvasIndex].name, "Canvas", StringComparison.Ordinal))
                {
                    continue;
                }

                var imageB = canvases[canvasIndex].transform.Find("ImageA/ImageB");
                if (imageB == null || imageB.GetComponent<Graphic>() == null)
                {
                    continue;
                }

                if (imageB.GetComponent<CanvasGroup>() == null)
                {
                    imageB.gameObject.AddComponent<CanvasGroup>();
                }

                if (imageB.GetComponent<UiImageFadeLoop>() == null)
                {
                    imageB.gameObject.AddComponent<UiImageFadeLoop>();
                }

                return;
            }
        }
    }
}
