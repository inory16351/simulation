// 증축: 시설 건설·유지비·효과 (src/core/facilities.ts)
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;

namespace Colosseum.Core
{
    public class FacilityDef
    {
        public string id, name, desc;
        /// <summary>레벨별 효과 설명 (0~5)</summary>
        public Func<int, string> effect;
    }

    /// <summary>dailyConstruction이 돌려주는 완공 정보</summary>
    public class ConstructionDone
    {
        public Ludus ludus;
        public string facility;
        public int level;
    }

    public static class FacilitiesLogic
    {
        public const int MaxLevel = 5;

        static string N(double x) => StateUtil.Num(x);

        public static readonly List<FacilityDef> FacilitiesConst = new()
        {
            new FacilityDef { id = "barracks", name = "숙소", desc = "검투사가 먹고 자는 곳", effect = l => (FmRules.Enabled ? "" : $"정원 {N(CapacityAt(l))}명 · ") + $"사기 +{N(l * 0.5)}/주" },
            new FacilityDef { id = "training", name = "훈련 마당", desc = "모래를 깐 연습장과 허수아비", effect = l => $"훈련 성장 +{N((l - 1) * 10)}% · 훈련 부상 -{N((l - 1) * 5)}%" },
            new FacilityDef { id = "medical", name = "의무실", desc = "의원과 약초, 붕대", effect = l => $"부상 회복 +{N((l - 1) * 20)}% · 사투 사망 -{N(l * 2)}%p" },
            new FacilityDef { id = "bath", name = "목욕탕", desc = "뜨거운 물과 마사지", effect = l => $"피로 회복 +{N(l * 2)}/일 · 혹사 회복 +{StateUtil.Fixed(l * 0.3, 1)}/일" },
            new FacilityDef { id = "arena", name = "홈 경기장", desc = "우리 루두스의 작은 투기장", effect = l => $"홈 관중 수입 ×{StateUtil.Fixed(ArenaTicketMult(l), 2)}" },
            new FacilityDef { id = "academy", name = "유소년 학당", desc = "고아와 가난한 집 아이를 검투사로 키우는 학당", effect = l => l != 0 ? (FmRules.Enabled ? $"모래 시험 후보 {N(l * 2 + 1)}명 · 높은 잠재력 유소년이 나올 확률 {StateUtil.Fixed(Youth.PaOdds(l).p160 * 100, 1)}% (특급 {StateUtil.Fixed(Youth.PaOdds(l).p180 * 100, 1)}%) · 정원 {N(l * 3 + 3)}명" : $"매 시즌 입학 {N(l + 1)}명 · 잠재력 상한 ★{StateUtil.Fixed(AcademyPaCap(l) / 40.0, 1)} · 정원 {N(l * 3 + 3)}명") : "없음" },
            new FacilityDef { id = "guard", name = "경비 초소", desc = "탈주와 반란을 막는 경비대", effect = l => $"탈주 -{N(l * 10)}% · 반란 -{N(l * 3)}%p" },
        };

        public static FacilityDef GetFacility(string id) => FacilitiesConst.Find(f => f.id == id);

        /// <summary>TS ludus.facilities[id] (C#에선 필드라 이름으로 읽는다)</summary>
        public static int Level(Facilities f, string id) => id switch
        {
            "barracks" => f.barracks,
            "training" => f.training,
            "medical" => f.medical,
            "bath" => f.bath,
            "arena" => f.arena,
            "guard" => f.guard,
            "academy" => f.academy,
            _ => 0,
        };

        public static void SetLevel(Facilities f, string id, int v)
        {
            switch (id)
            {
                case "barracks": f.barracks = v; break;
                case "training": f.training = v; break;
                case "medical": f.medical = v; break;
                case "bath": f.bath = v; break;
                case "arena": f.arena = v; break;
                case "guard": f.guard = v; break;
                case "academy": f.academy = v; break;
            }
        }

