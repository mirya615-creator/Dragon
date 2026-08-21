namespace DragonBound.Core
{
    /// <summary>
    /// Pure last-hit settlement rule. Gameplay owns the actual progression mutation;
    /// this class only answers whether a formal combat kill can award Hero XP.
    /// </summary>
    public static class HeroXpSettlement
    {
        public static int GetAwardedExperience(EnemyRuntime enemy)
        {
            if (enemy == null || enemy.LastDamageOwner.Kind != CombatDamageOwnerKind.Hero ||
                enemy.LastDamageOwner.Side != enemy.Team || !enemy.LastDamageOwner.IsValid)
            {
                return 0;
            }

            return enemy.ExperienceReward;
        }

        public static bool IsHeroLastHit(EnemyRuntime enemy)
        {
            return GetAwardedExperience(enemy) > 0;
        }

        public static bool IsBasicUnitLastHit(EnemyRuntime enemy)
        {
            return enemy != null && enemy.LastDamageOwner.Kind == CombatDamageOwnerKind.BasicUnit &&
                   enemy.LastDamageOwner.Side == enemy.Team;
        }
    }
}
