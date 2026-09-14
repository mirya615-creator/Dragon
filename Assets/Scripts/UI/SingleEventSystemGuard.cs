using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps scene transitions from leaving more than one active EventSystem. Scene-authored
/// EventSystems remain the source of truth; only active duplicates are disabled.
/// </summary>
public static class SingleEventSystemGuard
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureSingle(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureSingle(scene);
    }

    internal static void EnsureSingle(Scene activeScene)
    {
        EventSystem[] activeSystems = Object.FindObjectsOfType<EventSystem>(true)
            .Where(value => value != null && value.isActiveAndEnabled)
            .ToArray();
        if (activeSystems.Length <= 1) return;

        EventSystem preferred = activeSystems.FirstOrDefault(
            value => value.gameObject.scene == activeScene) ?? activeSystems[0];
        foreach (EventSystem duplicate in activeSystems)
        {
            if (duplicate == preferred) continue;

            foreach (BaseInputModule inputModule in duplicate.GetComponents<BaseInputModule>())
                inputModule.enabled = false;
            duplicate.enabled = false;
            Debug.LogWarning(
                $"Disabled duplicate EventSystem '{duplicate.name}' from scene " +
                $"'{duplicate.gameObject.scene.name}'. Active EventSystem is " +
                $"'{preferred.name}' in scene '{preferred.gameObject.scene.name}'.");
        }
    }
}