        static readonly int[] Capacity = { 8, 8, 12, 16, 20, 24 };
        static readonly int[] PaCap = { 0, 120, 140, 160, 180, 200 };
        static readonly double[] CostBase = { 0, 1000, 2500, 6000, 12000, 25000 };
        static readonly int[] CostDays = { 0, 10, 14, 21, 28, 42 };
        static readonly double[] Upkeep = { 0, 10, 20, 40, 70, 110 };

        public static int CapacityAt(int lvl) => lvl >= 0 && lvl < Capacity.Length ? Capacity[lvl] : 8;
        public static double ArenaTicketMult(int lvl) => 0.7 + lvl * 0.15;
        public static int AcademyPaCap(int lvl) => lvl >= 0 && lvl < PaCap.Length ? PaCap[lvl] : 0;

        /// <summary>다음 레벨로 올리는 비용·기간</summary>
        public static (double cost, int days) UpgradeCost(string id, int toLevel)
        {
            double b = toLevel >= 0 && toLevel < CostBase.Length ? CostBase[toLevel] : 0;
            double mult = id == "arena" ? 1.5 : 1;
            return (JsMath.Round(b * mult), toLevel >= 0 && toLevel < CostDays.Length ? CostDays[toLevel] : 0);
        }

        /// <summary>주간 유지비 (급료일에 나간다). FM식은 아래 부일수록 싸다 (FmRules.CostTierMult)</summary>
        public static double WeeklyUpkeep(Ludus ludus)
        {
            var f = ludus.facilities;
            double s = 0;
            foreach (var lvl in new[] { f.barracks, f.training, f.medical, f.bath, f.arena, f.guard, f.academy })
                s = s + (lvl >= 0 && lvl < Upkeep.Length ? Upkeep[lvl] : double.NaN);
            return JsMath.Round(s * FmRules.CostTierMult(ludus.tier));
        }

        public static string StartUpgrade(GameState state, Ludus ludus, string id)
        {
            if (ludus.construction != null) return "이미 공사 중인 시설이 있습니다";
            int lvl = Level(ludus.facilities, id);
            if (lvl >= MaxLevel) return "이미 최고 단계입니다";
            var (cost, days) = UpgradeCost(id, lvl + 1);
            if (ludus.treasury < cost) return "금고에 돈이 모자랍니다";
            Economy.AddMoney(state, ludus, -cost, $"{GetFacility(id).name} 증축");
            ludus.construction = new Construction { facility = id, toLevel = lvl + 1, doneAt = Calendar.AbsDay(state.season, state.day) + days };
            return null;
        }

        /// <summary>매일: 공사가 끝났는지. 완공된 것을 돌려준다</summary>
        public static List<ConstructionDone> DailyConstruction(GameState state)
        {
            int now = Calendar.AbsDay(state.season, state.day);
            var done = new List<ConstructionDone>();
            foreach (var l in new List<Ludus>(state.ludi.Values))
            {
                var c = l.construction;
                if (c == null || c.doneAt > now) continue;
                SetLevel(l.facilities, c.facility, c.toLevel);
                l.construction = null;
                done.Add(new ConstructionDone { ludus = l, facility = c.facility, level = c.toLevel });
                if (l.isPlayer)
                {
                    var f = GetFacility(c.facility);
                    StateUtil.PushMessage(state, "info", $"{f.name} {c.toLevel}단계 완공", $"{f.name} 공사가 끝났습니다.\n{f.effect(c.toLevel)}");
                }
            }
            return done;
        }

        public static string ConstructionLabel(GameState state, Ludus ludus)
        {
            var c = ludus.construction;
            if (c == null) return null;
            double left = c.doneAt - Calendar.AbsDay(state.season, state.day);
            return $"{GetFacility(c.facility).name} {c.toLevel}단계 공사 중 · {N(left)}일 남음 ({Calendar.DateLabel(Calendar.FromAbs(c.doneAt).day)} 완공)";
        }
    }
}
