using System;
using System.Collections.Generic;
using DragonBound.Analytics;
using DragonBound.Core;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class AnalyticsRunSessionV2Tests
    {
        [Test]
        public void GameplayAndRuneAdaptersShareOneContinuousSequence()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var session = CreateSession(sink);
            var gameplay = new DrakeforgeAnalyticsAdapterV1(session);
            var runes = new RuneAnalyticsAdapterV2(session, AnalyticsSides.Player);

            Assert.IsTrue(gameplay.RecordRunStart(), gameplay.LastError);
            Assert.AreEqual(
                AnalyticsRecordResultV2.Accepted,
                runes.RecordLoadoutAssign(
                    new RuneLoadoutOperationObservationV2("assign-1", 0, "hero-1", "Power", true, string.Empty),
                    out var runeError),
                runeError);
            Assert.IsTrue(gameplay.RecordItemCommand(TeamSide.Player, 1, "item-1", "activate"));

            Assert.AreEqual(3, sink.Events.Count);
            Assert.AreEqual(1, sink.Events[0].sequence);
            Assert.AreEqual(2, sink.Events[1].sequence);
            Assert.AreEqual(3, sink.Events[2].sequence);
            Assert.AreEqual(4, session.NextSequence);
            Assert.AreEqual(AnalyticsBuildLanes.Development, sink.Events[0].build_lane);
        }

        [Test]
        public void BufferedSinkPreservesEventUntilTransportBecomesAvailable()
        {
            var downstream = new SwitchableSink();
            var buffered = new BufferedAnalyticsSinkV2(downstream, 2);
            var session = CreateSession(buffered);

            Assert.IsTrue(session.TryRecord(
                AnalyticsEventNamesV2.RunStart,
                AnalyticsSides.System,
                0,
                null,
                null,
                out var error), error);
            Assert.AreEqual(1, buffered.PendingCount);
            Assert.AreEqual(0, downstream.Events.Count);

            downstream.IsAvailable = true;
            buffered.Flush();

            Assert.AreEqual(0, buffered.PendingCount);
            Assert.AreEqual(1, downstream.Events.Count);
        }

        [Test]
        public void UnknownOrDeniedConsentNeitherRecordsNorBuffersEvents()
        {
            var consentGranted = false;
            var downstream = new SwitchableSink { IsAvailable = false };
            var buffered = new BufferedAnalyticsSinkV2(
                downstream,
                4,
                () => consentGranted);
            var session = CreateSession(buffered);

            Assert.IsFalse(session.TryRecord(
                AnalyticsEventNamesV2.RunStart,
                AnalyticsSides.System,
                0,
                "run:start",
                null,
                out _));
            Assert.AreEqual(0, buffered.PendingCount);
            Assert.AreEqual(1, session.NextSequence);

            consentGranted = true;
            Assert.IsTrue(session.TryRecord(
                AnalyticsEventNamesV2.RunStart,
                AnalyticsSides.System,
                0,
                "run:start",
                null,
                out var error), error);
            Assert.AreEqual(1, buffered.PendingCount);
        }

        [Test]
        public void ConsentWithdrawalClearsPendingAndStopsFutureCollection()
        {
            var consentGranted = true;
            var downstream = new SwitchableSink { IsAvailable = false };
            var buffered = new BufferedAnalyticsSinkV2(
                downstream,
                4,
                () => consentGranted);
            var session = CreateSession(buffered);

            Assert.IsTrue(session.TryRecord(
                AnalyticsEventNamesV2.RunStart,
                AnalyticsSides.System,
                0,
                "run:start",
                null,
                out var error), error);
            Assert.AreEqual(1, buffered.PendingCount);

            consentGranted = false;
            session.ClearPending();
            Assert.AreEqual(0, buffered.PendingCount);
            Assert.IsFalse(session.TryRecord(
                AnalyticsEventNamesV2.WaveStart,
                AnalyticsSides.System,
                1,
                "wave:1:start",
                null,
                out _));
            Assert.AreEqual(0, buffered.PendingCount);
            Assert.AreEqual(0, downstream.Events.Count);
        }

        [Test]
        public void FirebaseRuntimeRequiresExplicitGrantedConsent()
        {
            try
            {
                FirebaseAnalyticsRuntimeState.SetReady(true);
                FirebaseAnalyticsRuntimeState.SetCollectionEnabled(true);
                FirebaseAnalyticsRuntimeState.SetConsentState(AnalyticsConsentStateV2.Unknown);
                Assert.IsFalse(FirebaseAnalyticsRuntimeState.CanCollect);
                Assert.IsFalse(FirebaseAnalyticsRuntimeState.CanRecord);

                FirebaseAnalyticsRuntimeState.SetConsentState(AnalyticsConsentStateV2.Denied);
                Assert.IsFalse(FirebaseAnalyticsRuntimeState.CanCollect);

                FirebaseAnalyticsRuntimeState.SetConsentState(AnalyticsConsentStateV2.Granted);
                Assert.IsTrue(FirebaseAnalyticsRuntimeState.CanCollect);
                Assert.IsTrue(FirebaseAnalyticsRuntimeState.CanRecord);
            }
            finally
            {
                FirebaseAnalyticsRuntimeState.SetReady(false);
                FirebaseAnalyticsRuntimeState.SetCollectionEnabled(false);
                FirebaseAnalyticsRuntimeState.SetConsentState(AnalyticsConsentStateV2.Unknown);
            }
        }

        [Test]
        public void BufferedSinkDropsNewestEventWhenCapacityIsReached()
        {
            var downstream = new SwitchableSink { IsAvailable = false };
            var buffered = new BufferedAnalyticsSinkV2(downstream, capacity: 2);
            var session = CreateSession(buffered);

            Assert.IsTrue(session.TryRecord(
                AnalyticsEventNamesV2.RunStart, AnalyticsSides.System, 0, "run:start", null, out _));
            Assert.IsTrue(session.TryRecord(
                AnalyticsEventNamesV2.WaveStart, AnalyticsSides.System, 1, "wave:1:start", null, out _));
            Assert.IsFalse(session.TryRecord(
                AnalyticsEventNamesV2.WaveFinish,
                AnalyticsSides.System,
                1,
                "wave:1:finish",
                value => value.elapsed_seconds = 10f,
                out _));

            Assert.AreEqual(2, buffered.PendingCount);
            Assert.AreEqual(1, buffered.DroppedCount);
            Assert.AreEqual(3, session.NextSequence);
        }

        [Test]
        public void PermanentlyFailingTransportRetriesOnlyThreeTimesThenDrops()
        {
            var downstream = new FlakySink(int.MaxValue);
            var buffered = new BufferedAnalyticsSinkV2(downstream, maxAttempts: 3);
            var session = CreateSession(buffered);

            Assert.IsTrue(session.TryRecord(
                AnalyticsEventNamesV2.RunStart, AnalyticsSides.System, 0, "run:start", null, out _));
            Assert.AreEqual(1, buffered.PendingCount);
            buffered.Flush();
            Assert.AreEqual(1, buffered.PendingCount);
            buffered.Flush();

            Assert.AreEqual(0, buffered.PendingCount);
            Assert.AreEqual(3, buffered.FailedAttemptCount);
            Assert.AreEqual(1, buffered.DroppedCount);
            Assert.AreEqual(3, downstream.RecordCalls);
        }

        [Test]
        public void TransientTransportFailurePreservesOrderAndEventuallyRecovers()
        {
            var downstream = new FlakySink(2);
            var buffered = new BufferedAnalyticsSinkV2(downstream, maxAttempts: 3);
            var session = CreateSession(buffered);

            Assert.IsTrue(session.TryRecord(
                AnalyticsEventNamesV2.RunStart, AnalyticsSides.System, 0, "run:start", null, out _));
            Assert.IsTrue(session.TryRecord(
                AnalyticsEventNamesV2.WaveStart, AnalyticsSides.System, 1, "wave:1:start", null, out _));
            buffered.Flush();

            Assert.AreEqual(0, buffered.PendingCount);
            Assert.AreEqual(0, buffered.DroppedCount);
            Assert.AreEqual(2, downstream.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.RunStart, downstream.Events[0].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.WaveStart, downstream.Events[1].event_name);
        }

        [Test]
        public void ThrowingSinkCannotEscapeIntoGameplaySession()
        {
            var session = CreateSession(new ThrowingSink());

            Assert.DoesNotThrow(() =>
            {
                Assert.IsFalse(session.TryRecord(
                    AnalyticsEventNamesV2.RunStart,
                    AnalyticsSides.System,
                    0,
                    "run:start",
                    null,
                    out var error));
                StringAssert.Contains("analytics sink threw", error);
            });
            Assert.DoesNotThrow(() => session.Flush());
            Assert.AreEqual(1, session.NextSequence);
        }

        [Test]
        public void FirebaseParametersContainContractAndBuildLane()
        {
            var value = AnalyticsEventV2Factory.Create(
                AnalyticsEventNamesV2.RunStart,
                "event-1",
                "run-1",
                99,
                AnalyticsExecutionContexts.LivePlayerVsAi,
                AnalyticsSides.System,
                0,
                AnalyticsRankTiers.Bronze,
                AnalyticsAiDifficulties.Easy,
                1,
                "rules-v1",
                "1.0.0",
                DateTime.UtcNow,
                AnalyticsBuildLanes.Qa);

            var parameters = FirebaseAnalyticsParameterBuilderV2.Build(value);

            Assert.That(parameters, Has.Some.Matches<FirebaseAnalyticsParameterV2>(
                parameter => parameter.Name == "event_version" && parameter.IntegerValue == 2));
            Assert.That(parameters, Has.Some.Matches<FirebaseAnalyticsParameterV2>(
                parameter => parameter.Name == "build_lane" && parameter.StringValue == AnalyticsBuildLanes.Qa));
        }

        [Test]
        public void FirebaseRuneEquipContainsLockedSnapshotReason()
        {
            var value = AnalyticsEventV2Factory.Create(
                AnalyticsEventNamesV2.RuneEquip,
                "event-rune-1",
                "run-1",
                99,
                AnalyticsExecutionContexts.LivePlayerVsAi,
                AnalyticsSides.Player,
                0,
                AnalyticsRankTiers.Bronze,
                AnalyticsAiDifficulties.Easy,
                1,
                "rules-v1",
                "1.0.0",
                DateTime.UtcNow);
            value.hero_id = "hero-1";
            value.rune_id = "Power";
            value.snapshot_reason = "run_start";
            value.result = "loadout_snapshot_locked";

            var parameters = FirebaseAnalyticsParameterBuilderV2.Build(value);

            Assert.That(parameters, Has.Some.Matches<FirebaseAnalyticsParameterV2>(
                parameter => parameter.Name == "snapshot_reason" && parameter.StringValue == "run_start"));
        }

        [Test]
        public void OptInAdObserverRecordsOneRequestAndOnePlaybackResultPerAttempt()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var observer = new AnalyticsSessionAdObserverV2(CreateSession(sink));

            Assert.IsTrue(observer.RecordRequest("attempt-1", "energy_reward"), observer.LastError);
            Assert.IsTrue(observer.RecordResult(
                "attempt-1", "energy_reward", "completed", string.Empty), observer.LastError);

            Assert.AreEqual(2, sink.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.AdRequest, sink.Events[0].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.AdResult, sink.Events[1].event_name);
            Assert.AreEqual("energy_reward", sink.Events[0].ad_point_id);
            Assert.AreEqual("completed", sink.Events[1].result);
            Assert.IsFalse(observer.RecordResult(
                "attempt-1", "energy_reward", "completed", string.Empty));
            Assert.AreEqual(2, sink.Events.Count);
        }

        [Test]
        public void AdObserverRecordsCancellationWithoutCreatingRewardOrLedgerEvents()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var observer = new AnalyticsSessionAdObserverV2(CreateSession(sink));

            Assert.IsTrue(observer.RecordRequest("attempt-2", "merchant_reward"), observer.LastError);
            Assert.IsTrue(observer.RecordResult(
                "attempt-2", "merchant_reward", "cancelled", "operation_cancelled"), observer.LastError);

            Assert.AreEqual(2, sink.Events.Count);
            Assert.AreEqual("cancelled", sink.Events[1].result);
            Assert.AreEqual("operation_cancelled", sink.Events[1].reason);
            Assert.That(sink.Events, Has.None.Matches<AnalyticsEventV2>(
                value => value.event_name == AnalyticsEventNamesV2.EnergyGrant ||
                         value.event_name == AnalyticsEventNamesV2.LedgerResult));
        }

        [Test]
        public void DefaultAdObserverIsNoOpUntilProductionSdkOptsIn()
        {
            Assert.IsFalse(NoOpAdAnalyticsObserverV2.Instance.RecordRequest(
                "mock-attempt", "energy_reward"));
            Assert.IsFalse(NoOpAdAnalyticsObserverV2.Instance.RecordResult(
                "mock-attempt", "energy_reward", "completed", string.Empty));
        }

        [Test]
        public void RegistryFallsBackToApplicationSessionOutsideGameplay()
        {
            var applicationSession = CreateSession(new InMemoryAnalyticsSinkV2());
            var gameplaySession = CreateSession(new InMemoryAnalyticsSinkV2());
            try
            {
                AnalyticsRuntimeRegistryV2.SetApplicationSession(applicationSession);
                Assert.AreSame(applicationSession, AnalyticsRuntimeRegistryV2.Active);

                AnalyticsRuntimeRegistryV2.SetCurrent(gameplaySession);
                Assert.AreSame(gameplaySession, AnalyticsRuntimeRegistryV2.Active);

                AnalyticsRuntimeRegistryV2.Clear(gameplaySession);
                Assert.AreSame(applicationSession, AnalyticsRuntimeRegistryV2.Active);
            }
            finally
            {
                AnalyticsRuntimeRegistryV2.Clear(gameplaySession);
                AnalyticsRuntimeRegistryV2.ClearApplicationSession(applicationSession);
            }
        }

        [Test]
        public void LongDedupeKeyProducesFirebaseSafeDeterministicEventId()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var session = CreateSession(sink);

            var result = session.Record(
                AnalyticsEventNamesV2.RunStart,
                AnalyticsSides.System,
                0,
                new string('x', 140),
                null,
                out var error);

            Assert.AreEqual(AnalyticsRecordResultV2.Accepted, result, error);
            Assert.AreEqual(1, sink.Events.Count);
            StringAssert.StartsWith("sha256:", sink.Events[0].event_id);
            Assert.LessOrEqual(sink.Events[0].event_id.Length, 100);
        }

        private static AnalyticsRunSessionV2 CreateSession(IAnalyticsSinkV2 sink)
        {
            var context = new DrakeforgeAnalyticsRunContext(
                "run-1",
                99,
                AnalyticsExecutionContexts.LivePlayerVsAi,
                "rules-v1",
                "1.0.0",
                AnalyticsRankTiers.Bronze,
                AnalyticsAiDifficulties.Easy);
            return new AnalyticsRunSessionV2(
                new AnalyticsRecorderV2(sink),
                context,
                () => new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc));
        }

        private sealed class SwitchableSink : IAnalyticsSinkV2, IAnalyticsSinkAvailabilityV2
        {
            public readonly List<AnalyticsEventV2> Events = new List<AnalyticsEventV2>();

            public bool IsAvailable { get; set; }
            public int WriteErrorCount { get; private set; }

            public bool Record(AnalyticsEventV2 value)
            {
                if (!IsAvailable || value == null)
                {
                    return false;
                }

                Events.Add(value.Clone());
                return true;
            }

            public void Flush()
            {
            }
        }

        private sealed class FlakySink : IAnalyticsSinkV2, IAnalyticsSinkAvailabilityV2
        {
            private readonly int failuresBeforeSuccess;

            public FlakySink(int failuresBeforeSuccess)
            {
                this.failuresBeforeSuccess = failuresBeforeSuccess;
            }

            public readonly List<AnalyticsEventV2> Events = new List<AnalyticsEventV2>();
            public bool IsAvailable => true;
            public int WriteErrorCount { get; private set; }
            public int RecordCalls { get; private set; }

            public bool Record(AnalyticsEventV2 value)
            {
                RecordCalls++;
                if (RecordCalls <= failuresBeforeSuccess)
                {
                    WriteErrorCount++;
                    return false;
                }

                Events.Add(value.Clone());
                return true;
            }

            public void Flush() { }
        }

        private sealed class ThrowingSink : IAnalyticsSinkV2
        {
            public int WriteErrorCount => 0;
            public bool Record(AnalyticsEventV2 value) => throw new InvalidOperationException("transport failed");
            public void Flush() => throw new InvalidOperationException("flush failed");
        }
    }

    public sealed class AuthoritativeServiceAnalyticsAdapterV2Tests
    {
        [Test]
        public void EnergyResponseRecordsEconomicResultAndLedgerAsOneOrderedPair()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = CreateAdapter(sink);
            var ledger = Ledger("energy_spend", AnalyticsLedgerStatuses.Accepted);

            Assert.IsTrue(adapter.RecordEnergyResult(
                "energy-run-1", 0, 5, false, "run_entry", ledger), adapter.LastError);
            Assert.AreEqual(2, sink.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.EnergySpend, sink.Events[0].event_name);
            Assert.AreEqual(5, sink.Events[0].energy_amount);
            Assert.AreEqual(AnalyticsEventNamesV2.LedgerResult, sink.Events[1].event_name);
            Assert.AreEqual("energy_spend", sink.Events[1].ledger_operation);
            Assert.AreEqual(1, sink.Events[0].sequence);
            Assert.AreEqual(2, sink.Events[1].sequence);

            Assert.IsFalse(adapter.RecordEnergyResult(
                "energy-run-1", 0, 5, false, "run_entry", ledger));
            Assert.AreEqual(2, sink.Events.Count);
        }

        [Test]
        public void RawTransactionReferenceIsRejectedBeforeAnyEventIsRecorded()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = CreateAdapter(sink);
            var unsafeLedger = new AuthoritativeLedgerResultV2(
                "settlement_gold",
                AnalyticsLedgerStatuses.Accepted,
                "raw-transaction-123456789",
                string.Empty,
                string.Empty);

            Assert.IsFalse(adapter.RecordSettlementGold(
                "settlement-1", 20, 100, "victory", unsafeLedger));
            StringAssert.Contains("sha256", adapter.LastError);
            Assert.AreEqual(0, sink.Events.Count);
            Assert.IsFalse(AnalyticsSchemaV2.IsHashedLedgerReference("bearer-token-raw"));
        }

        [Test]
        public void MerchantAndRankResponsesExposeOnlyStableFields()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = CreateAdapter(sink);

            Assert.IsTrue(adapter.RecordMerchantOpen(
                "merchant-open-1", "merchant-main", "opened", string.Empty), adapter.LastError);
            Assert.IsTrue(adapter.RecordMerchantOffer(
                "merchant-offer-1", "merchant-main", "offer-7", "gold"), adapter.LastError);
            Assert.IsTrue(adapter.RecordMerchantPurchase(
                "merchant-buy-1", "merchant-main", "offer-7", "gold", "accepted", string.Empty,
                Ledger("merchant_purchase", AnalyticsLedgerStatuses.Accepted)), adapter.LastError);
            Assert.IsTrue(adapter.RecordRankSnapshot(
                "rank-fetch-1", 120, "match_start"), adapter.LastError);
            Assert.IsTrue(adapter.RecordRankChange(
                "rank-result-1", 124, "victory"), adapter.LastError);
            Assert.IsTrue(adapter.RecordLeaderboardSnapshot(
                "leaderboard-week-1", "weekly", 17), adapter.LastError);

            Assert.AreEqual(7, sink.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.MerchantOpen, sink.Events[0].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.MerchantOffer, sink.Events[1].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.MerchantPurchase, sink.Events[2].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.LedgerResult, sink.Events[3].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.RankSnapshot, sink.Events[4].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.RankChange, sink.Events[5].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.LeaderboardSnapshot, sink.Events[6].event_name);
            Assert.AreEqual("weekly", sink.Events[6].leaderboard_period);
            Assert.AreEqual(17, sink.Events[6].rank_value);
        }

        [Test]
        public void NoOpObserverDoesNotCreateLocalAuthority()
        {
            Assert.IsFalse(NoOpAuthoritativeServiceAnalyticsObserverV2.Instance.RecordRankChange(
                "local-rank", 10, "local_fixture"));
        }

        private static AuthoritativeLedgerResultV2 Ledger(string operation, string status)
        {
            return new AuthoritativeLedgerResultV2(
                operation,
                status,
                string.Empty,
                "sha256:abcdef1234567890abcdef1234567890",
                string.Empty);
        }

        private static AuthoritativeServiceAnalyticsAdapterV2 CreateAdapter(InMemoryAnalyticsSinkV2 sink)
        {
            var context = new DrakeforgeAnalyticsRunContext(
                "service-run-1",
                77,
                AnalyticsExecutionContexts.LivePlayerVsAi,
                "rules-v1",
                "1.0.0",
                AnalyticsRankTiers.Unranked,
                AnalyticsAiDifficulties.Standard);
            var session = new AnalyticsRunSessionV2(
                new AnalyticsRecorderV2(sink),
                context,
                () => new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc));
            return new AuthoritativeServiceAnalyticsAdapterV2(session);
        }
    }

    public sealed class AnalyticsReleaseReadinessValidatorV2Tests
    {
        [Test]
        public void ValidAndroidReleaseConfigurationPasses()
        {
            var report = AnalyticsReleaseReadinessValidatorV2.Validate(ValidConfiguration());

            Assert.IsTrue(report.IsReady);
            Assert.AreEqual(0, report.Issues.Count);
        }

        [Test]
        public void ArmV7AlongsideArm64IsWarningNotBlocker()
        {
            var configuration = ValidConfiguration();
            configuration.IncludesArmV7 = true;

            var report = AnalyticsReleaseReadinessValidatorV2.Validate(configuration);

            Assert.IsTrue(report.IsReady);
            Assert.IsTrue(HasIssue(
                report,
                "android_armv7",
                AnalyticsReleaseIssueSeverityV2.Warning));
        }

        [Test]
        public void FirebaseProjectAndPackageMismatchBlockRelease()
        {
            var configuration = ValidConfiguration();
            configuration.FirebaseProjectId = "another-project";
            configuration.FirebaseAndroidPackageName = "com.example.wrong";

            var report = AnalyticsReleaseReadinessValidatorV2.Validate(configuration);

            Assert.IsFalse(report.IsReady);
            Assert.IsTrue(HasIssue(
                report,
                "firebase_project",
                AnalyticsReleaseIssueSeverityV2.Error));
            Assert.IsTrue(HasIssue(
                report,
                "firebase_package",
                AnalyticsReleaseIssueSeverityV2.Error));
        }

        [Test]
        public void ReleaseRequiresIl2CppArm64ConsentAndKnownLane()
        {
            var configuration = ValidConfiguration();
            configuration.UsesIl2Cpp = false;
            configuration.IncludesArm64 = false;
            configuration.ExplicitConsentRequired = false;
            configuration.BuildLane = "unknown";

            var report = AnalyticsReleaseReadinessValidatorV2.Validate(configuration);

            Assert.IsFalse(report.IsReady);
            Assert.IsTrue(HasIssue(report, "android_il2cpp", AnalyticsReleaseIssueSeverityV2.Error));
            Assert.IsTrue(HasIssue(report, "android_arm64", AnalyticsReleaseIssueSeverityV2.Error));
            Assert.IsTrue(HasIssue(report, "analytics_consent", AnalyticsReleaseIssueSeverityV2.Error));
            Assert.IsTrue(HasIssue(report, "build_lane", AnalyticsReleaseIssueSeverityV2.Error));
        }

        [Test]
        public void StartupSceneMustContainAnalyticsBootstrap()
        {
            var configuration = ValidConfiguration();
            configuration.AnalyticsBootstrapInStartupScene = false;

            var report = AnalyticsReleaseReadinessValidatorV2.Validate(configuration);

            Assert.IsFalse(report.IsReady);
            Assert.IsTrue(HasIssue(report, "analytics_bootstrap", AnalyticsReleaseIssueSeverityV2.Error));
        }

        [Test]
        public void StartupSceneRequiresEnabledEventSystem()
        {
            var configuration = ValidConfiguration();
            configuration.StartupEventSystemEnabled = false;

            var report = AnalyticsReleaseReadinessValidatorV2.Validate(configuration);

            Assert.IsFalse(report.IsReady);
            Assert.IsTrue(HasIssue(report, "startup_event_system", AnalyticsReleaseIssueSeverityV2.Error));
        }

        [Test]
        public void AnalyticsBootstrapRequiresDedicatedRootHost()
        {
            var configuration = ValidConfiguration();
            configuration.AnalyticsBootstrapIsDedicatedRoot = false;

            var report = AnalyticsReleaseReadinessValidatorV2.Validate(configuration);

            Assert.IsFalse(report.IsReady);
            Assert.IsTrue(HasIssue(
                report,
                "analytics_bootstrap_host",
                AnalyticsReleaseIssueSeverityV2.Error));
        }

        [Test]
        public void DesktopAndAndroidFirebaseProjectsMustMatch()
        {
            var configuration = ValidConfiguration();
            configuration.FirebaseDesktopProjectId = "another-project";

            var report = AnalyticsReleaseReadinessValidatorV2.Validate(configuration);

            Assert.IsFalse(report.IsReady);
            Assert.IsTrue(HasIssue(report, "firebase_desktop_project", AnalyticsReleaseIssueSeverityV2.Error));
        }

        [Test]
        public void ReleaseRequiresNativeDefaultOffAndConsentUi()
        {
            var configuration = ValidConfiguration();
            configuration.NativeCollectionDisabledByDefault = false;
            configuration.ConsentUiAvailable = false;

            var report = AnalyticsReleaseReadinessValidatorV2.Validate(configuration);

            Assert.IsFalse(report.IsReady);
            Assert.IsTrue(HasIssue(report, "analytics_native_default", AnalyticsReleaseIssueSeverityV2.Error));
            Assert.IsTrue(HasIssue(report, "analytics_consent_ui", AnalyticsReleaseIssueSeverityV2.Error));
        }

        [TestCase(AnalyticsBuildLanes.Development)]
        [TestCase(AnalyticsBuildLanes.Qa)]
        [TestCase(AnalyticsBuildLanes.Staging)]
        [TestCase(AnalyticsBuildLanes.Production)]
        public void EveryExplicitBuildLaneIsAccepted(string buildLane)
        {
            var configuration = ValidConfiguration();
            configuration.BuildLane = buildLane;

            Assert.IsTrue(AnalyticsReleaseReadinessValidatorV2.Validate(configuration).IsReady);
        }

        private static AnalyticsReleaseConfigurationV2 ValidConfiguration()
        {
            return new AnalyticsReleaseConfigurationV2
            {
                AndroidPackageName = AnalyticsReleaseReadinessValidatorV2.RequiredAndroidPackageName,
                AndroidMinimumApiLevel = 24,
                UsesIl2Cpp = true,
                IncludesArm64 = true,
                IncludesArmV7 = false,
                BundleVersion = "1.0.0",
                BundleVersionCode = 1,
                GoogleServicesFilePresent = true,
                FirebaseAndroidPackageName = AnalyticsReleaseReadinessValidatorV2.RequiredAndroidPackageName,
                FirebaseProjectId = "drakeforge",
                FirebaseDesktopConfigPresent = true,
                FirebaseDesktopProjectId = "drakeforge",
                ExpectedFirebaseProjectId = "drakeforge",
                FirebaseSdkVersion = AnalyticsReleaseReadinessValidatorV2.RequiredFirebaseSdkVersion,
                StartupScenePath = "Assets/Scenes/Login.unity",
                AnalyticsBootstrapInStartupScene = true,
                AnalyticsBootstrapIsDedicatedRoot = true,
                StartupEventSystemEnabled = true,
                NativeCollectionDisabledByDefault = true,
                ConsentUiAvailable = true,
                BuildLane = AnalyticsBuildLanes.Production,
                ExplicitConsentRequired = true
            };
        }

        private static bool HasIssue(
            AnalyticsReleaseReadinessReportV2 report,
            string code,
            AnalyticsReleaseIssueSeverityV2 severity)
        {
            for (var index = 0; index < report.Issues.Count; index++)
            {
                var issue = report.Issues[index];
                if (issue.Code == code && issue.Severity == severity)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
