using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public sealed class ActiveRunRecord
{
    public int schemaVersion = 2;
    public string playerId;
    public string runId;
    public string quitIdempotencyKey;
    public string startedAt;
    public string expiresAt;
    public long eventSequence;
    public string currentEventHash;
}

public static class ActiveRunStore
{
    private const string StorageKey = "dragonbound.active-run.v1";

    public static void Save(
        string playerId,
        string runId,
        string startedAt,
        string expiresAt)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("Player ID is required.", nameof(playerId));
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("Run ID is required.", nameof(runId));

        var record = new ActiveRunRecord
        {
            playerId = playerId,
            runId = runId,
            quitIdempotencyKey = runId + ":quit",
            startedAt = startedAt ?? string.Empty,
            expiresAt = expiresAt ?? string.Empty,
            eventSequence = 0,
            currentEventHash = string.Empty
        };
        SaveRecord(record);
    }

    public static bool TryGet(out ActiveRunRecord record)
    {
        record = null;
        string json = PlayerPrefs.GetString(StorageKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            record = JsonUtility.FromJson<ActiveRunRecord>(json);
        }
        catch (ArgumentException)
        {
            record = null;
        }

        if (record == null || (record.schemaVersion != 1 && record.schemaVersion != 2) ||
            string.IsNullOrWhiteSpace(record.playerId) ||
            string.IsNullOrWhiteSpace(record.runId))
        {
            Clear();
            record = null;
            return false;
        }
        // All quit paths deliberately share one key so a response lost during Play Mode
        // shutdown can be replayed safely on the next launch.
        record.quitIdempotencyKey = record.runId + ":quit";
        return true;
    }

    public static void InitializeEventChain(string runId, string initialHash)
    {
        if (!TryGet(out ActiveRunRecord record) ||
            !string.Equals(record.runId, runId, StringComparison.Ordinal))
            throw new InvalidOperationException("Active Run does not match the event chain.");
        if (!IsEventHash(initialHash))
            throw new ArgumentException("Initial event hash is invalid.", nameof(initialHash));

        record.schemaVersion = 2;
        record.eventSequence = 0;
        record.currentEventHash = initialHash;
        SaveRecord(record);
    }

    public static bool TryAdvanceEventChain(
        string runId,
        long expectedSequence,
        string expectedCurrentHash,
        long nextSequence,
        string nextCurrentHash)
    {
        if (!TryGet(out ActiveRunRecord record) ||
            !string.Equals(record.runId, runId, StringComparison.Ordinal) ||
            record.eventSequence != expectedSequence ||
            !string.Equals(record.currentEventHash, expectedCurrentHash, StringComparison.Ordinal) ||
            nextSequence != expectedSequence + 1 ||
            !IsEventHash(nextCurrentHash))
            return false;

        record.schemaVersion = 2;
        record.eventSequence = nextSequence;
        record.currentEventHash = nextCurrentHash;
        SaveRecord(record);
        return true;
    }

    public static bool TryGetForPlayer(string playerId, out ActiveRunRecord record)
    {
        if (!TryGet(out record)) return false;
        if (string.Equals(record.playerId, playerId, StringComparison.Ordinal)) return true;
        record = null;
        return false;
    }

    public static void ClearIfMatches(string runId)
    {
        if (!TryGet(out ActiveRunRecord record) ||
            !string.Equals(record.runId, runId, StringComparison.Ordinal)) return;
        Clear();
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(StorageKey);
        PlayerPrefs.Save();
    }

    private static void SaveRecord(ActiveRunRecord record)
    {
        PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(record));
        PlayerPrefs.Save();
    }

    private static bool IsEventHash(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 64) return false;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (!((character >= '0' && character <= '9') ||
                  (character >= 'a' && character <= 'f')))
                return false;
        }
        return true;
    }
}

public interface IActiveRunRecoveryGateway
{
    Task QuitActiveRunAsync(
        string playerId,
        string runId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    bool TryQuitActiveRunBlocking(
        string playerId,
        string runId,
        string idempotencyKey,
        int timeoutMilliseconds);
}

[DisallowMultipleComponent]
public sealed class ActiveRunLifecycleCoordinator : MonoBehaviour
{
    private static ActiveRunLifecycleCoordinator instance;
    private readonly SemaphoreSlim recoveryGate = new SemaphoreSlim(1, 1);

    public static ActiveRunLifecycleCoordinator EnsureCreated()
    {
        if (instance != null) return instance;
        instance = FindObjectOfType<ActiveRunLifecycleCoordinator>();
        if (instance != null) return instance;
        var owner = new GameObject(nameof(ActiveRunLifecycleCoordinator));
        instance = owner.AddComponent<ActiveRunLifecycleCoordinator>();
        DontDestroyOnLoad(owner);
        return instance;
    }

    public async Task<bool> RecoverPendingRunAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        if (!ActiveRunStore.TryGetForPlayer(playerId, out ActiveRunRecord record))
            return false;
        if (!(ClientCompositionRoot.Current.Gameplay is IActiveRunRecoveryGateway recovery))
            return false;

        await recoveryGate.WaitAsync(cancellationToken);
        try
        {
            if (!ActiveRunStore.TryGetForPlayer(playerId, out record)) return false;
            await recovery.QuitActiveRunAsync(
                record.playerId,
                record.runId,
                record.quitIdempotencyKey,
                cancellationToken);
            return true;
        }
        finally
        {
            recoveryGate.Release();
        }
    }

    public static bool TryForceQuitBlockingForEditor(int timeoutMilliseconds = 2500)
    {
        if (!ActiveRunStore.TryGet(out ActiveRunRecord record)) return true;
        IClientServices services;
        try
        {
            services = ClientCompositionRoot.Current;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        AuthSession session = services.AuthSession.Current;
        if (session == null ||
            !string.Equals(session.PlayerId, record.playerId, StringComparison.Ordinal) ||
            !(services.Gameplay is IActiveRunRecoveryGateway recovery))
        {
            return false;
        }
        return recovery.TryQuitActiveRunBlocking(
            record.playerId,
            record.runId,
            record.quitIdempotencyKey,
            timeoutMilliseconds);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
