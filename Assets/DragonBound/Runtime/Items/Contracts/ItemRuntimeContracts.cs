using System;
using System.Collections.Generic;
using DragonBound.Foundation.Contracts;

namespace DragonBound.Items.Contracts
{
    public enum ItemSnapshotState
    {
        Ready,
        Pending,
        NotConfigured
    }

    public readonly struct ItemSnapshot : IEquatable<ItemSnapshot>
    {
        public ItemSnapshot(
            RunId runId,
            IReadOnlyList<ItemId> activeItems,
            IReadOnlyList<ItemId> passiveItems,
            ItemSnapshotState state = ItemSnapshotState.Ready)
        {
            RunId = runId;
            ActiveItems = Copy(activeItems);
            PassiveItems = Copy(passiveItems);
            State = state;
        }

        public static ItemSnapshot Empty => new ItemSnapshot(
            new RunId(0),
            new ItemId[0],
            new ItemId[0],
            ItemSnapshotState.NotConfigured);

        public RunId RunId { get; }
        public IReadOnlyList<ItemId> ActiveItems { get; }
        public IReadOnlyList<ItemId> PassiveItems { get; }
        public ItemSnapshotState State { get; }
        public bool IsReady => State == ItemSnapshotState.Ready;

        public bool Equals(ItemSnapshot other)
        {
            return RunId.Equals(other.RunId) && State == other.State &&
                   SequenceEquals(ActiveItems, other.ActiveItems) &&
                   SequenceEquals(PassiveItems, other.PassiveItems);
        }

        public override bool Equals(object obj) => obj is ItemSnapshot other && Equals(other);
        public override int GetHashCode() => RunId.GetHashCode() ^ (int)State;

        private static IReadOnlyList<ItemId> Copy(IReadOnlyList<ItemId> source)
        {
            return new List<ItemId>(source ?? new ItemId[0]).AsReadOnly();
        }

        private static bool SequenceEquals(IReadOnlyList<ItemId> left, IReadOnlyList<ItemId> right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Count != right.Count) return false;
            for (var i = 0; i < left.Count; i++)
            {
                if (left[i] != right[i]) return false;
            }

            return true;
        }
    }

    public enum ItemCommandKind
    {
        Activate,
        SelectMerchantOffer,
        ClaimAdReward
    }

    public readonly struct ItemCommand
    {
        private ItemCommand(
            ItemCommandKind kind,
            ItemId itemId,
            MerchantSelection merchantSelection,
            AdRewardClaim adRewardClaim)
        {
            Kind = kind;
            ItemId = itemId;
            MerchantSelection = merchantSelection;
            AdRewardClaim = adRewardClaim;
        }

        public static ItemCommand Activate(ItemId itemId) =>
            new ItemCommand(ItemCommandKind.Activate, itemId, MerchantSelection.Empty, AdRewardClaim.Empty);

        public static ItemCommand Select(MerchantSelection selection) =>
            new ItemCommand(ItemCommandKind.SelectMerchantOffer, default(ItemId), selection, AdRewardClaim.Empty);

        public static ItemCommand Claim(AdRewardClaim claim) =>
            new ItemCommand(ItemCommandKind.ClaimAdReward, default(ItemId), MerchantSelection.Empty, claim);

        public ItemCommandKind Kind { get; }
        public ItemId ItemId { get; }
        public MerchantSelection MerchantSelection { get; }
        public AdRewardClaim AdRewardClaim { get; }
    }

    public enum ItemResultState
    {
        Accepted,
        Rejected,
        Pending,
        NotConfigured
    }

    public readonly struct ItemResult
    {
        public ItemResult(
            ItemResultState state,
            ItemId itemId,
            string reasonCode = "",
            Cooldown cooldown = default(Cooldown),
            LedgerReference ledgerReference = default(LedgerReference))
        {
            State = state;
            ItemId = itemId;
            ReasonCode = reasonCode ?? string.Empty;
            Cooldown = cooldown;
            LedgerReference = ledgerReference;
        }

        public ItemResultState State { get; }
        public ItemId ItemId { get; }
        public string ReasonCode { get; }
        public Cooldown Cooldown { get; }
        public LedgerReference LedgerReference { get; }
        public bool Succeeded => State == ItemResultState.Accepted;
    }

    public readonly struct Cooldown : IEquatable<Cooldown>
    {
        public Cooldown(float durationSeconds, float remainingSeconds)
        {
            if (durationSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            if (remainingSeconds < 0f || remainingSeconds > durationSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(remainingSeconds));
            }

            DurationSeconds = durationSeconds;
            RemainingSeconds = remainingSeconds;
        }

        public static Cooldown Ready => new Cooldown(0f, 0f);
        public float DurationSeconds { get; }
        public float RemainingSeconds { get; }
        public bool IsReady => RemainingSeconds <= 0.0001f;
        public bool Equals(Cooldown other) =>
            Math.Abs(DurationSeconds - other.DurationSeconds) <= 0.0001f &&
            Math.Abs(RemainingSeconds - other.RemainingSeconds) <= 0.0001f;
        public override bool Equals(object obj) => obj is Cooldown other && Equals(other);
        public override int GetHashCode() => DurationSeconds.GetHashCode() ^ RemainingSeconds.GetHashCode();
    }
}
