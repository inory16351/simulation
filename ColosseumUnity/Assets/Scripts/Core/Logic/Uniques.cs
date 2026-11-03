// 고유 장비 (Unity판 전용): 명품·전설 등급 장비에 이름과 효과가 붙는다 (Resources/Data/uniques.json).
// 장비 id는 'gladius+3~senate_edict'처럼 등급 뒤에 '~고유 id'. 성능은 등급대로 오르고, 효과는 전투(Sim)가 더한다.
// 팔·다리 칸 장비도 여기서 켜고 끈다: 웹판과 결과를 비교하는 골든 테스트만 Enabled를 끈다.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Colosseum.Core
{
    public class UniqueEffect
    {
        /// <summary>hit crit pierce dmg bleed stun block evade def crowd aura regen fury thorns sta first</summary>
        public string t;
        public double v;
    }

    public class UniqueDef
    {
        public string id, @base, name, lore;
        /// <summary>2 명품, 3 전설</summary>
        public int grade;
        public List<UniqueEffect> effects = new();
    }

    class UniquesFile { public List<UniqueDef> uniques; }

    public static class Uniques
    {
        /// <summary>고유 장비·팔다리 칸을 쓰는가. 골든 테스트만 끈다</summary>
        public static bool Enabled = true;

        public static List<UniqueDef> All = new();
        static Dictionary<string, UniqueDef> idx = new();

        public static void Load(List<UniqueDef> list)
        {
            All = list ?? new List<UniqueDef>();
            idx = All.ToDictionary(u => u.id);
        }

        public static UniqueDef Get(string id) => id != null && idx.TryGetValue(id, out var u) ? u : null;

        /// <summary>장비 id의 고유 정의. 없으면 null</summary>
        public static UniqueDef Of(string itemId) => Get(Data.UniqueOf(itemId));

        /// <summary>팔·다리 칸 장비인가 (Enabled가 꺼지면 시장·전리품에 나오지 않는다)</summary>
        public static bool IsLimb(string slot) => slot == "arms" || slot == "legs";

        /// <summary>시장·전리품에 나올 수 있는 장비인가</summary>
        public static bool InPool(string baseId)
        {
            if (Enabled) return true;
            var a = Data.Armor(baseId);
            return a == null || !IsLimb(a.slot);
        }

        /// <summary>장비 종류 묶음 (같은 묶음의 전설로 대신할 때): 무기 계열 또는 방어구 칸</summary>
        static string Family(string baseId) => Data.Weapon(baseId)?.@class ?? Data.Armor(baseId)?.slot;

        /// <summary>명품·전설로 굴린 장비에 고유 이름을 붙인다. 그 장비의 고유가 없으면 같은 계열에서 고르고, 그마저 없으면 그대로</summary>
        public static string Assign(Rng rng, string itemId)
        {
            if (!Enabled || Data.UniqueOf(itemId) != null) return itemId;
            var (b, q) = Data.SplitItemId(itemId);
            if (q < 2) return itemId;
            var own = All.Where(u => u.grade == q && u.@base == b).ToList();
            if (own.Count == 0)
            {
                string fam = Family(b);
                own = All.Where(u => u.grade == q && Family(u.@base) == fam).ToList();
            }
            if (own.Count == 0) return itemId;
            var u = own.Count == 1 ? own[0] : rng.Pick(own);
            return $"{u.@base}+{q}~{u.id}";
        }

        /// <summary>여러 장비의 효과 합</summary>
        public static Dictionary<string, double> Sum(IEnumerable<string> itemIds)
        {
            var d = new Dictionary<string, double>();
            foreach (var id in itemIds)
            {
                var u = Of(id);
                if (u == null) continue;
                foreach (var e in u.effects) d[e.t] = (d.TryGetValue(e.t, out var v) ? v : 0) + e.v;
            }
            return d;
        }

        /// <summary>효과 한 줄 (화면용)</summary>
        public static string Describe(UniqueEffect e)
        {
            string P(double x) => $"{StateUtil.Num(JsMath.Round(x * 100))}%";
            string N(double x) => StateUtil.Num(x);
            return e.t switch
            {
                "hit" => $"명중 +{P(e.v)}",
                "crit" => $"치명타 확률 +{P(e.v)}",
                "pierce" => $"관통 +{P(e.v)}",
                "dmg" => $"주는 피해 +{P(e.v)}",
                "bleed" => $"맞히면 {P(e.v)} 확률로 출혈",
                "stun" => $"맞히면 {P(e.v)} 확률로 기절",
                "block" => $"막기 +{P(e.v)}",
                "evade" => $"상대 명중 -{P(e.v)}",
                "def" => $"방어 +{N(e.v)}",
                "crowd" => $"큰 타격마다 관중 열기 +{N(e.v)}",
                "aura" => $"위압감 +{N(e.v)}",
                "regen" => $"라운드마다 체력 {P(e.v)} 회복",
                "fury" => $"체력 30% 아래에서 피해 +{P(e.v)}",
                "thorns" => $"근접으로 맞으면 받은 피해의 {P(e.v)}를 되갚음",
                "sta" => $"기력 소모 -{P(e.v)}",
                "first" => $"선공 +{N(e.v)}",
                _ => e.t,
            };
        }
    }
}
