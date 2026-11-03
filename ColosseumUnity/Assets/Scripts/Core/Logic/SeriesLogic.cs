// 여러 판을 잇는 경기: 단체전(승자 연속, 체력 이월)과 판테온 컵(삼판양승) (src/core/series.ts)
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Colosseum.Core
{
    public class FormatMult { public double rp, money; }

    public class Bout
    {
        public MatchResult result;
        /// <summary>0쪽, 1쪽 검투사 (경기 전 모습)</summary>
        public Gladiator a, b;
        public MatchSummary summary;
        /// <summary>이 판을 시작할 때 체력 비율</summary>
        public double[] startHp;
    }

    public class Series
    {
        public List<Bout> bouts = new();
        /// <summary>이긴 판 수</summary>
        public double[] score = { 0, 0 };
        /// <summary>0 | 1</summary>
        public int winner;
        /// <summary>나오지 못해 진 판</summary>
        public double[] walkovers = { 0, 0 };

        /// <summary>난전(Unity판 전용)이면 그 한 경기. 이때 bouts는 비고, score는 쓰러뜨린 적 수</summary>
        public MatchResult melee;
        /// <summary>난전 양쪽 명단 (경기 전 모습)</summary>
        public List<Gladiator>[] meleeSides;
        public MatchSummary meleeSummary;

        /// <summary>경기의 관중 열기 (판들의 평균)</summary>
        public double Crowd() => melee != null ? melee.crowd : bouts.Count == 0 ? 0 : bouts.Sum(b => b.result.crowd) / bouts.Count;
    }

    public class RunOptions
    {
        public string rule;
        /// <summary>winner_stays: 이긴 사람이 남아 다음 상대와 싸운다 (단체전). best_of: 한 판씩 짝지어 과반을 먼저 따면 이긴다 (컵)</summary>
        public string mode;
        public string homeLudusId;
        /// <summary>승자가 다음 판 전에 숨을 고르며 되찾는 체력 비율</summary>
        public double? breather;
    }

    public static class SeriesLogic
    {
        /// <summary>Format = 1 | 3 | 5</summary>
        public static readonly Dictionary<int, string> FormatNames = new() { [1] = "1:1", [3] = "3:3 단체전", [5] = "5:5 단체전" };

        /// <summary>단체전은 판이 클수록 RP와 돈이 커진다</summary>
        public static readonly Dictionary<int, FormatMult> FormatMultTable = new()
        {
            [1] = new FormatMult { rp = 1, money = 1 },
            [3] = new FormatMult { rp = 1.3, money = 2.2 },
            [5] = new FormatMult { rp = 1.6, money = 3.5 },
        };

        static double LineupKey(Gladiator g) => GladiatorRules.ComputeCA(g) * (1 - g.wear / 200);

        /// <summary>출전 명단을 자동으로 짠다: 강하고 상태 좋은 순</summary>
        public static List<Gladiator> AutoLineup(GameState state, Ludus ludus, int n, double at, List<string> keep = null)
        {
            keep ??= new List<string>();
            var output = keep
                .Select(id => id != null && state.gladiators.TryGetValue(id, out var g) ? g : null)
                .Where(g => g != null && g.alive && ludus.gladiators.Contains(g.id) && g.injury?.severity != "serious" && g.deployment == null)
                .ToList();
            var pool = ludus.gladiators.Select(id => state.gladiators[id])
                .Where(g => !output.Contains(g) && ChallengeLogic.Availability(state, g, at) == null && !Condition.IsJaded(g))
                .ToList()
                .OrderByDescending(LineupKey)
                .ToList();
            return output.Concat(pool).Take(n).ToList();
        }

        /// <summary>단체전·컵을 양쪽 전원이 동시에 싸우는 난전으로 치르는가 (Unity판). 웹판과 결과를 비교하는 골든 테스트만 끈다</summary>
        public static bool Melee = true;

        /// <summary>난전: 한 경기로 끝낸다. score는 각 편이 쓰러뜨린 적 수</summary>
        static Series RunMelee(GameState state, Ludus[] sides, List<Gladiator>[] lineups, RunOptions o, Rng rng)
        {
            var snaps = lineups.Select(l => l.Select(MatchLogic.CloneGladiator).ToList()).ToArray();
            var result = Sim.RunTeam(lineups[0], lineups[1], new MatchOptions
            {
                rule = o.rule,
                seed = MatchLogic.Seed31(rng),
                missioDeathMult = NationRules.RulesOfNation(sides[1].nation).missioDeathMult,
                medicalLevel = new[] { ChallengeLogic.MedicalOf(state, sides[0]), ChallengeLogic.MedicalOf(state, sides[1]) },
                // 우리 편은 전략 화면의 기본값, AI는 감독 성향대로
                teamTactics = new[] { Tactics.TeamFor(sides[0], lineups[0]), Tactics.TeamFor(sides[1], lineups[1]) },
            });
            var summary = MatchLogic.ApplyTeamResult(state, result, o.rule, rng);
            int n = Math.Max(lineups[0].Count, lineups[1].Count);
            double Downed(int side) => result.downs.Count(d => result.sides[1 - side].Contains(d.Key));
            return new Series
            {
                melee = result, meleeSides = snaps, meleeSummary = summary,
                score = new[] { Downed(0), Downed(1) },
                winner = result.winnerSide,
                walkovers = new double[] { Math.Max(0, n - lineups[0].Count), Math.Max(0, n - lineups[1].Count) },
            };
        }

        /// <summary>두 루두스가 명단대로 여러 판을 치른다. 개인 성장·부상·죽음은 판마다, RP와 돈은 호출한 쪽에서 정산한다</summary>
        public static Series RunSeries(GameState state, Ludus[] sides, List<Gladiator>[] lineups, RunOptions o, Rng rng)
        {
            if (Melee && lineups[0].Count > 0 && lineups[1].Count > 0) return RunMelee(state, sides, lineups, o, rng);
            var bouts = new List<Bout>();
            var score = new double[] { 0, 0 };
            var walkovers = new double[] { 0, 0 };
            int n = Math.Max(lineups[0].Count, lineups[1].Count);

            MatchResult Fight(Gladiator a, Gladiator b, double[] hp)
            {
                var snapA = MatchLogic.CloneGladiator(a);
                var snapB = MatchLogic.CloneGladiator(b);
                var result = Sim.Run(a, b, new MatchOptions
                {
                    rule = o.rule,
                    seed = MatchLogic.Seed31(rng),
                    missioDeathMult = NationRules.RulesOfNation(sides[1].nation).missioDeathMult,
                    medicalLevel = new[] { ChallengeLogic.MedicalOf(state, sides[0]), ChallengeLogic.MedicalOf(state, sides[1]) },
                    startHp = new double?[] { hp[0] < 1 ? hp[0] : null, hp[1] < 1 ? hp[1] : null },
                });
                var summary = MatchLogic.ApplyMatchResult(state, result, new ApplyOptions { rule = o.rule, ranked = false, homeLudusId = o.homeLudusId }, rng);
                bouts.Add(new Bout { result = result, a = snapA, b = snapB, summary = summary, startHp = hp });
                return result;
            }

            if (o.mode == "best_of")
            {
                int need = (int)Math.Floor(n / 2.0) + 1;
                for (int i = 0; i < n && score[0] < need && score[1] < need; i++)
                {
                    var a = i < lineups[0].Count ? lineups[0][i] : null;
                    var b = i < lineups[1].Count ? lineups[1][i] : null;
                    if (a == null || b == null)
                    {
                        // 사람이 모자라면 그 판은 기권패
                        if (a == null) walkovers[0]++;
                        if (b == null) walkovers[1]++;
                        if (a != null && b == null) score[0]++;
                        if (b != null && a == null) score[1]++;
                        continue;
                    }
                    var r = Fight(a, b, new double[] { 1, 1 });
                    score[r.winnerId == a.id ? 0 : 1]++;
                }
            }
            else
            {
                int i = 0;
                int j = 0;
                var hp = new double[] { 1, 1 };
                while (i < lineups[0].Count && j < lineups[1].Count)
                {
                    var a = lineups[0][i];
                    var b = lineups[1][j];
                    var r = Fight(a, b, hp);
                    double recover = o.breather ?? 0.2;
                    if (r.winnerId == a.id)
                    {
                        score[0]++;
                        hp = new double[] { Math.Min(1, r.hpLeft[a.id] + recover), 1 };
                        j++;
                    }
                    else
                    {
                        score[1]++;
                        hp = new double[] { 1, Math.Min(1, r.hpLeft[b.id] + recover) };
                        i++;
                    }
                }
                walkovers[0] = Math.Max(0, n - lineups[0].Count);
                walkovers[1] = Math.Max(0, n - lineups[1].Count);
            }
            // 단체전은 상대 명단을 다 쓰러뜨린 쪽이, 컵은 판을 더 많이 딴 쪽이 이긴다
            int winner = o.mode == "winner_stays"
                ? (lineups[1].Count == 0 || (lineups[0].Count > 0 && bouts.Count > 0 && bouts[bouts.Count - 1].result.winnerId == bouts[bouts.Count - 1].a.id) ? 0 : 1)
                : score[0] >= score[1] ? (score[0] == score[1] ? (rng.Chance(0.5) ? 0 : 1) : 0) : 1;
            return new Series { bouts = bouts, score = score, winner = winner, walkovers = walkovers };
        }
    }
}
