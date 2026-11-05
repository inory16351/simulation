// FM 세계 단계 3 (Unity판 전용, FM식): 몸값·주급이 능력만으로 정확히 정해지지 않고 선수 상황으로 흔들린다.
// 구매 루두스를 인자로 받아 유저와 AI가 같은 함수를 쓴다.
//   3-1 선수가 스스로 매기는 값: 요구 주급 = 그 부에서 받을 만한 주급 × 상황 배율(무소속 기간·승률·불만·벤치·소속 위기·쇠퇴기·리그 이동·자존심)
//   3-2 소속 구단이 부르는 값: 주전 ×1.3, 벤치 ×0.8, 재정 위기 ×0.75 (불만 ×0.85는 Valuation.MarketValue가 이미 곱한다)
//   3-3 영입 관심 판정: 절대 CA로 선수의 적정 부를 구해 '구매자 부 − 적정 부'가 2 이상이면 확률 판정. (선수, 구매자, 이적 기간)마다 고정
using System;
using System.Collections.Generic;
using System.Linq;

namespace Colosseum.Core
{
    public static class SelfValue
    {
        public static bool Enabled => FmRules.Enabled;

        /// <summary>요구 주급 배율의 전체 범위</summary>
        public const double MinMult = 0.4, MaxMult = 1.8;

        /// <summary>절대 CA로 본 선수의 적정 부 (주전 대역 아래 끝 기준: 140↑ 1부, 100↑ 2부, 70↑ 3부, 그 밖 4부)</summary>
        public static int ProperTier(Gladiator g)
        {
            double ca = GladiatorRules.ComputeCA(g);
            for (int t = 1; t < FmRules.MaxTier; t++) if (ca >= FmRules.StarterCa(t).lo) return t;
            return FmRules.MaxTier;
        }

        /// <summary>문자열 해시 (저장 없이 선수마다 고정된 값을 만든다. .NET GetHashCode는 실행마다 달라 쓰지 않는다)</summary>
        static uint Hash(string a)
        {
            uint h = 2166136261;
            foreach (char c in a) { h ^= c; h *= 16777619; }
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return h;
        }

        static double Unit(string a) => Hash(a) / 4294967296.0;

        /// <summary>선수별 자존심: 고정 ±15% (id 해시)</summary>
        public static double Pride(Gladiator g) => 1 + (Unit("pride:" + g.id) * 0.3 - 0.15);

        /// <summary>무소속으로 지낸 날 수 (모르면 0)</summary>
        public static double DaysFree(GameState s, Gladiator g) =>
            g.freeSince.HasValue && s.freeAgents.Contains(g.id) ? Math.Max(0, Calendar.AbsDay(s.season, s.day) - g.freeSince.Value) : 0;

        /// <summary>그 루두스가 위기인가: 금고가 바닥(주급 4주치 미만)이거나, 급료가 밀렸거나, 정규 리그 중 강등권</summary>
        public static bool InCrisis(GameState s, Ludus l)
        {
            if (l == null) return false;
            double wages = Economy.WeeklyWages(s, l);
            if (l.treasury < wages * 4) return true;
            if (l.gladiators.Any(id => s.gladiators.TryGetValue(id, out var x) && x.alive && x.unpaidWeeks > 0)) return true;
            if (l.tier < FmRules.MaxTier && s.day >= 40 && s.day <= FmRules.RegularEnd)
            {
                var table = Economy.TierRanking(s, l.tier, l.nation);
                int rank = table.FindIndex(x => x.id == l.id) + 1;
                if (rank > table.Count - FmRules.Relegate) return true;
            }
            return false;
        }

        /// <summary>소속 루두스 상위 5명(주전)인가</summary>
        public static bool IsStarter(GameState s, Gladiator g, Ludus owner)
        {
            if (owner == null) return false;
            int ca = GladiatorRules.ComputeCA(g);
            int better = owner.gladiators.Count(id => id != g.id && s.gladiators.TryGetValue(id, out var x) && x.alive && GladiatorRules.ComputeCA(x) > ca);
            return better < 5;
        }

        public struct Factor
        {
            public double mult;
            public string line;
        }

