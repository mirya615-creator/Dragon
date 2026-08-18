using System;
using System.IO;
using GameShared.Telemetry;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class JsonlTelemetryTests
    {
        [Test]
        public void RecordWritesOneJsonLine()
        {
            var directory = Path.Combine(Path.GetTempPath(), "DragonBoundTests", Guid.NewGuid().ToString("N"));
            var filePath = Path.Combine(directory, "telemetry.jsonl");

            try
            {
                using (var telemetry = new JsonlTelemetry(filePath))
                {
                    telemetry.Record("run_started", 73, 1, "{}");
                    telemetry.Flush();
                    Assert.AreEqual(0, telemetry.WriteErrorCount);
                }

                var lines = File.ReadAllLines(filePath);
                Assert.AreEqual(1, lines.Length);
                StringAssert.Contains("\"eventName\":\"run_started\"", lines[0]);
                StringAssert.Contains("\"seed\":73", lines[0]);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public void WriterFailureIsIsolatedAndCounted()
        {
            var directory = Path.Combine(Path.GetTempPath(), "DragonBoundTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            try
            {
                using (var telemetry = new JsonlTelemetry(directory))
                {
                    Assert.DoesNotThrow(() => telemetry.Record("run_started", 73, 1, "{}"));
                    Assert.AreEqual(1, telemetry.WriteErrorCount);
                }
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
