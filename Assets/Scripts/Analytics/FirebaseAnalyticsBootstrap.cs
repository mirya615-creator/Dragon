using Firebase;
using Firebase.Analytics;
using DragonBound.Analytics;
using System.Collections.Generic;
using System;
using UnityEngine;

public sealed class FirebaseAnalyticsBootstrap : MonoBehaviour
{
    private const string ConsentPreferenceKey = "dragonbound.analytics.consent.v1";

    private static FirebaseAnalyticsBootstrap instance;
    private AnalyticsRunSessionV2 applicationAnalyticsSession;

    public static bool IsReady { get; private set; }
    public static bool IsCollectionEnabled { get; private set; }
    public static AnalyticsConsentStateV2 ConsentState { get; private set; }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        ConsentState = LoadConsentState();
        IsCollectionEnabled = ConsentState == AnalyticsConsentStateV2.Granted;
        FirebaseAnalyticsGatewayRegistryV2.Current = new FirebaseSdkAnalyticsGatewayV2();
        FirebaseAnalyticsRuntimeState.SetConsentState(ConsentState);
        FirebaseAnalyticsRuntimeState.SetCollectionEnabled(IsCollectionEnabled);
        var appContext = new DrakeforgeAnalyticsRunContext(
            "app_session:" + Guid.NewGuid().ToString("N"),
            0,
            AnalyticsExecutionContexts.Application,
            "client",
            string.IsNullOrWhiteSpace(Application.version) ? "development" : Application.version,
            AnalyticsRankTiers.Unranked,
            AnalyticsAiDifficulties.None,
            buildLane: AnalyticsBuildLaneResolverV2.Resolve());
        applicationAnalyticsSession = new AnalyticsRunSessionV2(
            new AnalyticsRecorderV2(new BufferedAnalyticsSinkV2(
                new FirebaseAnalyticsSinkV2(),
                canBuffer: () => FirebaseAnalyticsRuntimeState.CanCollect)),
            appContext);
        AnalyticsRuntimeRegistryV2.SetApplicationSession(applicationAnalyticsSession);
    }

    private async void Start()
    {
        var status = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (status == DependencyStatus.Available)
        {
            _ = FirebaseApp.DefaultInstance;
            IsReady = true;
            FirebaseAnalyticsRuntimeState.SetReady(true);
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(IsCollectionEnabled);
            AnalyticsRuntimeRegistryV2.Flush();
            Debug.Log("[Analytics] Firebase Available");
        }
        else
        {
            IsReady = false;
            FirebaseAnalyticsRuntimeState.SetReady(false);
            Debug.LogError($"[Analytics] Firebase dependency error: {status}");
        }
    }

    public static void SetCollectionEnabled(bool enabled)
    {
        ConsentState = enabled ? AnalyticsConsentStateV2.Granted : AnalyticsConsentStateV2.Denied;
        IsCollectionEnabled = enabled;
        PlayerPrefs.SetInt(ConsentPreferenceKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        FirebaseAnalyticsRuntimeState.SetConsentState(ConsentState);
        FirebaseAnalyticsRuntimeState.SetCollectionEnabled(enabled);
        if (!enabled)
        {
            AnalyticsRuntimeRegistryV2.ClearPending();
        }
        if (!IsReady)
        {
            return;
        }

        FirebaseAnalytics.SetAnalyticsCollectionEnabled(enabled);
        if (enabled)
        {
            AnalyticsRuntimeRegistryV2.Flush();
        }
    }

    private static AnalyticsConsentStateV2 LoadConsentState()
    {
        if (!PlayerPrefs.HasKey(ConsentPreferenceKey))
        {
            return AnalyticsConsentStateV2.Unknown;
        }

        return PlayerPrefs.GetInt(ConsentPreferenceKey, 0) == 1
            ? AnalyticsConsentStateV2.Granted
            : AnalyticsConsentStateV2.Denied;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            AnalyticsRuntimeRegistryV2.Flush();
        }
    }

    private void OnApplicationQuit()
    {
        AnalyticsRuntimeRegistryV2.Flush();
    }

    private void OnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        FirebaseAnalyticsRuntimeState.SetReady(false);
        AnalyticsRuntimeRegistryV2.ClearApplicationSession(applicationAnalyticsSession);
        IsReady = false;
        instance = null;
    }
}

internal sealed class FirebaseSdkAnalyticsGatewayV2 : IFirebaseAnalyticsGatewayV2
{
    public void LogEvent(string eventName, IReadOnlyList<FirebaseAnalyticsParameterV2> parameters)
    {
        var converted = new Parameter[parameters.Count];
        for (var index = 0; index < parameters.Count; index++)
        {
            var value = parameters[index];
            switch (value.Kind)
            {
                case FirebaseAnalyticsParameterKindV2.Integer:
                    converted[index] = new Parameter(value.Name, value.IntegerValue);
                    break;
                case FirebaseAnalyticsParameterKindV2.Double:
                    converted[index] = new Parameter(value.Name, value.DoubleValue);
                    break;
                default:
                    converted[index] = new Parameter(value.Name, value.StringValue);
                    break;
            }
        }

        FirebaseAnalytics.LogEvent(eventName, converted);
    }
}
