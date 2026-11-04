// 검투사 불만과 주급 인상 요구 (Unity판 전용, FM처럼 — 단 과하지 않게). 우리 검투사도 남의 검투사도 같은 상황에서만 생긴다:
// · transfer: 소속 루두스보다 이름난 루두스의 영입 제안이 거절됐다 (6주) → 이적 명단에 올리거나, 다른 루두스로 옮기거나, 시간이 지나야 풀린다
// · raise: 받을 만한 주급보다 한참 적게 받아 인상을 요구했는데 거절·무시됐다 (4주) → 재계약하면 풀린다
// · bench: 리그 기간에 오랫동안 경기에 못 나갔다 (4주) → 경기에 나가거나 시간이 지나야 풀린다
// 불만인 검투사는 사기가 조금씩 떨어지고 몸값이 싸진다 (Valuation.MarketValue). 루두스를 옮기면 모두 풀린다.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Colosseum.Core
{
    public static class Grievances
    {
        public static bool Enabled => Awards.Enabled;

        /// <summary>이 정도로 덜 받으면 인상을 바란다 (받을 만한 주급 / 지금 주급)</summary>
        const double UnderpaidRatio = 1.35;
        const int RaiseDays = 14;
        /// <summary>이만큼 경기에 못 나가면 출전 불만 (리그 기간)</summary>
        const int BenchDays = 35;

        static int Now(GameState s) => Calendar.AbsDay(s.season, s.day);

        /// <summary>불만이 어떻게 풀리는지 (화면·서판용)</summary>
        public static string Remedy(string kind) => kind switch
        {
            "raise" => "재계약으로 주급을 올려 주면 풀립니다",
            "transfer" => "이적 명단에 올려 길을 열어 주거나, 시간이 지나야 풀립니다",
            "bench" => "경기에 내보내면 풀립니다. 아니면 시간이 지나야 풀립니다",
            _ => "시간이 지나면 풀립니다",
        };

        /// <summary>불만을 남긴다 (이미 있으면 더 늦게 끝나는 쪽으로)</summary>
        static void Set(GameState s, Gladiator g, string why, int days, string kind)
        {
            g.grievance = why;
            g.grievanceKind = kind;
            g.grievanceLudus = Career.LudusOf(s, g.id)?.id;
            g.grievanceAt = Now(s);
            g.grievanceUntil = Math.Max(g.grievanceUntil ?? 0, Now(s) + days);
        }

        static void Clear(Gladiator g)
        {
            g.grievance = null;
            g.grievanceKind = null;
            g.grievanceLudus = null;
            g.grievanceAt = null;
            g.grievanceUntil = null;
        }

        /// <summary>불만이 생길 때 사기를 깎고 (우리 검투사면) 서판에 알린다</summary>
        static void Upset(GameState s, Gladiator g, string why, int days, string kind, double hit, string title, string body)
        {
            g.morale = Math.Max(0, g.morale - hit);
            Set(s, g, why, days, kind);
            var home = Career.LudusOf(s, g.id);
            if (home?.isPlayer != true) return;
            Career.LogCareer(s, g, why, "other");
            StateUtil.PushMessage(s, "warning", title, body + $" 사기 -{hit}.\n{Remedy(kind)}.");
        }

        /// <summary>불만이 풀렸으면 그 까닭 (아니면 null): 시간, 루두스를 옮김, 경기에 나감, 이적 명단</summary>
        static string Resolved(GameState s, Gladiator g, int now)
        {
            if (now > (g.grievanceUntil ?? 0)) return "time";
            if (g.grievanceLudus != null && Career.LudusOf(s, g.id)?.id != g.grievanceLudus) return "moved";
            if (g.grievanceKind == "bench" && g.lastFightAt > g.grievanceAt) return "played";
            if (g.grievanceKind == "transfer" && Transfers.Listed(s, g.id)) return "listed";
            return null;
        }

        /// <summary>영입 제안이 소속 루두스에 거절됐을 때: 제안한 쪽이 더 이름날수록, 야망이 클수록 실망한다.
        /// 우리가 남의 제안을 거절했을 때(Transfers.RejectOffer)와 남이 우리 제안을 거절했을 때(Negotiations) 모두</summary>
        public static void OnOfferRejected(GameState s, Gladiator g, Ludus buyer)
        {
            if (!Enabled || g == null || buyer == null || !g.alive) return;
            var owner = Career.LudusOf(s, g.id);
            if (owner == null || owner == buyer) return;
            double gap = Reputation.LudusReputation(s, buyer) - Reputation.LudusReputation(s, owner);
            if (gap < 8) return;
            double amb = g.hidden.ambition / 20.0, loyal = g.hidden.loyalty / 20.0;
            double hit = Math.Round(Math.Min(12, (4 + (gap - 8) / 3) * (0.6 + amb * 0.6 - loyal * 0.3)));
            if (hit < 2) return;
            Upset(s, g, $"{buyer.name} 이적이 막혔다", 42, "transfer", hit, $"{g.name}, 이적 거절에 실망",
                $"{Josa.J(g.name, "은/는")} 더 이름난 {Josa.J(buyer.name, "으로/로")} 갈 기회를 놓쳐 실망했습니다. 한동안 사기가 조금씩 떨어집니다.");
            // 우리 제안이 막힌 남의 검투사: 소문으로 알려 준다 (몸값이 내려간다)
            if (buyer.isPlayer)
                StateUtil.PushMessage(s, "info", $"소문: {g.name}, 이적이 막혀 불만",
                    $"{Josa.J(owner.name, "이/가")} 우리 제안을 거절하자 {Josa.J(g.name, "이/가")} 불만을 품었다고 합니다. 불만인 검투사는 몸값이 내려갑니다.");
        }

        /// <summary>매일: 불만 끝남, 우리 검투사의 사기 하락과 기한 지난 인상 요구. 급료일마다 출전 불만과 인상 요구</summary>
        public static void Daily(GameState s, Rng rng)
        {
            if (!Enabled) return;
            int now = Now(s);
            var me = s.ludi[s.playerLudusId];
            // 불만이 풀렸나 (원인별). 우리 검투사가 경기·이적 명단으로 풀리면 알려 준다
            foreach (var x in s.gladiators.Values)
            {
                if (x.grievance == null || Resolved(s, x, now) is not string why) continue;
                bool mine = x.grievanceLudus == me.id && me.gladiators.Contains(x.id);
                string was = x.grievance;
                Clear(x);
                if (mine && why is "played" or "listed")
                    StateUtil.PushMessage(s, "info", $"{x.name}, 불만이 풀렸습니다",
                        why == "played" ? $"경기에 나서자 '{was}' 불만이 풀렸습니다." : $"이적 명단에 올려 길을 열어 주자 '{was}' 불만이 풀렸습니다.");
            }
            foreach (var id in me.gladiators.ToList())
            {
                if (!s.gladiators.TryGetValue(id, out var g) || !g.alive) continue;
                if (g.grievance != null && g.morale > 30) g.morale = Math.Max(30, g.morale - 0.2);
                if (g.raiseBy is double by && now > by)
                {
                    g.raiseBy = null;
                    Upset(s, g, "주급 인상 요구가 무시됐다", 28, "raise", 8, $"{g.name}, 주급 문제로 실망",
                        $"{Josa.J(g.name, "은/는")} 주급 인상 요구에 답을 듣지 못했다며 실망했습니다.");
                }
            }
            if (!Calendar.IsPayday(s.day)) return;

            // 루두스 명성은 급료일에 한 번만 센다
            var rep = s.ludi.Values.ToDictionary(x => x.id, x => Reputation.LudusReputation(s, x));
            bool benchDay = ChallengeLogic.IsLeagueDay(s.day) && s.day >= 50;
            foreach (var l in s.ludi.Values)
            {
                // 출전을 기대하는 주전급: 루두스 안 능력 상위 다섯 (출전 불만을 따지는 날만 센다)
                var starters = !benchDay ? new HashSet<string>() : l.gladiators.Where(x => s.gladiators.TryGetValue(x, out var y) && y.alive)
                    .OrderByDescending(x => GladiatorRules.ComputeCA(s.gladiators[x])).Take(5).ToHashSet();
                foreach (var id in l.gladiators.ToList())
                {
                    if (!s.gladiators.TryGetValue(id, out var g) || !g.alive || g.status == "slave" || g.grievance != null || g.raiseBy != null) continue;
                    if (GladiatorRules.AgeStage(g) == "youth" || g.deployment != null) continue;

                    // AI끼리 영입 시도 (FM처럼 큰 루두스가 작은 루두스 스타를 노린다): 소속보다 격이 높은 검투사에게
                    // 같은 나라의 더 이름난 루두스가 제안했다가 거절당한다. 우리 루두스 일은 실제 제안으로 따로 오므로 빼고
                    if (!l.isPlayer && rng.Chance(0.025) && Reputation.GladiatorStanding(g) > rep[l.id] + 8)
                    {
                        var buyer = s.ludi.Values.Where(x => !x.isPlayer && x.nation == l.nation && rep[x.id] >= rep[l.id] + 8)
                            .OrderBy(x => Math.Abs(rep[x.id] - Reputation.GladiatorStanding(g))).FirstOrDefault();
                        if (buyer != null) { OnOfferRejected(s, g, buyer); if (g.grievance != null) continue; }
                    }

                    // 출전 불만: 리그 중반부터, 다치지 않았는데 오래 못 나간 주전급
                    if (benchDay && g.injury == null && starters.Contains(id)
                        && now - (g.lastFightAt ?? Calendar.AbsDay(s.season, 1)) >= BenchDays
                        && rng.Chance(0.3 * (0.5 + g.hidden.ambition / 20.0)))
                    {
                        Upset(s, g, "출전 기회가 없다", 28, "bench", 5, $"{g.name}, 출전 기회 불만",
                            $"{Josa.J(g.name, "이/가")} {BenchDays}일 넘게 경기에 나가지 못했다며 불만을 품었습니다.");
                        continue;
                    }

                    // 인상 요구: 리그가 시작된 뒤 시즌에 한 번까지, 한참 덜 받는 검투사
                    if (s.day < 21 || g.contract == null || g.raiseSeason == s.season || g.wage <= 0) continue;
                    double fair = Valuation.FairWage(s, g);
                    if (fair / g.wage < UnderpaidRatio || !rng.Chance(0.2 * (0.5 + g.hidden.greed / 20.0))) continue;
                    g.raiseSeason = s.season;
                    double want = Math.Round(fair * 0.9);
                    if (l.isPlayer)
                    {
                        g.raiseBy = now + RaiseDays;
                        StateUtil.PushMessage(s, "warning", $"{g.name}, 주급 인상 요구",
                            $"{Josa.J(g.name, "이/가")} 지금 주급 {g.wage} D로는 부족하다며 {want} D 안팎을 바랍니다.\n" +
                            $"{RaiseDays}일 안에 검투사 상세 화면의 '재계약'으로 협상하면 됩니다. 답하지 않으면 실망합니다.");
                        continue;
                    }
                    // 남의 루두스: 금고가 넉넉하고 아끼는 검투사면 올려 주고, 아니면 거절해 불만이 생긴다. 주급 예산을 넘기는 인상은 거의 안 해 준다
                    double keep = !Economy.Affords(s, l, want - g.wage) ? 0.05
                        : 0.35 + (l.treasury > want * 60 ? 0.3 : 0) + (l.aiPersonality == "developer" ? 0.1 : l.aiPersonality == "merchant" ? -0.1 : 0);
                    if (rng.Chance(keep)) g.wage = want;
                    else Upset(s, g, "주급 인상 요구가 거절됐다", 28, "raise", 6, "", "");
                }
            }
        }

        /// <summary>새 계약(재계약)을 맺으면 주급 불만과 인상 요구만 풀린다. 이적·출전 불만은 그대로</summary>
        public static void OnNewContract(Gladiator g)
        {
            g.raiseBy = null;
            if (g.grievanceKind == "raise") Clear(g);
        }

        /// <summary>화면용 한 줄 (없으면 null)</summary>
        public static string Line(GameState s, Gladiator g)
        {
            if (g.raiseBy is double by) return $"주급 인상을 바랍니다 ({Math.Max(0, by - Now(s))}일 안에 재계약)";
            if (g.grievance == null) return null;
            string hint = g.grievanceKind switch { "raise" => "재계약하면 풀림", "transfer" => "이적 명단에 올리거나 시간이 지나면 풀림", "bench" => "경기에 내보내면 풀림", _ => null };
            return $"불만: {g.grievance}" + (hint != null ? $" ({hint})" : "");
        }
    }
}
