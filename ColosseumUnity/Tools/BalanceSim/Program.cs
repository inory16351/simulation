// 밸런스 시뮬레이션 (Unity 없이): 전투(능력 차이별 승률·능력치별 영향·스타일 상성·교범·운)와 경제(시작 리그별 금고 흐름), 하루 처리 속도.
// 사용 (ColosseumUnity 폴더에서): dotnet build Colosseum.Core.csproj && dotnet run --project Tools/BalanceSim -- combat|econ|all
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Colosseum.Core;
using Colosseum.Core.Combat;

class Program
{
    static int Signed;
    static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Data.LoadFrom(n => File.ReadAllText($"Assets/Resources/Data/{n}.json"));
        SkillBook.LoadFrom(n => File.ReadAllText($"Tables/{n}.csv"));
        NameBook.LoadFrom(n => File.ReadAllText($"Tables/{n}.csv"));
        string mode = args.Length > 0 ? args[0] : "all";
        if (mode is "combat" or "all") Combat();
        if (mode is "econ" or "all") Econ(args.Length > 1 ? int.Parse(args[1]) : 2);
        if (mode == "melee") MeleeCost();
        if (mode == "chal") ChallengeDebug();
        if (mode == "wages") Wages();
        if (mode == "aifin") AiFinance(args.Length > 1 ? int.Parse(args[1]) : 2);
        if (mode == "days")
        {
            var s = World.NewGame(4004, "aurelia", "시험 루두스", 2);
            var arng = new Rng(66); for (int i = 0; i < int.Parse(args[1]); i++) { var w = Stopwatch.StartNew(); Autoplay(s, arng); Console.WriteLine($"{s.day}일 {w.ElapsedMilliseconds} ms"); }
        }
        if (mode == "illegal")
        {
            var s = World.NewGame(4004, "aurelia", "시험 루두스", 2);
            var by = new Dictionary<string, int>();
            foreach (var g in s.gladiators.Values)
                foreach (var sl in Equipment.Slots)
                {
                    var it = g.equipment.Get(sl);
                    if (string.IsNullOrEmpty(it) || it == "cestus" || it == "loincloth") continue;
                    var why = GearLogic.CannotEquip(g, sl, it);
                    if (why != null) { string k = $"{sl} {it} {g.style} ({why})"; by[k] = (by.TryGetValue(k, out var v) ? v : 0) + 1; }
                }
            foreach (var (k, v) in by.OrderByDescending(x => x.Value).Take(12)) Console.WriteLine($"{v,5} {k}");
        }
        if (mode == "prof") Prof(args.Length > 1 ? int.Parse(args[1]) : 20, args.Length > 2 ? int.Parse(args[2]) : 15);
        if (mode == "ai") AiOps(args.Length > 1 ? int.Parse(args[1]) : 3);
        if (mode == "world") WorldStats(args.Length > 1 ? int.Parse(args[1]) : 1, args.Length > 2 ? int.Parse(args[2]) : 2);
        return 0;
    }

    static Gladiator Gen(Rng rng, string id, double target, string style = null, string nation = "aurelia") =>
        GladiatorGen.Generate(rng, new GenOptions { id = id, nation = nation, target = target, style = style, age = 26 });

    static Gladiator Clone(Gladiator g, string id)
    {
        var c = MatchLogic.CloneGladiator(g);
        c.id = id;
        return c;
    }

    static bool AWins(Gladiator a, Gladiator b, uint seed, Doctrine da = null, Doctrine db = null)
    {
        var r = Sim.Run(Clone(a, "A"), Clone(b, "B"), new MatchOptions { rule = "missio", seed = seed, medicalLevel = new double[] { 1, 1 }, noDeath = true, doctrines = da != null || db != null ? new[] { da ?? a.doctrine, db ?? b.doctrine } : null });
        return r.winnerId == "A";
    }

    static void Combat()
    {
        var sw = Stopwatch.StartNew();
        var rng = new Rng(777);
        Console.WriteLine("== 능력(CA) 차이별 강자 승률 ==");
        var buckets = new SortedDictionary<int, (int win, int n)>();
        for (int i = 0; i < 2500; i++)
        {
            double t = rng.Range(5, 14);
            var a = Gen(rng, "A", t);
            var b = Gen(rng, "B", t + rng.Range(-3, 3));
            int ca = GladiatorRules.ComputeCA(a), cb = GladiatorRules.ComputeCA(b);
            int gap = Math.Abs(ca - cb);
            int key = gap < 5 ? 0 : gap < 10 ? 5 : gap < 15 ? 10 : gap < 20 ? 15 : gap < 30 ? 20 : 30;
            bool strongA = ca >= cb;
            bool aw = AWins(a, b, (uint)(i * 7 + 1));
            var (w, n) = buckets.TryGetValue(key, out var x) ? x : (0, 0);
            buckets[key] = (w + (aw == strongA ? 1 : 0), n + 1);
        }
        foreach (var (k, (w, n)) in buckets) Console.WriteLine($"  CA 차이 {k,2}+ : 강자 승률 {100.0 * w / n,5:0.0}%  ({n}판)");

        Console.WriteLine("\n== 능력치 하나 +4 의 효과 (같은 검투사끼리, 올린 쪽 승률) ==");
        var bases = Enumerable.Range(0, 60).Select(i => Gen(rng, "A", 9 + rng.Range(-1, 1))).ToList();
        var effects = new List<(string, double)>();
        foreach (var attr in Attrs.All)
        {
            int win = 0, n = 0;
            foreach (var g in bases)
            {
                var up = Clone(g, "B");
                up.attrs[attr] = Math.Min(20, up.attrs[attr] + 4);
                for (uint s = 1; s <= 6; s++)
                {
                    // 자리 편향을 없애려고 좌우를 번갈아
                    bool upWins = s % 2 == 0 ? !AWins(g, up, s * 13 + (uint)n) : AWins(up, g, s * 13 + (uint)n);
                    if (upWins) win++;
                    n++;
                }
            }
            effects.Add((attr, 100.0 * win / n));
        }
        foreach (var (a, p) in effects.OrderByDescending(e => e.Item2)) Console.WriteLine($"  {a,-7} {Attrs.Names[a],-8} {p,5:0.0}%");
        {
            int w = 0, n = 0;
            foreach (var g in bases) for (uint s = 1; s <= 6; s++) { if (AWins(g, Clone(g, "B"), s * 17 + (uint)n)) w++; n++; }
            Console.WriteLine($"  (자리 A 승률, 같은 검투사끼리: {100.0 * w / n:0.0}%)");
        }

        Console.WriteLine("\n== 스타일 상성 (비슷한 능력, 상성 우위 쪽 승률) ==");
        var styles = Data.Styles.Select(s => s.id).ToList();
        int cw = 0, cn = 0, nw = 0, nn = 0;
        for (int i = 0; i < 1200; i++)
        {
            string sa = rng.Pick(styles), sb = rng.Pick(styles);
            if (sa == sb) continue;
            double t = rng.Range(7, 12);
            var a = Gen(rng, "A", t, sa);
            var b = Gen(rng, "B", t, sb);
            bool aw = AWins(a, b, (uint)(i * 3 + 5));
            bool counters = Data.Style(sa).counters?.Contains(sb) == true;
            bool countered = Data.Style(sb).counters?.Contains(sa) == true;
            if (counters && !countered) { cw += aw ? 1 : 0; cn++; }
            else if (!counters && !countered) { nw += aw ? 1 : 0; nn++; }
        }
        Console.WriteLine($"  상성 우위: {100.0 * cw / Math.Max(1, cn):0.0}% ({cn}판), 상성 없음: {100.0 * nw / Math.Max(1, nn):0.0}% ({nn}판)");

        Console.WriteLine("\n== 교범 (같은 검투사, 기본 교범 대비 자세만 바꿨을 때 승률) ==");
        foreach (var stance in new[] { "assault", "bulwark", "harass", "blitz", "showman" })
        {
            int w = 0, n = 0;
            foreach (var g in bases.Take(20))
            {
                var d = MatchLogic.CloneGladiator(g).doctrine ?? GladiatorRules.DefaultDoctrine(g);
                d.stance = stance;
                for (uint s = 1; s <= 8; s++)
                {
                    bool dw = s % 2 == 0 ? AWins(g, g, s * 31 + (uint)n, d, null) : !AWins(g, g, s * 31 + (uint)n, null, d);
                    if (dw) w++;
                    n++;
                }
            }
            Console.WriteLine($"  {stance,-8} {100.0 * w / n,5:0.0}%");
        }

        Console.WriteLine("\n== 전술: 상대 기본 자세를 이기는 자세로 바꿨을 때 (비슷한 능력, 다른 검투사) ==");
        {
            string Beat(string st) => st switch { "harass" => "assault", "bulwark" => "harass", "assault" => "bulwark", "showman" => "assault", _ => "blitz" };
            int w0 = 0, w1 = 0, n = 0;
            for (int i = 0; i < 400; i++)
            {
                double t = rng.Range(7, 12);
                var a = Gen(rng, "A", t); var b = Gen(rng, "B", t);
                var da = MatchLogic.CloneGladiator(a).doctrine ?? GladiatorRules.DefaultDoctrine(a);
                da.stance = Beat((b.doctrine ?? GladiatorRules.DefaultDoctrine(b)).stance);
                uint seed = (uint)(i * 11 + 3);
                if (AWins(a, b, seed)) w0++;
                if (AWins(a, b, seed, da, null)) w1++;
                n++;
            }
            Console.WriteLine($"  기본 자세 {100.0 * w0 / n:0.0}% → 상성 자세 {100.0 * w1 / n:0.0}%");
        }

        Console.WriteLine("\n== 운: CA 차이 8~14인 두 검투사 40쌍, 쌍마다 60번 → 강자 승률 분포 ==");
        {
            var ps = new List<double>();
            var rounds = new List<double>();
            for (int tries = 0; ps.Count < 40 && tries < 5000; tries++)
            {
                double t = rng.Range(6, 13);
                var a = Gen(rng, "A", t + 0.6); var b = Gen(rng, "B", t);
                int d = GladiatorRules.ComputeCA(a) - GladiatorRules.ComputeCA(b);
                if (d < 8 || d > 14) continue;
                int w = 0;
                for (uint s = 1; s <= 60; s++)
                {
                    var r = s % 2 == 0
                        ? Sim.Run(Clone(a, "A"), Clone(b, "B"), new MatchOptions { rule = "missio", seed = s * 101 + (uint)tries, medicalLevel = new double[] { 1, 1 }, noDeath = true })
                        : Sim.Run(Clone(b, "B"), Clone(a, "A"), new MatchOptions { rule = "missio", seed = s * 101 + (uint)tries, medicalLevel = new double[] { 1, 1 }, noDeath = true });
                    if (r.winnerId == "A") w++;
                    rounds.Add(r.rounds);
                }
                ps.Add(w / 60.0);
            }
            ps.Sort();
            Console.WriteLine($"  평균 {ps.Average() * 100:0.0}% · 하위 25% {ps[ps.Count / 4] * 100:0}% · 중앙 {ps[ps.Count / 2] * 100:0}% · 상위 25% {ps[ps.Count * 3 / 4] * 100:0}% · 90% 넘는 쌍 {ps.Count(p => p > 0.9)}개 · 약자가 더 이긴 쌍 {ps.Count(p => p < 0.5)}개 · 평균 {rounds.Average():0.0}라운드");
        }
        Console.WriteLine($"\n(전투 {sw.Elapsed.TotalSeconds:0.0}초)");
    }

    /// <summary>5:5 난전 한 판 비용 (국내 컵 하루 최대 120판을 견디는지)</summary>
    static void MeleeCost()
    {
        var s = World.NewGame(5, "aurelia", "시험", 2);
        var rng = new Rng(7);
        var ludi = s.ludi.Values.Where(l => !l.isPlayer).ToList();
        var sw = Stopwatch.StartNew();
        int n = 0;
        for (int i = 0; i + 1 < ludi.Count && n < 100; i += 2)
        {
            var la = SeriesLogic.AutoLineup(s, ludi[i], 5, 10);
            var lb = SeriesLogic.AutoLineup(s, ludi[i + 1], 5, 10);
            if (la.Count == 0 || lb.Count == 0) continue;
            SeriesLogic.RunSeries(s, new[] { ludi[i], ludi[i + 1] }, new[] { la, lb }, new RunOptions { rule = "missio", mode = "best_of", homeLudusId = ludi[i].id }, rng);
            n++;
        }
        Console.WriteLine($"5:5 난전 {n}판 {sw.ElapsedMilliseconds} ms (판당 {sw.ElapsedMilliseconds / (double)n:0.0} ms)");
    }

    /// <summary>우리 도전장이 왜 안 나가는지, 리그 경기 수가 얼마나 되는지</summary>
    static void ChallengeDebug()
    {
        var s = World.NewGame(1001, "aurelia", "시험", 1);
        var rng = new Rng(100);
        var me = s.ludi[s.playerLudusId];
        var why = new Dictionary<string, int>();
        for (int d = 0; d < 168 * 2 && s.gameOver == null; d++)
        {
            if (s.day % 3 == 0 && s.day >= 15 && s.day <= FmRules.ChallengeClose)
            {
                foreach (var l in s.ludi.Values.Where(l => !l.isPlayer && l.nation == me.nation && l.tier == me.tier))
                {
                    var e = ChallengeLogic.CanChallenge(s, me, l) ?? "가능";
                    why[e] = (why.TryGetValue(e, out var v) ? v : 0) + 1;
                }
                var g = ChallengeLogic.PickFighter(s, me, "missio", ChallengeLogic.Now(s));
                var k = g == null ? $"우리 출전자 없음 ({string.Join(",", me.gladiators.Select(id => ChallengeLogic.Availability(s, s.gladiators[id]) ?? (Condition.IsJaded(s.gladiators[id]) ? "지침" : "ok")).Distinct())})" : "우리 출전자 있음";
                why[k] = (why.TryGetValue(k, out var w) ? w : 0) + 1;
            }
            if (s.day == FmRules.RegularEnd + 1) { Console.WriteLine($"시즌 {s.season} 리그 끝: 우리 {me.matchesThisSeason}경기, AI 평균 {s.ludi.Values.Where(l => !l.isPlayer).Average(l => l.matchesThisSeason):0.0}경기"); }
            Autoplay(s, rng);
        }
        foreach (var kv in why.OrderByDescending(x => x.Value)) Console.WriteLine($"  {kv.Value,5}  {kv.Key}");
    }

    // ── 경제 ──

    /// <summary>AI 루두스 금고: 부별 금고 중앙값·적자 수·주급 합·인원 (시즌 안 몇 번)</summary>
    static void AiFinance(int seasons)
    {
        var s = World.NewGame(2001, "aurelia", "시험 루두스", 2);
        var rng = new Rng(77);
        void Report()
        {
            Console.WriteLine($"  시즌 {s.season} {s.day}일");
            for (int tier = 1; tier <= 4; tier++)
            {
                var ai = s.ludi.Values.Where(l => !l.isPlayer && l.tier == tier).ToList();
                var tr = ai.Select(l => l.treasury).OrderBy(x => x).ToList();
                var wages = ai.Select(l => Economy.WeeklyWages(s, l)).ToList();
                Console.WriteLine($"    {tier}부 {ai.Count}곳: 금고 중앙값 {tr[tr.Count / 2],8:N0} · 하위10% {tr[tr.Count / 10],8:N0} · 적자 {tr.Count(x => x < 0),2}곳 · 주급 합 평균 {wages.Average(),5:0} (최대 {wages.Max():0}) · 예산 평균 {ai.Average(l => Economy.WageBudget(s, l)),5:0} · 인원 {ai.Average(l => l.gladiators.Count(id => s.gladiators[id].alive)):0.0} · 경기 {ai.Average(l => l.matchesThisSeason):0.0}");
            }
        }
        Report();
        int start = s.season, days = 0;
        // 부별 실제 수입 (금고 변화 + 급료일에 낸 주급·식비)
        var earned = new double[5];
        while (s.season < start + seasons && s.gameOver == null)
        {
            var ai = s.ludi.Values.Where(l => !l.isPlayer).ToList();
            bool pay = Calendar.IsPayday(s.day);
            var pre = ai.ToDictionary(l => l.id, l => (l.treasury, cost: pay ? Economy.WeeklyWages(s, l) + Economy.WeeklyFood(s, l) : 0, l.tier));
            Autoplay(s, rng);
            days++;
            foreach (var l in ai) earned[pre[l.id].tier] += l.treasury - pre[l.id].treasury + pre[l.id].cost;
            if (s.day is 60 or 120 or 1) Report();
        }
        double weeks = days / 7.0;
        for (int t = 1; t <= 4; t++)
        {
            var l0 = s.ludi.Values.First(l => !l.isPlayer && l.tier == t);
            Console.WriteLine($"  {t}부 실제 수입 {earned[t] / 60 / weeks:0}/주 (예상 {Economy.ExpectedWeeklyIncome(s, l0):0})");
        }
    }

    /// <summary>하루 처리 시간 나눠 재기: startDay까지 진행한 뒤 days일 동안 하루 전체와 주요 단계를 따로 잰다</summary>
    static void Prof(int days, int startDay)
    {
        var s = World.NewGame(4004, "aurelia", "시험 루두스", 2);
        var rng = new Rng(66);
        var arng = new Rng(66);
        while (Calendar.AbsDay(s.season, s.day) < startDay) Autoplay(s, arng);
        var t = new Dictionary<string, double>();
        void M(string k, Action a) { var w = Stopwatch.StartNew(); a(); t[k] = (t.TryGetValue(k, out var v) ? v : 0) + w.Elapsed.TotalMilliseconds; }
        for (int i = 0; i < days; i++)
        {
            M("AiBrain.Daily", () => AiBrain.Daily(s, new Rng(1)));
            M("DailyConsignments", () => GearLogic.DailyConsignments(s, new Rng(1)));
            M("Registration.Daily", () => Registration.Daily(s));
            M("Negotiations.Daily", () => Negotiations.Daily(s, new Rng(1)));
            M("Grievances.Daily", () => Grievances.Daily(s, new Rng(1)));
            M("Transfers.DailyTransfers", () => Transfers.DailyTransfers(s, new Rng(1)));
            M("Events", () => Events.RollDailyEvents(s, new Rng(1)));
            M("DailyChallenges", () => ChallengeLogic.DailyChallenges(s, new Rng(1)));
            M("PlayAiScheduled", () => ChallengeLogic.PlayAiScheduled(s, new Rng(1)));
            M("Scouting", () => Scouting.DailyScouting(s));
            M("Dungeons", () => DungeonLogic.DailyDungeons(s, new Rng(1)));
            M("FreeAgency.Weekly", () => FreeAgency.Weekly(s, new Rng(1)));
            M("Payday(전체 루두스)", () => { foreach (var l in s.ludi.Values.ToList()) Economy.Payday(s, l); });
            M("SkillBook.WeeklyLearn", () => SkillBook.WeeklyLearn(s, new Rng(1)));
            M("Autoplay(하루 전체)", () => Autoplay(s, arng));
        }
        foreach (var (k, v) in t.OrderByDescending(x => x.Value)) Console.WriteLine($"{k,-28} {v / days,8:0.0} ms/일");
        Console.WriteLine($"장비 시장 {s.gearMarket?.Count} (AI 매물 {s.gearMarket?.Count(x => x.sellerLudus != null)}) · 위탁 {s.consignments?.Count} · AI 창고 합 {s.ludi.Values.Sum(l => l.armory?.Count ?? 0)} · 식솔 {s.staff.Count} · 식솔 시장 {s.staffMarket.Count} · 검투사 {s.gladiators.Count} · 무소속 {s.freeAgents.Count} · 받은 소식 {s.inbox.Count} · 장부 {s.ledger.Count}");
        // AiBrain 단계별
        var t2 = new Dictionary<string, double>();
        foreach (var l in s.ludi.Values.Where(x => !x.isPlayer).Take(70))
        {
            var w = Stopwatch.StartNew(); AiBrain.Turn(s, new Rng(1), l); t2["turn"] = (t2.TryGetValue("turn", out var v0) ? v0 : 0) + w.Elapsed.TotalMilliseconds;
        }
        Console.WriteLine($"AiBrain.Turn 70곳 {t2["turn"]:0} ms");
    }

    /// <summary>FM 세계 단계 7: AI 운영 판단 측정 (몇 곳이 던전·장비·식솔·시설을 했나, 금고 음수 비율, 불법 장비, 하루 처리 시간)</summary>
    static void AiOps(int seasons)
    {
        var s = World.NewGame(4004, "aurelia", "시험 루두스", 2);
        var rng = new Rng(66);
        AiBrain.Did.Clear();
        int start = s.season, days = 0;
        var sw = Stopwatch.StartNew();
        int ai = s.ludi.Values.Count(l => !l.isPlayer);
        int weeks = 0; double siteSum = 0, aiSum = 0;
        while (s.season < start + seasons && s.gameOver == null)
        {
            Autoplay(s, rng);
            days++;
            // 급료일 전날: 이번 주 던전 자리가 얼마나 찼나 (AI는 주에 60%까지)
            if (Calendar.DaysUntilPayday(s.day) == 1)
            {
                var sites = DungeonLogic.Sites(s);
                int byAi = sites.Count(x => x.claimedBy != null && x.claimedBy != s.playerLudusId);
                weeks++; siteSum += sites.Count; aiSum += byAi;
            }
            if (s.day == 1)
            {
                Console.WriteLine($"  던전 자리: 주 평균 {siteSum / Math.Max(1, weeks):0.0}곳 중 AI가 {aiSum / Math.Max(1, weeks):0.0}곳 차지 (주말 기준)");
                weeks = 0; siteSum = 0; aiSum = 0;
                var neg = s.ludi.Values.Where(l => !l.isPlayer).Count(l => l.treasury < 0);
                int illegal = 0;
                foreach (var l in s.ludi.Values)
                    foreach (var id in l.gladiators)
                    {
                        var g = s.gladiators[id];
                        if (!g.alive) continue;
                        foreach (var sl in Equipment.Slots)
                        {
                            var it = g.equipment.Get(sl);
                            if (string.IsNullOrEmpty(it) || it == "cestus" || it == "loincloth") continue;
                            if (sl == "off" && g.style == "dimachaerus" && it == Data.Style(g.style).loadout.off) continue; // 쌍검 기본 장비는 스타일 규칙
                            if (GearLogic.CannotEquip(g, sl, it) != null) illegal++;
                        }
                    }
                string Pct(string k) => AiBrain.Did.TryGetValue(k, out var set) ? $"{set.Count * 100.0 / ai:0}%" : "0%";
                var med = s.ludi.Values.Where(l => !l.isPlayer).GroupBy(l => l.tier).OrderBy(g => g.Key).Select(g => { var t = g.Select(l => l.treasury).OrderBy(x => x).ToList(); return $"{g.Key}부 {t[t.Count / 2]:N0}"; });
                Console.WriteLine($"시즌 {s.season - 1} 끝: 던전 {Pct("dungeon")} · 장비 교체 {Pct("gear")} · 식솔 고용 {Pct("staff")} · 시설 {Pct("facility")} · 매물 내놓음 {Pct("sell")} · 구매 {Pct("buy")} · 방출 {Pct("release")} | 금고 음수 {neg}/{ai} · 불법 장비 {illegal} · 금고 중앙값 {string.Join(" ", med)} · 식솔 평균 {s.ludi.Values.Where(l => !l.isPlayer).Average(l => l.staff.Count):0.0}명 · 하루 {sw.Elapsed.TotalMilliseconds / days:0} ms");
            }
        }
    }

    /// <summary>세계 측정 (FM 세계 확장 기준값): 나라·부별 주전(상위 5)·벤치 CA, 팀 간 편차, 주급 합, 금고, 하루 처리 시간, 저장 크기</summary>
    static void WorldStats(int seasons, int startTier)
    {
        var sw = Stopwatch.StartNew();
        var s = World.NewGame(3001, "aurelia", "시험 루두스", startTier);
        Console.WriteLine($"새 게임 {sw.ElapsedMilliseconds} ms · 루두스 {s.ludi.Count} · 검투사 {s.gladiators.Count} · 무소속 {s.freeAgents.Count} · 저장 {SaveSystem.ToJson(s).Length / 1024} KB");
        var rng = new Rng(55);
        void Report(string when)
        {
            Console.WriteLine($"── {when} (시즌 {s.season} {s.day}일) · 검투사 {s.gladiators.Values.Count(g => g.alive)} 생존 · 저장 {SaveSystem.ToJson(s).Length / 1024} KB");
            foreach (var n in Data.Nations.Select(x => x.id))
            {
                var line = new List<string>();
                for (int tier = 1; tier <= FmRules.MaxTier; tier++)
                {
                    var ls = s.ludi.Values.Where(l => l.nation == n && l.tier == tier).ToList();
                    if (ls.Count == 0) continue;
                    var tops = new List<double>(); var benches = new List<double>();
                    foreach (var l in ls)
                    {
                        var cas = l.gladiators.Select(id => s.gladiators[id]).Where(g => g.alive).Select(g => (double)GladiatorRules.ComputeCA(g)).OrderByDescending(x => x).ToList();
                        tops.Add(cas.Take(5).DefaultIfEmpty(0).Average());
                        benches.Add(cas.Skip(5).DefaultIfEmpty(0).Average());
                    }
                    line.Add($"{tier}부 {ls.Count}팀 주전 {tops.Average(),5:0.0}({tops.Min():0}~{tops.Max():0}) 벤치 {benches.Average(),4:0}");
                }
                Console.WriteLine($"  {n,-9} {string.Join(" | ", line)}");
            }
            for (int tier = 1; tier <= FmRules.MaxTier; tier++)
            {
                var ls = s.ludi.Values.Where(l => l.tier == tier && !l.isPlayer).ToList();
                var tr = ls.Select(l => l.treasury).OrderBy(x => x).ToList();
                Console.WriteLine($"  {tier}부 전체: 인원 {ls.Average(l => l.gladiators.Count(id => s.gladiators[id].alive)):0.0} · 주급 합 평균 {ls.Average(l => Economy.WeeklyWages(s, l)):0}/주 · 금고 중앙값 {tr[tr.Count / 2]:N0} · 적자 {tr.Count(x => x < 0)}곳 · 기대 수입 {ls.Average(l => Economy.ExpectedWeeklyIncome(s, l)):0}/주");
            }
            var me = s.ludi[s.playerLudusId];
            Console.WriteLine($"  우리 {me.tier}부: 금고 {me.treasury:N0} · 주급 {Economy.WeeklyWages(s, me):0}/주 · 인원 {me.gladiators.Count}");
        }
        Report("시작");
        int start = s.season, days = 0;
        var dayWatch = new Stopwatch();
        double worst = 0;
        while (s.season < start + seasons && s.gameOver == null)
        {
            dayWatch.Restart();
            Autoplay(s, rng);
            worst = Math.Max(worst, dayWatch.Elapsed.TotalMilliseconds);
            days++;
            if (s.day == 1) Report("시즌 끝");
            else if (s.day == 106) Report("정규 리그 끝");
        }
        Console.WriteLine($"하루 평균 {sw.Elapsed.TotalMilliseconds / Math.Max(1, days):0.0} ms (최대 {worst:0} ms, {days}일)");
    }

    /// <summary>부별 명성·격·주급, 무소속이 우리(그 부)에게 바라는 주급</summary>
    static void Wages()
    {
        for (int tier = 1; tier <= 4; tier++)
        {
            var s = World.NewGame(1000 + tier, "aurelia", "시험 루두스", tier);
            var me = s.ludi[s.playerLudusId];
            var ai = s.ludi.Values.Where(l => !l.isPlayer && l.tier == tier).ToList();
            var gs = ai.SelectMany(l => l.gladiators).Select(id => s.gladiators[id]).ToList();
            double myRep = Reputation.LudusReputation(s, me);
            var fa = s.freeAgents.Select(id => s.gladiators[id]).Select(g => (g, d: Economy.FreeAgentDemand(s, g))).ToList();
            var ok = fa.Where(x => !x.d.refuse).ToList();
            Console.WriteLine($"{tier}부: 우리 명성 {myRep:0}, AI 명성 평균 {ai.Average(l => Reputation.LudusReputation(s, l)):0.0} · 검투사 격 평균 {gs.Average(Reputation.GladiatorStanding):0.0} · CA {gs.Average(g => GladiatorRules.ComputeCA(g)):0} · 주급 {gs.Average(g => g.wage):0} (우리 {me.gladiators.Average(id => s.gladiators[id].wage):0})");
            Console.WriteLine($"     무소속 {fa.Count}명 (거절 {fa.Count - ok.Count}) · 격 {ok.Average(x => Reputation.GladiatorStanding(x.g)):0.0} · CA {ok.Average(x => GladiatorRules.ComputeCA(x.g)):0} · 지금 주급 {ok.Average(x => x.g.wage):0} → 요구 {ok.Average(x => x.d.wage):0} (×{ok.Average(x => x.d.wage / Math.Max(1, x.g.wage)):0.00})");
            Console.WriteLine($"     주당 고정비: 주급 {Economy.WeeklyWages(s, me):0}, 식솔 {StaffLogic.StaffWages(s, me):0} · 경기 상금 {FmRules.TierMoney("purseFm", tier)} · 관중 {FmRules.TierMoney("ticketIncomeFm", tier)} · 배당 {FmRules.TierMoney("dividendBaseFm", tier)}");
        }
    }

    static void Econ(int seasons)
    {
        for (int tier = 1; tier <= 4; tier++)
        {
            var sw = Stopwatch.StartNew();
            Signed = 0;
            var s = World.NewGame(1000 + tier, "aurelia", "시험 루두스", tier);
            var rng = new Rng((uint)(99 + tier));
            var me = s.ludi[s.playerLudusId];
            Console.WriteLine($"\n== {tier}부 시작: 금고 {me.treasury:N0} D, 검투사 {me.gladiators.Count}명, 주급 합 {Economy.WeeklyWages(s, me):N0} D, 루두스 {s.ludi.Count}개, 검투사 {s.gladiators.Count}명 ==");
            int days = 0;
            var income = new Dictionary<string, double>();
            double minTreasury = me.treasury;
            int startSeason = s.season;
            while (s.season < startSeason + seasons && s.gameOver == null)
            {
                int ledgerBefore = s.ledger.Count;
                var before = s.ledger.FirstOrDefault();
                Autoplay(s, rng);
                days++;
                foreach (var e in s.ledger.TakeWhile(e => e != before))
                {
                    string k = Category(e.desc);
                    income[k] = (income.TryGetValue(k, out var v) ? v : 0) + e.amount;
                }
                minTreasury = Math.Min(minTreasury, me.treasury);
                if (s.day == 1 && days > 1)
                {
                    var rank = Economy.TierRanking(s, me.tier, me.nation).FindIndex(l => l.isPlayer) + 1;
                    var last = me.seasons?.LastOrDefault();
                    var hon = (s.honours ?? new List<HonourEntry>()).Where(h => h.season == s.season - 1 && h.kind != "league").Select(h => $"{Honours.CompNameOf(h)} {h.winnerName}");
                    Console.WriteLine($"    컵: {string.Join(" / ", hon)}");
                    Console.WriteLine($"    판테온 출전 {s.cup?.teams.Count}곳, 경기 {s.cup?.ties.Count}판, 우리 국내 컵 경기 {NatCupLogic.Of(s, s.nation)?.ties.Count(t => t.home == me.id || t.away == me.id)}판");
                    Console.WriteLine($"  시즌 {s.season - 1} 끝: {last?.tier}부 {last?.rank}위 {last?.wins}승 {last?.losses}패 → 지금 {me.tier}부 · 금고 {me.treasury:N0} D · 최저 {minTreasury:N0} D · 검투사 {me.gladiators.Count}명 주급 {Economy.WeeklyWages(s, me):N0}/주 · 영입 {Signed}명 · 전사 {s.fallen.Count(id => me.gladiators.Contains(id) || true)}");
                    minTreasury = me.treasury;
                }
            }
            if (s.gameOver != null) Console.WriteLine($"  게임 오버: {s.gameOver}");
            Console.WriteLine($"  하루 평균 {sw.Elapsed.TotalMilliseconds / Math.Max(1, days):0} ms");
            foreach (var (k, v) in income.OrderByDescending(x => x.Value)) Console.WriteLine($"    {k,-14} {v,10:N0}");
        }
    }

    static string Category(string desc)
    {
        if (desc.Contains("배당")) return "배당금";
        if (desc.Contains("지원금")) return "하부 리그 지원금";
        if (desc.Contains("상금")) return "상금·관중";
        if (desc.Contains("주급")) return "주급";
        if (desc.Contains("식비")) return "식비";
        if (desc.Contains("유지비")) return "시설 유지비";
        if (desc.Contains("식솔")) return "식솔 급료";
        if (desc.Contains("이적료")) return "이적료";
        if (desc.Contains("계약금")) return "계약금";
        if (desc.Contains("수당") || desc.Contains("보너스")) return "수당·보너스";
        return Verbose ? "기타: " + System.Text.RegularExpressions.Regex.Replace(desc, @"[\d,]+", "#") : "기타";
    }

    static bool Verbose => Environment.GetEnvironmentVariable("ECON_VERBOSE") == "1";

    /// <summary>단순한 플레이어: 사건은 첫 선택지, 도전장은 받고, 사흘마다 같은 리그에 미시오 도전, 제안·역제안은 거절·철회</summary>
    static void Autoplay(GameState s, Rng rng)
    {
        var me = s.ludi[s.playerLudusId];
        foreach (var p in s.pendingEvents.ToList())
        {
            var def = Events.GetEvent(p.eventId);
            int i = def.choices.FindIndex(c => c.cost == null || c.cost == 0 || me.treasury >= c.cost);
            Events.ResolveEvent(s, rng, p.id, Math.Max(0, i));
        }
        foreach (var o in Transfers.OpenOffers(s)) Transfers.RejectOffer(s, o.id);
        foreach (var d in (s.decisions ?? new List<Decision>()).ToList()) Negotiations.Withdraw(s, d.id);
        // 주급 인상 요구에는 바라는 조건 그대로 재계약 (불만이 쌓이지 않게)
        foreach (var g in me.gladiators.Select(id => s.gladiators[id]).Where(g => g.alive && g.raiseBy != null).ToList())
        {
            var d = Contracts.RenewDemand(s, g);
            if (d.refuse) continue;
            int until = g.contract.untilSeason;
            Contracts.Apply(s, g, new ContractTerms { wage = d.wage, seasons = 2 });
            g.contract.untilSeason = Math.Max(until, s.season + 1);
        }
        // 재계약: 바라는 조건 그대로 2시즌
        if (s.day == 150)
            foreach (var g in Contracts.Expiring(s))
            {
                var d = Contracts.RenewDemand(s, g);
                if (d.refuse) continue;
                int until = Math.Max(s.season, g.contract.untilSeason);
                Contracts.Apply(s, g, new ContractTerms { wage = d.wage, seasons = 2 });
                g.contract.untilSeason = until + 2;
            }
        // 막사 유지: 살아 있는 검투사가 7명 아래면 금고가 허락하는 무소속 중 가장 나은 자를 바라는 주급에 데려온다
        var alive = me.gladiators.Count(id => s.gladiators[id].alive);
        if (alive < 7 && s.day % 7 == 2)
        {
            var best = s.freeAgents.Select(id => s.gladiators[id])
                .Select(g => (g, d: Economy.FreeAgentDemand(s, g)))
                .Where(x => !x.d.refuse && x.d.wage * 30 < me.treasury)
                .OrderByDescending(x => GladiatorRules.ComputeCA(x.g) - x.d.wage * 0.3).FirstOrDefault();
            if (best.g != null)
            {
                var err = Economy.SignFreeAgent(s, best.g.id, best.d.wage, 2, 0);
                if (err == null) Signed++;
            }
        }
        foreach (var c in s.challenges.Where(x => x.status == "pending" && s.ludi[x.to].isPlayer).ToList()) ChallengeLogic.Respond(s, c, true, rng);
        foreach (var c in ChallengeLogic.TodaysPlayerMatches(s)) ChallengeLogic.PlayChallenge(s, c, rng);
        var tie = CupLogic.TodaysPlayerTie(s);
        if (tie != null) CupLogic.PlayTie(s, tie.id, rng);
        var nc = NatCupLogic.TodaysPlayerTie(s);
        if (nc != null) NatCupLogic.PlayTie(s, s.nation, nc.id, rng);
        if (OlympicaLogic.Current(s)?.offer == true) OlympicaLogic.Accept(s);
        var ol = OlympicaLogic.TodaysPlayerTie(s);
        if (ol != null) OlympicaLogic.PlayTie(s, ol.id, rng);
        if (s.day % 3 == 0)
        {
            var targets = s.ludi.Values.Where(l => !l.isPlayer && l.nation == me.nation && l.tier == me.tier && ChallengeLogic.CanChallenge(s, me, l) == null).ToList();
            var g = ChallengeLogic.PickFighter(s, me, "missio", ChallengeLogic.Now(s));
            if (targets.Count > 0 && g != null) ChallengeLogic.CreateChallenge(s, me, rng.Pick(targets), g.id, "missio");
        }
        Day.AdvanceDay(s);
    }
}
