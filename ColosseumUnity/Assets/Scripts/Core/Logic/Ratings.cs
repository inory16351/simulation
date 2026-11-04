// 경기 평점 매니저 (Unity판 전용, FM 평점처럼): 모든 경기가 끝나면 전투 기록을 훑어 출전자마다 기록(입힌·받은 피해, 명중, 막기, 피하기,
// 치명타, 쓰러뜨림)을 모으고 6.0을 기준으로 3.0~10.0 평점을 매긴다. 결과 화면에 보이고, 공식 경기면 시즌 기록·대회 기록에 쌓인다.
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Colosseum.Core.Combat
{
    /// <summary>한 경기에서 한 검투사의 기록과 평점</summary>
    public class FighterStats
    {
        public double dealt, taken, maxHp, rating;
        public int attacks, hits, crits, blocks, parries, dodges, downs, kills;
        public bool won, downed, died;
    }
}

namespace Colosseum.Core
{
    public static class Ratings
    {
        public static bool Enabled => Awards.Enabled;

        /// <summary>전투 기록에서 출전자별 기록과 평점을 낸다</summary>
        public static Dictionary<string, FighterStats> Compute(MatchResult r)
        {
            var o = new Dictionary<string, FighterStats>();
            FighterStats Of(string id)
            {
                if (id == null) return null;
                if (!o.TryGetValue(id, out var st)) o[id] = st = new FighterStats();
                return st;
            }
            var ids = r.IsTeam ? r.sides[0].Concat(r.sides[1]) : new[] { r.winnerId, r.loserId };
            foreach (var id in ids) Of(id);

            foreach (var e in r.events)
            {
                if (e.t != "attack") continue;
                var att = Of(e.actor);
                var def = Of(e.target);
                if (att == null || def == null) continue;
                att.attacks++;
                if (e.maxHp > 0) def.maxHp = Math.Max(def.maxHp, e.maxHp);
                if (e.hit)
                {
                    att.hits++;
                    att.dealt += e.damage;
                    def.taken += e.damage;
                    if (e.crit) att.crits++;
                    if (e.blocked) def.blocks++;
                }
                else if (e.parried || e.blocked) def.parries++;
                else def.dodges++;
            }

            if (r.IsTeam)
            {
                for (int side = 0; side < 2; side++)
                    foreach (var id in r.sides[side]) Of(id).won = side == r.winnerSide;
                foreach (var d in r.downs)
                {
                    Of(d.Key).downed = true;
                    var by = Of(d.Value);
                    if (by != null) by.downs++;
                    if (r.deaths.Contains(d.Key)) { Of(d.Key).died = true; if (by != null) by.kills++; }
                }
            }
            else
            {
                var w = Of(r.winnerId);
                var l = Of(r.loserId);
                w.won = true;
                w.downs = 1;
                l.downed = true;
                if (r.died == r.loserId) { l.died = true; w.kills = 1; }
            }

            // 평점: 이겼나, 쓰러뜨렸나, 상대 체력을 얼마나 깎았나, 얼마나 맞았나, 정확했나, 잘 막고 피했나
            double oppHp = o.Values.Where(x => x.maxHp > 0).Select(x => x.maxHp).DefaultIfEmpty(100).Average();
            foreach (var st in o.Values)
            {
                double own = st.maxHp > 0 ? st.maxHp : oppHp;
                double v = 6.0 + (st.won ? 0.6 : -0.3)
                    + st.downs * 0.7 + st.kills * 0.2
                    + Math.Min(2.5, st.dealt / oppHp) * 0.9
                    - Math.Min(1.5, st.taken / own) * 0.7
                    + (st.attacks > 0 ? (st.hits / (double)st.attacks - 0.55) * 1.2 : 0)
                    + Math.Min(1.0, (st.dodges + st.parries + st.blocks * 0.5) * 0.12)
                    + st.crits * 0.1
                    - (st.downed ? 0.5 : 0) - (st.died ? 0.3 : 0);
                st.rating = Math.Round(Math.Max(3.0, Math.Min(10.0, v)), 1);
            }
            return o;
        }

        /// <summary>경기 결과에 기록을 붙이고, 공식 경기(official)면 출전자 시즌 기록에 평점을 쌓는다</summary>
        public static void Record(GameState s, MatchResult r, bool official)
        {
            if (!Enabled || r?.events == null) return;
            r.stats = Compute(r);
            if (!official) return;
            foreach (var (id, st) in r.stats)
            {
                if (!s.gladiators.TryGetValue(id, out var g)) continue;
                var rec = Career.SeasonRecord(s, g);
                rec.ratingSum = (rec.ratingSum ?? 0) + st.rating;
                rec.rated = (rec.rated ?? 0) + 1;
            }
        }

        /// <summary>평균 평점 (없으면 null)</summary>
        public static double? Average(double? sum, double? n) => n is double k && k > 0 ? Math.Round((sum ?? 0) / k, 2) : null;

        /// <summary>평점 글자색 (FM처럼 7.5 이상 녹색, 6.5 아래 붉은빛)</summary>
        public static string Tone(double rating) => rating >= 7.5 ? "good" : rating >= 6.5 ? "mid" : "bad";
    }
}
