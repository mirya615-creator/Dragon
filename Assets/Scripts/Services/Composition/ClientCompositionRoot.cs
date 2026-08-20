using System;
using UnityEngine;

public static class ClientCompositionRoot
{
    private const string ConfigResourcePath = "Configuration/ClientServiceConfig";
    private static IClientServices current;

    public static IClientServices Current => current ??
        throw new InvalidOperationException("Client services have not been initialized.");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        current = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (current != null) return;

        ClientServiceConfig config = Resources.Load<ClientServiceConfig>(ConfigResourcePath);
        if (config == null)
        {
            throw new InvalidOperationException(
                $"Client service config is missing at Resources/{ConfigResourcePath}.asset.");
        }

        switch (config.BackendMode)
        {
            case BackendMode.Local:
                current = LocalServiceModule.Build(config);
                break;
            case BackendMode.GoUnary:
                current = GoUnaryServiceModule.Build(config);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(config.BackendMode),
                    config.BackendMode,
                    "Unknown backend mode.");
        }
        AuthSessionCoordinator.EnsureCreated();
    }

    public static void InstallForTests(IClientServices services)
    {
        current = services ?? throw new ArgumentNullException(nameof(services));
    }
}
