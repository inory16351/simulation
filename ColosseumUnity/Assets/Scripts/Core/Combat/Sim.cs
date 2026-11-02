// 1:1 전투 시뮬레이션 (src/core/combat/sim.ts). 난수 소비 순서와 계산 순서는 TS를 따르되, 경기장 좌표(Arena: 이동·밀려남·벽에 몰림·도망·가운데 잡기)는 Unity판에만 있다.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Colosseum.Core.Combat
{
    /// <summary>전투 이벤트. TS의 판별 유니온을 필드 하나로 펼쳤다 (t가 종류)</summary>
    public class CombatEvent
    {
        public string t;
        public string a, b, rule, actor, target, skill, part, status, reason, winner, loser, kind, outcome, feint;
        public int round, dist, turns, dir, motion, guardDir, guard, seen;
        public double damage, hpAfter, maxHp, delta, value, deathChance;
        public bool closer, hit, blocked, crit, magic, parried, counter, resisted, died, improv;
        /// <summary>경기장 좌표: 도망 / 가운데 잡기 / 맞고 밀려남 / 벽에 몰린 채 맞음</summary>
        public bool flee, center, push, cornered;
        public RuleAction action;
        public string[] actors;
        /// <summary>이 사건 뒤 두 사람의 경기장 좌표 [x0, y0, x1, y1] (Arena 좌표, 위가 0). 위치가 바뀐 사건에만 있다</summary>
        public double[] pos;
    }

    /// <summary>경기장 좌표계. 웹판 장면과 같은 400×160 세계, 모래 마당은 타원.
    /// 거리 단계(0 근접·1 중거리·2 원거리)는 두 사람 사이 실제 거리에서 나온다.</summary>
    public static class Arena
    {
        public const double CX = 200, CY = 82;
        /// <summary>말이 설 수 있는 타원 (모래 타원 188×76에서 말 반지름만큼 안쪽)</summary>
        public const double RX = 172, RY = 60;
        /// <summary>거리 단계 경계: 이보다 가까우면 근접, 그다음은 중거리</summary>
        public const double CloseMax = 40, MidMax = 85;
        /// <summary>단계를 바꿀 때 서는 간격</summary>
        public static readonly double[] Gap = { 34, 62, 110 };
        /// <summary>벽에 몰렸다고 보는 타원 반경 비율</summary>
        public const double WallZone = 0.85;
        public static readonly double[] Start = { CX - 60, CY, CX + 60, CY };
        /// <summary>좌표 규칙(밀려남·몰림·도망·가운데 잡기, 좌표로 거리 단계 정하기)을 쓰는가. 웹판과 같은 결과를 비교하는 골든 테스트만 끈다. 꺼도 좌표는 기록한다(보기용)</summary>
        public static bool Enabled = true;

        public static int Bucket(double sep) => sep < CloseMax ? 0 : sep < MidMax ? 1 : 2;
        public static double Norm(double x, double y) => Math.Sqrt(Math.Pow((x - CX) / RX, 2) + Math.Pow((y - CY) / RY, 2));
    }

    public class MatchOptions
    {
        /// <summary>training | missio | sudden</summary>
        public string rule;
        public uint seed;
        public Doctrine[] doctrines;
        public double[] medicalLevel;
        public bool noDeath;
        public double? missioDeathMult;
        public double?[] startHp;
    }

    public class MatchResult
    {
        public List<CombatEvent> events;
        public string winnerId, loserId, reason, died;
        public int rounds;
        public double crowd;
        public Dictionary<string, double> damageDealt, crowdContribution, hpLeft;
        public Dictionary<string, Injury> injuries;
    }

    class Fighter
    {
        public Gladiator g;
        public int idx;
        public double hp, maxHp, sta, maxSta, mana, momentum;
        // 순서가 있는 상태/쿨다운 (JS Map처럼 넣은 순서를 지킨다)
        public List<string> statusKeys = new();
        public Dictionary<string, int> statuses = new();
        public List<string> cdKeys = new();
        public Dictionary<string, double> cooldowns = new();
        public WeaponDef main, off;
        public ArmorDef shield, armor, helmet;
        public double ammo;
        public bool netReady;
        public double wardHp;
        public bool usedUnbreakable, usedRaceSkill;
        public Doctrine doctrine;
        public string stance;
        public HashSet<int> firedRules = new();
        public string priorityStake;
        public double damageDealt, damageTaken, crowdContribution;
        public double perf;
        public string worstPart;
        public double worstHit;
        public Habits habits;
        public Mind mind;
        public ReadBook book = new();
        public int[] seenN = new int[9];

        public bool Has(string s) => statuses.ContainsKey(s);
        public int Get(string s) => statuses.TryGetValue(s, out var v) ? v : 0;
        public void Set(string s, int v)
        {
            if (!statuses.ContainsKey(s)) statusKeys.Add(s);
            statuses[s] = v;
        }
        public void Del(string s)
        {
            if (statuses.Remove(s)) statusKeys.Remove(s);
        }
        public void SetCd(string s, double v)
        {
            if (!cooldowns.ContainsKey(s)) cdKeys.Add(s);
            cooldowns[s] = v;
        }
        public bool HasPassive(string id) => g.skills.passive.Contains(id);
    }

    class AttackSpec
    {
        public SkillDef skill;
        public WeaponDef weapon;
        public double mult = 1, hitMod, pierceAdd, critAdd;
        public bool autoHit;
        public double? magicDamage;
        public bool counter;
        public AttackSpec Clone() => (AttackSpec)MemberwiseClone();
    }

    public static class Sim
    {
        static readonly List<KeyValuePair<string, double>> PartWeights = new()
        {
            new("head", 1), new("neck", 0.4), new("chest", 3), new("side", 2), new("shoulder", 2), new("arm", 2.5), new("thigh", 2), new("leg", 1.5),
        };

        static readonly Dictionary<string, double> FocusHit = new() { ["spread"] = 0, ["head"] = -0.15, ["body"] = 0.1, ["legs"] = -0.05, ["arm"] = -0.1 };

        static readonly Dictionary<string, List<KeyValuePair<string, double>>> FocusParts = new()
        {
            ["spread"] = PartWeights,
            ["head"] = new() { new("head", 4), new("neck", 1), new("shoulder", 1) },
            ["body"] = new() { new("chest", 4), new("side", 3), new("shoulder", 1) },
            ["legs"] = new() { new("thigh", 3), new("leg", 3), new("side", 0.5) },
            ["arm"] = new() { new("arm", 4), new("shoulder", 2) },
        };

        static readonly Dictionary<string, double> StanceMult = new()
        {
            ["assault"] = 1.35, ["balanced"] = 1, ["bulwark"] = 0.85, ["harass"] = 0.95, ["showman"] = 0.85, ["blitz"] = 1.15,
        };

        static Fighter MakeFighter(Gladiator g, int idx, Doctrine doctrine)
        {
            var main = Data.Weapon(g.equipment.main) ?? Data.Weapon("cestus");
            var offW = g.equipment.off != null ? Data.Weapon(g.equipment.off) : null;
            var offA = g.equipment.off != null ? Data.Armor(g.equipment.off) : null;
            var armor = Data.Armor(g.equipment.armor);
            var helmet = g.equipment.helmet != null ? Data.Armor(g.equipment.helmet) : null;
            double tou = GladiatorRules.EffectiveAttr(g, "tou");
            double maxHp = JsMath.Round(Bal.C("baseHp") + tou * Bal.C("hpPerTou") + GladiatorRules.EffectiveAttr(g, "str") * Bal.C("hpPerStr"));
            double maxSta = JsMath.Round(Bal.C("baseSta") + GladiatorRules.EffectiveAttr(g, "end") * Bal.C("staPerEnd"));
            double E(string a) => GladiatorRules.EffectiveAttr(g, a);
            return new Fighter
            {
                g = g, idx = idx, hp = maxHp, maxHp = maxHp, sta = maxSta, maxSta = maxSta,
                mana = E("mag") * Bal.C("manaPerMag"),
                momentum = 50,
                main = main, off = offW,
                shield = offA?.slot == "shield" ? offA : null,
                armor = armor, helmet = helmet,
                ammo = offW?.ammo ?? main.ammo ?? 0,
                netReady = offW?.id == "net",
                doctrine = doctrine, stance = doctrine.stance,
                perf = GladiatorRules.Performance(g),
                habits = Exchange.HabitsOf(g, main.@class, doctrine.focus, offA?.slot == "shield", g.race == "dwarf" ? 0 : armor?.agiPenalty ?? 0, E("agg"), E("genius"), E("blk"), E("eva")),
                mind = new Mind { read = E("read"), feint = E("feint"), genius = E("genius"), adapt = E("adapt"), dec = E("dec"), rea = E("rea"), comp = E("comp") },
            };
        }

        /// <summary>상태이상을 반영한 전투 중 능력치</summary>
        static double Attr(Fighter f, string a)
        {
            double v = GladiatorRules.EffectiveAttr(f.g, a);
            if (f.Has("weakened") && (a == "str" || a == "acc")) v -= 3;
            if (f.Has("hobbled") && (a == "agi" || a == "eva")) v -= 4;
            if (a == "agi" && f.armor != null && f.g.race != "dwarf") v -= f.armor.agiPenalty ?? 0;
            if (a == "acc" && f.helmet?.accPenalty is double ap && ap != 0) v -= ap;
            if (a == "show" && f.helmet?.showBonus is double sb && sb != 0) v += sb;
            return Math.Max(1, v);
        }

        public static MatchResult Run(Gladiator a, Gladiator b, MatchOptions opt) => new Match(a, b, opt).Play();

        sealed class Match
        {
            readonly Gladiator a, b;
            readonly MatchOptions opt;
            readonly Rng rng;
            readonly Fighter[] F;
            readonly List<CombatEvent> events = new();
            int dist = 2;
            /// <summary>두 사람의 경기장 좌표</summary>
            readonly double[] px = { Arena.Start[0], Arena.Start[2] }, py = { Arena.Start[1], Arena.Start[3] };
            double crowd;
            int stall;
            int round;
            readonly bool hasCrowd;
            (Fighter winner, Fighter loser, string reason, bool crit)? ended;

            public Match(Gladiator a, Gladiator b, MatchOptions opt)
            {
                this.a = a; this.b = b; this.opt = opt;
                rng = new Rng(opt.seed);
                var d0 = opt.doctrines?.ElementAtOrDefault(0) ?? a.doctrine ?? GladiatorRules.DefaultDoctrine(a);
                var d1 = opt.doctrines?.ElementAtOrDefault(1) ?? b.doctrine ?? GladiatorRules.DefaultDoctrine(b);
                F = new[] { MakeFighter(a, 0, d0), MakeFighter(b, 1, d1) };
                for (int i = 0; i < 2; i++)
                {
                    var f = F[i];
                    var r = opt.startHp?.ElementAtOrDefault(i);
                    if (r != null) f.hp = Math.Max(1, JsMath.Round(f.maxHp * r.Value));
                    // 일관성이 낮으면 그날그날 읽는 감이 들쭉날쭉하다
                    f.mind.form = (rng.Next() - 0.5) * (21 - f.g.hidden.consistency) * 0.06;
                }
                // 위압감: 마주 서는 것만으로 기세가 꺾인다
                foreach (var f in F)
                {
                    var o = F[1 - f.idx];
                    double d = Math.Max(0, GladiatorRules.EffectiveAttr(f.g, "aura") - GladiatorRules.EffectiveAttr(o.g, "comp"));
                    o.momentum = Math.Max(0, o.momentum - d / 2 * (GladiatorRules.EffectiveAttr(o.g, "grit") < 10 ? 2 : 1));
                }
                // 입장 자리: 경기마다 마주 선 축이 조금씩 기운다 (난수를 쓰지 않는다)
                double th = ((int)(opt.seed % 11) - 5) * 0.12, half = (Arena.Start[2] - Arena.Start[0]) / 2;
                px[0] = Arena.CX - Math.Cos(th) * half; py[0] = Arena.CY - Math.Sin(th) * half * 0.6;
                px[1] = Arena.CX + Math.Cos(th) * half; py[1] = Arena.CY + Math.Sin(th) * half * 0.6;
                crowd = Bal.C("crowdStart");
                // 훈련전은 관중 없이 치른다
                hasCrowd = (bool)Bal.Rule(opt.rule)["crowd"];
            }

            void Emit(CombatEvent e) => events.Add(e);

            // ───────── 경기장 좌표 ─────────

            double[] Pos() => new[] { px[0], py[0], px[1], py[1] };
            double Sep() => Math.Sqrt((px[0] - px[1]) * (px[0] - px[1]) + (py[0] - py[1]) * (py[0] - py[1]));
            double WallNorm(Fighter f) => Arena.Norm(px[f.idx], py[f.idx]);

            /// <summary>벽에 몰림: 벽 가까이 섰고 상대가 더 안쪽(가운데 쪽)에 있다</summary>
            bool Cornered(Fighter def, Fighter att) => WallNorm(def) > Arena.WallZone && WallNorm(att) < WallNorm(def);

            /// <summary>설 수 있는 타원 밖이면 가운데 쪽으로 끌어들인다</summary>
            void Clamp(int i)
            {
                double n = Arena.Norm(px[i], py[i]);
                if (n <= 1) return;
                px[i] = Arena.CX + (px[i] - Arena.CX) / n;
                py[i] = Arena.CY + (py[i] - Arena.CY) / n;
            }

            /// <summary>상대에게서 나를 향한 단위 벡터</summary>
            (double x, double y) Away(int i)
            {
                int o = 1 - i;
                double x = px[i] - px[o], y = py[i] - py[o], len = Math.Sqrt(x * x + y * y);
                return len < 1 ? (i == 0 ? -1 : 1, 0) : (x / len, y / len);
            }

            static (double x, double y) Rot((double x, double y) v, double deg)
            {
                double r = deg * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r);
                return (v.x * c - v.y * s, v.x * s + v.y * c);
            }

            /// <summary>상대 둘레의 어느 방향으로 설지 골라 간격 gap에 선다. 고른 방향 중 벽에서 가장 먼 쪽</summary>
            void StandAt(int i, double gap, params (double x, double y)[] dirs)
            {
                int o = 1 - i;
                (double x, double y) best = dirs[0];
                double bestN = double.MaxValue;
                foreach (var d in dirs)
                {
                    double n = Arena.Norm(px[o] + d.x * gap, py[o] + d.y * gap);
                    if (n < bestN - 1e-9) { bestN = n; best = d; }
                }
                px[i] = px[o] + best.x * gap;
                py[i] = py[o] + best.y * gap;
                Clamp(i);
                Rebucket();
            }

            /// <summary>좌표에서 거리 단계를 다시 낸다. 좌표 규칙을 끄면 좌표는 보기용이라 거리 단계를 건드리지 않는다</summary>
            void Rebucket()
            {
                if (Arena.Enabled) dist = Arena.Bucket(Sep());
            }

            /// <summary>라운드마다 두 사람이 서로를 노려보며 원을 그리고, 기세가 센 쪽이 상대를 밀어붙여 둘이 함께 흘러간다
            /// (원 방향은 네 라운드마다 바뀐다)</summary>
            void Orbit()
            {
                int strong = F[0].momentum >= F[1].momentum ? 0 : 1, weak = 1 - strong;
                var press = Away(weak);
                double amount = 2 + Math.Min(10, Math.Abs(F[0].momentum - F[1].momentum) * 0.15);
                for (int i = 0; i < 2; i++) { px[i] += press.x * amount; py[i] += press.y * amount; }
                double mx = (px[0] + px[1]) / 2, my = (py[0] + py[1]) / 2;
                double deg = (dist == 2 ? 8 : 14) * ((round / 4) % 2 == 0 ? 1 : -1);
                for (int i = 0; i < 2; i++)
                {
                    var v = Rot((px[i] - mx, py[i] - my), deg);
                    px[i] = mx + v.x;
                    py[i] = my + v.y;
                    Clamp(i);
                }
                Rebucket();
            }

            /// <summary>공격을 피한 쪽이 상대를 축으로 옆으로 비켜선다 (벽에서 먼 쪽으로)</summary>
            void Sidestep(Fighter def, Fighter att)
            {
                int i = def.idx;
                double gap = Math.Max(Sep(), 1);
                var u = Away(i);
                StandAt(i, gap, Rot(u, 25), Rot(u, -25));
            }

            /// <summary>다가서기: 상대보다 가운데 쪽으로 돌아 들어가 상대를 벽으로 몬다</summary>
            void Approach(int i, double gap)
            {
                int o = 1 - i;
                var u = Away(i);
                double cx = Arena.CX - px[o], cy = Arena.CY - py[o], cl = Math.Sqrt(cx * cx + cy * cy);
                (double x, double y) c = cl < 1 ? u : (cx / cl, cy / cl);
                double dx = u.x * 0.65 + c.x * 0.35, dy = u.y * 0.65 + c.y * 0.35, dl = Math.Sqrt(dx * dx + dy * dy);
                var dirv = dl < 1e-6 ? u : (dx / dl, dy / dl);
                StandAt(i, gap, Rot(dirv, 25 * ((round + i) % 2 == 0 ? 1 : -1)));
            }

            /// <summary>큰 타격에 맞은 쪽이 때린 쪽 반대로 밀려난다</summary>
            void Knockback(Fighter def, double amount)
            {
                var u = Away(def.idx);
                px[def.idx] += u.x * amount;
                py[def.idx] += u.y * amount;
                Clamp(def.idx);
                Rebucket();
            }
            Fighter Opp(Fighter f) => F[1 - f.idx];
            double Reach(Fighter f) => Math.Max(f.main.range, 0);
            static double HpRatio(Fighter f) => f.hp / f.maxHp;

            void AddCrowd(double delta, Fighter by = null)
            {
                if (delta == 0 || !hasCrowd) return;
                double before = crowd;
                crowd = Math.Max(0, Math.Min(100, crowd + delta));
                if (by != null) by.crowdContribution += crowd - before;
                Emit(new CombatEvent { t = "crowd", delta = crowd - before, value = crowd });
            }

            void ApplyStatus(Fighter target, string status, int turns)
            {
                bool immuneByStance = target.Has("earth_stance") && (status == "prone" || status == "bound" || status == "stun");
                bool immuneJaw = status == "stun" && target.g.traits.Contains("iron_jaw");
                bool resisted = immuneByStance || immuneJaw;
                if (status == "bound" && target.helmet?.netResist is double nr && nr != 0 && rng.Chance(nr)) resisted = true;
                if (status == "bound" && target.g.race == "minotaur") turns = (int)Math.Ceiling(turns / 2.0);
                if (!resisted) target.Set(status, Math.Max(target.Get(status), turns));
                Emit(new CombatEvent { t = "status", target = target.g.id, status = status, turns = turns, resisted = resisted });
            }

            double DealDamage(Fighter att, Fighter def, double amount)
            {
                double dmg = amount;
                if (def.Has("adapt")) dmg = JsMath.Round(dmg * 0.5);
                if (def.stance == "bulwark") dmg = JsMath.Round(dmg * 0.9);
                else if (def.stance == "assault") dmg = JsMath.Round(dmg * 1.05);
                if (def.wardHp > 0)
                {
                    double absorbed = Math.Min(def.wardHp, dmg);
                    def.wardHp -= absorbed;
                    dmg -= absorbed;
                }
                def.hp -= dmg;
                def.damageTaken += dmg;
                att.damageDealt += dmg;
                if (def.hp <= 0 && def.HasPassive("skill.unbreakable") && !def.usedUnbreakable)
                {
                    def.hp = 1;
                    def.usedUnbreakable = true;
                    Emit(new CombatEvent { t = "unbreakable", actor = def.g.id });
                }
                return dmg;
            }

            /// <summary>한 합의 수 싸움. 근접 공격이고 방어자가 움직일 수 있을 때만</summary>
            bool CanExchange(Fighter def, AttackSpec spec) =>
                spec.magicDamage == null && !spec.autoHit && !spec.counter && !(spec.skill?.ranged ?? false) && spec.weapon.@class != "throwing"
                && !(def.Has("bound") || def.Has("prone") || def.Has("stun") || def.Has("grappled"));

            (string outcome, int motion, int adv, int dir) RunExchange(Fighter att, Fighter def, AttackSpec spec)
            {
                int? fix = spec.skill != null && Exchange.SkillMotion.TryGetValue(spec.skill.id, out var m) ? m : null;
                double feintBias = att.stance == "harass" || att.stance == "showman" ? 0.05 : 0;
                var plan = Exchange.PlanAttack(rng, att.mind, att.habits, def.book, att.book, spec.weapon.@class, fix, def.mind.read, feintBias);
                if (plan.feint != null) att.sta = Math.Max(0, att.sta - 4);
                double pressure = Math.Max(0, Attr(att, "aura") - Attr(def, "comp")) * 0.06
                    + (HpRatio(def) < 0.3 && def.mind.comp < 12 ? (12 - def.mind.comp) * 0.05 : 0)
                    + (def.Has("exposed") ? 0.5 : 0);
                var guard = Exchange.PlanGuard(rng, def.mind, def.habits, def.book, att.mind.feint, pressure, plan.feint == "ok" ? plan.shown : null);
                string outcome = Exchange.OutcomeOf(rng, plan, guard, Attr(def, "rea"));
                // 곡검: 방패를 돌아 들어가 막기에 완전히 걸리지 않는다
                if (outcome == "parry" && guard.guard == 0 && (spec.weapon.blockBypass ?? 0) != 0) outcome = "neutral";
                int c = plan.dir == 3 ? -1 : Exchange.Cell(plan.dir, plan.motion);
                int seen = c >= 0 ? def.seenN[c] : 0;
                if (c >= 0)
                {
                    def.book.atk[c] += Exchange.LearnWeight(def.mind.adapt);
                    def.seenN[c]++;
                }
                att.book.guard[guard.guard] += Exchange.LearnWeight(att.mind.adapt);
                Emit(new CombatEvent
                {
                    t = "exchange", actor = att.g.id, target = def.g.id, dir = plan.dir, motion = plan.motion, guardDir = guard.dir, guard = guard.guard,
                    outcome = outcome, feint = plan.feint, improv = plan.improv, seen = seen,
                });
                return (outcome, plan.motion, Exchange.Adv[plan.motion, guard.guard], plan.dir);
            }

            bool Counters(Fighter x, Fighter y) => Data.Style(x.g.style).counters.Contains(y.g.style);

            AttackSpec BasicSpec(Fighter f) => new AttackSpec { weapon = f.main };

            void ResolveHit(Fighter att, Fighter def, AttackSpec spec, bool allowRiposte = true)
            {
                var w = spec.weapon;
                bool magic = spec.magicDamage != null;
                (string outcome, int motion, int adv, int dir)? ex = allowRiposte && CanExchange(def, spec) ? RunExchange(att, def, spec) : null;
                if (ex?.outcome == "parry")
                {
                    // 완전 방어: 피해 없음, 공격자는 자세가 흐트러지고 방어자는 되받아칠 틈을 본다
                    Emit(new CombatEvent { t = "attack", actor = att.g.id, target = def.g.id, skill = spec.skill?.id, hit = false, blocked = true, damage = 0, part = "chest", hpAfter = def.hp, maxHp = def.maxHp, magic = magic, parried = true });
                    att.Set("exposed", Math.Max(att.Get("exposed"), 1));
                    def.momentum = Math.Min(100, def.momentum + 6);
                    att.momentum = Math.Max(0, att.momentum - 4 - Math.Max(0, Attr(def, "aura") - Attr(att, "comp")) / 3);
                    AddCrowd(2, def);
                    if (dist <= Reach(def) && rng.Chance(0.2 + Attr(def, "timing") * 0.04))
                    {
                        var cs = BasicSpec(def);
                        cs.counter = true;
                        ResolveHit(def, att, cs, false);
                    }
                    return;
                }
                double hitChance = Bal.C("baseHit") + (Attr(att, "acc") - Attr(def, "eva")) * Bal.C("hitPerAttrDiff");
                if (ex != null)
                {
                    var e = ex.Value;
                    hitChance += Exchange.AffinityHit(w.@class, e.motion) + (e.outcome == "clean" ? 0.15 : 0) + (e.dir == 3 ? -0.15 : 0)
                        // 타이밍: 상대 호흡이 끊기는 순간에 들어간다
                        + (Attr(att, "timing") - 10) * 0.012;
                }
                // 마무리 본능: 빈사·무너진 상대를 놓치지 않는다 (낮으면 오히려 머뭇거린다)
                bool finishing = HpRatio(def) < 0.3 || def.Has("exposed");
                if (finishing) hitChance += Attr(att, "kill") * 0.01 - (Attr(att, "kill") <= 6 ? 0.1 : 0);
                hitChance += (att.g.prof[w.@class] - 10) * 0.01 + spec.hitMod;
                if (Counters(att, def)) hitChance += Bal.C("counterHit");
                if (Counters(def, att)) hitChance -= Bal.C("counterEvade");
                if (att.momentum >= 80) hitChance += 0.1;
                hitChance += (att.perf - 1) * 0.5 - (def.perf - 1) * 0.3;
                if (Attr(att, "str") < w.reqStr) hitChance -= 0.1;
                if (att.sta <= 0) hitChance -= Bal.C("exhaustedPenalty");
                if (att.Has("blinded")) hitChance -= 0.4;
                if (def.Has("evade_up")) hitChance -= 0.3;
                if (def.Has("wind_step")) hitChance -= 0.4;
                if (def.Has("bound")) hitChance += 0.25;
                if (spec.magicDamage == null) hitChance += FocusHit[att.doctrine.focus];
                if (def.Has("prone")) hitChance += 0.15;
                if (def.Has("exposed")) hitChance += 0.2;
                if (def.g.race == "elf") hitChance -= 0.1;
                // 무거운 갑옷은 회피를 어렵게 한다 (드워프는 강철 뼈로 무시)
                if (def.g.race != "dwarf") hitChance += (def.armor?.agiPenalty ?? 0) * Bal.C("heavyArmorHit");
                if (def.g.traits.Contains("shadow") && round <= 3) hitChance -= 0.2;
                // 벽에 몰리면 비켜설 곳이 없다
                bool cornered = Arena.Enabled && !magic && Cornered(def, att);
                if (cornered) hitChance += 0.08;
                hitChance = Math.Max(Bal.C("hitMin"), Math.Min(Bal.C("hitMax"), hitChance));

                string part = rng.Weighted(spec.magicDamage != null ? PartWeights : FocusParts[att.doctrine.focus]);
                bool hit = spec.autoHit || ex?.outcome == "break" || rng.Chance(hitChance);
                if (!hit)
                {
                    var missEv = new CombatEvent { t = "attack", actor = att.g.id, target = def.g.id, skill = spec.skill?.id, hit = false, damage = 0, part = part, hpAfter = def.hp, maxHp = def.maxHp, magic = magic, counter = spec.counter, cornered = cornered };
                    if (!def.Has("bound") && !def.Has("prone")) { Sidestep(def, att); missEv.pos = Pos(); }
                    Emit(missEv);
                    def.momentum = Math.Min(100, def.momentum + 4);
                    att.momentum = Math.Max(0, att.momentum - 2);
                    if (allowRiposte && def.Has("riposte") && dist <= Reach(def))
                    {
                        def.Del("riposte");
                        ResolveHit(def, att, BasicSpec(def), false);
                    }
                    return;
                }

                bool blocked = false;
                // 방향을 틀렸거나 방어가 무너졌으면 막기 판정이 없다
                bool openHit = ex?.outcome == "clean" || ex?.outcome == "break";
                if (!magic && !openHit && !def.Has("bound") && !def.Has("prone"))
                {
                    double blockChance = Attr(def, "blk") * Bal.C("blockPerBlk") + (def.shield?.block ?? 0);
                    if (def.Has("guard")) blockChance += 0.5;
                    if (def.Has("earth_stance")) blockChance += 0.2;
                    blockChance -= w.blockBypass ?? 0;
                    blocked = rng.Chance(Math.Max(0, Math.Min(0.85, blockChance)));
                }

                double raw;
                double critChance = Attr(att, "acc") * Bal.C("critPerAcc") + w.crit + spec.critAdd + (att.doctrine.focus == "head" ? 0.1 : 0);
                if (ex?.outcome == "clean" && ex.Value.adv > 0) critChance += 0.15;
                if (magic)
                {
                    double focusBonus = (att.main.magicBonus ?? 0) + (att.off?.magicBonus ?? 0);
                    raw = spec.magicDamage.Value * (1 + Attr(att, "mag") / 20) * (1 + focusBonus);
                    critChance *= 0.5;
                }
                else
                {
                    double profCoef = Bal.C("profBase") + att.g.prof[w.@class] * Bal.C("profPer");
                    raw = w.damage * (1 + Attr(att, "str") / 40) * profCoef * spec.mult;
                    if (Attr(att, "str") < w.reqStr) raw *= 0.85;
                    if (att.Has("flame")) raw *= 1.4;
                    if (def.Has("bound") && (w.boundBonus ?? 0) != 0) raw *= 1 + w.boundBonus.Value;
                    if (def.Has("prone")) raw *= 1.3;
                    if (att.Has("arm_wound")) raw *= 0.75;
                }
                // 상성 배율: 클린 히트 ×1.25 (동작까지 유리하면 ×1.5), 방어 무너짐 ×0.9
                if (ex?.outcome == "clean") raw *= ex.Value.adv > 0 ? 1.5 : 1.25;
                else if (ex?.outcome == "break") raw *= 0.9;
                if (finishing) raw *= 1 + Attr(att, "kill") * 0.015;
                if (att.g.race == "orc" && HpRatio(att) <= 0.3) raw *= 1.25;
                if (def.g.race == "orc" && HpRatio(def) <= 0.3) raw *= 1.1;
                bool crit = !blocked && rng.Chance(critChance);

                double defense = 0;
                if (!magic)
                {
                    defense = (def.armor?.defense ?? 0) + (def.helmet?.defense ?? 0);
                    if (part == "head" || part == "neck") defense = (def.helmet?.defense ?? 0) + 1;
                    if (def.Has("armor_break")) defense *= 0.7;
                    defense *= 1 - Math.Min(1, w.pierce + spec.pierceAdd);
                }
                // 방어구는 비율로 피해를 줄인다 (고정 차감은 약한 무기를 무력화하므로)
                double dmg = Math.Max(1, raw * (1 - defense / (defense + Bal.C("armorScale"))));
                if (crit) dmg *= Bal.C("critMult");
                if (blocked) dmg *= 1 - Bal.C("blockReduction");
                if (part == "head" || part == "neck") dmg *= 1.2;
                dmg *= att.perf;
                double dealt = DealDamage(att, def, Math.Max(1, JsMath.Round(dmg)));
                if (dealt > def.worstHit) { def.worstHit = dealt; def.worstPart = part; }

                var hitEv = new CombatEvent { t = "attack", actor = att.g.id, target = def.g.id, skill = spec.skill?.id, hit = true, blocked = blocked, crit = crit, damage = dealt, part = part, hpAfter = Math.Max(0, def.hp), maxHp = def.maxHp, magic = magic, counter = spec.counter, cornered = cornered };
                // 큰 타격은 맞은 쪽을 뒤로 밀어낸다 (벽 쪽으로 몰리거나 간격이 벌어진다)
                if (Arena.Enabled && !blocked && !magic && def.hp > 0 && (crit || dealt > def.maxHp * 0.12))
                {
                    Knockback(def, crit ? 16 : 10);
                    hitEv.push = true;
                    hitEv.pos = Pos();
                }
                Emit(hitEv);
                att.momentum = Math.Min(100, att.momentum + (crit ? 12 : blocked ? 2 : 6));
                def.momentum = Math.Max(0, def.momentum - (crit ? 12 : blocked ? 2 : 6));
                if (ex?.outcome == "break" && def.hp > 0) def.Set("exposed", Math.Max(def.Get("exposed"), 2));
                if (crit) def.momentum = Math.Max(0, def.momentum - Math.Max(0, Attr(att, "aura") - Attr(def, "comp")) / 3);
                if (crit)
                {
                    AddCrowd(8, att);
                    Emit(new CombatEvent { t = "highlight", kind = spec.skill?.id == "skill.execute" ? "execute" : "crit", actors = new[] { att.g.id, def.g.id } });
                }
                else if (dealt > def.maxHp * 0.15) AddCrowd(3, att);

                if (!blocked && !magic && def.hp > 0)
                {
                    string focus = att.doctrine.focus;
                    if (focus == "legs" && rng.Chance(0.3)) ApplyStatus(def, "hobbled", 2);
                    else if (focus == "arm" && rng.Chance(0.3)) ApplyStatus(def, "arm_wound", 2);
                    else if (focus == "head" && rng.Chance(0.12 * (def.g.traits.Contains("glass_jaw") ? 2 : 1))) ApplyStatus(def, "stun", 1);
                }
                if (!blocked && (w.stun ?? 0) != 0 && def.hp > 0)
                {
                    double p = w.stun.Value * (def.g.traits.Contains("glass_jaw") ? 2 : 1);
                    if (rng.Chance(p)) ApplyStatus(def, "stun", 1);
                }
                if (spec.skill?.apply != null && def.hp > 0 && !blocked && rng.Chance(spec.skill.apply.chance))
                    ApplyStatus(def, spec.skill.apply.status, spec.skill.apply.turns);
                if (att.g.race == "orc" && HpRatio(att) <= 0.3 && !att.Has("enraged"))
                {
                    att.Set("enraged", 99);
                    Emit(new CombatEvent { t = "enrage", actor = att.g.id });
                }
            }

            bool SkillUsable(Fighter f, SkillDef s)
            {
                if (s.kind == "passive") return false;
                if ((f.cooldowns.TryGetValue(s.id, out var cd) ? cd : 0) > 0) return false;
                if ((s.cost ?? 0) > f.sta) return false;
                if ((s.mana ?? 0) > f.mana) return false;
                var t = Opp(f);
                if (s.requireShield && f.shield == null) return false;
                if (s.requireNet && !f.netReady) return false;
                if (s.useAmmo && f.ammo <= 0) return false;
                if (s.requireTargetHpBelow != null && HpRatio(t) > s.requireTargetHpBelow) return false;
                if (s.self != null && f.Has(s.self.status)) return false;
                if (s.kind == "move") return dist == 0 && !f.Has("bound");
                if (s.kind == "attack" || s.kind == "control")
                {
                    if (s.apply != null && t.Has(s.apply.status)) return false;
                    if (s.closeIn) return dist > 0 && (s.fromAnyRange || dist == 1) && !f.Has("bound");
                    if (s.ranged) return true;
                    if (s.useGrapple) return dist == 0;
                    return dist <= Reach(f);
                }
                return true;
            }

            double ScoreSkill(Fighter f, SkillDef s)
            {
                var t = Opp(f);
                double score = 1.2;
                string st = f.stance;
                if (s.kind == "attack") score += st == "assault" ? 0.5 : st == "blitz" ? 0.8 : st == "bulwark" ? 0 : 0.3;
                if (s.kind == "control") score += st == "harass" ? 0.9 : 0.6;
                if (s.kind == "self") score += st == "bulwark" ? 0.7 : 0.1;
                if (s.kind == "move") score += st == "harass" ? 0.8 : 0;
                // 기술 순서: 앞일수록 먼저
                int idx = f.doctrine.order.IndexOf(s.id);
                if (idx >= 0) score += (4 - idx) * 0.25;
                if (f.priorityStake == s.id) score += 5;
                if (s.id == "skill.catch_breath") score = f.sta < f.maxSta * 0.25 ? 3 : -1;
                if (s.id == "skill.taunt") score = st == "showman" ? 2.5 : HpRatio(f) > 0.6 && HpRatio(t) < 0.5 ? 1.2 : 0.2;
                if (s.id == "skill.flourish") score = 3;
                if (s.id == "skill.execute") score = 3;
                if (s.id == "skill.ward_rune" || s.id == "skill.iron_wall") score += HpRatio(f) < 0.5 ? 0.8 : 0;
                if (s.id == "skill.net_throw") score = 2.2;
                if (s.id == "skill.sand_throw") score = st == "showman" ? 0 : 1;
                if (!string.IsNullOrEmpty(s.race)) score += 0.4;
                return score;
            }

            /// <summary>사거리가 닿는 한에서 교범이 원하는 거리</summary>
            double DesiredRange(Fighter f, double styleRange)
            {
                string pref = f.doctrine.range;
                double want = pref == "style" ? styleRange : pref == "close" ? 0 : pref == "mid" ? 1 : 2;
                // 실제로 쓸 수 있는 원거리 수단이 남아 있을 때만 멀리 선다
                bool hasRanged = f.g.skills.active.Any(id =>
                {
                    var sk = Data.Skill(id);
                    return sk != null && sk.ranged && (sk.mana ?? 0) <= f.mana && (!sk.useAmmo || f.ammo > 0) && (!sk.requireNet || f.netReady);
                });
                return Math.Min(want, hasRanged ? 2 : Reach(f));
            }

            /// <summary>조건 명령 판정. 숙련도와 판단력이 낮으면 상황을 늦게 알아챈다</summary>
            RuleAction CheckRules(Fighter f)
            {
                var t = Opp(f);
                double notice = Math.Min(1, 0.4 + f.doctrine.familiarity / 200 + Attr(f, "dec") / 50);
                for (int i = 0; i < f.doctrine.rules.Count; i++)
                {
                    var cond = f.doctrine.rules[i].cond;
                    var action = f.doctrine.rules[i].action;
                    bool met = cond.type switch
                    {
                        "myHpBelow" => HpRatio(f) * 100 <= cond.value,
                        "oppHpBelow" => HpRatio(t) * 100 <= cond.value,
                        "staBelow" => f.sta / f.maxSta * 100 <= cond.value,
                        "oppDisabled" => t.Has("bound") || t.Has("prone") || t.Has("stun"),
                        _ => round >= cond.value,
                    };
                    if (!met || !rng.Chance(notice)) continue;
                    // 자세 변경은 한 번만. 기술·휴식·접근은 조건이 맞는 동안 계속 적용
                    if (action.type == "stance")
                    {
                        if (f.firedRules.Contains(i)) continue;
                        f.firedRules.Add(i);
                        f.stance = action.stance;
                        Emit(new CombatEvent { t = "doctrine", actor = f.g.id, action = action });
                        continue;
                    }
                    if (action.type == "skill")
                    {
                        if (f.priorityStake != action.skill) Emit(new CombatEvent { t = "doctrine", actor = f.g.id, action = action });
                        f.priorityStake = action.skill;
                        return action;
                    }
                    if (!f.firedRules.Contains(i))
                    {
                        f.firedRules.Add(i);
                        Emit(new CombatEvent { t = "doctrine", actor = f.g.id, action = action });
                    }
                    return action;
                }
                f.priorityStake = null;
                return null;
            }

            void Act(Fighter f)
            {
                var t = Opp(f);
                var options = new List<(double score, Action run)>();
                double noise = (21 - Attr(f, "dec")) / 20 * 0.8;
                var style = Data.Style(f.g.style);
                var ruleAction = CheckRules(f);
                if (ruleAction?.type == "rest")
                {
                    var breath = Data.Skill("skill.catch_breath");
                    if (f.g.skills.active.Contains(breath.id) && SkillUsable(f, breath)) UseSkill(f, t, breath);
                    else Rest(f);
                    return;
                }
                if (ruleAction?.type == "close" && dist > 0 && !f.Has("bound"))
                {
                    Move(f, -1);
                    return;
                }

                foreach (var sid in f.g.skills.active)
                {
                    var s = Data.Skill(sid);
                    if (s == null || !SkillUsable(f, s)) continue;
                    options.Add((ScoreSkill(f, s) + rng.Next() * noise, () => UseSkill(f, t, s)));
                }
                if (dist <= Reach(f))
                {
                    double aggr = (style.aggression + Attr(f, "agg") / 40) * StanceMult[f.stance];
                    options.Add((0.6 + aggr + rng.Next() * noise, () => BasicAttack(f, t)));
                }
                if (!f.Has("bound"))
                {
                    double desired = DesiredRange(f, style.preferredRange);
                    double keepAway = f.stance == "harass" ? 0.2 : 0;
                    if (dist > Math.Max(Reach(f), desired)) options.Add((1.5 + rng.Next() * noise, () => Move(f, -1)));
                    else if (dist < desired) options.Add((0.9 + keepAway + rng.Next() * noise, () => Move(f, 1)));
                }
                if (Arena.Enabled && !f.Has("bound"))
                {
                    // 빈사에 투지가 약하면 등을 보이고 달아난다 (돌격형 자세는 버틴다)
                    if (dist < 2 && HpRatio(f) < 0.3 && Attr(f, "grit") < 12 && f.stance != "assault" && f.stance != "blitz")
                        options.Add((1.2 + (0.3 - HpRatio(f)) * 4 + rng.Next() * noise, () => Move(f, 1, null, "flee")));
                    // 벽을 등졌으면 거리를 둔 채 가운데로 돌아 들어간다
                    if (dist == 2 && WallNorm(f) > 0.7 && WallNorm(f) > WallNorm(t) + 0.15)
                        options.Add((1.0 + Attr(f, "dec") * 0.02 + rng.Next() * noise, () => Move(f, 0, null, "center")));
                    // 붙어 있어도 벽에 몰렸으면 옆으로 빠져나가려 한다 (판단력이 좋을수록)
                    else if (dist < 2 && Cornered(f, t))
                        options.Add((0.9 + Attr(f, "dec") * 0.03 + rng.Next() * noise, () => Move(f, 0, null, "center")));
                }
                if (f.sta < f.maxSta * 0.15) options.Add((1.4, () => Rest(f)));
                if (options.Count == 0) options.Add((0, () => Rest(f)));

                // JS sort처럼 안정 정렬
                options.OrderByDescending(o => o.score).First().run();
            }

            /// <summary>이동. dir -1 다가섬, +1 물러남, 0 거리는 두고 자리만 바꿈. kind: "flee"(달아남), "center"(가운데 잡기)</summary>
            void Move(Fighter f, int dir, string skill = null, string kind = null)
            {
                int i = f.idx;
                int target = kind == "flee" ? 2 : Math.Max(0, Math.Min(2, dist + dir));
                double gap = Arena.Gap[target];
                var u = Away(i);
                if (dir < 0) Approach(i, gap);
                // 물러날 때·자리를 바꿀 때는 벽을 등지지 않는 쪽으로 비스듬히 돈다 (벽에 막히면 덜 물러난다 = 몰림)
                else if (kind == "center") StandAt(i, Math.Max(gap, Sep()), Rot(u, 60), Rot(u, -60));
                else StandAt(i, gap, u, Rot(u, 35), Rot(u, -35));
                // 좌표 규칙을 끄면 거리 단계는 예전처럼 한 칸씩만 바뀐다
                if (!Arena.Enabled) dist = target;
                f.sta = Math.Max(0, f.sta - Bal.C("moveSta"));
                Emit(new CombatEvent { t = "move", actor = f.g.id, dist = dist, closer = dir < 0, skill = skill, flee = kind == "flee", center = kind == "center", pos = Pos() });
            }

            void Rest(Fighter f)
            {
                f.sta = Math.Min(f.maxSta, f.sta + f.maxSta * 0.15);
                Emit(new CombatEvent { t = "rest", actor = f.g.id });
            }

            void BasicAttack(Fighter f, Fighter t)
            {
                f.sta = Math.Max(0, f.sta - Bal.C("basicAttackSta") * (1 + (f.armor?.staCost ?? 0)));
                ResolveHit(f, t, BasicSpec(f));
            }

            void UseSkill(Fighter f, Fighter t, SkillDef s)
            {
                f.sta = Math.Max(0, f.sta - (s.cost ?? 0) * (1 + (f.armor?.staCost ?? 0)));
                f.mana -= s.mana ?? 0;
                f.SetCd(s.id, (s.cd ?? 0) + 1);
                if (!string.IsNullOrEmpty(s.race) && !f.usedRaceSkill)
                {
                    f.usedRaceSkill = true;
                    Emit(new CombatEvent { t = "highlight", kind = "race_skill", actors = new[] { f.g.id } });
                }
                if ((s.crowd ?? 0) != 0) AddCrowd(s.crowd.Value, f);

                if (s.kind == "move")
                {
                    Move(f, 1, s.id);
                    return;
                }
                if (s.kind == "self")
                {
                    Emit(new CombatEvent { t = "self", actor = f.g.id, skill = s.id });
                    if (s.self != null)
                    {
                        f.Set(s.self.status, s.self.turns);
                        if (s.self.status == "ward") f.wardHp = JsMath.Round(f.maxHp * 0.2);
                    }
                    if ((s.restoreSta ?? 0) != 0) f.sta = Math.Min(f.maxSta, f.sta + f.maxSta * s.restoreSta.Value);
                    if ((s.selfMomentum ?? 0) != 0) f.momentum = Math.Min(100, f.momentum + s.selfMomentum.Value);
                    if ((s.enemyMomentum ?? 0) != 0) t.momentum = Math.Max(0, t.momentum + s.enemyMomentum.Value);
                    return;
                }
                if (s.closeIn)
                {
                    Approach(f.idx, Arena.Gap[0]);
                    dist = 0;
                    Emit(new CombatEvent { t = "move", actor = f.g.id, dist = dist, closer = true, skill = s.id, pos = Pos() });
                }
                if (s.kind == "control")
                {
                    if (s.requireNet) f.netReady = false;
                    double chance = s.autoHit ? 1 : s.useGrapple
                        ? 0.5 + (Attr(f, "grp") - Attr(t, "grp")) * 0.04
                        : 0.6 + (Attr(f, "acc") - Attr(t, "eva")) * 0.03 + (s.magic ? 0.15 : 0);
                    if (rng.Chance(Math.Max(0.1, Math.Min(0.95, chance))))
                    {
                        if (s.apply != null) ApplyStatus(t, s.apply.status, s.apply.turns);
                        if (s.id == "skill.grapple") f.Set("grappling", 2);
                    }
                    else Emit(new CombatEvent { t = "attack", actor = f.g.id, target = t.g.id, skill = s.id, hit = false, damage = 0, part = "chest", hpAfter = t.hp, maxHp = t.maxHp, magic = s.magic });
                    return;
                }
                // attack
                if (s.useAmmo) f.ammo--;
                var weapon = s.useAmmo && (f.off?.ammo ?? 0) != 0 ? f.off : f.main;
                double hits = s.hits ?? 1;
                for (int i = 0; i < hits && t.hp > 0; i++)
                    ResolveHit(f, t, new AttackSpec
                    {
                        skill = s, weapon = weapon, mult = s.mult ?? 1, hitMod = s.hitMod ?? 0, pierceAdd = s.pierceAdd ?? 0,
                        critAdd = s.critAdd ?? 0, autoHit = s.autoHit, magicDamage = s.magicDamage,
                    });
            }

            /// <summary>턴 시작 처리. false면 이번 턴 행동 불가</summary>
            bool StartTurn(Fighter f)
            {
                foreach (var k in f.cdKeys) f.cooldowns[k] = Math.Max(0, f.cooldowns[k] - 1);
                bool canAct = true;
                if (f.Has("poison"))
                {
                    double dmg = Math.Max(1, JsMath.Round(f.maxHp * 0.03));
                    f.hp -= dmg;
                    f.damageTaken += dmg;
                    Opp(f).damageDealt += dmg;
                    Emit(new CombatEvent { t = "poison", target = f.g.id, damage = dmg, hpAfter = Math.Max(0, f.hp) });
                }
                if (f.Has("stun")) { Emit(new CombatEvent { t = "skip", actor = f.g.id, reason = "stun" }); canAct = false; }
                else if (f.Has("grappled"))
                {
                    double dmg = Math.Max(1, JsMath.Round(f.maxHp * 0.02));
                    DealDamage(Opp(f), f, dmg);
                    Emit(new CombatEvent { t = "skip", actor = f.g.id, reason = "grappled" });
                    canAct = false;
                }
                else if (f.Has("prone")) { Emit(new CombatEvent { t = "skip", actor = f.g.id, reason = "prone" }); canAct = false; }
                foreach (var k in f.statusKeys.ToList())
                {
                    int v = f.statuses[k];
                    if (v - 1 <= 0) f.Del(k);
                    else f.statuses[k] = v - 1;
                }
                if (!f.netReady && f.off?.id == "net" && dist >= 1 && canAct && rng.Chance(0.5)) f.netReady = true;
                if (HpRatio(f) < 0.3) f.momentum = Math.Max(0, f.momentum - 3);
                return canAct;
            }

            bool CheckKo()
            {
                foreach (var f in F)
                {
                    if (f.hp <= 0)
                    {
                        var lastHit = events.LastOrDefault(e => e.t == "attack" && e.target == f.g.id);
                        bool crit = lastHit != null && lastHit.crit;
                        ended = (Opp(f), f, "ko", crit);
                        f.hp = 0;
                        Emit(new CombatEvent { t = "ko", target = f.g.id, crit = crit });
                        AddCrowd(10, Opp(f));
                        return true;
                    }
                }
                return false;
            }

            bool CheckSurrender(Fighter f)
            {
                if (!f.doctrine.allowSurrender || f.momentum > Bal.C("surrenderMomentum") || HpRatio(f) > Bal.C("surrenderHpRatio")) return false;
                double grit = Attr(f, "grit");
                if (grit >= 20) return false;
                if (rng.Chance((20 - grit) * Bal.C("surrenderPerGrit")))
                {
                    Emit(new CombatEvent { t = "surrender", actor = f.g.id });
                    AddCrowd(-8, f);
                    ended = (Opp(f), f, "surrender", false);
                    return true;
                }
                return false;
            }

            public MatchResult Play()
            {
                Emit(new CombatEvent { t = "start", a = a.id, b = b.id, rule = opt.rule, pos = Pos() });
                Emit(new CombatEvent { t = "highlight", kind = "entrance", actors = new[] { a.id, b.id } });
                int maxRounds = (int)Bal.C("maxRounds");

                for (round = 1; round <= maxRounds && ended == null; round++)
                {
                    Orbit();
                    Emit(new CombatEvent { t = "round", round = round, pos = Pos() });
                    foreach (var f in F) f.mind.late = (f.mind.adapt - 8) * 0.01 * Math.Min(round, 20);
                    var init = F.Select(f =>
                    {
                        // 공격권: 타이밍이 가장 크고, 무너진 쪽은 선공을 잡기 어렵다
                        var o = F[1 - f.idx];
                        double v = Attr(f, "timing") + Attr(f, "rea") / 2 + Attr(f, "agi") / 2 - f.main.speed * 2 + rng.Next() * 8 - (f.Has("exposed") ? 6 : 0)
                            // 상대가 무너졌거나 지쳤으면 타이밍 좋은 쪽이 파고든다
                            + (o.Has("exposed") || o.sta <= 0 ? Attr(f, "timing") * 0.6 : 0);
                        if (round == 1 && f.g.race == "beastkin") v += 5;
                        return v;
                    }).ToArray();
                    var order = init[0] >= init[1] ? new[] { F[0], F[1] } : new[] { F[1], F[0] };
                    double dmgBefore = F[0].damageTaken + F[1].damageTaken;

                    foreach (var f in order)
                    {
                        if (ended != null) break;
                        bool canAct = StartTurn(f);
                        if (CheckKo()) break;
                        if (CheckSurrender(f)) break;
                        if (canAct) Act(f);
                        if (CheckKo()) break;
                        // 단검처럼 빠른 무기는 추가 공격 기회
                        if (canAct && f.main.speed <= 1 && dist <= Reach(f) && rng.Chance(0.2))
                        {
                            BasicAttack(f, Opp(f));
                            if (CheckKo()) break;
                        }
                    }
                    foreach (var f in F) f.sta = Math.Min(f.maxSta, f.sta + Attr(f, "end") * Bal.C("staRegenPerEnd") + 2);

                    if (F[0].damageTaken + F[1].damageTaken == dmgBefore)
                    {
                        stall++;
                        if (stall >= 5) AddCrowd(-3);
                    }
                    else stall = 0;
                }

                if (ended == null)
                {
                    Emit(new CombatEvent { t = "timeout" });
                    double Score(Fighter f) => f.damageDealt + f.crowdContribution * 0.5;
                    var w = Score(F[0]) >= Score(F[1]) ? F[0] : F[1];
                    ended = (w, Opp(w), "timeout", false);
                }
                var (winner, loser, reason, crit) = ended.Value;

                string died = null;
                if (reason != "timeout" && !(reason == "surrender" && opt.rule == "training"))
                {
                    double medical = opt.medicalLevel?.ElementAtOrDefault(loser.idx) ?? 0;
                    double deathChance;
                    if (opt.rule == "training") deathChance = crit ? Bal.D("trainingCritKo") : Bal.D("trainingKo");
                    else if (opt.rule == "missio")
                    {
                        deathChance = Bal.D("missioBase") - loser.g.popularity * Bal.D("missioPerPopularity") - Math.Max(0, loser.crowdContribution) * Bal.D("missioPerCrowd");
                        if (loser.HasPassive("skill.crowd_hand")) deathChance -= Bal.D("missioCrowdHand");
                        deathChance = Math.Max(Bal.D("missioMin"), Math.Min(Bal.D("missioMax"), deathChance)) * (opt.missioDeathMult ?? 1);
                        Emit(new CombatEvent { t = "highlight", kind = "missio", actors = new[] { loser.g.id } });
                    }
                    else deathChance = Math.Max(Bal.D("suddenMin"), Bal.D("suddenBase") - medical * Bal.D("suddenPerMedical"));
                    bool isDead = !opt.noDeath && rng.Chance(deathChance);
                    Emit(new CombatEvent { t = "verdict", target = loser.g.id, rule = opt.rule, deathChance = deathChance, died = isDead });
                    if (isDead)
                    {
                        died = loser.g.id;
                        Emit(new CombatEvent { t = "highlight", kind = "death", actors = new[] { loser.g.id, winner.g.id } });
                    }
                }
                if (died == null && HpRatio(winner) <= 0.3) Emit(new CombatEvent { t = "highlight", kind = "comeback", actors = new[] { winner.g.id } });
                Emit(new CombatEvent { t = "end", winner = winner.g.id, loser = loser.g.id, reason = reason });

                var injuries = new Dictionary<string, Injury>();
                foreach (var f in F)
                {
                    if (f.g.id == died) continue;
                    injuries[f.g.id] = RollInjury(rng, f, reason == "ko" && f == loser, (double)Bal.Rule(opt.rule)["injuryMult"]);
                }

                return new MatchResult
                {
                    events = events,
                    winnerId = winner.g.id,
                    loserId = loser.g.id,
                    reason = reason,
                    died = died,
                    rounds = Math.Min(round, maxRounds),
                    crowd = crowd,
                    damageDealt = new() { [a.id] = F[0].damageDealt, [b.id] = F[1].damageDealt },
                    crowdContribution = new() { [a.id] = F[0].crowdContribution, [b.id] = F[1].crowdContribution },
                    injuries = injuries,
                    hpLeft = new() { [a.id] = Math.Max(0, F[0].hp / F[0].maxHp), [b.id] = Math.Max(0, F[1].hp / F[1].maxHp) },
                };
            }
        }

        static Injury RollInjury(Rng rng, Fighter f, bool knockedOut, double mult)
        {
            double proneMult = f.g.hidden.injuryProne / 10.0 * (f.g.traits.Contains("injury_prone") ? 1.5 : 1);
            double p = (f.damageTaken / f.maxHp * 0.35 * proneMult + (knockedOut ? 0.25 : 0)) * mult;
            if (!rng.Chance(p)) return null;
            // 드워프는 강철 뼈 덕에 크게 다치는 일이 적다
            double tough = f.g.race == "dwarf" ? 0.6 : 1;
            double severe = (0.05 + (knockedOut ? 0.08 : 0)) * tough;
            double moderate = (0.2 + (knockedOut ? 0.15 : 0)) * tough;
            double roll = rng.Next();
            string tier = roll < severe ? "severe" : roll < severe + moderate ? "moderate" : "minor";
            return GladiatorRules.MakeInjury(rng, tier, GladiatorRules.AreaOf(f.worstPart));
        }
    }
}
