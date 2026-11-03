// 한 합의 수 싸움: 방향(상·중·하) × 동작 가위바위보, 버릇, 예측, 페인트 (src/core/combat/exchange.ts)
using System;
using System.Collections.Generic;
using System.Linq;

namespace Colosseum.Core.Combat
{
    public class Habits { public double[] atk, def; }

    /// <summary>한쪽이 상대에 대해 쌓는 관측 (이번 경기)</summary>
    public class ReadBook
    {
        public double[] atk = new double[9];
        public double[] guard = new double[3];
    }

    public class Mind
    {
        public double read, feint, genius, adapt, dec, rea, comp;
        /// <summary>이번 경기 컨디션 노이즈 (일관성)</summary>
        public double form;
        /// <summary>적응력: 경기가 길어질수록 더 정확히 읽는다</summary>
        public double late;
    }

    public class AttackPlan
    {
        /// <summary>0 상단, 1 중단, 2 하단, 3 사선</summary>
        public int dir;
        /// <summary>0 베기, 1 찌르기, 2 내려치기</summary>
        public int motion;
        /// <summary>null | "ok" | "fail"</summary>
        public string feint;
        public bool improv;
        public int? shown;
    }

    public class GuardPlan { public int dir, guard; }

    public static class Exchange
    {
        public static readonly string[] DirNames = { "상단", "중단", "하단", "사선" };
        public static readonly string[] MotionNames = { "베기", "찌르기", "내려치기" };
        public static readonly string[] GuardNames = { "막기", "흘리기", "피하기" };

        /// <summary>동작 상성: +1 공격 유리, -1 방어 유리. 막기 > 베기 > 피하기 > 내려치기 > 흘리기 > 찌르기 > 막기</summary>
        public static readonly int[,] Adv = { { -1, 0, 1 }, { 1, -1, 0 }, { 0, 1, -1 } };
        public static int CounterGuard(int m) => m;
        public static int BeatGuard(int g) => (g + 1) % 3;

        public static readonly Dictionary<string, double[]> MotionAffinity = new()
        {
            ["sword"] = new[] { 1.2, 1.0, 0.5 },
            ["greatsword"] = new[] { 1.2, 0.4, 1.1 },
            ["axe"] = new[] { 0.6, 0.2, 1.4 },
            ["mace"] = new[] { 0.4, 0.3, 1.5 },
            ["spear"] = new[] { 0.3, 1.6, 0.4 },
            ["trident_net"] = new[] { 0.2, 1.6, 0.3 },
            ["dagger"] = new[] { 0.6, 1.5, 0.2 },
            ["throwing"] = new[] { 0.5, 1.2, 0.3 },
            ["shield"] = new[] { 0.3, 1.0, 1.0 },
            ["unarmed"] = new[] { 0.3, 1.3, 0.7 },
            ["focus"] = new[] { 0.6, 0.8, 0.8 },
        };

        public static double AffinityHit(string w, int m) => (MotionAffinity[w][m] - 1) * 0.08;

        /// <summary>지대 상성과 높이 지시 (Unity판). 끄면 웹판과 같은 수 싸움, 난수 순서도 같다 (골든 테스트)</summary>
        public static bool Zones = true;

        /// <summary>무기 계열별 지대 상성: atk/def[상단, 중단, 하단]. 1보다 크면 강함. 방패를 들면 방어는 shield 프로필</summary>
        public static readonly Dictionary<string, (double[] atk, double[] def)> ZoneProfiles = new()
        {
            ["sword"] = (new[] { 1.1, 1.0, 0.9 }, new[] { 1.15, 1.0, 0.85 }),
            ["greatsword"] = (new[] { 1.2, 0.9, 1.1 }, new[] { 1.1, 0.95, 0.8 }),
            ["axe"] = (new[] { 1.25, 0.95, 0.8 }, new[] { 0.85, 1.05, 0.9 }),
            ["mace"] = (new[] { 1.25, 1.0, 0.75 }, new[] { 0.9, 1.05, 0.85 }),
            ["spear"] = (new[] { 0.85, 1.25, 1.0 }, new[] { 0.85, 1.0, 1.2 }),
            ["trident_net"] = (new[] { 0.8, 1.05, 1.25 }, new[] { 0.9, 1.0, 1.15 }),
            ["dagger"] = (new[] { 0.85, 1.2, 1.1 }, new[] { 0.8, 1.0, 1.05 }),
            ["throwing"] = (new[] { 1.0, 1.0, 1.0 }, new[] { 0.9, 0.95, 0.9 }),
            ["shield"] = (new[] { 0.9, 1.1, 0.9 }, new[] { 1.15, 1.25, 0.75 }),
            ["unarmed"] = (new[] { 1.0, 1.2, 0.9 }, new[] { 1.05, 0.9, 0.95 }),
            ["focus"] = (new[] { 1.0, 1.0, 1.0 }, new[] { 0.9, 0.9, 0.9 }),
        };