        /// <summary>요구 주급 상황 배율과 요인 목록. renew = 지금 루두스와 재계약</summary>
        public static List<Factor> WageFactors(GameState s, Gladiator g, Ludus buyer, bool renew = false)
        {
            var f = new List<Factor>();
            void Add(double m, string line) { if (Math.Abs(m - 1) > 1e-9) f.Add(new Factor { mult = m, line = line }); }
            var owner = s.freeAgents.Contains(g.id) ? null : Career.LudusOf(s, g.id);
            bool free = owner == null;
            // 무소속 기간: 0주 ×1.0 → 12주 ×0.7 → 한 시즌(24주) 이상 ×0.55
            double weeks = DaysFree(s, g) / 7.0;
            if (free && weeks > 0)
            {
                double m = weeks <= 12 ? 1 - 0.3 * weeks / 12 : Math.Max(0.55, 0.7 - 0.15 * (weeks - 12) / 12);
                Add(m, weeks >= 12 ? "\"석 달째 일이 없으니 까다롭게 굴 처지가 아니지.\"" : weeks >= 4 ? "\"쉬는 동안 모아 둔 돈도 다 떨어져 가.\"" : "\"놀고 있긴 해도 아무 데나 가진 않아.\"");
            }
            // 최근 승률 (경기 수만큼 믿는다)
            double games = g.record.wins + g.record.losses, proven = Math.Min(1, games / 12);
            double wr = Contracts.WinRate(g);
            if (wr < 0.35) Add(1 - 0.15 * proven, "\"요즘 성적이 나빴던 건 나도 알아.\"");
            else if (wr > 0.7) Add(1 + 0.15 * proven, "\"내 전적을 봐. 이 정도는 받아야지.\"");
            // 불만: 떠나고 싶으면 덜 바라고, 남으라면 더 바란다
            if (g.grievance != null) Add(renew ? 1.2 : 0.85, renew ? "\"이 루두스에 남으려면 그만한 대우를 해 줘야지.\"" : "\"여기를 떠날 수만 있다면 조건은 맞춰 보지.\"");
            if (!free && !renew)
            {
                if (!IsStarter(s, g, owner)) Add(0.9, "\"뛸 자리만 준다면 욕심은 덜 낼게.\"");
                if (InCrisis(s, owner)) Add(0.87, "\"지금 루두스는 언제 무너질지 몰라.\"");
            }
            if (GladiatorRules.AgeStage(g) == "decline") Add(0.8, "\"이 나이에 많은 걸 바라진 않아.\"");
            // 리그 이동: 윗 리그로 가면 덜 바라고, 아랫 리그로 가면 부 차이마다 비싸진다 (무소속은 적정 부에서 옮긴다고 본다)
            if (!renew && buyer != null)
            {
                int from = owner?.tier ?? ProperTier(g);
                if (buyer.tier < from) Add(0.9, "\"더 큰 무대에서 뛸 기회라면야.\"");
                else if (buyer.tier > from) Add(Math.Pow(1.25, buyer.tier - from), "\"그 아래 리그까지 내려가려면 값을 더 쳐 줘야 해.\"");
            }
            Add(Pride(g), Pride(g) > 1.07 ? "\"난 싸게 굴지 않아.\"" : Pride(g) < 0.93 ? "\"돈보다는 뛰는 게 좋아.\"" : null);
            return f;
        }

        /// <summary>요구 주급 배율 (범위 0.4~1.8)과 가장 큰 요인 대사</summary>
        public static (double mult, string reason) WageMult(GameState s, Gladiator g, Ludus buyer, bool renew = false)
        {
            if (!Enabled) return (1, null);
            var f = WageFactors(s, g, buyer, renew);
            double m = f.Aggregate(1.0, (a, x) => a * x.mult);
            var top = f.Where(x => x.line != null).OrderByDescending(x => Math.Abs(Math.Log(x.mult))).FirstOrDefault();
            return (Math.Max(MinMult, Math.Min(MaxMult, m)), top.line);
        }

        /// <summary>상황을 반영한 바라는 주급의 바탕 (명성 격차 웃돈 전). base는 그 부에서 받을 만한 주급</summary>
        public static Demand Apply(GameState s, Gladiator g, Ludus buyer, Demand d, bool renew = false)
        {
            if (!Enabled || d.refuse) return d;
            var (m, reason) = WageMult(s, g, buyer, renew);
            d.wage = Math.Max(1, JsMath.Round(d.wage * m));
            if (reason != null && Math.Abs(m - 1) >= 0.08) d.reason = reason;
            return d;
        }

