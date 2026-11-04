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

        /// <summary>경력에 적을 소속: 1군 또는 학당</summary>
        static Ludus Home(GameState state, Gladiator g)
        {
            var l = LudusOf(state, g.id);
            if (l != null || !FmRules.Enabled) return l;
            foreach (var x in state.ludi.Values)
                if (x.youth != null && x.youth.Contains(g.id)) return x;
            return null;
        }

        static bool SameClub(Colosseum.Core.SeasonRecord r, Ludus l) => r.ludusId != null ? r.ludusId == l.id : r.ludus == l.name;

        /// <summary>이번 시즌 기록 줄을 찾고 없으면 만든다. 시즌 중에 루두스를 옮겼으면 새 줄을 연다 (FM 경력처럼 이적마다 나뉜다)</summary>
        public static Colosseum.Core.SeasonRecord SeasonRecord(GameState state, Gladiator g)
        {
            var rec = g.history.LastOrDefault(h => h.season == state.season);
            var l = rec != null && !FmRules.Enabled ? null : Home(state, g);
            double ca = 0;
            if (rec != null)
            {
                // 무소속이거나 같은 루두스면 그 줄을 이어 쓴다
                if (l == null || SameClub(rec, l)) return rec;
                // 무소속으로 시작해 아직 뛰지 않은 줄은 새 소속으로 고쳐 쓴다
                if (rec.ludusId == null && rec.ludus == "무소속" && rec.apps == 0)
                {
                    rec.ludus = l.name;
                    rec.ludusId = l.id;
                    rec.tier = l.tier;
                    return rec;
                }
                ca = GladiatorRules.ComputeCA(g);
                rec.caEnd = ca;
                rec.popularity = g.popularity;
            }
            else ca = GladiatorRules.ComputeCA(g);
            rec = new Colosseum.Core.SeasonRecord
            {
                season = state.season, ludus = l?.name ?? "무소속", tier = l?.tier ?? 0, ludusId = FmRules.Enabled ? l?.id : null,
                apps = 0, wins = 0, losses = 0, kills = 0, caStart = ca, caEnd = ca, popularity = g.popularity, injuries = 0, earnings = 0,
            };
            g.history.Add(rec);
            return rec;
        }

        /// <summary>새 루두스에 들어왔을 때 (소속을 옮긴 뒤 부른다): 경력 줄을 열고 이적료를 적는다. fee 0 = 자유 이적, via = 이적료 대신 쓸 말</summary>
        public static void Joined(GameState state, Gladiator g, double? fee, string via = null)
        {
            var rec = SeasonRecord(state, g);
            if (!FmRules.Enabled) return;
            rec.fee = fee;
            rec.via = via;
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