        public static readonly string[] ZoneIds = { "high", "mid", "low" };
        public static readonly Dictionary<string, string> ZoneNames = new() { ["auto"] = "자동", ["high"] = "상단", ["mid"] = "중단", ["low"] = "하단" };

        public static double[] AtkZones(string weapon) => ZoneProfiles.TryGetValue(weapon, out var p) ? p.atk : new[] { 1.0, 1, 1 };
        public static double[] DefZones(string weapon, bool shield) => shield ? ZoneProfiles["shield"].def : ZoneProfiles.TryGetValue(weapon, out var p) ? p.def : new[] { 1.0, 1, 1 };

        /// <summary>맞붙은 방향의 공격 강함 − 방어 강함 → 명중 보정</summary>
        public static double ZoneHit(double[] atk, double[] def, int dir) => dir is < 0 or > 2 ? 0 : (atk[dir] - def[dir]) * 0.5;

        /// <summary>
        /// 공격 높이 지시 → 방향 가중치. auto는 상대의 약한 방어 지대를, 판단력이 높을수록 강하게 노린다.
        /// high/mid/low는 그 높이를 두 배로.
        /// </summary>
        public static double[] AttackBias(string zone, double[] oppDef, double dec)
        {
            int z = Array.IndexOf(ZoneIds, zone);
            if (z >= 0) return Enumerable.Range(0, 3).Select(d => d == z ? 2.0 : 1.0).ToArray();
            double k = 2 + dec * 0.15;
            return oppDef.Select(v => Math.Max(0.3, 1 + (1 - v) * k)).ToArray();
        }

        /// <summary>방어 높이 지시 → 방향 가중치. auto는 상대 무기가 강한 지대를 중점으로</summary>
        public static double[] GuardBias(string zone, double[] oppAtk, double dec)
        {
            int z = Array.IndexOf(ZoneIds, zone);
            if (z >= 0) return Enumerable.Range(0, 3).Select(d => d == z ? 2.0 : 1.0).ToArray();
            double k = 2 + dec * 0.15;
            return oppAtk.Select(v => Math.Max(0.3, 1 + (v - 1) * k)).ToArray();
        }

        /// <summary>툴팁 힌트: "상단 공격이 강하고 하단 방어가 약합니다" 식으로 1.1 이상·0.9 이하만</summary>
        public static string ZoneHint(string weapon, bool shield)
        {
            var a = AtkZones(weapon);
            var d = DefZones(weapon, shield);
            var parts = new List<string>();
            string Names(double[] v, Func<double, bool> f) => string.Join("·", Enumerable.Range(0, 3).Where(i => f(v[i])).Select(i => ZoneNames[ZoneIds[i]]));
            string strongA = Names(a, v => v >= 1.1), weakA = Names(a, v => v <= 0.9), strongD = Names(d, v => v >= 1.1), weakD = Names(d, v => v <= 0.9);
            if (strongA != "") parts.Add($"{strongA} 공격이 강합니다");
            if (weakA != "") parts.Add($"{weakA} 공격은 잘 막힙니다");
            if (strongD != "") parts.Add($"{strongD} 방어가 단단합니다");
            if (weakD != "") parts.Add($"{weakD}{(shield ? "(방패 아래)" : "")}가 빈틈입니다");
            return parts.Count > 0 ? string.Join(". ", parts) + "." : "어느 높이든 고르게 다룹니다.";
        }

        public static readonly Dictionary<string, int> SkillMotion = new()
        {
            ["skill.power_strike"] = 2, ["skill.flurry"] = 0, ["skill.armor_break"] = 2, ["skill.pierce"] = 1, ["skill.execute"] = 2,
            ["skill.shield_bash"] = 1, ["skill.charge"] = 1, ["skill.horn_charge"] = 1, ["skill.venom_bite"] = 1, ["skill.pounce"] = 2,
        };

        public static int Cell(int d, int m) => d * 3 + m;

