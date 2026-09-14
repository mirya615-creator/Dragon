using System;
using System.Collections.Generic;
using System.Text;

namespace GameShared.Random
{
    /// <summary>
    /// Cross-runtime wire contract for deterministic Run random streams.
    /// All integers are two's-complement, big-endian values and envelopes are Base64URL
    /// without padding. Go and Unity must implement these bytes exactly.
    /// </summary>
    public static class SharedRandomProtocolV1
    {
        public const string Version = "DragonBound.Random.v1";
        public const string Algorithm = "pcg32-xsh-rr-v1";

        private const byte SchemaVersion = 1;
        private const byte RecruitKind = 1;
        private const byte WaveKind = 2;
        private const byte CriticalKind = 3;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("DBRS");

        public static int DeriveSeed(int runSeed, string stream)
        {
            if (string.IsNullOrWhiteSpace(stream))
                throw new ArgumentException("A stable random stream name is required.", nameof(stream));

            unchecked
            {
                uint hash = 2166136261u;
                byte[] bytes = Encoding.UTF8.GetBytes(stream);
                foreach (byte value in bytes)
                {
                    hash ^= value;
                    hash *= 16777619u;
                }
                return (int)(hash ^ (uint)runSeed);
            }
        }

        public static string EncodeRecruitRandom(int runSeed)
        {
            var bytes = CreateEnvelope(RecruitKind, 8);
            int offset = 6;
            WriteInt32(bytes, ref offset, DeriveSeed(runSeed, "player.recruit"));
            WriteInt32(bytes, ref offset, DeriveSeed(runSeed, "ai.recruit"));
            return EncodeBase64Url(bytes);
        }

        public static void DecodeRecruitRandom(
            string encoded,
            out int playerRecruitSeed,
            out int aiRecruitSeed)
        {
            byte[] bytes = DecodeEnvelope(encoded, RecruitKind, 14);
            int offset = 6;
            playerRecruitSeed = ReadInt32(bytes, ref offset);
            aiRecruitSeed = ReadInt32(bytes, ref offset);
        }

        public static string EncodeWaveRandom(int runSeed)
        {
            var bytes = CreateEnvelope(WaveKind, 4);
            int offset = 6;
            WriteInt32(bytes, ref offset, runSeed);
            return EncodeBase64Url(bytes);
        }

        public static int DecodeWaveRandom(string encoded)
        {
            byte[] bytes = DecodeEnvelope(encoded, WaveKind, 10);
            int offset = 6;
            return ReadInt32(bytes, ref offset);
        }

        public static string EncodeCriticalRandomSequence(IReadOnlyList<uint> samples)
        {
            int count = samples?.Count ?? 0;
            var bytes = CreateEnvelope(CriticalKind, checked(4 + count * 4));
            int offset = 6;
            WriteUInt32(bytes, ref offset, (uint)count);
            for (int index = 0; index < count; index++)
                WriteUInt32(bytes, ref offset, samples[index]);
            return EncodeBase64Url(bytes);
        }

        public static uint[] DecodeCriticalRandomSequence(string encoded)
        {
            byte[] bytes = DecodeEnvelope(encoded, CriticalKind, null);
            if (bytes.Length < 10 || (bytes.Length - 10) % 4 != 0)
                throw new FormatException("Critical random sequence length is invalid.");
            int offset = 6;
            uint count = ReadUInt32(bytes, ref offset);
            if (count > int.MaxValue || bytes.Length != 10 + (int)count * 4)
                throw new FormatException("Critical random sequence count is invalid.");
            var samples = new uint[count];
            for (int index = 0; index < samples.Length; index++)
                samples[index] = ReadUInt32(bytes, ref offset);
            return samples;
        }

        private static byte[] CreateEnvelope(byte kind, int payloadLength)
        {
            var bytes = new byte[checked(6 + payloadLength)];
            Buffer.BlockCopy(Magic, 0, bytes, 0, Magic.Length);
            bytes[4] = SchemaVersion;
            bytes[5] = kind;
            return bytes;
        }

        private static byte[] DecodeEnvelope(string encoded, byte expectedKind, int? expectedLength)
        {
            byte[] bytes;
            try
            {
                bytes = DecodeBase64Url(encoded);
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is FormatException)
            {
                throw new FormatException("Random stream envelope is not valid Base64URL.", exception);
            }
            if (bytes.Length < 6 || bytes[0] != Magic[0] || bytes[1] != Magic[1] ||
                bytes[2] != Magic[2] || bytes[3] != Magic[3])
                throw new FormatException("Random stream envelope magic is invalid.");
            if (bytes[4] != SchemaVersion)
                throw new FormatException("Random stream envelope version is unsupported.");
            if (bytes[5] != expectedKind)
                throw new FormatException("Random stream envelope kind is invalid.");
            if (expectedLength.HasValue && bytes.Length != expectedLength.Value)
                throw new FormatException("Random stream envelope length is invalid.");
            return bytes;
        }

        private static string EncodeBase64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        private static byte[] DecodeBase64Url(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new FormatException("Value is empty.");
            string normalized = value.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
            return Convert.FromBase64String(normalized);
        }

        private static void WriteInt32(byte[] target, ref int offset, int value)
        {
            WriteUInt32(target, ref offset, unchecked((uint)value));
        }

        private static void WriteUInt32(byte[] target, ref int offset, uint value)
        {
            target[offset++] = (byte)(value >> 24);
            target[offset++] = (byte)(value >> 16);
            target[offset++] = (byte)(value >> 8);
            target[offset++] = (byte)value;
        }

        private static int ReadInt32(byte[] source, ref int offset)
        {
            return unchecked((int)ReadUInt32(source, ref offset));
        }

        private static uint ReadUInt32(byte[] source, ref int offset)
        {
            uint value = ((uint)source[offset] << 24) |
                         ((uint)source[offset + 1] << 16) |
                         ((uint)source[offset + 2] << 8) |
                         source[offset + 3];
            offset += 4;
            return value;
        }
    }
}
