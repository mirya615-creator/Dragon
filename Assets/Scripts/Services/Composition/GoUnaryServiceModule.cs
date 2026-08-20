using System;

public static class GoUnaryServiceModule
{
    public static IClientServices Build(ClientServiceConfig config)
    {
        throw new InvalidOperationException(
            "GoUnary backend is reserved but not enabled in this client build.");
    }
}
