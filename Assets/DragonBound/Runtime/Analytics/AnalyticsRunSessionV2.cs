using System;
using System.Security.Cryptography;
using System.Text;

namespace DragonBound.Analytics
{
    /// <summary>
    /// Owns the single event sequence for one gameplay run. Every analytics adapter for the run
    /// must share this instance; adapters must not allocate independent sequence counters.
    /// </summary>
    public sealed class AnalyticsRunSessionV2
    {
        private readonly object gate = new object();
        private readonly AnalyticsRecorderV2 recorder;
        private readonly DrakeforgeAnalyticsRunContext context;
        private readonly Func<DateTime> utcNow;
        private long nextSequence = 1;

        public AnalyticsRunSessionV2(
            AnalyticsRecorderV2 recorder,
            DrakeforgeAnalyticsRunContext context,
            Func<DateTime> utcNow = null)
        {
            this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public DrakeforgeAnalyticsRunContext Context => context;

        public long NextSequence
        {
            get
            {
                lock (gate)
                {
                    return nextSequence;
                }
            }
        }

        public AnalyticsRecordResultV2 Record(
            string eventName,
            string side,
            int wave,
            string dedupeKey,
            Action<AnalyticsEventV2> configure,
            out string error)
        {
            lock (gate)
            {
                var sequence = nextSequence;
                var rawEventId = string.IsNullOrWhiteSpace(dedupeKey)
                    ? context.RunId + ":analytics:" + sequence
                    : context.RunId + ":analytics:" + dedupeKey;
                var eventId = CreateFirebaseSafeEventId(rawEventId);
                var value = AnalyticsEventV2Factory.Create(
                    eventName,
                    eventId,
                    context.RunId,
                    context.RunSeed,
                    context.ExecutionContext,
                    side,
                    wave,
                    context.RankTier,
                    context.AiDifficulty,
                    sequence,
                    context.ConfigVersion,
                    context.BuildVersion,
                    utcNow(),
                    context.BuildLane);
                value.ai_profile = context.AiProfile;
                value.ai_algorithm_version = context.AiAlgorithmVersion;
                value.ai_decision_seed = context.AiDecisionSeed;
                value.ai_recovery_match = context.AiRecoveryMatch;
                value.player_rank_level = context.PlayerRankLevel;
                configure?.Invoke(value);

                var result = recorder.Record(value, out error);
                if (result == AnalyticsRecordResultV2.Accepted)
                {
                    nextSequence++;
                }

                return result;
            }
        }

        private static string CreateFirebaseSafeEventId(string value)
        {
            const int firebaseStringParameterLimit = 100;
            if (value.Length <= firebaseStringParameterLimit) return value;

            byte[] digest;
            using (var sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            }

            var builder = new StringBuilder("sha256:", 71);
            for (var index = 0; index < digest.Length; index++)
            {
                builder.Append(digest[index].ToString("x2"));
            }

            return builder.ToString();
        }

        public bool TryRecord(
            string eventName,
            string side,
            int wave,
            string dedupeKey,
            Action<AnalyticsEventV2> configure,
            out string error)
        {
            return Record(eventName, side, wave, dedupeKey, configure, out error) ==
                   AnalyticsRecordResultV2.Accepted;
        }

        public void Flush()
        {
            recorder.Flush();
        }

        public void ClearPending()
        {
            recorder.ClearPending();
        }
    }

    /// <summary>
    /// Playback-only observation boundary. A completed video is not proof of an economic grant;
    /// reward confirmation belongs to AuthoritativeServiceAnalyticsAdapterV2 plus ledger_result.
    /// </summary>
    public interface IAdAnalyticsObserverV2
    {
        bool RecordRequest(string attemptId, string placementId);
        bool RecordResult(string attemptId, string placementId, string result, string reason);
    }

    public sealed class NoOpAdAnalyticsObserverV2 : IAdAnalyticsObserverV2
    {
        public static NoOpAdAnalyticsObserverV2 Instance { get; } = new NoOpAdAnalyticsObserverV2();

        private NoOpAdAnalyticsObserverV2() { }

