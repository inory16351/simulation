// 판테온 컵 (docs/04_league.md): 여섯 나라 1부 상위 루두스 16곳이 겨루는 대륙 대회 (src/core/cup.ts)
// CupTie, Cup 타입은 StateTypes.cs에 있다. stage: group | qf | sf | final
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Colosseum.Core
{
    public class GroupRow
    {
        public string id;
        public double played, won, lost, boutsFor, boutsAgainst, pts;
    }

    public class PlayedTie
    {
        public CupTie tie;
        public Series series;
        public MatchSummary summary;
        /// <summary>치른 규칙 (missio, 전쟁 중인 나라끼리면 sudden)</summary>
        public string rule = "missio";
        /// <summary>TS가 string을 돌려주는 경우 (그런 경기가 없다, 이미 치른 경기). 있으면 나머지는 null</summary>
        public string error;
    }

    public class CoefficientRow
    {
        public string nation;
        public double coef;
        public int slots;
    }

    public static class CupConst
    {
        public const int DrawDay = 15;
        public static readonly int[] GroupDays = { 29, 43, 57, 71, 85, 99 };
        public const int QfDay = 113;
        public const int SfDay = 127;
        public const int FinalDay = 150;
        /// <summary>계수 순위별 출전권</summary>
        public static readonly int[] Slots = { 4, 4, 3, 3, 1, 1 };
        public const int Bouts = 3;
        public static class Prize
        {
            public const double Entry = 5000, GroupWin = 1000, Qf = 4000, Sf = 8000, RunnerUp = 12000, Winner = 25000;
        }
        public static class Coef
        {
            public const double Win = 2, Qf = 1, Sf = 1, Final = 1, Champion = 2;
        }
        public static double CoefOf(string stage) => stage switch
        {
            "qf" => Coef.Qf, "sf" => Coef.Sf, "final" => Coef.Final, _ => 0,
        };
    }

    public static class CupLogic
    {
        public static readonly Dictionary<string, string> StageNames = new()
        {
            ["po"] = "플레이오프", ["group"] = "조별 리그", ["qf"] = "8강", ["sf"] = "4강", ["final"] = "결승",
        };

        // ── FM식 판테온 컵 (Unity판): 국내 컵 우승 6팀 플레이오프(3팀 2개 조, 상위 2팀) → 나라별 1부 1·2위 12팀과 함께 16강 조별(4팀 4개 조, 상위 2팀) → 8강·4강·결승. 전부 5:5 미시오 단판 ──

        public const int FmDrawDay = 131;
        public static readonly int[] FmPoDays = { 133, 136, 139 };
        public static readonly int[] FmGroupDays = { 142, 145, 148 };
        public const int FmQfDay = 151, FmSfDay = 154, FmFinalDay = 157;
        public const double PrizePoEntry = 2000;

        /// <summary>한 경기에 나오는 인원</summary>
        public static int Bouts(GameState s) => s.cup?.fm == true ? 5 : CupConst.Bouts;

        /// <summary>단계별 날짜 (화면용)</summary>
        public static int StageDay(Cup cup, string stage) => cup?.fm == true
            ? stage switch { "qf" => FmQfDay, "sf" => FmSfDay, _ => FmFinalDay }
            : stage switch { "qf" => CupConst.QfDay, "sf" => CupConst.SfDay, _ => CupConst.FinalDay };

        static Cup DrawCupFm(GameState state, Rng rng)
        {
            var direct = new List<Ludus>();
            var playoff = new List<Ludus>();
            foreach (var n in Data.Nations)
            {
                var top = Economy.TierRanking(state, 1, n.id);
                direct.AddRange(top.Take(2));
                // 국내 컵 우승팀이 플레이오프에 나간다. 그 팀이 이미 1부 1·2위면 3위가 대신 나간다
                var nc = NatCupLogic.Of(state, n.id);
                Ludus champ = nc != null && nc.season == state.season && !string.IsNullOrEmpty(nc.champion) ? state.ludi[nc.champion] : null;
                bool alreadyIn = champ == null || top.Take(2).Contains(champ);
                var po = alreadyIn ? top.Skip(2).FirstOrDefault() : champ;
                if (po != null) playoff.Add(po);
            }
            var shuffled = rng.Shuffle(playoff);
            var a = shuffled.Take(shuffled.Count / 2).ToList();
            var b = shuffled.Skip(shuffled.Count / 2).ToList();
            var cup = new Cup
            {
                fm = true, season = state.season, points = new Dictionary<string, double>(),
                teams = direct.Concat(playoff).Select(l => l.id).ToList(),
                groups = new List<List<string>>(),
                poGroups = new List<List<string>> { a.Select(l => l.id).ToList(), b.Select(l => l.id).ToList() },
                ties = new List<CupTie>(),
            };
            for (int gi = 0; gi < cup.poGroups.Count; gi++)
            {
                var g = cup.poGroups[gi];
                var rounds = Fixtures.RoundRobin(g.Count);
                for (int r = 0; r < rounds.Count; r++)
                    foreach (var (x, y) in rounds[r])
                        cup.ties.Add(new CupTie { id = $"T{state.nextId++}", stage = "po", day = FmPoDays[Math.Min(r, FmPoDays.Length - 1)], home = g[x], away = g[y], group = gi });
            }
            foreach (var t in cup.teams) cup.points[t] = 0;
            foreach (var l in playoff) Economy.AddMoney(state, l, PrizePoEntry, "판테온 컵 플레이오프 출전 수당");
            foreach (var l in direct) Economy.AddMoney(state, l, CupConst.Prize.Entry, "판테온 컵 본선 출전 수당");
            return cup;
        }

        /// <summary>플레이오프가 끝나면 본선 16팀을 4팀 4개 조로 (RP 단지, 같은 나라는 되도록 다른 조)</summary>
        static void DrawMainFm(GameState state, Rng rng, Cup cup)
        {
            var poAll = cup.poGroups.SelectMany(g => g).ToList();
            var through = cup.poGroups.SelectMany((_, gi) => PlayoffTable(state, cup, gi).Take(2).Select(r => r.id)).ToList();
            foreach (var id in through) Economy.AddMoney(state, state.ludi[id], CupConst.Prize.Entry, "판테온 컵 본선 진출 상금");
            var main = cup.teams.Where(id => !poAll.Contains(id)).Concat(through).Select(id => state.ludi[id]).OrderByDescending(l => l.rp).ToList();
            var groups = new List<List<Ludus>>();
            for (int attempt = 0; attempt < 60; attempt++)
            {
                groups = new List<List<Ludus>> { new(), new(), new(), new() };
                for (int pot = 0; pot < 4; pot++)
                {
                    var potTeams = rng.Shuffle(main.Skip(pot * 4).Take(4).ToList());
                    for (int i = 0; i < potTeams.Count; i++) groups[i].Add(potTeams[i]);
                }
                if (groups.All(g => g.Select(l => l.nation).Distinct().Count() == g.Count)) break;
            }
            cup.groups = groups.Select(g => g.Select(l => l.id).ToList()).ToList();
            for (int gi = 0; gi < cup.groups.Count; gi++)
            {
                var g = cup.groups[gi];
                var rounds = Fixtures.RoundRobin(g.Count);
                for (int r = 0; r < rounds.Count; r++)
                    foreach (var (x, y) in rounds[r])
                        cup.ties.Add(new CupTie { id = $"T{state.nextId++}", stage = "group", day = FmGroupDays[Math.Min(r, FmGroupDays.Length - 1)], home = g[x], away = g[y], group = gi });
            }
            string me = state.playerLudusId;
            if (poAll.Contains(me))
                StateUtil.PushMessage(state, "info", through.Contains(me) ? "판테온 컵 본선 진출!" : "판테온 컵 플레이오프 탈락",
                    through.Contains(me) ? "플레이오프 조 2위 안에 들어 본선 16강 조별 리그에 나갑니다." : "플레이오프에서 조 2위 안에 들지 못해 탈락했습니다. 남은 시간은 던전 원정이나 훈련에 쓸 수 있습니다.");
        }

        /// <summary>플레이오프 조 순위</summary>
        public static List<GroupRow> PlayoffTable(GameState state, Cup cup, int gi) => TableOf(state, cup, cup.poGroups[gi], "po", gi);

        static void DailyCupFm(GameState state, Rng rng)
        {
            if (state.day == FmDrawDay)
            {
                state.cup = DrawCupFm(state, rng);
                string me = state.playerLudusId;
                bool po = state.cup.poGroups.Any(g => g.Contains(me));
                bool direct = !po && state.cup.teams.Contains(me);
                StateUtil.PushMessage(state, "info",
                    direct ? "판테온 컵 본선에 바로 나갑니다!" : po ? "판테온 컵 플레이오프에 나갑니다!" : "판테온 컵 대진 추첨",
                    (direct ? $"우리 나라 1부 2위 안에 들어 본선 출전 수당 {N0(CupConst.Prize.Entry)} D를 받았습니다.\n" : po ? $"플레이오프 출전 수당 {N0(PrizePoEntry)} D를 받았습니다.\n" : "") +
                    "나라마다 국내 컵 우승 팀 6곳이 플레이오프(3팀씩 2개 조)를 치르고 상위 2팀씩 4곳이 본선에 오릅니다. 국내 컵 우승 팀이 이미 1부 1·2위면 1부 3위가 대신 나갑니다.\n" +
                    "본선은 플레이오프를 통과한 4곳과 나라별 1부 1·2위 12곳, 모두 16곳이 4개 조로 겨루고 조 상위 2팀이 8강에 오릅니다. 모든 경기는 5:5 미시오 단판입니다.");
            }
            var cup = state.cup;
            if (cup == null || cup.season != state.season) return;
            AdvanceBracket(state, cup, rng);
            string my = state.playerLudusId;
            for (int i = 0; i < cup.ties.Count; i++)
            {
                var t = cup.ties[i];
                if (t.day != state.day || Truthy(t.winner) || t.home == my || t.away == my) continue;
                PlayTie(state, t.id, rng);
            }
        }

        /// <summary>같은 조 4팀이 홈·원정 한 번씩 (6라운드)</summary>
        static readonly int[][][] RoundRobin =
        {
            new[] { new[] { 0, 1 }, new[] { 2, 3 } }, new[] { new[] { 0, 2 }, new[] { 1, 3 } }, new[] { new[] { 0, 3 }, new[] { 1, 2 } },
            new[] { new[] { 1, 0 }, new[] { 3, 2 } }, new[] { new[] { 2, 0 }, new[] { 3, 1 } }, new[] { new[] { 3, 0 }, new[] { 2, 1 } },
        };

        public static readonly string[] GroupLetters = { "가", "나", "다", "라" };

        static string N0(double x) => x.ToString("N0", CultureInfo.InvariantCulture);
        static string N(double x) => x.ToString(CultureInfo.InvariantCulture);

        /// <summary>나라 계수: 최근 3시즌 점수 합</summary>
        public static double CoefficientOf(GameState state, string n)
        {
            double sum = 0;
            if (state.coefficients != null && state.coefficients.TryGetValue(n, out var list))
                foreach (var v in list) sum = sum + v;
            return JsMath.Round(sum * 10) / 10;
        }

        public static List<CoefficientRow> CoefficientRanking(GameState state) =>
            Data.Nations.Select(n => new CoefficientRow { nation = n.id, coef = CoefficientOf(state, n.id) })
                .OrderByDescending(x => x.coef)
                .Select((x, i) => new CoefficientRow { nation = x.nation, coef = x.coef, slots = i < CupConst.Slots.Length ? CupConst.Slots[i] : 0 })
                .ToList();

        /// <summary>조 추첨: RP 순으로 네 단지에 나누고, 같은 나라는 되도록 다른 조로</summary>
        public static Cup DrawCup(GameState state, Rng rng)
        {
            var teams = new List<Ludus>();
            foreach (var row in CoefficientRanking(state))
            {
                var top = state.ludi.Values.Where(l => l.nation == row.nation && l.tier == 1).OrderByDescending(l => l.rp).Take(row.slots);
                teams.AddRange(top);
            }
            var sorted = teams.OrderByDescending(l => l.rp).ToList();
            var groups = new List<List<Ludus>>();
            for (int attempt = 0; attempt < 40; attempt++)
            {
                groups = new List<List<Ludus>> { new(), new(), new(), new() };
                for (int pot = 0; pot < 4; pot++)
                {
                    var potTeams = rng.Shuffle(sorted.Skip(pot * 4).Take(4).ToList());
                    for (int i = 0; i < potTeams.Count; i++) groups[i].Add(potTeams[i]);
                }
                bool clash = groups.Any(g => g.Select(l => l.nation).Distinct().Count() < g.Count);
                if (!clash) break;
            }
            var ties = new List<CupTie>();
            for (int gi = 0; gi < groups.Count; gi++)
            {
                var g = groups[gi];
                for (int ri = 0; ri < RoundRobin.Length; ri++)
                {
                    foreach (var pair in RoundRobin[ri])
                    {
                        int h = pair[0], a = pair[1];
                        if (h >= g.Count || a >= g.Count) continue;
                        ties.Add(new CupTie { id = $"T{state.nextId++}", stage = "group", day = CupConst.GroupDays[ri], home = g[h].id, away = g[a].id, group = gi });
                    }
                }
            }
            var cup = new Cup
            {
                season = state.season,
                teams = teams.Select(t => t.id).ToList(),
                groups = groups.Select(g => g.Select(l => l.id).ToList()).ToList(),
                ties = ties,
                points = new Dictionary<string, double>(),
            };
            foreach (var t in teams)
            {
                cup.points[t.id] = 0;
                Economy.AddMoney(state, t, CupConst.Prize.Entry, "판테온 컵 출전 수당");
            }
            return cup;
        }

        public static List<GroupRow> GroupTable(GameState state, Cup cup, int gi) => TableOf(state, cup, cup.groups[gi], "group", gi);

        static List<GroupRow> TableOf(GameState state, Cup cup, List<string> ids, string stage, int gi)
        {
            var rows = new Dictionary<string, GroupRow>();
            var order = new List<string>();
            foreach (var id in ids)
            {
                // new Map(entries): 같은 키가 또 오면 값은 바뀌고 자리는 처음 그대로
                if (!rows.ContainsKey(id)) order.Add(id);
                rows[id] = new GroupRow { id = id };
            }
            foreach (var t in cup.ties)
            {
                if (t.stage != stage || t.group != gi || t.score == null) continue;
                var h = rows[t.home];
                var a = rows[t.away];
                h.played++; a.played++;
                h.boutsFor += t.score[0]; h.boutsAgainst += t.score[1];
                a.boutsFor += t.score[1]; a.boutsAgainst += t.score[0];
                if (t.winner == t.home) { h.won++; a.lost++; h.pts += 3; } else { a.won++; h.lost++; a.pts += 3; }
            }
            double Rp(string id) => state.ludi.TryGetValue(id, out var l) ? l.rp : 0;
            return order.Select(id => rows[id])
                .OrderByDescending(x => x.pts)
                .ThenByDescending(x => x.boutsFor - x.boutsAgainst)
                .ThenByDescending(x => Rp(x.id))
                .ToList();
        }

        static string WinnerOf(Cup cup, string stage, int i)
        {
            var list = cup.ties.Where(t => t.stage == stage).ToList();
            return i < list.Count ? list[i].winner : null;
        }

        static bool Truthy(string s) => !string.IsNullOrEmpty(s);

        static string IdAt(List<List<GroupRow>> t, int gi, int ri) =>
            gi < t.Count && ri < t[gi].Count ? t[gi][ri].id : null;

        /// <summary>단계가 끝났으면 다음 단계 대진을 만든다</summary>
        static void AdvanceBracket(GameState state, Cup cup, Rng rng = null)
        {
            bool Has(string s) => cup.ties.Any(t => t.stage == s);
            bool Done(string s) => cup.ties.Where(t => t.stage == s).All(t => Truthy(t.winner));
            bool fm = cup.fm == true;
            if (fm && Has("po") && Done("po") && !Has("group"))
            {
                // 난수가 없는 자리(경기 직후)에서는 다음 날 아침에 뽑는다
                if (rng != null) DrawMainFm(state, rng, cup);
                return;
            }
            if (fm && !Has("group")) return;
            int qfDay = fm ? FmQfDay : CupConst.QfDay, sfDay = fm ? FmSfDay : CupConst.SfDay, finalDay = fm ? FmFinalDay : CupConst.FinalDay;
            void Add(string stage, int day, (string h, string a)[] pairs)
            {
                foreach (var (h, a) in pairs)
                    if (Truthy(h) && Truthy(a)) cup.ties.Add(new CupTie { id = $"T{state.nextId++}", stage = stage, day = day, home = h, away = a });
            }
            if (!Has("qf") && Done("group"))
            {
                var t = cup.groups.Select((_, gi) => GroupTable(state, cup, gi)).ToList();
                Add("qf", qfDay, new[]
                {
                    (IdAt(t, 0, 0), IdAt(t, 1, 1)), (IdAt(t, 1, 0), IdAt(t, 0, 1)), (IdAt(t, 2, 0), IdAt(t, 3, 1)), (IdAt(t, 3, 0), IdAt(t, 2, 1)),
                });
                foreach (var g in t) foreach (var r in g.Take(2)) Reach(state, cup, r.id, "qf");
            }
            else if (Has("qf") && !Has("sf") && Done("qf"))
            {
                Add("sf", sfDay, new[] { (WinnerOf(cup, "qf", 0), WinnerOf(cup, "qf", 2)), (WinnerOf(cup, "qf", 1), WinnerOf(cup, "qf", 3)) });
                foreach (var tt in cup.ties.Where(x => x.stage == "qf").ToList()) Reach(state, cup, tt.winner, "sf");
            }
            else if (Has("sf") && !Has("final") && Done("sf"))
            {
                Add("final", finalDay, new[] { (WinnerOf(cup, "sf", 0), WinnerOf(cup, "sf", 1)) });
                foreach (var tt in cup.ties.Where(x => x.stage == "sf").ToList()) Reach(state, cup, tt.winner, "final");
            }
        }

        /// <summary>다음 단계 진출: 상금과 계수 점수. stage: qf | sf | final</summary>
        static void Reach(GameState state, Cup cup, string id, string stage)
        {
            if (id == null || !state.ludi.TryGetValue(id, out var l)) return;
            cup.points[id] = (cup.points.TryGetValue(id, out var p) ? p : 0) + CupConst.CoefOf(stage);
            if (stage == "qf") Economy.AddMoney(state, l, CupConst.Prize.Qf, "판테온 컵 8강 진출 상금");
            if (stage == "sf") Economy.AddMoney(state, l, CupConst.Prize.Sf, "판테온 컵 4강 진출 상금");
        }

        /// <summary>대결 한 번: 세 판 중 두 판을 먼저 따면 이긴다 (미시오). 실패하면 error만 채운다</summary>
        /// <summary>컵 경기 규칙 (Unity판 FM식): 전쟁 중인 나라 루두스끼리 만나면 사투, 아니면 미시오</summary>
        public static string TieRule(GameState state, CupTie tie) =>
            FmRules.Enabled && WarLogic.AtWar(state, state.ludi[tie.home].nation, state.ludi[tie.away].nation) ? "sudden" : "missio";

        public static PlayedTie PlayTie(GameState state, string tieId, Rng rng, List<string> lineup = null)
        {
            var cup = state.cup;
            var tie = cup?.ties.FirstOrDefault(t => t.id == tieId);
            if (cup == null || tie == null) return new PlayedTie { error = "그런 경기가 없습니다" };
            if (Truthy(tie.winner)) return new PlayedTie { error = "이미 치른 경기" };
            var home = state.ludi[tie.home];
            var away = state.ludi[tie.away];
            int at = (int)Calendar.AbsDay(state.season, state.day);
            int bouts = Bouts(state);
            var lh = SeriesLogic.AutoLineup(state, home, bouts, at, home.isPlayer ? lineup : null);
            var la = SeriesLogic.AutoLineup(state, away, bouts, at, away.isPlayer ? lineup : null);
            string rule = TieRule(state, tie);
            var series = SeriesLogic.RunSeries(state, new[] { home, away }, new[] { lh, la }, new RunOptions { rule = rule, mode = "best_of", homeLudusId = home.id, comp = "pantheon" }, rng);
            tie.score = series.score;
            var (w, l) = series.winner == 0 ? (home, away) : (away, home);
            tie.winner = w.id;
            var summary = new MatchSummary();
            double crowd = series.Crowd();
            MatchLogic.Settle(state, w, l, rule, new SettleOptions { homeLudusId = home.id, crowd = crowd + 20, popularity = 120, moneyMult = 2, noLeague = true }, summary);
            cup.points[w.id] = (cup.points.TryGetValue(w.id, out var wp) ? wp : 0) + CupConst.Coef.Win;
            if (tie.stage == "group") Economy.AddMoney(state, w, CupConst.Prize.GroupWin, "판테온 컵 조별 리그 승리 수당");
            if (tie.stage == "po") Economy.AddMoney(state, w, CupConst.Prize.GroupWin / 2, "판테온 컵 플레이오프 승리 수당");
            if (tie.stage == "final")
            {
                cup.champion = w.id;
                cup.points[w.id] += CupConst.Coef.Champion;
                Economy.AddMoney(state, w, CupConst.Prize.Winner, "판테온 컵 우승 상금");
                Economy.AddMoney(state, l, CupConst.Prize.RunnerUp, "판테온 컵 준우승 상금");
                w.titles = (w.titles ?? 0) + 1;
                Honours.RecordCup(state, w, l);
                Awards.PantheonEnd(state, cup, l.id);
                StateUtil.PushMessage(state, new InboxMessage
                {
                    kind = "info",
                    title = $"판테온 컵 우승: {w.name}",
                    body = $"{Data.Nation(w.nation).name}의 {Josa.J(w.name, "이/가")} 결승에서 {Josa.J(l.name, "을/를")} 꺾고 판테온 컵을 들어 올렸습니다 ({N(series.score.Max())}:{N(series.score.Min())}).",
                });
            }
            string me = state.playerLudusId;
            if (w.id == me || l.id == me)
            {
                bool won = w.id == me;
                var opp = won ? l : w;
                StateUtil.PushMessage(state, new InboxMessage
                {
                    kind = "match",
                    title = $"판테온 컵 {StageNames[tie.stage]}: {(won ? "승리" : "패배")}",
                    body = $"상대 {opp.name}({Data.Nation(opp.nation).name}) · {N(series.score[home.id == me ? 0 : 1])}:{N(series.score[home.id == me ? 1 : 0])}" +
                        (rule == "sudden" ? "\n두 나라가 전쟁 중이라 사투로 치렀습니다." : "") +
                        $"\n수입 {N0(summary.money.TryGetValue(me, out var mm) ? mm : 0)} D (세전)" +
                        (tie.stage != "group" && tie.stage != "po" && !won ? "\n여기서 탈락했습니다." : ""),
                });
            }
            AdvanceBracket(state, cup);
            return new PlayedTie { tie = tie, series = series, summary = summary, rule = rule };
        }

        /// <summary>오늘 플레이어가 치를 컵 경기</summary>
        public static CupTie TodaysPlayerTie(GameState state)
        {
            string me = state.playerLudusId;
            return state.cup?.ties.FirstOrDefault(t => t.day == state.day && !Truthy(t.winner) && (t.home == me || t.away == me));
        }

        /// <summary>매일: 추첨, 다음 단계 대진, AI끼리의 컵 경기</summary>
        public static void DailyCup(GameState state, Rng rng)
        {
            if (FmRules.Enabled) { DailyCupFm(state, rng); return; }
            if (state.day == CupConst.DrawDay)
            {
                state.cup = DrawCup(state, rng);
                bool mine = state.cup.teams.Contains(state.playerLudusId);
                StateUtil.PushMessage(state, new InboxMessage
                {
                    kind = "info",
                    title = mine ? "판테온 컵에 나갑니다!" : "판테온 컵 조 추첨",
                    body = (mine ? $"우리 루두스가 판테온 컵 출전권을 얻었습니다. 출전 수당 {N0(CupConst.Prize.Entry)} D가 들어왔습니다.\n" : "") +
                        $"여섯 나라 1부 상위 16곳이 네 조로 나뉘었습니다. 조별 리그는 {CupConst.GroupDays.Length}라운드, 상위 두 팀이 8강에 오릅니다." +
                        "\n대결마다 세 명씩 나와 미시오 난전으로 한꺼번에 싸우고, 상대 편을 모두 쓰러뜨리면 이깁니다.",
                });
            }
            var cup = state.cup;
            if (cup == null || cup.season != state.season) return;
            AdvanceBracket(state, cup);
            string me = state.playerLudusId;
            // for..of: 도는 중에 대진이 늘어나면 그것도 본다 (JS 배열 순회와 같게 인덱스로)
            for (int i = 0; i < cup.ties.Count; i++)
            {
                var t = cup.ties[i];
                if (t.day != state.day || Truthy(t.winner) || t.home == me || t.away == me) continue;
                PlayTie(state, t.id, rng);
            }
        }

        /// <summary>시즌 끝: 나라별 평균 점수를 계수에 넣고 오래된 시즌은 뺀다</summary>
        public static void CloseCupSeason(GameState state)
        {
            var cup = state.cup;
            if (cup == null || cup.season != state.season) return;
            foreach (var n in Data.Nations)
            {
                var teams = cup.teams.Where(id => state.ludi.TryGetValue(id, out var l) && l.nation == n.id).ToList();
                double score = 0;
                if (teams.Count > 0)
                {
                    double s = 0;
                    foreach (var id in teams) s = s + (cup.points.TryGetValue(id, out var p) ? p : 0);
                    score = s / teams.Count;
                }
                var hist = new List<double>(state.coefficients.TryGetValue(n.id, out var old) ? old : new List<double>());
                hist.Add(JsMath.Round(score * 10) / 10);
                state.coefficients[n.id] = hist.Skip(Math.Max(0, hist.Count - 3)).ToList();
            }
        }
    }
}
