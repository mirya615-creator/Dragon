using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class VisualStateToggleController : MonoBehaviour
{
    private Button toggleButton;
    private GameObject state;

    private void Awake()
    {
        toggleButton = GetComponent<Button>();

        Transform stateTransform = transform.Find("State");
        state = stateTransform != null ? stateTransform.gameObject : null;
        if (state == null)
        {
            Debug.LogError($"{name} requires a direct child named 'State'.", this);
            return;
        }

        toggleButton.onClick.AddListener(ToggleState);
    }

    private void OnDestroy()
    {
        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(ToggleState);
        }
    }

    private void ToggleState()
    {
        if (state != null)
        {
            state.SetActive(!state.activeSelf);
        }
    }
}