        public bool RecordRequest(string attemptId, string placementId) => false;
        public bool RecordResult(string attemptId, string placementId, string result, string reason) => false;
    }

    /// <summary>Opt-in Firebase/session observer for a real ad SDK integration.</summary>
    public sealed class AnalyticsSessionAdObserverV2 : IAdAnalyticsObserverV2
    {
        private readonly AnalyticsRunSessionV2 session;

        public AnalyticsSessionAdObserverV2(AnalyticsRunSessionV2 session)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public string LastError { get; private set; } = string.Empty;

        public bool RecordRequest(string attemptId, string placementId)
        {
            return Record(AnalyticsEventNamesV2.AdRequest, attemptId, placementId, string.Empty, string.Empty);
        }

        public bool RecordResult(string attemptId, string placementId, string result, string reason)
        {
            if (string.IsNullOrWhiteSpace(result))
            {
                LastError = "Ad playback result is required.";
                return false;
            }

            return Record(AnalyticsEventNamesV2.AdResult, attemptId, placementId, result, reason);
        }

        private bool Record(
            string eventName,
            string attemptId,
            string placementId,
            string result,
            string reason)
        {
            if (string.IsNullOrWhiteSpace(attemptId) || string.IsNullOrWhiteSpace(placementId))
            {
                LastError = "Ad attempt ID and placement ID are required.";
                return false;
            }

            var recordResult = session.Record(
                eventName,
                AnalyticsSides.System,
                0,
                "ad:" + attemptId + ":" + eventName,
                value =>
                {
                    value.ad_point_id = placementId;
                    value.result = result;
                    value.reason = reason;
                },
                out var error);
            LastError = error;
            return recordResult == AnalyticsRecordResultV2.Accepted;
        }
    }

    public enum AnalyticsReleaseIssueSeverityV2
    {
        Warning,
        Error
    }

