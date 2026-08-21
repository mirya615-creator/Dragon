using DragonBound.Analytics;

namespace DragonBound.Runes
{
    /// <summary>Optional presentation seam that records the typed loadout result.</summary>
    public sealed class RuneLoadoutAnalyticsBridge
    {
        private readonly RuneAnalyticsAdapterV2 analytics;
        private int sequence;

        public RuneLoadoutAnalyticsBridge(RuneAnalyticsAdapterV2 analytics)
        {
            this.analytics = analytics;
        }

        public void RecordAssign(string heroId, string runeId, bool accepted, string reason)
        {
            RecordOperation("loadout_assign", heroId, runeId, accepted, reason);
        }

        public void RecordUnequip(string heroId, bool accepted, string reason)
        {
            RecordOperation("loadout_unequip", heroId, string.Empty, accepted, reason);
        }

        public void RecordCraft(string runeId, bool accepted, string reason)
        {
            RecordOperation("craft", string.Empty, runeId, accepted, reason);
        }

        public void RecordGate(string operation, int accountDay, string reason)
        {
            if (analytics == null) return;
            analytics.RecordGateRejection(
                new RuneGateRejectionObservationV2(
                    NextKey("gate-" + operation), 0, operation, accountDay, reason),
                out _);
        }

        private void RecordOperation(
            string operation,
            string heroId,
            string runeId,
            bool accepted,
            string reason)
        {
            if (analytics == null) return;
            var observation = new RuneLoadoutOperationObservationV2(
                NextKey(operation), 0, heroId, runeId, accepted, reason);
            switch (operation)
            {
                case "loadout_assign": analytics.RecordLoadoutAssign(observation, out _); break;
                case "loadout_unequip": analytics.RecordLoadoutUnequip(observation, out _); break;
                case "craft": analytics.RecordCraft(observation, out _); break;
            }
        }

        private string NextKey(string prefix)
        {
            sequence++;
            return prefix + "-" + sequence;
        }
    }
}
