using System;
using DragonBound.Combat;

namespace DragonBound.Grid
{
    public enum BasicUnitArchetype
    {
        Axe,
        Rider,
        Berserker = Rider,
        Spear,
        Bow
    }

    public static class UnitRangeRules
    {
        public static float GetRadius(BasicUnitArchetype archetype)
        {
            switch (archetype)
            {
                case BasicUnitArchetype.Axe:
                    return 1.5f;
                case BasicUnitArchetype.Rider:
                    return 2f;
                case BasicUnitArchetype.Spear:
                    return 2.5f;
                case BasicUnitArchetype.Bow:
                    return 3.5f;
                default:
                    throw new ArgumentOutOfRangeException(nameof(archetype), archetype, null);
            }
        }

        public static float GetRadiusForConfig(string configId)
        {
            return BasicUnitCatalog.GetStats(configId, BasicUnitCatalog.MinLevel).RangeCells;
        }
    }
}
