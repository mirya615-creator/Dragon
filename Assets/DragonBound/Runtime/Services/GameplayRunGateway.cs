using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DragonBound.AI;
using UnityEngine;

namespace DragonBound.Services
{
    [Serializable]
    public sealed class StartGameplayRunRequest
    {
        public string PlayerId;
        public string GameMode;
        public string ClientRunNonce;
        public string ClientVersion;
        public string ContentVersion;
        public bool UseDiagnosticSeed;
        public int DiagnosticSeed;
        /// <summary>Rank level 1-10 supplied by the account/matchmaking boundary.</summary>
        public int PlayerRankLevel = 1;
    }

    [Serializable]
    public sealed class StartGameplayRunResult
    {
        public string RunId;
        public int RunSeed;
        public int PlayerRecruitSeed;
        public int AiRecruitSeed;
        public int CombatSeed;
        public string RulesVersion;
        public int StartingResources;
        public int ReconnectGraceSeconds;
        public int AfkTimeoutSeconds;
        public int PlayerRankLevel;
        public string AiProfile;
        public int AiDecisionSeed;
        public bool IsRecoveryMatch;
        public string AiAlgorithmVersion;
    }

    public enum ServerMatchResult
    {
        Pending,
        Victory,
        Defeat,
        NoContest
    }

    public enum GameplaySettlementType
    {
        Normal,
        Retry,
        Compensation
    }

    public enum GameplayTerminationReason
    {
        Natural,
        PlayerSurrender,
        DisconnectTimeout,
        AfkTimeout,
        ServerTimeout,
        ServerCrash,
        VersionMismatch,
        Unknown
    }

    public enum GameplayFaultAttribution
    {
        None,
        Player,
        Server,
        Unknown
    }

    [Serializable]
    public sealed class GameplayRecruitCardDto
    {
        public string RuntimeId;
        public string ConfigId;
        public string SourceInstanceId;
        public int Kind;
        public int SlotIndex;
    }

    [Serializable]
    public sealed class RecruitGameplayRequest
    {
        public string RunId;
        public string PlayerId;
        public string TeamSide;
        public int RecruitmentNumber;
        public int CurrentResources;
        public long StateVersion;
        public string IdempotencyKey;
        public List<GameplayRecruitCardDto> ProposedCards = new List<GameplayRecruitCardDto>();
    }

    [Serializable]
    public sealed class RecruitGameplayResult
    {
        public bool Accepted;
        public int RecruitmentNumber;
        public int ResourcesAfter;
        public int NextRecruitCost;
        public long StateVersion;
        public List<GameplayRecruitCardDto> Cards = new List<GameplayRecruitCardDto>();
    }

    [Serializable]
    public sealed class FinishGameplayRunRequest
    {
        public string RunId;
        public string PlayerId;
        public ServerMatchResult ProposedResult;
        public GameplaySettlementType SettlementType;
        public GameplayTerminationReason TerminationReason;
        public GameplayFaultAttribution FaultAttribution;
        public int ReachedWave;
        public int FinalResources;
        public int RecruitmentCount;
        public List<string> FormedHeroIds = new List<string>();
        public string GameplaySnapshotHash;
        public string IdempotencyKey;
    }

    [Serializable]
    public sealed class FinishGameplayRunResult
    {
        public bool Accepted;
        public string RunId;
        public ServerMatchResult Result;
        public GameplaySettlementType SettlementType;
        public GameplayTerminationReason TerminationReason;
        public GameplayFaultAttribution FaultAttribution;
        public bool ApplyRank;
        public bool ApplySeasonProgress;
        public bool CountCompletedRun;
        public bool GrantRewards;
    }

    /// <summary>
    /// Unary gameplay boundary. Local gameplay uses the deterministic implementation below;
    /// a Go adapter can replace it without changing the gameplay bootstrap or UI.
    /// </summary>
    public interface IGameplayRunGateway
    {
        Task<StartGameplayRunResult> StartRunAsync(
            StartGameplayRunRequest request,
            CancellationToken cancellationToken);

        Task<RecruitGameplayResult> RecruitAsync(
            RecruitGameplayRequest request,
            CancellationToken cancellationToken);

        Task<FinishGameplayRunResult> FinishRunAsync(
            FinishGameplayRunRequest request,
            CancellationToken cancellationToken);
    }

    public static class GameplayRunGatewayRegistry
    {
        private static IGameplayRunGateway current;

        public static IGameplayRunGateway Current => current ??= new LocalGameplayRunGateway();

        public static void Install(IGameplayRunGateway gateway)
        {
            current = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        public static void ResetForTests()
        {
            current = null;
        }
    }

