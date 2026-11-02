// 검투사 규칙 (src/core/gladiator.ts, condition.ts performance, injuries.ts 일부, tactics.ts defaultDoctrine)
using System;
using System.Collections.Generic;

namespace Colosseum.Core
{
    public static class GladiatorRules
    {
        /// <summary>특성·부상 보정을 포함한 실제 능력치 (1~20으로 제한)</summary>
        public static int EffectiveAttr(Gladiator g, string a)
        {
            int v = g.attrs.TryGetValue(a, out var x) ? x : 1;
            if (g.injury?.type != null && Data.Injury(g.injury.type)?.penalty is { } pen && pen.TryGetValue(a, out var p)) v += p;
            foreach (var t in g.traits)
                if (Data.Trait(t)?.attrMods is { } m && m.TryGetValue(a, out var d)) v += d;
            return Math.Max(1, Math.Min(20, v));
        }

        /// <summary>전투 감각이 생기기 전 저장의 검투사: 가까운 기존 능력치에서 감각을 만든다 (id로 고정된 흔들림)</summary>
        public static void FillSenseAttrs(Gladiator g)
        {
            var a = g.attrs;
            if (a.ContainsKey("read")) return;
            int h = 0;
            foreach (char ch in g.id) h = unchecked(h * 31 + ch);
            int Jitter()
            {
                // JS: (h * 1103515245 + 12345) | 0 — 곱이 2^53을 넘어 double로 반올림된 뒤 32비트로 자른다
                double p = (double)h * 1103515245.0 + 12345.0;
                h = unchecked((int)(long)p);
                return (int)(((uint)h >> 16) % 5) - 2;
            }
            int Mix(int x, int y) => Math.Max(1, Math.Min(20, JsMath.RoundI((x + y) / 2.0) + Jitter()));
            a["read"] = Mix(a["rea"], a["dec"]);
            a["feint"] = Mix(a["agi"], a["show"]);
            a["genius"] = Mix(a["dec"], 8);
            a["adapt"] = Mix(a["dec"], a["comp"]);
            a["team"] = Mix(a["lead"], a["disc"]);
            a["timing"] = Mix(a["rea"], a["agi"]);
            a["kill"] = Mix(a["agg"], a["grit"]);
            a["aura"] = Mix(a["grit"], a["str"]);
        }

        /// <summary>스타일 가중치로 계산한 현재 능력 CA (1~200)</summary>
        public static int ComputeCA(Gladiator g)
        {
            var style = Data.Style(g.style);
            double wsum = 0, total = 0;
            foreach (var a in Attrs.All)
            {
                double w = 1 + (style.weights.TryGetValue(a, out var sw) ? sw : 0) * 2;
                wsum += w;
                total += g.attrs[a] * w;
            }
            // 주 무기군 숙련을 함께 반영
            foreach (var p in style.profs)
            {
                wsum += 4;
                total += g.prof[p] * 4;
            }
            double avg = total / wsum;
            return (int)Math.Max(1, Math.Min(200, JsMath.Round((avg - 1) / 19 * 200)));
        }

        /// <summary>CA/PA → 별점 (0.5 단위, 0.5~5)</summary>
        public static double Stars(double value) => Math.Max(0.5, Math.Min(5, JsMath.Round(value / 40 * 2) / 2));

        /// <summary>youth | growth | prime | decline</summary>
        public static string AgeStage(Gladiator g)
        {
            var r = Data.Race(g.race).age;
            if (g.age < r.debut) return "youth";
            if (g.age <= r.growthEnd) return "growth";
            if (g.age <= r.primeEnd) return "prime";
            return "decline";
        }

        public static readonly Dictionary<string, string> AgeStageNames = new()
        {
            ["youth"] = "유년기", ["growth"] = "성장기", ["prime"] = "전성기", ["decline"] = "쇠퇴기",
        };

        public static bool CanFight(Gladiator g) => g.alive && g.injury?.severity != "serious" && AgeStage(g) != "youth";

        /// <summary>경기력 배율 (1 = 100%). 사기·미납·경기감각·혹사·피로·부상</summary>
        public static double Performance(Gladiator g)
        {
            double sum = 0;
            foreach (var (_, v) in PerformanceParts(g)) sum += v;
            return Math.Max(0.4, 1 + sum);
        }

