namespace DragonBound.Core
{
    public static class BossExperienceRewards
    {
        public const string SoulchainBinderBossId = "BOSS_SOULCHAIN_BINDER";
        public const string StormcallerPriestBossId = "BOSS_STORMCALLER_PRIEST";
        public const string BloodcrownTyrantBossId = "BOSS_BLOODCROWN_TYRANT";
        public const string WorldeaterWyrmBossId = "BOSS_WORLDEATER_WYRM";

        public static int Get(string bossId)
        {
            switch (bossId ?? string.Empty)
            {
                case SoulchainBinderBossId: return 6;
                case StormcallerPriestBossId: return 10;
                case BloodcrownTyrantBossId: return 15;
                case WorldeaterWyrmBossId: return 20;
                default: return 0;
            }
        }
    }
}
