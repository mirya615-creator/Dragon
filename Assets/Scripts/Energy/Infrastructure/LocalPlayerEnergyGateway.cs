using System;
using System.Globalization;
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
    public const int RecoveryIntervalSeconds = 3 * 60;

    private const string EnergyKeyPrefix = "dragonbound.player-energy.";
    private const string NextRecoveryKeySuffix = ".next-recovery";
    private const string RewardTransactionKeySegment = ".reward.";
    private const string RewardedAdDayKeySuffix = ".rewarded-ad-day";
    private const string RewardedAdCountKeySuffix = ".rewarded-ad-count";

    public Task<PlayerEnergyState> GetEnergyAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string key = GetEnergyKey(playerId);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        EnergyRecord record = LoadAndRecover(key, now);
        if (record.Changed) Save(key, record.Current, record.NextRecoveryUnixTime);
        return Task.FromResult(CreateState(record.Current, record.NextRecoveryUnixTime));
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
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        EnergyRecord record = LoadAndRecover(key, now);

        if (record.Current < amount)
        {
            if (record.Changed) Save(key, record.Current, record.NextRecoveryUnixTime);
            return Task.FromResult(new EnergyConsumeResult
            {
                Succeeded = false,
                State = CreateState(record.Current, record.NextRecoveryUnixTime)
            });
        }

        record.Current -= amount;
        if (record.NextRecoveryUnixTime <= 0)
        {
            record.NextRecoveryUnixTime = now + RecoveryIntervalSeconds;
        }
        Save(key, record.Current, record.NextRecoveryUnixTime);
        return Task.FromResult(new EnergyConsumeResult
        {
            Succeeded = true,
            State = CreateState(record.Current, record.NextRecoveryUnixTime)
        });
    }

    public Task<PlayerEnergyState> GrantEnergyAsync(
        string playerId,
        int amount,
        string rewardTransactionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (string.IsNullOrWhiteSpace(rewardTransactionId))
        {
            throw new ArgumentException("Reward transaction ID is required.", nameof(rewardTransactionId));
        }

        string key = GetEnergyKey(playerId);
        string transactionKey = key + RewardTransactionKeySegment + HashKey(rewardTransactionId);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        EnergyRecord record = LoadAndRecover(key, now);

        if (PlayerPrefs.HasKey(transactionKey))
        {
            if (record.Changed) Save(key, record.Current, record.NextRecoveryUnixTime);
            return Task.FromResult(CreateState(record.Current, record.NextRecoveryUnixTime));
        }

        record.Current = Mathf.Min(MaximumEnergy, record.Current + amount);
        if (record.Current >= MaximumEnergy)
        {
            record.NextRecoveryUnixTime = 0;
        }
        else if (record.NextRecoveryUnixTime <= 0)
        {
            record.NextRecoveryUnixTime = now + RecoveryIntervalSeconds;
        }

        PlayerPrefs.SetInt(transactionKey, 1);
        Save(key, record.Current, record.NextRecoveryUnixTime);
        return Task.FromResult(CreateState(record.Current, record.NextRecoveryUnixTime));
    }

    public Task<DailyRewardStatus> GetRewardedAdStatusAsync(
        string playerId,
        int dailyLimit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (dailyLimit <= 0) throw new ArgumentOutOfRangeException(nameof(dailyLimit));

        string key = GetEnergyKey(playerId);
        DailyAdRecord record = LoadDailyAdRecord(key);
        if (record.Changed) SaveDailyAdRecord(key, record);
        return Task.FromResult(CreateDailyRewardStatus(record.Count, dailyLimit));
    }

    public Task<RewardedAdEnergyClaimResult> ClaimRewardedAdEnergyAsync(
        string playerId,
        int amount,
        int dailyLimit,
        string rewardTransactionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (dailyLimit <= 0) throw new ArgumentOutOfRangeException(nameof(dailyLimit));
        if (string.IsNullOrWhiteSpace(rewardTransactionId))
        {
            throw new ArgumentException("Reward transaction ID is required.", nameof(rewardTransactionId));
        }

        string key = GetEnergyKey(playerId);
        string transactionKey = key + RewardTransactionKeySegment + HashKey(rewardTransactionId);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        EnergyRecord energy = LoadAndRecover(key, now);
        DailyAdRecord daily = LoadDailyAdRecord(key);

        if (PlayerPrefs.HasKey(transactionKey))
        {
            if (energy.Changed) Save(key, energy.Current, energy.NextRecoveryUnixTime);
            return Task.FromResult(CreateRewardedAdClaimResult(
                true, daily.Count, dailyLimit, energy));
        }

        if (daily.Count >= dailyLimit)
        {
            if (energy.Changed) Save(key, energy.Current, energy.NextRecoveryUnixTime);
            if (daily.Changed) SaveDailyAdRecord(key, daily);
            return Task.FromResult(CreateRewardedAdClaimResult(
                false, daily.Count, dailyLimit, energy));
        }

        energy.Current = Mathf.Min(MaximumEnergy, energy.Current + amount);
        if (energy.Current >= MaximumEnergy)
        {
            energy.NextRecoveryUnixTime = 0;
        }
        else if (energy.NextRecoveryUnixTime <= 0)
        {
            energy.NextRecoveryUnixTime = now + RecoveryIntervalSeconds;
        }

        daily.Count++;
        PlayerPrefs.SetInt(transactionKey, 1);
        PlayerPrefs.SetString(key + RewardedAdDayKeySuffix, daily.DayKey);
        PlayerPrefs.SetInt(key + RewardedAdCountKeySuffix, daily.Count);
        Save(key, energy.Current, energy.NextRecoveryUnixTime);
        return Task.FromResult(CreateRewardedAdClaimResult(
            true, daily.Count, dailyLimit, energy));
    }

    private static EnergyRecord LoadAndRecover(string energyKey, long now)
    {
        bool hasEnergy = PlayerPrefs.HasKey(energyKey);
        int storedCurrent = hasEnergy ? PlayerPrefs.GetInt(energyKey, MaximumEnergy) : MaximumEnergy;
        int current = Mathf.Clamp(storedCurrent, 0, MaximumEnergy);
        string recoveryKey = energyKey + NextRecoveryKeySuffix;
        string storedNextRecovery = PlayerPrefs.GetString(recoveryKey, string.Empty);
        bool validRecoveryTime = long.TryParse(
            storedNextRecovery,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out long nextRecovery);

        EnergyRecord record = new EnergyRecord
        {
            Current = current,
            NextRecoveryUnixTime = validRecoveryTime ? nextRecovery : 0,
            Changed = !hasEnergy || storedCurrent != current ||
                      (!string.IsNullOrEmpty(storedNextRecovery) && !validRecoveryTime)
        };

        if (record.Current >= MaximumEnergy)
        {
            if (record.NextRecoveryUnixTime != 0)
            {
                record.NextRecoveryUnixTime = 0;
                record.Changed = true;
            }
            return record;
        }

        // A missing timestamp starts a fresh recovery cycle. A timestamp farther
        // than one interval in the future is treated as a local-clock rollback.
        if (record.NextRecoveryUnixTime <= 0 ||
            record.NextRecoveryUnixTime > now + RecoveryIntervalSeconds)
        {
            record.NextRecoveryUnixTime = now + RecoveryIntervalSeconds;
            record.Changed = true;
            return record;
        }

        if (now < record.NextRecoveryUnixTime) return record;

        long elapsedIntervals = ((now - record.NextRecoveryUnixTime) / RecoveryIntervalSeconds) + 1;
        int missingEnergy = MaximumEnergy - record.Current;
        int recovered = (int)Math.Min(elapsedIntervals, missingEnergy);
        record.Current += recovered;
        record.NextRecoveryUnixTime = record.Current >= MaximumEnergy
            ? 0
            : record.NextRecoveryUnixTime + recovered * RecoveryIntervalSeconds;
        record.Changed = true;
        return record;
    }

    private static DailyAdRecord LoadDailyAdRecord(string energyKey)
    {
        string currentDayKey = DateTimeOffset.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        string storedDayKey = PlayerPrefs.GetString(
            energyKey + RewardedAdDayKeySuffix,
            string.Empty);
        int storedCount = PlayerPrefs.GetInt(energyKey + RewardedAdCountKeySuffix, 0);

        if (storedDayKey != currentDayKey)
        {
            return new DailyAdRecord
            {
                DayKey = currentDayKey,
                Count = 0,
                Changed = true
            };
        }

        int safeCount = Math.Max(0, storedCount);
        return new DailyAdRecord
        {
            DayKey = currentDayKey,
            Count = safeCount,
            Changed = safeCount != storedCount
        };
    }

    private static string GetEnergyKey(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            throw new ArgumentException("Player ID is required.", nameof(playerId));
        }

        return EnergyKeyPrefix + HashKey(playerId);
    }

    private static string HashKey(string value)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            return Convert.ToBase64String(digest).Replace('/', '_').Replace('+', '-').TrimEnd('=');
        }
    }

    private static PlayerEnergyState CreateState(int current, long nextRecoveryUnixTime)
    {
        return new PlayerEnergyState
        {
            Current = current,
            Maximum = MaximumEnergy,
            NextRecoveryUnixTime = nextRecoveryUnixTime
        };
    }

    private static DailyRewardStatus CreateDailyRewardStatus(int count, int dailyLimit)
    {
        return new DailyRewardStatus
        {
            ClaimsUsed = count,
            DailyLimit = dailyLimit,
            CanClaim = count < dailyLimit
        };
    }

    private static RewardedAdEnergyClaimResult CreateRewardedAdClaimResult(
        bool succeeded,
        int count,
        int dailyLimit,
        EnergyRecord energy)
    {
        return new RewardedAdEnergyClaimResult
        {
            Succeeded = succeeded,
            LimitReached = count >= dailyLimit,
            ClaimsUsed = count,
            DailyLimit = dailyLimit,
            State = CreateState(energy.Current, energy.NextRecoveryUnixTime)
        };
    }

    private static void Save(string key, int current, long nextRecoveryUnixTime)
    {
        PlayerPrefs.SetInt(key, current);
        PlayerPrefs.SetString(
            key + NextRecoveryKeySuffix,
            nextRecoveryUnixTime.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

    private static void SaveDailyAdRecord(string key, DailyAdRecord record)
    {
        PlayerPrefs.SetString(key + RewardedAdDayKeySuffix, record.DayKey);
        PlayerPrefs.SetInt(key + RewardedAdCountKeySuffix, record.Count);
        PlayerPrefs.Save();
    }

    private struct EnergyRecord
    {
        public int Current;
        public long NextRecoveryUnixTime;
        public bool Changed;
    }

    private struct DailyAdRecord
    {
        public string DayKey;
        public int Count;
        public bool Changed;
    }
}
