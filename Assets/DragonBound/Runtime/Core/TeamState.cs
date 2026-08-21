using System;

namespace DragonBound.Core
{
    public static class BattleSettlementDefinition
    {
        public const int InitialMaxHeart = 3;
        public const int InitialCurrentHeart = 3;
        public const int NormalGoalDamage = 1;
        public const int MaxScheduledWave = 20;
        public const bool GenerateWaveAfterW20 = false;
        public const bool BossGoalIsInstantDefeat = true;
    }

    public sealed class TeamState
    {
        public TeamState(TeamSide side, int hatchlingMaxHealth = BattleSettlementDefinition.InitialMaxHeart)
        {
            if (hatchlingMaxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(hatchlingMaxHealth));
            }

            Side = side;
            HatchlingMaxHealth = hatchlingMaxHealth;
            HatchlingHealth = hatchlingMaxHealth;
        }

        public TeamSide Side { get; }
        public int Resources { get; private set; }
        public int RecruitmentCount { get; private set; }
        public int HatchlingMaxHealth { get; private set; }
        public int HatchlingHealth { get; private set; }
        public bool IsInstantDefeated { get; private set; }
        public int RemainingEnemyCount { get; private set; }

        public void AddResources(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            Resources += amount;
        }

        public bool TrySpendResources(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            if (amount > Resources)
            {
                return false;
            }

            Resources -= amount;
            return true;
        }

        public void RecordRecruitment()
        {
            RecruitmentCount++;
        }

        public void ApplyHatchlingDamage(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            HatchlingHealth = Math.Max(0, HatchlingHealth - amount);
        }

        public void ApplyBossGoalInstantDefeat()
        {
            IsInstantDefeated = true;
            HatchlingHealth = 0;
        }

        public void ApplyHatchlingHealthBonus(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            HatchlingMaxHealth = checked(HatchlingMaxHealth + amount);
            HatchlingHealth = checked(HatchlingHealth + amount);
        }

        public void SetRemainingEnemyCount(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            RemainingEnemyCount = count;
        }

        public TeamSnapshot CaptureSnapshot()
        {
            return new TeamSnapshot
            {
                Side = Side,
                Resources = Resources,
                RecruitmentCount = RecruitmentCount,
                HatchlingHealth = HatchlingHealth,
                IsInstantDefeated = IsInstantDefeated,
                RemainingEnemyCount = RemainingEnemyCount
            };
        }
    }

    [Serializable]
    public sealed class TeamSnapshot
    {
        public TeamSide Side;
        public int Resources;
        public int RecruitmentCount;
        public int HatchlingHealth;
        public bool IsInstantDefeated;
        public int RemainingEnemyCount;
    }
}
