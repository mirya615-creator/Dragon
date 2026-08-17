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
    private CancellationTokenSource lifetimeCancellation;

    private void Awake()
    {
        amountText = transform.Find("CoinQua")?.GetComponent<TMP_Text>();
        goldGateway = new LocalPlayerGoldGateway();
        lifetimeCancellation = new CancellationTokenSource();

        if (amountText == null)
        {
            Debug.LogError("GoldBalanceController expects a CoinQua TMP text under CoinBg.");
            enabled = false;
            return;
        }

        amountText.text = "0";
    }

    private async void Start()
    {
        AuthSession session = AuthSessionStore.Current;
        if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
        {
            Debug.LogError("GoldBalanceController requires an authenticated player session.");
            return;
        }

        try
        {
            PlayerGoldState state = await goldGateway.GetGoldAsync(
                session.PlayerId,
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
        lifetimeCancellation?.Cancel();
        lifetimeCancellation?.Dispose();
        lifetimeCancellation = null;
    }
}
