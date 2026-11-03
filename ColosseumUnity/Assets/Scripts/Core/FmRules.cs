// FM식 규칙 묶음 (Unity판 전용): 4부 리그, 시작 리그 고르기, 복합 몸값·주급 요구, 계약 조항, 이적 명단·역제안,
// 결정하지 않으면 넘어가지 않는 제안, 식솔 세부 능력치. 골든 테스트(웹판 비교)는 Enabled = false로 끈다.
using System;
using Newtonsoft.Json.Linq;

namespace Colosseum.Core
{
    public static class FmRules
    {
        public static bool Enabled = true;

        /// <summary>리그 단계 수 (웹판은 2부까지)</summary>
        public static int MaxTier => Enabled ? 4 : 2;

        static JToken L => Data.Balance["league"];

        /// <summary>부마다 시작 RP (1부 1500, 2부 1200, 3부 950, 4부 700)</summary>
        public static double RpBase(int tier)
        {
            var k = L[$"rpTier{tier}"];
            if (k != null) return (double)k;
            return tier switch { 1 => 1500, 2 => 1200, 3 => 950, _ => 700 };
        }

        /// <summary>economy 표에서 부마다 값을 읽는다 (없는 부는 가장 아래 부 값의 비율로)</summary>
        public static double TierMoney(string key, int tier)
        {
            var t = Bal.Economy[key];
            var v = t?[tier.ToString()];
            if (v != null) return (double)v;
            double two = (double)t["2"];
            return Math.Round(two * Math.Pow(0.6, tier - 2));
        }

        /// <summary>AI 검투사 생성 목표 능력 (능력치 평균)</summary>
        public static double RosterTarget(int tier) => tier switch { 1 => 10.5, 2 => 7.5, 3 => 6.3, _ => 5.2 };

        /// <summary>루두스 명성의 바탕 (리그 단계)</summary>
        public static double ReputationBase(int tier) => tier switch { 1 => 45, 2 => 18, 3 => 9, _ => 3 };

        /// <summary>리그 단계에 따른 몸값·주급 배율 (높은 리그에서 뛰는 검투사가 비싸다)</summary>
        public static double TierValueMult(int tier) => tier switch { 1 => 1.35, 2 => 1.0, 3 => 0.78, _ => 0.62 };

        public static string TierName(int tier) => $"{tier}부";

        // ── 시즌 흐름: 정규 리그 15~105일 → 국내 컵 106~129일 → 판테온 컵 131~157일 ──

        /// <summary>정규 리그 마지막 날. 다음 날 순위가 굳고(이후 RP 변동 없음) 국내 컵 대진을 뽑는다</summary>
        public const int RegularEnd = 105;
        /// <summary>새 도전장을 받는 마지막 날 (잡힌 경기가 리그 안에 끝나게)</summary>
        public const int ChallengeClose = 98;

        /// <summary>리그 순위가 굳었는가 (RP가 더 움직이지 않는다)</summary>
        public static bool Frozen(GameState s) => Enabled && s.day > RegularEnd;

        /// <summary>시즌 배당 비율: 순위에 비례 (10팀이면 1위 ×1.6 ~ 10위 ×0.6)</summary>
        public static double DividendCoef(int rank, int teams) => teams <= 1 ? 1 : 1.6 - (rank - 1) / (double)(teams - 1);
    }
}
