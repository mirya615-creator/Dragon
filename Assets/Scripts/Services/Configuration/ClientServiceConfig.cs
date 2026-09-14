using UnityEngine;

[CreateAssetMenu(
    fileName = "ClientServiceConfig",
    menuName = "DragonBound/Client Service Config")]
public sealed class ClientServiceConfig : ScriptableObject
{
    [SerializeField] private BackendMode backendMode = BackendMode.Local;
    [SerializeField] private string apiBaseUrl = string.Empty;
    [SerializeField, Min(1)] private int timeoutSeconds = 15;
    [SerializeField] private string clientVersion = string.Empty;
    [SerializeField] private string contentVersion = string.Empty;
    [SerializeField] private string configVersion = string.Empty;
    [SerializeField] private string defaultStageId = string.Empty;
    [SerializeField] private string expectedStageSnapshotDigest = string.Empty;
    [SerializeField] private bool requireAuthoritativeRunContract = true;
    [SerializeField] private bool enableNetworkLogging;

    public BackendMode BackendMode => backendMode;
    public string ApiBaseUrl => apiBaseUrl;
    public int TimeoutSeconds => Mathf.Max(1, timeoutSeconds);
    public string ClientVersion => clientVersion;
    public string ContentVersion => contentVersion;
    public string ConfigVersion => configVersion;
    public string DefaultStageId => defaultStageId;
    public string ExpectedStageSnapshotDigest => expectedStageSnapshotDigest;
    public bool RequireAuthoritativeRunContract => requireAuthoritativeRunContract;
    public bool EnableNetworkLogging => enableNetworkLogging;
}
