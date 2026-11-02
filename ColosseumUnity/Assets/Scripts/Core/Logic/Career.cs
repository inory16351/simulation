// 검투사 일대기: 시즌별 기록과 주요 사건 (src/core/career.ts)
using Colosseum.Core.Combat;
using System.Linq;

namespace Colosseum.Core
{
    public static class Career
    {
        public static Ludus LudusOf(GameState state, string gid)
        {
            foreach (var l in state.ludi.Values)
                if (l.gladiators.Contains(gid)) return l;
            return null;
        }

        /// <summary>이번 시즌 기록 줄을 찾고 없으면 만든다</summary>
        public static Colosseum.Core.SeasonRecord SeasonRecord(GameState state, Gladiator g)
        {
            var rec = g.history.FirstOrDefault(h => h.season == state.season);
            if (rec == null)
            {
                var l = LudusOf(state, g.id);
                double ca = GladiatorRules.ComputeCA(g);
                rec = new Colosseum.Core.SeasonRecord
                {
                    season = state.season, ludus = l?.name ?? "무소속", tier = l?.tier ?? 0,
                    apps = 0, wins = 0, losses = 0, kills = 0, caStart = ca, caEnd = ca, popularity = g.popularity, injuries = 0, earnings = 0,
                };
                g.history.Add(rec);
            }
            return rec;
        }

        public static void LogCareer(GameState state, Gladiator g, string text, string kind = "other")
        {
            g.career.Add(new CareerEvent { season = state.season, day = state.day, text = text, kind = kind });
            if (g.career.Count > 200) g.career.RemoveAt(0);
        }

        /// <summary>시즌이 끝날 때 기록을 마감한다</summary>
        public static void CloseSeason(GameState state, Gladiator g)
        {
            var rec = SeasonRecord(state, g);
            rec.caEnd = GladiatorRules.ComputeCA(g);
            rec.popularity = g.popularity;
        }
    }
}
