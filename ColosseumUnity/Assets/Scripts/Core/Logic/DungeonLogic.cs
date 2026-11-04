// 던전 원정 (src/core/dungeon.ts): 검투사 1~5명을 며칠 보내 장비를 캐 온다. 싸움은 층마다 공식 단체전과 같은 난전(Sim.RunTeam), 적은 늘 5명.
// 체력이 바닥나 쓰러지면 미시오처럼 그 자리에서 생사를 판정한다. 적보다 약할수록 잘 죽는다 (DeathChance): 적정 능력이면 원정에서 5~10%쯤 죽는다.
// 돈은 거의 안 남고(급료를 생각하면 손해) 명성도 없다. 목적은 장비와 경험이다. 쓰러지면 죽을 수 있다.
// 나라가 가끔 내거는 토벌 의뢰는 아주 어렵지만 명성(RP)과 현상금을 준다.
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

    /// <summary>원정대 전술 이름과 설명 (단체전 방침과 같다)</summary>
    public class FormationDef
    {
        public string name, desc;
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
            // 아래부터 Unity판 추가 던전 (급료일 순환용. 앞 다섯의 순서는 바꾸지 않는다)
            new DungeonDef { id = "bandit", name = "산적 소굴", desc = "국경 언덕의 동굴. 탈영병과 산적이 진을 쳤습니다", tier = 1, floors = 3, days = 7, level = 5.5, death = 0.05, loot = new double[] { 56, 34, 10, 0 },
                foes = new() { new("human", "언덕 산적"), new("orc", "탈영한 용병") }, boss = new("human", "산적 두령") },
            new DungeonDef { id = "cellar", name = "버려진 술 창고", desc = "밀주꾼과 들짐승 같은 수인 패거리가 눌러앉은 지하 창고입니다", tier = 1, floors = 3, days = 8, level = 4.5, death = 0.03, loot = new double[] { 64, 30, 6, 0 },
                foes = new() { new("beastkin", "술 창고 패거리"), new("dwarf", "밀주꾼") }, boss = new("beastkin", "창고지기 늑대") },
            new DungeonDef { id = "drowned", name = "물에 잠긴 요새", desc = "썰물 때만 드러나는 요새. 늪의 약탈자들이 보물을 지킵니다", tier = 2, floors = 3, days = 10, level = 7.5, death = 0.08, loot = new double[] { 42, 38, 17, 3 },
                foes = new() { new("lizardfolk", "늪 약탈자"), new("human", "익사한 수비병") }, boss = new("lizardfolk", "물길 족장") },
            new DungeonDef { id = "forge", name = "식어 버린 대장간", desc = "옛 장인들의 산속 공방. 쓸 만한 무기가 아직 남아 있습니다", tier = 2, floors = 3, days = 11, level = 8, death = 0.08, loot = new double[] { 38, 40, 19, 3 },
                foes = new() { new("dwarf", "미친 대장장이"), new("orc", "고철 도적") }, boss = new("dwarf", "마지막 장인") },
            new DungeonDef { id = "oldarena", name = "피의 옛 투기장", desc = "무너진 옛 원형 경기장. 패배를 인정하지 않은 투사들이 남아 있습니다", tier = 3, floors = 4, days = 14, level = 9.5, death = 0.11, loot = new double[] { 28, 40, 26, 6 },
                foes = new() { new("human", "옛 투사의 망령"), new("minotaur", "사슬 황소") }, boss = new("minotaur", "피투성이 챔피언") },
            new DungeonDef { id = "thorn", name = "가시 숲 성소", desc = "길이 매일 바뀌는 숲. 침입자를 심판하는 사제들이 지킵니다", tier = 3, floors = 4, days = 13, level = 9, death = 0.1, loot = new double[] { 32, 40, 23, 5 },
                foes = new() { new("elf", "가시 사제"), new("beastkin", "숲 사냥꾼") }, boss = new("elf", "숲의 심판자") },
            new DungeonDef { id = "frost", name = "얼음 금고", desc = "얼어붙은 왕실 금고. 서리 파수꾼들이 아직 임무를 지킵니다", tier = 4, floors = 4, days = 18, level = 11.5, death = 0.14, loot = new double[] { 18, 38, 34, 10 },
                foes = new() { new("dwarf", "서리 파수꾼"), new("human", "얼어붙은 근위병") }, boss = new("dwarf", "얼음 왕") },
            new DungeonDef { id = "spire", name = "무너진 마탑", desc = "실험이 끝나지 않은 탑. 무엇이 튀어나올지 모릅니다", tier = 4, floors = 4, days = 17, level = 11, death = 0.13, loot = new double[] { 22, 36, 32, 10 },
                foes = new() { new("elf", "미친 마법사"), new("lizardfolk", "탑의 실험체") }, boss = new("elf", "탑의 주인") },
            new DungeonDef { id = "kings", name = "왕들의 골짜기", desc = "대륙 첫 왕조의 무덤. 황금과 저주가 함께 묻혀 있습니다", tier = 5, floors = 5, days = 21, level = 13.5, death = 0.18, loot = new double[] { 8, 32, 42, 18 },
                foes = new() { new("human", "왕의 근위 망령"), new("minotaur", "황금 황소") }, boss = new("human", "첫 번째 왕") },
        };

        /// <summary>급료일마다 등급별로 하나씩 열 던전을 새로 뽑는다</summary>
        static List<string> RollRotation(Rng rng) =>
            Dungeons.GroupBy(d => d.tier).OrderBy(g => g.Key).Select(g => rng.Pick(g.ToList()).id).ToList();

        /// <summary>이번 주에 열린 던전. 옛 저장·새 게임에서 아직 없으면 날짜로 정한다 (게임 난수를 건드리지 않게)</summary>
        public static List<string> Rotation(GameState s) => s.dungeonRotation ??= RollRotation(new Rng((uint)(s.season * 1000 + s.day)));

        /// <summary>급료일: 던전 입구가 바뀐다</summary>
        public static void WeeklyRotation(GameState s, Rng rng)
        {
            if (FmRules.Enabled) s.dungeonRotation = RollRotation(rng);
        }

        public static DungeonDef GetDungeon(string id) => Dungeons.First(d => d.id == id);
        public const int MaxParty = 5;

        /// <summary>원정대 전술: 단체전 방침 넷 (전략 화면과 같다)</summary>
        public static readonly Dictionary<string, FormationDef> Formations = Tactics.Policies.ToDictionary(p => p, p => new FormationDef { name = Tactics.PolicyNames[p], desc = Tactics.PolicyDesc[p] });

        /// <summary>옛 저장의 진형 이름을 단체전 방침으로</summary>
        public static string PolicyOf(string formation) => formation switch
        {
            "shieldwall" => "spread",
            "wedge" => "weak",
            "flank" => "focus",
            _ => Formations.ContainsKey(formation ?? "") ? formation : "focus",
        };

        /// <summary>적정 능력(적 평균보다 DeathFairGap만큼 강함)일 때 쓰러지면 죽을 확률. 원정에서 5~10%쯤 죽는 값 (측정: FmRulesTests)</summary>
        public static double DeathBase = 0.1;
        /// <summary>층을 이어 싸워야 하니 적보다 이만큼 강해야 적정이다 (CA, 능력치 평균 3쯤)</summary>
        const double DeathFairGap = 30;
        /// <summary>적정에서 능력(CA)이 이만큼 처지면 죽을 확률이 두 배, 이만큼 앞서면 바닥까지 내려간다</summary>
        const double DeathGapPerBase = 30;

        /// <summary>쓰러졌을 때 죽을 확률: 적 다섯의 평균 능력보다 약할수록 높다. 토벌 의뢰면 1.5배</summary>
        public static double DeathChance(double ca, double foeCa, bool quest) =>
            Math.Clamp(DeathBase * (1 + (foeCa + DeathFairGap - ca) / DeathGapPerBase), 0.02, 0.6) * (quest ? 1.5 : 1);

        /// <summary>적 수준 배율: 적이 늘 다섯이라 표의 level을 그대로 쓰면 너무 강하다</summary>
        public static double FoeLevelMult = 0.6;

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
            if (ids.Count == 0 || ids.Count > MaxParty) return $"1~{MaxParty}명까지 보낼 수 있습니다";
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
            if (FmRules.Enabled && q == null && !Rotation(s).Contains(dungeon)) return "이 던전은 이번 주에 열려 있지 않습니다";
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

        /// <summary>한 층: 몬스터 다섯과 난전. 쓰러진 검투사는 미시오처럼 생사가 갈린다. 이기면 전리품, 지면 물러난다</summary>
        static void RunFloor(GameState s, Rng rng, Expedition ex, DungeonDef d)
        {
            var quest = !string.IsNullOrEmpty(ex.questId) ? QuestsOf(s).FirstOrDefault(q => q.id == ex.questId) : null;
            bool boss = ex.floor + 1 == d.floors;
            var party = Alive(s, ex).Select(id => s.gladiators[id]).ToList();
            if (party.Count == 0) { ex.status = "failed"; return; }
            var foes = SpawnFoes(rng, d, boss, quest?.hard ?? 1, ex.floor);
            // 전투 화면으로 다시 볼 수 있게 싸우기 전 모습과 시드를 남긴다
            var battle = new FloorBattle
            {
                floor = (int)ex.floor + 1, boss = boss, seed = Seed31(rng), policy = PolicyOf(ex.formation),
                deathOf = party.ToDictionary(g => g.id, g => DeathChance(GladiatorRules.ComputeCA(g), foes.Average(f => GladiatorRules.ComputeCA(f)), quest != null)),
                startHp = party.Select(g => ex.hp.TryGetValue(g.id, out var h) && h < 1 ? h : (double?)null).ToList(),
                party = party.Select(Snapshot).ToList(), foes = foes.Select(Snapshot).ToList(),
            };
            var res = RunBattle(party, foes, battle);
            bool won = res.winnerSide == 0;
            battle.won = won;
            (ex.battles ??= new List<FloorBattle>()).Add(battle);
            var lines = new List<string> { $"{Num(ex.floor + 1)}층{(boss ? " (우두머리)" : "")}: {string.Join(", ", foes.Select(f => f.name))}" };

            foreach (var g in party)
            {
                Training.Grow(g, rng, "match", 6 + d.tier * 2, 1, 1);
                Career.SeasonRecord(s, g);
            }
            var dead = new HashSet<string>(res.deaths ?? new List<string>());
            foreach (var g in party)
            {
                if (dead.Contains(g.id))
                {
                    KillInDungeon(s, g, d);
                    lines.Add($"{Josa.J(g.name, "이/가")} 쓰러져 다시 일어나지 못했습니다.");
                    continue;
                }
                if (res.injuries != null && res.injuries.TryGetValue(g.id, out var inj) && inj != null && (g.injury == null || g.injury.daysLeft < inj.daysLeft))
                {
                    g.injury = inj;
                    Career.SeasonRecord(s, g).injuries++;
                    lines.Add($"{g.name}: {g.injury.desc}");
                }
            }
            if (won)
            {
                ex.floor++;
                foreach (var g in party) if (!dead.Contains(g.id)) ex.hp[g.id] = Math.Min(1, res.hpLeft[g.id] + 0.3);
                var drops = RollLoot(rng, d, boss, quest != null ? 1 : 0);
                ex.loot.AddRange(drops);
                int coins = rng.Int(10, 30) * d.tier;
                ex.coins += coins;
                lines.Add($"승리. 전리품: {string.Join(", ", drops.Select(x => GearLogic.ItemName(x)))} · 동전 {coins} D");
                foreach (var kv in res.downs)
                    if (res.sides[1].Contains(kv.Key) && s.gladiators.TryGetValue(kv.Value, out var killer) && party.Contains(killer)) killer.record.kills++;
            }
            else
            {
                lines.Add("패퇴. 남은 자들이 동료를 메고 물러났습니다.");
                ex.status = "failed";
            }
            ex.log.AddRange(lines);
            Msg(s, lines.Any(l => l.Contains("일어나지 못했습니다")) ? "death" : "info",
                $"원정 소식: {d.name} {Num(ex.floor + (won ? 0 : 1))}층",
                string.Join("\n", lines));
            s.inbox[0].replay = FightLink(ex, ex.battles.Count - 1);
        }

        /// <summary>층 전투 한 판. 실제 전투와 다시 보기가 같은 설정을 쓴다 (관중 없는 미시오 규칙)</summary>
        static MatchResult RunBattle(List<Gladiator> party, List<Gladiator> foes, FloorBattle b)
        {
            var ours = Tactics.AutoTeam(null, party);
            ours.policy = b.policy;
            return Sim.RunTeam(party, foes, new MatchOptions
            {
                rule = "missio", seed = b.seed ?? 0, noCrowd = true, deathChanceOf = b.deathOf,
                teamTactics = new[] { ours, Tactics.AutoTeam(null, foes) },
                startHp = b.startHp?.ToArray(),
            });
        }

        /// <summary>전투 다시 보기 링크 id (fight:원정:층 순서)</summary>
        public static string FightLink(Expedition ex, int index) => $"fight:{ex.id}:{index}";

        /// <summary>전투 기록용 사본: 경력·일대기는 빼서 저장을 가볍게</summary>
        static Gladiator Snapshot(Gladiator g)
        {
            var c = MatchLogic.CloneGladiator(g);
            c.history = new List<SeasonRecord>();
            c.career = new List<CareerEvent>();
            return c;
        }

        /// <summary>층 전투를 시드 그대로 다시 돌린다. 옛 방식 기록(시드 없음)이면 null</summary>
        public static MatchResult Replay(FloorBattle fb) => fb.seed == null ? null : RunBattle(fb.party, fb.foes, fb);

        /// <summary>첫 층 적의 대략의 능력 (화면 난이도 별). 목표 평균 능력치 → CA 환산은 GladiatorGen과 같다</summary>
        public static double FoeCa(DungeonDef d) => (d.level * FoeLevelMult - 1) / 19 * 200;

        static List<Gladiator> SpawnFoes(Rng rng, DungeonDef d, bool boss, double hard, double floor)
        {
            // 난전이니 적은 늘 다섯. 같은 이름은 번호로 구분한다
            const int n = 5;
            var o = new List<Gladiator>();
            var named = new Dictionary<string, int>();
            for (int i = 0; i < n; i++)
            {
                var kind = boss && i == 0 ? d.boss : rng.Pick(d.foes);
                double lvl = Math.Min(20, JsMath.Round((d.level + floor * 0.6 + (boss && i == 0 ? 2 : 0)) * hard * FoeLevelMult));
                var g = GladiatorGen.Generate(rng, new GenOptions { id = $"M{i}_{rng.Int(0, 1000000000)}", nation = rng.Pick(Data.Nations).id, target = lvl, race = kind.race });
                named[kind.name] = named.TryGetValue(kind.name, out var cnt) ? cnt + 1 : 1;
                g.name = named[kind.name] > 1 ? $"{kind.name} {named[kind.name]}" : kind.name;
                o.Add(g);
            }
            return o;
        }

        static List<string> LootPool() =>
            Data.Weapons.Select(w => w.id).Concat(Data.Armors.Select(a => a.id)).Where(id => id != "loincloth" && id != "cestus" && Uniques.InPool(id)).ToList();

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
                return q != 0 ? Uniques.Assign(rng, $"{@base}+{q}") : @base;
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
                    if (!FmRules.Frozen(s)) me.rp += q.rp;
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
            // 웹판과 같은 결과가 필요할 때(FM 규칙 끔)는 앞 다섯 안에서만 고른다
            var d = FmRules.Enabled ? rng.Pick(Dungeons.Where(x => x.tier >= 3).ToList()) : Dungeons[rng.Int(2, 4)];
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