        static readonly Dictionary<string, double[]> FocusDir = new()
        {
            ["spread"] = new[] { 1.0, 1, 1 }, ["head"] = new[] { 2, 0.7, 0.5 }, ["body"] = new[] { 0.7, 2, 0.7 },
            ["legs"] = new[] { 0.5, 0.7, 2 }, ["arm"] = new[] { 1.4, 1.4, 0.5 },
        };

        static int Imul(int a, int b) => unchecked(a * b);

        /// <summary>id로 고정된 0~1 난수열 (TS seeded와 같다)</summary>
        static Func<double> Seeded(string id, int salt)
        {
            int h = salt;
            foreach (var ch in id) h = unchecked(Imul(h, 31) + ch);
            return () =>
            {
                h = unchecked(Imul(h, 1103515245) + 12345);
                return ((int)((uint)h >> 8) & 0xffff) / 65536.0;
            };
        }

        static double[] Normalize(double[] g)
        {
            double s = 0;
            foreach (var v in g) s += v;
            if (s == 0) s = 1;
            return g.Select(v => v / s).ToArray();
        }

        public static Habits HabitsOf(Gladiator g, string weapon, string focus, bool shield, double armorAgiPenalty, double agg, double genius, double blk, double eva)
        {
            var r = Seeded(g.id, 7);
            var aff = MotionAffinity[weapon];
            var fdir = FocusDir[focus];
            // 공격성은 버릇을 뾰족하게, 천재성은 레퍼토리를 넓혀 고르게 만든다
            double peak = Math.Max(0.4, 1.6 + (agg - 10) * 0.1 - (genius - 10) * 0.06);
            var atk = new List<double>();
            for (int d = 0; d < 3; d++)
                for (int m = 0; m < 3; m++) atk.Add(Math.Pow(fdir[d] * aff[m] * (0.4 + r() * 1.2), peak));
            double[] guardW =
            {
                (shield ? 1.6 : 0.6) * (0.6 + blk / 20),
                weapon == "sword" || weapon == "dagger" || weapon == "spear" ? 1.2 : 0.9,
                Math.Max(0.2, 0.9 + (armorAgiPenalty == 0 ? 0.3 : -0.15 * armorAgiPenalty)) * (0.6 + eva / 20),
            };
            double d0 = 0.8 + r() * 0.6, d1 = 0.8 + r() * 0.6, d2 = 0.8 + r() * 0.6;
            double[] ddir = { d0, d1, d2 };
            var def = new List<double>();
            for (int d = 0; d < 3; d++)
                for (int k = 0; k < 3; k++) def.Add(ddir[d] * guardW[k] * (0.6 + r() * 0.8));
            return new Habits { atk = Normalize(atk.ToArray()), def = Normalize(def.ToArray()) };
        }

        static int PickWeighted(Rng rng, IList<double> w)
        {
            double s = 0;
            foreach (var v in w) s += v;
            double x = rng.Next() * s;
            for (int i = 0; i < w.Count; i++) { x -= w[i]; if (x <= 0) return i; }
            return w.Count - 1;
        }

        static double DirSum(double[] g, int d) => g[Cell(d, 0)] + g[Cell(d, 1)] + g[Cell(d, 2)];

