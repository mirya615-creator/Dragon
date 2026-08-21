using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace GameShared.Telemetry
{
    public interface ITelemetry
    {
        int WriteErrorCount { get; }
        void Record(string eventName, int seed, long sequence, string payloadJson);
        void Flush();
    }

    public sealed class JsonlTelemetry : ITelemetry, IAnalyticsSink, IDisposable
    {
        private readonly string filePath;
        private StreamWriter writer;

        public JsonlTelemetry(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A telemetry path is required.", nameof(path));
            }

            filePath = Path.GetFullPath(path);
        }

        public int WriteErrorCount { get; private set; }

        public void Record(string eventName, int seed, long sequence, string payloadJson)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(eventName) || sequence < 1)
                {
                    throw new ArgumentException("Telemetry event data is invalid.");
                }

                var envelope = new TelemetryEnvelope
                {
                    eventName = eventName,
                    seed = seed,
                    sequence = sequence,
                    timestampUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    payloadJson = payloadJson ?? string.Empty
                };

                WriteLine(JsonUtility.ToJson(envelope, false));
            }
            catch (Exception)
            {
                WriteErrorCount++;
            }
        }

        /// <summary>Writes the V1 analytics event as one top-level JSONL object.</summary>
        public bool Record(AnalyticsEvent value)
        {
            try
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                WriteLine(JsonUtility.ToJson(value, false));
                return true;
            }
            catch (Exception)
            {
                WriteErrorCount++;
                return false;
            }
        }

        public void Flush()
        {
            try
            {
                writer?.Flush();
            }
            catch (Exception)
            {
                WriteErrorCount++;
            }
        }

        public void Dispose()
        {
            writer?.Dispose();
            writer = null;
        }

        private void EnsureWriter()
        {
            if (writer != null)
            {
                return;
            }

            var directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new IOException("Telemetry path has no directory.");
            }

            Directory.CreateDirectory(directory);
            writer = new StreamWriter(filePath, true, new UTF8Encoding(false));
        }

        private void WriteLine(string json)
        {
            EnsureWriter();
            writer.WriteLine(json);
        }

        [Serializable]
        private sealed class TelemetryEnvelope
        {
            public string eventName;
            public int seed;
            public long sequence;
            public string timestampUtc;
            public string payloadJson;
        }
    }
}
