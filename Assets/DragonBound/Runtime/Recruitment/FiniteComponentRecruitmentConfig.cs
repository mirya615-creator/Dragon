namespace DragonBound.Recruitment
{
    /// <summary>
    /// Frozen balancing knobs for the finite component five-card recruitment flow.
    /// Keep these values in one place so balance changes do not alter transaction code.
    /// </summary>
    public static class FiniteComponentRecruitmentConfig
    {
        public const float ThreeComponentBatchChance = 0.50f;
        public const int NormalProbabilityBatchCount = 10;
        public const int GuaranteedCompletionBatch = 11;
        public const int NormalMinComponentsPerBatch = 2;
        public const int NormalMaxComponentsPerBatch = 3;
    }
}
