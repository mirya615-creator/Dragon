using System;
using System.Collections.Generic;
using DragonBound.Core;
using UnityEngine;

namespace DragonBound.Presentation
{
    /// <summary>
    /// Presentation-only art handoff for pressure-race enemies. A missing entry intentionally
    /// retains the existing greybox image, so gameplay can run without final art assigned.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PressureRaceArtCatalog",
        menuName = "DragonBound/Presentation/Pressure Race Art Catalog")]
    public sealed class PressureRaceArtCatalog : ScriptableObject
    {
        public const string DefaultResourcePath = "DragonBound/PressureRaceArtCatalog";
        public const string EnemyNormal = "ART_Enemy_Normal";
        public const string EnemyFast = "ART_Enemy_Fast";
        public const string EnemySwarm = "ART_Enemy_Swarm";
        public const string EnemyElite = "ART_Enemy_Elite";
        public const string EnemyBossReserved = "ART_Enemy_BossReserved";
        public const string EnemyHealthBar = "ART_Enemy_HealthBar";
        public const string EnemyHitFlash = "ART_Enemy_HitFlash";

        [SerializeField] private List<EnemyArtSlot> enemySlots = new List<EnemyArtSlot>();

        public string GetSlotId(EnemyArchetype archetype)
        {
            switch (archetype)
            {
                case EnemyArchetype.Fast:
                    return EnemyFast;
                case EnemyArchetype.Swarm:
                    return EnemySwarm;
                case EnemyArchetype.Elite:
                    return EnemyElite;
                case EnemyArchetype.Boss:
                    return EnemyBossReserved;
                default:
                    return EnemyNormal;
            }
        }

        public Sprite GetEnemySprite(EnemyArchetype archetype)
        {
            var slotId = GetSlotId(archetype);
            foreach (var slot in enemySlots)
            {
                if (slot != null && string.Equals(slot.ArtSlotId, slotId, StringComparison.Ordinal))
                {
                    return slot.Sprite;
                }
            }

            return null;
        }

        [Serializable]
        public sealed class EnemyArtSlot
        {
            [SerializeField] private string artSlotId;
            [SerializeField] private Sprite sprite;

            public string ArtSlotId => artSlotId;
            public Sprite Sprite => sprite;
        }
    }
}
