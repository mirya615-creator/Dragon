using System;
using System.Collections.Generic;
using System.Text;
using DragonBound.Grid;
using DragonBound.Recruitment;
using UnityEngine;

namespace DragonBound.Core
{
    [Serializable]
    public sealed class PressureRecruitmentRecord
    {
        public int Wave { get; internal set; }
        public float RunTime { get; internal set; }
        public int Cost { get; internal set; }
        public long Sequence { get; internal set; }
    }

    [Serializable]
    public sealed class PressureWaveSnapshot
    {
        public TeamSide Side { get; internal set; }
        public int Wave { get; internal set; }
        public float RunTime { get; internal set; }
        public int Resources { get; internal set; }
        public int SuccessfulRecruitCount { get; internal set; }
        public int DeliveredComponents { get; internal set; }
        public int RemainingComponents { get; internal set; }
        public int OpenCells { get; internal set; }
        public int OccupiedCells { get; internal set; }
        public int BenchOccupied { get; internal set; }
        public int Heroes { get; internal set; }
        public int ShovelsGenerated { get; internal set; }
        public int ShovelsUsed { get; internal set; }
        public int EnemiesKilled { get; internal set; }
        public int EnemiesReachedGoal { get; internal set; }
        public int BaseHP { get; internal set; }
    }

    /// <summary>Read-only per-side pressure counters. It owns no gameplay state.</summary>
    [Serializable]
    public sealed class PressureRunSideDiagnostics
    {
        private readonly TeamState team;
        private readonly RecruitmentService recruitment;
        private readonly BoardRecruitDestination destination;
        private readonly ShovelUnlockService shovelUnlocks;
        private readonly BoardGrid board;
        private int previousRemainingComponents;

        internal PressureRunSideDiagnostics(
            TeamSide side,
            TeamState team,
            RecruitmentService recruitment,
            BoardRecruitDestination destination,
            ShovelUnlockService shovelUnlocks)
        {
            Side = side;
            this.team = team ?? throw new ArgumentNullException(nameof(team));
            this.recruitment = recruitment ?? throw new ArgumentNullException(nameof(recruitment));
            this.destination = destination ?? throw new ArgumentNullException(nameof(destination));
            this.shovelUnlocks = shovelUnlocks;
            board = destination.Board;
            StartingResources = team.Resources;
            previousRemainingComponents = recruitment.RemainingHeroComponents;
            ComponentBagExhaustedAtRecruit = -1;
            ComponentBagExhaustedAtWave = -1;
            ComponentBagExhaustedAtTime = -1f;
        }

        public TeamSide Side { get; }

        public int CurrentWave { get; internal set; }
        public float ElapsedRunTime { get; internal set; }
        public int BaseHP => team.HatchlingHealth;
        public int SpawnedEnemies { get; internal set; }
        public int KilledEnemies { get; internal set; }
        public int ReachedGoalEnemies { get; internal set; }
        public int AliveEnemies { get; internal set; }

        public int StartingResources { get; }
        public int EarnedResources => team.Resources - StartingResources + SpentResources;
        public int SpentResources { get; internal set; }
        public int CurrentResources => team.Resources;
        public int SuccessfulRecruitCount => recruitment.CompletedRecruitments;
        public IReadOnlyList<PressureRecruitmentRecord> RecruitmentRecords => recruitmentRecords;
        public int DeliveredComponentCount => recruitment.DrawnHeroComponents;
        public int RemainingComponentCount => recruitment.RemainingHeroComponents;
        public int DiscardedComponentCount => recruitment.DiscardedHeroComponents;
        public int ComponentBagExhaustedAtRecruit { get; internal set; }
        public int ComponentBagExhaustedAtWave { get; internal set; }
        public float ComponentBagExhaustedAtTime { get; internal set; }

        public int ShovelsGenerated { get; internal set; }
        public int ShovelsGrantedExternally { get; internal set; }
        public int ShovelsUsed { get; internal set; }
        public int ShovelsDiscardedByRecruitOverwrite { get; internal set; }
        public int ShovelPityTriggers { get; internal set; }
        public int CurrentAvailableShovels => shovelUnlocks?.AvailableShovelCount ?? 0;

