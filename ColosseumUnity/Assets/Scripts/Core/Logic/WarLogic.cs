// 나라 사이 전쟁과 용병 의뢰 (src/core/war.ts, docs/08_mercenary.md)
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Colosseum.Core
{
    public class ContractSpec
    {
        public string name, desc;
        public int[] slots, days;
        public double minCA, pay, risk;
    }

    public static class WarLogic
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

        static double Now(GameState s) => Calendar.AbsDay(s.season, s.day);

        static Gladiator Glad(GameState s, string id) => id != null && s.gladiators.TryGetValue(id, out var g) ? g : null;

        public static readonly Dictionary<string, ContractSpec> ContractTypes = new()
        {
            ["escort"] = new ContractSpec { name = "호위", desc = "보급 행렬과 귀족을 지킵니다. 위험이 낮습니다", slots = new[] { 1, 2 }, days = new[] { 14, 21 }, minCA = 40, pay = 400, risk = 0.02 },
            ["front"] = new ContractSpec { name = "전선 파견", desc = "군단과 함께 전선에 섭니다", slots = new[] { 3, 5 }, days = new[] { 28, 42 }, minCA = 60, pay = 900, risk = 0.08 },
            ["siege"] = new ContractSpec { name = "공성전", desc = "성벽을 오릅니다. 죽기 쉽지만 보수가 큽니다", slots = new[] { 3, 6 }, days = new[] { 21, 21 }, minCA = 70, pay = 1400, risk = 0.15 },
            ["raid"] = new ContractSpec { name = "정찰·암습", desc = "적진에 숨어들어 지휘관을 노립니다", slots = new[] { 1, 2 }, days = new[] { 14, 14 }, minCA = 55, pay = 1100, risk = 0.1 },
            ["duel"] = new ContractSpec { name = "대리 결투", desc = "양국 대표가 1:1 사투로 전황을 가릅니다. 이기면 영웅, 지면 거의 죽습니다", slots = new[] { 1, 1 }, days = new[] { 7, 7 }, minCA = 120, pay = 5000, risk = 0 },
            ["levy"] = new ContractSpec { name = "징발령", desc = "조국이 루두스에 검투사를 내놓으라 합니다. 보수는 적고, 거부하면 대가가 큽니다", slots = new[] { 2, 2 }, days = new[] { 28, 28 }, minCA = 30, pay = 450, risk = 0.06 },
        };

        public static double Relation(GameState s, string a, string b) =>
            s.relations.TryGetValue(a, out var m) && m != null && m.TryGetValue(b, out var v) ? v : 0;

        static void SetRelation(GameState s, string a, string b, double v)
        {
            s.relations[a][b] = v;
            s.relations[b][a] = v;
        }

        public static List<War> ActiveWars(GameState s) => s.wars.Where(w => !w.over).ToList();

        public static double FavorOf(GameState s, string n)
        {
            var f = s.ludi[s.playerLudusId].favor;
            return f != null && f.TryGetValue(n, out var v) ? v : 0;
        }

        static void AddFavor(GameState s, string n, double d)
        {
            var p = s.ludi[s.playerLudusId];
            p.favor ??= new Dictionary<string, double>();
            p.favor[n] = Math.Max(-100, Math.Min(100, (p.favor.TryGetValue(n, out var v) ? v : 0) + d));
        }

        /// <summary>매일: 관계 변화, 전쟁 발발·진행·종전, 의뢰 게시·만료·판정</summary>
        public static void DailyWar(GameState s, Rng rng)
        {
            double t = Now(s);
            var nations = Data.Nations;

            // 전쟁 발발: 관계가 -50 이하인 나라 쌍
            for (int i = 0; i < nations.Count; i++)
            {
                for (int j = i + 1; j < nations.Count; j++)
                {
                    string a = nations[i].id, b = nations[j].id;
                    double rel = Relation(s, a, b);
                    if (ActiveWars(s).Any(w => (w.a == a && w.b == b) || (w.a == b && w.b == a))) continue;
                    // 평화로운 날에도 관계는 조금씩 흔들린다
                    if (rng.Chance(0.02)) SetRelation(s, a, b, Math.Max(-100, Math.Min(100, rel + rng.Int(-6, 4))));
                    if (rel > -50) continue;
                    if (rng.Chance(((-rel - 50) * 0.005) / 7)) StartWar(s, rng, a, b);
                }
            }

            foreach (var w in ActiveWars(s))
            {
                w.front = Math.Max(-100, Math.Min(100, w.front + rng.Normal(0, 3)));
                if (t >= w.endsAt || Math.Abs(w.front) >= 100) EndWar(s, rng, w);
                else if ((t - w.startedAt) % 7 == 0) PostContracts(s, rng, w);
            }

            // JS for-of: 처음 잡은 배열을 길이를 매번 보며 훑는다
            var contracts = s.contracts;
            for (int i = 0; i < contracts.Count; i++)
            {
                var c = contracts[i];
                if (c.status == "open" && t > c.expiresAt)
                {
                    c.status = "expired";
                    if (c.type == "levy")
                    {
                        // 징발령을 무시하면 조국이 등을 돌린다
                        AddFavor(s, c.employer, -30);
                        s.ludi[s.playerLudusId].rp -= 50;
                        Msg(s, "warning", "징발령을 거부했습니다", $"{Josa.J(Data.Nation(c.employer).name, "이/가")} 분노했습니다. 호감도 -30, RP -50. 사람들은 우리를 매국 루두스라 부릅니다.");
                    }
                }
                if (c.status != "active") continue;
                if ((t - c.startedAt.Value) % 7 == 0 && t < c.endsAt.Value) WeeklyResolve(s, rng, c);
                if (t >= c.endsAt.Value) FinishContract(s, rng, c);
            }
        }

        static void StartWar(GameState s, Rng rng, string a, string b)
        {
            var w = new War { id = $"W{s.nextId++}", a = a, b = b, startedAt = Now(s), endsAt = Now(s) + rng.Int(28, 84), front = 0, over = false };
            s.wars.Add(w);
            var na = Data.Nation(a);
            var nb = Data.Nation(b);
            bool ours = s.nation == a || s.nation == b;
            Msg(s, "warning", $"전쟁: {na.@short} ↔ {nb.@short}",
                $"{Josa.J(na.name, "과/와")} {nb.name} 사이에 전쟁이 터졌습니다. 양국이 루두스마다 용병을 구하러 다닙니다.\n대륙 정세 → 전쟁·용병에서 의뢰를 볼 수 있습니다." +
                (ours ? "\n\n조국이 전쟁에 휘말렸습니다. 적국의 의뢰는 받을 수 없고, 징발령이 내려올 수 있습니다." : ""));
            PostContracts(s, rng, w);
            // 조국이 참전하면 1부 상위 루두스에 징발령
            var me = s.ludi[s.playerLudusId];
            if (ours && me.tier == 1)
            {
                int rank = s.ludi.Values.Where(l => l.tier == 1 && l.nation == s.nation).OrderByDescending(l => l.rp).ToList().FindIndex(l => l.isPlayer) + 1;
                if (rank <= 5) AddContract(s, rng, w, s.nation, s.nation == a ? b : a, "levy");
            }
        }

        static void AddContract(GameState s, Rng rng, War w, string employer, string enemy, string type)
        {
            var spec = ContractTypes[type];
            double t = Now(s);
            s.contracts.Add(new Contract
            {
                id = $"K{s.nextId++}", warId = w.id, employer = employer, enemy = enemy, type = type,
                slots = rng.Int(spec.slots[0], spec.slots[1]),
                minCA = JsMath.Round(spec.minCA * rng.Range(0.9, 1.15)),
                days = rng.Int(spec.days[0], spec.days[1]),
                pay = JsMath.Round((spec.pay * rng.Range(0.85, 1.25)) / 10) * 10,
                risk = spec.risk,
                postedAt = t, expiresAt = t + 7, status = "open", assigned = new List<string>(), merit = 0,
            });
        }

        static void PostContracts(GameState s, Rng rng, War w)
        {
            foreach (var (emp, enemy) in new[] { (w.a, w.b), (w.b, w.a) })
            {
                int n = rng.Int(1, 2);
                for (int i = 0; i < n; i++)
                {
                    string type = rng.Weighted(new List<KeyValuePair<string, double>>
                    {
                        new("escort", 3), new("front", 3), new("siege", 1.5), new("raid", 1.5), new("duel", 0.5),
                    });
                    AddContract(s, rng, w, emp, enemy, type);
                }
            }
        }

        /// <summary>의뢰를 받을 수 있는가 (조국의 적국 의뢰 금지). 되면 null</summary>
        public static string CanTake(GameState s, Contract c)
        {
            if (c.status != "open") return "이미 마감됐습니다";
            if (c.enemy == s.nation) return "조국을 치는 의뢰는 받을 수 없습니다";
            return null;
        }

        /// <summary>파견할 수 있는 검투사인가. 되면 null</summary>
        public static string Eligible(GameState s, Gladiator g, Contract c)
        {
            if (!g.alive) return "사망";
            if (g.deployment != null) return "이미 파견 중";
            if (g.injury?.severity == "serious") return "중상";
            if (GladiatorRules.ComputeCA(g) < c.minCA) return $"능력 부족 (CA {Num(c.minCA)} 필요)";
            if (s.challenges.Any(x => x.status == "scheduled" && (x.fromGladiator == g.id || x.toGladiator == g.id))) return "경기가 잡혀 있습니다";
            return null;
        }

        /// <summary>의뢰 수락: 선금 30%, 파견 기간 주급은 고용국이 낸다. 되면 null</summary>
        public static string TakeContract(GameState s, string contractId, List<string> ids)
        {
            var c = s.contracts.FirstOrDefault(x => x.id == contractId);
            if (c == null) return "의뢰가 없습니다";
            var no = CanTake(s, c);
            if (no != null) return no;
            if (ids.Count == 0 || ids.Count > c.slots) return $"1~{Num(c.slots)}명을 보내십시오";
            foreach (var id in ids)
            {
                var why = Eligible(s, s.gladiators[id], c);
                if (why != null) return $"{s.gladiators[id].name}: {why}";
            }
            var me = s.ludi[s.playerLudusId];
            c.status = "active";
            c.assigned = ids;
            c.startedAt = Now(s);
            c.endsAt = Now(s) + c.days;
            foreach (var id in ids)
            {
                var g = s.gladiators[id];
                g.deployment = new Deployment { contractId = c.id, until = c.endsAt.Value };
                Career.LogCareer(s, g, $"{Data.Nation(c.employer).@short}의 {ContractTypes[c.type].name} 의뢰로 전장에 나갔습니다", "other");
                // 노예는 끌려가니 사기가 떨어진다
                if (g.status == "slave") g.morale = Math.Max(0, g.morale - 10);
            }
            Economy.AddMoney(s, me, JsMath.Round(c.pay * ids.Count * 0.3), $"{ContractTypes[c.type].name} 선금");
            if (c.type != "levy")
            {
                AddFavor(s, c.employer, 5);
                AddFavor(s, c.enemy, -5);
            }
            return null;
        }

        static readonly string[] Areas = { "arm", "leg", "torso", "head" };

        /// <summary>주간 판정: 검투사마다 전공·무사·부상·사망</summary>
        static void WeeklyResolve(GameState s, Rng rng, Contract c)
        {
            if (c.type == "duel") return;
            var war = s.wars.FirstOrDefault(w => w.id == c.warId);
            var lines = new List<string>();
            double weeks = Math.Max(1, JsMath.Round(c.days / 7));
            foreach (var id in c.assigned.ToList())
            {
                var g = Glad(s, id);
                if (g == null || !g.alive || g.deployment == null) continue;
                double margin = (GladiatorRules.ComputeCA(g) - c.minCA) / 100;
                double death = Math.Max(0.005, (c.risk / weeks) * (1 - margin));
                double roll = rng.Next();
                Training.Grow(g, rng, "match", 10, 1, 1);
                if (roll < death)
                {
                    g.alive = false;
                    g.diedAt = new DiedAt { season = s.season, day = s.day, cause = $"{Data.Nation(c.employer).@short}의 {ContractTypes[c.type].name}에서 전사" };
                    g.deployment = null;
                    var me = s.ludi[s.playerLudusId];
                    me.gladiators = me.gladiators.Where(x => x != id).ToList();
                    s.fallen.Add(id);
                    c.assigned = c.assigned.Where(x => x != id).ToList();
                    Career.LogCareer(s, g, "전장에서 쓰러져 돌아오지 못했습니다", "death");
                    lines.Add($"{Josa.J(g.name, "이/가")} 전사했습니다.");
                    foreach (var other in me.gladiators) s.gladiators[other].morale = Math.Max(0, s.gladiators[other].morale - 5);
                }
                else if (roll < death + 0.12)
                {
                    g.injury = GladiatorRules.MakeInjury(rng, rng.Chance(0.3) ? "moderate" : "minor", rng.Pick(Areas));
                    Career.SeasonRecord(s, g).injuries++;
                    lines.Add($"{g.name}: {g.injury.desc}");
                }
                else if (rng.Chance(0.25 + margin))
                {
                    c.merit++;
                    lines.Add($"{g.name}: 전공을 세웠습니다");
                    if (war != null) war.front += (war.a == c.employer ? 1 : -1) * 4;
                }
            }
            if (lines.Count > 0) Msg(s, lines.Any(l => l.Contains("전사")) ? "death" : "info", $"전장 소식: {ContractTypes[c.type].name}", string.Join("\n", lines));
        }

        static void FinishContract(GameState s, Rng rng, Contract c)
        {
            var me = s.ludi[s.playerLudusId];
            var war = s.wars.FirstOrDefault(w => w.id == c.warId);
            c.status = "done";
            if (c.type == "duel")
            {
                var champ = c.assigned.Count > 0 ? Glad(s, c.assigned[0]) : null;
                if (champ != null && champ.alive)
                {
                    var foe = GladiatorGen.Generate(rng, new GenOptions { id = "foe", nation = c.enemy, target = Math.Min(18, c.minCA / 10 + 2) });
                    foe.name = $"{Data.Nation(c.enemy).@short}의 챔피언 {foe.name}";
                    var r = Sim.Run(champ, foe, new MatchOptions { rule = "sudden", seed = Seed31(rng) });
                    if (r.winnerId == champ.id)
                    {
                        c.merit += 10;
                        if (war != null) war.front += (war.a == c.employer ? 1 : -1) * 40;
                        AddTrait(champ, "war_hero");
                        champ.popularity = Math.Min(100, champ.popularity + 15);
                        Career.LogCareer(s, champ, $"대리 결투에서 {Josa.J(foe.name, "을/를")} 꺾고 영웅이 됐습니다", "milestone");
                        AddFavor(s, c.employer, 25);
                        AddFavor(s, c.enemy, -25);
                        Msg(s, "match", $"대리 결투 승리: {champ.name}", $"{Josa.J(champ.name, "이/가")} {Josa.J(foe.name, "을/를")} 쓰러뜨렸습니다. 전황이 크게 기울었습니다.");
                    }
                    else if (!string.IsNullOrEmpty(r.died))
                    {
                        champ.alive = false;
                        champ.diedAt = new DiedAt { season = s.season, day = s.day, cause = $"대리 결투에서 {foe.name}에게 패배" };
                        var cid = champ.id;
                        me.gladiators = me.gladiators.Where(x => x != cid).ToList();
                        s.fallen.Add(champ.id);
                        c.assigned = new List<string>();
                        Career.LogCareer(s, champ, $"대리 결투에서 {foe.name}에게 졌습니다", "death");
                        Msg(s, "death", $"대리 결투 패배: {champ.name}", $"{Josa.J(champ.name, "이/가")} 돌아오지 못했습니다.");
                    }
                }
            }
            var survivors = c.assigned.Where(id => Glad(s, id)?.alive == true).ToList();
            double rest = JsMath.Round(c.pay * survivors.Count * 0.7 + c.merit * 150);
            if (rest != 0) Economy.AddMoney(s, me, rest, $"{ContractTypes[c.type].name} 완료 보수");
            // 전사자 위로금은 고용국이 낸다
            int dead = c.assigned.Count - survivors.Count + (c.type == "duel" && survivors.Count == 0 ? 1 : 0);
            foreach (var id in survivors)
            {
                var g = s.gladiators[id];
                g.deployment = null;
                if (c.merit >= 3 && rng.Chance(0.3)) AddTrait(g, "war_hero");
                Career.LogCareer(s, g, $"{Data.Nation(c.employer).@short}의 전장에서 돌아왔습니다", "other");
            }
            if (c.type != "levy") AddFavor(s, c.employer, 10);
            Msg(s, "info", $"{ContractTypes[c.type].name} 의뢰 완료",
                $"돌아온 자 {survivors.Count}명{(dead != 0 ? $" · 돌아오지 못한 자 {dead}명" : "")} · 전공 {Num(c.merit)}\n완료 보수 {Loc(rest)} D (세전)");
        }

        static void AddTrait(Gladiator g, string id)
        {
            if (!g.traits.Contains(id) && Data.Trait(id) != null) g.traits.Add(id);
        }

        static void EndWar(GameState s, Rng rng, War w)
        {
            w.over = true;
            w.winner = w.front >= 0 ? w.a : w.b;
            string loser = w.winner == w.a ? w.b : w.a;
            SetRelation(s, w.a, w.b, -30);
            // 패전국 포로가 무소속 명단에 나온다
            if (loser != s.nation) s.powPending = loser;
            foreach (var c in s.contracts) if (c.warId == w.id && c.status == "open") c.status = "expired";
            Msg(s, "info", $"종전: {Data.Nation(w.winner).@short} 승리",
                $"{Josa.J(Data.Nation(w.winner).name, "이/가")} {Josa.J(Data.Nation(loser).name, "을/를")} 꺾었습니다. {(loser != s.nation ? "곧 포로들이 무소속 명단에 헐값으로 나옵니다." : "조국이 졌습니다. 다음 시즌 살림이 빠듯해질 것입니다.")}");
        }
    }
}
