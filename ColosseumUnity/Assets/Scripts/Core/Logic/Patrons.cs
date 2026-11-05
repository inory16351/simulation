// FM 세계 단계 8 (Unity판 전용, FM식): 후원자 시즌 목표(이사회식)와 스폰서.
// - 시즌 시작에 같은 리그 안 주전 전력 순위로 목표: 상위 3 → 승격(1부는 우승 경쟁), 상위 절반 → 상위권, 그 밖 → 잔류.
//   달성하면 보너스·명성, 못 하면 다음 시즌 지원(스폰서 정액) 감소, 연속 실패면 후원 철회(지원 더 감소). 해고·게임오버는 없다.
// - 스폰서: 시즌 시작에 부·명성으로 후보 셋. 우리는 고르고(안 고르면 기본 제안 자동 체결), AI는 가장 좋은 것을 고른다.
//   조건은 급료일마다 받는 정액 + 시즌 끝 순위 보너스. AI의 정액 부수입(AiSideIncome)을 대신한다.
// 예산은 나누지 않는다: 결과는 금고에 돈으로만 들어간다 (돈 부족 → 반란·탈주 구조 유지).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Colosseum.Core
{
    public class BoardGoal
    {
        public int season;
        /// <summary>promote | top | survive (1부의 promote는 '우승 경쟁': 3위 안)</summary>
        public string kind;
        /// <summary>이 순위 안에 들면 달성</summary>
        public int targetRank;
        /// <summary>시즌 시작 때 본 주전 전력 순위</summary>
        public int expectedRank;
        public int? finalRank;
        public bool? met;
    }

    public class SponsorDeal
    {
        public string id, name;
        public int season;
        /// <summary>급료일마다 받는 돈 (세금 전)</summary>
        public double weekly;
        /// <summary>시즌 끝 순위 보너스의 최대치 (1위 전부, 꼴찌 0)</summary>
        public double rankBonus;
        /// <summary>문장 그림 (Resources/Art/sponsor): salt, wine, ... (Patrons.SponsorIcons)</summary>
        public string icon;
        /// <summary>0 안정형(정액 많음) · 1 균형형 · 2 성과형(보너스 큼)</summary>
        public int style;
    }

    public static class Patrons
    {
        public static bool Enabled => FmRules.Enabled;

        /// <summary>우리가 스폰서를 고를 수 있는 마지막 날 (안 고르면 기본 제안을 맺는다)</summary>
        public const int SponsorPickUntil = 14;

        static readonly string[] SponsorNames = { "소금 상인 조합", "포도주 상단", "곡물 창고", "청동 공방", "해운 조합", "향료 상회", "말 사육장", "대리석 채석장", "은행가 가문", "올리브유 상회", "노예 상인 길드", "무기 대장간" };
        /// <summary>SponsorNames와 같은 순서의 문장 그림 이름</summary>
        public static readonly string[] SponsorIcons = { "salt", "wine", "grain", "bronze", "ship", "spice", "horse", "marble", "bank", "olive", "slaver", "arms" };
        public static readonly string[] StyleNames = { "안정형", "균형형", "성과형" };
        public static readonly string[] StyleIcons = { "steady", "balanced", "bonus" };
        public static readonly string[] StyleDesc = { "급료일마다 받는 돈이 많고 순위 보너스는 적습니다", "정액과 순위 보너스가 고릅니다", "정액은 적지만 높은 순위로 끝내면 큰 보너스를 받습니다" };

        /// <summary>문장 그림 이름 (옛 저장의 계약은 이름으로 찾는다)</summary>
        public static string IconOf(SponsorDeal d)
        {
            if (!string.IsNullOrEmpty(d.icon)) return d.icon;
            for (int i = 0; i < SponsorNames.Length; i++) if (d.name != null && d.name.EndsWith(SponsorNames[i])) return SponsorIcons[i];
            return "seal";
        }

        /// <summary>이번 시즌 남은 급료일 수 (오늘 뒤로)</summary>
        public static int PaydaysLeft(GameState s) => Calendar.DaysPerSeason / Calendar.PaydayInterval - s.day / Calendar.PaydayInterval;

        /// <summary>이 계약으로 이번 시즌 남은 기간에 받을 돈 어림 (순위 보너스는 시즌 시작 전력 순위로)</summary>
        public static double SeasonValue(GameState s, Ludus l, SponsorDeal d)
        {
            int teams = FmRules.TeamsPerTier;
            int rank = l.boardGoal?.expectedRank ?? (teams + 1) / 2;
            return d.weekly * PaydaysLeft(s) + JsMath.Round(d.rankBonus * (teams - rank) / (double)(teams - 1));
        }

        /// <summary>주전 전력 (상위 5명 평균 CA)</summary>
        public static double Strength(GameState s, Ludus l)
        {
            var cas = l.gladiators.Select(id => s.gladiators.TryGetValue(id, out var g) && g.alive ? GladiatorRules.ComputeCA(g) : -1).Where(x => x >= 0).OrderByDescending(x => x).Take(5).ToList();
            return cas.Count == 0 ? 0 : cas.Average();
        }

        /// <summary>시즌 시작: 모든 루두스 목표, 스폰서 후보 (AI는 바로 체결)</summary>
        public static void SeasonStart(GameState s, Rng rng)
        {
            if (!Enabled) return;
            foreach (var group in s.ludi.Values.GroupBy(l => (l.nation, l.tier)))
            {
                var ranked = group.OrderByDescending(l => Strength(s, l)).ToList();
                int n = ranked.Count;
                for (int i = 0; i < n; i++)
                {
                    var l = ranked[i];
                    int exp = i + 1;
                    string kind = exp <= 3 ? "promote" : exp <= n / 2 ? "top" : "survive";
                    int target = kind == "promote" ? 3 : kind == "top" ? n / 2 : n - FmRules.Relegate;
                    l.boardGoal = new BoardGoal { season = s.season, kind = kind, targetRank = target, expectedRank = exp };
                    var offers = Offers(s, rng, l);
                    if (l.isPlayer) { l.sponsorOffers = offers; l.sponsor = null; }
                    else l.sponsor = offers.OrderByDescending(o => o.weekly * 24 + o.rankBonus * 0.5).First();
                }
            }
            var me = s.ludi[s.playerLudusId];
            StateUtil.PushMessage(s, "info", $"후원자 목표: {GoalText(me.boardGoal, me.tier)}",
                $"후원자들이 이번 시즌 {me.tier}부 {GoalText(me.boardGoal, me.tier)}을(를) 바랍니다 (시즌 시작 전력 {me.boardGoal.expectedRank}위).\n" +
                $"달성하면 보너스와 명성을 얻고, 못 하면 다음 시즌 스폰서 지원이 줄어듭니다. 해고는 없습니다.\n\n" +
                $"스폰서 후보 {me.sponsorOffers.Count}곳이 제안을 보냈습니다. 금고 화면에서 {Calendar.DateLabel(SponsorPickUntil)}까지 하나를 고르세요. 고르지 않으면 첫 번째 제안을 맺습니다.");
        }

        public static string GoalText(BoardGoal g, int tier) => g == null ? "—" : g.kind switch
        {
            "promote" => tier == 1 ? "우승 경쟁 (3위 안)" : "승격 (3위 안)",
            "top" => $"상위권 ({g.targetRank}위 안)",
            _ => $"잔류 ({g.targetRank}위 안)",
        };

        /// <summary>후원 수준: 목표를 놓치면 다음 시즌 줄고, 두 번 연속이면 후원 철회 (더 준다)</summary>
        public static double SupportMult(Ludus l) => (l.patronFails ?? 0) switch { 0 => 1.0, 1 => 0.85, _ => 0.6 };

        /// <summary>스폰서 정액의 바탕: 부마다 (예전 AI 부수입과 같은 크기)</summary>
        static double BaseWeekly(int tier) => Economy.AiSideIncome(tier);

        static List<SponsorDeal> Offers(GameState s, Rng rng, Ludus l)
        {
            double rep = Reputation.LudusReputation(s, l);
            double fame = Math.Max(0.8, Math.Min(1.25, 0.8 + (rep - FmRules.ReputationBase(l.tier)) / 60));
            double weekly = BaseWeekly(l.tier) * fame * SupportMult(l);
            var o = new List<SponsorDeal>();
            // 셋: 안정형(정액 많고 보너스 적음) · 균형 · 성과형(정액 적고 보너스 큼)
            double[] split = { 1.15, 1.0, 0.75 };
            double[] bonus = { 2, 6, 14 };
            var names = rng.Shuffle(Enumerable.Range(0, SponsorNames.Length).ToList());
            for (int i = 0; i < 3; i++)
                o.Add(new SponsorDeal
                {
                    id = $"SP{s.nextId++}", name = $"{Data.Nation(l.nation).@short} {SponsorNames[names[i]]}", season = s.season,
                    icon = SponsorIcons[names[i]], style = i,
                    weekly = JsMath.Round(weekly * split[i] * rng.Range(0.95, 1.05)),
                    rankBonus = JsMath.Round(weekly * bonus[i] * rng.Range(0.95, 1.05) / 10) * 10,
                });
            return o;
        }

        /// <summary>우리가 스폰서를 고른다</summary>
        public static string PickSponsor(GameState s, string offerId)
        {
            var me = s.ludi[s.playerLudusId];
            if (me.sponsor != null) return "이번 시즌 스폰서는 이미 정했습니다";
            var o = me.sponsorOffers?.FirstOrDefault(x => x.id == offerId);
            if (o == null) return "제안이 없습니다";
            me.sponsor = o;
            me.sponsorOffers = null;
            StateUtil.PushMessage(s, "info", $"스폰서 계약: {o.name}", $"급료일마다 {o.weekly:N0} D, 시즌 끝 순위에 따라 최대 {o.rankBonus:N0} D를 받습니다.");
            return null;
        }

        /// <summary>매일: 고를 기한이 지나면 첫 제안을 맺는다</summary>
        public static void Daily(GameState s)
        {
            if (!Enabled) return;
            var me = s.ludi[s.playerLudusId];
            if (me.sponsor == null && me.sponsorOffers != null && me.sponsorOffers.Count > 0 && s.day > SponsorPickUntil) PickSponsor(s, me.sponsorOffers[0].id);
        }

        /// <summary>급료일: 스폰서 정액 (Economy.Payday에서, 다른 돈을 내기 전에)</summary>
        public static double WeeklyOf(Ludus l) => Enabled && l.sponsor != null ? l.sponsor.weekly : 0;

        /// <summary>시즌 끝(승강 전 순위로): 목표 판정과 스폰서 순위 보너스</summary>
        public static void SeasonEnd(GameState s, Dictionary<string, int> finalRank)
        {
            if (!Enabled) return;
            foreach (var l in s.ludi.Values)
            {
                if (!finalRank.TryGetValue(l.id, out int rank)) continue;
                int teams = FmRules.TeamsPerTier;
                if (l.sponsor != null && l.sponsor.season == s.season)
                {
                    double b = JsMath.Round(l.sponsor.rankBonus * (teams - rank) / (double)(teams - 1));
                    if (b > 0) Economy.AddMoney(s, l, b, $"{l.sponsor.name} 순위 보너스");
                }
                var g = l.boardGoal;
                if (g == null || g.season != s.season) continue;
                g.finalRank = rank;
                g.met = rank <= g.targetRank;
                if (g.met == true)
                {
                    double bonus = JsMath.Round(FmRules.TierMoney("dividendBaseFm", l.tier) * (g.kind == "promote" ? 0.4 : g.kind == "top" ? 0.25 : 0.15));
                    Economy.AddMoney(s, l, bonus, "후원자 목표 달성 보너스");
                    l.titles = (l.titles ?? 0) + 0.1;
                    l.patronFails = 0;
                }
                else l.patronFails = (l.patronFails ?? 0) + 1;
                if (l.isPlayer)
                    StateUtil.PushMessage(s, g.met == true ? "info" : "warning", g.met == true ? "후원자 목표 달성" : "후원자 목표 미달",
                        g.met == true ? $"목표({GoalText(g, l.tier)})를 이뤘습니다. {rank}위. 보너스를 받았고 루두스 명성이 올랐습니다."
                        : $"목표({GoalText(g, l.tier)})를 이루지 못했습니다. {rank}위. " + ((l.patronFails ?? 0) >= 2 ? "두 시즌 연속이라 후원자 일부가 손을 뗐습니다. 다음 시즌 스폰서 지원이 크게 줄어듭니다." : "다음 시즌 스폰서 지원이 줄어듭니다."));
            }
        }

        /// <summary>입장 수입 배율 (FM 세계): 상대 명성 × 경기 중요도. MatchLogic.Settle의 관중 수입에 곱한다 (이중 지급 없음)</summary>
        public static double GateMult(GameState s, Ludus home, Ludus away)
        {
            if (!Enabled || home == null || away == null) return 1;
            double oppRep = Reputation.LudusReputation(s, away), baseRep = FmRules.ReputationBase(home.tier);
            double opp = Math.Max(0.85, Math.Min(1.25, 1 + (oppRep - baseRep) / 80));
            double importance = 1;
            if (home.tier == away.tier && s.day >= 70 && s.day <= FmRules.RegularEnd)
            {
                var table = Economy.TierRanking(s, home.tier, home.nation);
                int a = table.IndexOf(home) + 1, b = table.IndexOf(away) + 1, n = table.Count;
                bool top = a <= 3 && b <= 3, bottom = a > n - FmRules.Relegate - 1 && b > n - FmRules.Relegate - 1;
                if (top || bottom) importance = 1.2;
            }
            return opp * importance;
        }
    }
}
