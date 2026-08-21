using System;

namespace DragonBound.Foundation.Contracts
{
    public readonly struct RunId : IEquatable<RunId>
    {
        public RunId(int seed)
        {
            Seed = seed;
        }

        public int Seed { get; }
        public bool Equals(RunId other) => Seed == other.Seed;
        public override bool Equals(object obj) => obj is RunId other && Equals(other);
        public override int GetHashCode() => Seed;
        public override string ToString() => Seed.ToString();
    }

    public readonly struct RuntimeEntityId : IEquatable<RuntimeEntityId>
    {
        public RuntimeEntityId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A runtime entity id is required.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }
        public bool Equals(RuntimeEntityId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is RuntimeEntityId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct WaveNumber : IEquatable<WaveNumber>, IComparable<WaveNumber>
    {
        public WaveNumber(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Value = value;
        }

        public int Value { get; }
        public int CompareTo(WaveNumber other) => Value.CompareTo(other.Value);
        public bool Equals(WaveNumber other) => Value == other.Value;
        public override bool Equals(object obj) => obj is WaveNumber other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
    }
}
