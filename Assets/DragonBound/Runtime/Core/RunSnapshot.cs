using System;

namespace DragonBound.Core
{
    [Serializable]
    public sealed class RunSnapshot
    {
        public int RunSeed;
        public MatchState MatchState;
        public int CurrentWave;
        public TeamSnapshot Player;
        public TeamSnapshot AI;
    }
}
