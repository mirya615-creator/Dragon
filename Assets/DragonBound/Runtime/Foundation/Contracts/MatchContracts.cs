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

    public enum SettlementDecision
    {
        Undecided,
        PlayerVictory,
        PlayerDefeat
    }

    public readonly struct SettlementContext
    {
        public SettlementContext(
            int playerHatchlingHealth,
            int aiHatchlingHealth,
            bool playerDefeatedFinalBoss,
            bool aiDefeatedFinalBoss,
            float playerFinalBossHealth,
            float aiFinalBossHealth,
            float playerWave15ClearTime,
            float aiWave15ClearTime)
        {
            PlayerHatchlingHealth = playerHatchlingHealth;
            AIHatchlingHealth = aiHatchlingHealth;
            PlayerDefeatedFinalBoss = playerDefeatedFinalBoss;
            AIDefeatedFinalBoss = aiDefeatedFinalBoss;
            PlayerFinalBossHealth = playerFinalBossHealth;
            AIFinalBossHealth = aiFinalBossHealth;
            PlayerWave15ClearTime = playerWave15ClearTime;
            AIWave15ClearTime = aiWave15ClearTime;
        }

        public int PlayerHatchlingHealth { get; }
        public int AIHatchlingHealth { get; }
        public bool PlayerDefeatedFinalBoss { get; }
        public bool AIDefeatedFinalBoss { get; }
        public float PlayerFinalBossHealth { get; }
        public float AIFinalBossHealth { get; }
        public float PlayerWave15ClearTime { get; }
        public float AIWave15ClearTime { get; }
    }

    public interface IMatchSettlementRule
    {
        SettlementDecision Evaluate(SettlementContext context);
    }

    public interface IMatchClock
    {
        float ElapsedSeconds { get; }
        float DeltaSeconds { get; }
    }

    public interface IMatchEventSink<in TEvent>
    {
        void Publish(TEvent value);
    }

    public interface IWaveRuntimeStatus
    {
        bool IsGameplayRunning { get; }
        int CurrentWave { get; }
        float WaveDurationSeconds { get; }
        float WaveRemainingSeconds { get; }
    }
}
