// 기술 테이블 연결 (Unity판): Tables/Skill·Effect·SkillLearn·SkillConst.csv를 읽어
//  1) 기존 skills.json에 없는 능동 기술을 전투 기술(SkillDef)로 등록하고 (기존 33개는 JSON 정의 그대로),
//  2) 상시·반응 기술을 전투 보정치(Fighter.fx)로 바꿔 주고,
//  3) 급료일(주 1회)마다 조건을 채운 기술을 확률로 익히게 한다.
// 규칙은 Tables/README.md. 끄면(Enabled=false) 습득·발동 확률 굴림을 하지 않는다 (골든 테스트).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Colosseum.Core
{
    public static class SkillBook
    {
        public static bool Enabled = true;

        public class SkillRow
        {
            public int id, tier, cooldown, sta, mana, priority, effectGroup, learnGroup;
            public string key, name, desc, type, category, weapon, race, trigger, require, procStat, icon;
            public double triggerValue, requireValue, procRate, procPerStat, learnRate;
            public string Id => "skill." + key;
        }
        public class EffectRow { public int group, seq, duration; public string effect, target, valueType, stat, status; public double value, chance; }
        public class LearnRow { public int group, seq; public string cond, key, op; public double value; }
        public class StatusRow { public int id, maxDuration, effectGroup; public string key, name, desc, kind, icon; }

        public static List<SkillRow> Skills = new();
        public static Dictionary<int, List<EffectRow>> Effects = new();
        public static Dictionary<int, List<LearnRow>> Learns = new();
        public static Dictionary<string, double> Const = new();
        /// <summary>상태 이상 (키 = 표의 Status 키, 예: Bleed)</summary>
        public static Dictionary<string, StatusRow> Statuses = new();
        static Dictionary<string, SkillRow> byId = new();

        public static bool Loaded => Skills.Count > 0;
        public static SkillRow Row(string skillId) => skillId != null && byId.TryGetValue(skillId, out var r) ? r : null;
        public static double C(string k, double def = 0) => Const.TryGetValue(k, out var v) ? v : def;

        // ── 읽기 ──

        /// <summary>CSV 한 줄: "..." 안의 쉼표와 "" 처리</summary>
        static List<string> Split(string line)
        {
            var o = new List<string>();
            var cur = new System.Text.StringBuilder();
            bool q = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (q)
                {
                    if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                    else if (ch == '"') q = false;
                    else cur.Append(ch);
                }
                else if (ch == '"') q = true;
                else if (ch == ',') { o.Add(cur.ToString()); cur.Clear(); }
                else cur.Append(ch);
            }
            o.Add(cur.ToString());
            return o;
        }

        /// <summary>머리 3줄(열 이름·자료형·설명)을 건너뛰고 열 이름 → 값 사전으로</summary>
        static List<Dictionary<string, string>> Rows(string csv)
        {
            var lines = csv.Replace("\r", "").Split('\n').Where(l => l.Length > 0).ToList();
            var cols = Split(lines[0]);
            return lines.Skip(3).Select(l =>
            {
                var v = Split(l);
                var d = new Dictionary<string, string>();
                for (int i = 0; i < cols.Count; i++) d[cols[i]] = i < v.Count ? v[i] : "";
                return d;
            }).ToList();
        }

        static int I(Dictionary<string, string> r, string k) => int.TryParse(r[k], out var v) ? v : 0;
        static double F(Dictionary<string, string> r, string k) => double.TryParse(r[k], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;

        /// <summary>read(이름) = CSV 글 (이름: Skill, Effect, SkillLearn, SkillConst). 읽은 뒤 Data에 새 능동 기술을 등록한다</summary>
        public static void LoadFrom(Func<string, string> read)
        {
            Skills = Rows(read("Skill")).Select(r => new SkillRow
            {
                id = I(r, "id"), key = r["key"], name = r["name"], desc = r["desc"], type = r["type"], category = r["category"], weapon = r["weapon"], race = r["race"],
                tier = I(r, "tier"), trigger = r["trigger"], triggerValue = F(r, "trigger_value"), require = r["require"], requireValue = F(r, "require_value"),
                procRate = F(r, "proc_rate"), procStat = r["proc_stat"], procPerStat = F(r, "proc_per_stat"), cooldown = I(r, "cooldown"), sta = I(r, "sta_cost"),
                mana = I(r, "mana_cost"), priority = I(r, "priority"), effectGroup = I(r, "effect_group"), learnGroup = I(r, "learn_group"), learnRate = F(r, "learn_rate"), icon = r["icon"],
            }).ToList();
            Effects = Rows(read("Effect")).Select(r => new EffectRow
            {
                group = I(r, "group"), seq = I(r, "seq"), effect = r["effect"], target = r["target"], valueType = r["value_type"], value = F(r, "value"),
                stat = r["stat"], status = r["status"], duration = I(r, "duration"), chance = F(r, "chance"),
            }).GroupBy(e => e.group).ToDictionary(g => g.Key, g => g.OrderBy(e => e.seq).ToList());
            Learns = Rows(read("SkillLearn")).Select(r => new LearnRow { group = I(r, "group"), seq = I(r, "seq"), cond = r["cond"], key = r["key"], op = r["op"], value = F(r, "value") })
                .GroupBy(e => e.group).ToDictionary(g => g.Key, g => g.OrderBy(e => e.seq).ToList());
            Const = Rows(read("SkillConst")).ToDictionary(r => r["key"], r => F(r, "value"));
            Statuses = Rows(read("Status")).Select(r => new StatusRow
            {
                id = I(r, "id"), key = r["key"], name = r["name"], desc = r["desc"], kind = r["kind"], maxDuration = I(r, "max_duration"), effectGroup = I(r, "effect_group"), icon = r["icon"],
            }).ToDictionary(x => x.key);
            byId = Skills.ToDictionary(s => s.Id);
            foreach (var s in Skills.Where(s => s.type == "Active" && Data.Skill(s.Id) == null)) Data.AddSkill(ToDef(s));
        }

        // ── 전투 연결 ──

        /// <summary>표의 상태 키 → 전투 상태 이름 (출혈은 전투에서 중독과 같은 지속 피해로 다룬다)</summary>
        public static string StatusName(string k) => k switch
        {
            "Bleed" => "poison", "ArmorBreak" => "armor_break", "ArmWound" => "arm_wound", "EvadeUp" => "evade_up", "WindStep" => "wind_step", "EarthStance" => "earth_stance",
            _ => k.ToLowerInvariant(),
        };

        static List<EffectRow> Fx(SkillRow s) => Effects.TryGetValue(s.effectGroup, out var l) ? l : new List<EffectRow>();
        public static List<EffectRow> EffectsOfStatus(StatusRow s) => Effects.TryGetValue(s.effectGroup, out var l) ? l : new List<EffectRow>();

        /// <summary>표의 능동 기술 → 전투 기술 정의. 전투 기술이 아직 못 다루는 효과(회복·능력치 증감)는 비슷한 것으로 옮긴다</summary>
        static SkillDef ToDef(SkillRow s)
        {
            var d = new SkillDef
            {
                id = s.Id, name = s.name, cost = s.sta, mana = s.mana > 0 ? s.mana : null, cd = s.cooldown,
                kind = s.category switch { "Attack" => "attack", "Control" => "control", "Move" => "move", _ => "self" },
                race = s.race == "Any" ? null : s.race.ToLowerInvariant(),
                requireShield = s.require == "Shield", requireNet = s.require == "Net", useAmmo = s.require == "Ammo", ranged = s.require == "Far" && s.category != "Attack",
                requireTargetHpBelow = s.require == "TargetHpBelow" ? s.requireValue : s.trigger == "OnOppHpBelow" ? s.triggerValue : null,
            };
            foreach (var e in Fx(s))
            {
                switch (e.effect)
                {
                    case "Damage" when e.target == "Enemy": d.mult = e.value; if (d.kind == "self") d.kind = "attack"; break;
                    case "ExtraHit": d.hits = 1 + e.value; break;
                    case "MagicDamage" when e.valueType == "Flat": d.magicDamage = e.value; d.magic = true; break;
                    case "HitMod": d.hitMod = e.value; break;
                    case "CritMod": d.critAdd = e.value; break;
                    case "PierceMod": d.pierceAdd = e.value; break;
                    case "CloseIn": d.closeIn = true; d.fromAnyRange = s.require == "Far"; break;
                    case "AutoHit": d.autoHit = true; break;
                    case "Crowd": d.crowd = e.value; break;
                    case "Momentum": if (e.target == "Enemy") d.enemyMomentum = e.value; else d.selfMomentum = e.value; break;
                    case "RestoreSta": d.restoreSta = e.value; break;
                    case "Heal": d.restoreSta = (d.restoreSta ?? 0) + e.value; break;
                    case "ApplyStatus" when e.target == "Enemy": d.apply = new StatusApply { status = StatusName(e.status), turns = Math.Max(1, e.duration), chance = e.chance }; break;
                    case "ApplyStatus": d.self = new SelfStatus { status = StatusName(e.status), turns = Math.Max(1, e.duration) }; break;
                    case "StatMod": d.self ??= new SelfStatus { status = "adapt", turns = Math.Max(1, e.duration) }; break;
                }
            }
            if (d.kind == "attack" && d.mult == null && d.magicDamage == null) d.mult = 1;
            return d;
        }

        /// <summary>상시·반응 기술의 전투 보정치 (Fighter.fx 키: hit crit dmg block evade pierce taken crowd staRegen thorns parryCounter)</summary>
        public static Dictionary<string, double> PassiveFx(Gladiator g)
        {
            var fx = new Dictionary<string, double>();
            if (!Enabled || !Loaded) return fx;
            void Add(string k, double v) => fx[k] = (fx.TryGetValue(k, out var o) ? o : 0) + v;
            foreach (var id in g.skills.passive.Concat(g.skills.active))
            {
                var s = Row(id);
                if (s == null || s.type == "Active") continue;
                // 반응 기술: 지금은 흘린 뒤 되받아치기와 맞으면 되갚기만 전투가 다룬다
                if (s.type == "Reaction")
                {
                    if (s.trigger == "OnParry") Add("parryCounter", s.procRate);
                    else if (s.trigger == "OnDamaged") Add("thorns", 0.15);
                    continue;
                }
                // 조건부 상시 기술은 그 조건에서 절반쯤 걸린다고 보고 반만 넣는다
                double k = s.trigger == "Always" ? 1 : 0.5;
                foreach (var e in Fx(s))
                    switch (e.effect)
                    {
                        case "HitMod": Add("hit", e.value * k); break;
                        case "CritMod": Add("crit", e.value * k); break;
                        case "DamageMod": Add("dmg", e.value * k); break;
                        case "BlockMod": Add("block", e.value * k); break;
                        case "EvadeMod": Add("evade", e.value * k); break;
                        case "PierceMod": Add("pierce", e.value * k); break;
                        case "DamageTakenMod": Add("taken", e.value * k); break;
                        case "Crowd": Add("crowd", e.value * k); break;
                        case "RestoreSta": Add("staRegen", e.value); break;
                    }
            }
            return fx;
        }

        /// <summary>사건 반응 기술 (막음·피함·동료 쓰러짐·동료 위험). 흘림·피격 반응은 PassiveFx가 보정치로 다룬다</summary>
        static readonly HashSet<string> EventTriggers = new() { "OnBlock", "OnDodge", "OnAllyDown", "OnAllyHpBelow" };
        public static List<SkillRow> Reactions(Gladiator g) =>
            !Enabled || !Loaded ? null : g.skills.passive.Select(Row).Where(s => s != null && s.type == "Reaction" && EventTriggers.Contains(s.trigger)).ToList();

        public static List<EffectRow> EffectsOf(SkillRow s) => Fx(s);

        /// <summary>표의 능동 기술이면 발동 조건(trigger)과 확률을 굴린다. 표에 없는 기존 기술은 늘 통과 (난수도 안 씀)</summary>
        public static bool ProcGate(string skillId, Func<string, double> attr, double myHp, double oppHp, double sta, int round, bool oppDisabled, bool matchStart, Func<double> roll)
        {
            var s = Row(skillId);
            if (!Enabled || s == null || s.type != "Active" || Data.SkillFromJson(skillId)) return true;
            bool ok = s.trigger switch
            {
                "OnTurn" => true,
                "OnMatchStart" => matchStart,
                "OnRoundAtLeast" => round >= s.triggerValue,
                "OnHpBelow" => myHp <= s.triggerValue,
                "OnOppHpBelow" => oppHp <= s.triggerValue,
                "OnStaBelow" => sta <= s.triggerValue,
                "OnOppDisabled" => oppDisabled,
                _ => false,
            };
            if (!ok) return false;
            double p = s.procRate + (s.procStat != "None" ? attr(s.procStat.ToLowerInvariant()) * s.procPerStat : 0);
            p = Math.Max(C("ProcMin", 0.02), Math.Min(C("ProcMax", 0.85), p));
            return roll() < p;
        }

        // ── 습득 ──

        static double Stat(Gladiator g, string cond, string key)
        {
            switch (cond)
            {
                case "WeaponProf":
                    if (key == "Any") return g.prof.Count > 0 ? g.prof.Values.Max() : 0;
                    string cls = string.Concat(key.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLower(c) : char.ToLower(c).ToString()));
                    return g.prof.TryGetValue(cls, out var p) ? p : 0;
                case "Attr": return GladiatorRules.EffectiveAttr(g, key.ToLowerInvariant());
                case "Matches": return g.record.wins + g.record.losses;
                case "Wins": return g.record.wins;
                case "Kills": return g.record.kills;
                case "Popularity": return g.popularity;
                case "Age": return g.age;
                case "Race": return string.Equals(g.race, key, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                case "Style": return g.style == key ? 1 : 0;
                case "Trait": return g.traits.Contains(key) ? 1 : 0;
                case "HasSkill": return g.skills.active.Contains("skill." + key) || g.skills.passive.Contains("skill." + key) ? 1 : 0;
            }
            return 0;
        }

        public static bool Meets(Gladiator g, SkillRow s) =>
            Learns.TryGetValue(s.learnGroup, out var conds) && conds.All(c =>
            {
                double v = Stat(g, c.cond, c.key);
                return c.op switch { "GE" => v >= c.value, "LE" => v <= c.value, _ => Math.Abs(v - c.value) < 1e-9 };
            });

        static bool Has(Gladiator g, SkillRow s) => g.skills.active.Contains(s.Id) || g.skills.passive.Contains(s.Id);

        /// <summary>아직 없는 기술 중 조건을 다 채운 것 (화면 안내용)</summary>
        public static List<SkillRow> Ready(Gladiator g) => Skills.Where(s => !Has(g, s) && Meets(g, s)).ToList();

        /// <summary>
        /// LearnCheckDays마다 (기본 28일, 달마다 한 번): 조건을 채운 기술 중 하나를 골라 learn_rate × 등급 배율 (+ 교관 평점/20 × LearnTrainerBonus) 확률로 익힌다.
        /// 칸이 차면 등급·우선순위가 낮은 것을 밀어낸다. 우리 검투사가 익히면 서판에 알린다.
        /// </summary>
        public static void WeeklyLearn(GameState state, Rng rng)
        {
            if (!Enabled || !Loaded) return;
            // 옛 저장·다른 길로 생긴 검투사도 종족 기술은 알림 없이 챙긴다
            foreach (var g in state.gladiators.Values) if (g.alive) GiveRaceSkills(g);
            // 급료일(7일)마다 불리지만 판정은 LearnCheckDays마다만 한다
            if (state.day % Math.Max(1, (int)C("LearnCheckDays", 28)) != 0) return;
            foreach (var l in state.ludi.Values)
            {
                double doctore = l.staff.Select(id => state.staff != null && state.staff.TryGetValue(id, out var st) ? st : null).Where(st => st?.role == "doctore").Select(st => st.rating / 20).DefaultIfEmpty(0).Max();
                foreach (var gid in l.gladiators)
                {
                    if (!state.gladiators.TryGetValue(gid, out var g) || !g.alive) continue;
                    // 한 번에 후보 하나만 골라 한 번 굴린다 (후보가 많아도 빨라지지 않게). 종족 기술은 생성 때 이미 가진다
                    var ready = Ready(g).Where(r => !IsRaceSkill(r)).ToList();
                    if (ready.Count == 0) continue;
                    var s = ready[rng.Int(0, ready.Count - 1)];
                    double p = s.learnRate * Math.Pow(C("LearnTierPenalty", 0.5), s.tier - 1) + doctore * C("LearnTrainerBonus", 0.06);
                    if (!rng.Chance(p)) continue;
                    Learn(g, s);
                    if (l.isPlayer)
                        StateUtil.PushMessage(state, "info", $"{g.name}: 새 기술", $"{g.name}이(가) '{s.name}' 기술을 익혔습니다. {s.desc}.");
                }
            }
        }

        /// <summary>종족 기술: 기술 테이블에서 종족 조건으로 바로 주는 것 (learn_rate 1)</summary>
        static bool IsRaceSkill(SkillRow s) => s.learnRate >= 1;

        /// <summary>종족 기술을 서판 알림 없이 바로 준다 (검투사를 만들 때, 옛 저장을 불러올 때)</summary>
        public static void GiveRaceSkills(Gladiator g)
        {
            if (!Enabled || !Loaded || g.skills == null) return;
            foreach (var s in Skills.Where(s => IsRaceSkill(s) && !Has(g, s) && Meets(g, s)).ToList()) Learn(g, s);
        }

        /// <summary>기술을 칸에 넣는다. 칸이 차면 등급·우선순위가 가장 낮은 표 기술 하나를 뺀다</summary>
        public static void Learn(Gladiator g, SkillRow s)
        {
            var list = s.type == "Active" ? g.skills.active : g.skills.passive;
            int cap = (int)(s.type == "Active" ? C("ActiveSlots", 4) : C("PassiveSlots", 3) + C("ReactionSlots", 2));
            if (list.Count >= cap)
            {
                var drop = list.Select(Row).Where(r => r != null).OrderBy(r => r.tier).ThenBy(r => r.priority).FirstOrDefault();
                if (drop == null || drop.tier > s.tier) return;
                list.Remove(drop.Id);
            }
            list.Add(s.Id);
            if (s.type == "Active" && g.doctrine != null && !g.doctrine.order.Contains(s.Id)) g.doctrine.order.Add(s.Id);
        }
    }
}
