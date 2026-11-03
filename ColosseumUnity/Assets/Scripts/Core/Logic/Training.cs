// 연무장 훈련과 성장 (src/core/training.ts). 하루치 훈련, 실전 경험치를 같은 성장 함수로 처리한다.
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Colosseum.Core
{
    public static class Training
    {
        static JObject T => Bal.Training;

        public static readonly Dictionary<string, string> FocusNames = new()
        {
            ["physical"] = "체력 단련", ["weapon"] = "무기술", ["defense"] = "방어", ["grapple"] = "격투", ["mental"] = "정신 수양", ["sense"] = "실전 감각",
            ["magic"] = "주술", ["show"] = "쇼맨십", ["doctrine"] = "교범 수련", ["rest"] = "휴식",
        };
        public static readonly Dictionary<string, string> FocusDesc = new()
        {
            ["physical"] = "근력·민첩·지구력·강인함·반응·회복력",
            ["weapon"] = "주무기 숙련과 정확도",
            ["defense"] = "회피·막기·방패 숙련",
            ["grapple"] = "제압·맨손 숙련",
            ["mental"] = "투지·침착·판단력·규율",
            ["sense"] = "예측력·기만·천재성·적응력·팀워크·타이밍·마무리 본능·위압감 (대련)",
            ["magic"] = "마력·주술 촉매 숙련 (마력이 있어야 효과)",
            ["show"] = "쇼맨십",
            ["doctrine"] = "전투 교범 숙련도",
            ["rest"] = "성장 없음. 피로·혹사 회복이 빠릅니다",
        };
        public static readonly Dictionary<string, string> IntensityNames = new() { ["low"] = "가볍게", ["normal"] = "보통", ["high"] = "강하게", ["extreme"] = "극한" };

        /// <summary>키: 능력치 id 또는 "prof:무기군". 넣은 순서를 지킨다 (지우지 않으므로 Dictionary로 충분)</summary>
        public static Dictionary<string, double> FocusTargets(Gladiator g, string focus)
        {
            var style = Data.Style(g.style);
            var t = new Dictionary<string, double>();
            void Add(string k, double w = 1) => t[k] = (t.TryGetValue(k, out var o) ? o : 0) + w;
            switch (focus)
            {
                case "physical": foreach (var a in Attrs.Physical) Add(a, 1 + (style.weights.TryGetValue(a, out var sw) ? sw : 0)); break;
                case "weapon": foreach (var p in style.profs) Add($"prof:{p}", 3); Add("acc", 2); break;
                case "defense": Add("eva", 2); Add("blk", 2); Add("prof:shield", 1); break;
                case "grapple": Add("grp", 3); Add("prof:unarmed", 2); break;
                case "mental": foreach (var a in new[] { "grit", "comp", "dec", "disc" }) Add(a); break;
                case "sense": foreach (var a in Attrs.Sense) Add(a); break;
                case "magic": if (g.attrs["mag"] > 1) { Add("mag", 3); Add("prof:focus", 1); } break;
                case "show": Add("show", 3); break;
                case "match":
                    // 실전은 스타일 핵심 능력치와 주무기, 정신력을 고르게 키운다
                    foreach (var kv in style.weights) Add(kv.Key, kv.Value);
                    foreach (var p in style.profs) Add($"prof:{p}", 2);
                    Add("grit"); Add("comp"); Add("dec");
                    // 실전에서만 제대로 느는 감각
                    Add("read", 0.6); Add("adapt", 0.6); Add("timing", 0.6); Add("kill", 0.4);
                    break;
            }
            return t;
        }

        public static void Bump(Gladiator g, string key, int delta)
        {
            if (key.StartsWith("prof:", StringComparison.Ordinal))
            {
                string p = key.Substring(5);
                g.prof[p] = Math.Max(1, Math.Min(20, g.prof[p] + delta));
            }
            else
            {
                g.attrs[key] = Math.Max(1, Math.Min(20, g.attrs[key] + delta));
            }
        }

        /// <summary>
        /// 성장 적용. days는 '보통 강도 훈련 며칠분'인지. focus는 훈련 종류 또는 "match".
        /// 쇠퇴기에는 신체가 떨어지고 정신은 조금 오른다.
        /// </summary>
        public static void Grow(Gladiator g, Rng rng, string focus, double days, double intensityMult = 1, double facilityLevel = 1)
        {
            string stage = GladiatorRules.AgeStage(g);
            var race = Data.Race(g.race);
            double ca = GladiatorRules.ComputeCA(g);
            double amount = (double)T["dailyBase"] * days * intensityMult * (1 + (facilityLevel - 1) * (double)T["facilityBonus"]);
            amount *= 0.7 + g.attrs["disc"] / 40.0;
            amount *= race.growthMult;
            if (g.traits.Contains("ambitious")) amount *= 1.15;
            if (g.traits.Contains("prodigy") && stage == "growth") amount *= 1.3;

            if (stage == "decline")
            {
                // 쇠퇴: 신체 능력치는 떨어지고, 노련함이 쌓인다
                double loss = amount * Math.Abs((double)T["ageMult"]["decline"]) * (g.traits.Contains("veteran") ? 0.5 : 1);
                var phys = new Dictionary<string, double>();
                foreach (var a in Attrs.Physical) phys[a] = 1;
                Accumulate(g, rng, phys, -loss);
                var ment = new Dictionary<string, double>();
                foreach (var a in Attrs.Mental.Take(3)) ment[a] = 1;
                Accumulate(g, rng, ment, amount * 0.3);
                return;
            }
            double paRoom = Math.Max(0, Math.Min(1, (g.hidden.pa - ca) / 40));
            if (paRoom <= 0) return;
            amount *= (double)T["ageMult"][stage] * Math.Max(0.1, paRoom);
            var targets = FocusTargets(g, focus);
            if (targets.Count > 0) Accumulate(g, rng, targets, amount);
        }

        public static void Accumulate(Gladiator g, Rng rng, Dictionary<string, double> targets, double amount)
        {
            // 성장량을 목표 능력치들에 가중치대로 나눠 누적한다
            double total = 0;
            foreach (var w in targets.Values) total = total + w;
            foreach (var kv in targets)
            {
                string k = kv.Key;
                double w = kv.Value;
                double share = amount * (w / total) * (0.6 + rng.Next() * 0.8);
                double v = (g.growth.TryGetValue(k, out var o) ? o : 0) + share;
                if (v >= 1) { Bump(g, k, 1); g.growth[k] = v - 1; }
                else if (v <= -1) { Bump(g, k, -1); g.growth[k] = v + 1; }
                else g.growth[k] = v;
            }
        }

        static readonly string[] InjuryAreas = { "arm", "leg", "torso" };

        /// <summary>하루치 훈련. 훈련 중 부상이 나면 설명을 돌려준다 (없으면 null)</summary>
        public static string DailyTraining(Gladiator g, Rng rng, double facilityLevel, double nationGrowth = 1, double injuryMult = 1)
        {
            var plan = g.training;
            // 급료가 세 번 넘게 밀리면 태업한다
            if (plan.focus == "rest" || g.injury?.severity == "serious" || g.unpaidWeeks >= 3) return null;
            var intensity = T["intensity"][plan.intensity];
            double ig = (double)intensity["growth"];
            if (plan.focus == "doctrine")
            {
                g.doctrine.familiarity = Math.Min(100, g.doctrine.familiarity + (double)T["familiarityPerDay"] * ig * (FmRules.Enabled ? nationGrowth : 1));
            }
            else
            {
                // 교범 숙련은 평소 훈련으로도 조금씩 오른다
                g.doctrine.familiarity = Math.Min(100, g.doctrine.familiarity + 0.3);
                Grow(g, rng, plan.focus, 1, ig * nationGrowth, facilityLevel);
            }
            if (plan.intensity == "extreme") g.morale = Math.Max(0, g.morale - 0.4);
            double injuryChance = (double)intensity["injury"] * (g.traits.Contains("injury_prone") ? 1.5 : 1) * (1 - (facilityLevel - 1) * 0.05) * injuryMult;
            if (g.injury == null && rng.Chance(injuryChance))
            {
                string area = rng.Pick(InjuryAreas);
                g.injury = GladiatorRules.MakeInjury(rng, "minor", area);
                return g.injury.desc;
            }
            return null;
        }
    }
}
