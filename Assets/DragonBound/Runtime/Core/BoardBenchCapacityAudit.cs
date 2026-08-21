using System;
using System.Collections.Generic;
using DragonBound.AI;
using DragonBound.Combat;
using DragonBound.Grid;
using DragonBound.Recruitment;

namespace DragonBound.Core
{
    public enum ComponentDiscardCapacityCause
    {
        NoCapacityAnywhere,
        FormableRecipeButBlocked,
        NoRecipeMateOwned,
        DuplicateSharedCore,
        MovableButAiDidNotMove,
        MergeCouldFreeSpace,
        ForgePickAvailableButNotUsed,
        Other
    }

    /// <summary>Per-run, read-only capacity telemetry. It never makes a placement decision.</summary>
    public sealed class BoardBenchCapacitySideRun
    {
        private readonly int[] occupancyHistogram = new int[101];
        private readonly int[] freeCellsHistogram = new int[41];
        private readonly HashSet<string> uniqueCapacityBlocks = new HashSet<string>(StringComparer.Ordinal);
        private bool boardFull;
        private bool benchFull;
        private float boardFullStartedAt;
        private float benchFullStartedAt;
        private int lastFreeCells;
        private int lastMergeAvailable;

        public int Samples { get; private set; }
        public double OccupancyRatioTotal { get; private set; }
        public int BasicUnitCells { get; private set; }
        public int UnpairedComponentCells { get; private set; }
        public int PairLinkComponentCells { get; private set; }
        public int OtherCells { get; private set; }
        public int BoardFullEnterCount { get; private set; }
        public int BoardFullExitCount { get; private set; }
        public float TotalBoardFullDuration { get; private set; }
        public float LongestBoardFullDuration { get; private set; }
        public int BenchFullEnterCount { get; private set; }
        public int BenchFullExitCount { get; private set; }
        public float TotalBenchFullDuration { get; private set; }
        public float LongestBenchFullDuration { get; private set; }
        public int MergeAvailableSamples { get; private set; }
        public int MergePerformedCount { get; private set; }
        public int DiscardedComponents { get; private set; }
        public int AvoidableDiscards { get; private set; }
        public int UnavoidableDiscards { get; private set; }
        public readonly Dictionary<ComponentDiscardCapacityCause, int> DiscardsByCause =
            new Dictionary<ComponentDiscardCapacityCause, int>();
        public int RawBoardCapacityFailures { get; private set; }
        public int UniqueBoardCapacityBlocks => uniqueCapacityBlocks.Count;
        public int RetryBoardCapacityFailures => Math.Max(0, RawBoardCapacityFailures - UniqueBoardCapacityBlocks);
        public float ActiveRunTime { get; private set; }
        public readonly Dictionary<int, BoardBenchWaveSnapshot> WaveSnapshots = new Dictionary<int, BoardBenchWaveSnapshot>();
        public void CaptureWave(int wave, BoardGrid board, BoardRecruitDestination destination)
        {
            if (wave < 1 || WaveSnapshots.ContainsKey(wave)) return;
            var open = board.GetPositions(CellType.Battle).Count; var occupied = 0; var basic = 0; var unpaired = 0; var paired = 0;
            foreach (var position in board.GetPositions(CellType.Battle))
            {
                if (!board.TryGetOccupant(position, out var id) || !destination.TryGetCard(id, out var card)) continue;
                occupied++;
                if (card.Kind == RecruitItemKind.BasicUnit) basic++;
                else if (card.Kind == RecruitItemKind.HeroComponent && destination.TryGetPairLinkForComponent(id, out _)) paired++;
                else if (card.Kind == RecruitItemKind.HeroComponent) unpaired++;
            }
            WaveSnapshots[wave] = new BoardBenchWaveSnapshot(open, occupied, Math.Max(0, open - occupied), basic, unpaired, paired, destination.CampCount);
        }

