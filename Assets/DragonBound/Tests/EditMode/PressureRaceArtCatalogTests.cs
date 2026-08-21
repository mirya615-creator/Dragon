using DragonBound.Core;
using DragonBound.Presentation;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class PressureRaceArtCatalogTests
    {
        [TestCase(EnemyArchetype.Normal, PressureRaceArtCatalog.EnemyNormal)]
        [TestCase(EnemyArchetype.Fast, PressureRaceArtCatalog.EnemyFast)]
        [TestCase(EnemyArchetype.Swarm, PressureRaceArtCatalog.EnemySwarm)]
        [TestCase(EnemyArchetype.Elite, PressureRaceArtCatalog.EnemyElite)]
        [TestCase(EnemyArchetype.Boss, PressureRaceArtCatalog.EnemyBossReserved)]
        public void ArtSlotsAreStableAndPresentationOnly(EnemyArchetype archetype, string expectedSlot)
        {
            var catalog = UnityEngine.ScriptableObject.CreateInstance<PressureRaceArtCatalog>();

            Assert.AreEqual(expectedSlot, catalog.GetSlotId(archetype));
            Assert.IsNull(catalog.GetEnemySprite(archetype));
        }
    }
}
