using System;

namespace DragonBound.Items.Contracts
{
    public readonly struct MerchantOffer
    {
        public MerchantOffer(
            string offerId,
            ItemId itemId,
            ItemCategory category,
            ItemConfigurationState state = ItemConfigurationState.Configured)
        {
            OfferId = Require(offerId, nameof(offerId));
            ItemId = itemId;
            Category = category;
            State = state;
        }

        public string OfferId { get; }
        public ItemId ItemId { get; }
        public ItemCategory Category { get; }
        public ItemConfigurationState State { get; }
        public bool IsSelectable => State == ItemConfigurationState.Configured;

        private static string Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", name);
            return value;
        }
    }

    public readonly struct MerchantSelection
    {
        public MerchantSelection(string offerId, ItemId itemId)
        {
            OfferId = string.IsNullOrWhiteSpace(offerId) ? string.Empty : offerId;
            ItemId = itemId;
        }

        public static MerchantSelection Empty => new MerchantSelection(string.Empty, default(ItemId));
        public string OfferId { get; }
        public ItemId ItemId { get; }
        public bool IsValid => !string.IsNullOrEmpty(OfferId) && !string.IsNullOrEmpty(ItemId.Value);
    }

    public readonly struct AdRewardClaim
    {
        public AdRewardClaim(string claimId, string placementId, string clientSessionId, DayKey dayKey)
        {
            ClaimId = Require(claimId, nameof(claimId));
            PlacementId = Require(placementId, nameof(placementId));
            ClientSessionId = Require(clientSessionId, nameof(clientSessionId));
            DayKey = dayKey;
        }

        public static AdRewardClaim Empty => default(AdRewardClaim);

        public string ClaimId { get; }
        public string PlacementId { get; }
        public string ClientSessionId { get; }
        public DayKey DayKey { get; }

        private static string Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", name);
            return value;
        }
    }

    public readonly struct LedgerReference : IEquatable<LedgerReference>
    {
        public LedgerReference(string value)
        {
            Value = value ?? string.Empty;
        }

        public static LedgerReference None => new LedgerReference(string.Empty);
        public string Value { get; }
        public bool IsAssigned => !string.IsNullOrWhiteSpace(Value);
        public bool Equals(LedgerReference other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is LedgerReference other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;
    }
}
