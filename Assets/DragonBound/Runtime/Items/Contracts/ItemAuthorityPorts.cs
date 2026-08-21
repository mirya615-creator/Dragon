using System;

namespace DragonBound.Items.Contracts
{
    public readonly struct AccountProgress
    {
        public AccountProgress(int normalCompletedMatchCount)
        {
            if (normalCompletedMatchCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(normalCompletedMatchCount));
            }

            NormalCompletedMatchCount = normalCompletedMatchCount;
        }

        public int NormalCompletedMatchCount { get; }
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

        public static DayKey Empty => default(DayKey);
        public string Value { get; }
        public bool Equals(DayKey other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is DayKey other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;
    }

    public interface IItemAccountProgressProvider
    {
        bool TryGetAccountProgress(out AccountProgress progress);
    }

    public interface IItemDayKeyProvider
    {
        bool TryGetDayKey(out DayKey dayKey);
    }

    public interface IItemSnapshotProvider
    {
        bool TryGetSnapshot(out ItemSnapshot snapshot);
    }

    public interface IItemCommandPort
    {
        ItemResult Execute(ItemCommand command);
    }

    public interface IMerchantOfferProvider
    {
        bool TryGetOffers(out MerchantOffer[] offers);
    }

    public interface IAdRewardClaimPort
    {
        ItemResult Submit(AdRewardClaim claim);
    }
}
