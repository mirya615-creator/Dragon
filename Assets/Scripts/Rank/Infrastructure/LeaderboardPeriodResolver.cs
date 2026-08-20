using System;

public static class LeaderboardPeriodResolver
{
    public static LeaderboardPeriod Resolve(
        LeaderboardPeriodType periodType,
        DateTimeOffset utcNow)
    {
        DateTimeOffset normalized = utcNow.ToUniversalTime();
        DateTime date = normalized.UtcDateTime.Date;

        DateTime start;
        DateTime end;
        string key;
        switch (periodType)
        {
            case LeaderboardPeriodType.Weekly:
                int daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
                start = date.AddDays(-daysSinceMonday);
                end = start.AddDays(7);
                key = "W-" + start.ToString("yyyyMMdd");
                break;
            case LeaderboardPeriodType.Monthly:
                start = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                end = start.AddMonths(1);
                key = "M-" + start.ToString("yyyyMM");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(periodType), periodType, null);
        }

        return new LeaderboardPeriod
        {
            Type = periodType,
            PeriodKey = key,
            StartsAtUnixMilliseconds = new DateTimeOffset(start).ToUnixTimeMilliseconds(),
            EndsAtUnixMilliseconds = new DateTimeOffset(end).ToUnixTimeMilliseconds()
        };
    }
}