        public int OpenCellCount => board.GetPositions(CellType.Battle).Count;
        public int LockedCellCount => board.GetPositions(CellType.Locked).Count;
        public int OccupiedBoardCellCount => destination.DeployedCount;
        public int BenchOccupiedCount => destination.CampCount;
        public int CampOccupiedCount => destination.CampCount;
        public int FormedPairLinkCount { get; internal set; }
        public int CurrentHeroCount => destination.ActivePairLinkCount;

        public int RecruitOverwriteCount { get; internal set; }
        public int OverwrittenBasicUnitCount { get; internal set; }
        public int OverwrittenComponentCount { get; internal set; }
        public int OverwrittenShovelCount { get; internal set; }
        public int BoardPlacementFailureCount { get; internal set; }
        public int BenchPlacementFailureCount { get; internal set; }
        public int FirstBoardPressureWave { get; internal set; } = -1;
        public float FirstBoardPressureTime { get; internal set; } = -1f;

        private readonly List<PressureRecruitmentRecord> recruitmentRecords =
            new List<PressureRecruitmentRecord>();

        internal bool Sync(
            float runTime,
            int wave,
            int spawned,
            int killed,
            int reachedGoal,
            int alive)
        {
            CurrentWave = wave;
            ElapsedRunTime = runTime;
            SpawnedEnemies = spawned;
            KilledEnemies = killed;
            ReachedGoalEnemies = reachedGoal;
            AliveEnemies = alive;

            var remaining = recruitment.RemainingHeroComponents;
            var becameExhausted = previousRemainingComponents > 0 && remaining == 0 && ComponentBagExhaustedAtRecruit < 0;
            if (becameExhausted)
            {
                ComponentBagExhaustedAtRecruit = recruitment.CompletedRecruitments;
                ComponentBagExhaustedAtWave = wave;
                ComponentBagExhaustedAtTime = runTime;
            }

            previousRemainingComponents = remaining;
            if (FirstBoardPressureWave < 0 && OpenCellCount > 0 && OccupiedBoardCellCount >= OpenCellCount)
            {
                FirstBoardPressureWave = wave;
                FirstBoardPressureTime = runTime;
            }

            return becameExhausted;
        }

        internal void RecordRecruitment(RecruitmentAttempt attempt, int wave, float runTime, Action<string> log)
        {
            if (attempt.Status != RecruitmentStatus.Success)
            {
                return;
            }

            SpentResources += attempt.Cost;
            recruitmentRecords.Add(new PressureRecruitmentRecord
            {
                Wave = wave,
                RunTime = runTime,
                Cost = attempt.Cost,
                Sequence = attempt.Sequence
            });
            log($"RECRUIT_SUCCESS Side={Side} Wave={wave} RunTime={runTime:0.00} " +
                $"Sequence={attempt.Sequence} Cost={attempt.Cost}");

            var generated = 0;
            foreach (var card in attempt.Batch.Cards)
            {
                if (card.Kind == RecruitItemKind.Shovel)
                {
                    generated++;
                }
            }

            if (generated > 0)
            {
                ShovelsGenerated += generated;
                log($"SHOVEL_GENERATED Side={Side} Count={generated} Wave={wave} RunTime={runTime:0.00}");
            }

            if (!attempt.RefreshedBench)
            {
                return;
            }

            RecruitOverwriteCount++;
            foreach (var card in attempt.RefreshedCards)
            {
                switch (card.Kind)
                {
                    case RecruitItemKind.BasicUnit:
                        OverwrittenBasicUnitCount++;
                        break;
                    case RecruitItemKind.HeroComponent:
                        OverwrittenComponentCount++;
                        log($"COMPONENT_DISCARDED Side={Side} RuntimeId={card.RuntimeId} " +
                            $"Wave={wave} RunTime={runTime:0.00}");
                        break;
                    case RecruitItemKind.Shovel:
                        OverwrittenShovelCount++;
                        ShovelsDiscardedByRecruitOverwrite++;
                        log($"SHOVEL_DISCARDED Side={Side} RuntimeId={card.RuntimeId} " +
                            $"Wave={wave} RunTime={runTime:0.00}");
                        break;
                }
            }
        }

        internal void RecordExternalShovels(int count, int wave, float runTime, Action<string> log)
        {
            ShovelsGrantedExternally += count;
            log($"SHOVEL_GENERATED Side={Side} ExternalCount={count} Wave={wave} RunTime={runTime:0.00}");
        }

