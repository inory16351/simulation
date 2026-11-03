// 올림피카 컵 (Unity판 전용): 4시즌마다 여섯 나라 대표(5명 + 후보 2명)가 겨루는 국가 대항전. 5:5 미시오, 홈·원정 풀리그 10라운드, 많이 이긴 나라가 우승.
// 정규 리그 사이 '대표 주간'에 치른다: 그날은 리그 경기가 잡히지 않는다. 우리 루두스 명성이 나라 1위면 대표팀 감독 제안이 온다 (사양하면 자동 선발).
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Colosseum.Core
{
    public static class OlympicaLogic
    {
        public const string Name = "올림피카 컵";
        public const int Interval = 4;
        public const int SetupDay = 5;
        public static readonly int[] MatchDays = { 20, 28, 36, 44, 52, 60, 68, 76, 84, 92 };
        public const int Squad = 7, Lineup = 5;
        public const double RewardManager = 15000, CallUpFee = 500;

        public static bool IsYear(int season) => season % Interval == 0;

        /// <summary>다음 대회가 열리는 시즌</summary>
        public static int NextSeason(int season) => IsYear(season) ? season : season + Interval - season % Interval;

        public static Olympica Current(GameState s) => s.olympica != null && s.olympica.season == s.season ? s.olympica : null;

        static bool Done(CupTie t) => !string.IsNullOrEmpty(t.winner);

        /// <summary>오늘이 대표 주간 경기 날인가 (리그 경기를 잡지 않는다)</summary>
        public static bool IsMatchDay(GameState s, int day) => FmRules.Enabled && IsYear(s.season) && Array.IndexOf(MatchDays, day) >= 0;

        /// <summary>선발 점수: 실력 + 명성</summary>
        static double Score(Gladiator g) => GladiatorRules.ComputeCA(g) * (1 - g.wear / 300) + g.popularity * 0.3;

        static bool Fit(Gladiator g) => g.alive && g.injury?.severity != "serious" && g.deployment == null && GladiatorRules.CanFight(g);

        /// <summary>그 나라 출신으로 루두스에 있는 검투사 (선발 후보), 점수 순</summary>
        public static List<Gladiator> Candidates(GameState s, string nation)
        {
            var owned = new HashSet<string>(s.ludi.Values.SelectMany(l => l.gladiators));
            return s.gladiators.Values.Where(g => g.nation == nation && owned.Contains(g.id) && Fit(g)).OrderByDescending(Score).ToList();
        }

        static void Setup(GameState s)
        {
            var o = new Olympica { season = s.season };
            foreach (var n in Data.Nations) o.squads[n.id] = Candidates(s, n.id).Take(Squad).Select(g => g.id).ToList();
            var nations = Data.Nations.Select(n => n.id).ToList();
            var rounds = Fixtures.RoundRobin(nations.Count);
            // 전반 5라운드, 후반 5라운드는 홈·원정을 바꿔 다시
            for (int leg = 0; leg < 2; leg++)
                for (int r = 0; r < rounds.Count; r++)
                    foreach (var (a, b) in rounds[r])
                    {
                        int idx = leg * rounds.Count + r;
                        o.ties.Add(new CupTie { id = $"T{s.nextId++}", stage = "olympica", day = MatchDays[idx], group = idx, home = nations[leg == 0 ? a : b], away = nations[leg == 0 ? b : a] });
                    }
            s.olympica = o;
            var me = s.ludi[s.playerLudusId];
            double myRep = Reputation.LudusReputation(s, me);
            bool best = s.ludi.Values.Where(l => l.nation == s.nation && !l.isPlayer).All(l => Reputation.LudusReputation(s, l) <= myRep);
            var mine = o.squads[s.nation].Where(id => me.gladiators.Contains(id)).ToList();
            foreach (var id in mine)
            {
                var g = s.gladiators[id];
                g.morale = Math.Min(100, g.morale + 6);
                g.popularity = Math.Min(100, g.popularity + 3);
                Career.LogCareer(s, g, $"시즌 {s.season} {Name} {Data.Nation(s.nation).@short} 대표로 뽑혔습니다", "milestone");
                Economy.AddMoney(s, me, CallUpFee, $"{g.name} 대표 차출 수당");
            }
            string called = mine.Count > 0 ? $"\n우리 검투사 중 대표로 뽑힌 자: {string.Join(", ", mine.Select(id => s.gladiators[id].name))} (한 명당 수당 {CallUpFee:N0} D)" : "\n우리 검투사 중 대표로 뽑힌 자는 없습니다.";
            StateUtil.PushMessage(s, "info", $"{Name} 개막 — 시즌 {s.season}",
                $"4년에 한 번, 여섯 나라 대표가 맞붙는 {Name}가 열립니다. 나라마다 5명 + 후보 2명이 5:5 미시오로 홈·원정 풀리그 10라운드를 치르고, 가장 많이 이긴 나라가 우승합니다.\n" +
                $"대표 주간: {Calendar.DateLabel(MatchDays[0])}부터 8일마다. 그날은 리그 경기가 잡히지 않습니다." + called);
            if (best)
            {
                o.offer = true;
                StateUtil.PushMessage(s, "decision", $"{Data.Nation(s.nation).@short} 대표팀 감독 제안",
                    $"우리 루두스의 명성이 나라에서 가장 높아 대표팀을 맡아 달라는 제안이 왔습니다. 맡으면 대표 7명을 직접 뽑고 경기마다 출전 5명을 정합니다. 우승하면 포상금 {RewardManager:N0} D를 받습니다.\n\n맡을지 정해야 다음 날로 넘어갈 수 있습니다.");
            }
        }

        /// <summary>감독 제안 수락</summary>
        public static void Accept(GameState s)
        {
            var o = Current(s);
            if (o == null) return;
            o.offer = null;
            o.playerManager = true;
        }

        public static void Decline(GameState s)
        {
            var o = Current(s);
            if (o != null) o.offer = null;
        }

        public static bool Managing(GameState s) => Current(s)?.playerManager == true;

        /// <summary>감독: 대표 명단을 바꾼다 (첫 경기 전까지). 후보 안에서 7명까지</summary>
        public static string SetSquad(GameState s, List<string> ids)
        {
            var o = Current(s);
            if (o == null || o.playerManager != true) return "대표팀을 맡고 있지 않습니다";
            if (s.day >= MatchDays[0]) return "첫 경기가 지나 명단을 바꿀 수 없습니다";
            var allowed = new HashSet<string>(Candidates(s, s.nation).Select(g => g.id));
            var pick = ids.Where(allowed.Contains).Distinct().Take(Squad).ToList();
            if (pick.Count < Lineup) return $"적어도 {Lineup}명은 뽑아야 합니다";
            o.squads[s.nation] = pick;
            return null;
        }

        /// <summary>오늘 우리가 감독으로 치를 경기</summary>
        public static CupTie TodaysPlayerTie(GameState s)
        {
            var o = Current(s);
            if (o == null || o.playerManager != true) return null;
            return o.ties.FirstOrDefault(t => t.day == s.day && !Done(t) && (t.home == s.nation || t.away == s.nation));
        }

        /// <summary>출전 명단: 감독이 정한 사람 먼저, 나머지는 대표 중 강한 순. 모자라면 나라에서 다음으로 강한 자를 불러 대표로 올린다</summary>
        public static List<Gladiator> LineupFor(GameState s, string nation, List<string> keep = null)
        {
            var o = s.olympica;
            var squad = o.squads[nation];
            var picked = (keep ?? new List<string>()).Where(squad.Contains).Select(id => s.gladiators[id]).Where(Fit).ToList();
            var rest = squad.Select(id => s.gladiators[id]).Where(g => Fit(g) && !picked.Contains(g)).OrderByDescending(Score);
            var line = picked.Concat(rest).Take(Lineup).ToList();
            if (line.Count < Lineup)
                foreach (var g in Candidates(s, nation))
                {
                    if (line.Count >= Lineup) break;
                    if (line.Contains(g)) continue;
                    line.Add(g);
                    if (!squad.Contains(g.id)) squad.Add(g.id);
                }
            return line;
        }

        /// <summary>순위: 승수 → 쓰러뜨린 상대 수 → 승자승 → 계수</summary>
        public static List<FixtureRow> Table(GameState s, Olympica o) =>
            Fixtures.Table(Data.Nations.Select(n => n.id), o.ties, id => CupLogic.CoefficientOf(s, id));

        /// <summary>경기 하나를 치른다</summary>
        public static PlayedTie PlayTie(GameState s, string tieId, Rng rng, List<string> lineup = null)
        {
            var o = s.olympica;
            var t = o?.ties.FirstOrDefault(x => x.id == tieId);
            if (t == null) return new PlayedTie { error = "그런 경기가 없습니다" };
            if (Done(t)) return new PlayedTie { error = "이미 치른 경기" };
            bool mineHome = o.playerManager == true && t.home == s.nation, mineAway = o.playerManager == true && t.away == s.nation;
            var lh = LineupFor(s, t.home, mineHome ? lineup : null);
            var la = LineupFor(s, t.away, mineAway ? lineup : null);
            if (lh.Count == 0 || la.Count == 0)
            {
                int walk = lh.Count > 0 ? 0 : 1;
                t.score = walk == 0 ? new double[] { Lineup, 0 } : new double[] { 0, Lineup };
                t.winner = walk == 0 ? t.home : t.away;
                t.forfeit = true;
                Finish(s, o);
                return new PlayedTie { tie = t, series = new Series { winner = walk }, summary = new MatchSummary() };
            }
            var series = SeriesLogic.RunNations(s, t.home, t.away, new[] { lh, la }, "missio", rng);
            t.score = series.score;
            t.winner = series.winner == 0 ? t.home : t.away;
            Notify(s, t);
            Finish(s, o);
            return new PlayedTie { tie = t, series = series, summary = series.meleeSummary };
        }

        static void Notify(GameState s, CupTie t)
        {
            if (t.home != s.nation && t.away != s.nation) return;
            bool won = t.winner == s.nation;
            string opp = t.home == s.nation ? t.away : t.home;
            double my = t.score[t.home == s.nation ? 0 : 1], their = t.score[t.home == s.nation ? 1 : 0];
            StateUtil.PushMessage(s, "match", $"{Name}: {Data.Nation(s.nation).@short} {(won ? "승리" : "패배")}",
                $"상대 {Data.Nation(opp).name} ({(t.home == s.nation ? "홈" : "원정")}) · 쓰러뜨린 상대 {my:0} : 쓰러진 우리 {their:0}");
        }

        static void Finish(GameState s, Olympica o)
        {
            if (!string.IsNullOrEmpty(o.champion) || !o.ties.All(Done)) return;
            var table = Table(s, o);
            o.champion = table[0].id;
            o.runnerUp = table[1].id;
            Honours.RecordOlympica(s, o.champion, o.runnerUp);
            foreach (var id in o.squads[o.champion])
            {
                if (!s.gladiators.TryGetValue(id, out var g) || !g.alive) continue;
                g.popularity = Math.Min(100, g.popularity + 10);
                g.morale = Math.Min(100, g.morale + 10);
                Career.LogCareer(s, g, $"{Name} 우승 ({Data.Nation(o.champion).@short} 대표)", "milestone");
            }
            bool ours = o.champion == s.nation;
            string lines = string.Join("\n", table.Select((x, i) => $"{i + 1}위 {Data.Nation(x.id).name} · {x.won}승 {x.lost}패 · 쓰러뜨린 상대 {x.downs:0}"));
            StateUtil.PushMessage(s, "info", ours ? $"{Name} 우승!" : $"{Name} 우승: {Data.Nation(o.champion).name}",
                $"시즌 {s.season} {Name}가 끝났습니다. {Josa.J(Data.Nation(o.champion).name, "이/가")} 정상에 섰습니다.\n\n{lines}");
            if (ours && o.playerManager == true)
            {
                var me = s.ludi[s.playerLudusId];
                Economy.AddMoney(s, me, RewardManager, $"{Name} 우승 포상금");
                me.titles = (me.titles ?? 0) + 1;
            }
        }

        public static void Daily(GameState s, Rng rng)
        {
            if (!FmRules.Enabled) return;
            if (IsYear(s.season) && s.day == SetupDay) Setup(s);
            var o = Current(s);
            if (o == null) return;
            foreach (var t in o.ties.Where(x => x.day == s.day && !Done(x)).ToList())
            {
                if (o.playerManager == true && (t.home == s.nation || t.away == s.nation)) continue;
                PlayTie(s, t.id, rng);
            }
        }
    }
}
