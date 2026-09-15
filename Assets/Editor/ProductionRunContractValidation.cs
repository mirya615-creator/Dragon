using System;
using System.Security.Cryptography;
using System.Text;
using DragonBound.Core;
using GameShared.Random;
using UnityEditor;
using UnityEngine;

public static class ProductionRunContractValidation
{
    private const string ClientConfigPath = "Configuration/ClientServiceConfig";
    private const string StageManifestPrefix = "Configuration/Stages/";

    [Serializable]
    private sealed class StageManifest
    {
        public int schema_version;
        public string stage_id;
        public string status;
        public string configuration_id;
        public string content_version;
        public string config_version;
        public int wave_count;
        public int[] enemy_counts_per_side;
        public float[] enemy_max_hp;
        public int[] boss_waves;
        public float start_preparation_seconds;
        public float spawn_interval_seconds;
        public float inter_wave_gap_seconds;
        public int starting_health;
        public int normal_breach_damage;
    }

    [MenuItem("DragonBound/Validation/Validate Production Run Contract %#p")]
    public static void Run()
    {
        ClientServiceConfig config = Resources.Load<ClientServiceConfig>(ClientConfigPath);
        Require(config != null, "ClientServiceConfig is missing.");
        Require(config.RequireAuthoritativeRunContract,
            "Authoritative Run contract enforcement must be enabled.");
        Require(!string.IsNullOrWhiteSpace(config.DefaultStageId), "Default Stage ID is missing.");
        Require(!string.IsNullOrWhiteSpace(config.ExpectedStageSnapshotDigest),
            "Expected Stage snapshot digest is missing.");

        TextAsset asset = Resources.Load<TextAsset>(StageManifestPrefix + config.DefaultStageId);
        Require(asset != null, "Published client Stage manifest is missing.");
        string canonicalJson = asset.text.Trim();
        StageManifest manifest = JsonUtility.FromJson<StageManifest>(canonicalJson);
        Require(manifest != null, "Stage manifest JSON is invalid.");
        ValidateManifest(config, manifest);

        string digest;
        using (SHA256 sha = SHA256.Create())
            digest = Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(canonicalJson)));
        Require(string.Equals(digest, config.ExpectedStageSnapshotDigest,
            StringComparison.OrdinalIgnoreCase), "Stage manifest digest does not match ClientServiceConfig.");

        // These calls also exercise the exact binary payload shape used by the online adapter.
        SharedRandomProtocolV1.DecodeRecruitRandom(
            SharedRandomProtocolV1.EncodeRecruitRandom(73), out _, out _);
        Require(SharedRandomProtocolV1.DecodeWaveRandom(
            SharedRandomProtocolV1.EncodeWaveRandom(73)) == 73,
            "Wave random envelope failed its round trip.");
        Require(SharedRandomProtocolV1.DecodeCriticalRandomSequence(
            SharedRandomProtocolV1.EncodeCriticalRandomSequence(null)).Length == 0,
            "Empty critical random sequence is not valid.");

        Debug.Log("Production Run client contract validation passed: " +
                  config.DefaultStageId + " / " + SharedRandomProtocolV1.Version + ".");
    }

    private static void ValidateManifest(ClientServiceConfig config, StageManifest manifest)
    {
        TwentyWavePressureConfiguration runtime =
            TwentyWavePressureConfiguration.CreateCoreLoopV2();
        Require(manifest.schema_version == 1, "Stage manifest schema is unsupported.");
        Require(manifest.stage_id == config.DefaultStageId, "Stage manifest ID is inconsistent.");
        Require(manifest.status == "published", "Stage manifest is not marked published.");
        Require(manifest.configuration_id == TwentyWavePressureConfiguration.ConfigurationId,
            "Stage configuration ID is inconsistent.");
        Require(manifest.content_version == config.ContentVersion,
            "Stage content version is inconsistent.");
        Require(manifest.config_version == config.ConfigVersion,
            "Stage config version is inconsistent.");
        Require(manifest.wave_count == TwentyWavePressureConfiguration.WaveCount,
            "Stage wave count is inconsistent.");
        Require(manifest.enemy_counts_per_side?.Length == manifest.wave_count,
            "Stage enemy count vector is incomplete.");
        Require(manifest.enemy_max_hp?.Length == manifest.wave_count,
            "Stage health vector is incomplete.");

        for (int index = 0; index < manifest.wave_count; index++)
        {
            int wave = index + 1;
            Require(manifest.enemy_counts_per_side[index] == runtime.GetWave(wave).EnemyCountPerSide,
                "Stage enemy count differs at wave " + wave + ".");
            Require(Mathf.Approximately(
                    manifest.enemy_max_hp[index],
                    TwentyWavePressureConfiguration.GetProductionMaxHitPoints(wave)),
                "Stage health differs at wave " + wave + ".");
        }
        Require(manifest.boss_waves != null && manifest.boss_waves.Length == 4,
            "Stage boss wave vector is invalid.");
        Require(manifest.boss_waves[0] == TwentyWavePressureConfiguration.SoulChainBossWave &&
                manifest.boss_waves[1] == TwentyWavePressureConfiguration.StormcallerBossWave &&
                manifest.boss_waves[2] == TwentyWavePressureConfiguration.BloodcrownBossWave &&
                manifest.boss_waves[3] == TwentyWavePressureConfiguration.WorldeaterBossWave,
            "Stage boss waves are inconsistent.");
        Require(Mathf.Approximately(manifest.start_preparation_seconds,
                TwentyWavePressureConfiguration.StartPreparationSeconds),
            "Stage start preparation is inconsistent.");
        Require(Mathf.Approximately(manifest.spawn_interval_seconds,
                TwentyWavePressureConfiguration.RegularSpawnIntervalSeconds),
            "Stage spawn interval is inconsistent.");
        Require(Mathf.Approximately(manifest.inter_wave_gap_seconds,
                TwentyWavePressureConfiguration.InterWaveSpawnGapSeconds),
            "Stage inter-wave gap is inconsistent.");
        Require(manifest.starting_health == BattleSettlementDefinition.InitialCurrentHeart,
            "Stage starting health is inconsistent.");
        Require(manifest.normal_breach_damage == BattleSettlementDefinition.NormalGoalDamage,
            "Stage breach damage is inconsistent.");
    }

    private static string Hex(byte[] bytes)
    {
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (byte value in bytes) builder.Append(value.ToString("x2"));
        return builder.ToString();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
