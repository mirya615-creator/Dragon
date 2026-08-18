using System;
using System.Collections.Generic;
using DragonBound.Core;

namespace DragonBound.Combat
{
    public enum GroundHazardShape
    {
        Circle,
        Line
    }

    public sealed class GroundHazardDefinition
    {
        public GroundHazardDefinition(
            TeamSide side,
            string sourceRuntimeId,
            string sourceRecipeId,
            CombatPoint center,
            GroundHazardShape shape,
            float radius,
            float durationSeconds,
            float tickIntervalSeconds,
            float damagePerTick,
            int maximumTargets,
            CombatPoint direction = default,
            float length = 0f,
            float width = 0f)
        {
            if (string.IsNullOrWhiteSpace(sourceRuntimeId) ||
                string.IsNullOrWhiteSpace(sourceRecipeId) ||
                radius < 0f || durationSeconds <= 0f || tickIntervalSeconds <= 0f ||
                damagePerTick < 0f || maximumTargets < 1 ||
                (shape == GroundHazardShape.Line && (length <= 0f || width <= 0f)))
            {
                throw new ArgumentException("A ground hazard requires valid source, timing, damage, and target values.");
            }

            Side = side;
            SourceRuntimeId = sourceRuntimeId;
            SourceRecipeId = sourceRecipeId;
            Center = center;
            Shape = shape;
            Radius = radius;
            DurationSeconds = durationSeconds;
            TickIntervalSeconds = tickIntervalSeconds;
            DamagePerTick = damagePerTick;
            MaximumTargets = maximumTargets;
            Direction = direction;
            Length = length;
            Width = width;
        }

        public TeamSide Side { get; }
        public string SourceRuntimeId { get; }
        public string SourceRecipeId { get; }
        public CombatPoint Center { get; }
        public GroundHazardShape Shape { get; }
        public float Radius { get; }
        public float DurationSeconds { get; }
        public float TickIntervalSeconds { get; }
        public float DamagePerTick { get; }
        public int MaximumTargets { get; }
        public CombatPoint Direction { get; }
        public float Length { get; }
        public float Width { get; }

        internal bool HasSameRefreshKey(GroundHazardDefinition other)
        {
            if (other == null ||
                Side != other.Side ||
                Shape != other.Shape ||
                !string.Equals(SourceRuntimeId, other.SourceRuntimeId, StringComparison.Ordinal) ||
                !string.Equals(SourceRecipeId, other.SourceRecipeId, StringComparison.Ordinal) ||
                Center.DistanceSquared(other.Center) > 0.0001f)
            {
                return false;
            }

            // Ember circles refresh at their impact point. A Dragon Rider may dive from the
            // same midpoint in another direction, which is a distinct line hazard.
            return Shape != GroundHazardShape.Line ||
                   (Direction.Equals(other.Direction) &&
                    Math.Abs(Length - other.Length) <= 0.0001f &&
                    Math.Abs(Width - other.Width) <= 0.0001f);
        }
    }

    public sealed class GroundHazardRuntime
    {
        internal GroundHazardRuntime(GroundHazardDefinition definition)
        {
            Definition = definition;
            RemainingSeconds = definition.DurationSeconds;
        }

        public GroundHazardDefinition Definition { get; }
        public float RemainingSeconds { get; private set; }
        public float TickElapsedSeconds { get; private set; }
        public int TicksApplied { get; private set; }

        internal void Refresh()
        {
            RemainingSeconds = Definition.DurationSeconds;
            TickElapsedSeconds = 0f;
            TicksApplied = 0;
        }

        internal void Advance(float deltaSeconds)
        {
            RemainingSeconds = Math.Max(0f, RemainingSeconds - deltaSeconds);
            TickElapsedSeconds += deltaSeconds;
        }

        internal bool ConsumeTick()
        {
            if (TickElapsedSeconds + 0.0001f < Definition.TickIntervalSeconds ||
                TicksApplied >= (int)Math.Ceiling(Definition.DurationSeconds / Definition.TickIntervalSeconds))
            {
                return false;
            }

            TickElapsedSeconds -= Definition.TickIntervalSeconds;
            TicksApplied++;
            return true;
        }
    }

    public sealed class GroundHazardSystem
    {
        private readonly List<GroundHazardRuntime> hazards = new List<GroundHazardRuntime>();

        public int ActiveCount => hazards.Count;
        public IReadOnlyList<GroundHazardRuntime> ActiveHazards => hazards;

        public GroundHazardRuntime CreateOrRefresh(GroundHazardDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            foreach (var existing in hazards)
            {
                if (existing.Definition.HasSameRefreshKey(definition))
                {
                    existing.Refresh();
                    return existing;
                }
            }

            var runtime = new GroundHazardRuntime(definition);
            hazards.Add(runtime);
            return runtime;
        }

        public List<HeroDamageResult> Tick(float deltaSeconds, EnemyRegistry registry, AttackKind kind)
        {
            var results = new List<HeroDamageResult>();
            if (deltaSeconds <= 0f || registry == null)
            {
                return results;
            }

            for (var index = hazards.Count - 1; index >= 0; index--)
            {
                var hazard = hazards[index];
                hazard.Advance(deltaSeconds);
                while (hazard.ConsumeTick())
                {
                    foreach (var target in SelectTargets(hazard.Definition, registry.Snapshot()))
                    {
                        if (!target.IsAlive)
                        {
                            continue;
                        }

                        target.HitPoints = Math.Max(0f, target.HitPoints - hazard.Definition.DamagePerTick);
                        results.Add(new HeroDamageResult(
                            kind,
                            target,
                            hazard.Definition.DamagePerTick,
                            target.HitPoints <= 0.0001f));
                    }
                }

                if (hazard.RemainingSeconds <= 0.0001f)
                {
                    hazards.RemoveAt(index);
                }
            }

            return results;
        }

        public void Clear()
        {
            hazards.Clear();
        }

        private static List<EnemyRuntime> SelectTargets(
            GroundHazardDefinition definition,
            IEnumerable<EnemyRuntime> enemies)
        {
            var result = new List<EnemyRuntime>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive || enemy.Team != definition.Side ||
                    !Contains(definition, enemy.CombatPosition))
                {
                    continue;
                }

                result.Add(enemy);
            }

            result.Sort((first, second) =>
            {
                var progress = second.PathProgress.CompareTo(first.PathProgress);
                return progress != 0 ? progress : string.CompareOrdinal(first.RuntimeId, second.RuntimeId);
            });
            if (result.Count > definition.MaximumTargets)
            {
                result.RemoveRange(definition.MaximumTargets, result.Count - definition.MaximumTargets);
            }

            return result;
        }

        private static bool Contains(GroundHazardDefinition definition, CombatPoint point)
        {
            if (definition.Shape == GroundHazardShape.Circle)
            {
                return definition.Center.DistanceSquared(point) <= (definition.Radius * definition.Radius) + 0.0001f;
            }

            var relativeX = point.X - definition.Center.X;
            var relativeY = point.Y - definition.Center.Y;
            var forward = (relativeX * definition.Direction.X) + (relativeY * definition.Direction.Y);
            if (forward < 0f || forward > definition.Length)
            {
                return false;
            }

            var lateral = Math.Abs((relativeX * -definition.Direction.Y) + (relativeY * definition.Direction.X));
            return lateral <= (definition.Width * 0.5f) + 0.0001f;
        }
    }
}