        /// <summary>3-2 소속 구단이 부르는 값의 상황 배율: 주전 ×1.3, 벤치 ×0.8, 재정 위기 ×0.75</summary>
        public static double AskingMult(GameState s, Gladiator g, Ludus owner)
        {
            if (!Enabled || owner == null) return 1;
            double m = IsStarter(s, g, owner) ? 1.3 : 0.8;
            if (InCrisis(s, owner) && owner.treasury < Economy.WeeklyWages(s, owner) * 4) m *= 0.75;
            return m;
        }

        // ── 3-3 영입 관심 판정 ──

        /// <summary>구매자 부와 선수 적정 부의 차이에 따른 기본 확률 (1 이하는 판정 없음)</summary>
        public static double BaseChance(int gap) => gap <= 1 ? 1 : gap == 2 ? 0.15 : 0.02;
        public static double CapChance(int gap) => gap <= 1 ? 1 : gap == 2 ? 0.4 : 0.1;

        /// <summary>이 루두스에서 뛸 마음이 생길 확률. offerRatio = 제안한 값 ÷ 기준 값 (높게 부르면 최대 ×1.5)</summary>
        public static double InterestChance(GameState s, Gladiator g, Ludus buyer, double offerRatio = 1)
        {
            if (!Enabled || buyer == null) return 1;
            int gap = buyer.tier - ProperTier(g);
            if (gap <= 1) return 1;
            double p = BaseChance(gap);
            var owner = s.freeAgents.Contains(g.id) ? null : Career.LudusOf(s, g.id);
            if (owner == null && DaysFree(s, g) >= 12 * 7) p *= 3;
            if (g.grievance != null && owner != null && !IsStarter(s, g, owner)) p *= 2;
            if (GladiatorRules.AgeStage(g) == "decline") p *= 3;
            if (owner != null && InCrisis(s, owner)) p *= 1.5;
            if (g.nation == buyer.nation) p *= 1.3;
            p *= 1 + Math.Max(0, Math.Min(0.5, offerRatio - 1));
            return Math.Min(CapChance(gap), p);
        }

        /// <summary>판정 열쇠: (선수, 구매자, 이적 기간). 다시 해 보거나 불러오기로 뚫지 못한다</summary>
        static string Key(GameState s, Gladiator g, Ludus buyer) => $"{g.id}:{buyer.id}:{Transfers.WindowKey(s)}";

        /// <summary>이 루두스로 올 마음이 있는가. 판정 굴림은 열쇠마다 고정이고, 한 번 거절하면 그 이적 기간 동안은 다시 묻지 못한다</summary>
        public static bool Interested(GameState s, Gladiator g, Ludus buyer, double offerRatio = 1)
        {
            if (!Enabled || buyer == null) return true;
            string key = Key(s, g, buyer);
            if (s.leagueRefusals != null && s.leagueRefusals.Contains(key)) return false;
            double p = InterestChance(s, g, buyer, offerRatio);
            if (p >= 1) return true;
            bool ok = Unit("interest:" + key) < p;
            // 판정은 열쇠 해시로 이미 고정이다. 남겨 둘 것은 우리가 값을 올려 다시 묻는 경우뿐이라 우리 거절만 적는다
            // (AI 거절까지 쌓으면 목록이 한 기간에 수만 줄이 되어 매번 훑는 데 오래 걸린다)
            if (!ok && buyer.isPlayer)
            {
                s.leagueRefusals ??= new List<string>();
                // 지난 이적 기간의 거절은 지운다
                string tail = ":" + Transfers.WindowKey(s);
                s.leagueRefusals.RemoveAll(k => !k.EndsWith(tail));
                s.leagueRefusals.Add(key);
            }
            return ok;
        }

        /// <summary>거절할 때 대사</summary>
        public static string RefusalLine(Gladiator g, Ludus buyer) =>
            $"{Josa.J(g.name, "이/가")} 고개를 젓습니다. \"{buyer.tier}부 리그에서 뛸 생각은 없습니다.\" (이번 이적 시장에서는 다시 물을 수 없습니다)";
    }
}
