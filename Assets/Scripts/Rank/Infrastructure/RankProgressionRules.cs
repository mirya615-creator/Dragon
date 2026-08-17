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

    public static long CalculateTotalAfterDefeat(long totalRankStars)
    {
        long safeTotal = Math.Max(0, totalRankStars);
        PlayerRankState currentState = Calculate(safeTotal);

        // Recruit through Corporal never lose rank progress.
        if (currentState.Level <= 3) return safeTotal;

        // Dragon Marshal can lose bonus stars, but never drops below level 10.
        if (currentState.Level >= 10)
        {
            return Math.Max(DragonMarshalThreshold, safeTotal - 1);
        }

        // Sergeant through General lose one star and may be demoted.
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
            TotalRankStars = previous.TotalRankStars + 1
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
