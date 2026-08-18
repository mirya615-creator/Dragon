using System;

namespace DragonBound.Core
{
    public enum MatchState
    {
        Boot,
        Initializing,
        Ready,
        Preparing,
        Running,
        BossPrompt,
        Paused,
        Victory,
        Defeat
    }

    public enum TeamSide
    {
        Player,
        AI
    }

    public sealed class MatchController
    {
        public const int StartingResources = 20;

        public MatchController(int runSeed = 0)
        {
            RunSeed = runSeed;
            Player = new TeamState(TeamSide.Player);
            AI = new TeamState(TeamSide.AI);
            Player.AddResources(StartingResources);
            AI.AddResources(StartingResources);
            State = MatchState.Initializing;
        }

        public int RunSeed { get; }
        public MatchState State { get; private set; } = MatchState.Boot;
        public int CurrentWave { get; private set; }
        public TeamState Player { get; }
        public TeamState AI { get; }
        public event Action<MatchState> StateChanged;

        public bool TryTransition(MatchState next)
        {
            if (!IsAllowed(State, next))
            {
                return false;
            }

            State = next;
            StateChanged?.Invoke(State);
            return true;
        }

        public void SetCurrentWave(int waveNumber)
        {
            if (waveNumber < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(waveNumber));
            }

            CurrentWave = waveNumber;
        }

        public SettlementDecision TrySettle(IMatchSettlementRule rule, SettlementContext context)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            var decision = rule.Evaluate(context);
            if (decision == SettlementDecision.PlayerVictory)
            {
                TryTransition(MatchState.Victory);
            }
            else if (decision == SettlementDecision.PlayerDefeat)
            {
                TryTransition(MatchState.Defeat);
            }

            return decision;
        }

        public RunSnapshot CaptureSnapshot()
        {
            return new RunSnapshot
            {
                RunSeed = RunSeed,
                MatchState = State,
                CurrentWave = CurrentWave,
                Player = Player.CaptureSnapshot(),
                AI = AI.CaptureSnapshot()
            };
        }

        private static bool IsAllowed(MatchState current, MatchState next)
        {
            if (next == MatchState.Defeat || next == MatchState.Victory)
            {
                return current != MatchState.Victory && current != MatchState.Defeat;
            }

            switch (current)
            {
                case MatchState.Boot:
                    return next == MatchState.Initializing || next == MatchState.Preparing;
                case MatchState.Initializing:
                    // Preparing remains a compatibility transition for pure model tests;
                    // production bootstrap completes through Ready before Running.
                    return next == MatchState.Ready || next == MatchState.Preparing;
                case MatchState.Ready:
                    return next == MatchState.Running || next == MatchState.Paused;
                case MatchState.Preparing:
                    return next == MatchState.Ready || next == MatchState.Running || next == MatchState.Paused;
                case MatchState.Running:
                    return next == MatchState.BossPrompt || next == MatchState.Paused;
                case MatchState.BossPrompt:
                    return next == MatchState.Running || next == MatchState.Paused;
                case MatchState.Paused:
                    return next == MatchState.Preparing || next == MatchState.Running || next == MatchState.BossPrompt;
                default:
                    return false;
            }
        }
    }
}
