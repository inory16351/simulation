// 몸값과 주급 요구 (Unity판 전용, FM식): 능력(CA)만으로 정하지 않는다.
// 명성(인기), 전적·승률, 킬, 나이와 잠재력, 지금 뛰는 리그 단계, 계약 남은 기간을 함께 본다.
// FM식 규칙이 꺼져 있으면(골든 테스트) 웹판 계산 그대로.
using System;
using System.Linq;

namespace Colosseum.Core
{
    public static class Valuation
    {
        /// <summary>관중·업계가 보는 '격' 0~100: 능력 35%, 명성 40%, 승률·킬 25%</summary>
        public static double Standing(Gladiator g)
        {
            double ca = GladiatorRules.ComputeCA(g);
            double form = (Contracts.WinRate(g) - 0.5) * 60;          // -30 ~ +30
            double games = g.record.wins + g.record.losses;
            double proven = Math.Min(1, games / 12.0);                // 경기를 치를수록 전적을 믿는다
            double kills = Math.Min(8, g.record.kills * 1.5);
            return Math.Max(0, Math.Min(100, ca * 0.32 + g.popularity * 0.42 + form * proven * 0.5 + kills + 6));
        }

        /// <summary>뛰는 리그 단계 (무소속은 2부와 3부 사이로 본다)</summary>
        static double TierMult(GameState s, Gladiator g)
        {
            var l = s == null ? null : Career.LudusOf(s, g.id);
            return l == null ? 0.88 : FmRules.TierValueMult(l.tier);
        }

        static double AgeMult(Gladiator g) => Economy.StageOf(g) switch { "youth" => 0.75, "growth" => 1.15, "prime" => 1.0, _ => 0.55 };

        /// <summary>시장 몸값 (D)</summary>
        public static double MarketValue(GameState s, Gladiator g)
        {
            double ca = GladiatorRules.ComputeCA(g);
            double wr = Contracts.WinRate(g);
            double games = g.record.wins + g.record.losses;
            double proven = Math.Min(1, games / 12.0);
            double form = 1 + (wr - 0.5) * 0.9 * proven;               // 승률 70%면 ×1.18 (경기 수만큼)
            double ability = ca * ca * 0.075 * form;
            double fame = g.popularity * g.popularity * 0.22 + g.popularity * 4;
            double potential = Math.Max(0, g.hidden.pa - ca) * 3.5 * (Economy.StageOf(g) == "decline" ? 0 : 1);
            double kills = Math.Min(400, g.record.kills * 35);
            double v = (ability + fame + potential + kills + 120) * AgeMult(g) * TierMult(s, g);
            // FM처럼: 계약이 짧게 남을수록 싸다. 이번 시즌 만료면 시즌 말로 갈수록 더 떨어진다 (×0.85 → ×0.65)
            if (s != null && g.contract != null)
            {
                int left = g.contract.untilSeason - s.season;
                v *= left <= 0 ? 0.85 - 0.2 * s.day / Calendar.DaysPerSeason : left == 1 ? 0.92 : left == 2 ? 1.0 : 1.05;
            }
            // 불만인 검투사는 팔려는 쪽도 붙잡을 생각이 덜해 싸다
            if (g.grievance != null) v *= 0.85;
            return JsMath.Round(v / 10) * 10;
        }

        /// <summary>리그 단계별 주급 수준. 몸값(TierValueMult)보다 아래 부를 더 낮춘다 — 3·4부 수입으로 감당할 수 있게</summary>
        static double WageTierMult(int tier) => tier switch { 1 => 0.95, 2 => 0.88, 3 => 0.68, _ => 0.52 };

        /// <summary>이만한 검투사가 받을 만한 주급 (자유민 기준), 지금 뛰는 리그 기준 (무소속은 2부와 3부 사이). 노예는 웹판 셈 그대로</summary>
        public static double FairWage(GameState s, Gladiator g)
        {
            var l = s == null ? null : Career.LudusOf(s, g.id);
            return FairWageMult(g, l == null ? 0.86 : WageTierMult(l.tier));
        }

        /// <summary>그 부 루두스에서 받을 만한 주급 (영입할 때: 무소속·이적 검투사가 옮겨 갈 루두스의 부 기준)</summary>
        public static double FairWageAt(Gladiator g, int tier) => FairWageMult(g, WageTierMult(tier));

        static double FairWageMult(Gladiator g, double tierMult)
        {
            if (g.status == "slave") return GladiatorGen.ComputeWage(g, GladiatorRules.ComputeCA(g));
            double st = Standing(g);
            double race = Data.Race(g.race).wageMult;
            double w = (12 + st * 0.95 + st * st * 0.012) * race * tierMult;
            return Math.Max(8, JsMath.Round(w));
        }
    }
}
