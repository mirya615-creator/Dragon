using System;

namespace DragonBound.Analytics
{
    /// <summary>Sanitized ledger metadata returned by an authoritative service.</summary>
    public readonly struct AuthoritativeLedgerResultV2
    {
        public AuthoritativeLedgerResultV2(
            string operation,
            string status,
            string transactionRefHash,
            string idempotencyKeyHash,
            string reason)
        {
            Operation = operation ?? string.Empty;
            Status = status ?? string.Empty;
            TransactionRefHash = transactionRefHash ?? string.Empty;
            IdempotencyKeyHash = idempotencyKeyHash ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public string Operation { get; }
        public string Status { get; }
        public string TransactionRefHash { get; }
        public string IdempotencyKeyHash { get; }
        public string Reason { get; }
    }

    public interface IAuthoritativeServiceAnalyticsObserverV2
    {
        bool RecordEnergyResult(string operationKey, int wave, int amount, bool isGrant, string reason,
            AuthoritativeLedgerResultV2 ledger);
        bool RecordSettlementGold(string operationKey, int wave, int amount, string reason,
            AuthoritativeLedgerResultV2 ledger);
        bool RecordMerchantOpen(string operationKey, string merchantId, string result, string reason);
        bool RecordMerchantOffer(string operationKey, string merchantId, string offerId, string currencyType);
        bool RecordMerchantPurchase(string operationKey, string merchantId, string offerId, string currencyType,
            string result, string reason, AuthoritativeLedgerResultV2 ledger);
        bool RecordRankSnapshot(string operationKey, int rankValue, string reason);
        bool RecordRankChange(string operationKey, int rankValue, string reason);
        bool RecordLeaderboardSnapshot(string operationKey, string period, int rankValue);
    }

    /// <summary>Default observer while no authoritative server gateway is installed.</summary>
    public sealed class NoOpAuthoritativeServiceAnalyticsObserverV2 : IAuthoritativeServiceAnalyticsObserverV2
    {
        public static NoOpAuthoritativeServiceAnalyticsObserverV2 Instance { get; } =
            new NoOpAuthoritativeServiceAnalyticsObserverV2();

        private NoOpAuthoritativeServiceAnalyticsObserverV2() { }

        public bool RecordEnergyResult(string operationKey, int wave, int amount, bool isGrant, string reason,
            AuthoritativeLedgerResultV2 ledger) => false;
        public bool RecordSettlementGold(string operationKey, int wave, int amount, string reason,
            AuthoritativeLedgerResultV2 ledger) => false;
        public bool RecordMerchantOpen(string operationKey, string merchantId, string result, string reason) => false;
        public bool RecordMerchantOffer(string operationKey, string merchantId, string offerId,
            string currencyType) => false;
        public bool RecordMerchantPurchase(string operationKey, string merchantId, string offerId,
            string currencyType, string result, string reason, AuthoritativeLedgerResultV2 ledger) => false;
        public bool RecordRankSnapshot(string operationKey, int rankValue, string reason) => false;
        public bool RecordRankChange(string operationKey, int rankValue, string reason) => false;
        public bool RecordLeaderboardSnapshot(string operationKey, string period, int rankValue) => false;
    }

    /// <summary>
    /// Records only typed, sanitized responses already returned by a server boundary. It never
    /// calls a service or treats a local balance mutation as authoritative.
    /// </summary>
    public sealed class AuthoritativeServiceAnalyticsAdapterV2 : IAuthoritativeServiceAnalyticsObserverV2
    {
        private readonly AnalyticsRunSessionV2 session;
        private readonly string side;

        public AuthoritativeServiceAnalyticsAdapterV2(
            AnalyticsRunSessionV2 session,
            string side = AnalyticsSides.Player)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.side = side ?? string.Empty;
        }

        public string LastError { get; private set; } = string.Empty;

        public bool RecordEnergyResult(
            string operationKey,
            int wave,
            int amount,
            bool isGrant,
            string reason,
            AuthoritativeLedgerResultV2 ledger)
        {
            if (!ValidateOperationKey(operationKey) || amount <= 0 || string.IsNullOrWhiteSpace(reason) ||
                !ValidateLedger(ledger))
            {
                return false;
            }

            var eventName = isGrant ? AnalyticsEventNamesV2.EnergyGrant : AnalyticsEventNamesV2.EnergySpend;
            var recorded = Record(
                eventName,
                wave,
                "service:" + operationKey + ":energy",
                value =>
                {
                    value.energy_amount = amount;
                    value.reason = reason;
                });
            recorded &= RecordLedger(operationKey, wave, ledger);
            return recorded;
        }

        public bool RecordSettlementGold(
            string operationKey,
            int wave,
            int amount,
            string reason,
            AuthoritativeLedgerResultV2 ledger)
        {
            if (!ValidateOperationKey(operationKey) || amount < 0 || string.IsNullOrWhiteSpace(reason) ||
                !ValidateLedger(ledger))
            {
                return false;
            }

            var recorded = Record(
                AnalyticsEventNamesV2.SettlementGold,
                wave,
                "service:" + operationKey + ":gold",
                value =>
                {
                    value.gold_amount = amount;
                    value.reason = reason;
                });
            recorded &= RecordLedger(operationKey, wave, ledger);
            return recorded;
        }

        public bool RecordMerchantOpen(string operationKey, string merchantId, string result, string reason)
        {
            return RecordMerchant(
                AnalyticsEventNamesV2.MerchantOpen,
                operationKey,
                merchantId,
                string.Empty,
                string.Empty,
                result,
                reason);
        }

        public bool RecordMerchantOffer(
            string operationKey,
            string merchantId,
            string offerId,
            string currencyType)
        {
            return RecordMerchant(
                AnalyticsEventNamesV2.MerchantOffer,
                operationKey,
                merchantId,
                offerId,
                currencyType,
                "shown",
                string.Empty);
        }

        public bool RecordMerchantPurchase(
            string operationKey,
            string merchantId,
            string offerId,
            string currencyType,
            string result,
            string reason,
            AuthoritativeLedgerResultV2 ledger)
        {
            if (!ValidateLedger(ledger))
            {
                return false;
            }

            var recorded = RecordMerchant(
                AnalyticsEventNamesV2.MerchantPurchase,
                operationKey,
                merchantId,
                offerId,
                currencyType,
                result,
                reason);
            if (!recorded)
            {
                return false;
            }

            return RecordLedger(operationKey, 0, ledger);
        }

        public bool RecordRankSnapshot(string operationKey, int rankValue, string reason)
        {
            return RecordRank(AnalyticsEventNamesV2.RankSnapshot, operationKey, rankValue, reason);
        }

        public bool RecordRankChange(string operationKey, int rankValue, string reason)
        {
            return RecordRank(AnalyticsEventNamesV2.RankChange, operationKey, rankValue, reason);
        }

        public bool RecordLeaderboardSnapshot(string operationKey, string period, int rankValue)
        {
            if (!ValidateOperationKey(operationKey) || string.IsNullOrWhiteSpace(period) || rankValue < 0)
            {
                LastError = "Leaderboard response requires operation key, period and non-negative rank.";
                return false;
            }

            return Record(
                AnalyticsEventNamesV2.LeaderboardSnapshot,
                0,
                "service:" + operationKey + ":leaderboard",
                value =>
                {
                    value.leaderboard_period = period;
                    value.rank_value = rankValue;
                });
        }

        private bool RecordMerchant(
            string eventName,
            string operationKey,
            string merchantId,
            string offerId,
            string currencyType,
            string result,
            string reason)
        {
            if (!ValidateOperationKey(operationKey) || string.IsNullOrWhiteSpace(merchantId) ||
                (eventName != AnalyticsEventNamesV2.MerchantOpen && string.IsNullOrWhiteSpace(offerId)))
            {
                LastError = "Merchant response requires operation key, merchant ID and applicable offer ID.";
                return false;
            }

            return Record(
                eventName,
                0,
                "service:" + operationKey + ":" + eventName,
                value =>
                {
                    value.merchant_id = merchantId;
                    value.offer_id = offerId;
                    value.currency_type = currencyType;
                    value.result = result;
                    value.reason = reason;
                });
        }

        private bool RecordRank(string eventName, string operationKey, int rankValue, string reason)
        {
            if (!ValidateOperationKey(operationKey) || rankValue < 0)
            {
                LastError = "Rank response requires operation key and non-negative rank.";
                return false;
            }

            return Record(
                eventName,
                0,
                "service:" + operationKey + ":" + eventName,
                value =>
                {
                    value.rank_value = rankValue;
                    value.reason = reason;
                });
        }

        private bool RecordLedger(string operationKey, int wave, AuthoritativeLedgerResultV2 ledger)
        {
            return Record(
                AnalyticsEventNamesV2.LedgerResult,
                wave,
                "service:" + operationKey + ":ledger",
                value =>
                {
                    value.ledger_operation = ledger.Operation;
                    value.ledger_status = ledger.Status;
                    value.transaction_ref_hash = ledger.TransactionRefHash;
                    value.idempotency_key_hash = ledger.IdempotencyKeyHash;
                    value.reason = ledger.Reason;
                });
        }

        private bool ValidateOperationKey(string operationKey)
        {
            if (!string.IsNullOrWhiteSpace(operationKey))
            {
                return true;
            }

            LastError = "Sanitized service operation key is required.";
            return false;
        }

        private bool ValidateLedger(AuthoritativeLedgerResultV2 ledger)
        {
            if (string.IsNullOrWhiteSpace(ledger.Operation) ||
                !AnalyticsLedgerStatuses.IsKnown(ledger.Status) ||
                (!AnalyticsSchemaV2.IsHashedLedgerReference(ledger.TransactionRefHash) &&
                 !AnalyticsSchemaV2.IsHashedLedgerReference(ledger.IdempotencyKeyHash)))
            {
                LastError = "Ledger response requires operation, known status and sha256 hashed reference.";
                return false;
            }

            return true;
        }

        private bool Record(string eventName, int wave, string dedupeKey, Action<AnalyticsEventV2> configure)
        {
            var result = session.Record(
                eventName,
                side,
                Math.Max(0, wave),
                dedupeKey,
                configure,
                out var error);
            LastError = error;
            return result == AnalyticsRecordResultV2.Accepted;
        }
    }
}
