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
        /// <summary>모래 마당 가운데: 탑뷰 경기장 그림(Resources/Art/ui/arena_crowd.png)의 모래 타원 가운데에 맞췄다</summary>
        public const double CX = 197, CY = 77;
        /// <summary>말이 설 수 있는 타원 (그림의 모래 타원 반지름 162×60에서 말 반지름만큼 안쪽)</summary>
        public const double RX = 146, RY = 46;
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
        /// <summary>난전 단체 전술 [편0, 편1]. null이면 예전처럼 가까운·약한 적</summary>
        public TeamTactic[] teamTactics;
        /// <summary>관중이 없는 싸움 (던전). 규칙이 미시오여도 관중 사건이 나오지 않는다</summary>
        public bool noCrowd;
        /// <summary>검투사별로 정한 쓰러졌을 때 죽을 확률 (던전). 없는 사람은 규칙대로</summary>
        public Dictionary<string, double> deathChanceOf;
    }

    public class MatchResult
    {
        public List<CombatEvent> events;
        public string winnerId, loserId, reason, died;
        public int rounds;
        public double crowd;
        public Dictionary<string, double> damageDealt, crowdContribution, hpLeft;
        public Dictionary<string, Injury> injuries;

        // ── 난전(Unity판 전용, Sim.RunTeam)만 채운다. 1:1이면 null ──
        /// <summary>양쪽 검투사 id. 경기장 좌표 pos는 sides[0] 다음 sides[1] 순서</summary>
        public List<string>[] sides;
        /// <summary>이긴 쪽 0 | 1</summary>
        public int winnerSide = -1;
        /// <summary>쓰러진 순서대로 (쓰러진 자 → 쓰러뜨린 자)</summary>
        public List<KeyValuePair<string, string>> downs;
        /// <summary>죽은 검투사들 (died는 그중 첫 번째)</summary>
        public List<string> deaths;
        public bool IsTeam => sides != null;
        /// <summary>Unity판: 평점 매니저가 붙인 출전자별 기록과 평점 (Logic/Ratings.cs)</summary>
        public Dictionary<string, FighterStats> stats;
    }

    class Fighter
    {
        public Gladiator g;
        public int idx;
        /// <summary>편 (1:1이면 idx와 같다)</summary>
        public int side;
        /// <summary>노리는 상대 (1:1이면 맞은편)</summary>
        public Fighter target;
        /// <summary>난전에서 쓰러졌다</summary>
        public bool down;
        public double hp, maxHp, sta, maxSta, mana, momentum;
        // 순서가 있는 상태/쿨다운 (JS Map처럼 넣은 순서를 지킨다)
        public List<string> statusKeys = new();
        public Dictionary<string, int> statuses = new();
        public List<string> cdKeys = new();
        public Dictionary<string, double> cooldowns = new();
        public WeaponDef main, off;
        public ArmorDef shield, armor, helmet;
        /// <summary>Unity판: 팔·다리 보호구, 고유 장비 효과 합 (Uniques)</summary>
        public ArmorDef arms, legs;
        public Dictionary<string, double> fx = new();
        public double Fx(string k) => fx.TryGetValue(k, out var v) ? v : 0;
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
        /// <summary>사건에 반응하는 기술 (기술 테이블). 없으면 null</summary>
        public List<SkillBook.SkillRow> reactions;
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

        /// <summary>장비 고유 효과 + 상시 기술 보정치를 더한다 (기술이 없으면 장비 사전 그대로)</summary>
        static Dictionary<string, double> MergeFx(Dictionary<string, double> a, Dictionary<string, double> b)
        {
            if (b.Count == 0) return a;
            var o = new Dictionary<string, double>(a);
            foreach (var kv in b) o[kv.Key] = (o.TryGetValue(kv.Key, out var v) ? v : 0) + kv.Value;
            return o;
        }

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
                arms = Uniques.Enabled && g.equipment.arms != null ? Data.Armor(g.equipment.arms) : null,
                legs = Uniques.Enabled && g.equipment.legs != null ? Data.Armor(g.equipment.legs) : null,
                reactions = SkillBook.Reactions(g),
                fx = MergeFx(Uniques.Enabled ? Uniques.Sum(g.equipment.Items()) : new Dictionary<string, double>(), SkillBook.PassiveFx(g)),
                ammo = offW?.ammo ?? main.ammo ?? 0,
                netReady = offW?.id == "net",
                doctrine = doctrine, stance = doctrine.stance,
                perf = GladiatorRules.Performance(g),
                habits = Exchange.HabitsOf(g, main.@class, doctrine.focus, offA?.slot == "shield", g.race == "dwarf" ? 0 : armor?.agiPenalty ?? 0, E("agg"), E("genius"), E("blk"), E("eva")),
                mind = new Mind { read = E("read"), feint = E("feint"), genius = E("genius"), adapt = E("adapt"), dec = E("dec"), rea = E("rea"), comp = E("comp") },
            };
        }

        /// <summary>상태이상을 반영한 전투 중 능력치</summary>
        // ── FM식 전투 보정 (Unity판 전용, FmRules.Enabled): 능력치만으로 승부가 나지 않게 ──

        /// <summary>그날의 컨디션: 일관성이 낮을수록 들쭉날쭉, 큰 무대(관중 있는 경기)에 강한 자는 더 잘한다</summary>
        static double DayForm(Rng rng, Gladiator g, string rule)
        {
            double sigma = 0.11 + (20 - g.hidden.consistency) * 0.005;
            double z = (rng.Next() + rng.Next() + rng.Next() - 1.5) * 2; // 대략 정규분포 (표준편차 1)
            double stage = rule == "training" ? 0 : (g.hidden.bigStage - 10) * 0.004;
            return Math.Max(0.65, Math.Min(1.4, 1 + z * sigma + stage));
        }

        /// <summary>자세끼리 상성 (교범 가위바위보): 맹공은 견제를 덮치고, 견제는 철벽을 갉아먹고, 철벽은 맹공을 받아친다.
        /// 질풍은 균형·쇼를 흔들지만 철벽에 막힌다. 쇼는 맹공에 약하다</summary>
        static double StanceEdge(string a, string d) => (a, d) switch
        {
            ("assault", "harass") or ("harass", "bulwark") or ("bulwark", "assault") => 0.085,
            ("harass", "assault") or ("bulwark", "harass") or ("assault", "bulwark") => -0.07,
            ("blitz", "balanced") or ("blitz", "showman") or ("assault", "showman") => 0.07,
            ("blitz", "bulwark") => -0.08,
            ("showman", "assault") or ("showman", "blitz") => -0.05,
            _ => 0,
        };

        /// <summary>공격성이 침착보다 너무 높으면 무모하다 (빈틈이 생긴다)</summary>
        static double Reckless(Fighter f) => Math.Max(0, Attr(f, "agg") - Attr(f, "comp") - 3);

        /// <summary>명중 보정: 민첩·반응이 회피를 돕고, 공격성은 지치지 않았을 때만 밀어붙이는 힘, 무모하면 빈틈, 상성·자세 상성</summary>
        static double FmHit(Fighter att, Fighter def, WeaponDef w)
        {
            double h = 0;
            // 민첩: 몸놀림으로 피하고(크게), 빈틈을 파고든다(작게)
            h -= (Attr(def, "agi") - 10) * 0.016;
            h += (Attr(att, "agi") - 10) * 0.006;
            // 반응: 빠른 무기일수록 반응이 좋아야 따라간다
            h -= (Attr(def, "rea") - 10) * 0.004 * (1 + Math.Max(0, w.speed) * 0.25);
            // 공격성: 기력이 남았을 때 밀어붙인다
            double fresh = att.maxSta > 0 ? Math.Max(0, att.sta / att.maxSta) : 1;
            h += (Attr(att, "agg") - 10) * 0.007 * fresh;
            // 무모함: 공격성 > 침착이면 받아치기에 걸린다
            h += Reckless(def) * 0.01;
            // 판단력·예측력: 노련한 자는 같은 수에 두 번 안 당한다
            h -= (Attr(def, "dec") - 10) * 0.004 + (Attr(def, "read") - 10) * 0.007;
            // 타이밍: 상대 호흡이 끊기는 순간
            h += (Attr(att, "timing") - 10) * 0.004;
            // 정확도·회피 차이의 기울기를 조금 눕힌다 (웹판 3%/점 → 2.2%/점)
            h -= (Attr(att, "acc") - Attr(def, "eva")) * 0.008;
            // 스타일 상성 강화, 교범 자세 상성
            if (Data.Style(att.g.style).counters.Contains(def.g.style)) h += 0.05;
            if (Data.Style(def.g.style).counters.Contains(att.g.style)) h -= 0.04;
            h += StanceEdge(att.stance, def.stance);
            return h;
        }

        /// <summary>피해 보정: 공격성이 실은 힘, 상성, 강인함이 버티는 힘</summary>
        static double FmDamage(Fighter att, Fighter def)
        {
            double m = 1 + (Attr(att, "agg") - 10) * 0.012;
            if (Data.Style(att.g.style).counters.Contains(def.g.style)) m *= 1.08;
            m *= 1 - (Attr(def, "tou") - 10) * 0.008;
            return Math.Max(0.6, m);
        }

        static double Attr(Fighter f, string a)
        {
            double v = GladiatorRules.EffectiveAttr(f.g, a);
            if (f.Has("weakened") && (a == "str" || a == "acc")) v -= 3;
            if (f.Has("hobbled") && (a == "agi" || a == "eva")) v -= 4;
            if (a == "agi" && f.armor != null && f.g.race != "dwarf") v -= f.armor.agiPenalty ?? 0;
            if (a == "agi" && f.g.race != "dwarf") v -= (f.arms?.agiPenalty ?? 0) + (f.legs?.agiPenalty ?? 0);
            if (a == "aura") v += f.Fx("aura");
            if (a == "acc" && f.helmet?.accPenalty is double ap && ap != 0) v -= ap;
            if (a == "show" && f.helmet?.showBonus is double sb && sb != 0) v += sb;
            return Math.Max(1, v);
        }

        public static MatchResult Run(Gladiator a, Gladiator b, MatchOptions opt) => new Match(a, b, opt).Play();

        /// <summary>단체전 난전 (Unity판 전용): 양쪽 전원이 한 경기장에서 동시에 싸운다.
        /// 각자 가까운·약한 적을 노리고, 쓰러지면 그 자리에서 생사를 판정한다. 한쪽이 전멸하거나 마지막 한 명이 항복하면 끝.
        /// medicalLevel은 편마다 [0쪽, 1쪽]</summary>
        public static MatchResult RunTeam(List<Gladiator> side0, List<Gladiator> side1, MatchOptions opt) => new Match(side0, side1, opt).PlayTeam();

        sealed class Match
        {
            readonly Gladiator a, b;
            readonly MatchOptions opt;
            readonly Rng rng;
            readonly Fighter[] F;
            readonly List<CombatEvent> events = new();
            /// <summary>난전인가 (사람 수와 상관없이 RunTeam으로 불렀으면)</summary>
            readonly bool team;
            int dist2 = 2;
            /// <summary>지금 맞붙은 둘 (행동하는 쪽과 그 상대). 거리 단계·좌표 계산의 기준</summary>
            Fighter ctxA, ctxB;
            /// <summary>거리 단계. 1:1은 저장한 값, 난전은 지금 맞붙은 둘의 실제 거리에서 낸다</summary>
            int dist
            {
                get => team ? Arena.Bucket(SepOf(ctxA.idx, ctxB.idx)) : dist2;
                set { if (!team) dist2 = value; }
            }
            /// <summary>경기장 좌표 (F 순서)</summary>
            readonly double[] px, py;
            /// <summary>난전 단체 전술 [편0, 편1]과 각자의 능력(CA). 1:1이면 null</summary>
            readonly TeamTactic[] tac;
            readonly double[] power;
            readonly List<KeyValuePair<string, string>> downs = new();
            readonly List<string> deaths = new();
            int? winSide;
            string teamReason;
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
                F[0].side = 0; F[1].side = 1;
                F[0].target = F[1]; F[1].target = F[0];
                ctxA = F[0]; ctxB = F[1];
                px = new[] { Arena.Start[0], Arena.Start[2] };
                py = new[] { Arena.Start[1], Arena.Start[3] };
                for (int i = 0; i < 2; i++)
                {
                    var f = F[i];
                    var r = opt.startHp?.ElementAtOrDefault(i);
                    if (r != null) f.hp = Math.Max(1, JsMath.Round(f.maxHp * r.Value));
                    // 일관성이 낮으면 그날그날 읽는 감이 들쭉날쭉하다
                    f.mind.form = (rng.Next() - 0.5) * (21 - f.g.hidden.consistency) * 0.06;
                    if (FmRules.Enabled) f.perf *= DayForm(rng, f.g, opt.rule);
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

            /// <summary>난전: 양쪽이 마당 왼쪽·오른쪽에 줄지어 선다</summary>
            public Match(List<Gladiator> side0, List<Gladiator> side1, MatchOptions opt)
            {
                team = true;
                this.opt = opt;
                a = side0[0]; b = side1[0];
                rng = new Rng(opt.seed);
                var all = side0.Select(g => (g, side: 0)).Concat(side1.Select(g => (g, side: 1))).ToList();
                F = all.Select((x, i) =>
                {
                    var f = MakeFighter(x.g, i, x.g.doctrine ?? GladiatorRules.DefaultDoctrine(x.g));
                    f.side = x.side;
                    // 앞선 싸움에서 깎인 체력을 이어 간다 (던전)
                    var r = opt.startHp?.ElementAtOrDefault(i);
                    if (r != null) f.hp = Math.Max(1, JsMath.Round(f.maxHp * r.Value));
                    return f;
                }).ToArray();
                px = new double[F.Length];
                py = new double[F.Length];
                foreach (var f in F)
                {
                    var mates = F.Where(o => o.side == f.side).ToList();
                    int k = mates.IndexOf(f);
                    double spread = Math.Min(30, 80.0 / Math.Max(1, mates.Count - 1));
                    px[f.idx] = Arena.CX + (f.side == 0 ? -75 : 75) + (k % 2 == 0 ? 0 : (f.side == 0 ? -14 : 14));
                    py[f.idx] = Arena.CY + (k - (mates.Count - 1) / 2.0) * spread;
                    Clamp(f.idx);
                    f.mind.form = (rng.Next() - 0.5) * (21 - f.g.hidden.consistency) * 0.06;
                    if (FmRules.Enabled) f.perf *= DayForm(rng, f.g, opt.rule);
                }
                // 위압감: 맞은편에서 가장 무서운 자에게 기세가 꺾인다
                foreach (var o in F)
                {
                    double d = F.Where(f => f.side != o.side).Max(f => Math.Max(0, GladiatorRules.EffectiveAttr(f.g, "aura") - GladiatorRules.EffectiveAttr(o.g, "comp")));
                    o.momentum = Math.Max(0, o.momentum - d / 2 * (GladiatorRules.EffectiveAttr(o.g, "grit") < 10 ? 2 : 1));
                }
                // 단체 전술: 편마다 역할·방침. 강한 적 판단용 능력(CA)은 한 번만 잰다
                tac = opt.teamTactics;
                power = F.Select(f => (double)GladiatorRules.ComputeCA(f.g)).ToArray();
                foreach (var f in F) f.target = PickTarget(f);
                ctxA = F[0]; ctxB = F[0].target;
                crowd = Bal.C("crowdStart");
                hasCrowd = (bool)Bal.Rule(opt.rule)["crowd"] && !opt.noCrowd;
            }

            void Emit(CombatEvent e) => events.Add(e);

            // ───────── 난전: 상대 고르기 ─────────

            IEnumerable<Fighter> Standing(int side) => F.Where(f => f.side == side && !f.down);

            /// <summary>가까운 적, 약한 적을 노린다. 돌격형 자세는 빈사인 적을 끝내러 가고, 이미 노리던 적은 웬만하면 계속 노린다</summary>
            Fighter PickTarget(Fighter f)
            {
                Fighter best = null;
                double bestScore = double.MaxValue;
                bool hunter = f.stance == "assault" || f.stance == "blitz" || Attr(f, "kill") >= 14;
                var t = tac?.ElementAtOrDefault(f.side);
                if (t != null) return PickByTactic(f, t, hunter);
                foreach (var e in Standing(1 - f.side))
                {
                    double s = SepOf(f.idx, e.idx) + HpRatio(e) * (hunter ? 60 : 25);
                    // 여럿이 한 명에게 몰리지 않게 조금 흩어진다
                    s += F.Count(m => m != f && !m.down && m.side == f.side && m.target == e) * 12;
                    if (e == f.target) s -= 25;
                    if (s < bestScore) { bestScore = s; best = e; }
                }
                return best;
            }

            /// <summary>
            /// 단체 전술대로 상대 고르기 (점수가 낮을수록 좋다). 거리는 늘 조금 본다.
            /// 방침: 집중(몰릴수록 좋고 첫 목표 우선) / 분산(몰리면 크게 손해) / 약한 적(HP) / 강한 적(능력).
            /// 역할: 전열(가까운 적) / 사냥꾼(빈사) / 호위(지킬 동료를 노리는 적) / 견제(아무도 안 붙은 강한 적).
            /// </summary>
            Fighter PickByTactic(Fighter f, TeamTactic t, bool hunterStance)
            {
                string role = t.roles != null && t.roles.TryGetValue(f.g.id, out var r) ? r : "hunter";
                var enemies = Standing(1 - f.side).ToList();
                if (enemies.Count == 0) return null;
                double maxPow = enemies.Max(e => power[e.idx]);
                // 호위: 지정한 동료, 없으면 서 있는 동료 중 가장 약한 자
                Fighter ward = null;
                if (role == "guard")
                {
                    string wid = t.guardOf != null && t.guardOf.TryGetValue(f.g.id, out var w) ? w : null;
                    ward = F.FirstOrDefault(m => m.side == f.side && !m.down && m != f && m.g.id == wid)
                        ?? F.Where(m => m.side == f.side && !m.down && m != f).OrderBy(HpRatio).FirstOrDefault();
                }
                // 집중 공격의 첫 목표: 지정한 적, 없으면 가장 강한 적
                var focus = t.policy == "focus" ? enemies.FirstOrDefault(e => e.g.id == t.focusTarget) ?? enemies.OrderByDescending(e => power[e.idx]).First() : null;

                Fighter best = null;
                double bestScore = double.MaxValue;
                foreach (var e in enemies)
                {
                    int crowding = F.Count(m => m != f && !m.down && m.side == f.side && m.target == e);
                    double strength = maxPow > 0 ? power[e.idx] / maxPow : 1;
                    double s = SepOf(f.idx, e.idx) * (role == "front" ? 1.6 : 0.7);
                    s += t.policy switch
                    {
                        "focus" => -crowding * 18 + (e == focus ? -60 : 0),
                        "spread" => crowding * 30,
                        "weak" => HpRatio(e) * 70 + crowding * 8,
                        "strong" => (1 - strength) * 70 + crowding * 8,
                        _ => crowding * 12,
                    };
                    s += role switch
                    {
                        "hunter" => HpRatio(e) * (hunterStance ? 70 : 50),
                        "guard" => ward != null && e.target == ward ? -90 : 25,
                        "harass" => crowding * 40 - strength * 30,
                        _ => 0,
                    };
                    if (e == f.target) s -= 20;
                    if (s < bestScore) { bestScore = s; best = e; }
                }
                return best;
            }

            /// <summary>행동할 사람과 그 상대를 맞붙은 둘로 둔다</summary>
            void Engage(Fighter f)
            {
                ctxA = f;
                ctxB = f.target;
            }

            /// <summary>난전에서 서로 겹쳐 서지 않게 살짝 떼어 놓는다</summary>
            void Separate()
            {
                const double minGap = 22;
                for (int i = 0; i < F.Length; i++)
                    for (int j = i + 1; j < F.Length; j++)
                    {
                        if (F[i].down || F[j].down) continue;
                        double dx = px[j] - px[i], dy = py[j] - py[i], d = Math.Sqrt(dx * dx + dy * dy);
                        if (d >= minGap) continue;
                        if (d < 0.5) { dx = F[i].side == 0 ? 1 : -1; dy = 0.3; d = 1; }
                        double push = (minGap - d) / 2;
                        px[i] -= dx / d * push; py[i] -= dy / d * push;
                        px[j] += dx / d * push; py[j] += dy / d * push;
                        Clamp(i); Clamp(j);
                    }
            }

            // ───────── 경기장 좌표 ─────────

            double[] Pos()
            {
                var p = new double[F.Length * 2];
                for (int i = 0; i < F.Length; i++) { p[i * 2] = px[i]; p[i * 2 + 1] = py[i]; }
                return p;
            }
            double SepOf(int i, int j) => Math.Sqrt((px[i] - px[j]) * (px[i] - px[j]) + (py[i] - py[j]) * (py[i] - py[j]));
            double Sep() => SepOf(ctxA.idx, ctxB.idx);
            /// <summary>i의 맞상대: 맞붙은 둘 중 하나면 다른 하나, 아니면 i가 노리는 적</summary>
            int Other(int i) => !team ? 1 - i : ctxA.idx == i ? ctxB.idx : ctxB.idx == i ? ctxA.idx : F[i].target?.idx ?? i;
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
                int o = Other(i);
                double x = px[i] - px[o], y = py[i] - py[o], len = Math.Sqrt(x * x + y * y);
                return len < 1 ? (F[i].side == 0 ? -1 : 1, 0) : (x / len, y / len);
            }

            static (double x, double y) Rot((double x, double y) v, double deg)
            {
                double r = deg * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r);
                return (v.x * c - v.y * s, v.x * s + v.y * c);
            }

            /// <summary>상대 둘레의 어느 방향으로 설지 골라 간격 gap에 선다. 고른 방향 중 벽에서 가장 먼 쪽</summary>
            void StandAt(int i, double gap, params (double x, double y)[] dirs)
            {
                int o = Other(i);
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
                int o = Other(i);
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
            Fighter Opp(Fighter f) => team ? f.target : F[1 - f.idx];
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
                // 상시 기술 '쇠가죽' 같은 받는 피해 감소 (기술 테이블)
                if (def.Fx("taken") != 0) dmg = JsMath.Round(dmg * Math.Max(0.5, 1 + def.Fx("taken")));
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

            /// <summary>
            /// 반응 기술 (기술 테이블): 사건이 나면 그 trigger의 기술을 우선순위대로 하나만 굴린다.
            /// other = 사건 상대(적), ally = 대상 동료(엄호·쓰러진 동료). 반응 기술이 없으면 난수를 쓰지 않는다
            /// </summary>
            void React(Fighter f, string trigger, Fighter other, Fighter ally = null)
            {
                if (f.reactions == null || f.reactions.Count == 0 || f.down) return;
                foreach (var s in f.reactions.Where(r => r.trigger == trigger).OrderByDescending(r => r.priority))
                {
                    if ((f.cooldowns.TryGetValue(s.Id, out var cd) ? cd : 0) > 0 || s.sta > f.sta) continue;
                    if (s.require == "Shield" && f.shield == null) continue;
                    if (trigger == "OnAllyHpBelow" && (ally == null || HpRatio(ally) > s.triggerValue)) continue;
                    double p = s.procRate + (s.procStat != "None" ? Attr(f, s.procStat.ToLowerInvariant()) * s.procPerStat : 0);
                    if (!rng.Chance(Math.Max(SkillBook.C("ProcMin", 0.02), Math.Min(SkillBook.C("ProcMax", 0.85), p)))) continue;
                    f.sta -= s.sta;
                    f.SetCd(s.Id, s.cooldown);
                    Emit(new CombatEvent { t = "react", actor = f.g.id, skill = s.Id, target = (ally ?? other)?.g.id });
                    foreach (var e in SkillBook.EffectsOf(s))
                    {
                        if (e.chance < 1 && !rng.Chance(e.chance)) continue;
                        var who = e.target switch { "Self" => f, "Enemy" => other, "Ally" => ally, _ => null };
                        switch (e.effect)
                        {
                            case "Damage" when other != null && !other.down && other.hp > 0:
                                var spec = BasicSpec(f);
                                spec.mult = e.value;
                                spec.counter = true;
                                ResolveHit(f, other, spec, false);
                                break;
                            case "ApplyStatus" when who != null && !who.down:
                                ApplyStatus(who, SkillBook.StatusName(e.status), Math.Max(1, e.duration));
                                break;
                            case "Momentum":
                                foreach (var m in e.target == "AllAllies" ? Standing(f.side).ToList() : who != null ? new List<Fighter> { who } : new List<Fighter>())
                                    m.momentum = Math.Max(0, Math.Min(100, m.momentum + e.value));
                                break;
                            case "Disengage" when !f.Has("bound") && other != null:
                                Move(f, 1);
                                break;
                        }
                    }
                    return;
                }
            }

            /// <summary>한 합의 수 싸움. 근접 공격이고 방어자가 움직일 수 있을 때만</summary>
            bool CanExchange(Fighter def, AttackSpec spec) =>
                spec.magicDamage == null && !spec.autoHit && !spec.counter && !(spec.skill?.ranged ?? false) && spec.weapon.@class != "throwing"
                && !(def.Has("bound") || def.Has("prone") || def.Has("stun") || def.Has("grappled"));

            (string outcome, int motion, int adv, int dir) RunExchange(Fighter att, Fighter def, AttackSpec spec)
            {
                int? fix = spec.skill != null && Exchange.SkillMotion.TryGetValue(spec.skill.id, out var m) ? m : null;
                double feintBias = att.stance == "harass" || att.stance == "showman" ? 0.05 : 0;
                // 지대 상성: 공격자는 노릴 높이, 방어자는 막을 높이 지시대로 방향을 기울인다
                double[] atkBias = null, defBias = null;
                if (Exchange.Zones)
                {
                    atkBias = Exchange.AttackBias(att.doctrine.atkZone ?? "auto", Exchange.DefZones(def.main.@class, def.shield != null), Attr(att, "dec"));
                    defBias = Exchange.GuardBias(def.doctrine.guardZone ?? "auto", Exchange.AtkZones(spec.weapon.@class), Attr(def, "dec"));
                }
                var plan = Exchange.PlanAttack(rng, att.mind, att.habits, def.book, att.book, spec.weapon.@class, fix, def.mind.read, feintBias, atkBias);
                if (plan.feint != null) att.sta = Math.Max(0, att.sta - 4);
                double pressure = Math.Max(0, Attr(att, "aura") - Attr(def, "comp")) * 0.06
                    + (HpRatio(def) < 0.3 && def.mind.comp < 12 ? (12 - def.mind.comp) * 0.05 : 0)
                    + (def.Has("exposed") ? 0.5 : 0);
                var guard = Exchange.PlanGuard(rng, def.mind, def.habits, def.book, att.mind.feint, pressure, plan.feint == "ok" ? plan.shown : null, defBias);
                string outcome = Exchange.OutcomeOf(rng, plan, guard, Attr(def, "rea") * (FmRules.Enabled ? 2.5 : 1));
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
                    if (dist <= Reach(def) && rng.Chance(0.2 + Attr(def, "timing") * 0.04 + def.Fx("parryCounter")))
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
                    if (Exchange.Zones) hitChance += Exchange.ZoneHit(Exchange.AtkZones(w.@class), Exchange.DefZones(def.main.@class, def.shield != null), e.dir);
                }
                // 마무리 본능: 빈사·무너진 상대를 놓치지 않는다 (낮으면 오히려 머뭇거린다)
                bool finishing = HpRatio(def) < 0.3 || def.Has("exposed");
                if (finishing) hitChance += Attr(att, "kill") * 0.01 - (Attr(att, "kill") <= 6 ? 0.1 : 0);
                hitChance += (att.g.prof[w.@class] - 10) * 0.01 + spec.hitMod;
                if (Counters(att, def)) hitChance += Bal.C("counterHit");
                if (Counters(def, att)) hitChance -= Bal.C("counterEvade");
                if (att.momentum >= 80) hitChance += 0.1;
                hitChance += (att.perf - 1) * 0.5 - (def.perf - 1) * 0.3;
                hitChance += att.Fx("hit") - def.Fx("evade");
                if (FmRules.Enabled) hitChance += FmHit(att, def, w);
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
                    if (allowRiposte) React(def, "OnDodge", att);
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
                    blockChance += def.Fx("block");
                    blocked = rng.Chance(Math.Max(0, Math.Min(0.85, blockChance)));
                }

                double raw;
                double critChance = Attr(att, "acc") * Bal.C("critPerAcc") + w.crit + spec.critAdd + (att.doctrine.focus == "head" ? 0.1 : 0) + att.Fx("crit");
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
                raw *= 1 + att.Fx("dmg") + (HpRatio(att) <= 0.3 ? att.Fx("fury") : 0);
                if (def.g.race == "orc" && HpRatio(def) <= 0.3) raw *= 1.1;
                bool crit = !blocked && rng.Chance(critChance);

                double defense = 0;
                if (!magic)
                {
                    defense = (def.armor?.defense ?? 0) + (def.helmet?.defense ?? 0);
                    if (part == "head" || part == "neck") defense = (def.helmet?.defense ?? 0) + 1;
                    // 팔·다리 보호구는 그 부위를 맞을 때만 막는다
                    else if (part == "arm" || part == "shoulder") defense += def.arms?.defense ?? 0;
                    else if (part == "thigh" || part == "leg") defense += def.legs?.defense ?? 0;
                    defense += def.Fx("def");
                    if (def.Has("armor_break")) defense *= 0.7;
                    defense *= 1 - Math.Min(1, w.pierce + spec.pierceAdd + att.Fx("pierce"));
                }
                // 방어구는 비율로 피해를 줄인다 (고정 차감은 약한 무기를 무력화하므로)
                double dmg = Math.Max(1, raw * (1 - defense / (defense + Bal.C("armorScale"))));
                if (crit) dmg *= Bal.C("critMult");
                if (blocked) dmg *= 1 - Bal.C("blockReduction");
                if (part == "head" || part == "neck") dmg *= 1.2;
                dmg *= att.perf;
                if (FmRules.Enabled) dmg *= FmDamage(att, def);
                double dealt = DealDamage(att, def, Math.Max(1, JsMath.Round(dmg)));
                if (dealt > def.worstHit) { def.worstHit = dealt; def.worstPart = part; }
                if (allowRiposte && blocked && def.hp > 0) React(def, "OnBlock", att);
                // 단체전: 위험해진 동료를 엄호한다
                if (team && def.hp > 0)
                    foreach (var mate in F.Where(m => m.side == def.side && m != def && !m.down && m.reactions != null))
                        React(mate, "OnAllyHpBelow", att, def);

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
                if (att.Fx("crowd") > 0 && (crit || dealt > def.maxHp * 0.12)) AddCrowd(att.Fx("crowd"), att);

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
                // 고유 장비: 출혈·기절, 근접 피격 되갚기
                if (!blocked && def.hp > 0 && att.Fx("bleed") > 0 && rng.Chance(att.Fx("bleed"))) ApplyStatus(def, "poison", 2);
                if (!blocked && def.hp > 0 && att.Fx("stun") > 0 && rng.Chance(att.Fx("stun"))) ApplyStatus(def, "stun", 1);
                if (!magic && def.Fx("thorns") > 0 && dealt > 0)
                {
                    double back = Math.Max(1, JsMath.Round(dealt * def.Fx("thorns")));
                    att.hp -= back;
                    att.damageTaken += back;
                    def.damageDealt += back;
                    Emit(new CombatEvent { t = "thorns", actor = def.g.id, target = att.g.id, damage = back, hpAfter = Math.Max(0, att.hp), maxHp = att.maxHp });
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
                    // 기술 테이블의 새 기술: 발동 시점이 맞고 확률을 넘어야 후보가 된다 (기존 기술은 그대로)
                    if (!SkillBook.ProcGate(sid, k => Attr(f, k), HpRatio(f), HpRatio(t), f.maxSta > 0 ? f.sta / f.maxSta : 1, round, t.Has("bound") || t.Has("prone") || t.Has("stun"), round <= 1, () => rng.Next())) continue;
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
                f.sta = Math.Max(0, f.sta - Bal.C("basicAttackSta") * (1 + (f.armor?.staCost ?? 0)) * (1 - f.Fx("sta")));
                ResolveHit(f, t, BasicSpec(f));
            }

            void UseSkill(Fighter f, Fighter t, SkillDef s)
            {
                f.sta = Math.Max(0, f.sta - (s.cost ?? 0) * (1 + (f.armor?.staCost ?? 0)) * (1 - f.Fx("sta")));
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
                // 장비 고유 '재생': HP 회복. 화면이 막대를 맞출 수 있게 이벤트로 남긴다
                if (f.Fx("regen") > 0 && f.hp > 0 && f.hp < f.maxHp)
                {
                    double before = f.hp;
                    f.hp = Math.Min(f.maxHp, f.hp + Math.Max(1, JsMath.Round(f.maxHp * f.Fx("regen"))));
                    Emit(new CombatEvent { t = "regen", target = f.g.id, damage = f.hp - before, hpAfter = f.hp, maxHp = f.maxHp });
                }
                // 상시 기술 '강철 폐': 기력 회복 (기술 테이블)
                if (f.Fx("staRegen") > 0) f.sta = Math.Min(f.maxSta, f.sta + f.Fx("staRegen"));
                bool canAct = true;
                if (f.Has("poison"))
                {
                    double dmg = Math.Max(1, JsMath.Round(f.maxHp * 0.03));
                    f.hp -= dmg;
                    f.damageTaken += dmg;
                    Opp(f).damageDealt += dmg;
                    Emit(new CombatEvent { t = "poison", target = f.g.id, damage = dmg, hpAfter = Math.Max(0, f.hp), maxHp = f.maxHp });
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

            /// <summary>쓰러지거나 항복한 자의 생사 판정. 죽었으면 true</summary>
            bool Verdict(Fighter loser, Fighter winner, bool crit)
            {
                double medical = opt.medicalLevel?.ElementAtOrDefault(loser.side) ?? 0;
                double deathChance;
                if (opt.rule == "training") deathChance = crit ? Bal.D("trainingCritKo") : Bal.D("trainingKo");
                else if (opt.rule == "missio")
                {
                    deathChance = Bal.D("missioBase") - loser.g.popularity * Bal.D("missioPerPopularity") - Math.Max(0, loser.crowdContribution) * Bal.D("missioPerCrowd");
                    if (loser.HasPassive("skill.crowd_hand")) deathChance -= Bal.D("missioCrowdHand");
                    deathChance = Math.Max(Bal.D("missioMin"), Math.Min(Bal.D("missioMax"), deathChance)) * (opt.missioDeathMult ?? 1);
                    if (opt.deathChanceOf != null && opt.deathChanceOf.TryGetValue(loser.g.id, out var fixedChance)) deathChance = fixedChance;
                    Emit(new CombatEvent { t = "highlight", kind = "missio", actors = new[] { loser.g.id } });
                }
                else deathChance = Math.Max(Bal.D("suddenMin"), Bal.D("suddenBase") - medical * Bal.D("suddenPerMedical"));
                bool isDead = !opt.noDeath && rng.Chance(deathChance);
                Emit(new CombatEvent { t = "verdict", target = loser.g.id, rule = opt.rule, deathChance = deathChance, died = isDead });
                if (isDead) Emit(new CombatEvent { t = "highlight", kind = "death", actors = new[] { loser.g.id, winner.g.id } });
                return isDead;
            }

            // ───────── 난전 진행 ─────────

            /// <summary>쓰러진 자를 치우고 그 자리에서 생사를 판정한다. 한쪽이 전멸하면 true</summary>
            bool CheckDowns()
            {
                foreach (var f in F)
                {
                    if (f.down || f.hp > 0) continue;
                    var lastHit = events.LastOrDefault(e => e.t == "attack" && e.target == f.g.id && e.hit);
                    var by = (lastHit != null ? F.FirstOrDefault(x => x.g.id == lastHit.actor) : null) ?? F.Where(x => x.side != f.side && !x.down).OrderBy(x => SepOf(x.idx, f.idx)).FirstOrDefault() ?? f.target;
                    bool crit = lastHit != null && lastHit.crit;
                    f.down = true;
                    f.hp = 0;
                    downs.Add(new KeyValuePair<string, string>(f.g.id, by.g.id));
                    Emit(new CombatEvent { t = "ko", target = f.g.id, actor = by.g.id, crit = crit });
                    AddCrowd(10, by);
                    foreach (var mate in Standing(f.side).Where(m => m.reactions != null).ToList()) React(mate, "OnAllyDown", by, f);
                    if (Verdict(f, by, crit)) deaths.Add(f.g.id);
                    if (!Standing(f.side).Any())
                    {
                        winSide = 1 - f.side;
                        teamReason = "ko";
                        return true;
                    }
                }
                return false;
            }

            /// <summary>혼자 남은 자만 항복할 수 있다 (동료가 서 있으면 버틴다)</summary>
            bool CheckTeamSurrender(Fighter f)
            {
                if (Standing(f.side).Count() > 1) return false;
                if (!f.doctrine.allowSurrender || f.momentum > Bal.C("surrenderMomentum") || HpRatio(f) > Bal.C("surrenderHpRatio")) return false;
                double grit = Attr(f, "grit");
                if (grit >= 20 || !rng.Chance((20 - grit) * Bal.C("surrenderPerGrit"))) return false;
                Emit(new CombatEvent { t = "surrender", actor = f.g.id });
                AddCrowd(-8, f);
                winSide = 1 - f.side;
                teamReason = "surrender";
                var by = f.target ?? Standing(1 - f.side).First();
                if (opt.rule != "training" && Verdict(f, by, false)) deaths.Add(f.g.id);
                f.down = true;
                downs.Add(new KeyValuePair<string, string>(f.g.id, by.g.id));
                return true;
            }

            public MatchResult PlayTeam()
            {
                Emit(new CombatEvent { t = "start", a = a.id, b = b.id, rule = opt.rule, actors = F.Select(f => f.g.id).ToArray(), turns = F.Count(f => f.side == 0), pos = Pos() });
                Emit(new CombatEvent { t = "highlight", kind = "entrance", actors = F.Select(f => f.g.id).ToArray() });
                int maxRounds = (int)Bal.C("maxRounds");
                for (round = 1; round <= maxRounds && winSide == null; round++)
                {
                    Separate();
                    Emit(new CombatEvent { t = "round", round = round, pos = Pos() });
                    var alive = F.Where(f => !f.down).ToList();
                    foreach (var f in alive)
                    {
                        f.mind.late = (f.mind.adapt - 8) * 0.01 * Math.Min(round, 20);
                        f.target = PickTarget(f);
                    }
                    var order = alive.Select(f =>
                    {
                        var o = f.target;
                        double v = (Attr(f, "timing") + Attr(f, "rea") / 2 + Attr(f, "agi") / 2) * (FmRules.Enabled ? 0.3 : 1) - f.main.speed * 2 + rng.Next() * 8 - (f.Has("exposed") ? 6 : 0)
                            + (o != null && (o.Has("exposed") || o.sta <= 0) ? Attr(f, "timing") * 0.6 : 0);
                        if (round == 1 && f.g.race == "beastkin") v += 5;
                        v += f.Fx("first");
                        return (f, v);
                    }).OrderByDescending(x => x.v).Select(x => x.f).ToList();
                    double dmgBefore = F.Sum(f => f.damageTaken);

                    foreach (var f in order)
                    {
                        if (winSide != null) break;
                        if (f.down) continue;
                        if (f.target == null || f.target.down) f.target = PickTarget(f);
                        if (f.target == null) break;
                        Engage(f);
                        bool canAct = StartTurn(f);
                        if (CheckDowns()) break;
                        if (f.down) continue;
                        if (CheckTeamSurrender(f)) break;
                        if (canAct) Act(f);
                        if (CheckDowns()) break;
                        if (canAct && !f.down && !f.target.down && f.main.speed <= 1 && dist <= Reach(f) && rng.Chance(0.2))
                        {
                            BasicAttack(f, f.target);
                            if (CheckDowns()) break;
                        }
                    }
                    foreach (var f in F) if (!f.down) f.sta = Math.Min(f.maxSta, f.sta + Attr(f, "end") * Bal.C("staRegenPerEnd") + 2);

                    if (F.Sum(f => f.damageTaken) == dmgBefore)
                    {
                        stall++;
                        if (stall >= 5) AddCrowd(-3);
                    }
                    else stall = 0;
                }

                if (winSide == null)
                {
                    Emit(new CombatEvent { t = "timeout" });
                    double Score(int side) => F.Where(f => f.side == side).Sum(f => f.damageDealt + f.crowdContribution * 0.5 + (f.down ? 0 : 30));
                    winSide = Score(0) >= Score(1) ? 0 : 1;
                    teamReason = "timeout";
                }
                int ws = winSide.Value;
                var winners = F.Where(f => f.side == ws).ToList();
                var losers = F.Where(f => f.side != ws).ToList();
                var best = winners.OrderByDescending(f => f.down ? 0 : 1).ThenByDescending(f => f.damageDealt).First();
                var lastDown = downs.LastOrDefault(d => losers.Any(l => l.g.id == d.Key)).Key;
                var worst = lastDown != null ? losers.First(l => l.g.id == lastDown) : losers.OrderBy(f => f.damageDealt).First();
                if (deaths.Count == 0 && HpRatio(best) <= 0.3 && !best.down) Emit(new CombatEvent { t = "highlight", kind = "comeback", actors = new[] { best.g.id } });
                Emit(new CombatEvent { t = "end", winner = best.g.id, loser = worst.g.id, reason = teamReason });

                var injuries = new Dictionary<string, Injury>();
                double injMult = (double)Bal.Rule(opt.rule)["injuryMult"];
                foreach (var f in F)
                {
                    if (deaths.Contains(f.g.id)) continue;
                    injuries[f.g.id] = RollInjury(rng, f, f.down, injMult);
                }
                return new MatchResult
                {
                    events = events,
                    winnerId = best.g.id,
                    loserId = worst.g.id,
                    reason = teamReason,
                    died = deaths.FirstOrDefault(),
                    rounds = Math.Min(round, maxRounds),
                    crowd = crowd,
                    damageDealt = F.ToDictionary(f => f.g.id, f => f.damageDealt),
                    crowdContribution = F.ToDictionary(f => f.g.id, f => f.crowdContribution),
                    injuries = injuries,
                    hpLeft = F.ToDictionary(f => f.g.id, f => Math.Max(0, f.hp / f.maxHp)),
                    sides = new[] { F.Where(f => f.side == 0).Select(f => f.g.id).ToList(), F.Where(f => f.side == 1).Select(f => f.g.id).ToList() },
                    winnerSide = ws,
                    downs = downs,
                    deaths = deaths,
                };
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
                        double v = (Attr(f, "timing") + Attr(f, "rea") / 2 + Attr(f, "agi") / 2) * (FmRules.Enabled ? 0.3 : 1) - f.main.speed * 2 + rng.Next() * 8 - (f.Has("exposed") ? 6 : 0)
                            // 상대가 무너졌거나 지쳤으면 타이밍 좋은 쪽이 파고든다
                            + (o.Has("exposed") || o.sta <= 0 ? Attr(f, "timing") * 0.6 : 0);
                        if (round == 1 && f.g.race == "beastkin") v += 5;
                        v += f.Fx("first");
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
                if (reason != "timeout" && !(reason == "surrender" && opt.rule == "training") && Verdict(loser, winner, crit)) died = loser.g.id;
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
