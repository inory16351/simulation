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

        /// <summary>AI 검투사 생성 목표 능력 (능력치 평균). 부의 주전·벤치 CA 대역 가운데쯤</summary>
        public static double RosterTarget(int tier) => TargetForCa((StarterCa(tier).lo + BenchCa(tier).hi) / 2);

        // ── FM 세계 (2026-11-05): 부당 20팀, 승강 3팀. balance.json의 teamsPerTier·promote·relegate는 웹판 비교용이라 그대로 둔다 ──

        /// <summary>부마다 루두스 수</summary>
        public static int TeamsPerTier => Enabled ? 20 : (int)L["teamsPerTier"];
        /// <summary>시즌 끝에 한 부 올라가는 수</summary>
        public static int Promote => Enabled ? 3 : (int)L["promote"];
        /// <summary>시즌 끝에 한 부 내려가는 수</summary>
        public static int Relegate => Enabled ? 3 : (int)L["relegate"];

        /// <summary>부별 주전(상위 5) CA 대역 — 리그 수준 차가 곧 난이도</summary>
        public static (double lo, double hi) StarterCa(int tier) => tier switch { 1 => (140, 180), 2 => (100, 140), 3 => (70, 110), _ => (40, 80) };
        /// <summary>부별 벤치 CA 대역</summary>
        public static (double lo, double hi) BenchCa(int tier) => tier switch { 1 => (115, 140), 2 => (80, 105), 3 => (55, 75), _ => (30, 50) };

        /// <summary>CA를 생성기 목표 능력치 평균으로 (ComputeCA = (평균-1)/19×200, 스타일 가중·숙련으로 생성 CA가 조금 높게 나오는 만큼 뺀다)</summary>
        public static double TargetForCa(double ca) => 1 + ca * 19 / 200 - CaBias;
        /// <summary>생성기 목표 대비 실제 CA 쏠림 (능력치 평균으로, 측정값)</summary>
        public const double CaBias = 0.6;

        /// <summary>루두스 명성의 바탕 (리그 단계)</summary>
        public static double ReputationBase(int tier) => tier switch { 1 => 45, 2 => 18, 3 => 9, _ => 3 };

        /// <summary>리그 단계에 따른 몸값·주급 배율 (높은 리그에서 뛰는 검투사가 비싸다)</summary>
        public static double TierValueMult(int tier) => tier switch { 1 => 1.35, 2 => 1.0, 3 => 0.78, _ => 0.62 };

        /// <summary>아래 부의 고정비 배율 (시설 유지비·식솔 급료). 3·4부 수입으로도 루두스를 꾸릴 수 있게. 웹판 규칙은 1</summary>
        public static double CostTierMult(int tier) => !Enabled ? 1 : tier switch { 3 => 0.8, 4 => 0.6, _ => 1.0 };

        /// <summary>하부 리그 지원금: 3·4부 플레이어 루두스가 급료일마다 받는 돈 (세금 전). AI의 부수입(Economy.AiSideIncome)에 해당한다. 웹판 규칙은 0</summary>
        public static double LowerLeagueGrant(int tier) => !Enabled ? 0 : tier switch { 4 => 120, 3 => 70, _ => 0 };

        public static string TierName(int tier) => $"{tier}부";

        /// <summary>AI 루두스 기본 인원 (웹판 aiRoster 8). 등록 로스터 15명 중 여유를 두고, 나머지는 AI가 무소속 영입으로 판단해 채운다</summary>
        public static int AiRoster(int tier) => !Enabled ? (int)Bal.Economy["aiRoster"] : tier switch { 1 => 14, 2 => 13, 3 => 12, _ => 11 };

        /// <summary>시즌당 같은 상대에게 보낼 수 있는 리그 도전장 수 (부 안에서 일정이 고르게 잡히게)</summary>
        public const int SameOpponentPerSeason = 2;
        /// <summary>정규 리그 최소 경기 수 (미달이면 시즌 말 RP 벌점)</summary>
        public const int MinLeagueMatches = 10;

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
