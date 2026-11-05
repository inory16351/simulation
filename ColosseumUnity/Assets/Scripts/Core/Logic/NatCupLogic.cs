// 국내 컵 (Unity판 전용, FM식): 정규 리그가 끝나면 나라의 1~4부 80개 루두스(부당 20)가 모두 나오는 5:5 미시오 컵.
// 80강 → 40강 → 20강 → 10강 단판 뒤 5강 리그(한 번씩). 승수 → 쓰러뜨린 상대 수 → 승자승 → RP 순. 우승하면 몇 부든 판테온 컵 플레이오프에 나간다.
// 다른 나라의 80강·40강·20강은 하루에 100판이 넘어 전력 공식으로 정한다 (우리 나라는 전부 실제 전투).
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Colosseum.Core
{
    /// <summary>리그표 한 줄 (국내 컵 5강, 올림피카 컵)</summary>
    public class FixtureRow
    {
        public string id;
        public int played, won, lost;
        /// <summary>쓰러뜨린 상대 수, 쓰러진 우리 수</summary>
        public double downs, downed;
    }

    /// <summary>대진·순위 공용</summary>
    public static class Fixtures
    {
        /// <summary>한 번씩 만나는 대진 (돌려짝 짓기). 홀수면 라운드마다 한 팀이 쉰다. 결과: 라운드마다 (앞, 뒤) 번호 쌍</summary>
        public static List<List<(int a, int b)>> RoundRobin(int n)
        {
            var slots = Enumerable.Range(0, n % 2 == 0 ? n : n + 1).ToList();
            int m = slots.Count, bye = n % 2 == 0 ? -1 : n;
            var rounds = new List<List<(int, int)>>();
            for (int r = 0; r < m - 1; r++)
            {
                var pairs = new List<(int, int)>();
                for (int i = 0; i < m / 2; i++)
                {
                    int x = slots[i], y = slots[m - 1 - i];
                    if (x == bye || y == bye) continue;
                    // 홈이 한쪽으로 몰리지 않게 라운드마다 뒤집는다
                    pairs.Add(r % 2 == 0 ? (x, y) : (y, x));
                }
                rounds.Add(pairs);
                int last = slots[m - 1];
                slots.RemoveAt(m - 1);
                slots.Insert(1, last);
            }
            return rounds;
        }

        /// <summary>순위: 승수 → 쓰러뜨린 상대 수 → 승자승(같은 줄끼리 이긴 수) → tiebreak(RP 등)</summary>
        public static List<FixtureRow> Table(IEnumerable<string> ids, IEnumerable<CupTie> ties, Func<string, double> tiebreak)
        {
            var rows = ids.ToDictionary(id => id, id => new FixtureRow { id = id });
            var done = ties.Where(t => !string.IsNullOrEmpty(t.winner) && t.score != null).ToList();
            foreach (var t in done)
            {
                if (!rows.TryGetValue(t.home, out var h) || !rows.TryGetValue(t.away, out var a)) continue;
                h.played++; a.played++;
                h.downs += t.score[0]; h.downed += t.score[1];
                a.downs += t.score[1]; a.downed += t.score[0];
                if (t.winner == t.home) { h.won++; a.lost++; } else { a.won++; h.lost++; }
            }
            var result = new List<FixtureRow>();
            foreach (var same in rows.Values.GroupBy(r => (r.won, r.downs)).OrderByDescending(g => g.Key.won).ThenByDescending(g => g.Key.downs))
            {
                var group = same.ToList();
                var inGroup = new HashSet<string>(group.Select(r => r.id));
                int H2H(string id) => done.Count(t => t.winner == id && inGroup.Contains(t.home) && inGroup.Contains(t.away));
                result.AddRange(group.OrderByDescending(r => H2H(r.id)).ThenByDescending(r => tiebreak(r.id)));
            }
            return result;
        }
    }

    public static class NatCupLogic
    {
        public const int DrawDay = 106;
        public static readonly int[] KoDays = { 108, 110, 112, 114 };
        public static readonly int[] LeagueDays = { 117, 120, 123, 126, 129 };
        public const int Lineup = 5;
        public const double PrizeR10 = 800, PrizeLeague = 2500, PrizeRunnerUp = 5000, PrizeWinner = 12000;

        public static readonly string[] KoStages = { "r80", "r40", "r20", "r10" };

        public static readonly Dictionary<string, string> StageNames = new()
        {
            ["r80"] = "80강", ["r40"] = "40강", ["r20"] = "20강", ["r10"] = "10강", ["league5"] = "5강 리그",
        };

        public static string Name(string nation) => $"{Data.Nation(nation).@short} 컵";

        static string N0(double x) => x.ToString("N0", CultureInfo.InvariantCulture);
        static bool Done(CupTie t) => !string.IsNullOrEmpty(t.winner);

        public static NatCup Of(GameState s, string nation) => s.natCups != null && s.natCups.TryGetValue(nation, out var c) ? c : null;

        /// <summary>5강 리그표</summary>
        public static List<FixtureRow> LeagueTable(GameState s, NatCup c) =>
            Fixtures.Table(c.final5 ?? new List<string>(), c.ties.Where(t => t.stage == "league5"), id => s.ludi.TryGetValue(id, out var l) ? l.rp : 0);

        /// <summary>오늘 우리가 치를 국내 컵 경기</summary>
        public static CupTie TodaysPlayerTie(GameState s)
        {
            var c = Of(s, s.nation);
            if (c == null || c.season != s.season) return null;
            string me = s.playerLudusId;
            return c.ties.FirstOrDefault(t => t.day == s.day && !Done(t) && (t.home == me || t.away == me));
        }

        /// <summary>조 추첨: 나라마다 40개를 섞어 짝 짓는다. 아래 부 루두스가 홈</summary>
        static NatCup Draw(GameState s, Rng rng, string nation)
        {
            var teams = rng.Shuffle(s.ludi.Values.Where(l => l.nation == nation).ToList());
            var cup = new NatCup { season = s.season, nation = nation, teams = teams.Select(l => l.id).ToList() };
            AddKnockout(s, cup, KoStages[0], KoDays[0], cup.teams);
            return cup;
        }

        static void AddKnockout(GameState s, NatCup cup, string stage, int day, List<string> ids)
        {
            for (int i = 0; i + 1 < ids.Count; i += 2)
            {
                var a = s.ludi[ids[i]];
                var b = s.ludi[ids[i + 1]];
                var (h, w) = a.tier >= b.tier ? (a, b) : (b, a);
                cup.ties.Add(new CupTie { id = $"T{s.nextId++}", stage = stage, day = day, home = h.id, away = w.id });
            }
        }

        /// <summary>단계가 끝났으면 다음 단계를 만든다. 다 끝났으면 우승을 정한다</summary>
        static void Advance(GameState s, NatCup c)
        {
            if (!string.IsNullOrEmpty(c.champion)) return;
            bool Has(string st) => c.ties.Any(t => t.stage == st);
            bool All(string st) => Has(st) && c.ties.Where(t => t.stage == st).All(Done);
            List<string> Winners(string st) => c.ties.Where(t => t.stage == st).Select(t => t.winner).ToList();
            for (int i = 0; i + 2 < KoStages.Length; i++)
                if (All(KoStages[i]) && !Has(KoStages[i + 1])) { AddKnockout(s, c, KoStages[i + 1], KoDays[i + 1], Winners(KoStages[i])); return; }
            if (All("r20") && !Has("r10"))
            {
                AddKnockout(s, c, "r10", KoDays[KoStages.Length - 1], Winners("r20"));
                foreach (var id in Winners("r20")) Economy.AddMoney(s, s.ludi[id], PrizeR10, $"{Name(c.nation)} 10강 진출 상금");
            }
            else if (All("r10") && !Has("league5"))
            {
                c.final5 = Winners("r10");
                foreach (var id in c.final5) Economy.AddMoney(s, s.ludi[id], PrizeLeague, $"{Name(c.nation)} 5강 진출 상금");
                var rounds = Fixtures.RoundRobin(c.final5.Count);
                for (int r = 0; r < rounds.Count; r++)
                    foreach (var (a, b) in rounds[r])
                        c.ties.Add(new CupTie { id = $"T{s.nextId++}", stage = "league5", day = LeagueDays[Math.Min(r, LeagueDays.Length - 1)], home = c.final5[a], away = c.final5[b], group = r });
            }
            else if (All("league5")) Finish(s, c);
        }

        static void Finish(GameState s, NatCup c)
        {
            var table = LeagueTable(s, c);
            var w = s.ludi[table[0].id];
            var r = s.ludi[table[1].id];
            c.champion = w.id;
            c.runnerUp = r.id;
            Economy.AddMoney(s, w, PrizeWinner, $"{Name(c.nation)} 우승 상금");
            Economy.AddMoney(s, r, PrizeRunnerUp, $"{Name(c.nation)} 준우승 상금");
            w.titles = (w.titles ?? 0) + 0.5;
            Honours.RecordNatCup(s, c.nation, w, r);
            Awards.NatCupEnd(s, c);
            if (c.nation != s.nation) return;
            bool mine = w.isPlayer;
            string lines = string.Join("\n", table.Select((x, i) => $"{i + 1}위 {s.ludi[x.id].name} · {x.won}승 {x.lost}패 · 쓰러뜨린 상대 {x.downs:0}"));
            StateUtil.PushMessage(s, "info", mine ? $"{Name(c.nation)} 우승!" : $"{Name(c.nation)} 우승: {w.name}",
                $"{Josa.J(w.name, "이/가")} {Name(c.nation)} 정상에 섰습니다. 몇 부 리그든 상관없이 판테온 컵 플레이오프에 나갑니다.\n\n{lines}");
        }

        /// <summary>결과를 적고 돈을 나눈다 (실제 전투·전력 공식·기권 공용)</summary>
        static MatchSummary Commit(GameState s, NatCup c, CupTie t, int winnerSide, double[] score, double crowd)
        {
            var home = s.ludi[t.home];
            var away = s.ludi[t.away];
            t.score = score;
            var (w, l) = winnerSide == 0 ? (home, away) : (away, home);
            t.winner = w.id;
            var summary = new MatchSummary();
            if (t.forfeit != true)
                MatchLogic.Settle(s, w, l, "missio", new SettleOptions { homeLudusId = home.id, crowd = crowd + 10, popularity = 60, moneyMult = 1.5, noLeague = true }, summary);
            Notify(s, c, t, w, l, summary);
            Advance(s, c);
            return summary;
        }

        static void Notify(GameState s, NatCup c, CupTie t, Ludus w, Ludus l, MatchSummary summary)
        {
            string me = s.playerLudusId;
            if (w.id != me && l.id != me) return;
            bool won = w.id == me;
            var opp = won ? l : w;
            bool ko = t.stage != "league5";
            double my = t.score[t.home == me ? 0 : 1], their = t.score[t.home == me ? 1 : 0];
            string how = t.forfeit == true ? (won ? "상대가 나오지 못해 부전승입니다." : "우리가 나가지 않아 기권패입니다.") : $"쓰러뜨린 상대 {my:0} : 쓰러진 우리 {their:0}";
            string after = !ko ? "" : won ? "\n다음 라운드에 오릅니다." :
                "\n여기서 탈락했습니다. 판테온 컵까지 남은 시간은 던전 원정이나 훈련에 쓰기 좋습니다.";
            StateUtil.PushMessage(s, "match", $"{Name(c.nation)} {StageNames[t.stage]}: {(won ? "승리" : "패배")}",
                $"상대 {opp.name} ({opp.tier}부) · {how}{after}" + (summary.money.TryGetValue(me, out var m) && m > 0 ? $"\n수입 {N0(m)} D (세전)" : ""));
        }

        /// <summary>실제 전투로 치른다. 나올 사람이 없는 쪽은 기권패</summary>
        public static PlayedTie PlayTie(GameState s, string nation, string tieId, Rng rng, List<string> lineup = null)
        {
            var c = Of(s, nation);
            var t = c?.ties.FirstOrDefault(x => x.id == tieId);
            if (t == null) return new PlayedTie { error = "그런 경기가 없습니다" };
            if (Done(t)) return new PlayedTie { error = "이미 치른 경기" };
            var home = s.ludi[t.home];
            var away = s.ludi[t.away];
            int at = (int)Calendar.AbsDay(s.season, s.day);
            var lh = SeriesLogic.AutoLineup(s, home, Lineup, at, home.isPlayer ? lineup : null);
            var la = SeriesLogic.AutoLineup(s, away, Lineup, at, away.isPlayer ? lineup : null);
            if (lh.Count == 0 || la.Count == 0)
            {
                t.forfeit = true;
                int side = lh.Count > 0 ? 0 : la.Count > 0 ? 1 : (rng.Chance(0.5) ? 0 : 1);
                Commit(s, c, t, side, side == 0 ? new double[] { 5, 0 } : new double[] { 0, 5 }, 0);
                return new PlayedTie { tie = t, series = new Series { winner = side }, summary = new MatchSummary() };
            }
            var series = SeriesLogic.RunSeries(s, new[] { home, away }, new[] { lh, la }, new RunOptions { rule = "missio", mode = "best_of", homeLudusId = home.id, comp = "natcup:" + nation }, rng);
            var summary = Commit(s, c, t, series.winner, series.score, series.Crowd());
            return new PlayedTie { tie = t, series = series, summary = summary };
        }

        /// <summary>우리가 이 경기에 나가지 않는다 (기권패). 컵을 일찍 접고 던전이나 훈련에 집중하려 할 때</summary>
        public static string Forfeit(GameState s, string tieId)
        {
            var c = Of(s, s.nation);
            var t = c?.ties.FirstOrDefault(x => x.id == tieId);
            if (t == null || Done(t)) return "그런 경기가 없습니다";
            string me = s.playerLudusId;
            if (t.home != me && t.away != me) return "우리 경기가 아닙니다";
            t.forfeit = true;
            int side = t.home == me ? 1 : 0;
            Commit(s, c, t, side, side == 0 ? new double[] { 5, 0 } : new double[] { 0, 5 }, 0);
            return null;
        }

        /// <summary>전력 공식: 다른 나라 초반 라운드를 빠르게 정한다. 위 부일수록·명단이 강할수록 유리하지만 이변이 난다</summary>
        static void QuickTie(GameState s, NatCup c, CupTie t, Rng rng)
        {
            double Power(Ludus l) => l.gladiators.Select(id => s.gladiators[id]).Where(g => g.alive && g.injury?.severity != "serious" && g.deployment == null)
                .Select(g => (double)GladiatorRules.ComputeCA(g)).OrderByDescending(x => x).Take(Lineup).Sum();
            double ph = Power(s.ludi[t.home]), pa = Power(s.ludi[t.away]);
            if (ph <= 0 && pa <= 0) ph = 1;
            // 능력 합 차이 50(한 명당 10)이면 약 65%
            double p = 1 / (1 + Math.Pow(10, (pa - ph) / 230));
            int side = rng.Chance(p) ? 0 : 1;
            double loserDowns = rng.Int(0, 4);
            Commit(s, c, t, side, side == 0 ? new[] { 5, loserDowns } : new[] { loserDowns, 5 }, 25);
        }

        public static void Daily(GameState s, Rng rng)
        {
            if (!FmRules.Enabled) return;
            if (s.day == DrawDay)
            {
                s.natCups = new Dictionary<string, NatCup>();
                foreach (var n in Data.Nations) s.natCups[n.id] = Draw(s, rng, n.id);
                var mine = Of(s, s.nation);
                var first = mine.ties.FirstOrDefault(t => t.home == s.playerLudusId || t.away == s.playerLudusId);
                string opp = first == null ? "" : s.ludi[first.home == s.playerLudusId ? first.away : first.home].name;
                StateUtil.PushMessage(s, "info", $"{Name(s.nation)} 대진 추첨",
                    $"정규 리그가 끝나 나라의 1~4부 {mine.teams.Count}개 루두스가 모두 나오는 {Name(s.nation)}가 열립니다.\n" +
                    $"80강 → 40강 → 20강 → 10강은 5:5 미시오 단판이고, 5강은 한 번씩 맞붙어 승수로 우승을 가립니다. 승수가 같으면 쓰러뜨린 상대 수, 그다음 승자승입니다.\n" +
                    $"우승하면 몇 부든 판테온 컵 플레이오프에 나갑니다.\n\n우리 80강 상대: {opp} ({Calendar.DateLabel(KoDays[0])})\n" +
                    "국내 컵 화면에서 경기에 나가지 않기로 정할 수도 있습니다 (기권패). 그 시간은 던전 원정이나 훈련에 쓸 수 있습니다.");
            }
            if (s.natCups == null) return;
            string me = s.playerLudusId;
            foreach (var c in s.natCups.Values)
            {
                if (c.season != s.season) continue;
                Advance(s, c);
                foreach (var t in c.ties.ToList())
                {
                    if (t.day != s.day || Done(t) || t.home == me || t.away == me) continue;
                    if (c.nation != s.nation && t.stage != "r10" && t.stage != "league5") QuickTie(s, c, t, rng);
                    else PlayTie(s, c.nation, t.id, rng);
                }
            }
        }
    }
}
