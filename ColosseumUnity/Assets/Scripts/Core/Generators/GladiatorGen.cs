// 검투사 생성 (src/core/generators/gladiator.ts). 난수 소비 순서를 TS와 똑같이 맞춘다.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Colosseum.Core
{
    public class GenOptions
    {
        public string id, nation, race, style, status;
        /// <summary>목표 평균 능력치 (1~20). CA 대략 (target-1)/19*200</summary>
        public double target;
        public double? age;
        public int? paCap;
        public HashSet<string> usedNames;
    }

    public static class GladiatorGen
    {
        static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));

        static string UniqueName(Rng rng, string raceId, string gender, HashSet<string> used)
        {
            if (NameBook.Ready) return NameBook.PersonName(rng, raceId, gender, used);
            var female = new HashSet<string>(Data.Names.female);
            var pool = Data.Names.gladiator[raceId].Where(n => female.Contains(n) == (gender == "f")).ToList();
            string name = rng.Pick(pool);
            for (int i = 0; used != null && used.Contains(name) && i < 30; i++)
                name = $"{rng.Pick(pool)} {rng.Pick(Data.Names.surname[raceId])}";
            // 이름이 바닥나면 남은 조합을 훑고, 그래도 없으면 몇 세를 붙인다
            if (used != null && used.Contains(name))
            {
                var combos = pool.SelectMany(p => Data.Names.surname[raceId].Select(sn => $"{p} {sn}")).Where(n => !used.Contains(n)).ToList();
                if (combos.Count > 0) name = rng.Pick(combos);
                else for (int k = 2; used.Contains(name); k++) name = $"{rng.Pick(pool)} {k}세";
            }
            used?.Add(name);
            return name;
        }

        public static Gladiator Generate(Rng rng, GenOptions opt)
        {
            var nation = Data.Nation(opt.nation);
            string raceId = opt.race ?? rng.Weighted(nation.races);
            var race = Data.Race(raceId);
            var style = Data.Style(opt.style ?? PickStyle(rng, raceId));

            // 나이: 데뷔 ~ 전성기 끝 사이에서 성장기 쪽으로 치우치게
            double ageSpan = race.age.primeEnd + 2 - race.age.debut;
            double age = opt.age ?? race.age.debut + Math.Floor(Math.Pow(rng.Next(), 1.4) * ageSpan);

            var attrs = new Dictionary<string, int>();
            foreach (var a in Attrs.All)
            {
                double styleBoost = (style.weights.TryGetValue(a, out var w) ? w : 0) * 0.8;
                double v = rng.Normal(opt.target + styleBoost, 2.2) + (race.attrMods.TryGetValue(a, out var rm) ? rm : 0);
                attrs[a] = (int)Clamp(JsMath.Round(v), 1, 20);
            }
            // 마력은 대부분 낮게. 마법 스타일이나 엘프만 높게 시작
            bool magical = style.weights.ContainsKey("mag") || raceId == "elf";
            if (!magical) attrs["mag"] = (int)Clamp(JsMath.Round(rng.Range(0, 6)) + (race.attrMods.TryGetValue("mag", out var mm) ? mm : 0), 1, 20);

            var prof = new Dictionary<string, int>();
            foreach (var wc in Attrs.WeaponClasses)
            {
                bool main = style.profs.Contains(wc);
                prof[wc] = (int)Clamp(JsMath.Round(main ? rng.Normal(opt.target + 1.5, 2) : rng.Range(1, opt.target * 0.6)), 1, 20);
            }

            var traits = PickTraits(rng, rng.Chance(0.55) ? 2 : 1);
            string gender = rng.Chance(0.25) ? "f" : "m";

            var g = new Gladiator
            {
                id = opt.id,
                name = UniqueName(rng, raceId, gender, opt.usedNames),
                gender = gender,
                race = raceId,
                nation = opt.nation,
                age = age,
                status = opt.status ?? (rng.Chance(0.7) ? "slave" : "free"),
                style = style.id,
                attrs = attrs,
                prof = prof,
            };
            g.hidden = new Hidden
            {
                pa = 0,
                consistency = rng.Int(4, 18),
                bigStage = rng.Int(3, 18),
                injuryProne = rng.Int(2, 16),
                loyalty = rng.Int(4, 18),
                ambition = rng.Int(3, 18),
                greed = rng.Int(3, 18),
            };
            g.traits = traits;
            g.skills = new Skills { active = PickSkills(rng, style.skills, raceId, opt.target), passive = new List<string>() };
            // 종족 기술은 태어날 때부터 가진다 (기술 테이블의 종족 조건 기술)
            SkillBook.GiveRaceSkills(g);
            g.equipment = FixLoadout(raceId, new Equipment { main = style.loadout.main, off = style.loadout.off, armor = style.loadout.armor, helmet = style.loadout.helmet });
            g.morale = rng.Int(50, 75);
            g.popularity = Clamp(JsMath.Round(rng.Normal(opt.target * 2, 8)), 0, 60);
            g.portraitSeed = rng.Int(0, 1_000_000);
            g.sharpness = rng.Int(45, 80);
            g.wear = rng.Int(0, 15);

            g.doctrine = GladiatorRules.DefaultDoctrine(g, rng.Int(40, 90));
            int ca = GladiatorRules.ComputeCA(g);
            double stageGap = age <= race.age.growthEnd ? rng.Range(15, 70) : rng.Range(0, 20);
            g.hidden.pa = (int)Clamp(JsMath.Round(ca + stageGap), ca, opt.paCap ?? 200);
            g.wage = ComputeWage(g, ca);
            if (g.popularity >= 40 && rng.Chance(0.5)) g.nickname = NameBook.Ready ? NameBook.Epithet(rng) : rng.Pick(Data.Names.epithet);
            // 외형은 태어날 때 정해지고 바뀌지 않는다
            var look = Avatar.Make(g);
            g.portrait = new Dictionary<string, string>
            {
                ["skin"] = look.skin, ["hair"] = look.hair, ["hairColor"] = look.hairColor, ["eyes"] = look.eyes, ["mouth"] = look.mouth,
                ["beard"] = look.beard, ["helmet"] = look.helmet, ["cloth"] = look.cloth, ["variant"] = look.variant.ToString(),
            };
            return g;
        }

        static readonly Dictionary<string, string[]> Affinity = new()
        {
            ["human"] = new[] { "murmillo", "secutor", "thraex", "retiarius" },
            ["orc"] = new[] { "crusher", "secutor", "pankratiast" },
            ["minotaur"] = new[] { "crusher", "pankratiast" },
            ["elf"] = new[] { "thraex", "runeblade", "sorcerer", "dimachaerus" },
            ["dwarf"] = new[] { "murmillo", "crusher", "hoplomachus" },
            ["lizardfolk"] = new[] { "retiarius", "hoplomachus", "dimachaerus" },
            ["beastkin"] = new[] { "bestiarius", "dimachaerus", "thraex" },
        };

        static string PickStyle(Rng rng, string raceId)
        {
            var weights = new Dictionary<string, double>();
            foreach (var s in Data.Styles) weights[s.id] = 1;
            // 종족별 어울리는 스타일 가중치
            foreach (var s in Affinity[raceId]) weights[s] += 4;
            if (raceId == "minotaur" || raceId == "orc")
            {
                weights["sorcerer"] = 0.1;
                weights["runeblade"] = 0.2;
            }
            return rng.Weighted(weights);
        }

        static List<string> PickTraits(Rng rng, int n)
        {
            // TS는 delete로 키를 빼므로 순서가 유지되는 목록으로 흉내 낸다
            var pool = new List<KeyValuePair<string, double>>();
            foreach (var t in Data.Traits) if (t.spawn > 0) pool.Add(new(t.id, t.spawn));
            var o = new List<string>();
            while (o.Count < n)
            {
                string t = rng.Weighted(pool);
                pool.RemoveAll(p => p.Key == t);
                o.Add(t);
            }
            return o;
        }

        static List<string> PickSkills(Rng rng, List<string> styleSkills, string raceId, double target)
        {
            // 실력이 높을수록 스킬을 많이 안다 (최대 4칸)
            int count = (int)Clamp(JsMath.Round(target / 5), 1, 3);
            var o = styleSkills.Take(count).ToList();
            if (target >= 9 && rng.Chance(0.5)) o.Add(Data.Race(raceId).raceSkill);
            return o.Take(4).ToList();
        }

        static Equipment FixLoadout(string raceId, Equipment l)
        {
            // 미노타우로스는 경량 방어구 착용 불가 (거구)
            if (raceId == "minotaur" && Data.Armor(l.armor)?.weight == "light") l.armor = "chainmail";
            // Unity판: 갑옷 무게에 맞춰 팔·다리 보호구를 기본으로 갖춘다 (난수를 쓰지 않는다)
            if (Uniques.Enabled)
            {
                string wt = Data.Armor(l.armor)?.weight;
                if (wt == "heavy") { l.arms = "plate_vambrace"; l.legs = "ocrea"; }
                else if (wt == "medium") { l.arms = "bronze_vambrace"; l.legs = "leather_greaves"; }
                else l.arms = "leather_bracer";
            }
            return l;
        }

        public static double ComputeWage(Gladiator g, int ca)
        {
            var w = Bal.Economy["wage"];
            double slaveMin = (double)w["slaveMin"], slaveMax = (double)w["slaveMax"], perCa = (double)w["perCa"];
            var race = Data.Race(g.race);
            double b = g.status == "slave" ? slaveMin + ca / 200.0 * (slaveMax - slaveMin) * 3 : ca * perCa;
            return Math.Max(slaveMin, JsMath.Round(b * race.wageMult * (1 + g.popularity / 200)));
        }
    }
}