    public readonly struct AnalyticsReleaseIssueV2
    {
        public AnalyticsReleaseIssueV2(
            string code,
            AnalyticsReleaseIssueSeverityV2 severity,
            string message)
        {
            Code = code ?? string.Empty;
            Severity = severity;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public AnalyticsReleaseIssueSeverityV2 Severity { get; }
        public string Message { get; }
    }

    public sealed class AnalyticsReleaseConfigurationV2
    {
        public string AndroidPackageName;
        public int AndroidMinimumApiLevel;
        public bool UsesIl2Cpp;
        public bool IncludesArm64;
        public bool IncludesArmV7;
        public string BundleVersion;
        public int BundleVersionCode;
        public bool GoogleServicesFilePresent;
        public string FirebaseAndroidPackageName;
        public string FirebaseProjectId;
        public bool FirebaseDesktopConfigPresent;
        public string FirebaseDesktopProjectId;
        public string ExpectedFirebaseProjectId;
        public string FirebaseSdkVersion;
        public string StartupScenePath;
        public bool AnalyticsBootstrapInStartupScene;
        public bool AnalyticsBootstrapIsDedicatedRoot;
        public bool StartupEventSystemEnabled;
        public bool NativeCollectionDisabledByDefault;
        public bool ConsentUiAvailable;
        public string BuildLane;
        public bool ExplicitConsentRequired;
    }

    public sealed class AnalyticsReleaseReadinessReportV2
    {
        private readonly System.Collections.Generic.List<AnalyticsReleaseIssueV2> issues =
            new System.Collections.Generic.List<AnalyticsReleaseIssueV2>();

        public System.Collections.Generic.IReadOnlyList<AnalyticsReleaseIssueV2> Issues => issues;
        public bool IsReady
        {
            get
            {
                for (var index = 0; index < issues.Count; index++)
                {
                    if (issues[index].Severity == AnalyticsReleaseIssueSeverityV2.Error) return false;
                }
                return true;
            }
        }

        internal void Add(string code, AnalyticsReleaseIssueSeverityV2 severity, string message)
        {
            issues.Add(new AnalyticsReleaseIssueV2(code, severity, message));
        }
    }

    public static class AnalyticsReleaseReadinessValidatorV2
    {
        public const string RequiredAndroidPackageName = "com.drakeforge.mergedefense";
        public const int RequiredMinimumApiLevel = 23;
        public const string RequiredFirebaseSdkVersion = "13.14.0";

        public static AnalyticsReleaseReadinessReportV2 Validate(AnalyticsReleaseConfigurationV2 value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var report = new AnalyticsReleaseReadinessReportV2();
            if (!string.Equals(value.AndroidPackageName, RequiredAndroidPackageName, StringComparison.Ordinal))
            {
                report.Add("android_package", AnalyticsReleaseIssueSeverityV2.Error,
                    "Android package name must be " + RequiredAndroidPackageName + ".");
            }
            if (value.AndroidMinimumApiLevel < RequiredMinimumApiLevel)
            {
                report.Add("android_min_api", AnalyticsReleaseIssueSeverityV2.Error,
                    "Android Minimum API Level must be at least 23.");
            }
            if (!value.UsesIl2Cpp)
            {
                report.Add("android_il2cpp", AnalyticsReleaseIssueSeverityV2.Error,
                    "Android release must use IL2CPP.");
            }
            if (!value.IncludesArm64)
            {
                report.Add("android_arm64", AnalyticsReleaseIssueSeverityV2.Error,
                    "Android release must include ARM64.");
            }
            if (value.IncludesArmV7)
            {
                report.Add("android_armv7", AnalyticsReleaseIssueSeverityV2.Warning,
                    "ARMv7 is also enabled; confirm whether the release should be ARM64-only.");
            }
            if (string.IsNullOrWhiteSpace(value.BundleVersion) || value.BundleVersionCode < 1)
            {
                report.Add("build_version", AnalyticsReleaseIssueSeverityV2.Error,
                    "Bundle version and positive Android version code are required.");
            }
            if (!value.GoogleServicesFilePresent)
            {
                report.Add("google_services", AnalyticsReleaseIssueSeverityV2.Error,
                    "Assets/google-services.json is required.");
            }
            if (!string.Equals(value.FirebaseAndroidPackageName, value.AndroidPackageName, StringComparison.Ordinal))
            {
                report.Add("firebase_package", AnalyticsReleaseIssueSeverityV2.Error,
                    "Firebase Android package does not match PlayerSettings.");
            }
            if (string.IsNullOrWhiteSpace(value.FirebaseProjectId) ||
                (!string.IsNullOrWhiteSpace(value.ExpectedFirebaseProjectId) &&
                 !string.Equals(value.FirebaseProjectId, value.ExpectedFirebaseProjectId, StringComparison.Ordinal)))
            {
                report.Add("firebase_project", AnalyticsReleaseIssueSeverityV2.Error,
                    "Firebase Project ID does not match the approved release project.");
            }
            if (!value.FirebaseDesktopConfigPresent)
            {
                report.Add("firebase_desktop_config", AnalyticsReleaseIssueSeverityV2.Warning,
                    "Generated desktop Firebase config is absent; Unity Editor DebugView cannot be validated.");
            }
            else if (!string.Equals(
                         value.FirebaseDesktopProjectId,
                         value.FirebaseProjectId,
                         StringComparison.Ordinal))
            {
                report.Add("firebase_desktop_project", AnalyticsReleaseIssueSeverityV2.Error,
                    "Desktop and Android Firebase configurations must use the same project.");
            }
            if (!string.Equals(value.FirebaseSdkVersion, RequiredFirebaseSdkVersion, StringComparison.Ordinal))
            {
                report.Add("firebase_sdk", AnalyticsReleaseIssueSeverityV2.Error,
                    "Firebase Unity SDK must be " + RequiredFirebaseSdkVersion + ".");
            }
            if (string.IsNullOrWhiteSpace(value.StartupScenePath))
            {
                report.Add("startup_scene", AnalyticsReleaseIssueSeverityV2.Error,
                    "Build Settings must contain an enabled startup scene.");
            }
            else if (!value.AnalyticsBootstrapInStartupScene)
            {
                report.Add("analytics_bootstrap", AnalyticsReleaseIssueSeverityV2.Error,
                    "The first enabled scene must contain FirebaseAnalyticsBootstrap.");
            }
            else if (!value.AnalyticsBootstrapIsDedicatedRoot)
            {
                report.Add("analytics_bootstrap_host", AnalyticsReleaseIssueSeverityV2.Error,
                    "FirebaseAnalyticsBootstrap must be the only behaviour on a dedicated root object.");
            }
            if (!value.StartupEventSystemEnabled)
            {
                report.Add("startup_event_system", AnalyticsReleaseIssueSeverityV2.Error,
                    "The startup scene must contain an enabled EventSystem for consent and login buttons.");
            }
            if (!value.NativeCollectionDisabledByDefault)
            {
                report.Add("analytics_native_default", AnalyticsReleaseIssueSeverityV2.Error,
                    "Android must disable native Firebase Analytics collection until explicit consent.");
            }
            if (!value.ConsentUiAvailable)
            {
                report.Add("analytics_consent_ui", AnalyticsReleaseIssueSeverityV2.Error,
                    "A user-facing Analytics allow/deny control is required.");
            }
            if (!AnalyticsBuildLanes.IsKnown(value.BuildLane))
            {
                report.Add("build_lane", AnalyticsReleaseIssueSeverityV2.Error,
                    "Build Lane must be development, qa, staging or production.");
            }
            if (!value.ExplicitConsentRequired)
            {
                report.Add("analytics_consent", AnalyticsReleaseIssueSeverityV2.Error,
                    "Analytics collection must default to Unknown until explicit consent.");
            }
            return report;
        }
    }

    public static class AnalyticsBuildLaneResolverV2
    {
        public static string Resolve()
        {
#if DRAGONBOUND_STAGING
            return AnalyticsBuildLanes.Staging;
#elif UNITY_EDITOR
            return AnalyticsBuildLanes.Development;
#elif DEVELOPMENT_BUILD
            return AnalyticsBuildLanes.Qa;
#else
            return AnalyticsBuildLanes.Production;
#endif
        }
    }

    /// <summary>Current local run handle used only for lifecycle flush and integration discovery.</summary>
    public static class AnalyticsRuntimeRegistryV2
    {
        private static readonly object Gate = new object();
        private static AnalyticsRunSessionV2 current;
        private static AnalyticsRunSessionV2 applicationSession;

        public static AnalyticsRunSessionV2 Current
        {
            get
            {
                lock (Gate)
                {
                    return current;
                }
            }
        }

        public static AnalyticsRunSessionV2 Active
        {
            get
            {
                lock (Gate)
                {
                    return current ?? applicationSession;
                }
            }
        }

        public static void SetApplicationSession(AnalyticsRunSessionV2 value)
        {
            lock (Gate)
            {
                applicationSession?.Flush();
                applicationSession = value;
            }
        }

        public static void SetCurrent(AnalyticsRunSessionV2 value)
        {
            lock (Gate)
            {
                current?.Flush();
                if (!ReferenceEquals(applicationSession, current))
                {
                    applicationSession?.Flush();
                }
                current = value;
            }
        }

        public static void Flush()
        {
            lock (Gate)
            {
                current?.Flush();
                if (!ReferenceEquals(applicationSession, current))
                {
                    applicationSession?.Flush();
                }
            }
        }

        public static void ClearPending()
        {
            lock (Gate)
            {
                current?.ClearPending();
                if (!ReferenceEquals(applicationSession, current))
                {
                    applicationSession?.ClearPending();
                }
            }
        }

        public static void Clear(AnalyticsRunSessionV2 value)
        {
            lock (Gate)
            {
                if (!ReferenceEquals(current, value))
                {
                    return;
                }

                current.Flush();
                current = null;
            }
        }

        public static void ClearApplicationSession(AnalyticsRunSessionV2 value)
        {
            lock (Gate)
            {
                if (!ReferenceEquals(applicationSession, value))
                {
                    return;
                }

                applicationSession.Flush();
                applicationSession = null;
            }
        }
    }
}
