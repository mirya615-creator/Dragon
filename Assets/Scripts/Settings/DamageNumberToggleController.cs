using GameShared.Settings;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class DamageNumberToggleController : MonoBehaviour
{
    private Button toggleButton;
    private GameObject selectedState;

    private void Awake()
    {
        toggleButton = GetComponent<Button>();
        Transform stateTransform = transform.Find("State");
        selectedState = stateTransform != null ? stateTransform.gameObject : null;
        if (selectedState == null)
        {
            Debug.LogError("DegBtn requires a direct child named 'State'.", this);
        }

        toggleButton.onClick.AddListener(ToggleDamageNumbers);
        RefreshState();
    }

    private void OnEnable()
    {
        RefreshState();
    }

    private void OnDestroy()
    {
        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(ToggleDamageNumbers);
        }
    }

    private void ToggleDamageNumbers()
    {
        DamageNumberSettings.Visible = !DamageNumberSettings.Visible;
        RefreshState();
    }

    private void RefreshState()
    {
        if (selectedState != null)
        {
            selectedState.SetActive(DamageNumberSettings.Visible);
        }
    }
}