        internal void RecordShovelUsed(GridPosition position, int wave, float runTime, Action<string> log)
        {
            ShovelsUsed++;
            log($"SHOVEL_USED Side={Side} Cell={position} Wave={wave} RunTime={runTime:0.00}");
        }

        internal void RecordPityTrigger(int count)
        {
            ShovelPityTriggers = Math.Max(ShovelPityTriggers, count);
        }

        internal void RecordPairLinkFormed()
        {
            FormedPairLinkCount++;
        }
    }

    /// <summary>
    /// Development-only observer for the pressure race. It never drives recruitment, combat,
    /// unlocking, saving, or MatchState transitions.
    /// </summary>
    public sealed class PressureRunDiagnostics : IDisposable
    {
        private static readonly HashSet<int> SnapshotWaves = new HashSet<int>
        {
            1, 3, 6, 8, 11, 12, 15, 16, 20
        };

        private readonly MatchController match;
        private readonly TwentyWavePressureRuntime runtime;
        private readonly RecruitmentService playerRecruitment;
        private readonly RecruitmentService aiRecruitment;
        private readonly BoardRecruitDestination playerDestination;
        private readonly BoardRecruitDestination aiDestination;
        private readonly BoardGrid playerBoard;
        private readonly BoardGrid aiBoard;
        private readonly ShovelUnlockService playerShovels;
        private readonly ShovelUnlockService aiShovels;
        private readonly HashSet<int> capturedSnapshotWaves = new HashSet<int>();
        private float runTime;
        private bool disposed;
        private bool summaryReported;
        private DragPlacementController playerDrag;
        private DragPlacementController aiDrag;

        public PressureRunDiagnostics(
            MatchController match,
            TwentyWavePressureRuntime runtime,
            RecruitmentService playerRecruitment,
            RecruitmentService aiRecruitment,
            BoardRecruitDestination playerDestination,
            BoardRecruitDestination aiDestination,
            ShovelUnlockService playerShovels = null,
            ShovelUnlockService aiShovels = null)
        {
            this.match = match ?? throw new ArgumentNullException(nameof(match));
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.playerRecruitment = playerRecruitment ?? throw new ArgumentNullException(nameof(playerRecruitment));
            this.aiRecruitment = aiRecruitment ?? throw new ArgumentNullException(nameof(aiRecruitment));
            this.playerDestination = playerDestination ?? throw new ArgumentNullException(nameof(playerDestination));
            this.aiDestination = aiDestination ?? throw new ArgumentNullException(nameof(aiDestination));
            playerBoard = playerDestination.Board;
            aiBoard = aiDestination.Board;
            this.playerShovels = playerShovels;
            this.aiShovels = aiShovels;
            Player = new PressureRunSideDiagnostics(TeamSide.Player, match.Player, playerRecruitment, playerDestination, playerShovels);
            AI = new PressureRunSideDiagnostics(TeamSide.AI, match.AI, aiRecruitment, aiDestination, aiShovels);
            Subscribe();
        }

        public PressureRunSideDiagnostics Player { get; }
        public PressureRunSideDiagnostics AI { get; }
        public IReadOnlyList<PressureWaveSnapshot> Snapshots => snapshots;
        public float ElapsedRunTime => runTime;
        public string RunSeed => match.RunSeed.ToString();
        public string Result { get; private set; } = "Running";
        public int DeathWave { get; private set; } = -1;
        public bool IsEnabled { get; } = true;

        private readonly List<PressureWaveSnapshot> snapshots = new List<PressureWaveSnapshot>();

        public void Tick(float deltaSeconds)
        {
            if (disposed || deltaSeconds <= 0f)
            {
                return;
            }

            if (runtime.IsGameplayRunning)
            {
                runTime += deltaSeconds;
            }

            SyncSides();
            CaptureWaveSnapshotIfNeeded();
            DetectTerminalState();
        }

        public string StopAndReport(string result = "DeveloperStopped")
        {
            if (!summaryReported)
            {
                Result = string.IsNullOrWhiteSpace(result) ? "DeveloperStopped" : result;
                SyncSides();
                summaryReported = true;
                Debug.Log(CreateSummary());
            }

            return CreateSummary();
        }

