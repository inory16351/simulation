// 던전 원정 (src/core/dungeon.ts): 검투사 1~5명을 며칠 보내 장비를 캐 온다. 싸움은 층마다 5:5 진형 전투.
// 돈은 거의 안 남고(급료를 생각하면 손해) 명성도 없다. 목적은 장비와 경험이다. 쓰러지면 죽을 수 있다.
// 나라가 가끔 내거는 토벌 의뢰는 아주 어렵지만 명성(RP)과 현상금을 준다.
// 전투 규칙은 바뀔 수 있게 Formations와 Skirmish() 한곳에 모아 둔다.
// Expedition/DungeonQuest 타입은 StateTypes.cs에 있다.
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Colosseum.Core
{
    public class DungeonFoe
    {
        public string race, name;
        public DungeonFoe(string race, string name) { this.race = race; this.name = name; }
    }

    public class DungeonDef
    {
        /// <summary>sewer | mine | catacomb | magma | abyss</summary>
        public string id;
        public string name, desc;
        public int tier, floors, days;
        /// <summary>몬스터 실력 (generateGladiator target)</summary>
        public double level;
        /// <summary>쓰러진 검투사가 죽을 기본 확률</summary>
        public double death;
        /// <summary>품질 0~3 가중치</summary>
        public double[] loot;
        public List<DungeonFoe> foes;
        public DungeonFoe boss;
    }

    /// <summary>5:5 진형. 배율은 (주는 피해, 받는 피해)</summary>
    public class FormationDef
    {
        public string name, desc;
        public double dealt, taken, ranged;
        /// <summary>front | weakest | strongest</summary>
        public string focus;
    }

    /// <summary>dungeon.ts의 Skirmish (함수 이름과 겹쳐 Result를 붙였다)</summary>
    public class SkirmishResult
    {
        public bool won;
        public List<string> downed;
        public Dictionary<string, double> hp;
        public Dictionary<string, double> kills;
    }

    public static class DungeonLogic
    {
        static void Msg(GameState s, string kind, string title, string body) =>
            StateUtil.PushMessage(s, new InboxMessage { kind = kind, title = title, body = body });

        static string Num(double v)
        {
            if (v == Math.Floor(v) && Math.Abs(v) < 1e15) return ((long)v).ToString(CultureInfo.InvariantCulture);
            return v.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>toLocaleString()</summary>
        static string Loc(double v) => v.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>rng.int(0, 2 ** 31 - 1). Rng.Int(0, int.MaxValue)는 max-min+1이 int를 넘쳐서 직접 계산한다</summary>
        static uint Seed31(Rng rng) => (uint)Math.Floor(rng.Next() * 2147483648.0);

        static Gladiator Glad(GameState s, string id) => id != null && s.gladiators.TryGetValue(id, out var g) ? g : null;

        public static readonly List<DungeonDef> Dungeons = new()
        {
            new DungeonDef { id = "sewer", name = "쥐굴 하수도", desc = "도시 밑 하수도. 쥐인간과 도적 떼가 삽니다. 처음 가 보기 좋습니다", tier = 1, floors = 3, days = 7, level = 5, death = 0.04, loot = new double[] { 60, 32, 8, 0 },
                foes = new() { new("beastkin", "쥐인간"), new("human", "하수도 도적") }, boss = new("beastkin", "쥐왕") },
            new DungeonDef { id = "mine", name = "버려진 은광", desc = "무너진 갱도에 오크 약탈자와 광부 망령이 숨어 있습니다", tier = 2, floors = 3, days = 10, level = 7, death = 0.07, loot = new double[] { 45, 38, 15, 2 },
                foes = new() { new("orc", "오크 약탈자"), new("dwarf", "광부 망령") }, boss = new("orc", "갱도 두목") },
            new DungeonDef { id = "catacomb", name = "망자의 지하묘지", desc = "옛 검투사들이 묻힌 묘지. 죽은 자들이 아직 싸웁니다", tier = 3, floors = 4, days = 14, level = 9, death = 0.1, loot = new double[] { 30, 40, 24, 6 },
                foes = new() { new("human", "해골 검투사"), new("elf", "묘지 사령술사") }, boss = new("human", "무덤의 챔피언") },
            new DungeonDef { id = "magma", name = "용암 동굴", desc = "불타는 동굴. 도마뱀 부족과 미노타우로스 광전사가 지킵니다", tier = 4, floors = 4, days = 18, level = 11, death = 0.13, loot = new double[] { 20, 38, 32, 10 },
                foes = new() { new("lizardfolk", "화염 도마뱀 전사"), new("minotaur", "용암 광전사") }, boss = new("minotaur", "불의 미궁 주인") },
            new DungeonDef { id = "abyss", name = "심연의 신전", desc = "가장 깊은 곳. 살아 돌아온 자가 드뭅니다", tier = 5, floors = 5, days = 21, level = 13, death = 0.17, loot = new double[] { 10, 32, 40, 18 },
                foes = new() { new("elf", "심연의 사제"), new("orc", "타락한 전사"), new("minotaur", "심연의 수문장") }, boss = new("elf", "잊힌 신의 대리인") },
        };

        public static DungeonDef GetDungeon(string id) => Dungeons.First(d => d.id == id);
        public const int MaxParty = 5;

        /// <summary>5:5 진형 (docs/13_tactics.md)</summary>
        public static readonly Dictionary<string, FormationDef> Formations = new()
        {
            ["shieldwall"] = new FormationDef { name = "방패벽", desc = "전열 3·후열 2. 받는 피해가 줄지만 느리게 이깁니다", dealt = 0.9, taken = 0.8, ranged = 1, focus = "front" },
            ["wedge"] = new FormationDef { name = "쐐기", desc = "중앙 돌파. 약한 적부터 몰아칩니다. 받는 피해도 늘어납니다", dealt = 1.2, taken = 1.1, ranged = 1, focus = "weakest" },
            ["flank"] = new FormationDef { name = "포위", desc = "양 날개 전진. 수가 많을수록 강합니다", dealt = 1, taken = 1, ranged = 1, focus = "strongest" },
            ["spread"] = new FormationDef { name = "산개", desc = "흩어져 싸웁니다. 투척·창·주술이 강해지고 근접은 약해집니다", dealt = 0.95, taken = 1, ranged = 1.3, focus = "front" },
        };

        static double Now(GameState s) => Calendar.AbsDay(s.season, s.day);
        public static List<Expedition> ExpeditionsOf(GameState s) => s.expeditions ??= new List<Expedition>();
        public static List<DungeonQuest> QuestsOf(GameState s) => s.dungeonQuests ??= new List<DungeonQuest>();

        /// <summary>원정에 보낼 수 있는가. 되면 null</summary>
        public static string CannotSend(GameState s, Gladiator g)
        {
            if (!g.alive) return "사망";
            if (g.deployment != null) return g.deployment.kind == "dungeon" ? "원정 중" : "파견 중";
            if (g.injury?.severity == "serious") return "중상";
            if (s.challenges.Any(x => x.status == "scheduled" && (x.fromGladiator == g.id || x.toGladiator == g.id))) return "경기가 잡혀 있습니다";
            return null;
        }

        /// <summary>되면 null</summary>
        public static string StartExpedition(GameState s, string dungeon, List<string> ids, string formation, string questId = null)
        {
            if (ids.Count == 0 || ids.Count > MaxParty) return $"1~{MaxParty}명을 보내십시오";
            var me = s.ludi[s.playerLudusId];
            foreach (var id in ids)
            {
                var g = Glad(s, id);
                if (!me.gladiators.Contains(id)) return "우리 검투사가 아닙니다";
                var why = CannotSend(s, g);
                if (why != null) return $"{g.name}: {why}";
            }
            var q = !string.IsNullOrEmpty(questId) ? QuestsOf(s).FirstOrDefault(x => x.id == questId) : null;
            if (!string.IsNullOrEmpty(questId) && (q == null || q.status != "open")) return "의뢰가 마감됐습니다";
            var d = GetDungeon(q?.dungeon ?? dungeon);
            double t = Now(s);
            var hp = new Dictionary<string, double>();
            foreach (var id in ids) hp[id] = 1;
            var ex = new Expedition
            {
                id = $"X{s.nextId++}", dungeon = d.id, party = ids, formation = formation, startedAt = t, endsAt = t + d.days,
                floor = 0, status = "active", questId = questId, loot = new List<string>(), coins = 0, log = new List<string>(), hp = hp,
            };
            ExpeditionsOf(s).Add(ex);
            if (q != null) q.status = "taken";
            foreach (var id in ids)
            {
                var g = s.gladiators[id];
                g.deployment = new Deployment { contractId = ex.id, until = ex.endsAt, kind = "dungeon" };
                Career.LogCareer(s, g, $"{d.name}{(q != null ? " 토벌" : "")} 원정을 떠났습니다", "other");
            }
            return null;
        }

        /// <summary>원정 일정에서 n번째 층을 치르는 날</summary>
        static double FloorDay(Expedition ex, DungeonDef d, double n) => ex.startedAt + JsMath.Round((d.days * n) / d.floors);

        /// <summary>매일: 원정 층 진행, 귀환, 토벌 의뢰 만료</summary>
        public static void DailyDungeons(GameState s, Rng rng)
        {
            double t = Now(s);
            var exs = ExpeditionsOf(s);
            for (int i = 0; i < exs.Count; i++)
            {
                var ex = exs[i];
                if (ex.status != "active") continue;
                var d = GetDungeon(ex.dungeon);
                while (ex.status == "active" && ex.floor < d.floors && t >= FloorDay(ex, d, ex.floor + 1)) RunFloor(s, rng, ex, d);
                if (t >= ex.endsAt || ex.status != "active" || Alive(s, ex).Count == 0) FinishExpedition(s, ex);
            }
            foreach (var q in QuestsOf(s)) if (q.status == "open" && t > q.expiresAt) q.status = "expired";
            // 끝난 원정·의뢰는 조금만 남긴다
            s.expeditions = ExpeditionsOf(s).Where(x => x.status == "active" || t - x.endsAt < 28).ToList();
            s.dungeonQuests = QuestsOf(s).Where(q => q.status == "open" || q.status == "taken" || t - q.expiresAt < 14).ToList();
        }

        static List<string> Alive(GameState s, Expedition ex) =>
            ex.party.Where(id => Glad(s, id)?.alive == true && s.gladiators[id].deployment?.contractId == ex.id).ToList();

        static readonly string[] Areas = { "arm", "leg", "torso", "head" };

        /// <summary>한 층: 몬스터 무리와 5:5 진형 전투. 이기면 전리품, 지면 쓰러진 자를 끌고 물러난다</summary>
        static void RunFloor(GameState s, Rng rng, Expedition ex, DungeonDef d)
        {
            var quest = !string.IsNullOrEmpty(ex.questId) ? QuestsOf(s).FirstOrDefault(q => q.id == ex.questId) : null;
            bool boss = ex.floor + 1 == d.floors;
            var party = Alive(s, ex).Select(id => s.gladiators[id]).ToList();
            var foes = SpawnFoes(rng, d, boss, quest?.hard ?? 1, ex.floor);
            var res = Skirmish(rng, party, foes, ex.formation, ex.hp);
            var lines = new List<string> { $"{Num(ex.floor + 1)}층{(boss ? " (우두머리)" : "")}: {string.Join(", ", foes.Select(f => f.name))}" };

            foreach (var g in party)
            {
                Training.Grow(g, rng, "match", 6 + d.tier * 2, 1, 1);
                Career.SeasonRecord(s, g);
            }
            // 쓰러진 검투사: 죽거나 다친다. 죽을 확률은 실력 차이와 의뢰 난이도에 따라
            foreach (var id in res.downed)
            {
                var g = s.gladiators[id];
                double margin = (GladiatorRules.ComputeCA(g) - d.level * 10 * (quest?.hard ?? 1)) / 120;
                double p = Math.Max(0.01, d.death * (quest != null ? 1.5 : 1) * (1 - margin) * (res.won ? 0.6 : 1.2));
                if (rng.Chance(p))
                {
                    KillInDungeon(s, g, d);
                    lines.Add($"{Josa.J(g.name, "이/가")} 쓰러져 다시 일어나지 못했습니다.");
                }
                else
                {
                    g.injury = GladiatorRules.MakeInjury(rng, rng.Chance(0.35) ? "moderate" : "minor", rng.Pick(Areas));
                    Career.SeasonRecord(s, g).injuries++;
                    lines.Add($"{g.name}: {g.injury.desc}");
                }
            }
            if (res.won)
            {
                ex.floor++;
                foreach (var id in res.hp.Keys.ToList()) ex.hp[id] = Math.Min(1, res.hp[id] + 0.3);
                var drops = RollLoot(rng, d, boss, quest != null ? 1 : 0);
                ex.loot.AddRange(drops);
                int coins = rng.Int(10, 30) * d.tier;
                ex.coins += coins;
                lines.Add($"승리. 전리품: {string.Join(", ", drops.Select(x => GearLogic.ItemName(x)))} · 동전 {coins} D");
                foreach (var g in party)
                    if (res.kills.TryGetValue(g.id, out var k) && k != 0) g.record.kills += (int)k;
            }
            else
            {
                lines.Add("패퇴. 남은 자들이 동료를 메고 물러났습니다.");
                ex.status = "failed";
            }
            ex.log.AddRange(lines);
            Msg(s, lines.Any(l => l.Contains("일어나지 못했습니다")) ? "death" : "info",
                $"원정 소식: {d.name} {Num(ex.floor + (res.won ? 0 : 1))}층",
                string.Join("\n", lines));
        }

        static List<Gladiator> SpawnFoes(Rng rng, DungeonDef d, bool boss, double hard, double floor)
        {
            double n = Math.Min(5, 3 + Math.Floor(floor / 2) + (boss ? 1 : 0));
            var o = new List<Gladiator>();
            for (int i = 0; i < n; i++)
            {
                var kind = boss && i == 0 ? d.boss : rng.Pick(d.foes);
                double lvl = Math.Min(20, JsMath.Round((d.level + floor * 0.6 + (boss && i == 0 ? 2 : 0)) * hard));
                var g = GladiatorGen.Generate(rng, new GenOptions { id = $"M{i}_{rng.Int(0, 1000000000)}", nation = rng.Pick(Data.Nations).id, target = lvl, race = kind.race });
                g.name = kind.name;
                o.Add(g);
            }
            return o;
        }

        static bool IsRanged(Gladiator g)
        {
            var w = Data.Weapon(g.equipment.main);
            return w != null && (w.range > 0 || w.@class == "focus");
        }

        /// <summary>
        /// 5:5 진형 전투. 한 교환마다 살아 있는 아군이 차례로 진형이 고른 적과 1:1로 붙고(기존 1:1 전투 엔진),
        /// 남은 체력을 다음 대결로 이어 간다. 같은 적에게 여럿이 붙으면 자연스레 협공이 된다.
        /// 진형 배율은 대결에서 깎인 체력에 곱한다.
        /// </summary>
        public static SkirmishResult Skirmish(Rng rng, List<Gladiator> party, List<Gladiator> foes, string formation, Dictionary<string, double> startHp = null)
        {
            startHp ??= new Dictionary<string, double>();
            var F = Formations[formation];
            var hp = new Dictionary<string, double>();
            foreach (var g in party.Concat(foes)) hp[g.id] = startHp.TryGetValue(g.id, out var h) ? h : 1;
            var kills = new Dictionary<string, double>();
            var downed = new List<string>();
            List<Gladiator> Up(List<Gladiator> list) => list.Where(g => hp[g.id] > 0.02).ToList();
            // 방패벽·산개는 근접을 앞에, 원거리를 뒤에 세운다
            List<Gladiator> Order(List<Gladiator> list) => list.OrderBy(g => IsRanged(g) ? 1 : 0).ToList();

            for (int round = 0; round < 12 && Up(party).Count > 0 && Up(foes).Count > 0; round++)
            {
                var allies = Order(Up(party));
                for (int i = 0; i < allies.Count; i++)
                {
                    var a = allies[i];
                    var targets = Up(foes);
                    if (targets.Count == 0 || hp[a.id] <= 0.02) continue;
                    Gladiator foe;
                    if (F.focus == "weakest")
                    {
                        foe = targets[0];
                        for (int k = 1; k < targets.Count; k++) if (hp[targets[k].id] < hp[foe.id]) foe = targets[k];
                    }
                    else if (F.focus == "strongest")
                    {
                        foe = targets[0];
                        for (int k = 1; k < targets.Count; k++) if (GladiatorRules.ComputeCA(targets[k]) > GladiatorRules.ComputeCA(foe)) foe = targets[k];
                    }
                    else foe = targets[i % targets.Count];
                    var r = Sim.Run(a, foe, new MatchOptions
                    {
                        rule = "training", noDeath = true, seed = Seed31(rng),
                        startHp = new double?[] { hp[a.id] < 1 ? hp[a.id] : (double?)null, hp[foe.id] < 1 ? hp[foe.id] : (double?)null },
                    });
                    // 포위: 아군이 많을수록, 산개: 원거리면 더 세게
                    double outnumber = formation == "flank" ? 1 + 0.12 * Math.Max(0, Up(party).Count - targets.Count) : 1;
                    double dealt = F.dealt * outnumber * (IsRanged(a) ? F.ranged : formation == "spread" ? 0.9 : 1);
                    double lostA = (hp[a.id] - r.hpLeft[a.id]) * F.taken;
                    double lostF = (hp[foe.id] - r.hpLeft[foe.id]) * dealt;
                    hp[a.id] = r.winnerId == a.id ? Math.Max(0.03, hp[a.id] - lostA) : 0;
                    hp[foe.id] = r.winnerId == foe.id ? Math.Max(0.03, hp[foe.id] - lostF) : 0;
                    if (hp[foe.id] <= 0.02) kills[a.id] = (kills.TryGetValue(a.id, out var kc) ? kc : 0) + 1;
                    if (hp[a.id] <= 0.02) downed.Add(a.id);
                }
                // 적의 반격: 아군이 다 붙지 못한 남는 적은 후열을 노린다
                var spare = Up(foes).Skip(allies.Count).ToList();
                foreach (var foe in spare)
                {
                    var ordered = Order(Up(party));
                    if (ordered.Count == 0) break;
                    var t = ordered[ordered.Count - 1]; // reverse()[0]
                    var r = Sim.Run(foe, t, new MatchOptions
                    {
                        rule = "training", noDeath = true, seed = Seed31(rng),
                        startHp = new double?[] { hp[foe.id] < 1 ? hp[foe.id] : (double?)null, hp[t.id] < 1 ? hp[t.id] : (double?)null },
                    });
                    hp[t.id] = r.winnerId == t.id ? Math.Max(0.03, hp[t.id] - (hp[t.id] - r.hpLeft[t.id]) * F.taken) : 0;
                    hp[foe.id] = r.winnerId == foe.id ? Math.Max(0.03, hp[foe.id] - (hp[foe.id] - r.hpLeft[foe.id]) * F.dealt) : 0;
                    if (hp[t.id] <= 0.02) downed.Add(t.id);
                }
            }
            bool won = Up(foes).Count == 0 && Up(party).Count > 0;
            var partyHp = new Dictionary<string, double>();
            foreach (var g in party) partyHp[g.id] = hp[g.id];
            // [...new Set(downed)]: 처음 나온 순서
            var uniq = new List<string>();
            var seen = new HashSet<string>();
            foreach (var id in downed) if (seen.Add(id)) uniq.Add(id);
            return new SkirmishResult { won = won, downed = uniq, hp = partyHp, kills = kills };
        }

        static List<string> LootPool() =>
            Data.Weapons.Select(w => w.id).Concat(Data.Armors.Select(a => a.id)).Where(id => id != "loincloth" && id != "cestus").ToList();

        /// <summary>층 전리품: 1~2개, 우두머리는 하나 더 (한 단계 위 품질). bonus는 토벌 의뢰의 품질 가산</summary>
        public static List<string> RollLoot(Rng rng, DungeonDef d, bool boss, int bonus = 0)
        {
            var pool = LootPool();
            string Roll(int extra)
            {
                var w = d.loot;
                int q = Math.Min(3, int.Parse(rng.Weighted(new List<KeyValuePair<string, double>>
                {
                    new("0", w[0]), new("1", w[1]), new("2", w[2]), new("3", w[3]),
                }), CultureInfo.InvariantCulture) + extra + bonus);
                string @base = rng.Pick(pool);
                return q != 0 ? $"{@base}+{q}" : @base;
            }
            var o = new List<string> { Roll(0) };
            if (rng.Chance(0.45)) o.Add(Roll(0));
            if (boss) o.Add(Roll(1));
            return o;
        }

        static void KillInDungeon(GameState s, Gladiator g, DungeonDef d)
        {
            var me = s.ludi[s.playerLudusId];
            g.alive = false;
            g.injury = null;
            g.deployment = null;
            g.diedAt = new DiedAt { season = s.season, day = s.day, cause = $"{d.name}에서 전사" };
            var gid = g.id;
            me.gladiators = me.gladiators.Where(x => x != gid).ToList();
            s.fallen.Add(g.id);
            Career.LogCareer(s, g, $"{d.name} 깊은 곳에서 쓰러져 돌아오지 못했습니다", "death");
            foreach (var other in me.gladiators) s.gladiators[other].morale = Math.Max(0, s.gladiators[other].morale - 5);
        }

        /// <summary>귀환: 전리품은 창고로, 동전은 금고로. 토벌 의뢰를 끝까지 깼으면 현상금과 명성</summary>
        static void FinishExpedition(GameState s, Expedition ex)
        {
            var d = GetDungeon(ex.dungeon);
            var me = s.ludi[s.playerLudusId];
            bool cleared = ex.floor >= d.floors;
            if (ex.status == "active") ex.status = cleared ? "done" : "failed";
            ex.endsAt = Math.Min(ex.endsAt, Now(s));
            foreach (var id in ex.party)
            {
                var g = Glad(s, id);
                if (g?.deployment?.contractId == ex.id) g.deployment = null;
            }
            (s.armory ??= new List<string>()).AddRange(ex.loot);
            if (ex.coins != 0) Economy.AddMoney(s, me, ex.coins, $"{d.name} 원정 동전");
            var q = !string.IsNullOrEmpty(ex.questId) ? QuestsOf(s).FirstOrDefault(x => x.id == ex.questId) : null;
            var lines = new List<string>
            {
                $"{d.floors}층 중 {Num(ex.floor)}층을 깼습니다.",
                ex.loot.Count > 0 ? $"가져온 장비: {string.Join(", ", ex.loot.Select(x => GearLogic.ItemName(x)))}" : "가져온 장비가 없습니다.",
            };
            if (q != null)
            {
                if (cleared)
                {
                    q.status = "done";
                    Economy.AddMoney(s, me, q.bounty, $"{Data.Nation(q.nation).@short} 토벌 현상금");
                    me.rp += q.rp;
                    me.favor ??= new Dictionary<string, double>();
                    me.favor[q.nation] = Math.Min(100, (me.favor.TryGetValue(q.nation, out var fv) ? fv : 0) + 10);
                    foreach (var id in ex.party)
                    {
                        var g = Glad(s, id);
                        if (g != null && g.alive) { g.popularity = Math.Min(100, g.popularity + 6); Career.LogCareer(s, g, $"{d.name} 토벌에 성공했습니다", "milestone"); }
                    }
                    lines.Add($"토벌 성공! 현상금 {Loc(q.bounty)} D, RP +{Num(q.rp)}, {Data.Nation(q.nation).@short} 호감 +10.");
                }
                else
                {
                    q.status = "failed";
                    lines.Add("토벌에 실패했습니다.");
                }
            }
            Msg(s, "info", $"원정 귀환: {d.name}", string.Join("\n", lines) + "\n\n장비는 막사 → 검투사 화면에서 착용할 수 있습니다.");
        }

        /// <summary>급료일마다: 가끔 나라가 던전 토벌 의뢰를 낸다 (어렵다)</summary>
        public static void WeeklyQuests(GameState s, Rng rng)
        {
            if (QuestsOf(s).Any(q => q.status == "open") || !rng.Chance(0.18)) return;
            var d = Dungeons[rng.Int(2, Dungeons.Count - 1)];
            string nation = rng.Chance(0.6) ? s.nation : rng.Pick(Data.Nations).id;
            double t = Now(s);
            var quest = new DungeonQuest
            {
                id = $"Q{s.nextId++}", nation = nation, dungeon = d.id, hard = rng.Range(1.3, 1.5),
                bounty = JsMath.Round((d.tier * 1500 * rng.Range(0.9, 1.3)) / 100) * 100,
                rp = d.tier * 12 + rng.Int(0, 10), postedAt = t, expiresAt = t + 14, status = "open",
            };
            QuestsOf(s).Add(quest);
            Msg(s, "warning", $"토벌 의뢰: {d.name}",
                $"{Josa.J(Data.Nation(nation).name, "이/가")} {d.name}의 괴물을 소탕할 루두스를 찾습니다. 평소보다 훨씬 강한 놈들이 들끓습니다.\n현상금은 {Loc(quest.bounty)} D이고 RP도 {Num(quest.rp)} 오릅니다. 14일 안에 떠나야 합니다.\n\n던전 화면에서 받을 수 있습니다.");
        }
    }
}
