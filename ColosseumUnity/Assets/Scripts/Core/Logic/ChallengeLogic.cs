// 도전 시스템 (docs/04_league.md): 신청, 응답, 거절 페널티, 경기 일정, AI 루두스끼리의 경기 (src/core/challenge.ts)
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Colosseum.Core
{
    public class PlayedMatch
    {
        public Challenge challenge;
        public MatchResult result;
        public Gladiator a, b;
        public MatchSummary summary;
        public string forfeit;
        /// <summary>단체전</summary>
        public Series series;
    }

    public static class ChallengeLogic
    {
        static double L(string k) => (double)Data.Balance["league"][k];
        static int LI(string k) => (int)Data.Balance["league"][k];
        static string RuleName(string r) => (string)Bal.Rule(r)["name"];
        static string N(double x) => x.ToString(CultureInfo.InvariantCulture);
        static string DateOf(double abs) => Calendar.DateLabel((int)Calendar.FromAbs((int)abs).day);
        /// <summary>TS의 `c.format ?` (undefined·0이면 거짓)</summary>
        static bool HasFormat(Challenge c) => c.format.HasValue && c.format.Value != 0;

        public static int Now(GameState state) => (int)Calendar.AbsDay(state.season, state.day);

        /// <summary>사투 생존에 쓰는 의료 수준: 의무실 단계 + 메디쿠스 실력</summary>
        public static double MedicalOf(GameState state, Ludus l) => l.facilities.medical + StaffLogic.RatingOf(state, l, "medicus") / 8;

        /// <summary>정규 기간에만 도전 경기를 치른다</summary>
        public static bool InRegularPeriod(GameState state) => state.day >= 15 && state.day <= (FmRules.Enabled ? FmRules.RegularEnd : 154);

        /// <summary>오늘 출전할 수 있는가 (쉬는 기간·부상·예약된 경기 확인). null이면 출전 가능</summary>
        public static string Availability(GameState state, Gladiator g, double? atOpt = null)
        {
            double at = atOpt ?? Now(state);
            if (!g.alive) return "사망";
            if (g.deployment != null) return g.deployment.kind == "dungeon" ? "던전 원정 중" : "용병 파견 중";
            if (GladiatorRules.AgeStage(g) == "youth") return "아직 어립니다";
            if (g.injury?.severity == "serious") return "중상";
            double rest = L("restDays");
            if (g.lastFightAt.HasValue && at - g.lastFightAt.Value < rest) return $"휴식 중 ({N(rest - (at - g.lastFightAt.Value))}일)";
            var booked = state.challenges.FirstOrDefault(c => c.status == "scheduled" && (c.fromGladiator == g.id || c.toGladiator == g.id) && c.matchAt != at);
            if (booked != null) return $"경기 예정 ({DateOf(booked.matchAt.Value)})";
            return null;
        }

        /// <summary>이 루두스가 상대에게 도전할 수 있는가. null이면 가능</summary>
        public static string CanChallenge(GameState state, Ludus from, Ludus to)
        {
            if (from.id == to.id) return "자기 자신";
            if (from.nation != to.nation) return "다른 나라";
            if (FmRules.Enabled && (state.day < 15 || state.day > FmRules.ChallengeClose)) return $"도전장은 {Calendar.DateLabel(15)}부터 {Calendar.DateLabel(FmRules.ChallengeClose)}까지만 보낼 수 있습니다 (정규 리그 기간)";
            if (!InRegularPeriod(state)) return "정규 기간(서리달 15일 ~ 그림자달 14일)이 아닙니다";
            double range = L("challengeRange");
            if (from.tier != to.tier && Math.Abs(from.rp - to.rp) > range) return $"다른 리그는 RP ±{N(range)} 안에서만";
            int week = Now(state) - 6;
            int sent = state.challenges.Count(c => c.from == from.id && c.createdAt >= week);
            double perWeek = L("challengesPerWeek");
            if (sent >= perWeek) return $"일주일에 {N(perWeek)}번까지만 신청할 수 있습니다";
            if (state.challenges.Any(c => c.from == from.id && c.to == to.id && (c.status == "pending" || c.status == "scheduled"))) return "이미 도전 중";
            if (FmRules.Enabled) return CanReceive(state, to, 1);
            return null;
        }

        /// <summary>FM식: 받는 쪽이 이 도전을 받을 수 있는가 (부상·경기 예정·휴식·원정으로 내보낼 사람이 없으면 보낼 수 없다).
        /// 이미 답을 기다리는 도전장만큼 사람을 빼고 센다. 우리 루두스에는 답을 기다리는 도전장이 2통까지만 온다</summary>
        public static string CanReceive(GameState state, Ludus to, int format)
        {
            int t = Now(state);
            var pending = state.challenges.Where(c => c.to == to.id && c.status == "pending").ToList();
            if (to.isPlayer && pending.Count >= 2) return "상대가 이미 답할 도전장이 밀려 있습니다";
            int reserved = pending.Sum(c => Math.Max(1, c.format ?? 1));
            int free = to.gladiators.Count(id => state.gladiators.TryGetValue(id, out var g) && Availability(state, g, t) == null && Availability(state, g, t + L("matchDelayMin")) == null);
            if (free - reserved < Math.Max(1, format))
                return format > 1 ? $"상대에게 지금 {format}명을 내보낼 여력이 없습니다 (부상·경기 예정·휴식)" : "상대에게 지금 내보낼 검투사가 없습니다 (부상·경기 예정·휴식)";
            return null;
        }

        /// <summary>TS createChallenge (Challenge | string). 실패하면 null</summary>
        public static Challenge CreateChallenge(GameState state, Ludus from, Ludus to, string fromGladiator, string rule, string toGladiator = null, double stake = 0, int format = 1) =>
            TryCreateChallenge(state, from, to, fromGladiator, rule, out _, toGladiator, stake, format);

        /// <summary>실패 이유는 error로 돌려준다 (UI용)</summary>
        public static Challenge TryCreateChallenge(GameState state, Ludus from, Ludus to, string fromGladiator, string rule, out string error, string toGladiator = null, double stake = 0, int format = 1)
        {
            error = CanChallenge(state, from, to);
            if (error != null) return null;
            if (rule == "sudden" && NationRules.RulesOfNation(from.nation).suddenBanned) { error = "이 나라 리그에서는 사투를 금합니다"; return null; }
            var g = state.gladiators[fromGladiator];
            var busy = Availability(state, g);
            if (busy != null) { error = $"{g.name}: {busy}"; return null; }
            if (FmRules.Enabled && format > 1 && CanReceive(state, to, format) is string full) { error = full; return null; }
            if (format > 1 && SeriesLogic.AutoLineup(state, from, format, Now(state) + L("matchDelayMin")).Count < format)
            {
                error = $"{SeriesLogic.FormatNames[format]}에 내보낼 검투사가 {format}명이 안 됩니다";
                return null;
            }
            var c = new Challenge
            {
                id = $"C{state.nextId++}", from = from.id, to = to.id, fromGladiator = fromGladiator, toGladiator = toGladiator, rule = rule, stake = stake,
                createdAt = Now(state), respondBy = Now(state) + L("respondDays"), status = "pending",
                format = format > 1 ? format : (int?)null,
            };
            state.challenges.Add(c);
            return c;
        }

        /// <summary>경기에 내보낼 검투사를 고른다 (가장 강하고 상태가 좋은 쪽)</summary>
        public static Gladiator PickFighter(GameState state, Ludus ludus, string rule, double at)
        {
            var pool = ludus.gladiators.Select(id => state.gladiators[id]).Where(g => Availability(state, g, at) == null && !Condition.IsJaded(g)).ToList();
            if (pool.Count == 0) return null;
            // 사투에는 아끼는 최강자 대신 두 번째로 강한 자를 내보내는 루두스가 많다
            var sorted = pool.OrderByDescending(g => GladiatorRules.ComputeCA(g) * (1 - g.wear / 200)).ToList();
            return rule == "sudden" && sorted.Count > 2 ? sorted[1] : sorted[0];
        }

        static void Schedule(GameState state, Challenge c, Rng rng, Gladiator defender)
        {
            c.toGladiator = defender.id;
            c.status = "scheduled";
            // 두 검투사의 다른 경기와 휴식 기간이 겹치지 않는 날로 잡는다
            var others = state.challenges
                .Where(x => x != c && x.status == "scheduled" && new[] { x.fromGladiator, x.toGladiator }.Any(id => id == c.fromGladiator || id == defender.id))
                .Select(x => x.matchAt.Value)
                .ToList();
            double rest = L("restDays");
            double day = Now(state) + rng.Int(LI("matchDelayMin"), LI("matchDelayMax"));
            // 대표 주간(올림피카)에는 리그 경기를 잡지 않는다
            bool Intl(double abs) { var (se, dd) = Calendar.FromAbs(abs); return se == state.season && OlympicaLogic.IsMatchDay(state, dd); }
            while (others.Any(d => Math.Abs(d - day) < rest) || Intl(day)) day++;
            c.matchAt = day;
        }

        /// <summary>도전을 받거나 거절한다. 거절은 시즌당 무료 횟수를 넘으면 RP를 잃는다. 실패하면 이유를 돌려준다</summary>
        public static string Respond(GameState state, Challenge c, bool accept, Rng rng, string defenderId = null)
        {
            var to = state.ludi[c.to];
            var from = state.ludi[c.from];
            if (accept)
            {
                var defender = !string.IsNullOrEmpty(defenderId) ? (state.gladiators.TryGetValue(defenderId, out var dg) ? dg : null) : PickFighter(state, to, c.rule, Now(state) + L("matchDelayMin"));
                if (defender == null) return "내보낼 검투사가 없습니다";
                var busy = Availability(state, defender);
                if (busy != null) return $"{defender.name}: {busy}";
                Schedule(state, c, rng, defender);
                var msgTarget = from.isPlayer ? from : to.isPlayer ? to : null;
                if (msgTarget != null)
                {
                    var mine = from.isPlayer ? state.gladiators[c.fromGladiator] : defender;
                    var theirs = from.isPlayer ? defender : state.gladiators[c.fromGladiator];
                    StateUtil.PushMessage(state, new InboxMessage
                    {
                        kind = "match",
                        title = HasFormat(c)
                            ? $"{SeriesLogic.FormatNames[c.format.Value]} {RuleName(c.rule)} 확정: {(from.isPlayer ? to.name : from.name)}"
                            : $"{RuleName(c.rule)} 확정: {mine.name} vs {theirs.name}",
                        body = $"상대 루두스: {(from.isPlayer ? to.name : from.name)}\n날짜: {DateOf(c.matchAt.Value)}\n우리: {mine.name} / 상대: {theirs.name}",
                    });
                }
                return null;
            }
            c.status = "declined";
            to.refusalsUsed++;
            // 사투 도전은 거절해도 페널티가 절반, 약한 상대를 피하면 두 배
            double penalty = 0;
            double free = L("freeRefusals");
            if (to.refusalsUsed > free)
            {
                penalty = L("refusalPenalty") * (c.rule == "sudden" ? 0.5 : 1) * (from.rp < to.rp ? 2 : 1);
                if (!FmRules.Frozen(state)) to.rp -= JsMath.Round(penalty);
            }
            if (from.isPlayer)
            {
                StateUtil.PushMessage(state, new InboxMessage
                {
                    kind = "info",
                    title = $"{Josa.J(to.name, "이/가")} 도전을 거절했습니다",
                    body = $"상대가 {RuleName(c.rule)} 신청을 거절했습니다.{(penalty != 0 ? $" 상대는 RP를 {N(JsMath.Round(penalty))} 잃었습니다." : "")}",
                });
            }
            if (to.isPlayer)
            {
                StateUtil.PushMessage(state, new InboxMessage
                {
                    kind = "warning",
                    title = "도전을 거절했습니다",
                    body = $"{from.name}의 {RuleName(c.rule)} 도전을 거절했습니다. 이번 시즌에 {N(to.refusalsUsed)}번 거절했습니다. {N(free)}번까지는 불이익이 없습니다.{(penalty != 0 ? $"\nRP -{N(JsMath.Round(penalty))}. 관중이 '겁쟁이 루두스'라고 수군댑니다." : "")}",
                });
            }
            return null;
        }

        static readonly Dictionary<string, double> AcceptChance = new()
        {
            ["aggressive"] = 0.85, ["stable"] = 0.6, ["grudge"] = 0.8, ["merchant"] = 0.65, ["developer"] = 0.6,
        };

        /// <summary>AI가 받은 도전에 응답할지 정한다</summary>
        static bool AiWantsToAccept(GameState state, Challenge c, Rng rng)
        {
            var to = state.ludi[c.to];
            var challenger = state.gladiators[c.fromGladiator];
            var best = PickFighter(state, to, c.rule, Now(state) + L("matchDelayMin"));
            if (best == null) return false;
            string p = to.aiPersonality ?? "stable";
            double chance = AcceptChance[p];
            if (c.rule == "sudden") chance *= p == "aggressive" || p == "grudge" ? 0.8 : 0.35;
            double diff = GladiatorRules.ComputeCA(best) - GladiatorRules.ComputeCA(challenger);
            // FM식: 정규 리그가 짧아 강한 상대를 무작정 피하지 않는다 (차이 영향 절반, 최소 30%)
            chance += diff / (FmRules.Enabled ? 200 : 100);
            // 무료 거절을 다 쓰면 웬만하면 받는다
            if (to.refusalsUsed >= L("freeRefusals")) chance += 0.25;
            return rng.Chance(Math.Max(FmRules.Enabled ? 0.3 : 0.05, Math.Min(0.97, chance)));
        }

        static readonly Dictionary<string, double[]> RuleWeights = new()
        {
            // training, missio, sudden
            ["aggressive"] = new[] { 3, 4, 1.2 },
            ["stable"] = new[] { 7, 3, 0.1 },
            ["grudge"] = new[] { 3, 4, 1.5 },
            ["merchant"] = new[] { 4, 5, 0.5 },
            ["developer"] = new[] { 8, 2, 0.1 },
        };

        static string PickRule(Rng rng, Ludus ludus)
        {
            string p = ludus.aiPersonality ?? "stable";
            var w = RuleWeights[p];
            var nr = NationRules.RulesOfNation(ludus.nation);
            double sudden = nr.suddenBanned ? 0 : w[2] * nr.aiSuddenMult;
            return rng.Weighted(new List<KeyValuePair<string, double>>
            {
                new("training", w[0]), new("missio", w[1]), new("sudden", sudden),
            });
        }

        /// <summary>매일: AI 응답, 기한 지난 도전 처리, AI의 도전, AI끼리의 경기</summary>
        public static void DailyChallenges(GameState state, Rng rng)
        {
            int t = Now(state);
            var player = state.ludi[state.playerLudusId];

            foreach (var c in state.challenges.Where(x => x.status == "pending").ToList())
            {
                var to = state.ludi[c.to];
                if (!to.isPlayer)
                {
                    // AI는 다음 날 답한다
                    if (t > c.createdAt) Respond(state, c, AiWantsToAccept(state, c, rng), rng);
                }
                else if (t > c.respondBy)
                {
                    Respond(state, c, false, rng);
                }
            }

            if (!InRegularPeriod(state)) return;

            // AI가 플레이어에게 도전장을 보낸다
            var rivals = state.ludi.Values.Where(l => !l.isPlayer && CanChallenge(state, l, player) == null).ToList();
            foreach (var ai in rivals)
            {
                if (!rng.Chance(L("aiChallengePlayerChance") / (FmRules.Enabled ? 8 : 3))) continue;
                if (FmRules.Enabled && state.challenges.Any(x => x.to == player.id && x.status == "pending")) break;
                string rule = PickRule(rng, ai);
                var g = PickFighter(state, ai, rule, t);
                if (g == null) continue;
                double roll = rng.Next();
                int format = roll < 0.08 ? 5 : roll < 0.25 ? 3 : 1;
                var c = CreateChallenge(state, ai, player, g.id, rule, null, 0, format);
                if (c == null && format > 1) c = CreateChallenge(state, ai, player, g.id, rule);
                if (c == null) continue;
                string fmt = HasFormat(c) ? $"{SeriesLogic.FormatNames[c.format.Value]} " : "";
                string intro = HasFormat(c)
                    ? $"{Josa.J(ai.name, "이/가")} {Josa.J(SeriesLogic.FormatNames[c.format.Value], "을/를")} 걸어왔습니다. 앞장서는 검투사는 {g.name}입니다."
                    : $"{ai.name}의 {Josa.J(g.name, "이/가")} {Josa.J(RuleName(rule), "을/를")} 걸어왔습니다.";
                StateUtil.PushMessage(state, new InboxMessage
                {
                    kind = "match",
                    title = $"{ai.name}의 도전장 ({fmt}{RuleName(rule)})",
                    body = $"{intro}\n{DateOf(c.respondBy)}까지 답하지 않으면 거절한 것으로 칩니다.\n\n받을지는 도전장 화면에서 정할 수 있습니다.",
                });
                break;
            }

            // AI 루두스끼리의 경기 (바로 치르고 결과만 반영). 대표 주간에는 쉰다
            if (OlympicaLogic.IsMatchDay(state, state.day)) return;
            var groupKeys = new List<string>();
            var groups = new Dictionary<string, List<Ludus>>();
            foreach (var l in state.ludi.Values)
            {
                if (l.isPlayer) continue;
                string key = $"{l.nation}-{l.tier}";
                if (!groups.TryGetValue(key, out var list)) { list = new List<Ludus>(); groups[key] = list; groupKeys.Add(key); }
                list.Add(l);
            }
            foreach (var key in groupKeys)
            {
                var ludi = groups[key];
                foreach (var a in ludi)
                {
                    // FM식은 정규 리그가 90일로 짧아진 만큼 자주 붙는다
                    if (!rng.Chance(L("aiMatchChancePerDay") * (FmRules.Enabled ? 1.4 : 1))) continue;
                    var opponents = ludi.Where(x => x != a).ToList();
                    var b = rng.Pick(opponents);
                    string rule = PickRule(rng, a);
                    if (rng.Chance(0.15))
                    {
                        var la = SeriesLogic.AutoLineup(state, a, 3, t);
                        var lb = SeriesLogic.AutoLineup(state, b, 3, t);
                        if (la.Count == 3 && lb.Count == 3)
                        {
                            TeamSettle(state, a, b, SeriesLogic.RunSeries(state, new[] { a, b }, new[] { la, lb }, new RunOptions { rule = rule, mode = "winner_stays", homeLudusId = b.id }, rng), rule, 3, b.id);
                            continue;
                        }
                    }
                    var ga = PickFighter(state, a, rule, t);
                    var gb = PickFighter(state, b, rule, t);
                    if (ga == null || gb == null) continue;
                    var r = Sim.Run(ga, gb, new MatchOptions
                    {
                        rule = rule,
                        seed = MatchLogic.Seed31(rng),
                        missioDeathMult = NationRules.RulesOfNation(a.nation).missioDeathMult,
                        medicalLevel = new[] { MedicalOf(state, a), MedicalOf(state, b) },
                    });
                    MatchLogic.ApplyMatchResult(state, r, new ApplyOptions { rule = rule, ranked = true, homeLudusId = b.id }, rng);
                }
            }
        }

        /// <summary>오늘 플레이어가 치러야 할 경기</summary>
        public static List<Challenge> TodaysPlayerMatches(GameState state)
        {
            int t = Now(state);
            return state.challenges
                .Where(c => c.status == "scheduled" && c.matchAt == t && (state.ludi[c.from].isPlayer || state.ludi[c.to].isPlayer))
                .ToList();
        }

        /// <summary>단체전 결과를 루두스끼리 정산한다</summary>
        public static MatchSummary TeamSettle(GameState state, Ludus a, Ludus b, Series s, string rule, int format, string homeId)
        {
            var summary = new MatchSummary();
            var (wl, ll) = s.winner == 0 ? (a, b) : (b, a);
            double crowd = s.Crowd();
            double pop;
            if (s.melee != null)
            {
                // 난전: 전원 평균 인기를 1:1 두 사람 몫으로
                pop = s.meleeSides[0].Concat(s.meleeSides[1]).Average(g => g.popularity) * 2;
            }
            else
            {
                double popSum = 0;
                foreach (var bt in s.bouts) popSum = popSum + bt.a.popularity + bt.b.popularity;
                pop = popSum / Math.Max(1, s.bouts.Count);
            }
            wl.record.wins++;
            ll.record.losses++;
            var fm = SeriesLogic.FormatMultTable[format];
            MatchLogic.Settle(state, wl, ll, rule, new SettleOptions { homeLudusId = homeId, crowd = crowd, popularity = pop, rpMult = fm.rp, moneyMult = fm.money }, summary);
            return summary;
        }

        /// <summary>예정된 경기를 치른다. 출전자가 못 나오면 대체 선수, 그마저 없으면 기권패</summary>
        public static PlayedMatch PlayChallenge(GameState state, Challenge c, Rng rng)
        {
            int t = Now(state);
            var from = state.ludi[c.from];
            var to = state.ludi[c.to];
            Gladiator Resolve(Ludus ludus, string id)
            {
                Gladiator g = !string.IsNullOrEmpty(id) && state.gladiators.TryGetValue(id, out var x) ? x : null;
                if (g != null && g.alive && Career.LudusOf(state, g.id) == ludus && g.injury?.severity != "serious") return g;
                return PickFighter(state, ludus, c.rule, t);
            }
            if (c.format.HasValue && c.format.Value > 1)
            {
                c.status = "done";
                var la = SeriesLogic.AutoLineup(state, from, c.format.Value, t, c.fromLineup ?? new List<string> { c.fromGladiator });
                var lb = SeriesLogic.AutoLineup(state, to, c.format.Value, t, c.toLineup ?? (!string.IsNullOrEmpty(c.toGladiator) ? new List<string> { c.toGladiator } : new List<string>()));
                if (la.Count == 0 || lb.Count == 0)
                {
                    var loser = la.Count == 0 ? from : to;
                    if (!FmRules.Frozen(state)) loser.rp -= 25;
                    loser.matchesThisSeason++;
                    return new PlayedMatch
                    {
                        challenge = c, result = null,
                        a = la.FirstOrDefault() ?? lb.FirstOrDefault(), b = lb.FirstOrDefault() ?? la.FirstOrDefault(),
                        forfeit = $"{loser.name} 기권 (출전 가능한 검투사가 없습니다)",
                    };
                }
                var snaps = new[] { MatchLogic.CloneGladiator(la[0]), MatchLogic.CloneGladiator(lb[0]) };
                var series = SeriesLogic.RunSeries(state, new[] { from, to }, new[] { la, lb }, new RunOptions { rule = c.rule, mode = "winner_stays", homeLudusId = to.id }, rng);
                var teamSummary = TeamSettle(state, from, to, series, c.rule, c.format.Value, to.id);
                return new PlayedMatch
                {
                    challenge = c, result = series.melee ?? (series.bouts.Count > 0 ? series.bouts[series.bouts.Count - 1].result : null),
                    a = snaps[0], b = snaps[1], summary = teamSummary, series = series,
                };
            }
            var a = Resolve(from, c.fromGladiator);
            var b = Resolve(to, c.toGladiator);
            c.status = "done";
            if (a == null || b == null)
            {
                // 기권: 나오지 못한 쪽이 RP를 잃는다
                var loser = a == null ? from : to;
                if (!FmRules.Frozen(state)) loser.rp -= 15;
                loser.matchesThisSeason++;
                return new PlayedMatch { challenge = c, result = null, a = a ?? b, b = b ?? a, forfeit = $"{loser.name} 기권 (출전 가능한 검투사가 없습니다)" };
            }
            var snapA = MatchLogic.CloneGladiator(a);
            var snapB = MatchLogic.CloneGladiator(b);
            var result = Sim.Run(a, b, new MatchOptions
            {
                rule = c.rule,
                seed = MatchLogic.Seed31(rng),
                missioDeathMult = NationRules.RulesOfNation(to.nation).missioDeathMult,
                medicalLevel = new[] { MedicalOf(state, from), MedicalOf(state, to) },
            });
            var summary = MatchLogic.ApplyMatchResult(state, result, new ApplyOptions { rule = c.rule, ranked = true, homeLudusId = to.id }, rng);
            return new PlayedMatch { challenge = c, result = result, a = snapA, b = snapB, summary = summary };
        }

        /// <summary>AI끼리 잡힌 경기는 날짜가 되면 바로 치른다</summary>
        public static void PlayAiScheduled(GameState state, Rng rng)
        {
            int t = Now(state);
            // for..of: 도는 중에 늘어난 항목도 본다 (JS 배열 순회와 같게 인덱스로)
            for (int i = 0; i < state.challenges.Count; i++)
            {
                var c = state.challenges[i];
                if (c.status != "scheduled" || c.matchAt != t) continue;
                if (state.ludi[c.from].isPlayer || state.ludi[c.to].isPlayer) continue;
                PlayChallenge(state, c, rng);
            }
        }
    }
}
