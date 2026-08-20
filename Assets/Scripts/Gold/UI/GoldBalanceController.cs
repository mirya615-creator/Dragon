using System;
using System.Globalization;
using System.Threading;
using TMPro;
using UnityEngine;

/// <summary>
/// Displays the authenticated player's gold on a CoinBg prefab instance.
/// </summary>
[DisallowMultipleComponent]
public sealed class GoldBalanceController : MonoBehaviour
{
    private TMP_Text amountText;
    private IPlayerGoldGateway goldGateway;
    private IAuthSessionStore authSessionStore;
    private CancellationTokenSource lifetimeCancellation;
    private string playerId;

    private void Awake()
    {
        IClientServices services = ClientCompositionRoot.Current;
        amountText = transform.Find("CoinQua")?.GetComponent<TMP_Text>();
        goldGateway = services.Gold;
        authSessionStore = services.AuthSession;
        lifetimeCancellation = new CancellationTokenSource();

        if (amountText == null)
        {
            Debug.LogError("GoldBalanceController expects a CoinQua TMP text under CoinBg.");
            enabled = false;
            return;
        }

        amountText.text = "0";
        PlayerGoldEvents.BalanceChanged += OnBalanceChanged;
    }

    private async void Start()
    {
        AuthSession session = authSessionStore.Current;
        if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
        {
            Debug.LogError("GoldBalanceController requires an authenticated player session.");
            return;
        }

        try
        {
            playerId = session.PlayerId;
            PlayerGoldState state = await goldGateway.GetGoldAsync(
                playerId,
                lifetimeCancellation.Token);
            amountText.text = state.Balance.ToString(CultureInfo.InvariantCulture);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogError($"Unable to load player gold: {exception.Message}");
        }
    }

    private void OnDestroy()
    {
        PlayerGoldEvents.BalanceChanged -= OnBalanceChanged;
        lifetimeCancellation?.Cancel();
        lifetimeCancellation?.Dispose();
        lifetimeCancellation = null;
    }

    private void OnBalanceChanged(string changedPlayerId, long balance)
    {
        if (changedPlayerId != playerId || amountText == null) return;
        amountText.text = balance.ToString(CultureInfo.InvariantCulture);
    }
}