        /// <summary>경기력 보정 항목 (웹판 condition.ts performance().parts)</summary>
        public static List<(string label, double value)> PerformanceParts(Gladiator g)
        {
            var parts = new List<(string, double)>();
            double m = g.morale;
            double moraleMod = m >= 90 ? 0.1 : m >= 70 ? 0.05 : m >= 50 ? 0 : m >= 30 ? -0.1 : m >= 10 ? -0.25 : -0.4;
            if (moraleMod != 0) parts.Add(("사기", moraleMod));
            var unpaidPerf = Bal.Morale["unpaidPerf"];
            int n = ((Newtonsoft.Json.Linq.JArray)unpaidPerf).Count;
            double unpaid = (double)unpaidPerf[(int)Math.Min(g.unpaidWeeks, n - 1)];
            if (unpaid != 0) parts.Add(("급료 미납", -unpaid));
            if (g.sharpness < 40) parts.Add(("경기감각", -(40 - g.sharpness) * 0.004));
            else if (g.sharpness >= 85) parts.Add(("경기감각", 0.03));
            if (g.wear > 50) parts.Add(("혹사", -(g.wear - 50) * 0.004));
            if (g.fatigue >= 70) parts.Add(("피로", -0.15));
            if (g.injury?.severity == "light") parts.Add(("부상", -0.15));
            return parts;
        }

        /// <summary>전투 부위 → 부상 부위</summary>
        public static string AreaOf(string hitPart) => hitPart switch
        {
            "head" or "neck" => "head",
            "arm" or "shoulder" => "arm",
            "thigh" or "leg" => "leg",
            _ => "torso",
        };

        /// <summary>부상 하나를 만든다. 단계와 부위에 맞는 종류를 고른다</summary>
        public static Injury MakeInjury(Rng rng, string tier, string area)
        {
            var pool = Data.Injuries.FindAll(i => i.tier == tier && (i.part == area || (area == "head" && i.part == "eye")));
            if (pool.Count == 0) pool = Data.Injuries.FindAll(i => i.tier == tier);
            var def = rng.Pick(pool);
            int days = rng.Int(def.days[0], def.days[1]);
            return new Injury { severity = tier == "minor" ? "light" : "serious", daysLeft = days, total = days, desc = def.name, type = def.id, tier = tier };
        }

        /// <summary>스타일에 맞는 기본 교범</summary>
        public static Doctrine DefaultDoctrine(Gladiator g, double familiarity = 60)
        {
            var order = new List<string>(g.skills.active);
            var b = new Doctrine { order = order, familiarity = familiarity };
            bool Has(string s) => order.Contains(s);
            void Rule(string ct, double v, RuleAction a) => b.rules.Add(new DoctrineRule { cond = new RuleCond { type = ct, value = v }, action = a });
            switch (g.style)
            {
                case "murmillo":
                case "hoplomachus":
                    b.stance = "bulwark"; b.focus = "body";
                    Rule("oppHpBelow", 35, new RuleAction { type = "stance", stance = "assault" });
                    break;
                case "secutor": b.stance = "assault"; b.range = "close"; break;
                case "retiarius":
                    b.stance = "harass"; b.range = "mid"; b.focus = "legs";
                    if (Has("skill.pierce")) Rule("oppDisabled", 0, new RuleAction { type = "skill", skill = "skill.pierce" });
                    break;
                case "thraex": b.focus = "arm"; break;
                case "pankratiast": b.stance = "assault"; b.range = "close"; b.focus = "body"; break;
                case "bestiarius": b.stance = "harass"; b.range = "mid"; break;
                case "crusher":
                    b.stance = "assault"; b.focus = "head";
                    if (Has("skill.execute")) Rule("oppHpBelow", 25, new RuleAction { type = "skill", skill = "skill.execute" });
                    break;
                case "runeblade": b.stance = "blitz"; break;
                case "sorcerer": b.stance = "harass"; b.range = "far"; break;
            }
            Rule("staBelow", 20, new RuleAction { type = "rest" });
            return b;
        }
    }
}
