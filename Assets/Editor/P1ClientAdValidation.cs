using System;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;

public static class P1ClientAdValidation
{
    [MenuItem("DragonBound/Validation/Validate P1 Client Ads")]
    public static void Run()
    {
        ValidateImmediateClientSuccess();
        ValidatePendingEventRecovery();
        ValidateWireContract();
        Debug.Log("P1 client-reported ad validation passed.");
    }

    private static void ValidateImmediateClientSuccess()
    {
        var service = new ClientReportedRewardedAdService();
        RewardedAdResult result = service.ShowAsync("energy_restore", CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        Require(result == RewardedAdResult.Completed, "Ad click was not reported as completed.");
    }

    private static void ValidatePendingEventRecovery()
    {
        string playerId = "ad-validation-" + Guid.NewGuid().ToString("N");
        const string placement = "settle_double";
        const string scope = "validation-run";
        string first = PendingAdEventStore.GetOrCreate(playerId, placement, scope);
        string retry = PendingAdEventStore.GetOrCreate(playerId, placement, scope);
        Require(first == retry, "A retry replaced the pending ad event ID.");

        PendingAdEventStore.Complete(playerId, placement, scope, "different-event");
        Require(
            PendingAdEventStore.GetOrCreate(playerId, placement, scope) == first,
            "A mismatched completion removed the pending ad event.");

        PendingAdEventStore.Complete(playerId, placement, scope, first);
        string next = PendingAdEventStore.GetOrCreate(playerId, placement, scope);
        Require(next != first, "A completed ad event was reused for a new reward.");
        PendingAdEventStore.Complete(playerId, placement, scope, next);
    }

    private static void ValidateWireContract()
    {
        Assembly runtime = typeof(ClientReportedRewardedAdService).Assembly;
        Type type = runtime.GetType("GoAdClaimRequest");
        Require(type != null, "GoAdClaimRequest is missing.");
        foreach (string fieldName in new[]
                 {
                     "transaction_id", "player_id", "placement", "provider", "payload",
                     "signature", "run_id", "client_ad_succeeded", "client_ad_event_id",
                     "ad_platform"
                 })
        {
            Require(
                type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public) != null,
                "GoAdClaimRequest." + fieldName + " is missing.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
