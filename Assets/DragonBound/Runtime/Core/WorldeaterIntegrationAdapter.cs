using System;
using System.Collections.Generic;
using DragonBound.Bosses.Contracts;
using DragonBound.Bosses.Runtime;
using DragonBound.Recruitment;

namespace DragonBound.Core
{
    internal sealed class WorldeaterIntegrationAdapter :
        IWorldeaterBossTarget,
        IWorldeaterTargetPort,
        IWorldeaterSummonPort,
        IWorldeaterSpellbreaker
    {
        private readonly EnemyRuntime boss;
        private readonly PressureRaceSideRuntime sideRuntime;
        private readonly BoardRecruitDestination destination;
        private readonly TeamSide side;
        private readonly ISoulChainSpellbreakerResolver spellbreaker;
        private readonly float initialMaxHitPoints;

        public WorldeaterIntegrationAdapter(
            EnemyRuntime boss,
            PressureRaceSideRuntime sideRuntime,
            BoardRecruitDestination destination,
            TeamSide side,
            ISoulChainSpellbreakerResolver spellbreaker)
        {
            this.boss = boss ?? throw new ArgumentNullException(nameof(boss));
            this.sideRuntime = sideRuntime ?? throw new ArgumentNullException(nameof(sideRuntime));
            this.destination = destination;
            this.side = side;
            this.spellbreaker = spellbreaker;
            initialMaxHitPoints = boss.MaxHitPoints;
        }

        public EnemyRuntime Boss => boss;
        public float InitialMaxHitPoints => initialMaxHitPoints;
        public float MaxHitPoints => boss.MaxHitPoints;
        public bool IsAlive => boss.IsAlive;

        public void ApplyReflectedDamage(float damage)
        {
            if (damage > 0f && boss.IsAlive)
            {
                boss.ApplyDamage(damage);
            }
        }

        public void AddHealth(float amount)
        {
            if (amount > 0f && boss.IsAlive)
            {
                boss.IncreaseMaxHitPoints(amount);
            }
        }

        public IReadOnlyList<WorldeaterTarget> GetEligibleTargets()
        {
            var result = new List<WorldeaterTarget>();
            if (destination != null)
            {
                foreach (var unit in destination.GetDeployedUnits())
                {
                    result.Add(new WorldeaterTarget(
                        unit.Card.RuntimeId,
                        WorldeaterTargetClass.Basic,
                        unit.Card.Level));
                }
            }

            foreach (var enemy in sideRuntime.Registry.Snapshot())
            {
                if (enemy.IsAlive && enemy.Archetype == EnemyArchetype.Swarm)
                {
                    result.Add(new WorldeaterTarget(enemy.RuntimeId, WorldeaterTargetClass.Minion, 0));
                }
            }

            return result;
        }

        public bool IsStillEligible(WorldeaterTarget target)
        {
            if (target.TargetClass == WorldeaterTargetClass.Basic)
            {
                if (destination == null || !destination.TryGetCard(target.RuntimeId, out var card) ||
                    card.Kind != RecruitItemKind.BasicUnit || card.Level != target.StoredLevel)
                {
                    return false;
                }

                foreach (var unit in destination.GetDeployedUnits())
                {
                    if (string.Equals(unit.Card.RuntimeId, target.RuntimeId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }

            return sideRuntime.Registry.TryGet(target.RuntimeId, out var enemy) &&
                   enemy.IsAlive && enemy.Archetype == EnemyArchetype.Swarm;
        }

        public bool Consume(WorldeaterTarget target)
        {
            return target.TargetClass == WorldeaterTargetClass.Basic
                ? destination != null && destination.TryRemoveUnit(target.RuntimeId)
                : sideRuntime.RemoveEnemyWithoutRewards(target.RuntimeId);
        }

        public void SpawnMinions(int count, float maxHitPoints, float moveSpeedCellsPerSecond)
        {
            sideRuntime.SpawnBossSummons(
                20,
                WorldeaterWyrmConfiguration.BossId,
                WorldeaterWyrmConfiguration.MinionId,
                count,
                maxHitPoints,
                moveSpeedCellsPerSecond);
        }

        public SpellbreakerOutcome Evaluate(BossCastAttempt attempt)
        {
            if (spellbreaker == null)
            {
                return SpellbreakerOutcome.NotEvaluated;
            }

            return spellbreaker.ShouldBlockCast(new SoulChainBossCastContext(
                attempt.BossId.Value,
                side,
                attempt.AttemptNumber,
                boss.MaxHitPoints))
                ? SpellbreakerOutcome.Blocked
                : SpellbreakerOutcome.Passed;
        }
    }
}
