using DragonBound.Core;
using DragonBound.Items;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class ItemEconomyFlowEffectsTests
    {
        [Test]
        public void ForgeTreasury_GrantsThreeAfterTenExplicitLegalKillsOnly()
        {
            var team = new TeamState(TeamSide.Player);
            var effect = new ForgeTreasuryEffect();
            var context = new ItemRunContext(team, new EnemyRegistry());

            for (var i = 0; i < 9; i++)
            {
                effect.HandleCombatEvent(context, new ItemCombatEvent(
                    ItemCombatEventKind.EnemyKilled, TeamSide.Player, "basic-" + i,
                    isLegalKill: true, source: ItemCombatEventSource.Basic));
            }
            effect.HandleCombatEvent(context, new ItemCombatEvent(
                ItemCombatEventKind.EnemyKilled, TeamSide.Player, "item-kill",
                isLegalKill: false, source: ItemCombatEventSource.Item));

            Assert.AreEqual(9, effect.LegalKillCount);
            Assert.AreEqual(0, team.Resources);
            effect.HandleCombatEvent(context, new ItemCombatEvent(
                ItemCombatEventKind.EnemyKilled, TeamSide.Player, "hero-10",
                isLegalKill: true, source: ItemCombatEventSource.Hero));

            Assert.AreEqual(10, effect.LegalKillCount);
            Assert.AreEqual(3, team.Resources);
            Assert.AreEqual(1, effect.GrantedCount);
        }

        [Test]
        public void BattlefieldCommand_UsesOneFreeRecruitPortOnFirstHeroFormation()
        {
            var freeRecruit = new FreeRecruitPort();
            var effect = new BattlefieldCommandEffect();
            var context = new ItemRunContext(
                new TeamState(TeamSide.Player), new EnemyRegistry(), freeRecruit: freeRecruit);

            effect.HandleCombatEvent(context, new ItemCombatEvent(ItemCombatEventKind.HeroFormed, TeamSide.Player, "hero-1"));
            effect.HandleCombatEvent(context, new ItemCombatEvent(ItemCombatEventKind.HeroFormed, TeamSide.Player, "hero-2"));

            Assert.IsTrue(effect.Consumed);
            Assert.AreEqual(1, effect.AttemptCount);
            Assert.AreEqual(1, freeRecruit.GrantCount);
        }

        [Test]
        public void BattlefieldCommand_DoesNotPretendSuccessWithoutIntegrationPort()
        {
            var effect = new BattlefieldCommandEffect();
            var context = new ItemRunContext(new TeamState(TeamSide.Player), new EnemyRegistry());

            effect.HandleCombatEvent(context, new ItemCombatEvent(ItemCombatEventKind.HeroFormed, TeamSide.Player));

            Assert.IsFalse(effect.Consumed);
            Assert.AreEqual(1, effect.AttemptCount);
        }

        [Test]
        public void ForgekeepersGift_RequestsAtNinetySecondIntervalsAndStopsOnNoLockedCell()
        {
            var forgePick = new ForgePickPort();
            var runtime = new ItemRunRuntime(
                CreateForgekeepersSnapshot(),
                new TeamState(TeamSide.Player), new EnemyRegistry(),
                forgePick: forgePick);

            Assert.IsTrue(runtime.StartRun(out var reason), reason);
            runtime.Tick(89.9f);
            Assert.AreEqual(0, forgePick.RequestCount);
            runtime.Tick(0.1f);
            Assert.AreEqual(1, forgePick.RequestCount);
            Assert.AreEqual(1, forgePick.GrantedCount);
            runtime.Tick(90f);
            Assert.AreEqual(2, forgePick.RequestCount);
            Assert.IsTrue(forgePick.NoLockedCellReturned);
            runtime.Tick(90f);
            Assert.AreEqual(2, forgePick.RequestCount);
        }

        private static ItemRunSnapshot CreateForgekeepersSnapshot()
        {
            var profile = new ItemProfile();
            Assert.IsTrue(profile.RefreshDay(new FixedDayKey(), out _));
            Assert.IsTrue(profile.RefreshAuthoritativeAccountProgress(new FixedProgress(), out _));
            Assert.IsTrue(profile.Inventory.TryGrantOwned(ItemIds.ForgekeepersGift));
            Assert.IsTrue(profile.Loadout.TryEquip(ItemIds.ForgekeepersGift, profile.Inventory, out _));
            Assert.IsTrue(profile.TryCreateRunSnapshot(out var snapshot, out _));
            return snapshot;
        }

        private sealed class FreeRecruitPort : IItemFreeRecruitPort
        {
            public int GrantCount { get; private set; }
            public bool TryGrantFreeRecruit(out string reason)
            {
                reason = ItemOperationFailure.None;
                GrantCount++;
                return true;
            }
        }

        private sealed class ForgePickPort : IItemForgePickPort
        {
            public int RequestCount { get; private set; }
            public int GrantedCount { get; private set; }
            public bool NoLockedCellReturned { get; private set; }

            public ItemForgePickResult TryGrantForgePick(bool requiresAdvertisement)
            {
                RequestCount++;
                if (RequestCount == 1)
                {
                    GrantedCount++;
                    return new ItemForgePickResult(ItemForgePickResultKind.Granted);
                }

                NoLockedCellReturned = true;
                return new ItemForgePickResult(ItemForgePickResultKind.NoLockedCell);
            }
        }

        private sealed class FixedDayKey : IItemDayKeyProvider
        {
            public string GetDayKey() => "2026-08-18";
        }

        private sealed class FixedProgress : IItemAccountProgressProvider
        {
            public bool TryGetNormalCompletedMatchCount(out int completedMatchCount)
            {
                completedMatchCount = ItemProfile.UnlockCompletedMatchCount;
                return true;
            }
        }
    }
}
