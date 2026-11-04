// 계약 조항 (Unity판 전용, FM 계약 협상처럼): 주급·기간 말고도 계약금, 승리 수당, 킬 수당, 승격 보너스,
// 승격 시 자동 연장·주급 인상, 강등 시 주급 삭감, 최소 이적료 조항을 걸 수 있다.
// 검투사는 조항 하나하나를 '주급으로 치면 얼마'로 따져(성격·나이·우리 루두스 형편에 따라) 원하는 주급과 견준다.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Colosseum.Core
{
    /// <summary>계약 제안 하나 (협상 창이 채운다)</summary>
    public class ContractTerms
    {
        public double wage;
        public int seasons = 2;
        /// <summary>서명할 때 한 번 주는 돈</summary>
        public double signingFee;
        /// <summary>공식 경기 승리마다</summary>
        public double winBonus;
        /// <summary>상대를 죽일 때마다</summary>
        public double killBonus;
        /// <summary>우리 루두스가 승격하면 한 번</summary>
        public double promoBonus;
        /// <summary>승격하면 계약 1시즌 자동 연장</summary>
        public bool promoExtend;
        /// <summary>승격하면 주급 인상 (%)</summary>
        public double promoRaise;
        /// <summary>강등되면 주급 삭감 (%)</summary>
        public double relegCut;
        /// <summary>최소 이적료 조항 (0 = 없음). 이 값 이상 제안이 오면 막을 수 없다</summary>
        public double releaseClause;

        public ContractTerms Clone() => (ContractTerms)MemberwiseClone();
    }

    /// <summary>제안에 대한 검투사의 생각</summary>
    public class ContractView
    {
        /// <summary>조항까지 주급으로 환산한 값</summary>
        public double value;
        /// <summary>바라는 값 (주급 환산)</summary>
        public double want;
        public bool refuse;
        public string reason;
        /// <summary>제안 하나하나가 몇 D/주로 보이나 (협상 창 설명용)</summary>
        public List<(string label, double perWeek)> parts = new();
        public double Ratio => want > 0 ? value / want : 0;
        /// <summary>검투사 격 − 우리 명성 (클수록 '작은' 루두스로 오는 셈이라 보장 조항을 바란다)</summary>
        public double gap;
    }

    public static class Contracts
    {
        public const int WeeksPerSeason = 24;

        public static readonly int[] SeasonChoices = { 1, 2, 3, 4, 5 };

        /// <summary>계약에 든 조항 (저장용: GladiatorContract의 새 칸)</summary>
        public static ContractTerms TermsOf(Gladiator g) => g.contract == null ? null : new ContractTerms
        {
            wage = g.wage, seasons = 1, winBonus = g.contract.winBonus ?? 0, killBonus = g.contract.killBonus ?? 0,
            promoBonus = g.contract.promoBonus ?? 0, promoExtend = g.contract.promoExtend ?? false, promoRaise = g.contract.promoRaise ?? 0,
            relegCut = g.contract.relegCut ?? 0, releaseClause = g.contract.releaseClause ?? 0,
        };

        /// <summary>계약서를 쓴다 (서명 순간). seasons만큼 이번 시즌부터</summary>
        public static void Apply(GameState s, Gladiator g, ContractTerms t)
        {
            g.wage = t.wage;
            Grievances.OnNewContract(g);
            g.contract = new GladiatorContract
            {
                untilSeason = s.season + t.seasons - 1, clauses = new List<string>(),
                winBonus = t.winBonus > 0 ? t.winBonus : null, killBonus = t.killBonus > 0 ? t.killBonus : null,
                promoBonus = t.promoBonus > 0 ? t.promoBonus : null, promoExtend = t.promoExtend ? true : null,
                promoRaise = t.promoRaise > 0 ? t.promoRaise : null, relegCut = t.relegCut > 0 ? t.relegCut : null,
                releaseClause = t.releaseClause > 0 ? t.releaseClause : null,
            };
        }

        /// <summary>계약 조항 한 줄 요약 ("승리 수당 30 D · 승격 보너스 500 D")</summary>
        public static string ClauseLine(GladiatorContract c)
        {
            if (c == null) return "";
            var l = new List<string>();
            if (c.winBonus > 0) l.Add($"승리 수당 {c.winBonus:N0} D");
            if (c.killBonus > 0) l.Add($"킬 수당 {c.killBonus:N0} D");
            if (c.promoBonus > 0) l.Add($"승격 보너스 {c.promoBonus:N0} D");
            if (c.promoExtend == true) l.Add("승격 시 1시즌 연장");
            if (c.promoRaise > 0) l.Add($"승격 시 주급 +{c.promoRaise:0}%");
            if (c.relegCut > 0) l.Add($"강등 시 주급 -{c.relegCut:0}%");
            if (c.releaseClause > 0) l.Add($"최소 이적료 {c.releaseClause:N0} D");
            return string.Join(" · ", l);
        }

        // ── 검투사의 셈법 ──

        /// <summary>한 주에 치르는 공식 경기 수 (대략)</summary>
        const double FightsPerWeek = 0.45;

        /// <summary>이길 확률 (전적, 적으면 반반으로 당긴다)</summary>
        public static double WinRate(Gladiator g) => (g.record.wins + 3.0) / (g.record.wins + g.record.losses + 6.0);

        static double KillRate(Gladiator g) => (g.record.kills + 0.5) / (g.record.wins + g.record.losses + 6.0);

        /// <summary>우리 루두스가 이번 시즌 승격·강등할 확률 (지금 순위로 대충)</summary>
        public static (double up, double down) TierOdds(GameState s, Ludus l)
        {
            var table = Economy.TierRanking(s, l.tier, l.nation);
            int rank = table.FindIndex(x => x.id == l.id) + 1;
            if (rank <= 0) return (0, 0);
            int n = table.Count;
            double up = l.tier <= 1 ? 0 : rank <= 2 ? 0.55 : rank <= 4 ? 0.25 : rank <= 6 ? 0.08 : 0.03;
            double down = l.tier >= FmRules.MaxTier ? 0 : rank > n - 2 ? 0.5 : rank > n - 4 ? 0.2 : 0.05;
            return (up, down);
        }

        /// <summary>몇 시즌 계약을 바라나: 어리고 야망 크면 짧게(더 큰 데로 갈 문을 열어 두려고), 나이 들면 길게</summary>
        public static int PreferredSeasons(Gladiator g)
        {
            string st = GladiatorRules.AgeStage(g);
            int b = st == "growth" ? 2 : st == "prime" ? 3 : 4;
            if (g.hidden.ambition >= 15) b--;
            if (g.hidden.loyalty >= 15) b++;
            return Math.Max(1, Math.Min(5, b));
        }

        /// <summary>제안을 검투사 눈으로 본다. want는 FM식 몸값 모델의 바라는 주급 + 우리 명성 차이</summary>
        public static ContractView View(GameState s, Gladiator g, ContractTerms t, Demand demand, Ludus to)
        {
            var v = new ContractView { want = demand.wage, refuse = demand.refuse, reason = demand.reason, gap = demand.gap };
            double amb = g.hidden.ambition / 20.0, greed = g.hidden.greed / 20.0;
            double wr = WinRate(g), kr = KillRate(g);
            var (up, down) = TierOdds(s, to);
            int weeks = WeeksPerSeason * Math.Max(1, t.seasons);
            void Part(string label, double perWeek) { if (Math.Abs(perWeek) >= 0.5) v.parts.Add((label, perWeek)); v.value += perWeek; }

            Part("주급", t.wage);
            // 계약금: 욕심 많을수록 크게 본다. 몇 년에 걸쳐 나눠 생각한다
            Part("계약금", t.signingFee / weeks * (0.9 + greed * 0.5));
            // 수당: 자신 있는(많이 이긴) 검투사일수록, 야망 클수록 반긴다
            Part("승리 수당", t.winBonus * FightsPerWeek * wr * (0.8 + amb * 0.4));
            Part("킬 수당", t.killBonus * FightsPerWeek * kr * (0.7 + amb * 0.3 + (g.traits.Contains("bloodthirsty") ? 0.5 : 0)));
            // 승격 조항: 올라갈 듯한 루두스일 때만 값어치가 있다
            Part("승격 보너스", t.promoBonus * up / WeeksPerSeason * (0.8 + amb * 0.5));
            if (t.promoExtend) Part("승격 시 연장", t.wage * 0.06 * up * (1 - amb * 0.6) + t.wage * 0.02);
            Part("승격 시 인상", t.wage * t.promoRaise / 100 * up * 0.8);
            Part("강등 시 삭감", -t.wage * t.relegCut / 100 * (down + 0.05) * 1.2);
            // 최소 이적료 조항: 야망 큰 검투사가 반긴다. 몸값에 가까울수록 더 반긴다
            if (t.releaseClause > 0)
            {
                double value = Valuation.MarketValue(s, g);
                double closeness = Math.Max(0, 1 - t.releaseClause / Math.Max(1, value * 3));
                Part("최소 이적료 조항", t.wage * 0.15 * closeness * (0.3 + amb));
            }
            // 기간: 바라는 기간에서 멀수록 싫다
            int pref = PreferredSeasons(g);
            Part("계약 기간", -t.wage * 0.04 * Math.Abs(t.seasons - pref));
            // FM식: 자기보다 이름 없는 루두스로 갈 때는 주급만으로는 아쉽다 — 계약금이나 최소 이적료 같은 보장을 바란다 (최대 −12%)
            if (FmRules.Enabled && demand.gap > 12 && t.signingFee <= 0 && t.releaseClause <= 0)
                Part("보장 조항 없음", -t.wage * Math.Min(0.12, (demand.gap - 12) / 150));
            v.value = Math.Round(v.value);
            return v;
        }

        /// <summary>만족도 글 (협상 창). 정확한 수치는 숨긴다</summary>
        public static string Mood(ContractView v) =>
            v.refuse ? "협상할 생각이 없습니다" :
            v.Ratio >= 1.02 ? "아주 만족스러워합니다" :
            v.Ratio >= 0.95 ? "긍정적으로 보입니다" :
            v.Ratio >= 0.85 ? "조금 더 바라는 눈치입니다" :
            v.Ratio >= 0.7 ? "많이 모자라다고 봅니다" : "모욕적이라고 여깁니다";

        /// <summary>검투사의 역제안: 다른 조항은 그대로 두고 주급만 올린 안. 아쉬운 조항을 짚는 한마디도</summary>
        public static (ContractTerms terms, string note) Counter(GameState s, Gladiator g, ContractTerms t, ContractView v, double target)
        {
            var c = t.Clone();
            c.wage = Math.Max(t.wage, Math.Round(t.wage + (target - v.value)));
            string note = "";
            var (up, _) = TierOdds(s, Career.LudusOf(s, g.id) ?? s.ludi[s.playerLudusId]);
            if (FmRules.Enabled && v.gap > 12 && t.signingFee <= 0 && t.releaseClause <= 0) note = "이름이 덜한 루두스로 오는 만큼 계약금이나 최소 이적료 조항 같은 보장을 원합니다.";
            else if (g.hidden.ambition >= 14 && t.releaseClause <= 0) note = "최소 이적료 조항을 넣어 주면 주급은 조금 양보할 수 있다는 눈치입니다.";
            else if (g.hidden.greed >= 14 && t.signingFee <= 0) note = "계약금을 두둑이 주면 주급은 조금 양보할 수 있다는 눈치입니다.";
            else if (up > 0.2 && t.promoBonus <= 0) note = "승격하면 보너스를 받고 싶다고 합니다.";
            else if (t.relegCut > 0) note = "강등 시 주급 삭감 조항이 마음에 들지 않는다고 합니다.";
            else if (Math.Abs(t.seasons - PreferredSeasons(g)) >= 2) note = $"{PreferredSeasons(g)}시즌 계약을 바랍니다.";
            return (c, note);
        }

        /// <summary>제안의 총비용 (협상 창: 이번 계약으로 금고에서 나갈 돈 대략)</summary>
        public static double UpfrontCost(ContractTerms t) => t.signingFee;

        // ── 수당 지급 ──

        static void Pay(GameState s, Gladiator g, double amount, string what)
        {
            if (amount <= 0) return;
            var l = Career.LudusOf(s, g.id);
            if (l == null || !l.isPlayer) return;
            Economy.AddMoney(s, l, -amount, $"{g.name} {what}");
        }

        /// <summary>공식 경기 승리 (훈련전 빼고)</summary>
        public static void OnWin(GameState s, Gladiator g, string rule)
        {
            if (!FmRules.Enabled || rule == "training" || g.contract?.winBonus is not double b) return;
            Pay(s, g, b, "승리 수당");
        }

        public static void OnKill(GameState s, Gladiator g)
        {
            if (!FmRules.Enabled || g.contract?.killBonus is not double b) return;
            Pay(s, g, b, "킬 수당");
        }

        /// <summary>시즌 종료(승강 반영 뒤): 승격·강등 조항, 계약 만료 안내</summary>
        public static void SeasonEnd(GameState s, bool promoted, bool relegated)
        {
            var me = s.ludi[s.playerLudusId];
            var notes = new List<string>();
            foreach (var id in me.gladiators)
            {
                if (!s.gladiators.TryGetValue(id, out var g) || g.contract == null) continue;
                var c = g.contract;
                if (promoted)
                {
                    if (c.promoBonus > 0) { Pay(s, g, c.promoBonus.Value, "승격 보너스"); notes.Add($"{g.name}: 승격 보너스 {c.promoBonus:N0} D"); }
                    if (c.promoExtend == true) { c.untilSeason++; notes.Add($"{g.name}: 계약 1시즌 연장 (시즌 {c.untilSeason}까지)"); }
                    if (c.promoRaise > 0) { g.wage = JsMath.Round(g.wage * (1 + c.promoRaise.Value / 100)); notes.Add($"{g.name}: 주급 {c.promoRaise:0}% 인상 → {g.wage:N0} D"); }
                }
                if (relegated && c.relegCut > 0)
                {
                    g.wage = JsMath.Round(g.wage * (1 - c.relegCut.Value / 100));
                    notes.Add($"{g.name}: 주급 {c.relegCut:0}% 삭감 → {g.wage:N0} D");
                }
            }
            if (notes.Count > 0)
                StateUtil.PushMessage(s, "info", promoted ? "승격 조항이 발동했습니다" : "강등 조항이 발동했습니다", string.Join("\n", notes));
        }

        // ── 협상 판정 (답장이 올 때) ──

        public static string RenewKey(string gladiatorId) => "renew:" + gladiatorId;

        /// <summary>계약 제안 하나를 판정한다. 흥정(인내심·마지노선)은 '주급 환산 값'으로 따진다.
        /// 역제안이면 res.terms에 검투사가 바라는 조건(주급만 올린 안)이 든다</summary>
        public static HaggleResult Resolve(GameState s, Rng rng, string key, Gladiator g, ContractTerms t, Demand d, Ludus to)
        {
            var v = View(s, g, t, d, to);
            if (v.refuse) return new HaggleResult { status = "error", line = $"{g.name}: {d.reason}" };
            Haggling.Open(s, rng, key, v.want, 0.03, 0.12);
            var res = Haggling.Offer(s, key, v.value);
            if (res.status == "countered")
            {
                var (c, note) = Counter(s, g, t, v, res.counter);
                res.terms = c;
                var h = Haggling.Get(s, key);
                res.line = $"{(h != null ? Haggling.Mood(h) : "")} \"다른 건 그대로 두고 주급을 {c.wage:N0} D로 올리면 서명하지.\"{(note.Length > 0 ? " " + note : "")}";
            }
            else if (res.status == "accepted") res.line = "\"좋소, 서명하겠소.\"";
            return res;
        }

        /// <summary>재계약 제안을 보낸다 (답은 하루이틀 뒤 서판으로)</summary>
        public static HaggleResult SendRenew(GameState s, Rng rng, string gladiatorId, ContractTerms t)
        {
            var me = s.ludi[s.playerLudusId];
            if (!me.gladiators.Contains(gladiatorId) || !s.gladiators.TryGetValue(gladiatorId, out var g) || !g.alive)
                return new HaggleResult { status = "error", line = "우리 검투사가 아닙니다" };
            if (g.status == "slave" || g.contract == null) return new HaggleResult { status = "error", line = "노예는 계약 없이 루두스에 매여 있습니다" };
            string key = RenewKey(gladiatorId);
            if (Haggling.IsLocked(s, key)) return new HaggleResult { status = "locked", line = Haggling.LockedLine(s, key) };
            var d = RenewDemand(s, g);
            if (d.refuse) return new HaggleResult { status = "error", line = $"{g.name}: {d.reason}" };
            if (me.treasury < t.signingFee) return new HaggleResult { status = "error", line = "계약금을 낼 돈이 없습니다" };
            return Negotiations.Send(s, rng, "renew", gladiatorId, null, t.wage, t.seasons, t);
        }

        /// <summary>재계약 답장 (Negotiations.Daily)</summary>
        internal static HaggleResult ResolveRenew(GameState s, Rng rng, string gladiatorId, ContractTerms t)
        {
            var me = s.ludi[s.playerLudusId];
            if (!me.gladiators.Contains(gladiatorId)) return new HaggleResult { status = "error", line = "이미 우리 검투사가 아닙니다" };
            var g = s.gladiators[gladiatorId];
            if (me.treasury < t.signingFee) return new HaggleResult { status = "error", line = "계약금을 낼 돈이 없어 재계약하지 못했습니다" };
            var res = Resolve(s, rng, RenewKey(gladiatorId), g, t, RenewDemand(s, g), me);
            if (res.Accepted)
            {
                if (t.signingFee > 0) Economy.AddMoney(s, me, -t.signingFee, $"{g.name} 재계약금");
                int from = Math.Max(s.season, g.contract?.untilSeason ?? s.season);
                Apply(s, g, t);
                g.contract.untilSeason = from + t.seasons;
                g.morale = Math.Min(100, g.morale + 6);
                Career.LogCareer(s, g, $"주급 {t.wage:N0} D로 재계약했습니다 (시즌 {g.contract.untilSeason}까지)", "other");
            }
            return res;
        }

        /// <summary>재계약이 필요한 우리 검투사 (이번 시즌으로 계약이 끝나는 자유민)</summary>
        public static List<Gladiator> Expiring(GameState s)
        {
            var me = s.ludi[s.playerLudusId];
            return me.gladiators.Select(id => s.gladiators[id]).Where(g => g.alive && g.contract != null && g.contract.untilSeason <= s.season).ToList();
        }

        /// <summary>재계약 때 바라는 주급: 지금 몸값 모델의 주급과 지금 주급 중 높은 쪽, 사기·충성심으로 조금 깎는다</summary>
        public static Demand RenewDemand(GameState s, Gladiator g)
        {
            double fair = Valuation.FairWage(s, g);
            double baseWage = Math.Max(g.wage * 1.05, fair);
            var me = s.ludi[s.playerLudusId];
            double manage = StaffLogic.Roles.Max(r => StaffLogic.AttrOf(s, me, r, "manage"));
            double loyal = 1 - g.hidden.loyalty / 200.0 - Math.Max(0, g.morale - 60) / 400.0 - manage * 0.004;
            if (g.morale < 25) return new Demand { refuse = true, reason = "\"여기서 더 있을 생각은 없어.\"" };
            // 이미 우리 루두스에 있으니 명성 격차는 영입보다 약하게 본다 (격차 1점당 1%, 최대 +40%).
            // FM식은 영입(DemandFor)처럼 격차 12까지 웃돈이 없고 최대 +30%
            double gap = Reputation.GladiatorStanding(g) - Reputation.LudusReputation(s, me);
            double mult = FmRules.Enabled ? 1 + Math.Min(30, Math.Max(0, gap - 12)) / 100 : 1 + Math.Min(40, Math.Max(0, gap)) / 100;
            string reason = gap > 25 ? "\"이 정도 루두스에 남으려면 그만한 값은 받아야겠어.\"" : gap > 0 ? "\"조금만 더 쳐 주면 남지.\"" : "\"여기가 마음에 들어. 남겠어.\"";
            return new Demand { wage = JsMath.Round(baseWage * loyal * mult), reason = reason };
        }
    }
}
