using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Offline player energy used during client development.
/// </summary>
public sealed class LocalPlayerEnergyGateway : IPlayerEnergyGateway
{
    public const int MaximumEnergy = 30;
    public const int GameStartCost = 5;

    private const string EnergyKeyPrefix = "dragonbound.player-energy.";

    public Task<PlayerEnergyState> GetEnergyAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string key = GetEnergyKey(playerId);
        int current;

        if (!PlayerPrefs.HasKey(key))
        {
            current = MaximumEnergy;
            Save(key, current);
        }
        else
        {
            current = Mathf.Clamp(PlayerPrefs.GetInt(key, MaximumEnergy), 0, MaximumEnergy);
            Save(key, current);
        }

        return Task.FromResult(CreateState(current));
    }

    public Task<EnergyConsumeResult> ConsumeEnergyAsync(
        string playerId,
        int amount,
        string requestId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (string.IsNullOrWhiteSpace(requestId)) throw new ArgumentException("Request ID is required.", nameof(requestId));

        string key = GetEnergyKey(playerId);
        int current = PlayerPrefs.HasKey(key)
            ? Mathf.Clamp(PlayerPrefs.GetInt(key, MaximumEnergy), 0, MaximumEnergy)
            : MaximumEnergy;

        if (current < amount)
        {
            Save(key, current);
            return Task.FromResult(new EnergyConsumeResult
            {
                Succeeded = false,
                State = CreateState(current)
            });
        }

        current -= amount;
        Save(key, current);
        return Task.FromResult(new EnergyConsumeResult
        {
            Succeeded = true,
            State = CreateState(current)
        });
    }

    private static string GetEnergyKey(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            throw new ArgumentException("Player ID is required.", nameof(playerId));
        }

        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(playerId));
            string hash = Convert.ToBase64String(digest).Replace('/', '_').Replace('+', '-').TrimEnd('=');
            return EnergyKeyPrefix + hash;
        }
    }

    private static PlayerEnergyState CreateState(int current)
    {
        return new PlayerEnergyState
        {
            Current = current,
            Maximum = MaximumEnergy
        };
    }

    private static void Save(string key, int current)
    {
        PlayerPrefs.SetInt(key, current);
        PlayerPrefs.Save();
    }
}
