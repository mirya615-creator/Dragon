using DragonBound.Recruitment;

namespace DragonBound.Core
{
    // Facade used by bootstrap and diagnostics so no wave update can bypass the
    // MatchState gate in ThreeWaveSliceRuntime.
    public sealed class WaveSystem
    {
        private readonly MatchController match;

        public WaveSystem(
            MatchController match,
            BoardRecruitDestination playerDestination,
            BoardRecruitDestination aiDestination,
            ThreeWaveEnemyDurabilityProfile durabilityProfile = ThreeWaveEnemyDurabilityProfile.BasicUnitBaseline)
        {
            this.match = match;
            Runtime = new ThreeWaveSliceRuntime(
                match,
                playerDestination,
                aiDestination,
                durabilityProfile);
        }

        public ThreeWaveSliceRuntime Runtime { get; }

        public void Tick(float deltaSeconds)
        {
            if (match == null || match.State != MatchState.Running)
            {
                return;
            }

            Runtime.Tick(deltaSeconds);
        }
    }
}
