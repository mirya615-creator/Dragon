using System;
using DragonBound.Analytics;
using DragonBound.Recruitment;

namespace DragonBound.Runes
{
    /// <summary>
    /// Progression comes from the account/backend boundary. Local profiles retain the last trusted
    /// value for offline display but never derive it from mutable device time.
    /// </summary>
    public interface IRuneProgressionProvider
    {
        int AccountDay { get; }
    }

    public sealed class RuneProfileProgressionProvider : IRuneProgressionProvider
    {
        private readonly RuneSaveData profile;

        public RuneProfileProgressionProvider(RuneSaveData profile)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public int AccountDay => Math.Max(1, profile.AccountDay);
    }

    public sealed class RuneFeatureGate
    {
        public const int UnlockAccountDay = 3;

        private readonly IRuneProgressionProvider progression;

        public RuneFeatureGate(IRuneProgressionProvider progression)
        {
            this.progression = progression ?? throw new ArgumentNullException(nameof(progression));
        }

        public int AccountDay => progression.AccountDay;
        public bool IsUnlocked => AccountDay >= UnlockAccountDay;

        public bool TryAuthorize(out string reason)
        {
            if (IsUnlocked)
            {
                reason = string.Empty;
                return true;
            }

            reason = "RuneSystemLockedUntilDay3";
            return false;
        }
    }

    /// <summary>
    /// Product-facing mutation boundary. UI, wave rewards and future deep links must use this
    /// service so Day 3, copy ownership and the run-start snapshot remain enforceable.
    /// </summary>
    public sealed class RuneLoadoutService
    {
        private readonly RuneSaveData profile;
        private readonly RuneFeatureGate gate;
        private readonly Func<bool> persist;
        private readonly RuneLoadoutAnalyticsBridge analyticsBridge;

        public RuneLoadoutService(
            RuneSaveData profile,
            RuneFeatureGate gate,
            Func<bool> persist = null,
            RuneAnalyticsAdapterV2 analytics = null)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
            this.persist = persist;
            analyticsBridge = analytics == null ? null : new RuneLoadoutAnalyticsBridge(analytics);
            if (profile.Inventory == null || profile.Loadout == null)
            {
                if (!profile.EnsureRuntimeState(out var error))
                {
                    throw new ArgumentException(error, nameof(profile));
                }
            }
        }

        public RuneSaveData Profile => profile;
        public RuneInventory Inventory => profile.Inventory;
        public HeroRuneLoadout Loadout => profile.Loadout;
        public RuneFeatureGate Gate => gate;

        public bool TryEquip(string heroId, string runeId, out string reason)
        {
            if (!TryAuthorizeLoadoutChange(out reason))
            {
                RecordAssign(heroId, runeId, false, reason);
                return false;
            }

            if (!IsKnownHero(heroId))
            {
                reason = "UnknownHeroId";
                RecordAssign(heroId, runeId, false, reason);
                return false;
            }

            if (RuneCatalog.Get(runeId) == null)
            {
                reason = "UnknownRuneId";
                RecordAssign(heroId, runeId, false, reason);
                return false;
            }

            if (!Loadout.Assign(heroId, runeId, Inventory))
            {
                reason = Inventory.OwnedCount(runeId) <= Loadout.AssignedCopies(runeId, heroId)
                    ? "InsufficientOwnedCopies"
                    : "LoadoutRejected";
                RecordAssign(heroId, runeId, false, reason);
                return false;
            }

            var accepted = Persist(out reason);
            RecordAssign(heroId, runeId, accepted, reason);
            return accepted;
        }

        public bool TryUnequip(string heroId, out string reason)
        {
            if (!TryAuthorizeLoadoutChange(out reason))
            {
                RecordUnequip(heroId, false, reason);
                return false;
            }

            if (!Loadout.Unassign(heroId))
            {
                reason = "HeroHasNoEquippedRune";
                RecordUnequip(heroId, false, reason);
                return false;
            }

            var accepted = Persist(out reason);
            RecordUnequip(heroId, accepted, reason);
            return accepted;
        }

        public bool TryCraft(string runeId, out string reason)
        {
            if (!gate.TryAuthorize(out reason))
            {
                RecordCraft(runeId, false, reason);
                return false;
            }

            if (RuneCatalog.Get(runeId) == null)
            {
                reason = "UnknownRuneId";
                RecordCraft(runeId, false, reason);
                return false;
            }

            if (!Inventory.CraftRune(runeId))
            {
                reason = "InsufficientFragments";
                RecordCraft(runeId, false, reason);
                return false;
            }

            var accepted = Persist(out reason);
            RecordCraft(runeId, accepted, reason);
            return accepted;
        }

        public bool TryGrantReward(RuneReward reward, out string reason)
        {
            if (!gate.TryAuthorize(out reason))
            {
                return false;
            }

            RuneDropRules.GrantToInventory(reward, Inventory);
            if (reward == null)
            {
                reason = string.Empty;
                return true;
            }

            return Persist(out reason);
        }

        public bool LockForRunStart(out string reason)
        {
            if (!gate.IsUnlocked)
            {
                Loadout.LockEmptyAtRunStart();
                reason = string.Empty;
                return true;
            }

            if (!Loadout.LockAtRunStart(Inventory))
            {
                reason = "InvalidLoadout";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool TryAuthorizeLoadoutChange(out string reason)
        {
            if (!gate.TryAuthorize(out reason))
            {
                return false;
            }

            if (Loadout.IsLocked)
            {
                reason = "RunInProgress";
                return false;
            }

            return true;
        }

        private bool Persist(out string reason)
        {
            if (persist == null || persist())
            {
                reason = string.Empty;
                return true;
            }

            reason = "ProfileSaveFailed";
            return false;
        }

        private void RecordAssign(string heroId, string runeId, bool accepted, string reason)
        {
            analyticsBridge?.RecordAssign(heroId, runeId, accepted, reason);
            RecordGate("loadout_assign", reason);
        }

        private void RecordUnequip(string heroId, bool accepted, string reason)
        {
            analyticsBridge?.RecordUnequip(heroId, accepted, reason);
            RecordGate("loadout_unequip", reason);
        }

        private void RecordCraft(string runeId, bool accepted, string reason)
        {
            analyticsBridge?.RecordCraft(runeId, accepted, reason);
            RecordGate("craft", reason);
        }

        private void RecordGate(string operation, string reason)
        {
            if (analyticsBridge != null && reason == "RuneSystemLockedUntilDay3")
            {
                analyticsBridge.RecordGate(operation, gate.AccountDay, reason);
            }
        }

        private static bool IsKnownHero(string heroId)
        {
            if (string.IsNullOrWhiteSpace(heroId))
            {
                return false;
            }

            foreach (var hero in HeroDefinitionCatalog.Definitions)
            {
                if (string.Equals(hero.Id, heroId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
