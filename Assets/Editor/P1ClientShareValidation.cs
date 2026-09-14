using System;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;

public static class P1ClientShareValidation
{
    [MenuItem("DragonBound/Validation/Validate P1 Client Share")]
    public static void Run()
    {
        ValidateImmediateClientSuccess();
        ValidateWireContracts();
        Debug.Log("P1 client-reported share validation passed.");
    }

    private static void ValidateImmediateClientSuccess()
    {
        var service = new ClientReportedShareService();
        ShareResult result = service.ShareAsync(
                new ShareRequest { PlacementId = "energy_share" },
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        Require(result == ShareResult.Completed, "Energy share was not reported as completed.");
    }

    private static void ValidateWireContracts()
    {
        Assembly runtime = typeof(ClientReportedShareService).Assembly;
        RequireFields(runtime, "GoSigninClaimRequest",
            "claim_mode", "client_share_succeeded", "client_share_event_id", "share_platform");
        RequireFields(runtime, "GoShareCreateRequest",
            "platform", "placement_id", "client_share_succeeded", "client_share_event_id");
        RequireFields(runtime, "GoShareClaimRequest",
            "share_id", "share_token", "platform", "placement_id",
            "client_share_succeeded", "client_share_event_id");
    }

    private static void RequireFields(Assembly assembly, string typeName, params string[] fieldNames)
    {
        Type type = assembly.GetType(typeName);
        Require(type != null, typeName + " is missing.");
        foreach (string fieldName in fieldNames)
        {
            Require(
                type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public) != null,
                typeName + "." + fieldName + " is missing.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
