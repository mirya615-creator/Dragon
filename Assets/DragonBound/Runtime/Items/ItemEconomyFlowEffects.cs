using System;

namespace DragonBound.Items
{
    /// <summary>V1 local run-resource effect. The port owns any authoritative economy boundary.</summary>
    public sealed class ForgeTreasuryEffect : IItemEffectRuntime
    {
        public const int LegalKillsPerGrant = 10;
        public const int RunResourcePerGrant = 3;

        public string ItemId => Items.ItemIds.ForgeTreasury;
        public int LegalKillCount { get; private set; }
        public int GrantedCount { get; private set; }
        public int RejectedCount { get; private set; }

        public void OnRunStart(ItemRunContext context) { }
        public void Tick(ItemRunContext context, float deltaSeconds) { }
        public bool TryActivate(ItemRunContext context, out string reason)
        {
            reason = "PassiveOnly";
            return false;
        }

        public void HandleCombatEvent(ItemRunContext context, ItemCombatEvent combatEvent)
        {
            if (combatEvent.Kind != ItemCombatEventKind.EnemyKilled || !combatEvent.IsLegalKill) return;
            LegalKillCount++;
            if (LegalKillCount % LegalKillsPerGrant != 0) return;

            if (context.RunResource.TryGrant(RunResourcePerGrant, out _)) GrantedCount++;
            else RejectedCount++;
        }
    }

    /// <summary>First-formation trigger. Integration supplies the real free Recruit transaction.</summary>
    public sealed class BattlefieldCommandEffect : IItemEffectRuntime
    {
        public string ItemId => Items.ItemIds.BattlefieldCommand;
        public bool Consumed { get; private set; }
        public int AttemptCount { get; private set; }

        public void OnRunStart(ItemRunContext context) { }
        public void Tick(ItemRunContext context, float deltaSeconds) { }
        public bool TryActivate(ItemRunContext context, out string reason)
        {
            reason = "PassiveOnly";
            return false;
        }

        public void HandleCombatEvent(ItemRunContext context, ItemCombatEvent combatEvent)
        {
            if (Consumed || combatEvent.Kind != ItemCombatEventKind.HeroFormed) return;
            AttemptCount++;
            if (context.FreeRecruit == null)
            {
                return;
            }

            if (context.FreeRecruit.TryGrantFreeRecruit(out _)) Consumed = true;
        }
    }

    /// <summary>Ad-gated Forge Pick schedule. The provider decides authority and locked-cell state.</summary>
    public sealed class ForgekeepersGiftEffect : IItemEffectRuntime
    {
        public const float FirstForgePickSeconds = 90f;
        public const float RepeatForgePickSeconds = 90f;

        private float nextDueSeconds = FirstForgePickSeconds;
        private bool stoppedForNoLockedCell;

        public string ItemId => Items.ItemIds.ForgekeepersGift;
        public int AttemptCount { get; private set; }
        public int GrantedCount { get; private set; }
        public bool StoppedForNoLockedCell => stoppedForNoLockedCell;

        public void OnRunStart(ItemRunContext context)
        {
            nextDueSeconds = FirstForgePickSeconds;
            stoppedForNoLockedCell = false;
        }

        public void Tick(ItemRunContext context, float deltaSeconds)
        {
            if (stoppedForNoLockedCell || context.ForgePick == null || deltaSeconds <= 0f) return;
            var target = context.ElapsedSeconds;
            while (target + 0.0001f >= nextDueSeconds)
            {
                AttemptCount++;
                var result = context.ForgePick.TryGrantForgePick(requiresAdvertisement: true);
                if (result.Granted) GrantedCount++;
                if (result.Kind == ItemForgePickResultKind.NoLockedCell)
                {
                    stoppedForNoLockedCell = true;
                    break;
                }

                nextDueSeconds += RepeatForgePickSeconds;
            }
        }

        public bool TryActivate(ItemRunContext context, out string reason)
        {
            reason = "PassiveOnly";
            return false;
        }

        public void HandleCombatEvent(ItemRunContext context, ItemCombatEvent combatEvent) { }
    }
}
