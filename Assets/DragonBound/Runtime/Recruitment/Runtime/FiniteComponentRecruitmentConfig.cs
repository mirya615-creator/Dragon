namespace DragonBound.Recruitment
{
    /// <summary>
    /// Frozen balancing knobs for the finite component five-card recruitment flow.
    /// Keep these values in one place so balance changes do not alter transaction code.
    /// </summary>
    public static class FiniteComponentRecruitmentConfig
    {
        public const int TargetCompletionRecruitCount = 11;
        public const int MaxComponentsPerBatch = 4;
        public const int MultiMinComponentsPerBatch = 2;
        public const int MultiMaxComponentsPerBatch = 4;
        public const int MinBasicUnitsPerBatch = 1;

        public const float BasePureBasicWeight = 0.50f;
        public const float BaseOneComponentWeight = 0.20f;
        public const float BaseMultiComponentWeight = 0.20f;
        public const float BaseShovelWeight = 0.10f;

        public const int OpeningProtectedRecruitCount = 3;
        public const float BaseExpectedComponentsPerRecruit = 0.80f;
        public const float CatchupAllowedComponentsPerRecruit = 5.00f;
        public const float CatchupFullPressureLag = 1.50f;
        public const float OneComponentTransferPressureFloor = 0.35f;

        public const string DynamicKindStreamId = "RecruitDynamicKind";
        public const string DynamicMultiCountStreamId = "RecruitDynamicMultiCount";
        public const string DynamicKindContext = "RecruitDynamicKind.v1";
        public const string DynamicMultiCountContext = "RecruitDynamicMultiCount.v1";
    }
}
