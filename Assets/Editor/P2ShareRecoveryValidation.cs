using System;
using UnityEditor;
using UnityEngine;

public static class P2ShareRecoveryValidation
{
    [MenuItem("DragonBound/Validation/Validate P2 Share Recovery")]
    public static void Run()
    {
        string playerId = "validation-player-" + Guid.NewGuid().ToString("N");
        const string placement = "energy_share";

        string first = PendingShareEventStore.GetOrCreate(playerId, placement);
        string retry = PendingShareEventStore.GetOrCreate(playerId, placement);
        Require(first == retry, "A retry replaced the pending share event ID.");
        Require(Guid.TryParseExact(first, "N", out _), "Pending share event ID format is invalid.");

        PendingShareEventStore.Complete(playerId, placement, "different-event");
        Require(
            PendingShareEventStore.GetOrCreate(playerId, placement) == first,
            "A mismatched completion removed the pending share event.");

        PendingShareEventStore.Complete(playerId, placement, first);
        string next = PendingShareEventStore.GetOrCreate(playerId, placement);
        Require(next != first, "A completed share event was reused for a new reward.");
        PendingShareEventStore.Complete(playerId, placement, next);

        Debug.Log("P2 share recovery validation passed.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
