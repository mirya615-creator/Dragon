using System;
using System.Collections.Generic;
using DragonBound.Foundation.Contracts;

namespace DragonBound.Runes.Contracts
{
    public enum RuneContractStatus
    {
        Ready,
        Pending,
        NotConfigured,
        Rejected
    }

    public enum FeatureGateState
    {
        Locked,
        Unlocked,
        Pending,
        NotConfigured
    }

    public enum RewardGrantState
    {
        Pending,
        Granted,
        NotConfigured,
        Rejected
    }

    public enum RuneRarity
    {
        Common,
        Excellent,
        Epic,
        Legendary
    }

    public readonly struct RuneId : IEquatable<RuneId>
    {
        public RuneId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A RuneId is required.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public bool Equals(RuneId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is RuneId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(RuneId left, RuneId right) => left.Equals(right);
        public static bool operator !=(RuneId left, RuneId right) => !left.Equals(right);
    }

    public readonly struct AccountDay : IEquatable<AccountDay>, IComparable<AccountDay>
    {
        public AccountDay(int value)
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "AccountDay starts at 1.");
            }

            Value = value;
        }

        public int Value { get; }

        public int CompareTo(AccountDay other) => Value.CompareTo(other.Value);
        public bool Equals(AccountDay other) => Value == other.Value;
        public override bool Equals(object obj) => obj is AccountDay other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();

        public static bool operator ==(AccountDay left, AccountDay right) => left.Equals(right);
        public static bool operator !=(AccountDay left, AccountDay right) => !left.Equals(right);
    }

    public readonly struct DayKey : IEquatable<DayKey>
    {
        public DayKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A DayKey is required.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public bool Equals(DayKey other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is DayKey other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(DayKey left, DayKey right) => left.Equals(right);
        public static bool operator !=(DayKey left, DayKey right) => !left.Equals(right);
    }

    public sealed class LoadoutAssignment
    {
        public LoadoutAssignment(string heroId, RuneId runeId)
        {
            if (string.IsNullOrWhiteSpace(heroId))
            {
                throw new ArgumentException("A HeroId is required.", nameof(heroId));
            }

            if (!runeId.IsValid)
            {
                throw new ArgumentException("A valid RuneId is required.", nameof(runeId));
            }

            HeroId = heroId;
            RuneId = runeId;
        }

        public string HeroId { get; }
        public RuneId RuneId { get; }
    }

    public sealed class RuneInventoryEntrySnapshot
    {
        public RuneInventoryEntrySnapshot(RuneId runeId, RuneRarity rarity, int ownedCount, int fragmentCount)
        {
            if (!runeId.IsValid)
            {
                throw new ArgumentException("A valid RuneId is required.", nameof(runeId));
            }

            if (ownedCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ownedCount));
            }

            if (fragmentCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fragmentCount));
            }

            RuneId = runeId;
            Rarity = rarity;
            OwnedCount = ownedCount;
            FragmentCount = fragmentCount;
        }

        public RuneId RuneId { get; }
        public RuneRarity Rarity { get; }
        public int OwnedCount { get; }
        public int FragmentCount { get; }
    }

    public sealed class RuneProfileSnapshot
    {
        public RuneProfileSnapshot(
            RuneContractStatus status,
            AccountDay accountDay,
            string contentVersion,
            IEnumerable<RuneInventoryEntrySnapshot> inventory,
            IEnumerable<LoadoutAssignment> loadoutAssignments)
        {
            Status = status;
            AccountDay = accountDay;
            ContentVersion = contentVersion ?? string.Empty;
            Inventory = Copy(inventory);
            LoadoutAssignments = Copy(loadoutAssignments);
        }

        public RuneContractStatus Status { get; }
        public AccountDay AccountDay { get; }
        public string ContentVersion { get; }
        public IReadOnlyList<RuneInventoryEntrySnapshot> Inventory { get; }
        public IReadOnlyList<LoadoutAssignment> LoadoutAssignments { get; }
        public bool IsReady => Status == RuneContractStatus.Ready;

        public static RuneProfileSnapshot Pending => CreateUnavailable(RuneContractStatus.Pending);
        public static RuneProfileSnapshot NotConfigured => CreateUnavailable(RuneContractStatus.NotConfigured);

        private static RuneProfileSnapshot CreateUnavailable(RuneContractStatus status)
        {
            return new RuneProfileSnapshot(
                status,
                default(AccountDay),
                string.Empty,
                null,
                null);
        }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> source)
        {
            return new List<T>(source ?? Array.Empty<T>()).AsReadOnly();
        }
    }

    public sealed class RunSnapshot
    {
        public RunSnapshot(
            RuneContractStatus status,
            RunId runId,
            AccountDay accountDay,
            DayKey dayKey,
            IEnumerable<LoadoutAssignment> loadoutAssignments)
        {
            Status = status;
            RunId = runId;
            AccountDay = accountDay;
            DayKey = dayKey;
            LoadoutAssignments = new List<LoadoutAssignment>(loadoutAssignments ?? Array.Empty<LoadoutAssignment>()).AsReadOnly();
        }

        public RuneContractStatus Status { get; }
        public RunId RunId { get; }
        public AccountDay AccountDay { get; }
        public DayKey DayKey { get; }
        public IReadOnlyList<LoadoutAssignment> LoadoutAssignments { get; }
        public bool IsReady => Status == RuneContractStatus.Ready;

        public static RunSnapshot Pending => new RunSnapshot(RuneContractStatus.Pending, default(RunId), default(AccountDay), default(DayKey), null);
        public static RunSnapshot NotConfigured => new RunSnapshot(RuneContractStatus.NotConfigured, default(RunId), default(AccountDay), default(DayKey), null);
    }

    public sealed class FeatureGateResult
    {
        private FeatureGateResult(FeatureGateState state, AccountDay accountDay, string reasonCode)
        {
            State = state;
            AccountDay = accountDay;
            ReasonCode = reasonCode ?? string.Empty;
        }

        public FeatureGateState State { get; }
        public AccountDay AccountDay { get; }
        public string ReasonCode { get; }
        public bool IsUnlocked => State == FeatureGateState.Unlocked;

        public static FeatureGateResult Locked(AccountDay accountDay, string reasonCode)
        {
            return new FeatureGateResult(FeatureGateState.Locked, accountDay, reasonCode);
        }

        public static FeatureGateResult Unlocked(AccountDay accountDay)
        {
            return new FeatureGateResult(FeatureGateState.Unlocked, accountDay, string.Empty);
        }

        public static FeatureGateResult Pending => new FeatureGateResult(FeatureGateState.Pending, default(AccountDay), "Pending");
        public static FeatureGateResult NotConfigured => new FeatureGateResult(FeatureGateState.NotConfigured, default(AccountDay), "NotConfigured");
    }

    public sealed class RuneRewardRequest
    {
        public RuneRewardRequest(RunId runId, WaveNumber completedWave)
        {
            RunId = runId;
            CompletedWave = completedWave;
        }

        public RunId RunId { get; }
        public WaveNumber CompletedWave { get; }
    }

    public sealed class RewardGrant
    {
        public RewardGrant(
            RewardGrantState state,
            string grantId,
            RuneId runeId,
            RuneRarity rarity,
            int quantity,
            bool isFragment)
        {
            if (quantity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity));
            }

            State = state;
            GrantId = grantId ?? string.Empty;
            RuneId = runeId;
            Rarity = rarity;
            Quantity = quantity;
            IsFragment = isFragment;
        }

        public RewardGrantState State { get; }
        public string GrantId { get; }
        public RuneId RuneId { get; }
        public RuneRarity Rarity { get; }
        public int Quantity { get; }
        public bool IsFragment { get; }
        public bool IsGranted => State == RewardGrantState.Granted;

        public static RewardGrant Pending => CreateUnavailable(RewardGrantState.Pending);
        public static RewardGrant NotConfigured => CreateUnavailable(RewardGrantState.NotConfigured);

        private static RewardGrant CreateUnavailable(RewardGrantState state)
        {
            return new RewardGrant(state, string.Empty, default(RuneId), default(RuneRarity), 0, false);
        }
    }

    public interface IRuneProfileProvider
    {
        RuneProfileSnapshot GetProfile();
    }

    public interface IRuneSnapshotProvider
    {
        RunSnapshot CreateSnapshot(RunId runId, RuneProfileSnapshot profile);
    }

    public interface IRuneRewardProvider
    {
        RewardGrant RequestReward(RuneRewardRequest request);
    }
}
