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
    public sealed class BattlefieldCommandEffect : IItemEffectRuntime, IOneShotItemEffectState
    {
        public string ItemId => Items.ItemIds.BattlefieldCommand;
        public bool Consumed { get; private set; }
        public bool IsConsumed => Consumed;
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

    /// <summary>Ad-gated Shovel schedule. Cooldown restarts after each player decision.</summary>
    public sealed class ForgekeepersGiftEffect : IItemEffectRuntime
    {
        public const float FirstForgePickSeconds = 90f;
        public const float RepeatForgePickSeconds = 90f;

        private float cooldownRemainingSeconds = FirstForgePickSeconds;
        private bool awaitingDecision;

        public string ItemId => Items.ItemIds.ForgekeepersGift;
        public int AttemptCount { get; private set; }
        public int GrantedCount { get; private set; }
        public float CooldownRemainingSeconds => cooldownRemainingSeconds;
        public bool AwaitingDecision => awaitingDecision;

        public void SynchronizeCooldown(float remainingSeconds)
        {
            if (float.IsNaN(remainingSeconds) || float.IsInfinity(remainingSeconds)) return;
            cooldownRemainingSeconds = Math.Max(0f, remainingSeconds);
        }

        public void OnRunStart(ItemRunContext context)
        {
            cooldownRemainingSeconds = FirstForgePickSeconds;
            awaitingDecision = false;
        }

        public void Tick(ItemRunContext context, float deltaSeconds)
        {
            if (awaitingDecision || context.ForgePick == null || deltaSeconds <= 0f)
            {
                return;
            }

            cooldownRemainingSeconds = Math.Max(0f, cooldownRemainingSeconds - deltaSeconds);
            if (cooldownRemainingSeconds > 0.0001f)
            {
                return;
            }

            awaitingDecision = true;
            var result = context.ForgePick.TryBeginForgePickClaim(HandleClaimResolved);
            if (result.Kind != ItemForgePickRequestKind.PromptOpened)
            {
                awaitingDecision = false;
            }
            if (result.Kind == ItemForgePickRequestKind.PromptOpened)
            {
                AttemptCount++;
            }
        }

        private void HandleClaimResolved(ItemForgePickClaimResult result)
        {
            if (!awaitingDecision)
            {
                return;
            }

            awaitingDecision = false;
            if (result.Granted) GrantedCount++;
            cooldownRemainingSeconds = RepeatForgePickSeconds;
        }

        public bool TryActivate(ItemRunContext context, out string reason)
        {
            reason = "PassiveOnly";
            return false;
        }

        public void HandleCombatEvent(ItemRunContext context, ItemCombatEvent combatEvent) { }
    }
}
