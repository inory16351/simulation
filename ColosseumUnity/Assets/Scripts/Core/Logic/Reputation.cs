// 루두스 명성과 영입 의사 (src/core/reputation.ts)
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Colosseum.Core
{
    public class Demand
    {
        /// <summary>아예 오지 않는다</summary>
        public bool refuse;
        /// <summary>원하는 주급</summary>
        public double wage;
        public string reason;
    }

    public static class Reputation
    {
        /// <summary>루두스 명성 0~100: 리그 단계, 리그 순위, 스타들의 인기, 시설, 우승 기록</summary>
        public static double LudusReputation(GameState state, Ludus ludus)
        {
            var same = state.ludi.Values.Where(l => l.tier == ludus.tier && l.nation == ludus.nation).OrderByDescending(l => l.rp).ToList();
            int idx = same.FindIndex(l => l.id == ludus.id) + 1;
            int rank = idx != 0 ? idx : same.Count;
            double b = ludus.tier == 1 ? 45 : 18;
            double rankBonus = Math.Max(0, (same.Count - rank) * 1.6);
            var stars = new List<double>();
            foreach (var id in ludus.gladiators)
                if (state.gladiators.TryGetValue(id, out var g) && g != null && g.alive) stars.Add(g.popularity);
            stars = stars.OrderByDescending(v => v).Take(3).ToList();
            double starBonus = 0;
            if (stars.Count > 0)
            {
                double s = 0;
                foreach (var v in stars) s += v;
                starBonus = (s / stars.Count) * 0.2;
            }
            var f = ludus.facilities;
            double fsum = 0;
            fsum += f.barracks; fsum += f.training; fsum += f.medical; fsum += f.bath; fsum += f.arena; fsum += f.guard; fsum += f.academy;
            double facilityBonus = fsum * 0.6;
            return JsMath.Round(Math.Max(0, Math.Min(100, b + rankBonus + starBonus + facilityBonus + (ludus.titles ?? 0) * 6)));
        }

        public static string ReputationNames(double r) =>
            r >= 80 ? "대륙의 명문" : r >= 60 ? "이름난 루두스" : r >= 40 ? "알려진 루두스" : r >= 20 ? "지방 루두스" : "무명 루두스";

        /// <summary>사람의 '격' 0~100</summary>
        public static double StaffStanding(Staff s) => s.rating * 5;
        public static double GladiatorStanding(Gladiator g) => Math.Min(100, GladiatorRules.ComputeCA(g) * 0.55 + g.popularity * 0.35);

        /// <summary>격이 명성보다 높을수록 더 많이 요구하고, 너무 차이 나면 거절한다.</summary>
        public static Demand DemandFor(double standing, double baseWage, double reputation)
        {
            double gap = standing - reputation;
            if (gap > 35) return new Demand { refuse = true, wage = 0, reason = "이런 무명 루두스에는 관심이 없다" };
            double mult = gap > 0 ? 1 + gap / 30 : Math.Max(0.85, 1 + gap / 120);
            double wage = JsMath.Round(baseWage * mult);
            string reason = gap > 20 ? "가긴 하겠지만 그만한 값을 달라" : gap > 0 ? "조금 더 쳐 달라" : "명문에 들어가는 거라면 기꺼이";
            return new Demand { refuse = false, wage = wage, reason = reason };
        }

        /// <summary>제안을 받아들이는가: 원하는 주급 이상이면 수락, 조금 모자라면 확률</summary>
        public static bool AcceptsOffer(double offer, Demand demand, double roll)
        {
            if (demand.refuse) return false;
            if (offer >= demand.wage) return true;
            double sh = (demand.wage - offer) / demand.wage;
            return sh < 0.1 && roll < 1 - sh * 8;
        }
    }
}
