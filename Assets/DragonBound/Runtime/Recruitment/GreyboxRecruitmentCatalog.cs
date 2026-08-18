namespace DragonBound.Recruitment
{
    public static class GreyboxRecruitmentCatalog
    {
        public static RecruitmentCatalog Create()
        {
            var basicUnits = new[]
            {
                "basic.axe_raider",
                "basic.longbow_hunter",
                "basic.spear_raider",
                "basic.twinaxe_berserker"
            };

            var frozen = FrozenHeroConfigurationCatalog.Configuration;
            return new RecruitmentCatalog(
                basicUnits,
                frozen.Components,
                frozen.Recipes,
                frozen.BagTemplate);
        }
    }
}