        /// <summary>Optional UI drag observers add failure counters without affecting drops.</summary>
        public void AttachDragControllers(
            DragPlacementController playerDragController,
            DragPlacementController aiDragController)
        {
            if (disposed)
            {
                return;
            }

            if (playerDrag != null)
            {
                playerDrag.Completed -= HandlePlayerDragCompleted;
            }

            if (aiDrag != null)
            {
                aiDrag.Completed -= HandleAiDragCompleted;
            }

            playerDrag = playerDragController;
            aiDrag = aiDragController;
            if (playerDrag != null)
            {
                playerDrag.Completed += HandlePlayerDragCompleted;
            }

            if (aiDrag != null)
            {
                aiDrag.Completed += HandleAiDragCompleted;
            }
        }

        public string CreateSummary()
        {
            var builder = new StringBuilder();
            builder.AppendLine("PressureRunSummary");
            builder.AppendLine($"RunSeed={RunSeed} Result={Result} DeathWave={DeathWave} RunDuration={runTime:0.00}");
            AppendSideSummary(builder, Player);
            AppendSideSummary(builder, AI);
            return builder.ToString();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Unsubscribe();
        }

        private void Subscribe()
        {
            runtime.CombatEmitted += HandleCombat;
            playerRecruitment.Attempted += HandlePlayerRecruitment;
            aiRecruitment.Attempted += HandleAiRecruitment;
            playerDestination.HeroPairLinked += HandlePlayerPairLinked;
            aiDestination.HeroPairLinked += HandleAiPairLinked;
            playerBoard.Changed += HandlePlayerBoardChanged;
            aiBoard.Changed += HandleAiBoardChanged;
            playerBoard.DropRejectedBecauseNoSpace += HandlePlayerDropRejected;
            aiBoard.DropRejectedBecauseNoSpace += HandleAiDropRejected;
            if (playerShovels != null)
            {
                playerShovels.ShovelGrantedExternally += HandlePlayerExternalShovels;
                playerShovels.ShovelUsed += HandlePlayerShovelUsed;
            }

            if (aiShovels != null)
            {
                aiShovels.ShovelGrantedExternally += HandleAiExternalShovels;
                aiShovels.ShovelUsed += HandleAiShovelUsed;
            }
        }

        private void Unsubscribe()
        {
            runtime.CombatEmitted -= HandleCombat;
            playerRecruitment.Attempted -= HandlePlayerRecruitment;
            aiRecruitment.Attempted -= HandleAiRecruitment;
            playerDestination.HeroPairLinked -= HandlePlayerPairLinked;
            aiDestination.HeroPairLinked -= HandleAiPairLinked;
            playerBoard.Changed -= HandlePlayerBoardChanged;
            aiBoard.Changed -= HandleAiBoardChanged;
            playerBoard.DropRejectedBecauseNoSpace -= HandlePlayerDropRejected;
            aiBoard.DropRejectedBecauseNoSpace -= HandleAiDropRejected;
            if (playerShovels != null)
            {
                playerShovels.ShovelGrantedExternally -= HandlePlayerExternalShovels;
                playerShovels.ShovelUsed -= HandlePlayerShovelUsed;
            }

            if (aiShovels != null)
            {
                aiShovels.ShovelGrantedExternally -= HandleAiExternalShovels;
                aiShovels.ShovelUsed -= HandleAiShovelUsed;
            }

            if (playerDrag != null)
            {
                playerDrag.Completed -= HandlePlayerDragCompleted;
            }

            if (aiDrag != null)
            {
                aiDrag.Completed -= HandleAiDragCompleted;
            }
        }

