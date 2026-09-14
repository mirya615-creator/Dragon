using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ActiveRunPlayModeExitHook
{
    private static bool exitHandled;

    static ActiveRunPlayModeExitHook()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            exitHandled = false;
            return;
        }
        if (state != PlayModeStateChange.ExitingPlayMode || exitHandled) return;

        exitHandled = true;
        if (!ActiveRunLifecycleCoordinator.TryForceQuitBlockingForEditor())
        {
            Debug.LogWarning(
                "Unable to close the active server Run while leaving Play Mode. " +
                "The next Play session will retry before enabling Start.");
        }
    }
}
