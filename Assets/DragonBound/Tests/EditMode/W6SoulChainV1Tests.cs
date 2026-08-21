using System;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Grid;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class W6SoulChainV1Tests
    {
        [Test]
        public void SameSeedSelectsTheSameTwoByTwoRegionAndAtMostTwoBasics()
        {
            var firstProvider = new TestTargetProvider();
            var secondProvider = new TestTargetProvider();
            var first = CreateController(firstProvider, 701);
            var second = CreateController(secondProvider, 701);

            first.Tick(8f);
            second.Tick(8f);
            Assert.AreEqual(first.SelectedRegionAnchor, second.SelectedRegionAnchor);

            first.Tick(0.5f);
            second.Tick(0.5f);
            Assert.LessOrEqual(first.LastAffectedCount, SoulchainBinderConfiguration.MaxAffectedBasic);
            Assert.AreEqual(first.LastAffectedCount, second.LastAffectedCount);
            Assert.AreEqual(firstProvider.DisabledCount, secondProvider.DisabledCount);
        }

        [Test]
        public void SoulChainOnlyDisablesBasicTargetsAndRestoresAfterTwoSeconds()
        {
            var provider = new TestTargetProvider();
            var controller = CreateController(provider, 702);

            controller.Tick(8.5f);
            Assert.GreaterOrEqual(provider.DisabledCount, 1);
            Assert.LessOrEqual(provider.DisabledCount, 2);
            Assert.IsTrue(provider.AllDisabledIdsAreBasic);

            controller.Tick(2f);
            Assert.AreEqual(0, provider.DisabledCount);
        }

        [Test]
        public void MergeInheritsTheLongestRemainingControlTime()
        {
            var provider = new TestTargetProvider();
            var controller = CreateController(provider, 703);
            controller.Tick(8.5f);

            var disabled = provider.FirstDisabledId;
            Assert.IsNotNull(disabled);
            controller.NotifyMerge(disabled, "fixture.basic.merge-target");

            Assert.IsFalse(provider.IsDisabled(disabled));
            Assert.IsTrue(provider.IsDisabled("fixture.basic.merge-target"));
            Assert.Greater(controller.GetRemainingControl("fixture.basic.merge-target"), 0f);
            controller.Tick(2f);
            Assert.IsFalse(provider.IsDisabled("fixture.basic.merge-target"));
        }

        [Test]
        public void BossDeathImmediatelyClearsActiveControl()
        {
            var provider = new TestTargetProvider();
            var boss = new EnemyRuntime("fixture.boss", TeamSide.Player, 500f, EnemyArchetype.Boss);
            var controller = new SoulChainController(boss, TeamSide.Player, provider, 704);
            controller.Tick(8.5f);
            Assert.Greater(provider.DisabledCount, 0);

            controller.NotifyBossDeath();
            Assert.AreEqual(0, provider.DisabledCount);
            Assert.IsFalse(controller.IsCasting);
        }

        [Test]
        public void EmptyRegionStillConsumesCastAndCooldown()
        {
            var provider = new TestTargetProvider();
            var controller = CreateController(provider, 705);
            controller.Tick(8f);
            provider.MoveAllOutsideSelectedRegion(controller.SelectedRegionAnchor);
            controller.Tick(0.5f);

            Assert.AreEqual(1, controller.CastsStarted);
            Assert.AreEqual(1, controller.CastsSucceeded);
            Assert.AreEqual(0, controller.LastAffectedCount);
            Assert.IsTrue(controller.IsEffectActive);
            controller.Tick(2f);
            Assert.Greater(controller.CooldownRemainingSeconds, 14.8f);
        }

        [Test]
        public void SuccessfulCastTimelineAllowsTheSecondCastAtTwentyFivePointFiveSeconds()
        {
            var provider = new TestTargetProvider();
            var controller = CreateController(provider, 706);
            var events = new List<SoulChainCastEvent>();
            controller.CastEvent += events.Add;

            for (var index = 0; index < 280; index++)
            {
                controller.Tick(0.1f);
            }

            AssertEventTime(events, SoulChainCastEventKind.CastStarted, 1, 8f);
            AssertEventTime(events, SoulChainCastEventKind.EffectApplied, 1, 8.5f);
            AssertEventTime(events, SoulChainCastEventKind.EffectEnded, 1, 10.5f);
            AssertEventTime(events, SoulChainCastEventKind.CastStarted, 2, 25.5f);
            AssertEventTime(events, SoulChainCastEventKind.EffectApplied, 2, 26f);
            AssertEventTime(events, SoulChainCastEventKind.EffectEnded, 2, 28f);
        }

        [Test]
        public void SpellbreakerFailureReflectsTenPercentAndStartsCooldownWithoutControl()
        {
            var provider = new TestTargetProvider();
            var boss = new EnemyRuntime("fixture.boss", TeamSide.Player, 500f, EnemyArchetype.Boss);
            var controller = new SoulChainController(
                boss,
                TeamSide.Player,
                provider,
                707,
                new BlockingSpellbreaker());
            var failed = new List<SoulChainCastEvent>();
            controller.CastEvent += value =>
            {
                if (value.Kind == SoulChainCastEventKind.CastFailed)
                {
                    failed.Add(value);
                }
            };

            controller.Tick(8.5f);
            Assert.AreEqual(1, controller.CastsFailed);
            Assert.AreEqual(450f, boss.HitPoints, 0.0001f);
            Assert.AreEqual(0, provider.DisabledCount);
            Assert.AreEqual(50f, failed[0].ReflectionDamage, 0.0001f);
            controller.Tick(15f);
            Assert.AreEqual(2, controller.CastsStarted);
        }

        [Test]
        public void DamageTelemetryBucketsConserveTotalAndRunSeedComparisonIsDeterministic()
        {
            var telemetry = new DamageCompositionTelemetry();
            for (var index = 0; index < 6; index++)
            {
                telemetry.RecordDamage((DamageCompositionSource)index, index + 1f, index % 2 == 0);
            }

            Assert.AreEqual(21f, telemetry.TotalDamage, 0.0001f);
            Assert.AreEqual(telemetry.TotalDamage, telemetry.SumSourceTotals(), 0.0001f);

            var first = W6SoulChainTelemetryRunner.RunOne(708, true);
            var second = W6SoulChainTelemetryRunner.RunOne(708, true);
            Assert.AreEqual(first.BossTtkSeconds, second.BossTtkSeconds, 0.0001f);
            Assert.AreEqual(first.SourceSum(), second.SourceSum(), 0.0001f);
            Assert.AreEqual(first.SourceSum(), first.BossDamage + first.NormalDamage, 0.02f);
        }

        [Test]
        public void W6BossUsesSoulchainBinderGreyboxSlotAndDoesNotIncreaseNormalCount()
        {
            var configuration = TwentyWavePressureConfiguration.CreateCoreLoopV2();
            var match = new MatchController(709);
            var runtime = new TwentyWavePressureRuntime(match, null, null, 709, configuration);
            Assert.IsTrue(runtime.StartRun());
            Assert.IsTrue(runtime.JumpToWave(6));

            Assert.IsNotNull(runtime.PlayerW6Boss);
            StringAssert.Contains(SoulchainBinderConfiguration.BossId.ToLowerInvariant(), runtime.PlayerW6Boss.RuntimeId);
            Assert.AreEqual(EnemyArchetype.Boss, runtime.PlayerW6Boss.Archetype);
            Assert.AreEqual(600f, runtime.PlayerW6Boss.MaxHitPoints, 0.0001f);
            Assert.AreEqual(1, runtime.PlayerSpawnedThisWave);
            Assert.IsTrue(runtime.PlayerEnemyRegistry.Count >= 2);
            Assert.AreEqual(16, configuration.GetWave(6).EnemyCountPerSide);
        }

        [Test]
        public void TargetSelectionPrefersPathProgressOverBossClass()
        {
            var targeting = new TargetingSystem();
            var normal = Target("normal", EnemyArchetype.Normal, 0.80f, 5);
            var boss = Target("boss", EnemyArchetype.Boss, 0.70f, 1);

            Assert.AreSame(normal, targeting.SelectFrontmostInRange(
                new CombatPoint(0f, 0f),
                5f,
                new[] { boss, normal }));
        }

        [Test]
        public void TargetSelectionUsesSpawnSequenceWhenProgressTies()
        {
            var targeting = new TargetingSystem();
            var later = Target("later", EnemyArchetype.Normal, 0.60f, 8);
            var earlier = Target("earlier", EnemyArchetype.Boss, 0.60f, 2);

            Assert.AreSame(earlier, targeting.SelectFrontmostInRange(
                new CombatPoint(0f, 0f),
                5f,
                new[] { later, earlier }));
        }

        [Test]
        public void FrozenSettlementDefinitionKeepsThreeHeartsAndBossGoalIsInstantDefeat()
        {
            var team = new TeamState(TeamSide.Player);
            Assert.AreEqual(3, team.HatchlingMaxHealth);
            Assert.AreEqual(3, team.HatchlingHealth);
            team.ApplyHatchlingDamage(BattleSettlementDefinition.NormalGoalDamage);
            Assert.AreEqual(2, team.HatchlingHealth);
            team.ApplyBossGoalInstantDefeat();
            Assert.IsTrue(team.IsInstantDefeated);
            Assert.AreEqual(0, team.HatchlingHealth);
            Assert.AreEqual(20, BattleSettlementDefinition.MaxScheduledWave);
            Assert.IsFalse(BattleSettlementDefinition.GenerateWaveAfterW20);
        }

        private static SoulChainController CreateController(TestTargetProvider provider, int seed)
        {
            return new SoulChainController(
                new EnemyRuntime("fixture.boss." + seed, TeamSide.Player, 500f, EnemyArchetype.Boss),
                TeamSide.Player,
                provider,
                seed);
        }

        private static EnemyRuntime Target(
            string runtimeId,
            EnemyArchetype archetype,
            float pathProgress,
            int spawnSequence)
        {
            var enemy = new EnemyRuntime(runtimeId, TeamSide.Player, 100f, archetype, spawnSequence);
            enemy.SetTargetingState(0, pathProgress, new CombatPoint(1f, 0f));
            return enemy;
        }

        private static void AssertEventTime(
            IReadOnlyList<SoulChainCastEvent> events,
            SoulChainCastEventKind kind,
            int castNumber,
            float expected)
        {
            for (var index = 0; index < events.Count; index++)
            {
                if (events[index].Kind == kind && events[index].CastNumber == castNumber)
                {
                    Assert.AreEqual(expected, events[index].ElapsedSeconds, 0.001f);
                    return;
                }
            }

            Assert.Fail("Missing event " + kind + " for cast " + castNumber);
        }

        private sealed class BlockingSpellbreaker : ISoulChainSpellbreakerResolver
        {
            public bool ShouldBlockCast(SoulChainBossCastContext context)
            {
                return true;
            }
        }

        private sealed class TestTargetProvider : ISoulChainTargetProvider
        {
            private readonly List<SoulChainBasicCandidate> candidates = new List<SoulChainBasicCandidate>
            {
                new SoulChainBasicCandidate("fixture.basic.01", new GridPosition(0, 0)),
                new SoulChainBasicCandidate("fixture.basic.02", new GridPosition(1, 0)),
                new SoulChainBasicCandidate("fixture.basic.03", new GridPosition(0, 1)),
                new SoulChainBasicCandidate("fixture.basic.04", new GridPosition(1, 1))
            };
            private readonly Dictionary<string, bool> disabled = new Dictionary<string, bool>(StringComparer.Ordinal);

            public TestTargetProvider()
            {
                foreach (var candidate in candidates)
                {
                    disabled[candidate.RuntimeId] = false;
                }
                disabled["fixture.basic.merge-target"] = false;
            }

            public string FirstDisabledId
            {
                get
                {
                    foreach (var entry in disabled)
                    {
                        if (entry.Value)
                        {
                            return entry.Key;
                        }
                    }

                    return null;
                }
            }

            public int DisabledCount
            {
                get
                {
                    var count = 0;
                    foreach (var entry in disabled)
                    {
                        if (entry.Value)
                        {
                            count++;
                        }
                    }

                    return count;
                }
            }

            public bool AllDisabledIdsAreBasic
            {
                get
                {
                    foreach (var entry in disabled)
                    {
                        if (entry.Value && !entry.Key.StartsWith("fixture.basic.", StringComparison.Ordinal))
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            public IReadOnlyList<SoulChainBasicCandidate> GetBasicCandidates()
            {
                return candidates;
            }

            public bool SetAttackDisabled(string runtimeId, bool value)
            {
                if (!disabled.ContainsKey(runtimeId))
                {
                    return false;
                }

                disabled[runtimeId] = value;
                return true;
            }

            public bool IsDisabled(string runtimeId)
            {
                return disabled.TryGetValue(runtimeId, out var value) && value;
            }

            public void MoveAllOutsideSelectedRegion(GridPosition anchor)
            {
                for (var index = 0; index < candidates.Count; index++)
                {
                    candidates[index] = new SoulChainBasicCandidate(
                        candidates[index].RuntimeId,
                        new GridPosition(anchor.X + 3 + index, anchor.Y + 3));
                }
            }
        }
    }
}
