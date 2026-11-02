// 경기 결과 반영과 루두스끼리의 정산 (src/core/match.ts)
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace Colosseum.Core
{
    public class ApplyOptions
    {
        public string rule;
        /// <summary>공식 경기: RP·상금·관중 수입이 오간다</summary>
        public bool ranked;
        /// <summary>홈 루두스 (관중 수입을 더 가져간다)</summary>
        public string homeLudusId;
    }

    public class MatchSummary
    {
        public Dictionary<string, double> rpChange = new();
        public Dictionary<string, double> money = new();
        public Dictionary<string, double> fame = new();
    }

    public class SettleOptions
    {
        public string homeLudusId;
        /// <summary>경기의 관중 열기 (0~100)</summary>
        public double crowd;
        /// <summary>두 쪽 검투사 인기 합</summary>
        public double popularity;
        /// <summary>단체전: 판이 클수록 RP와 돈이 커진다</summary>
        public double? rpMult;
        public double? moneyMult;
        /// <summary>컵처럼 리그 경기 수에 안 들어가는 경기</summary>
        public bool noLeague;
    }

    public static class MatchLogic
    {
        static string N(double x) => x.ToString(CultureInfo.InvariantCulture);

        /// <summary>rng.int(0, 2 ** 31 - 1). Rng.Int(0, int.MaxValue)는 max - min + 1이 int 범위를 넘어 쓸 수 없다</summary>
        public static uint Seed31(Rng rng) => (uint)Math.Floor(rng.Next() * 2147483648.0);

        static readonly JsonSerializerSettings CloneSettings = new() { ObjectCreationHandling = ObjectCreationHandling.Replace };

        /// <summary>structuredClone(g)</summary>
        public static Gladiator CloneGladiator(Gladiator g) =>
            g == null ? null : JsonConvert.DeserializeObject<Gladiator>(JsonConvert.SerializeObject(g), CloneSettings);

        public static double EloDelta(double myRp, double oppRp, bool won, double k)
        {
            double expected = 1 / (1 + Math.Pow(10, (oppRp - myRp) / 400));
            return JsMath.Round(k * ((won ? 1 : 0) - expected));
        }

        /// <summary>경기 결과를 게임 상태에 반영한다</summary>
        public static MatchSummary ApplyMatchResult(GameState state, MatchResult r, ApplyOptions opt, Rng rng)
        {
            var rule = Bal.Rule(opt.rule);
            var nr = NationRules.RulesOfNation(Career.LudusOf(state, r.winnerId)?.nation ?? state.nation);
            var w = state.gladiators[r.winnerId];
            var l = state.gladiators[r.loserId];
            var wl = Career.LudusOf(state, w.id);
            var ll = Career.LudusOf(state, l.id);
            int at = (int)Calendar.AbsDay(state.season, state.day);
            var summary = new MatchSummary();
            string ruleName = (string)rule["name"];
            double fameMult = (double)rule["fameMult"];

            w.record.wins++;
            l.record.losses++;
            w.morale = Math.Min(100, w.morale + 5);
            l.morale = Math.Max(0, l.morale - 5);

            // 명성(인기): 관중이 있어야 쌓인다. 사투일수록 크다
            // 주술 검투사를 떠받드는 나라도 있다
            double MagicMult(Gladiator g)
            {
                foreach (var id in g.skills.active)
                    if (Data.Skill(id)?.magic == true) return nr.magicFameMult;
                return 1;
            }
            double wFame = JsMath.Round((2 + Math.Max(0, r.crowdContribution[w.id]) / 10) * fameMult * nr.fameMult * MagicMult(w));
            double lFame = JsMath.Round((r.crowdContribution[l.id] / 15) * fameMult * nr.fameMult * MagicMult(l));
            w.popularity = Math.Max(0, Math.Min(100, w.popularity + wFame));
            l.popularity = Math.Max(0, Math.Min(100, l.popularity + lFame));
            summary.fame[w.id] = wFame;
            summary.fame[l.id] = lFame;

            foreach (var g in new[] { w, l })
            {
                Condition.AfterMatch(g, at, opt.rule);
                // 실전 경험: 훈련전·미시오·사투 모두 성장한다 (진 쪽도 배운다)
                var home = Career.LudusOf(state, g.id);
                Training.Grow(g, rng, "match",
                    (double)rule["growthDays"] * (g == w ? 1 : 0.8) * nr.growthMult * StaffLogic.CoachMult(StaffLogic.RatingOf(state, home, "doctore")),
                    1, home?.facilities.training ?? 1);
                var rec = Career.SeasonRecord(state, g);
                rec.apps++;
                if (g == w) rec.wins++;
                else rec.losses++;
                if (g.injury != null) Injuries.Aggravate(state, g, rng);
                r.injuries.TryGetValue(g.id, out var inj);
                if (inj != null && (g.injury == null || g.injury.daysLeft < inj.daysLeft))
                {
                    g.injury = inj;
                    rec.injuries++;
                    Career.LogCareer(state, g, $"{Josa.J(OpponentName(state, r, g), "과/와")}의 경기에서 {inj.desc} ({N(inj.daysLeft)}일)", "injury");
                }
            }
            Milestones(state, w);

            if (r.died != null)
            {
                var dead = state.gladiators[r.died];
                dead.alive = false;
                dead.injury = null;
                dead.diedAt = new DiedAt { season = state.season, day = state.day, cause = $"{w.name}에게 패배" };
                w.record.kills++;
                Career.SeasonRecord(state, w).kills++;
                Career.LogCareer(state, w, $"{ruleName}에서 {dead.name}의 목숨을 거뒀다", "milestone");
                Career.LogCareer(state, dead, $"{ruleName}에서 {w.name}에게 패해 모래 위에서 죽었다", "death");
                // 사투에서 우리 검투사가 적을 죽이면 그 장비를 거둔다
                if (opt.rule == "sudden" && wl?.isPlayer == true && !(ll?.isPlayer == true)) GearLogic.LootFallen(state, w, dead);
                var home = Career.LudusOf(state, dead.id);
                if (home?.isPlayer == true) state.fallen.Add(dead.id);
                if (home != null)
                {
                    foreach (var gid in home.gladiators)
                    {
                        var g = state.gladiators[gid];
                        if (g.alive && g.id != dead.id) g.morale = Math.Max(0, g.morale - (double)Bal.Morale["teammateDeath"]);
                    }
                    home.gladiators = home.gladiators.FindAll(x => x != dead.id);
                }
            }

            if (wl != null && ll != null)
            {
                wl.record.wins++;
                ll.record.losses++;
                if (opt.ranked)
                {
                    Settle(state, wl, ll, opt.rule, new SettleOptions { homeLudusId = opt.homeLudusId, crowd = r.crowd, popularity = w.popularity + l.popularity }, summary);
                    Career.SeasonRecord(state, w).earnings += summary.money.TryGetValue(wl.id, out var mw) ? mw : 0;
                    Career.SeasonRecord(state, l).earnings += summary.money.TryGetValue(ll.id, out var ml) ? ml : 0;
                    // 항복을 수치로 여기는 나라: 항복한 쪽은 인기와 RP를 더 잃는다
                    if (nr.surrenderShame && r.reason == "surrender")
                    {
                        l.popularity = Math.Max(0, l.popularity - 10);
                        ll.rp -= 5;
                        summary.rpChange[ll.id] -= 5;
                        Career.LogCareer(state, l, "항복했다. 사람들이 등을 돌린다", "other");
                    }
                }
            }
            return summary;
        }

        /// <summary>루두스끼리 RP·상금·관중 수입을 정산한다 (1:1, 단체전, 컵 모두)</summary>
        public static void Settle(GameState state, Ludus wl, Ludus ll, string ruleId, SettleOptions o, MatchSummary summary)
        {
            var rule = Bal.Rule(ruleId);
            var nr = NationRules.RulesOfNation(ll.nation);
            if (!o.noLeague)
            {
                wl.matchesThisSeason++;
                ll.matchesThisSeason++;
                double k = (double)Data.Balance["league"]["eloK"] * (double)rule["rpMult"] * (o.rpMult ?? 1) * (wl.tier == 2 && ll.tier == 1 ? 1.25 : 1);
                double dw = EloDelta(wl.rp, ll.rp, true, k);
                double dl = EloDelta(ll.rp, wl.rp, false, k);
                wl.rp += dw;
                ll.rp += dl;
                summary.rpChange[wl.id] = dw;
                summary.rpChange[ll.id] = dl;
            }
            // 상금과 관중 수입. 훈련전은 관중이 없어 둘 다 없다
            var E = Bal.Economy;
            string tier = Math.Min(wl.tier, ll.tier).ToString(CultureInfo.InvariantCulture);
            double mult = o.moneyMult ?? 1;
            double purse = JsMath.Round((double)E["purse"][tier] * (double)rule["purseMult"] * nr.purseMult * (ruleId == "sudden" ? nr.suddenPurseMult : 1) * mult);
            double crowdFactor = 0.5 + o.popularity / 200 + o.crowd / 200;
            double baseTickets = JsMath.Round((double)E["ticketIncome"][tier] * (double)rule["ticketMult"] * crowdFactor * Modifiers.ModifierMult(state, "ticket") * nr.ticketMult * mult);
            string homeId = o.homeLudusId ?? ll.id;
            // 홈 경기장이 클수록 관중 수입이 늘어난다
            int arena = state.ludi.TryGetValue(homeId, out var homeLudus) ? homeLudus.facilities.arena : 1;
            double tickets = JsMath.Round(baseTickets * FacilitiesLogic.ArenaTicketMult(arena));
            double homeShare = (double)E["homeShare"];
            double loserShare = (double)E["loserPurseShare"];
            string ruleName = (string)rule["name"];
            foreach (var lu in new[] { wl, ll })
            {
                bool won = lu == wl;
                double share = (won ? purse : JsMath.Round(purse * loserShare)) + JsMath.Round(tickets * (lu.id == homeId ? homeShare : 1 - homeShare));
                if (share > 0) Economy.AddMoney(state, lu, share, $"{ruleName} {(won ? "승리" : "패배")} 상금·관중 수입");
                summary.money[lu.id] = (summary.money.TryGetValue(lu.id, out var prev) ? prev : 0) + share;
            }
        }

        static string OpponentName(GameState state, MatchResult r, Gladiator g) =>
            state.gladiators[g.id == r.winnerId ? r.loserId : r.winnerId].name;

        static void Milestones(GameState state, Gladiator w)
        {
            int wins = w.record.wins;
            if (wins == 1) Career.LogCareer(state, w, "첫 승리를 거뒀다", "milestone");
            else if (wins % 10 == 0) Career.LogCareer(state, w, $"통산 {wins}승", "milestone");
        }
    }
}