    /// <summary>
    /// Persists the launch nonce between Main's energy debit and gameplay bootstrap.
    /// The same value is used as the energy and StartRun idempotency key.
    /// </summary>
    public static class GameplayLaunchContext
    {
        private const string PlayerKey = "dragonbound.gameplay-launch.player";
        private const string NonceKey = "dragonbound.gameplay-launch.nonce";
        private const string RankLevelKey = "dragonbound.gameplay-launch.rank-level";

        public static string GetOrCreateNonce(string playerId)
        {
            return GetOrCreateNonce(playerId, 1);
        }

        public static string GetOrCreateNonce(string playerId, int playerRankLevel)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                throw new ArgumentException("Player ID is required.", nameof(playerId));
            string storedPlayer = PlayerPrefs.GetString(PlayerKey, string.Empty);
            string nonce = PlayerPrefs.GetString(NonceKey, string.Empty);
            if (storedPlayer == playerId && !string.IsNullOrWhiteSpace(nonce))
            {
                PlayerPrefs.SetInt(RankLevelKey, Math.Max(1, Math.Min(10, playerRankLevel)));
                PlayerPrefs.Save();
                return nonce;
            }
            nonce = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(PlayerKey, playerId);
            PlayerPrefs.SetString(NonceKey, nonce);
            PlayerPrefs.SetInt(RankLevelKey, Math.Max(1, Math.Min(10, playerRankLevel)));
            PlayerPrefs.Save();
            return nonce;
        }

        public static bool TryGet(out string playerId, out string nonce)
        {
            playerId = PlayerPrefs.GetString(PlayerKey, string.Empty);
            nonce = PlayerPrefs.GetString(NonceKey, string.Empty);
            return !string.IsNullOrWhiteSpace(playerId) && !string.IsNullOrWhiteSpace(nonce);
        }

        public static bool TryGet(out string playerId, out string nonce, out int playerRankLevel)
        {
            bool found = TryGet(out playerId, out nonce);
            playerRankLevel = Math.Max(1, Math.Min(10, PlayerPrefs.GetInt(RankLevelKey, 1)));
            return found;
        }

        public static void Complete(string nonce)
        {
            if (string.IsNullOrWhiteSpace(nonce) ||
                PlayerPrefs.GetString(NonceKey, string.Empty) != nonce) return;
            PlayerPrefs.DeleteKey(PlayerKey);
            PlayerPrefs.DeleteKey(NonceKey);
            PlayerPrefs.DeleteKey(RankLevelKey);
            PlayerPrefs.Save();
        }
    }

    public sealed class LocalGameplayRunGateway : IGameplayRunGateway
    {
        public const string LocalRulesVersion = "DragonBound.Gameplay.v1";
        public const string LocalAiAlgorithmVersion = "ai.strategy.v1";
        private const string StartKeyPrefix = "dragonbound.gameplay-start.";
        private const string RecoveryDefeatKeyPrefix = "dragonbound.ai-recovery.defeats.";

        public Task<StartGameplayRunResult> StartRunAsync(
            StartGameplayRunRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();

            string startKey = string.IsNullOrWhiteSpace(request.ClientRunNonce)
                ? string.Empty
                : StartKeyPrefix + HashKey(request.ClientRunNonce);
            if (!string.IsNullOrEmpty(startKey) && PlayerPrefs.HasKey(startKey))
            {
                StartGameplayRunResult stored = JsonUtility.FromJson<StartGameplayRunResult>(
                    PlayerPrefs.GetString(startKey, string.Empty));
                if (stored != null && !string.IsNullOrWhiteSpace(stored.RunId))
                    return Task.FromResult(stored);
            }

            var runSeed = request.UseDiagnosticSeed ? request.DiagnosticSeed : CreateRandomSeed();
            var runId = request.UseDiagnosticSeed
                ? $"diagnostic.{runSeed}"
                : Guid.NewGuid().ToString("N");
            int playerRankLevel = Math.Max(1, Math.Min(10, request.PlayerRankLevel));
            int normalDefeats = LoadNormalDefeatStreak(request.PlayerId);
            bool recoveryMatch = AiRecoveryPolicy.ShouldStartRecovery(playerRankLevel, normalDefeats);
            AiStrategyProfileId normalProfile = AiRankProfileMapping.FromRankLevel(playerRankLevel);
            AiStrategyProfileId effectiveProfile = AiRecoveryPolicy.ResolveEffectiveProfile(
                normalProfile,
                recoveryMatch);
            var result = new StartGameplayRunResult
            {
                RunId = runId,
                RunSeed = runSeed,
                PlayerRecruitSeed = unchecked(runSeed ^ 0x13579BDF),
                AiRecruitSeed = unchecked(runSeed ^ 0x2468ACE0),
                CombatSeed = runSeed,
                RulesVersion = LocalRulesVersion,
                StartingResources = 20,
                ReconnectGraceSeconds = 90,
                AfkTimeoutSeconds = 180,
                PlayerRankLevel = playerRankLevel,
                AiProfile = effectiveProfile.ToString(),
                AiDecisionSeed = DeriveSeed(runSeed, "ai.decision"),
                IsRecoveryMatch = recoveryMatch,
                AiAlgorithmVersion = LocalAiAlgorithmVersion
            };
            if (recoveryMatch)
            {
                // A recovery ticket is consumed by exactly one successfully-created Run.
                SaveNormalDefeatStreak(request.PlayerId, 0);
            }
            if (!string.IsNullOrEmpty(startKey))
            {
                PlayerPrefs.SetString(startKey, JsonUtility.ToJson(result));
                PlayerPrefs.Save();
            }
            return Task.FromResult(result);
        }

