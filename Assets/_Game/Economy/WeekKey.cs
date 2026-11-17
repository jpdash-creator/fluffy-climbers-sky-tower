using System;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// Week key of the weekly boards, integer maths only, identical to the server rule (Tools/leaderboard_service/Logic/LeaderboardRules.WeekKey and the client
    /// Leaderboard/LeaderboardTime.WeekKey): a week runs from Monday 00:00 UTC (owner 2026-11-16; before that Monday 00:00 India time) to the next Monday 00:00 UTC;
    /// the key is the ISO date of that Monday in UTC ("2026-11-09"). Pure functions with no clock of their own: the caller passes the UTC instant (ProgressService.WeekClock
    /// is the injectable clock). Lives in Economy so the save logic needs no reference to the Leaderboard module.
    /// </summary>
    public static class WeekKeys
    {
        /// <summary>Offset of the week calendar from UTC in seconds: 0 since 2026-11-16 (was 19800 = India time).</summary>
        public const int WeekOffsetSeconds = 0;
        const long DaySeconds = 86400;

        static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>Week key of a Unix time in seconds (UTC).</summary>
        public static string KeyOf(long unixSeconds)
        {
            long days = FloorDiv(unixSeconds + WeekOffsetSeconds, DaySeconds);   // days since 1970-01-01 on the UTC calendar
            long monday = days - MondayIndex(days);
            int y, m, d;
            CivilFromDays(monday, out y, out m, out d);
            var c = new char[10];
            c[0] = (char)('0' + y / 1000 % 10); c[1] = (char)('0' + y / 100 % 10); c[2] = (char)('0' + y / 10 % 10); c[3] = (char)('0' + y % 10);
            c[4] = '-';
            c[5] = (char)('0' + m / 10); c[6] = (char)('0' + m % 10);
            c[7] = '-';
            c[8] = (char)('0' + d / 10); c[9] = (char)('0' + d % 10);
            return new string(c);
        }

        /// <summary>Week key of a UTC instant (a non-UTC DateTime is converted first).</summary>
        public static string KeyOf(DateTime utc)
        {
            return KeyOf(ToUnix(utc));
        }

        /// <summary>The first second (Unix, UTC) of the week that contains the given Unix time = Monday 00:00 UTC.</summary>
        public static long StartOfWeekUnix(long unixSeconds)
        {
            long days = FloorDiv(unixSeconds + WeekOffsetSeconds, DaySeconds);
            return (days - MondayIndex(days)) * DaySeconds - WeekOffsetSeconds;
        }

        /// <summary>The first second (Unix, UTC) of the NEXT week = the moment the boards reset.</summary>
        public static long EndOfWeekUnix(long unixSeconds) { return StartOfWeekUnix(unixSeconds) + 7 * DaySeconds; }

        public static long ToUnix(DateTime utc)
        {
            DateTime u = utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return (long)Math.Floor((u - Epoch).TotalSeconds);
        }

        /// <summary>0 = Monday .. 6 = Sunday for a day count since 1970-01-01 (a Thursday).</summary>
        static int MondayIndex(long days) { return (int)(((days + 3) % 7 + 7) % 7); }

        static long FloorDiv(long a, long b) { long q = a / b; return (a % b != 0 && ((a < 0) != (b < 0))) ? q - 1 : q; }

        /// <summary>Days since 1970-01-01 to a proleptic Gregorian date (Howard Hinnant's civil_from_days).</summary>
        static void CivilFromDays(long z, out int y, out int m, out int d)
        {
            z += 719468;
            long era = FloorDiv(z, 146097);
            long doe = z - era * 146097;
            long yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365;
            long yy = yoe + era * 400;
            long doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
            long mp = (5 * doy + 2) / 153;
            d = (int)(doy - (153 * mp + 2) / 5 + 1);
            m = (int)(mp < 10 ? mp + 3 : mp - 9);
            y = (int)(yy + (m <= 2 ? 1 : 0));
        }
    }
}
