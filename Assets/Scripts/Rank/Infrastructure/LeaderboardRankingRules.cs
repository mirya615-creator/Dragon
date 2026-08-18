using System;
using System.Collections.Generic;

public static class LeaderboardRankingRules
{
    public static List<LeaderboardPlayer> Sort(IEnumerable<LeaderboardPlayer> players)
    {
        var result = players != null
            ? new List<LeaderboardPlayer>(players)
            : new List<LeaderboardPlayer>();
        result.RemoveAll(player => player == null);
        result.Sort(Compare);
        return result;
    }

    private static int Compare(LeaderboardPlayer left, LeaderboardPlayer right)
    {
        int comparison = right.RankLevel.CompareTo(left.RankLevel);
        if (comparison != 0) return comparison;

        if (left.RankLevel >= 10)
        {
            comparison = right.TotalRankStars.CompareTo(left.TotalRankStars);
        }
        else
        {
            comparison = right.Division.CompareTo(left.Division);
            if (comparison == 0)
            {
                comparison = right.CurrentStars.CompareTo(left.CurrentStars);
            }
        }

        if (comparison != 0) return comparison;

        comparison = left.ReachedRankAtUnixMilliseconds.CompareTo(right.ReachedRankAtUnixMilliseconds);
        if (comparison != 0) return comparison;

        return string.Compare(left.PlayerId, right.PlayerId, StringComparison.Ordinal);
    }
}
