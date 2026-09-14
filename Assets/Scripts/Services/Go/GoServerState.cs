using System;
using System.Collections.Generic;
using System.Threading;

internal sealed class GoServerState
{
    internal sealed class RunTicket
    {
        public string TicketSignature;
        public string RunNonce;
        public string StartedAt;
        public long EventSequence;
        public string CurrentEventHash;
        public long GoldReward;
        public long RuneFragmentReward;
        public GoRunFinishResponse Finish;
        public GoRunFinishRequest PendingFinishRequest;
        public string PendingFinishIdempotencyKey;
        public string PendingFinishFingerprint;
    }

    private readonly Dictionary<string, RunTicket> runs =
        new Dictionary<string, RunTicket>(StringComparer.Ordinal);

    public GoBootstrapResponse Bootstrap { get; private set; }
    internal SemaphoreSlim EventChainGate { get; } = new SemaphoreSlim(1, 1);

    public void UpdateBootstrap(GoBootstrapResponse value)
    {
        Bootstrap = value;
    }

    public void StoreTicket(
        string runId,
        string signature,
        string runNonce,
        string startedAt)
    {
        if (string.IsNullOrWhiteSpace(runId)) return;
        if (!runs.TryGetValue(runId, out RunTicket ticket))
        {
            ticket = new RunTicket();
            runs.Add(runId, ticket);
        }
        ticket.TicketSignature = signature ?? string.Empty;
        ticket.RunNonce = runNonce ?? string.Empty;
        ticket.StartedAt = startedAt ?? string.Empty;
    }

    public void InitializeEventChain(string runId, string initialHash)
    {
        if (!runs.TryGetValue(runId ?? string.Empty, out RunTicket ticket))
            throw new InvalidOperationException("Run ticket is unavailable.");
        ticket.EventSequence = 0;
        ticket.CurrentEventHash = initialHash ?? string.Empty;
    }

    public bool TryAdvanceEventChain(
        string runId,
        long expectedSequence,
        string expectedCurrentHash,
        long nextSequence,
        string nextCurrentHash)
    {
        if (!runs.TryGetValue(runId ?? string.Empty, out RunTicket ticket) ||
            ticket.EventSequence != expectedSequence ||
            !string.Equals(ticket.CurrentEventHash, expectedCurrentHash, StringComparison.Ordinal) ||
            nextSequence != expectedSequence + 1)
            return false;
        ticket.EventSequence = nextSequence;
        ticket.CurrentEventHash = nextCurrentHash ?? string.Empty;
        return true;
    }

    public bool TryGetTicket(string runId, out RunTicket ticket)
    {
        return runs.TryGetValue(runId ?? string.Empty, out ticket);
    }

    public void StoreFinish(GoRunFinishResponse finish)
    {
        if (finish == null || string.IsNullOrWhiteSpace(finish.run_id)) return;
        if (!runs.TryGetValue(finish.run_id, out RunTicket ticket))
        {
            ticket = new RunTicket();
            runs.Add(finish.run_id, ticket);
        }
        long previousGoldReward = ticket.GoldReward;
        long previousRuneFragmentReward = ticket.RuneFragmentReward;
        ticket.Finish = finish;
        ticket.PendingFinishRequest = null;
        ticket.PendingFinishIdempotencyKey = string.Empty;
        ticket.PendingFinishFingerprint = string.Empty;
        ticket.GoldReward = finish.settlement == null ? 0 : finish.settlement.total_gold;
        ticket.RuneFragmentReward = finish.settlement == null
            ? 0
            : finish.settlement.rune_fragments;
        if (Bootstrap?.resources != null && finish.settlement != null)
        {
            // Idempotent/replayed settlement responses replace the cached contribution
            // instead of crediting the same Run twice.
            Bootstrap.resources.gold += ticket.GoldReward - previousGoldReward;
            Bootstrap.resources.rune_fragment +=
                ticket.RuneFragmentReward - previousRuneFragmentReward;
        }
    }
}