        public void RecordTick(float time, float deltaTime, BoardGrid board, BoardRecruitDestination destination, BasicUnitAiController controller)
        {
            var open = board.GetPositions(CellType.Battle).Count;
            var occupied = 0;
            var basic = 0;
            var unpaired = 0;
            var paired = 0;
            foreach (var position in board.GetPositions(CellType.Battle))
            {
                if (!board.TryGetOccupant(position, out var id) || !destination.TryGetCard(id, out var card))
                {
                    continue;
                }

                occupied++;
                if (card.Kind == RecruitItemKind.BasicUnit) basic++;
                else if (card.Kind == RecruitItemKind.HeroComponent && destination.TryGetPairLinkForComponent(id, out _)) paired++;
                else if (card.Kind == RecruitItemKind.HeroComponent) unpaired++;
            }

            var free = Math.Max(0, open - occupied);
            var ratio = open == 0 ? 0f : (float)occupied / open;
            Samples++;
            ActiveRunTime += deltaTime;
            OccupancyRatioTotal += ratio;
            BasicUnitCells += basic;
            UnpairedComponentCells += unpaired;
            PairLinkComponentCells += paired;
            OtherCells += Math.Max(0, occupied - basic - unpaired - paired);
            occupancyHistogram[Math.Min(100, Math.Max(0, (int)Math.Round(ratio * 100f)))]++;
            freeCellsHistogram[Math.Min(freeCellsHistogram.Length - 1, free)]++;
            lastFreeCells = free;
            lastMergeAvailable = CountMergePairs(destination.GetBoardCards());
            if (lastMergeAvailable > 0) MergeAvailableSamples++;

            UpdateFullState(time, free == 0, true);
            UpdateFullState(time, destination.CampCount >= board.GetPositions(CellType.Bench).Count, false);
            foreach (var pending in controller.PendingFormableRecipes)
            {
                if (pending.LastFailureReason == AiRecipeBlockedReason.BoardCapacity)
                {
                    uniqueCapacityBlocks.Add(pending.Key);
                }
            }
        }

        public void RecordRecruitment(RecruitmentAttempt attempt, BoardRecruitDestination destination, BoardGrid board, BasicUnitAiController controller)
        {
            if (attempt.Status != RecruitmentStatus.Success) return;
            foreach (var card in attempt.RefreshedCards)
            {
                if (card.Kind != RecruitItemKind.HeroComponent) continue;
                DiscardedComponents++;
                var cause = lastFreeCells == 0 && destination.CampCount >= board.GetPositions(CellType.Bench).Count
                    ? ComponentDiscardCapacityCause.NoCapacityAnywhere
                    : controller.BlockedRecipeCount > 0
                        ? ComponentDiscardCapacityCause.FormableRecipeButBlocked
                        : lastMergeAvailable > 0
                            ? ComponentDiscardCapacityCause.MergeCouldFreeSpace
                            : ComponentDiscardCapacityCause.NoRecipeMateOwned;
                DiscardsByCause.TryGetValue(cause, out var count);
                DiscardsByCause[cause] = count + 1;
                if (cause == ComponentDiscardCapacityCause.NoCapacityAnywhere || cause == ComponentDiscardCapacityCause.NoRecipeMateOwned)
                    UnavoidableDiscards++;
                else
                    AvoidableDiscards++;
            }
        }

        public void RecordMergePerformed() => MergePerformedCount++;

        public void Complete(float time, BoardRecruitDestination destination, BoardGrid board)
        {
            CloseState(time, boardFull, true);
            CloseState(time, benchFull, false);
        }

        public double OccupancyPercentile(int percentile)
        {
            if (Samples == 0) return 0d;
            var target = (int)Math.Ceiling(Samples * percentile / 100d);
            var running = 0;
            for (var index = 0; index < occupancyHistogram.Length; index++)
            {
                running += occupancyHistogram[index];
                if (running >= target) return index / 100d;
            }
            return 1d;
        }

        public double FreeCellProbability(int freeCells)
        {
            return Samples == 0 || freeCells < 0 || freeCells >= freeCellsHistogram.Length ? 0d : (double)freeCellsHistogram[freeCells] / Samples;
        }

        public double FreeCellProbabilityAtLeast(int freeCells)
        {
            var count = 0;
            for (var index = Math.Max(0, freeCells); index < freeCellsHistogram.Length; index++) count += freeCellsHistogram[index];
            return Samples == 0 ? 0d : (double)count / Samples;
        }

        public void FinalizeRecipeFailures(BasicUnitAiController controller)
        {
            controller.RecipeFailureCounts.TryGetValue(AiRecipeBlockedReason.BoardCapacity, out var count);
            RawBoardCapacityFailures = count;
        }