        private void SyncSides()
        {
            var playerHadNoPressure = Player.FirstBoardPressureWave < 0;
            var aiHadNoPressure = AI.FirstBoardPressureWave < 0;
            var playerBagBecameEmpty = Player.Sync(runTime, runtime.CurrentWave, runtime.PlayerTotalSpawned, runtime.PlayerTotalKilled, runtime.PlayerTotalReachedGoal, runtime.PlayerAliveEnemyCount);
            var aiBagBecameEmpty = AI.Sync(runTime, runtime.CurrentWave, runtime.AiTotalSpawned, runtime.AiTotalKilled, runtime.AiTotalReachedGoal, runtime.AiAliveEnemyCount);
            if (playerBagBecameEmpty)
            {
                LogEvent($"COMPONENT_BAG_EMPTY Side=Player Recruit={Player.ComponentBagExhaustedAtRecruit} " +
                    $"Wave={Player.ComponentBagExhaustedAtWave} RunTime={Player.ComponentBagExhaustedAtTime:0.00}");
            }

            if (aiBagBecameEmpty)
            {
                LogEvent($"COMPONENT_BAG_EMPTY Side=AI Recruit={AI.ComponentBagExhaustedAtRecruit} " +
                    $"Wave={AI.ComponentBagExhaustedAtWave} RunTime={AI.ComponentBagExhaustedAtTime:0.00}");
            }

            if (playerHadNoPressure && Player.FirstBoardPressureWave >= 0)
            {
                LogEvent($"BOARD_PRESSURE Side=Player Wave={Player.FirstBoardPressureWave} " +
                    $"RunTime={Player.FirstBoardPressureTime:0.00}");
            }

            if (aiHadNoPressure && AI.FirstBoardPressureWave >= 0)
            {
                LogEvent($"BOARD_PRESSURE Side=AI Wave={AI.FirstBoardPressureWave} " +
                    $"RunTime={AI.FirstBoardPressureTime:0.00}");
            }

            Player.RecordPityTrigger(playerRecruitment.ShovelPityTriggerCount);
            AI.RecordPityTrigger(aiRecruitment.ShovelPityTriggerCount);
        }

        private void CaptureWaveSnapshotIfNeeded()
        {
            var wave = runtime.CurrentWave;
            if (!SnapshotWaves.Contains(wave) || !capturedSnapshotWaves.Add(wave))
            {
                return;
            }

            snapshots.Add(CreateSnapshot(Player, wave));
            snapshots.Add(CreateSnapshot(AI, wave));
            LogEvent($"WAVE_SNAPSHOT Wave={wave} RunTime={runTime:0.00}");
        }

        private static PressureWaveSnapshot CreateSnapshot(PressureRunSideDiagnostics side, int wave)
        {
            return new PressureWaveSnapshot
            {
                Side = side.Side,
                Wave = wave,
                RunTime = side.ElapsedRunTime,
                Resources = side.CurrentResources,
                SuccessfulRecruitCount = side.SuccessfulRecruitCount,
                DeliveredComponents = side.DeliveredComponentCount,
                RemainingComponents = side.RemainingComponentCount,
                OpenCells = side.OpenCellCount,
                OccupiedCells = side.OccupiedBoardCellCount,
                BenchOccupied = side.BenchOccupiedCount,
                Heroes = side.CurrentHeroCount,
                ShovelsGenerated = side.ShovelsGenerated,
                ShovelsUsed = side.ShovelsUsed,
                EnemiesKilled = side.KilledEnemies,
                EnemiesReachedGoal = side.ReachedGoalEnemies,
                BaseHP = side.BaseHP
            };
        }

        private void DetectTerminalState()
        {
            if (summaryReported)
            {
                return;
            }

            if (match.State == MatchState.Victory || match.State == MatchState.Defeat)
            {
                Result = match.State.ToString();
                if (DeathWave < 0 && match.State == MatchState.Defeat)
                {
                    DeathWave = runtime.CurrentWave;
                }

                summaryReported = true;
                Debug.Log(CreateSummary());
            }
        }

        private void HandlePlayerRecruitment(RecruitmentAttempt attempt)
        {
            Player.RecordRecruitment(attempt, runtime.CurrentWave, runTime, LogEvent);
        }

        private void HandleAiRecruitment(RecruitmentAttempt attempt)
        {
            AI.RecordRecruitment(attempt, runtime.CurrentWave, runTime, LogEvent);
        }

        private void HandlePlayerPairLinked(HeroPairLinkedEvent value)
        {
            Player.RecordPairLinkFormed();
            LogEvent($"PAIRLINK_FORMED Side=Player PairLinkId={value.PairLink.PairLinkId} HeroId={value.PairLink.HeroId}");
        }

        private void HandleAiPairLinked(HeroPairLinkedEvent value)
        {
            AI.RecordPairLinkFormed();
            LogEvent($"PAIRLINK_FORMED Side=AI PairLinkId={value.PairLink.PairLinkId} HeroId={value.PairLink.HeroId}");
        }

        private void HandlePlayerBoardChanged(GridMutation mutation)
        {
            if (mutation.Kind == GridMutationKind.CellUnlocked)
            {
                LogEvent($"CELL_UNLOCKED Side=Player Cell={mutation.To} Wave={runtime.CurrentWave} RunTime={runTime:0.00}");
            }
        }

