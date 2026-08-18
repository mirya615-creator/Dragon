using System;

namespace DragonBound.Core
{
    public sealed class TeamState
    {
        public TeamState(TeamSide side, int hatchlingMaxHealth = 3)
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
        public int HatchlingMaxHealth { get; }
        public int HatchlingHealth { get; private set; }
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
        public int RemainingEnemyCount;
    }
}
