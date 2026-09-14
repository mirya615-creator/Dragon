using GameShared.Random;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class RunRandomTests
    {
        [Test]
        public void SameSeedProducesSameSequence()
        {
            var first = new RunSeed(73).Random;
            var second = new RunSeed(73).Random;

            for (var i = 0; i < 16; i++)
            {
                Assert.AreEqual(first.NextInt("test.sequence", -10, 50), second.NextInt("test.sequence", -10, 50));
            }
        }

        [Test]
        public void Pcg32Seed73MatchesGoldenIntegerVector()
        {
            var random = new RunRandom(73);
            int[] expected = { 44, 25, -6, 20, 31, 28, 17, -9 };

            foreach (int value in expected)
                Assert.AreEqual(value, random.NextInt("golden.int", -10, 50));
            Assert.AreEqual(expected.Length, random.CallIndex);
        }

        [Test]
        public void Pcg32Seed73MatchesGoldenUnitVector()
        {
            var random = new RunRandom(73);
            float[] expected =
            {
                0.3577647805213928f,
                0.4159070849418640f,
                0.9635037779808044f,
                0.20192474126815796f,
                0.14966952800750732f,
                0.4871952533721924f
            };

            foreach (float value in expected)
                Assert.AreEqual(value, random.NextUnit("golden.unit"));
            Assert.AreEqual(expected.Length, random.CallIndex);
        }

        [TestCase(0, -1334359544, 663295327, 484666913, 1977970329)]
        [TestCase(73, -1334359487, 663295254, 484666984, 1977970384)]
        [TestCase(-1, 1334359543, -663295328, -484666914, -1977970330)]
        public void SharedSeedDerivationMatchesGoldenVectors(
            int seed,
            int expectedPlayerRecruit,
            int expectedAiRecruit,
            int expectedCombat,
            int expectedAiDecision)
        {
            Assert.AreEqual(expectedPlayerRecruit, SharedRandomProtocolV1.DeriveSeed(seed, "player.recruit"));
            Assert.AreEqual(expectedAiRecruit, SharedRandomProtocolV1.DeriveSeed(seed, "ai.recruit"));
            Assert.AreEqual(expectedCombat, SharedRandomProtocolV1.DeriveSeed(seed, "combat"));
            Assert.AreEqual(expectedAiDecision, SharedRandomProtocolV1.DeriveSeed(seed, "ai.decision"));
        }

        [Test]
        public void RandomWireEnvelopesMatchGoldenVectorsAndRoundTrip()
        {
            const int seed = 73;
            const string recruit = "REJSUwEBsHdKQSeJFRY";
            const string wave = "REJSUwECAAAASQ";
            const string emptyCritical = "REJSUwEDAAAAAA";

            Assert.AreEqual(recruit, SharedRandomProtocolV1.EncodeRecruitRandom(seed));
            Assert.AreEqual(wave, SharedRandomProtocolV1.EncodeWaveRandom(seed));
            Assert.AreEqual(emptyCritical, SharedRandomProtocolV1.EncodeCriticalRandomSequence(null));

            SharedRandomProtocolV1.DecodeRecruitRandom(recruit, out int player, out int ai);
            Assert.AreEqual(-1334359487, player);
            Assert.AreEqual(663295254, ai);
            Assert.AreEqual(seed, SharedRandomProtocolV1.DecodeWaveRandom(wave));
            CollectionAssert.IsEmpty(SharedRandomProtocolV1.DecodeCriticalRandomSequence(emptyCritical));
        }

        [Test]
        public void CriticalRandomSamplesMatchGoldenVectorAndRoundTrip()
        {
            uint[] samples = { 0u, 1u, uint.MaxValue, 0x12345678u };
            string encoded = SharedRandomProtocolV1.EncodeCriticalRandomSequence(samples);

            Assert.AreEqual("REJSUwEDAAAABAAAAAAAAAAB_____xI0Vng", encoded);
            CollectionAssert.AreEqual(samples, SharedRandomProtocolV1.DecodeCriticalRandomSequence(encoded));
        }

        [TestCase("")]
        [TestCase("REJSUwECAAAASQ")]
        [TestCase("not-base64!")]
        public void RecruitEnvelopeRejectsInvalidPayload(string encoded)
        {
            Assert.Throws<System.FormatException>(() =>
                SharedRandomProtocolV1.DecodeRecruitRandom(encoded, out _, out _));
        }
    }
}