        private void HandleAiBoardChanged(GridMutation mutation)
        {
            if (mutation.Kind == GridMutationKind.CellUnlocked)
            {
                LogEvent($"CELL_UNLOCKED Side=AI Cell={mutation.To} Wave={runtime.CurrentWave} RunTime={runTime:0.00}");
            }
        }

        private void HandlePlayerDropRejected(GridDropRejectedBecauseNoSpace value)
        {
            Player.BoardPlacementFailureCount++;
        }

        private void HandleAiDropRejected(GridDropRejectedBecauseNoSpace value)
        {
            AI.BoardPlacementFailureCount++;
        }

        private void HandlePlayerDragCompleted(DragCompletion completion)
        {
            RecordBenchPlacementFailure(Player, playerBoard, completion);
        }

        private void HandleAiDragCompleted(DragCompletion completion)
        {
            RecordBenchPlacementFailure(AI, aiBoard, completion);
        }

        private void HandlePlayerExternalShovels(int count)
        {
            Player.RecordExternalShovels(count, runtime.CurrentWave, runTime, LogEvent);
        }

        private void HandleAiExternalShovels(int count)
        {
            AI.RecordExternalShovels(count, runtime.CurrentWave, runTime, LogEvent);
        }

        private void HandlePlayerShovelUsed(GridPosition position)
        {
            Player.RecordShovelUsed(position, runtime.CurrentWave, runTime, LogEvent);
        }

        private void HandleAiShovelUsed(GridPosition position)
        {
            AI.RecordShovelUsed(position, runtime.CurrentWave, runTime, LogEvent);
        }

        private void HandleCombat(CombatEvent value)
        {
            if (!value.Leaked)
            {
                return;
            }

            var side = value.Team == TeamSide.Player ? Player : AI;
            LogEvent($"BASE_DAMAGED Side={value.Team} HP={side.BaseHP} Wave={runtime.CurrentWave} RunTime={runTime:0.00}");
            if (side.BaseHP <= 0)
            {
                DeathWave = runtime.CurrentWave;
                LogEvent($"SIDE_DEFEATED Side={value.Team} Wave={runtime.CurrentWave} RunTime={runTime:0.00}");
            }
        }

        private static void RecordBenchPlacementFailure(
            PressureRunSideDiagnostics side,
            BoardGrid board,
            DragCompletion completion)
        {
            if (completion.Status != DragDropStatus.Reverted ||
                !board.TryGetCellType(completion.Origin, out var originType))
            {
                return;
            }

            // Bench-to-board pressure is counted from BoardGrid's no-space event. A reverted
            // board-origin drag is the remaining independent bench-placement failure class.
            if (originType == CellType.Battle)
            {
                side.BenchPlacementFailureCount++;
            }
        }

        private void LogEvent(string message)
        {
            Debug.Log($"PressureDiag {message}");
        }

        private static void AppendSideSummary(StringBuilder builder, PressureRunSideDiagnostics side)
        {
            builder.AppendLine($"{side.Side}:");
            builder.AppendLine(
                $"RecruitCount={side.SuccessfulRecruitCount} EarnedResources={side.EarnedResources} " +
                $"SpentResources={side.SpentResources} CurrentResources={side.CurrentResources}");
            builder.AppendLine(
                $"ComponentsDelivered={side.DeliveredComponentCount} ComponentsDiscarded={side.DiscardedComponentCount} " +
                $"ComponentsRemaining={side.RemainingComponentCount} " +
                $"ComponentBagExhaustionRecruit={side.ComponentBagExhaustedAtRecruit} " +
                $"ComponentBagExhaustionWave={side.ComponentBagExhaustedAtWave}");
            builder.AppendLine(
                $"ShovelsGenerated={side.ShovelsGenerated} ShovelsGrantedExternally={side.ShovelsGrantedExternally} " +
                $"ShovelsUsed={side.ShovelsUsed} ShovelsDiscarded={side.ShovelsDiscardedByRecruitOverwrite}");
            builder.AppendLine(
                $"OpenCells={side.OpenCellCount} HeroesFormed={side.CurrentHeroCount} " +
                $"PairLinksFormed={side.FormedPairLinkCount} " +
                $"EnemyKills={side.KilledEnemies} EnemyLeaks={side.ReachedGoalEnemies} BaseHP={side.BaseHP}");
        }
    }
}
