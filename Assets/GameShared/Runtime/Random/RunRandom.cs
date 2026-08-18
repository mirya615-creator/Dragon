using System;

namespace GameShared.Random
{
    public interface IRunRandom
    {
        int Seed { get; }
        long CallIndex { get; }

        int NextInt(string context, int minInclusive, int maxExclusive);
        float NextUnit(string context);
    }

    public sealed class RunSeed
    {
        public RunSeed(int value)
        {
            Value = value;
            Random = new RunRandom(value);
        }

        public int Value { get; }
        public IRunRandom Random { get; }
    }

    public sealed class RunRandom : IRunRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const ulong Increment = 1442695040888963407UL;
        private const float UnitScale = 1f / 16777216f;

        private ulong state;

        public RunRandom(int seed)
        {
            Seed = seed;
            state = 0UL;
            NextUIntRaw();
            state += unchecked((uint)seed);
            NextUIntRaw();
        }

        public int Seed { get; }
        public long CallIndex { get; private set; }

        public int NextInt(string context, int minInclusive, int maxExclusive)
        {
            ValidateContext(context);
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            }

            var range = (uint)((long)maxExclusive - minInclusive);
            var threshold = unchecked(0U - range) % range;
            uint sample;
            do
            {
                sample = NextUIntRaw();
            }
            while (sample < threshold);

            CallIndex++;
            return (int)(minInclusive + (long)(sample % range));
        }

        public float NextUnit(string context)
        {
            ValidateContext(context);
            CallIndex++;
            return (NextUIntRaw() >> 8) * UnitScale;
        }

        private static void ValidateContext(string context)
        {
            if (string.IsNullOrWhiteSpace(context))
            {
                throw new ArgumentException("A stable random context is required.", nameof(context));
            }
        }

        private uint NextUIntRaw()
        {
            var previous = state;
            state = unchecked(previous * Multiplier + Increment);
            var xorShifted = (uint)(((previous >> 18) ^ previous) >> 27);
            var rotation = (int)(previous >> 59);
            return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
        }
    }
}
