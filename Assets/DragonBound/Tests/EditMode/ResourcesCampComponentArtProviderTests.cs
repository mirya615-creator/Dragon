using System.Collections.Generic;
using DragonBound.Presentation;
using DragonBound.Recruitment;
using NUnit.Framework;
using UnityEngine;

namespace DragonBound.Tests.EditMode
{
    public sealed class ResourcesCampComponentArtProviderTests
    {
        [Test]
        public void EveryFormalHeroComponentLoadsOneResourcesSprite()
        {
            var provider = new ResourcesCampComponentArtProvider();
            var sprites = new HashSet<Sprite>();

            Assert.AreEqual(18, HeroComponentCatalog.Definitions.Count);
            Assert.AreEqual(HeroComponentCatalog.Definitions.Count, provider.ComponentMappingCount);
            foreach (var definition in HeroComponentCatalog.Definitions)
            {
                Assert.IsTrue(
                    provider.TryGetHeroComponentSprite(definition.Id, out var sprite),
                    definition.Id);
                Assert.IsNotNull(sprite, definition.Id);
                Assert.IsTrue(sprites.Add(sprite), definition.Id + " reused another component sprite.");
            }
        }

        [Test]
        public void ResourceProviderLoadsAllBasicUnitAndHeroSprites()
        {
            var provider = new ResourcesCampComponentArtProvider();
            var unitSprites = new HashSet<Sprite>();

            foreach (var unitId in new[]
                     {
                         "basic.axe_raider",
                         "basic.twinaxe_berserker",
                         "basic.longbow_hunter",
                         "basic.spear_raider"
                     })
            {
                Assert.IsTrue(provider.TryGetBasicUnitSprite(unitId, out var unitSprite), unitId);
                Assert.IsNotNull(unitSprite, unitId);
                Assert.IsTrue(unitSprites.Add(unitSprite), unitId + " reused another unit sprite.");
            }

            var heroSprites = new HashSet<Sprite>();
            Assert.AreEqual(HeroDefinitionCatalog.Definitions.Count, provider.HeroMappingCount);
            foreach (var hero in HeroDefinitionCatalog.Definitions)
            {
                Assert.IsTrue(provider.TryGetHeroSprite(hero.Id, out var heroSprite), hero.Id);
                Assert.IsNotNull(heroSprite, hero.Id);
                Assert.IsTrue(heroSprites.Add(heroSprite), hero.Id + " reused another hero sprite.");
            }
        }
    }
}