        public static AttackPlan PlanAttack(Rng rng, Mind me, Habits habits, ReadBook shownBook, ReadBook myBook, string weapon, int? fixedMotion, double oppRead, double feintBias, double[] dirBias = null)
        {
            // 높이 지시: 버릇의 방향 가중치에 곱한다 (뽑기 한 번은 그대로)
            int c = PickWeighted(rng, dirBias == null ? habits.atk : habits.atk.Select((v, i) => v * dirBias[i / 3]).ToArray());
            int dir = c / 3;
            int motion = fixedMotion ?? c % 3;
            bool improv = false;

            if (rng.Chance(me.genius * 0.005))
            {
                // 즉흥: 상대가 거의 못 본 칸
                improv = true;
                var seen = shownBook.atk.Select(v => v + rng.Next() * 0.5).ToArray();
                c = Array.IndexOf(seen, seen.Min());
                dir = c / 3;
                if (fixedMotion == null) motion = c % 3;
                if (me.genius >= 18 && rng.Chance(0.3)) dir = 3;
            }
            else if (rng.Chance(Math.Max(0.05, Math.Min(0.5, 0.15 + (me.dec - oppRead) * 0.02 + me.feint * 0.01))))
            {
                // 판단: 많이 보여 준 방향은 막힐 테니 다른 방향으로, 상대가 즐겨 쓰는 방어를 이기는 동작으로
                double[] shownDir = { DirSum(shownBook.atk, 0), DirSum(shownBook.atk, 1), DirSum(shownBook.atk, 2) };
                double total = shownDir[0] + shownDir[1] + shownDir[2];
                int top = Array.IndexOf(shownDir, shownDir.Max());
                if (total >= 2 && dir == top)
                {
                    var others = new[] { 0, 1, 2 }.Where(d => d != top).ToArray();
                    dir = others[PickWeighted(rng, others.Select(d => shownDir[d] + 0.5).ToArray())];
                }
                double gTotal = myBook.guard[0] + myBook.guard[1] + myBook.guard[2];
                if (fixedMotion == null && gTotal >= 2)
                {
                    int fav = BeatGuard(Array.IndexOf(myBook.guard, myBook.guard.Max()));
                    if (MotionAffinity[weapon][fav] >= 0.5) motion = fav;
                }
            }

            string feint = null;
            int? shown = null;
            if (dir != 3 && rng.Chance(me.feint * 0.012 + feintBias))
            {
                double p = Math.Max(0.1, Math.Min(0.9, 0.45 + (me.feint - oppRead) * 0.04 + (me.genius >= 10 ? 0.08 : 0)));
                feint = rng.Chance(p) ? "ok" : "fail";
                if (feint == "ok")
                {
                    var others = new[] { 0, 1, 2 }.Where(d => d != dir).ToArray();
                    shown = others[rng.Int(0, 1)];
                }
            }
            return new AttackPlan { dir = dir, motion = motion, feint = feint, improv = improv, shown = shown };
        }

        public static GuardPlan PlanGuard(Rng rng, Mind me, Habits habits, ReadBook book, double oppFeint, double pressure, int? fake, double[] dirBias = null)
        {
            // 속았다: 보여 준 방향을 막는다
            if (fake != null)
            {
                int f = fake.Value;
                int k = PickWeighted(rng, new[] { habits.def[Cell(f, 0)], habits.def[Cell(f, 1)], habits.def[Cell(f, 2)] });
                return new GuardPlan { dir = f, guard = k };
            }
            double beta = Math.Max(0.2, Math.Min(6, 1.2 + (me.read - oppFeint) * 0.12 + me.read * 0.04 + me.form + me.late - pressure));
            var est = book.atk.Select(v => v + 1).ToArray();
            double[] dirW = { DirSum(est, 0), DirSum(est, 1), DirSum(est, 2) };
            double[] habitDir = { DirSum(habits.def, 0), DirSum(habits.def, 1), DirSum(habits.def, 2) };
            if (dirBias != null)
            {
                for (int d = 0; d < 3; d++) { dirW[d] *= dirBias[d]; habitDir[d] *= dirBias[d]; }
            }
            // 판단력이 낮으면 읽은 것보다 제 버릇대로 막는다
            bool useRead = rng.Chance(Math.Min(1, 0.35 + me.dec * 0.035 + me.read * 0.01));
            int dir = useRead ? PickWeighted(rng, dirW.Select(v => Math.Pow(v, beta)).ToArray()) : PickWeighted(rng, habitDir);
            int guard;
            if (useRead && rng.Chance(Math.Min(0.95, 0.35 + me.dec * 0.03 + me.read * 0.01)))
            {
                var motionW = new[] { Math.Pow(est[Cell(dir, 0)], beta), Math.Pow(est[Cell(dir, 1)], beta), Math.Pow(est[Cell(dir, 2)], beta) };
                guard = CounterGuard(PickWeighted(rng, motionW));
            }
            else guard = PickWeighted(rng, new[] { habits.def[Cell(dir, 0)], habits.def[Cell(dir, 1)], habits.def[Cell(dir, 2)] });
            return new GuardPlan { dir = dir, guard = guard };
        }

        /// <summary>clean | late | parry | neutral | break</summary>
        public static string OutcomeOf(Rng rng, AttackPlan a, GuardPlan g, double defRea)
        {
            if (a.dir == 3 || a.dir != g.dir) return rng.Chance(defRea * 0.01) ? "late" : "clean";
            int adv = Adv[a.motion, g.guard];
            return adv < 0 ? "parry" : adv > 0 ? "break" : "neutral";
        }

        /// <summary>관측 가중치: 적응력이 높을수록 한 번 본 것을 크게 기억한다</summary>
        public static double LearnWeight(double adapt) => 0.4 + adapt * 0.12;
    }
}
