// 부상 (src/core/injuries.ts). areaOf/makeInjury는 GladiatorRules에 있고 여기서는 넘겨 부른다.
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;

namespace Colosseum.Core
{
    public static class Injuries
    {
        /// <summary>TS INJURIES (클래스 이름과 겹쳐 Const를 붙였다)</summary>
        public static List<InjuryDef> InjuriesConst => Data.Injuries;
        public static InjuryDef GetInjury(string id) => Data.Injury(id);

        public static readonly Dictionary<string, string> TierNames = new() { ["minor"] = "경상", ["moderate"] = "중상", ["severe"] = "치명상" };
        public static readonly Dictionary<string, string> PartNames = new() { ["head"] = "머리", ["eye"] = "눈", ["arm"] = "팔", ["leg"] = "다리", ["torso"] = "몸통" };

        /// <summary>전투 부위 → 부상 부위</summary>
        public static string AreaOf(string hitPart) => GladiatorRules.AreaOf(hitPart);

        /// <summary>부상 하나를 만든다</summary>
        public static Injury MakeInjury(Rng rng, string tier, string area) => GladiatorRules.MakeInjury(rng, tier, area);

        /// <summary>부상 중 능력치 저하</summary>
        public static int InjuryPenalty(Gladiator g, string a)
        {
            if (g.injury == null) return 0;
            var def = GetInjury(g.injury.type);
            return def != null && def.penalty != null && def.penalty.TryGetValue(a, out var p) ? p : 0;
        }

        /// <summary>다친 채로 출전하면 악화될 수 있다</summary>
        public static void Aggravate(GameState state, Gladiator g, Rng rng)
        {
            var inj = g.injury;
            if (inj == null || !rng.Chance(0.3)) return;
            inj.daysLeft = JsMath.Round(inj.daysLeft * 1.6 + 3);
            inj.total = Math.Max(inj.total ?? 0, inj.daysLeft);
            Career.LogCareer(state, g, $"부상({inj.desc})을 안고 싸우다 상처가 덧났습니다", "injury");
        }

        /// <summary>부상이 다 나을 때: 영구 손상 판정. 의무실 단계와 메디쿠스 실력이 확률을 낮춘다.</summary>
        public static void OnHealed(GameState state, Gladiator g, Rng rng, double medicalLevel, double medicusRating, bool mine)
        {
            var def = GetInjury(g.injury?.type);
            if (def == null || def.permanent == 0 || def.perm == null) return;
            double chance = def.permanent == 1 ? 1 : def.permanent * (1 - (medicalLevel - 1) * 0.08) * (1 - medicusRating * 0.025) * (g.race == "dwarf" ? 0.6 : 1);
            if (!rng.Chance(Math.Max(0, chance))) return;
            var parts = new List<string>();
            if (def.perm.attrs != null)
            {
                foreach (var kv in def.perm.attrs)
                {
                    string k = kv.Key;
                    int v = kv.Value;
                    g.attrs[k] = Math.Max(1, g.attrs[k] + v);
                    parts.Add($"{Attrs.Names[k]} {StateUtil.Num(v)}");
                }
            }
            if (!string.IsNullOrEmpty(def.perm.trait) && !g.traits.Contains(def.perm.trait))
            {
                g.traits.Add(def.perm.trait);
                if (g.traits.Count > 6) g.traits.RemoveAt(0);
                parts.Add(Data.Trait(def.perm.trait)?.name ?? def.perm.trait);
            }
            Career.SeasonRecord(state, g);
            Career.LogCareer(state, g, $"{def.name}의 후유증이 남았습니다 ({string.Join(", ", parts)})", "injury");
            if (mine)
            {
                string tail = !string.IsNullOrEmpty(def.perm.trait) ? $"새 특성: 「{Data.Trait(def.perm.trait)?.name ?? "undefined"}」" : "능력치가 영구히 떨어졌습니다.";
                StateUtil.PushMessage(state, "warning", $"{g.name}: 부상 후유증", $"{g.name}의 부상({def.name})은 나았지만 후유증이 남았습니다.\n{tail}");
            }
        }

        /// <summary>영구 손상 위험 (UI 표시용)</summary>
        public static double PermanentRisk(Gladiator g) => GetInjury(g.injury?.type)?.permanent ?? 0;
    }
}
