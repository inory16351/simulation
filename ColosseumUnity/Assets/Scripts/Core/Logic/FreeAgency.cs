// AI 루두스의 무소속 영입 (Unity판 전용, FM식): 급료일마다 AI가 스스로 판단한다 —
// 인원이 기본(aiRoster)보다 적으면 채우고, 등록 자리(15)가 남고 금고에 여유(주급 반 시즌치)가 있으면 주전(능력 5위)보다 나은 무소속을 더 데려온다.
// 어느 쪽이든 새 주급을 더해도 주급 예산(Economy.WageBudget) 안이어야 한다 — 감당 못 하면 포기.
// 검투사가 올 마음이 있어야 하고(명성 격차), 우리가 역제안에 답해야 하는 무소속만 건드리지 않는다 (제안을 보낸 정도면 겹쳐서 먼저 데려갈 수 있다). 무소속 명단은 우리 몫으로 12명은 남긴다.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Colosseum.Core
{
    public static class FreeAgency
    {
        public static bool Enabled => FmRules.Enabled;

        static double Roster => FmRules.AiRoster;

        /// <summary>우리가 답해야 하는 역제안이 걸린 무소속 (다음 날로 못 넘어가는 일이 사라지면 안 된다)</summary>
        static bool Busy(GameState s, string id) => s.decisions?.Any(d => d.gladiatorId == id) ?? false;

        /// <summary>이 루두스가 데려올 만하고, 올 마음이 있고, 감당할 수 있는 무소속 중 가장 나은 검투사</summary>
        static Gladiator BestFor(GameState s, Ludus l, double rep, double minCa, double weeklyWages)
        {
            Gladiator best = null;
            double bestScore = double.MinValue;
            foreach (var id in s.freeAgents)
            {
                if (!s.gladiators.TryGetValue(id, out var g) || !g.alive || g.status == "slave" || Busy(s, id)) continue;
                if (GladiatorRules.AgeStage(g) == "youth" || Transfers.MovedThisWindow(s, g)) continue;
                double ca = GladiatorRules.ComputeCA(g);
                if (ca < minCa) continue;
                var d = DemandOf(g, l, rep);
                // 여유: 새 주급까지 합친 주급으로 반 시즌(12주)을 버틸 금고, 그리고 주급 예산 안 (감당 못 하면 영입을 포기한다)
                if (d.refuse || (weeklyWages + d.wage) * 12 > l.treasury || !Economy.Affords(s, l, d.wage)) continue;
                double score = ca + (g.nation == l.nation ? 5 : 0);
                if (score > bestScore) { bestScore = score; best = g; }
            }
            return best;
        }

        static Demand DemandOf(Gladiator g, Ludus l, double rep) =>
            Reputation.DemandFor(Reputation.GladiatorStanding(g), Economy.FreeAgentBaseWage(g, l.tier), rep);

        static void Sign(GameState s, Ludus l, Gladiator g, double rep)
        {
            var d = DemandOf(g, l, rep);
            s.freeAgents.Remove(g.id);
            s.suitors?.Remove(g.id);
            l.gladiators.Add(g.id);
            g.wage = d.wage;
            g.contract = new GladiatorContract { untilSeason = s.season + Contracts.PreferredSeasons(g) - 1, clauses = new List<string>() };
            if (FmRules.Enabled) Career.Joined(s, g, 0);
            Career.LogCareer(s, g, $"무소속에서 {l.name}에 입단했습니다", "join");
        }

        /// <summary>한 주에 AI 전체가 데려가는 최대 인원</summary>
        const int PerWeek = 3;
        /// <summary>무소속 명단이 이만큼 남으면 멈춘다 (우리가 고를 몫)</summary>
        const int Floor = 12;

        /// <summary>급료일마다 (Economy.WeeklyFreeAgents 뒤). 자리가 빈 루두스 먼저, 그다음 주전보다 나은 검투사를 노리는 루두스</summary>
        public static void Weekly(GameState s, Rng rng)
        {
            if (!Enabled) return;
            int signed = 0;
            var ai = rng.Shuffle(s.ludi.Values.Where(x => !x.isPlayer).ToList());
            var alive = ai.ToDictionary(l => l.id, l => l.gladiators.Where(id => s.gladiators.TryGetValue(id, out var g) && g.alive).Select(id => s.gladiators[id]).ToList());
            foreach (var l in ai.OrderBy(l => alive[l.id].Count))
            {
                if (signed >= PerWeek || s.freeAgents.Count <= Floor) break;
                int n = alive[l.id].Count;
                bool short_ = n < Roster;
                // 등록 자리가 꽉 찼으면 데려와도 못 쓴다
                if (!short_ && Registration.Enabled && Registration.Count(l) >= Registration.Max) continue;
                if (!rng.Chance(short_ ? 0.6 : 0.15)) continue;
                // 인원이 차 있으면 주전(능력 5위)보다 나은 검투사만
                var cas = alive[l.id].Select(g => GladiatorRules.ComputeCA(g)).OrderByDescending(x => x).ToList();
                double minCa = short_ ? 0 : cas.Count >= 5 ? cas[4] + 2 : 0;
                double rep = Reputation.LudusReputation(s, l);
                var pick = BestFor(s, l, rep, minCa, alive[l.id].Sum(g => g.wage));
                if (pick == null) continue;
                Sign(s, l, pick, rep);
                signed++;
            }
        }
    }
}
