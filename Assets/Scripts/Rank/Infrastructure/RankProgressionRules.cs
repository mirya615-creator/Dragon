using System;

public static class RankProgressionRules
{
    public const long DragonMarshalThreshold = 108;

    private static readonly string[] RankNames =
    {
        string.Empty,
        "Recruit",
        "Private",
        "Corporal",
        "Sergeant",
        "Lieutenant",
        "Captain",
        "Major",
        "Colonel",
        "General",
        "Dragon Marshal"
    };

    public static PlayerRankState Calculate(long totalRankStars)
    {
        long safeTotal = Math.Max(0, totalRankStars);
        long levelStart = 0;

        for (int level = 1; level <= 9; level++)
        {
            int requiredStars = GetStarsPerDivision(level);
            int levelCapacity = requiredStars * 3;
            if (safeTotal < levelStart + levelCapacity)
            {
                int progressInLevel = (int)(safeTotal - levelStart);
                int divisionIndex = progressInLevel / requiredStars;
                return new PlayerRankState
                {
                    Level = level,
                    RankName = RankNames[level],
                    Division = divisionIndex + 1,
                    CurrentStars = progressInLevel % requiredStars,
                    RequiredStars = requiredStars,
                    TotalRankStars = safeTotal
                };
            }

            levelStart += levelCapacity;
        }

        return new PlayerRankState
        {
            Level = 10,
            RankName = RankNames[10],
            Division = 0,
            CurrentStars = (int)Math.Min(int.MaxValue, safeTotal - DragonMarshalThreshold),
            RequiredStars = 0,
            TotalRankStars = safeTotal
        };
    }

    public static PlayerRankState FromServerState(string rankId, int segment, long stars)
    {
        int level = ParseRankLevel(rankId);
        long safeStars = Math.Max(0, stars);
        if (level >= 10) return Calculate(DragonMarshalThreshold + safeStars);

        int safeSegment = Math.Max(1, Math.Min(3, segment));
        long total = 0;
        for (int previousLevel = 1; previousLevel < level; previousLevel++)
            total += GetStarsPerDivision(previousLevel) * 3L;
        total += (safeSegment - 1) * (long)GetStarsPerDivision(level);
        total += Math.Min(safeStars, GetStarsPerDivision(level) - 1L);
        return Calculate(total);
    }

    public static long CalculateTotalAfterDefeat(long totalRankStars)
    {
        long safeTotal = Math.Max(0, totalRankStars);
        return Math.Max(0, safeTotal - 1);
    }

    public static PlayerRankState CreateFullPromotionState(PlayerRankState previous)
    {
        if (previous == null) return null;
        return new PlayerRankState
        {
            Level = previous.Level,
            RankName = previous.RankName,
            Division = previous.Division,
            CurrentStars = previous.RequiredStars,
            RequiredStars = previous.RequiredStars,
            TotalRankStars = previous.TotalRankStars + 1,
            ReachedStateAtUnixMilliseconds = previous.ReachedStateAtUnixMilliseconds
        };
    }

    public static string GetDisplayName(PlayerRankState state)
    {
        if (state == null) return string.Empty;
        if (state.Level >= 10) return $"{state.RankName}  ★{state.CurrentStars}";
        return $"{state.RankName} {GetDivisionLabel(state.Division)}";
    }

    private static int GetStarsPerDivision(int level)
    {
        if (level <= 3) return 3;
        if (level <= 6) return 4;
        return 5;
    }

    private static int ParseRankLevel(string rankId)
    {
        if (!string.IsNullOrWhiteSpace(rankId) &&
            rankId.StartsWith("RANK_", StringComparison.Ordinal) &&
            int.TryParse(rankId.Substring(5), out int level))
            return Math.Max(1, Math.Min(10, level));
        return 1;
    }

    private static string GetDivisionLabel(int division)
    {
        switch (division)
        {
            case 3: return "III";
            case 2: return "II";
            case 1: return "I";
            default: return string.Empty;
        }
    }
}
