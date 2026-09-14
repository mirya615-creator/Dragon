using System;

namespace DragonBound.Analytics
{
    public struct RuneLoadoutOperationObservationV2
    {
        public RuneLoadoutOperationObservationV2(
            string dedupeKey,
            int wave,
            string heroId,
            string runeId,
            bool accepted,
            string reason)
        {
            DedupeKey = dedupeKey ?? string.Empty;
            Wave = wave;
            HeroId = heroId ?? string.Empty;
            RuneId = runeId ?? string.Empty;
            Accepted = accepted;
            Reason = reason ?? string.Empty;
        }

        public readonly string DedupeKey;
        public readonly int Wave;
        public readonly string HeroId;
        public readonly string RuneId;
        public readonly bool Accepted;
        public readonly string Reason;
    }

    public struct RuneGateRejectionObservationV2
    {
        public RuneGateRejectionObservationV2(
            string dedupeKey,
            int wave,
            string operation,
            int accountDay,
            string reason)
        {
            DedupeKey = dedupeKey ?? string.Empty;
            Wave = wave;
            Operation = operation ?? string.Empty;
            AccountDay = accountDay;
            Reason = reason ?? string.Empty;
        }

        public readonly string DedupeKey;
        public readonly int Wave;
        public readonly string Operation;
        public readonly int AccountDay;
        public readonly string Reason;
    }

    public struct RuneRewardPendingObservationV2
    {
        public RuneRewardPendingObservationV2(string dedupeKey, int wave)
        {
            DedupeKey = dedupeKey ?? string.Empty;
            Wave = wave;
        }

        public readonly string DedupeKey;
        public readonly int Wave;
    }

    public struct RuneRewardResultObservationV2
    {
        public RuneRewardResultObservationV2(
            string dedupeKey,
            int wave,
            string runeId,
            string rewardForm,
            bool granted,
            string reason)
        {
            DedupeKey = dedupeKey ?? string.Empty;
            Wave = wave;
            RuneId = runeId ?? string.Empty;
            RewardForm = rewardForm ?? string.Empty;
            Granted = granted;
            Reason = reason ?? string.Empty;
        }

        public readonly string DedupeKey;
        public readonly int Wave;
        public readonly string RuneId;
        public readonly string RewardForm;
        public readonly bool Granted;
        public readonly string Reason;
    }

    /// <summary>
    /// Converts typed Rune profile/reward observations into V2 events. It does not call Rune
    /// services, own inventory, or infer a reward from a failed operation.
    /// </summary>
    public sealed class RuneAnalyticsAdapterV2
    {
        private readonly AnalyticsRunSessionV2 session;
        private readonly string side;

        public RuneAnalyticsAdapterV2(
            AnalyticsRecorderV2 recorder,
            string runId,
            int runSeed,
            string executionContext,
            string side,
            string rankTier,
            string aiDifficulty,
            string configVersion,
            string buildVersion)
            : this(
                new AnalyticsRunSessionV2(
                    recorder,
                    new DrakeforgeAnalyticsRunContext(
                        runId,
                        runSeed,
                        executionContext,
                        configVersion,
                        buildVersion,
                        rankTier,
                        aiDifficulty)),
                side)
        {
        }

        public RuneAnalyticsAdapterV2(AnalyticsRunSessionV2 session, string side)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.side = side ?? string.Empty;
        }

        public AnalyticsRecordResultV2 RecordLoadoutAssign(
            RuneLoadoutOperationObservationV2 observation,
            out string error)
        {
            return RecordOperation(
                AnalyticsEventNamesV2.RuneLoadoutAssign,
                observation,
                observation.HeroId,
                observation.RuneId,
                out error);
        }

        public AnalyticsRecordResultV2 RecordLoadoutUnequip(
            RuneLoadoutOperationObservationV2 observation,
            out string error)
        {
            return RecordOperation(
                AnalyticsEventNamesV2.RuneLoadoutUnequip,
                observation,
                observation.HeroId,
                string.Empty,
                out error);
        }

        public AnalyticsRecordResultV2 RecordCraft(
            RuneLoadoutOperationObservationV2 observation,
            out string error)
        {
            return RecordOperation(
                AnalyticsEventNamesV2.RuneCraft,
                observation,
                string.Empty,
                observation.RuneId,
                out error);
        }

        public AnalyticsRecordResultV2 RecordGateRejection(
            RuneGateRejectionObservationV2 observation,
            out string error)
        {
            return session.Record(
                AnalyticsEventNamesV2.RuneGateRejection,
                side,
                observation.Wave,
                observation.DedupeKey,
                value =>
                {
                    value.rune_operation = observation.Operation;
                    value.gate_state = AnalyticsRuneGateStates.Locked;
                    value.account_day = observation.AccountDay;
                    value.reason = observation.Reason;
                },
                out error);
        }

        public AnalyticsRecordResultV2 RecordRewardPending(
            RuneRewardPendingObservationV2 observation,
            out string error)
        {
            return session.Record(
                AnalyticsEventNamesV2.RuneRewardPending,
                side,
                observation.Wave,
                observation.DedupeKey,
                value =>
                {
                    value.reward_wave = observation.Wave;
                    value.reward_state = AnalyticsRuneRewardStates.Pending;
                },
                out error);
        }

        public AnalyticsRecordResultV2 RecordRewardResult(
            RuneRewardResultObservationV2 observation,
            out string error)
        {
            return session.Record(
                observation.Granted
                    ? AnalyticsEventNamesV2.RuneRewardGranted
                    : AnalyticsEventNamesV2.RuneRewardRejected,
                side,
                observation.Wave,
                observation.DedupeKey,
                value =>
                {
                    value.reward_wave = observation.Wave;
                    value.rune_id = observation.RuneId;
                    value.reward_form = observation.RewardForm;
                    value.reward_state = observation.Granted
                        ? AnalyticsRuneRewardStates.Granted
                        : AnalyticsRuneRewardStates.Rejected;
                    value.reason = observation.Reason;
                },
                out error);
        }

        private AnalyticsRecordResultV2 RecordOperation(
            string eventName,
            RuneLoadoutOperationObservationV2 observation,
            string heroId,
            string runeId,
            out string error)
        {
            return session.Record(
                eventName,
                side,
                observation.Wave,
                observation.DedupeKey,
                value =>
                {
                    value.hero_id = heroId;
                    value.rune_id = runeId;
                    value.operation_result = observation.Accepted
                        ? AnalyticsRuneOperationResults.Accepted
                        : AnalyticsRuneOperationResults.Rejected;
                    value.reason = observation.Reason;
                },
                out error);
        }
    }
}