        private void UpdateFullState(float time, bool isFull, bool isBoard)
        {
            var wasFull = isBoard ? boardFull : benchFull;
            if (isFull == wasFull) return;
            if (isFull)
            {
                if (isBoard) { boardFull = true; boardFullStartedAt = time; BoardFullEnterCount++; }
                else { benchFull = true; benchFullStartedAt = time; BenchFullEnterCount++; }
            }
            else
            {
                CloseState(time, wasFull, isBoard);
                if (isBoard) { boardFull = false; BoardFullExitCount++; }
                else { benchFull = false; BenchFullExitCount++; }
            }
        }

        private void CloseState(float time, bool wasFull, bool isBoard)
        {
            if (!wasFull) return;
            var duration = Math.Max(0f, time - (isBoard ? boardFullStartedAt : benchFullStartedAt));
            if (isBoard) { TotalBoardFullDuration += duration; LongestBoardFullDuration = Math.Max(LongestBoardFullDuration, duration); }
            else { TotalBenchFullDuration += duration; LongestBenchFullDuration = Math.Max(LongestBenchFullDuration, duration); }
        }

        private static int CountMergePairs(IReadOnlyList<RecruitCard> cards)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var card in cards)
            {
                if (card.Kind != RecruitItemKind.BasicUnit || card.Level >= BasicUnitCatalog.MaxLevel) continue;
                var key = card.ConfigId + "|" + card.Level;
                counts.TryGetValue(key, out var count);
                counts[key] = count + 1;
            }
            var result = 0;
            foreach (var pair in counts) result += pair.Value / 2;
            return result;
        }
    }

    public readonly struct BoardBenchWaveSnapshot
    {
        public BoardBenchWaveSnapshot(int open, int occupied, int free, int basic, int unpaired, int paired, int bench)
        { OpenCellCount = open; OccupiedOpenCellCount = occupied; FreeOpenCellCount = free; BasicUnitCells = basic; UnpairedComponentCells = unpaired; PairLinkComponentCells = paired; BenchOccupiedCount = bench; }
        public int OpenCellCount { get; }
        public int OccupiedOpenCellCount { get; }
        public int FreeOpenCellCount { get; }
        public int BasicUnitCells { get; }
        public int UnpairedComponentCells { get; }
        public int PairLinkComponentCells { get; }
        public int BenchOccupiedCount { get; }
        public double OccupancyRatio => OpenCellCount == 0 ? 0d : (double)OccupiedOpenCellCount / OpenCellCount;
    }

    public sealed class BoardBenchCapacityAuditReport
    {
        public BoardBenchCapacityAuditReport(int sampleCount)
        {
            SampleCount = sampleCount;
            Player = new BoardBenchCapacityAuditAggregate(sampleCount, "Player");
            AI = new BoardBenchCapacityAuditAggregate(sampleCount, "AI");
        }
        public int SampleCount { get; }
        public BoardBenchCapacityAuditAggregate Player { get; }
        public BoardBenchCapacityAuditAggregate AI { get; }
        public string FormatReport() => "BOARD_BENCH_CAPACITY_AUDIT_V1 SampleCount=" + SampleCount + "\n" + Player.FormatReport() + AI.FormatReport();
    }

    public sealed class BoardBenchCapacityAuditAggregate
    {
        private readonly int sampleCount;
        private readonly string label;
        private readonly List<BoardBenchCapacitySideRun> runs = new List<BoardBenchCapacitySideRun>();
        internal BoardBenchCapacityAuditAggregate(int sampleCount, string label) { this.sampleCount = sampleCount; this.label = label; }
        internal void Add(BoardBenchCapacitySideRun run) { if (run != null) runs.Add(run); }
        public int RunCount => runs.Count;
        public double AverageOccupancyRatio => Average(run => run.Samples == 0 ? 0d : run.OccupancyRatioTotal / run.Samples);
        public double P50OccupancyRatio => Percentile(run => run.OccupancyPercentile(50), 50);
        public double P75OccupancyRatio => Percentile(run => run.OccupancyPercentile(75), 75);
        public double P90OccupancyRatio => Percentile(run => run.OccupancyPercentile(90), 90);
        public double P95OccupancyRatio => Percentile(run => run.OccupancyPercentile(95), 95);
        public double P99OccupancyRatio => Percentile(run => run.OccupancyPercentile(99), 99);
        public double BoardFullActiveTimeRate => Average(run => run.ActiveRunTime == 0f ? 0d : run.TotalBoardFullDuration / run.ActiveRunTime);
        public double BenchFullActiveTimeRate => Average(run => run.ActiveRunTime == 0f ? 0d : run.TotalBenchFullDuration / run.ActiveRunTime);
        public int RawBoardCapacityFailures => Sum(run => run.RawBoardCapacityFailures);
        public int UniqueBoardCapacityBlocks => Sum(run => run.UniqueBoardCapacityBlocks);
        public int RetryBoardCapacityFailures => Sum(run => run.RetryBoardCapacityFailures);
        public int AvoidableDiscards => Sum(run => run.AvoidableDiscards);
        public int UnavoidableDiscards => Sum(run => run.UnavoidableDiscards);
        public int ComponentDiscards => Sum(run => run.DiscardedComponents);
        public string FormatReport()
        {
            var text = "[" + label + "] Occupancy Average=" + AverageOccupancyRatio.ToString("P2") + " P50=" + P50OccupancyRatio.ToString("P2") + " P75=" + P75OccupancyRatio.ToString("P2") + " P90=" + P90OccupancyRatio.ToString("P2") + " P95=" + P95OccupancyRatio.ToString("P2") + " P99=" + P99OccupancyRatio.ToString("P2") + " P(Free=0)=" + FreeProbability(0).ToString("P2") + " P(Free=1)=" + FreeProbability(1).ToString("P2") + " P(Free=2)=" + FreeProbability(2).ToString("P2") + " P(Free>=3)=" + FreeProbabilityAtLeast(3).ToString("P2") + "\n";
            text += "[" + label + "] BoardFullTime=" + BoardFullActiveTimeRate.ToString("P2") + " BenchFullTime=" + BenchFullActiveTimeRate.ToString("P2") + " RawBoardCapacityFailures=" + RawBoardCapacityFailures + " UniqueBoardCapacityBlocks=" + UniqueBoardCapacityBlocks + " RetryBoardCapacityFailures=" + RetryBoardCapacityFailures + " AvoidableDiscard=" + AvoidableDiscards + " UnavoidableDiscard=" + UnavoidableDiscards + "\n";
            foreach (var wave in new[] { 3, 6, 8, 10, 12, 16 }) { var s = WaveSnapshot(wave); text += "[" + label + "] W" + wave + " Occupancy=" + s.OccupancyRatio.ToString("P2") + " Open=" + s.OpenCellCount + " Occupied=" + s.OccupiedOpenCellCount + " Free=" + s.FreeOpenCellCount + " Basic=" + s.BasicUnitCells + " Unpaired=" + s.UnpairedComponentCells + " PairLinkCells=" + s.PairLinkComponentCells + " Bench=" + s.BenchOccupiedCount + "\n"; }
            return text;
        }
        private double Average(Func<BoardBenchCapacitySideRun, double> selector) { if (runs.Count == 0) return 0d; var total = 0d; foreach (var run in runs) total += selector(run); return total / runs.Count; }
        private int Sum(Func<BoardBenchCapacitySideRun, int> selector) { var total = 0; foreach (var run in runs) total += selector(run); return total; }
        private double Percentile(Func<BoardBenchCapacitySideRun, double> selector, int percentile) { if (runs.Count == 0) return 0d; var values = new List<double>(); foreach (var run in runs) values.Add(selector(run)); values.Sort(); return values[Math.Min(values.Count - 1, (int)Math.Ceiling(values.Count * percentile / 100d) - 1)]; }
        private double FreeProbability(int free) => Average(run => run.FreeCellProbability(free));
        private double FreeProbabilityAtLeast(int free) => Average(run => run.FreeCellProbabilityAtLeast(free));
        private BoardBenchWaveSnapshot WaveSnapshot(int wave)
        { var open = 0d; var occupied = 0d; var free = 0d; var basic = 0d; var unpaired = 0d; var paired = 0d; var bench = 0d; var count = 0; foreach (var run in runs) if (run.WaveSnapshots.TryGetValue(wave, out var s)) { open += s.OpenCellCount; occupied += s.OccupiedOpenCellCount; free += s.FreeOpenCellCount; basic += s.BasicUnitCells; unpaired += s.UnpairedComponentCells; paired += s.PairLinkComponentCells; bench += s.BenchOccupiedCount; count++; } return count == 0 ? default : new BoardBenchWaveSnapshot((int)Math.Round(open / count), (int)Math.Round(occupied / count), (int)Math.Round(free / count), (int)Math.Round(basic / count), (int)Math.Round(unpaired / count), (int)Math.Round(paired / count), (int)Math.Round(bench / count)); }
    }
}
