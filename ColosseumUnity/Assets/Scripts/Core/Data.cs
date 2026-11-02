// 게임 데이터 정의와 로더 (src/core/data.ts). JSON은 Assets/Resources/Data에 있고 웹판 data/와 같은 파일이다.
// 필드 이름은 JSON 키와 같게 소문자로 둔다 (Newtonsoft가 그대로 읽는다).
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Colosseum.Core
{
    public class AgeDef { public int debut, growthEnd, primeEnd, retire; }
    public class NameDesc { public string name, desc; }

    public class RaceDef
    {
        public string id, name, color, raceSkill;
        public Dictionary<string, int> attrMods = new();
        public AgeDef age;
        public double growthMult, wageMult, upkeepMult;
        public NameDesc racialTrait;
    }

    public class NationDef
    {
        public string id, name, @short, motif, color, capital, arena, league, lore, culture;
        public Dictionary<string, double> races = new();
        public List<string> styles = new();
        public JObject rules;
        public List<string> ruleText = new();
        public Dictionary<string, double> relations = new();
    }

    public class Loadout { public string main, off, armor, helmet; }

    public class StyleDef
    {
        public string id, name, desc;
        public Dictionary<string, double> weights = new();
        public Loadout loadout;
        public List<string> profs = new();
        public int preferredRange;
        public double aggression;
        public List<string> counters = new();
        public List<string> skills = new();
    }

    public class WeaponDef
    {
        public string id, name, @class;
        public double damage, speed, pierce, range, reqStr, crit;
        public double? blockBypass, stun, boundBonus, ammo, magicBonus;
        public bool twoHanded, offhandOnly;
        public WeaponDef Clone() => (WeaponDef)MemberwiseClone();
    }

    public class ArmorDef
    {
        public string id, name, slot, weight;
        public double? defense, agiPenalty, staCost, crowd, accPenalty, netResist, showBonus, block;
        public ArmorDef Clone() => (ArmorDef)MemberwiseClone();
    }

    public class StatusApply { public string status; public int turns; public double chance; }
    public class SelfStatus { public string status; public int turns; }

    public class SkillDef
    {
        public string id, name, kind, race;
        public double? cost, mana, cd, mult, hits, hitMod, pierceAdd, critAdd, requireTargetHpBelow, magicDamage, crowd, enemyMomentum, selfMomentum, restoreSta;
        public bool requireShield, requireNet, ranged, useAmmo, useGrapple, closeIn, fromAnyRange, autoHit, magic;
        public StatusApply apply;
        public SelfStatus self;
        public Dictionary<string, int> req;
    }

    public class TraitDef
    {
        public string id, name, cat, desc;
        public Dictionary<string, int> attrMods;
        public double spawn;
        public bool hidden, negative;
    }

    public class InjuryPerm { public Dictionary<string, int> attrs; public string trait; }

    public class InjuryDef
    {
        public string id, name, part, tier;
        public int[] days;
        public Dictionary<string, int> penalty = new();
        public double permanent;
        public InjuryPerm perm;
    }

    public class NamesDef
    {
        public Dictionary<string, List<string>> gladiator, surname;
        public List<string> female, epithet, ludusNoun, ludusAdj, ludusSuffix, ludusSuffixAfterAdj;
    }

    class ItemsFile { public List<WeaponDef> weapons; public List<ArmorDef> armors; }

    public static class Data
    {
        public static List<RaceDef> Races;
        public static List<NationDef> Nations;
        public static List<StyleDef> Styles;
        public static List<WeaponDef> Weapons;
        public static List<ArmorDef> Armors;
        public static List<SkillDef> Skills;
        public static List<TraitDef> Traits;
        public static List<InjuryDef> Injuries;
        public static NamesDef Names;
        /// <summary>랜덤 이벤트 정의 (data/events.json). 타입과 효과 처리는 Logic/Events.cs</summary>
        public static List<EventDef> Events;
        /// <summary>balance.json 전체. 자주 쓰는 값은 Bal에 숫자로 꺼내 둔다</summary>
        public static JObject Balance;

        static Dictionary<string, RaceDef> raceIdx;
        static Dictionary<string, NationDef> nationIdx;
        static Dictionary<string, StyleDef> styleIdx;
        static Dictionary<string, WeaponDef> weaponIdx;
        static Dictionary<string, ArmorDef> armorIdx;
        static Dictionary<string, SkillDef> skillIdx;
        static Dictionary<string, TraitDef> traitIdx;
        static Dictionary<string, InjuryDef> injuryIdx;
        static readonly Dictionary<string, object> qualityCache = new();
        static bool loaded;

        /// <summary>Resources에서 읽는다. 테스트는 LoadFrom으로 문자열을 넣을 수 있다</summary>
        public static void Ensure()
        {
            if (loaded) return;
            LoadFrom(name => Resources.Load<TextAsset>("Data/" + name).text);
        }

        public static void LoadFrom(Func<string, string> read)
        {
            T Read<T>(string n) => JsonConvert.DeserializeObject<T>(read(n));
            Races = Read<List<RaceDef>>("races");
            Nations = Read<List<NationDef>>("nations");
            Styles = Read<List<StyleDef>>("styles");
            var items = Read<ItemsFile>("items");
            Weapons = items.weapons;
            Armors = items.armors;
            Skills = Read<List<SkillDef>>("skills");
            Traits = Read<List<TraitDef>>("traits");
            Injuries = Read<List<InjuryDef>>("injuries");
            Names = Read<NamesDef>("names");
            Events = Read<List<EventDef>>("events");
            Balance = JObject.Parse(read("balance"));
            raceIdx = Index(Races, x => x.id);
            nationIdx = Index(Nations, x => x.id);
            styleIdx = Index(Styles, x => x.id);
            weaponIdx = Index(Weapons, x => x.id);
            armorIdx = Index(Armors, x => x.id);
            skillIdx = Index(Skills, x => x.id);
            traitIdx = Index(Traits, x => x.id);
            injuryIdx = Index(Injuries, x => x.id);
            qualityCache.Clear();
            Bal.Load(Balance);
            loaded = true;
        }

        static Dictionary<string, T> Index<T>(List<T> l, Func<T, string> key)
        {
            var d = new Dictionary<string, T>();
            foreach (var x in l) d[key(x)] = x;
            return d;
        }

        public static RaceDef Race(string id) => raceIdx[id];
        public static NationDef Nation(string id) => nationIdx[id];
        public static StyleDef Style(string id) => styleIdx.TryGetValue(id, out var s) ? s : null;
        public static SkillDef Skill(string id) => id != null && skillIdx.TryGetValue(id, out var s) ? s : null;
        public static TraitDef Trait(string id) => traitIdx.TryGetValue(id, out var s) ? s : null;
        public static InjuryDef Injury(string id) => id != null && injuryIdx.TryGetValue(id, out var s) ? s : null;

        /// <summary>장비 품질: 'gladius+2'처럼 id 뒤에 붙는다. 0 보통, 1 정교한, 2 명품, 3 전설의</summary>
        public static readonly string[] QualityNames = { "", "정교한", "명품", "전설의" };

        public static (string, int) SplitItemId(string id)
        {
            int i = id.IndexOf('+');
            if (i < 0) return (id, 0);
            int.TryParse(id.Substring(i + 1), out int q);
            return (id.Substring(0, i), Math.Max(0, Math.Min(3, q)));
        }

        public static WeaponDef Weapon(string id)
        {
            if (id == null) return null;
            var (b, q) = SplitItemId(id);
            if (!weaponIdx.TryGetValue(b, out var w)) return null;
            if (q == 0) return w;
            if (qualityCache.TryGetValue(id, out var c)) return (WeaponDef)c;
            var d = w.Clone();
            d.damage = JsMath.Round(w.damage * (1 + 0.12 * q));
            d.crit = w.crit + 0.03 * q;
            if (w.magicBonus.HasValue && w.magicBonus.Value != 0) d.magicBonus = w.magicBonus + 0.05 * q;
            d.id = id;
            d.name = $"{QualityNames[q]} {w.name}";
            qualityCache[id] = d;
            return d;
        }

        public static ArmorDef Armor(string id)
        {
            if (id == null) return null;
            var (b, q) = SplitItemId(id);
            if (!armorIdx.TryGetValue(b, out var a)) return null;
            if (q == 0) return a;
            if (qualityCache.TryGetValue(id, out var c)) return (ArmorDef)c;
            var d = a.Clone();
            if (a.defense.HasValue) d.defense = a.defense + (a.slot == "helmet" ? Math.Ceiling(q / 2.0) : q);
            if (a.block.HasValue) d.block = a.block + 0.04 * q;
            // 좋은 갑옷일수록 몸에 잘 맞아 덜 무겁다
            if (a.staCost.HasValue) d.staCost = Math.Max(0, a.staCost.Value - 0.03 * q);
            d.id = id;
            d.name = $"{QualityNames[q]} {a.name}";
            qualityCache[id] = d;
            return d;
        }
    }

    /// <summary>JS Math와 같은 반올림 (C# Math.Round는 은행가 반올림이라 다르다)</summary>
    public static class JsMath
    {
        public static double Round(double x) => Math.Floor(x + 0.5);
        public static int RoundI(double x) => (int)Math.Floor(x + 0.5);
        public static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));
    }

    /// <summary>balance.json에서 전투·사망에 쓰는 숫자</summary>
    public static class Bal
    {
        public static JObject Combat, Death, Rules, Training, Economy, Morale, League, Unrest, Events;
        public static double C(string k) => (double)Combat[k];
        public static double D(string k) => (double)Death[k];
        public static JObject Rule(string rule) => (JObject)Rules[rule];

        public static void Load(JObject b)
        {
            Combat = (JObject)b["combat"];
            Death = (JObject)b["death"];
            Rules = (JObject)b["rules"];
            Training = (JObject)b["training"];
            Economy = (JObject)b["economy"];
            Morale = (JObject)b["morale"];
            League = (JObject)b["league"];
            Unrest = (JObject)b["unrest"];
            Events = (JObject)b["events"];
        }
    }
}
