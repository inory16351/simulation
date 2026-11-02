// 일 단위 달력 (src/core/calendar.ts). 1시즌 = 6달 × 28일 = 168일
using Colosseum.Core.Combat;
using System;

namespace Colosseum.Core
{
    public static class Calendar
    {
        public const int DaysPerMonth = 28;
        public static readonly string[] Months = { "서리달", "씨앗달", "꽃달", "불꽃달", "수확달", "그림자달" };
        public const int DaysPerSeason = DaysPerMonth * 6;
        public const int PaydayInterval = 7;

        public static string MonthOf(int day) => Months[(int)Math.Floor((day - 1) / (double)DaysPerMonth)];

        public static int DayOfMonth(int day) => ((day - 1) % DaysPerMonth) + 1;

        /// <summary>"서리달 3일"</summary>
        public static string DateLabel(int day) => $"{MonthOf(day)} {DayOfMonth(day)}일";

        /// <summary>시즌을 넘어 이어지는 절대 일수</summary>
        public static int AbsDay(int season, int day) => (season - 1) * DaysPerSeason + day;

        public static (int season, int day) FromAbs(double abs)
        {
            return ((int)Math.Floor((abs - 1) / DaysPerSeason) + 1, (int)((abs - 1) % DaysPerSeason) + 1);
        }

        public static bool IsPayday(int day) => day % PaydayInterval == 0;

        /// <summary>다음 급료일까지 남은 날 (오늘이 급료일이면 0)</summary>
        public static int DaysToPayday(int day)
        {
            int r = day % PaydayInterval;
            return r == 0 ? 0 : PaydayInterval - r;
        }
    }
}
