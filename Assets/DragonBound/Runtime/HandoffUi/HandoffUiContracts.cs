using System;
using System.Collections.Generic;

namespace DragonBound.HandoffUi
{
    public enum ItemHudState { Locked, UnlockNotice, Empty, Available, Selected, Cooldown, Disabled }
    public enum MerchantOfferState { Normal, Ad, Unavailable, Claimed, Expired, Loading, Error }

    /// <summary>Immutable UI-only data. Gameplay services translate their state into these snapshots.</summary>
    public sealed class ItemHudSnapshot
    {
        public ItemHudSnapshot(ItemHudState state, string title, string detail, int cooldownSeconds = 0)
        {
            State = state; Title = title ?? string.Empty; Detail = detail ?? string.Empty; CooldownSeconds = cooldownSeconds;
        }
        public ItemHudState State { get; }
        public string Title { get; }
        public string Detail { get; }
        public int CooldownSeconds { get; }
    }

    public sealed class MerchantOfferSnapshot
    {
        public MerchantOfferSnapshot(string id, string title, string detail, MerchantOfferState state)
        {
            Id = id ?? string.Empty; Title = title ?? string.Empty; Detail = detail ?? string.Empty; State = state;
        }
        public string Id { get; }
        public string Title { get; }
        public string Detail { get; }
        public MerchantOfferState State { get; }
    }

    public sealed class MerchantSnapshot
    {
        public MerchantSnapshot(IReadOnlyList<MerchantOfferSnapshot> offers, string status)
        {
            Offers = offers ?? Array.Empty<MerchantOfferSnapshot>(); Status = status ?? string.Empty;
        }
        public IReadOnlyList<MerchantOfferSnapshot> Offers { get; }
        public string Status { get; }
    }

    /// <summary>Commands leave the view through events. This layer never mutates game state.</summary>
    public sealed class HandoffUiCommands
    {
        public event Action ItemRequested;
        public event Action<string> MerchantOfferRequested;
        public void RequestItem() => ItemRequested?.Invoke();
        public void RequestMerchantOffer(string offerId) => MerchantOfferRequested?.Invoke(offerId);
    }
}