        public Task<RecruitGameplayResult> RecruitAsync(
            RecruitGameplayRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            var cost = checked(10 + (2 * (request.RecruitmentNumber - 1)));
            var accepted = request.RecruitmentNumber > 0 &&
                           request.CurrentResources >= cost &&
                           request.ProposedCards != null &&
                           request.ProposedCards.Count == 5;
            return Task.FromResult(new RecruitGameplayResult
            {
                Accepted = accepted,
                RecruitmentNumber = request.RecruitmentNumber,
                ResourcesAfter = accepted ? request.CurrentResources - cost : request.CurrentResources,
                NextRecruitCost = checked(10 + (2 * request.RecruitmentNumber)),
                StateVersion = accepted ? request.StateVersion + 1 : request.StateVersion,
                Cards = !accepted
                    ? new List<GameplayRecruitCardDto>()
                    : new List<GameplayRecruitCardDto>(request.ProposedCards)
            });
        }

        public Task<FinishGameplayRunResult> FinishRunAsync(
            FinishGameplayRunRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(request.RunId))
                throw new ArgumentException("Run ID is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
                throw new ArgumentException("Idempotency key is required.", nameof(request));

            ServerMatchResult result = ResolveLocalResult(request);
            bool normalResult = result == ServerMatchResult.Victory ||
                                result == ServerMatchResult.Defeat;
            if (!string.IsNullOrWhiteSpace(request.PlayerId))
            {
                int currentStreak = LoadNormalDefeatStreak(request.PlayerId);
                int updatedStreak = AiRecoveryPolicy.UpdateDefeatStreak(
                    currentStreak,
                    normalResult,
                    result == ServerMatchResult.Victory);
                SaveNormalDefeatStreak(request.PlayerId, updatedStreak);
            }
            return Task.FromResult(new FinishGameplayRunResult
            {
                Accepted = true,
                RunId = request.RunId,
                Result = result,
                SettlementType = request.SettlementType,
                TerminationReason = request.TerminationReason,
                FaultAttribution = request.FaultAttribution,
                ApplyRank = normalResult,
                ApplySeasonProgress = normalResult,
                CountCompletedRun = normalResult,
                GrantRewards = normalResult
            });
        }

        private static ServerMatchResult ResolveLocalResult(FinishGameplayRunRequest request)
        {
            if (request.FaultAttribution == GameplayFaultAttribution.Server)
                return ServerMatchResult.NoContest;
            if (request.FaultAttribution == GameplayFaultAttribution.Unknown ||
                request.TerminationReason == GameplayTerminationReason.Unknown)
                return ServerMatchResult.Pending;
            if (request.FaultAttribution == GameplayFaultAttribution.Player ||
                request.TerminationReason == GameplayTerminationReason.PlayerSurrender ||
                request.TerminationReason == GameplayTerminationReason.DisconnectTimeout ||
                request.TerminationReason == GameplayTerminationReason.AfkTimeout)
                return ServerMatchResult.Defeat;
            return request.ProposedResult;
        }

        private static int CreateRandomSeed()
        {
            var bytes = new byte[4];
            using (var generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(bytes);
            }

            return BitConverter.ToInt32(bytes, 0);
        }

        internal static int DeriveSeed(int seed, string stream)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (var character in stream)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }

                hash ^= (uint)seed;
                hash *= 16777619u;
                return (int)hash;
            }
        }

        private static int LoadNormalDefeatStreak(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) return 0;
            return Math.Max(0, PlayerPrefs.GetInt(
                RecoveryDefeatKeyPrefix + HashKey(playerId),
                0));
        }

        private static void SaveNormalDefeatStreak(string playerId, int value)
        {
            if (string.IsNullOrWhiteSpace(playerId)) return;
            PlayerPrefs.SetInt(
                RecoveryDefeatKeyPrefix + HashKey(playerId),
                Math.Max(0, value));
            PlayerPrefs.Save();
        }

        private static string HashKey(string value)
        {
            using (SHA256 hash = SHA256.Create())
            {
                return Convert.ToBase64String(hash.ComputeHash(
                        System.Text.Encoding.UTF8.GetBytes(value)))
                    .Replace('/', '_').Replace('+', '-').TrimEnd('=');
            }
        }
    }
}
