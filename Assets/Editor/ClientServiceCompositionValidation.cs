using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ClientServiceCompositionValidation
{
    private const string ConfigPath = "Configuration/ClientServiceConfig";

    [MenuItem("DragonBound/Validation/Validate Client Services")]
    public static void Run()
    {
        ClientServiceConfig config = Resources.Load<ClientServiceConfig>(ConfigPath);
        Require(config != null, "ClientServiceConfig is missing.");
        Require(config.BackendMode == BackendMode.Local, "Development backend must remain Local.");

        IClientServices services = LocalServiceModule.Build(config);
        Require(services.Auth != null, "Auth service is missing.");
        Require(services.AuthSession != null, "Auth session service is missing.");
        Require(services.GuestIdentity != null, "Guest identity service is missing.");
        Require(services.GoogleOAuth != null, "Google OAuth service is missing.");
        Require(services.Energy != null, "Energy service is missing.");
        Require(services.Gold != null, "Gold service is missing.");
        Require(services.Rank != null, "Rank service is missing.");
        Require(services.Leaderboard != null, "Leaderboard service is missing.");
        Require(services.Merchant != null, "Merchant service is missing.");
        Require(services.Runes != null, "Rune service is missing.");
        Require(services.RewardedAds != null, "Rewarded ad service is missing.");
        Require(services.Share != null, "Share service is missing.");

        RequireDependency(services.Merchant, "goldGateway", services.Gold);
        RequireDependency(services.Leaderboard, "rankGateway", services.Rank);
        RequireSharedDependency(
            services.Rank,
            "leaderboardStore",
            services.Leaderboard,
            "periodStore");
        ValidatePersistentSessionStore();
        ValidateLeaderboardPeriods();
        ValidateLeaderboardRanking();

        Debug.Log("Client service composition validation passed in Local mode.");
    }

    private static void ValidatePersistentSessionStore()
    {
        const string validationKey = "dragonbound.validation.auth-session";
        var writer = new PersistentAuthSessionStore(validationKey);
        writer.Clear();

        try
        {
            writer.Set(new AuthSession
            {
                PlayerId = "validation-player",
                IsOffline = true,
                IsGuest = true
            });

            var reader = new PersistentAuthSessionStore(validationKey);
            Require(reader.TryRestore(out AuthSession restored), "Saved auth session was not restored.");
            Require(restored.PlayerId == "validation-player", "Restored auth session is incorrect.");
            Require(reader.IsValid(restored), "Restored auth session is invalid.");
            Require(
                !reader.IsValid(new AuthSession
                {
                    SchemaVersion = restored.SchemaVersion,
                    PlayerId = restored.PlayerId,
                    ExpiresAtUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1
                }),
                "Expired auth session was accepted.");
            reader.Clear();
            Require(!reader.TryRestore(out _), "Cleared auth session was restored.");
        }
        finally
        {
            writer.Clear();
        }
    }

    private static void ValidateLeaderboardPeriods()
    {
        var monday = new DateTimeOffset(2026, 8, 17, 0, 0, 0, TimeSpan.Zero);
        LeaderboardPeriod week = LeaderboardPeriodResolver.Resolve(
            LeaderboardPeriodType.Weekly,
            monday);
        Require(week.PeriodKey == "W-20260817", "Weekly period does not start on UTC Monday.");
        Require(
            week.StartsAtUnixMilliseconds == monday.ToUnixTimeMilliseconds(),
            "Weekly period start is incorrect.");
        Require(
            week.EndsAtUnixMilliseconds == monday.AddDays(7).ToUnixTimeMilliseconds(),
            "Weekly period end is incorrect.");

        LeaderboardPeriod sunday = LeaderboardPeriodResolver.Resolve(
            LeaderboardPeriodType.Weekly,
            monday.AddDays(6).AddHours(23).AddMinutes(59));
        Require(sunday.PeriodKey == week.PeriodKey, "UTC Sunday left the weekly period early.");
        Require(
            LeaderboardPeriodResolver.Resolve(
                LeaderboardPeriodType.Weekly,
                monday.AddDays(7)).PeriodKey != week.PeriodKey,
            "Next UTC Monday did not start a new weekly period.");
        Require(
            LeaderboardPeriodResolver.Resolve(
                LeaderboardPeriodType.Weekly,
                new DateTimeOffset(2027, 1, 1, 12, 0, 0, TimeSpan.Zero)).PeriodKey ==
            "W-20261228",
            "Cross-year weekly period key is incorrect.");

        LeaderboardPeriod month = LeaderboardPeriodResolver.Resolve(
            LeaderboardPeriodType.Monthly,
            new DateTimeOffset(2026, 8, 31, 23, 59, 59, TimeSpan.Zero));
        Require(month.PeriodKey == "M-202608", "Monthly period key is incorrect.");
        Require(
            month.EndsAtUnixMilliseconds ==
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            "Monthly period end is incorrect.");
        Require(
            LeaderboardPeriodResolver.Resolve(
                LeaderboardPeriodType.Monthly,
                new DateTimeOffset(2028, 2, 29, 23, 59, 59, TimeSpan.Zero))
                .EndsAtUnixMilliseconds ==
            new DateTimeOffset(2028, 3, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            "Leap-year monthly period end is incorrect.");
    }

    private static void ValidateLeaderboardRanking()
    {
        var earlier = new LeaderboardPlayer
        {
            PlayerId = "earlier",
            RankLevel = 9,
            Division = 3,
            CurrentStars = 2,
            TotalRankStars = 107,
            ReachedStateAtUnixMilliseconds = 1000
        };
        var later = new LeaderboardPlayer
        {
            PlayerId = "later",
            RankLevel = 9,
            Division = 3,
            CurrentStars = 2,
            TotalRankStars = 107,
            ReachedStateAtUnixMilliseconds = 2000
        };
        var marshalHigh = new LeaderboardPlayer
        {
            PlayerId = "marshal-high",
            RankLevel = 10,
            TotalRankStars = 140,
            ReachedStateAtUnixMilliseconds = 3000
        };
        var marshalLow = new LeaderboardPlayer
        {
            PlayerId = "marshal-low",
            RankLevel = 10,
            TotalRankStars = 120,
            ReachedStateAtUnixMilliseconds = 500
        };
        var higherDivision = new LeaderboardPlayer
        {
            PlayerId = "higher-division",
            RankLevel = 8,
            Division = 3,
            CurrentStars = 0,
            ReachedStateAtUnixMilliseconds = 4000
        };
        var higherStars = new LeaderboardPlayer
        {
            PlayerId = "higher-stars",
            RankLevel = 8,
            Division = 2,
            CurrentStars = 4,
            ReachedStateAtUnixMilliseconds = 100
        };
        var lowerStars = new LeaderboardPlayer
        {
            PlayerId = "lower-stars",
            RankLevel = 8,
            Division = 2,
            CurrentStars = 1,
            ReachedStateAtUnixMilliseconds = 50
        };

        var sorted = LeaderboardRankingRules.Sort(new[]
        {
            later, lowerStars, higherStars, marshalLow, earlier, higherDivision, marshalHigh
        });
        Require(sorted[0] == marshalHigh, "RANK_10 unlimited stars were not sorted descending.");
        Require(sorted[1] == marshalLow, "RANK_10 ordering is incorrect.");
        Require(sorted[2] == earlier, "Earlier arrival did not win the tied rank state.");
        Require(sorted[3] == later, "Later arrival tie-break is incorrect.");
        Require(sorted[4] == higherDivision, "Division was not sorted descending.");
        Require(sorted[5] == higherStars, "Current stars were not sorted descending.");
        Require(sorted[6] == lowerStars, "Lower current stars were ranked too high.");
    }

    private static void RequireDependency(object owner, string fieldName, object expected)
    {
        FieldInfo field = owner.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Require(field != null, $"{owner.GetType().Name}.{fieldName} is missing.");
        Require(
            ReferenceEquals(field.GetValue(owner), expected),
            $"{owner.GetType().Name}.{fieldName} does not use the shared service instance.");
    }

    private static void RequireSharedDependency(
        object left,
        string leftFieldName,
        object right,
        string rightFieldName)
    {
        FieldInfo leftField = left.GetType().GetField(
            leftFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo rightField = right.GetType().GetField(
            rightFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Require(leftField != null, $"{left.GetType().Name}.{leftFieldName} is missing.");
        Require(rightField != null, $"{right.GetType().Name}.{rightFieldName} is missing.");
        Require(
            ReferenceEquals(leftField.GetValue(left), rightField.GetValue(right)),
            "Rank and leaderboard do not share the same period store.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
