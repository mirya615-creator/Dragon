using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the Main scene energy display and guarded Start button flow.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainEnergyController : MonoBehaviour
{
    private Button startButton;
    private TMP_Text currentAmountText;
    private TMP_Text maximumAmountText;
    private TMP_Text tipText;
    private IPlayerEnergyGateway energyGateway;
    private CancellationTokenSource lifetimeCancellation;
    private bool requestInProgress;
    private bool transitionRequested;

    private void Awake()
    {
        energyGateway = new LocalPlayerEnergyGateway();
        lifetimeCancellation = new CancellationTokenSource();

        if (!ResolveView())
        {
            enabled = false;
            return;
        }

        tipText.text = string.Empty;
        maximumAmountText.text = "/" + LocalPlayerEnergyGateway.MaximumEnergy;
        startButton.onClick.AddListener(OnStartClicked);
    }

    private async void Start()
    {
        string playerId = GetPlayerId();
        if (string.IsNullOrEmpty(playerId))
        {
            Debug.LogError("MainEnergyController requires an authenticated player session.");
            startButton.interactable = false;
            return;
        }

        try
        {
            PlayerEnergyState state = await energyGateway.GetEnergyAsync(
                playerId, lifetimeCancellation.Token);
            RefreshEnergy(state);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            startButton.interactable = false;
        }
    }

    private void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
        lifetimeCancellation?.Cancel();
        lifetimeCancellation?.Dispose();
    }

    private async void OnStartClicked()
    {
        if (requestInProgress || transitionRequested) return;

        string playerId = GetPlayerId();
        if (string.IsNullOrEmpty(playerId) || SceneLoader.Instance == null)
        {
            Debug.LogError("Cannot start the game without a player session and SceneLoader.");
            return;
        }

        requestInProgress = true;
        startButton.interactable = false;

        try
        {
            EnergyConsumeResult result = await energyGateway.ConsumeEnergyAsync(
                playerId,
                LocalPlayerEnergyGateway.GameStartCost,
                Guid.NewGuid().ToString("N"),
                lifetimeCancellation.Token);

            RefreshEnergy(result.State);
            if (!result.Succeeded)
            {
                ShowTip("Not enough");
                return;
            }

            ShowTip(string.Empty);
            transitionRequested = true;
            SceneLoader.Instance.LoadSceneAsync("Game");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            requestInProgress = false;
            if (!transitionRequested && startButton != null)
            {
                startButton.interactable = true;
            }
        }
    }

    private bool ResolveView()
    {
        startButton = transform.Find("StartBtn")?.GetComponent<Button>();
        currentAmountText = transform.Find("EnergyBg/RAmount")?.GetComponent<TMP_Text>();
        maximumAmountText = transform.Find("EnergyBg/MaxAmount")?.GetComponent<TMP_Text>();
        tipText = FindDescendant(transform, "TipText")?.GetComponent<TMP_Text>();

        if (tipText == null && currentAmountText != null)
        {
            tipText = CreateTipText(currentAmountText);
        }

        bool complete = startButton != null && currentAmountText != null &&
                        maximumAmountText != null && tipText != null;
        if (!complete)
        {
            Debug.LogError("MainEnergyController is missing StartBtn, RAmount, MaxAmount, or TipText.");
        }
        return complete;
    }

    private TMP_Text CreateTipText(TMP_Text styleSource)
    {
        GameObject tipObject = new GameObject("TipText", typeof(RectTransform), typeof(TextMeshProUGUI));
        tipObject.layer = gameObject.layer;
        RectTransform rect = tipObject.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -190f);
        rect.sizeDelta = new Vector2(600f, 80f);

        TextMeshProUGUI createdText = tipObject.GetComponent<TextMeshProUGUI>();
        createdText.font = styleSource.font;
        createdText.fontSize = styleSource.fontSize;
        createdText.alignment = TextAlignmentOptions.Center;
        createdText.color = new Color32(220, 65, 65, 255);
        createdText.raycastTarget = false;
        return createdText;
    }

    private void RefreshEnergy(PlayerEnergyState state)
    {
        if (state == null) return;
        currentAmountText.text = state.Current.ToString();
        maximumAmountText.text = "/" + state.Maximum;
    }

    private void ShowTip(string message)
    {
        if (tipText != null) tipText.text = message;
    }

    private static string GetPlayerId()
    {
        AuthSession session = AuthSessionStore.Current;
        return session != null ? session.PlayerId : string.Empty;
    }

    private static Transform FindDescendant(Transform parent, string objectName)
    {
        if (parent == null) return null;
        foreach (Transform child in parent)
        {
            if (string.Equals(child.name, objectName, StringComparison.Ordinal)) return child;
            Transform nested = FindDescendant(child, objectName);
            if (nested != null) return nested;
        }
        return null;
    }
}
