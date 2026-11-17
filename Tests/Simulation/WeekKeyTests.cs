using System;
using FluffyClimbers.Economy;
using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// The weekly crown key (Economy/WeekKey.cs) must equal the server rule (Tools/leaderboard_service/Logic/LeaderboardRules.WeekMath.WeekKey): since 2026-11-16 a week starts Monday 00:00 UTC
    /// and the key is the ISO date of that Monday in UTC. The reference below is plain DateTime code.
    /// </summary>
    public class WeekKeyTests
    {
        static string ReferenceWeekKey(DateTime utcNow)
        {
            DateTime u = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
            int back = ((int)u.DayOfWeek + 6) % 7;
            return u.Date.AddDays(-back).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        static DateTime Utc(int y, int mo, int d, int h, int mi, int s) { return new DateTime(y, mo, d, h, mi, s, DateTimeKind.Utc); }

        [Theory]
        [InlineData(2026, 11, 8, 23, 59, 59, "2026-11-02")]   // Sunday 23:59:59 UTC: still the old week
        [InlineData(2026, 11, 9, 0, 0, 0, "2026-11-09")]      // Monday 00:00:00 UTC: the new week
        [InlineData(2026, 11, 8, 18, 30, 0, "2026-11-02")]    // the old India-time rollover instant is now mid Sunday: no change
        [InlineData(2026, 11, 11, 12, 0, 0, "2026-11-09")]    // mid week
        [InlineData(2026, 11, 15, 23, 59, 59, "2026-11-09")]  // end of that week
        [InlineData(2026, 11, 16, 0, 0, 0, "2026-11-16")]
        [InlineData(2027, 1, 3, 23, 59, 59, "2026-12-28")]    // across a year change
        [InlineData(2027, 1, 4, 0, 0, 0, "2027-01-04")]
        [InlineData(2028, 2, 29, 0, 0, 0, "2028-02-28")]      // leap day (a Tuesday)
        [InlineData(1970, 1, 1, 0, 0, 0, "1969-12-29")]       // the epoch is a Thursday
        public void KeysAroundTheBoundary(int y, int mo, int d, int h, int mi, int s, string expected)
        {
            DateTime t = Utc(y, mo, d, h, mi, s);
            Assert.Equal(expected, WeekKeys.KeyOf(t));
            Assert.Equal(expected, ReferenceWeekKey(t));
        }

        [Fact]
        public void MatchesTheReferenceEveryMinuteAroundManyBoundariesAndEverySixHoursForSevenYears()
        {
            DateTime boundary = Utc(2026, 11, 9, 0, 0, 0);
            for (int w = -60; w <= 60; w++)
                for (int m = -3; m <= 3; m++)
                {
                    DateTime t = boundary.AddDays(7 * w).AddMinutes(m);
                    Assert.Equal(ReferenceWeekKey(t), WeekKeys.KeyOf(t));
                }
            for (DateTime t = Utc(2024, 1, 1, 0, 0, 0); t < Utc(2031, 1, 1, 0, 0, 0); t = t.AddHours(6))
                Assert.Equal(ReferenceWeekKey(t), WeekKeys.KeyOf(t));
        }

        [Fact]
        public void WeekStartAndEndAreMondayMidnightUtc()
        {
            long t = WeekKeys.ToUnix(Utc(2026, 11, 11, 12, 0, 0));
            Assert.Equal(WeekKeys.ToUnix(Utc(2026, 11, 9, 0, 0, 0)), WeekKeys.StartOfWeekUnix(t));
            Assert.Equal(WeekKeys.ToUnix(Utc(2026, 11, 16, 0, 0, 0)), WeekKeys.EndOfWeekUnix(t));
            Assert.Equal(WeekKeys.StartOfWeekUnix(t), WeekKeys.StartOfWeekUnix(WeekKeys.StartOfWeekUnix(t)));
            Assert.NotEqual(WeekKeys.KeyOf(WeekKeys.StartOfWeekUnix(t) - 1), WeekKeys.KeyOf(WeekKeys.StartOfWeekUnix(t)));
        }

        [Fact]
        public void TheRolloverMovesBy5HoursAndThirtyMinutesComparedWithTheOldIndiaWeek()
        {
            // old rule: the week "2026-11-09" began Sunday 2026-11-08 18:30 UTC; now it begins Monday 2026-11-09 00:00 UTC (5.5 hours later)
            Assert.Equal(TimeSpan.FromMinutes(330), Utc(2026, 11, 9, 0, 0, 0) - Utc(2026, 11, 8, 18, 30, 0));
            Assert.Equal("2026-11-02", WeekKeys.KeyOf(Utc(2026, 11, 8, 18, 30, 0)));
        }

        [Fact]
        public void ALocalKindTimeIsConvertedToUtcFirst()
        {
            DateTime utc = Utc(2026, 11, 9, 0, 0, 0);
            Assert.Equal(WeekKeys.KeyOf(utc), WeekKeys.KeyOf(utc.ToLocalTime()));
        }
    }
}
