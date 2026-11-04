// 대회 수상 (Unity판 전용, FM 시즌 시상처럼): 대회가 끝날 때마다 최우수 검투사·최다 처치·최우수 신인(성장기)·올해의 라니스타,
// 시즌이 완전히 끝나면 나라별 올해의 검투사와 세계 올해의 검투사. 상을 받으면 명성·소정의 상금·기록이 남는다.
// 기록은 경기마다 대회별로 쌓는다 (GameState.compStats). 다른 나라 국내 컵 앞쪽 라운드처럼 전력 공식으로만 끝난 경기는 개인 기록이 없다.
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Colosseum.Core
{
    /// <summary>한 대회에서 한 검투사의 기록</summary>
    public class CompStat
    {
        public int apps, wins, downs, kills;
        public double ratingSum;
        public string ludus;
    }

    /// <summary>받은 상 하나. 라니스타 상은 gladiatorId가 없고, 올림피카 감독 상은 ludusId 대신 nation</summary>
    public class AwardEntry
    {
        public int season;
        /// <summary>league:나라:부 | natcup:나라 | pantheon | olympica | nation:나라 | world</summary>
        public string comp;
        /// <summary>best | top | rookie | lanista | nation_best | world_best</summary>
        public string kind;
        public string gladiatorId, name, ludusId, ludusName, nation, note;
    }

    public static class Awards
    {
        public static bool Enabled => Honours.Enabled && FmRules.Enabled;

        public static readonly Dictionary<string, string> KindNames = new()
        {
            ["best"] = "최우수 검투사", ["top"] = "최다 처치", ["rookie"] = "최우수 신인", ["lanista"] = "올해의 라니스타",
            ["nation_best"] = "올해의 검투사", ["world_best"] = "세계 올해의 검투사",
        };

        static string N0(double x) => x.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>대회 이름 (서판·카드용)</summary>
        public static string CompName(string comp)
        {
            var p = comp.Split(':');
            return p[0] switch
            {
                "league" => Honours.LeagueName(p[1], int.Parse(p[2])),
                "natcup" => NatCupLogic.Name(p[1]),
                "pantheon" => Honours.CupName,
                "olympica" => OlympicaLogic.Name,
                "nation" => $"{Data.Nation(p[1]).@short} 시즌 시상",
                _ => "대륙 시즌 시상",
            };
        }

        // ── 기록 쌓기 ──

        /// <summary>"league"는 그 검투사가 뛰는 루두스의 리그로 바꾼다</summary>
        static string KeyFor(GameState s, string comp, Gladiator g, out Ludus home)
        {
            home = Career.LudusOf(s, g.id);
            if (comp != "league") return comp;
            return home == null ? null : $"league:{home.nation}:{home.tier}";
        }

        static void Add(GameState s, string comp, Gladiator g, bool won, int downs, int kills, MatchResult r)
        {
            string key = KeyFor(s, comp, g, out var home);
            if (key == null) return;
            s.compStats ??= new Dictionary<string, Dictionary<string, CompStat>>();
            if (!s.compStats.TryGetValue(key, out var table)) s.compStats[key] = table = new Dictionary<string, CompStat>();
            if (!table.TryGetValue(g.id, out var st)) table[g.id] = st = new CompStat();
            st.apps++;
            if (won) st.wins++;
            st.downs += downs;
            st.kills += kills;
            // 평점 매니저가 매긴 평점 (Ratings.Record가 먼저 붙인다)
            st.ratingSum += r?.stats != null && r.stats.TryGetValue(g.id, out var fs) ? fs.rating : 6.0;
            if (home != null) st.ludus = home.id;
        }

        /// <summary>1:1 한 판 (죽음 처리 전에 부른다)</summary>
        public static void RecordBout(GameState s, string comp, Gladiator w, Gladiator l, MatchResult r)
        {
            if (!Enabled || comp == null) return;
            Add(s, comp, w, true, 1, r.died != null ? 1 : 0, r);
            Add(s, comp, l, false, 0, 0, r);
        }

        /// <summary>난전 한 판: downs는 쓰러진 사람 → 쓰러뜨린 사람</summary>
        public static void RecordMelee(GameState s, string comp, MatchResult r)
        {
            if (!Enabled || comp == null) return;
            for (int side = 0; side < 2; side++)
                foreach (var id in r.sides[side])
                {
                    if (!s.gladiators.TryGetValue(id, out var g)) continue;
                    int downs = r.downs.Count(d => d.Value == id);
                    int kills = r.downs.Count(d => d.Value == id && r.deaths.Contains(d.Key));
                    Add(s, comp, g, side == r.winnerSide, downs, kills, r);
                }
        }

        // ── 고르기 ──

        /// <summary>평균 평점</summary>
        public static double AvgRating(CompStat st) => st.apps > 0 ? st.ratingSum / st.apps : 6.0;

        /// <summary>수상 점수: 평균 평점을 가장 크게, 그다음 승·쓰러뜨림·죽음·출전 수</summary>
        static double Score(CompStat st) => (AvgRating(st) - 6.0) * 8 + st.wins * 2 + st.downs + st.kills * 1.5 + st.apps * 0.5 + (st.apps > 0 ? 3.0 * st.wins / st.apps : 0);

        static List<(Gladiator g, CompStat st)> Pool(GameState s, Dictionary<string, CompStat> table, int minApps) =>
            table.Where(kv => kv.Value.apps >= minApps && s.gladiators.TryGetValue(kv.Key, out _))
                .Select(kv => (s.gladiators[kv.Key], kv.Value)).ToList();

        static string StatLine(CompStat st) => $"평점 {AvgRating(st):0.00} · {st.apps}경기 {st.wins}승 · 쓰러뜨린 상대 {st.downs}" + (st.kills > 0 ? $" (죽음 {st.kills})" : "");

        /// <summary>대회 상금 기준 (최우수 검투사 몫)</summary>
        static double PrizeBase(string comp)
        {
            var p = comp.Split(':');
            return p[0] switch
            {
                "league" => int.Parse(p[2]) switch { 1 => 2000, 2 => 1200, 3 => 800, _ => 500 },
                "natcup" => 1000,
                "pantheon" => 3000,
                "olympica" => 2500,
                _ => 0,
            };
        }

        static AwardEntry Give(GameState s, string comp, string kind, Gladiator g, CompStat st, double money, double fame)
        {
            var home = st?.ludus != null && s.ludi.TryGetValue(st.ludus, out var h) ? h : Career.LudusOf(s, g.id);
            var e = new AwardEntry
            {
                season = s.season, comp = comp, kind = kind, gladiatorId = g.id, name = g.name,
                ludusId = home?.id, ludusName = home?.name, nation = home?.nation ?? g.nation, note = st != null ? StatLine(st) : null,
            };
            s.awards ??= new List<AwardEntry>();
            s.awards.Add(e);
            if (g.alive)
            {
                g.popularity = Math.Min(100, g.popularity + fame);
                g.morale = Math.Min(100, g.morale + 5);
            }
            Career.LogCareer(s, g, $"{CompName(comp)} {KindNames[kind]} 수상", "milestone");
            if (home != null && money > 0) Economy.AddMoney(s, home, money, $"{g.name} {CompName(comp)} {KindNames[kind]} 상금");
            return e;
        }

        static AwardEntry GiveLanista(GameState s, string comp, Ludus l, string note, double money)
        {
            var e = new AwardEntry { season = s.season, comp = comp, kind = "lanista", ludusId = l.id, ludusName = l.name, nation = l.nation, note = note };
            s.awards ??= new List<AwardEntry>();
            s.awards.Add(e);
            if (money > 0) Economy.AddMoney(s, l, money, $"{CompName(comp)} 올해의 라니스타 상금");
            return e;
        }

        /// <summary>한 대회의 개인 상 셋 (최우수·최다 처치·성장기 신인)</summary>
        static List<AwardEntry> PlayerAwards(GameState s, string comp, int minApps)
        {
            var o = new List<AwardEntry>();
            if (s.compStats == null || !s.compStats.TryGetValue(comp, out var table)) return o;
            var pool = Pool(s, table, minApps);
            if (pool.Count == 0) return o;
            double b = PrizeBase(comp);
            var best = pool.OrderByDescending(x => Score(x.st)).ThenByDescending(x => x.st.downs).First();
            o.Add(Give(s, comp, "best", best.g, best.st, b, 6));
            var top = pool.Where(x => x.st.downs > 0).OrderByDescending(x => x.st.downs).ThenByDescending(x => x.st.kills).ThenByDescending(x => x.st.wins).FirstOrDefault();
            if (top.g != null) o.Add(Give(s, comp, "top", top.g, top.st, Math.Round(b * 0.6), 4));
            // 신인: 종족마다 다른 성장기 나이에 있는 검투사
            var rookie = pool.Where(x => GladiatorRules.AgeStage(x.g) == "growth").OrderByDescending(x => Score(x.st)).FirstOrDefault();
            if (rookie.g != null) o.Add(Give(s, comp, "rookie", rookie.g, rookie.st, Math.Round(b * 0.6), 4));
            return o;
        }

        /// <summary>루두스 전력 (가장 강한 다섯의 평균 능력)</summary>
        static double Strength(GameState s, Ludus l) =>
            l.gladiators.Where(id => s.gladiators.TryGetValue(id, out var g) && g.alive).Select(id => GladiatorRules.ComputeCA(s.gladiators[id]))
                .OrderByDescending(x => x).Take(5).DefaultIfEmpty(0).Average();

        // ── 대회가 끝날 때 ──

        /// <summary>정규 리그가 끝난 날 (순위가 굳은 뒤): 모든 나라·모든 부</summary>
        public static void LeagueEnd(GameState s)
        {
            if (!Enabled) return;
            var mine = new List<AwardEntry>();
            var me = s.ludi[s.playerLudusId];
            foreach (var n in Data.Nations)
                for (int tier = 1; tier <= FmRules.MaxTier; tier++)
                {
                    var table = Economy.TierRanking(s, tier, n.id);
                    if (table.Count == 0) continue;
                    string comp = $"league:{n.id}:{tier}";
                    var list = PlayerAwards(s, comp, 4);
                    // 라니스타: 전력 순위보다 가장 높이 올라간 루두스 (같으면 높은 순위)
                    var byStrength = table.OrderByDescending(l => Strength(s, l)).ToList();
                    var lan = table.Select((l, i) => (l, rank: i + 1, over: byStrength.IndexOf(l) - i)).OrderByDescending(x => x.over).ThenBy(x => x.rank).First();
                    list.Add(GiveLanista(s, comp, lan.l, $"전력 {byStrength.IndexOf(lan.l) + 1}위 → 리그 {lan.rank}위", Math.Round(PrizeBase(comp) * 1.2)));
                    if (n.id == me.nation) mine.AddRange(list);
                }
            Announce(s, $"{Data.Nation(me.nation).@short} 리그 시상", mine, true);
            Ceremony(s, $"awards:comp:league:{me.nation}:{me.tier}:{s.season}");
        }

        /// <summary>국내 컵 우승이 정해진 뒤</summary>
        public static void NatCupEnd(GameState s, NatCup c)
        {
            if (!Enabled) return;
            string comp = $"natcup:{c.nation}";
            var list = PlayerAwards(s, comp, 2);
            // 라니스타: 얼마나 멀리 갔나 + 아래 부일수록 가산
            int Reach(string id) => c.champion == id ? 5 : c.runnerUp == id ? 4 : c.final5?.Contains(id) == true ? 3
                : c.ties.Any(t => t.stage == "r10" && (t.home == id || t.away == id)) ? 2 : c.ties.Any(t => t.stage == "r20" && (t.home == id || t.away == id)) ? 1 : 0;
            var lan = c.teams.Where(id => s.ludi.ContainsKey(id)).Select(id => s.ludi[id]).OrderByDescending(l => Reach(l.id) + (l.tier - 1) * 0.75).ThenBy(l => l.tier).First();
            list.Add(GiveLanista(s, comp, lan, $"{lan.tier}부 · {(Reach(lan.id) switch { 5 => "우승", 4 => "준우승", 3 => "5강", 2 => "10강", 1 => "20강", _ => "40강" })}", Math.Round(PrizeBase(comp) * 1.2)));
            if (c.nation != s.nation) return;
            Announce(s, $"{NatCupLogic.Name(c.nation)} 시상", list, false);
            Ceremony(s, $"awards:comp:{comp}:{s.season}");
        }

        /// <summary>판테온 컵 결승이 끝난 뒤</summary>
        public static void PantheonEnd(GameState s, Cup cup, string runnerUp)
        {
            if (!Enabled) return;
            var list = PlayerAwards(s, "pantheon", 2);
            bool Plays(string id, string stage) => cup.ties.Any(t => t.stage == stage && (t.home == id || t.away == id));
            int Reach(string id) => cup.champion == id ? 6 : runnerUp == id ? 5 : Plays(id, "sf") ? 4 : Plays(id, "qf") ? 3 : cup.groups.Any(g => g.Contains(id)) ? 2 : 1;
            var teams = cup.teams.Where(id => s.ludi.ContainsKey(id)).Select(id => s.ludi[id]).ToList();
            if (teams.Count > 0)
            {
                var byStrength = teams.OrderByDescending(l => Strength(s, l)).ToList();
                var lan = teams.OrderByDescending(l => Reach(l.id) + byStrength.IndexOf(l) * 0.25).First();
                list.Add(GiveLanista(s, "pantheon", lan, $"출전 팀 전력 {byStrength.IndexOf(lan) + 1}위", Math.Round(PrizeBase("pantheon") * 1.2)));
            }
            Announce(s, $"{Honours.CupName} 시상", list, false);
            Ceremony(s, $"awards:comp:pantheon:{s.season}");
        }

        /// <summary>올림피카 컵이 끝난 뒤. 라니스타 상은 우승한 나라의 대표팀 감독</summary>
        public static void OlympicaEnd(GameState s, Olympica o)
        {
            if (!Enabled) return;
            var list = PlayerAwards(s, "olympica", 2);
            bool mine = o.champion == s.nation && o.playerManager == true;
            var me = s.ludi[s.playerLudusId];
            var e = new AwardEntry
            {
                season = s.season, comp = "olympica", kind = "lanista", nation = o.champion,
                ludusId = mine ? me.id : null, ludusName = mine ? me.name : null, note = $"{Data.Nation(o.champion).@short} 대표팀 감독",
            };
            s.awards ??= new List<AwardEntry>();
            s.awards.Add(e);
            list.Add(e);
            Announce(s, $"{OlympicaLogic.Name} 시상", list, false);
            Ceremony(s, $"awards:comp:olympica:{s.season}");
        }

        // ── 시즌이 완전히 끝날 때 ──

        /// <summary>대회 무게: 1부와 대륙 대회일수록 크다</summary>
        static double Weight(string comp)
        {
            var p = comp.Split(':');
            return p[0] switch
            {
                "league" => int.Parse(p[2]) switch { 1 => 1.5, 2 => 1.0, 3 => 0.7, _ => 0.5 },
                "pantheon" or "olympica" => 2.0,
                _ => 1.0,
            };
        }

        /// <summary>EndSeason 처음에 부른다 (승강·나이 반영 전): 나라별·세계 올해의 검투사, 시상식 표시, 기록 비우기</summary>
        public static void SeasonEnd(GameState s)
        {
            if (!Enabled) return;
            var totals = new Dictionary<string, (double score, CompStat sum)>();
            foreach (var (comp, table) in s.compStats ?? new Dictionary<string, Dictionary<string, CompStat>>())
                foreach (var (id, st) in table)
                {
                    var cur = totals.TryGetValue(id, out var t) ? t : (0, new CompStat());
                    cur.sum.apps += st.apps; cur.sum.wins += st.wins; cur.sum.downs += st.downs; cur.sum.kills += st.kills; cur.sum.ratingSum += st.ratingSum;
                    cur.score += Score(st) * Weight(comp);
                    totals[id] = cur;
                }
            var pool = totals.Where(kv => kv.Value.sum.apps >= 5 && s.gladiators.TryGetValue(kv.Key, out var g) && g.alive && Career.LudusOf(s, g.id) != null)
                .Select(kv => (g: s.gladiators[kv.Key], kv.Value.score, kv.Value.sum)).ToList();
            var list = new List<AwardEntry>();
            foreach (var n in Data.Nations)
            {
                var best = pool.Where(x => Career.LudusOf(s, x.g.id).nation == n.id).OrderByDescending(x => x.score).FirstOrDefault();
                if (best.g == null) continue;
                var st = new CompStat { apps = best.sum.apps, wins = best.sum.wins, downs = best.sum.downs, kills = best.sum.kills, ratingSum = best.sum.ratingSum, ludus = Career.LudusOf(s, best.g.id).id };
                var e = Give(s, $"nation:{n.id}", "nation_best", best.g, st, 3000, 8);
                if (n.id == s.nation) list.Add(e);
            }
            if (pool.Count > 0)
            {
                var w = pool.OrderByDescending(x => x.score).First();
                var st = new CompStat { apps = w.sum.apps, wins = w.sum.wins, downs = w.sum.downs, kills = w.sum.kills, ratingSum = w.sum.ratingSum, ludus = Career.LudusOf(s, w.g.id).id };
                list.Insert(0, Give(s, "world", "world_best", w.g, st, 8000, 12));
            }
            Ceremony(s, $"awards:season:{s.season}");
            s.compStats = null;
            Announce(s, $"시즌 {s.season} 시상식", list, false);
        }

        // ── 알림 ──

        public static string Line(AwardEntry e)
        {
            string who = e.gladiatorId != null ? $"{e.name} ({e.ludusName ?? "무소속"})" : e.ludusName ?? (e.nation != null ? $"{Data.Nation(e.nation).@short} 대표팀" : "");
            return $"{KindNames[e.kind]}: {who}" + (e.note != null ? $" — {e.note}" : "");
        }

        /// <summary>시상 카드를 띄울 차례에 넣는다 (Shell이 하나씩 띄운다)</summary>
        static void Ceremony(GameState s, string link) =>
            (s.ceremonies ??= new List<string>()).Add(link);

        static void Announce(GameState s, string title, List<AwardEntry> list, bool byComp)
        {
            if (list.Count == 0) return;
            var me = s.ludi[s.playerLudusId];
            bool ours = list.Any(e => e.ludusId == me.id);
            string body = byComp
                ? string.Join("\n\n", list.GroupBy(e => e.comp).Select(g => $"[{CompName(g.Key)}]\n" + string.Join("\n", g.Select(Line))))
                : string.Join("\n", list.Select(Line));
            StateUtil.PushMessage(s, new InboxMessage
            {
                kind = "info",
                title = ours ? $"{title} — 우리 루두스 수상!" : title,
                body = body + "\n\n상을 받은 검투사는 명성이 오르고, 소속 루두스는 상금을 받습니다. 수상 기록은 비석의 언덕에 남습니다.",
            });
        }
    }
}
