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
        public void ComponentProviderLeavesUnitAndHeroArtForFutureProviders()
        {
            var provider = new ResourcesCampComponentArtProvider();

            Assert.IsFalse(provider.TryGetBasicUnitSprite("basic.axe_raider", out var unitSprite));
            Assert.IsNull(unitSprite);
            Assert.IsFalse(provider.TryGetHeroSprite(DragonBoundHeroIds.WindclawRanger, out var heroSprite));
            Assert.IsNull(heroSprite);
        }
    }
}
