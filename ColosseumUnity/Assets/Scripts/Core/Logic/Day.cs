// 하루 진행 (src/core/day.ts, docs/10_tech.md 일일 처리 순서)
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Colosseum.Core
{
    public static class Day
    {
        static void Msg(GameState s, string kind, string title, string body) =>
            StateUtil.PushMessage(s, new InboxMessage { kind = kind, title = title, body = body });

        /// <summary>JS 숫자 → 문자열 (`${n}`)</summary>
        static string Num(double v)
        {
            if (v == Math.Floor(v) && Math.Abs(v) < 1e15) return ((long)v).ToString(CultureInfo.InvariantCulture);
            return v.ToString("R", CultureInfo.InvariantCulture);
        }

        static string Num(double? v) => v.HasValue ? Num(v.Value) : "undefined";

        /// <summary>toLocaleString()</summary>
        static string Loc(double v) => v.ToString("N0", CultureInfo.InvariantCulture);

        static double L(string k) => (double)Bal.League[k];

        public static bool InRegularPeriod(GameState state) => ChallengeLogic.InRegularPeriod(state);

        /// <summary>다음 날로 넘어가기 전에 플레이어가 처리해야 할 일</summary>
        public static List<string> Blockers(GameState state)
        {
            var o = new List<string>();
            if (!string.IsNullOrEmpty(state.gameOver)) o.Add(state.gameOver);
            if (state.pendingEvents.Count > 0) o.Add($"결정할 사건 {state.pendingEvents.Count}건");
            var matches = ChallengeLogic.TodaysPlayerMatches(state);
            if (matches.Count > 0) o.Add($"오늘 치를 경기 {matches.Count}건");
            if (CupLogic.TodaysPlayerTie(state) != null) o.Add("오늘 판테온 컵 경기");
            return o;
        }

        /// <summary>하루를 진행한다. 막힌 일이 있으면 진행하지 않고 그 이유를 돌려준다</summary>
        public static List<string> AdvanceDay(GameState state)
        {
            var blocked = Blockers(state);
            if (blocked.Count > 0) return blocked;
            var rng = new Rng(state.rngState);

            if (state.day >= Calendar.DaysPerSeason) EndSeason(state, rng);
            else state.day++;
            var at = Calendar.AbsDay(state.season, state.day);
            var player = state.ludi[state.playerLudusId];

            // 1. 아침: 이벤트, 시장
            Events.RollDailyEvents(state, rng);
            // 학당을 처음 지으면 바로 첫 입학생을 받는다
            foreach (var d in FacilitiesLogic.DailyConstruction(state))
                if (d.facility == "academy" && d.level == 1) Youth.Intake(state, rng, d.ludus);
            if (state.day == 78)
            {
                Economy.RefreshFreeAgents(state, rng);
                StaffLogic.RefreshStaffMarket(state, rng);
                ReplenishAi(state, rng);
                Msg(state, "info", "이적 시장이 열렸습니다", "앞으로 14일 동안 다른 루두스와 검투사를 사고팔 수 있습니다. 무소속 검투사 명단도 새로 바뀌었습니다.");
            }
            if (state.day == 15) Msg(state, "info", "정규 기간 시작", "오늘부터 도전장을 주고받을 수 있습니다. 시즌 동안 최소 8경기는 치러야 합니다.");

            // 2. 급료일 (무소속 명단도 이때 조금씩 바뀐다)
            if (Calendar.IsPayday(state.day))
            {
                foreach (var l in state.ludi.Values.ToList()) Economy.Payday(state, l);
                Economy.WeeklyFreeAgents(state, rng);
                GearLogic.RefreshGearMarket(state, rng, state.gearMarket == null || state.gearMarket.Count == 0);
                DungeonLogic.WeeklyQuests(state, rng);
                // 기술 습득 판정 (기술 테이블, 주 1회)
                SkillBook.WeeklyLearn(state, rng);
            }

            // 3. 훈련과 회복 (모든 루두스)
            foreach (var l in state.ludi.Values.ToList())
            {
                // JS for-of는 처음 잡은 배열을 길이를 매번 보며 훑는다
                var ids = l.gladiators;
                for (int i = 0; i < ids.Count; i++)
                {
                    var g = state.gladiators[ids[i]];
                    if (!g.alive) continue;
                    // 전장에 나간 검투사는 루두스에서 훈련하지 않는다
                    if (g.deployment != null) continue;
                    if (!l.isPlayer) AiTrainingPlan(g);
                    string injured = Training.DailyTraining(g, rng, l.facilities.training, NationRules.RulesOfNation(l.nation).growthMult * StaffLogic.CoachMult(StaffLogic.RatingOf(state, l, "doctore")));
                    var healed = Condition.DailyCondition(g, at, l.facilities.medical, l.facilities.bath, StaffLogic.HealMult(StaffLogic.RatingOf(state, l, "medicus")));
                    if (healed != null)
                    {
                        g.injury = healed;
                        Injuries.OnHealed(state, g, rng, l.facilities.medical, StaffLogic.RatingOf(state, l, "medicus"), l.isPlayer);
                        g.injury = null;
                        if (l.isPlayer && healed.severity == "serious") Msg(state, "info", $"{g.name} 복귀", $"부상({healed.desc})이 다 나았습니다. 다시 싸울 수 있습니다.");
                    }
                    if (!string.IsNullOrEmpty(injured) && l.isPlayer)
                    {
                        Career.SeasonRecord(state, g).injuries++;
                        Career.LogCareer(state, g, $"훈련 중 다쳤습니다: {injured}", "injury");
                        Msg(state, "warning", $"훈련 중 부상: {g.name}", $"{injured}. {Num(g.injury?.daysLeft)}일 정도 쉬어야 합니다.");
                    }
                }
            }

            // 유소년: 매일 훈련, 28일마다 견습 투기
            foreach (var l in state.ludi.Values.ToList())
            {
                if (l.youth == null || l.youth.Count == 0) continue;
                Youth.DailyYouth(state, rng, l);
                if (Youth.IsApprenticeDay(state) && l.isPlayer) Youth.ApprenticeBouts(state, rng, l);
            }

            // 4. 도전과 경기
            ChallengeLogic.DailyChallenges(state, rng);
            CupLogic.DailyCup(state, rng);
            WarLogic.DailyWar(state, rng);
            DungeonLogic.DailyDungeons(state, rng);
            // 답장은 합의 기한 정리보다 먼저: 기한 안에 보낸 제안은 답을 받는다
            Negotiations.Daily(state, rng);
            Transfers.DailyTransfers(state, rng);
            GearLogic.DailyConsignments(state, rng);
            Haggling.Prune(state);
            Scouting.DailyScouting(state);
            ChallengeLogic.PlayAiScheduled(state, rng);

            // 5. 급료일 다음 날: 탈주와 반란
            if (state.day % 7 == 1 && state.day > 1) Unrest(state, rng, player);

            // 6. 파산
            if (player.treasury < 0)
            {
                player.bankruptSince ??= at;
                double days = at - player.bankruptSince.Value;
                double bankruptDays = (double)Bal.Economy["bankruptDays"];
                if (days >= bankruptDays) state.gameOver = "파산: 채권자들이 루두스를 압류했습니다.";
                else if (days == 0) Msg(state, "warning", "금고가 바닥났습니다", $"{Num(bankruptDays)}일 안에 빚을 갚지 못하면 루두스를 압류당합니다. 검투사를 팔거나 경기로 돈을 벌어야 합니다.");
            }
            else player.bankruptSince = null;

            state.rngState = rng.State;
            return new List<string>();
        }

        /// <summary>AI 검투사의 훈련 계획: 지치면 쉬고 아니면 스타일에 맞게</summary>
        public static void AiTrainingPlan(Gladiator g)
        {
            if (g.fatigue > 60 || g.wear > 60) g.training = new TrainingPlan { focus = "rest", intensity = "normal" };
            else g.training = new TrainingPlan { focus = g.age % 2 != 0 ? "weapon" : "physical", intensity = "normal" };
        }

        /// <summary>AI 루두스 인원 보충 (시장 갱신 때)</summary>
        public static void ReplenishAi(GameState state, Rng rng)
        {
            var used = new HashSet<string>(state.gladiators.Values.Select(g => g.name));
            double aiRoster = (double)Bal.Economy["aiRoster"];
            foreach (var l in state.ludi.Values.ToList())
            {
                if (l.isPlayer) continue;
                while (l.gladiators.Count < aiRoster)
                {
                    var g = GladiatorGen.Generate(rng, new GenOptions { id = $"G{state.nextId++}", nation = l.nation, target = (l.tier == 1 ? 10.5 : 7.5) + rng.Range(-1.5, 1.5), usedNames = used });
                    state.gladiators[g.id] = g;
                    l.gladiators.Add(g.id);
                }
            }
        }

        static List<Gladiator> AliveOf(GameState state, Ludus l) =>
            l.gladiators.Select(id => state.gladiators[id]).Where(g => g.alive).ToList();

        /// <summary>탈주와 반란 (docs/05_economy.md)</summary>
        public static void Unrest(GameState state, Rng rng, Ludus player)
        {
            double U(string k) => (double)Bal.Unrest[k];
            var roster = AliveOf(state, player);
            if (roster.Count == 0) return;

            foreach (var g in roster)
            {
                if (g.unpaidWeeks < 4 || g.traits.Contains("loyal_hound")) continue;
                double p = (g.unpaidWeeks - 3) * U("desertBase") * (1 - g.hidden.loyalty / 25.0) * (1 - player.facilities.guard * 0.1);
                if (rng.Chance(p))
                {
                    var gid = g.id;
                    player.gladiators = player.gladiators.Where(x => x != gid).ToList();
                    Career.LogCareer(state, g, $"급료가 {Num(g.unpaidWeeks)}번 밀리자 {player.name}에서 달아났습니다", "other");
                    Msg(state, "warning", $"{Josa.J(g.name, "이/가")} 달아났습니다", $"급료가 {Num(g.unpaidWeeks)}번 밀리자 밤사이 담을 넘었습니다.");
                }
            }

            var left = AliveOf(state, player);
            double maxUnpaid = 0;
            foreach (var g in left) maxUnpaid = Math.Max(maxUnpaid, g.unpaidWeeks);
            if (left.Count == 0 || maxUnpaid == 0) return;
            double moraleSum = 0;
            foreach (var g in left) moraleSum += g.morale;
            double avgMorale = moraleSum / left.Count;
            double chance = (100 - avgMorale) * maxUnpaid * U("revoltPerMoralePerWeek");
            chance += left.Count(g => g.traits.Contains("agitator") && g.morale <= 30) * U("agitatorBonus");
            chance -= player.facilities.guard * U("guardReduction");
            chance -= left.Count(g => g.traits.Contains("loyal_hound") && g.morale >= 70) * U("loyalReduction");
            if (!rng.Chance(Math.Max(0, chance))) return;

            // 반란: 사기 낮은 자들과 나머지+경비대가 맞붙는다
            var rebels = left.Where(g => g.morale <= 30 && !g.traits.Contains("loyal_hound")).ToList();
            if (rebels.Count == 0) return;
            var loyal = left.Where(g => !rebels.Contains(g)).ToList();
            double Power(List<Gladiator> gs) { double s = 0; foreach (var g in gs) s += GladiatorRules.ComputeCA(g); return s; }
            double guardPower = player.facilities.guard * 2 * 60;
            double rebelPower = Power(rebels) * rng.Range(0.8, 1.2);
            double loyalPower = (Power(loyal) + guardPower) * rng.Range(0.8, 1.2);
            if (rebelPower > loyalPower)
            {
                state.gameOver = $"반란: {string.Join(", ", rebels.Select(g => g.name))} 일당이 루두스를 장악했습니다.";
                Msg(state, "death", "반란 — 루두스를 빼앗겼습니다", state.gameOver);
                return;
            }
            // 진압 성공: 반란자 중 일부가 죽고 금고가 털린다
            var dead = new List<string>();
            foreach (var g in rebels)
            {
                if (rng.Chance(0.3))
                {
                    g.alive = false;
                    g.diedAt = new DiedAt { season = state.season, day = state.day, cause = "반란 진압 중 사망" };
                    var gid = g.id;
                    player.gladiators = player.gladiators.Where(x => x != gid).ToList();
                    state.fallen.Add(g.id);
                    Career.LogCareer(state, g, "반란을 일으켰다가 진압 중 죽었습니다", "death");
                    dead.Add(g.name);
                }
                else
                {
                    g.morale = Math.Max(0, g.morale - 10);
                    Career.LogCareer(state, g, "반란에 가담했다가 진압당했습니다", "other");
                }
            }
            double lost = JsMath.Round(Math.Max(0, player.treasury) * 0.1);
            player.treasury -= lost;
            Msg(state, "death", "반란을 진압했습니다",
                $"{Josa.J(string.Join(", ", rebels.Select(g => g.name)), "이/가")} 들고일어났지만 진압됐습니다.{(dead.Count > 0 ? $"\n죽은 자: {string.Join(", ", dead)}" : "")}\n금고에서 {Num(lost)} D를 잃었습니다.");
        }

        /// <summary>시즌 종료: 승강, 페널티, 나이, RP 회귀, 배당금</summary>
        public static void EndSeason(GameState state, Rng rng)
        {
            var player = state.ludi[state.playerLudusId];
            foreach (var g in state.gladiators.Values.ToList()) if (g.alive) Career.CloseSeason(state, g);

            // 최소 경기 수 페널티
            foreach (var l in state.ludi.Values.ToList())
            {
                double missing = Math.Max(0, L("minMatches") - l.matchesThisSeason);
                if (missing != 0) l.rp -= missing * L("minMatchPenalty");
            }

            var t1 = Economy.TierRanking(state, 1);
            // 신전 후원 (케메르)
            double subsidy = NationRules.RulesOfNation(state.nation).templeSubsidy;
            if (subsidy != 0)
            {
                foreach (var l in t1.Take(3).ToList())
                {
                    l.treasury += subsidy;
                    if (l.isPlayer)
                    {
                        state.ledger.Insert(0, new LedgerEntry { at = Calendar.AbsDay(state.season, state.day), amount = subsidy, desc = "신전 후원금" });
                        Msg(state, "info", "신전이 금을 내렸습니다", $"1부 상위 루두스에 신전 후원금 {Loc(subsidy)} D가 내려왔습니다.");
                    }
                }
            }
            var t2 = Economy.TierRanking(state, 2);
            int promote = (int)L("promote"), relegate = (int)L("relegate");
            var promoted = t2.Take(promote).ToList();
            // slice(-n)
            var relegated = t1.Skip(Math.Max(0, t1.Count - relegate)).ToList();
            var finalRank = new Dictionary<string, int>();
            for (int i = 0; i < t1.Count; i++) finalRank[t1[i].id] = i + 1;
            for (int i = 0; i < t2.Count; i++) finalRank[t2[i].id] = i + 1;
            // 역대 기록 (Unity판): 승강을 반영하기 전 순위로
            Honours.RecordSeason(state, promoted, relegated);
            foreach (var l in promoted) { l.tier = 1; l.rp = Math.Max(l.rp, L("rpTier1") - 80); }
            foreach (var l in relegated) { l.tier = 2; l.rp = Math.Min(l.rp, L("rpTier2") + 80); }

            int playerRank = finalRank[player.id];
            bool wasPromoted = promoted.Contains(player);
            bool wasRelegated = relegated.Contains(player);
            Msg(state, "info",
                $"시즌 {state.season} 종료 — {(wasPromoted ? "승격!" : wasRelegated ? "강등" : $"{playerRank}위")}",
                $"{Josa.J(player.name, "은/는")} {(wasPromoted ? "2부" : wasRelegated ? "1부" : $"{player.tier}부")} {playerRank}위로 시즌을 마쳤습니다." +
                $"\n승격: {string.Join(", ", promoted.Select(l => l.name))}\n강등: {string.Join(", ", relegated.Select(l => l.name))}");

            CupLogic.CloseCupSeason(state);
            state.season++;
            state.day = 1;
            foreach (var l in state.ludi.Values.ToList())
            {
                double @base = l.tier == 1 ? L("rpTier1") : L("rpTier2");
                l.rp = JsMath.Round(l.rp - (l.rp - @base) * L("seasonRegression"));
                l.matchesThisSeason = 0;
                l.refusalsUsed = 0;
                // 다른 나라 루두스는 자기 나라 순위대로 배당을 받는다
                double? coef;
                if (!finalRank.TryGetValue(l.id, out int rank)) coef = null;
                else if (promoted.Contains(l)) coef = (double)Bal.Economy["promotedDividendCoef"];
                else if (relegated.Contains(l)) coef = (double)Bal.Economy["relegatedDividendCoef"];
                else coef = 1.5 - rank / L("teamsPerTier");
                Economy.PayDividend(state, l, coef);
            }
            foreach (var g in state.gladiators.Values.ToList())
            {
                if (!g.alive) continue;
                g.age++;
                Career.SeasonRecord(state, g);
            }
            // 자유민 계약 만료: 사기가 괜찮으면 주급을 올려 1시즌 재계약, 아니면 떠난다
            {
                var me = state.ludi[state.playerLudusId];
                foreach (var id in me.gladiators.ToList())
                {
                    state.gladiators.TryGetValue(id, out var g);
                    if (g?.contract == null || g.contract.untilSeason >= state.season) continue;
                    if (g.morale >= 50)
                    {
                        g.wage = JsMath.Round(g.wage * 1.1);
                        g.contract.untilSeason = state.season;
                        Msg(state, "info", $"{g.name} 재계약", $"계약이 끝났지만 1시즌 더 남기로 했습니다. 주급은 {Num(g.wage)} D로 올랐습니다.");
                    }
                    else
                    {
                        me.gladiators = me.gladiators.Where(x => x != id).ToList();
                        Career.LogCareer(state, g, $"계약이 끝나 {Josa.J(me.name, "을/를")} 떠났습니다", "other");
                        Msg(state, "warning", $"{g.name} 떠남", "계약이 끝났습니다. 사기가 떨어져 재계약을 거절했습니다.");
                    }
                }
            }
            // 유소년: 졸업 안내, 새 입학생
            foreach (var l in state.ludi.Values.ToList())
            {
                if (l.youth == null) l.youth = new List<string>();
                Youth.SeasonEndYouth(state, l);
                Youth.Intake(state, rng, l);
            }
            state.challenges = state.challenges.Where(c => c.status == "pending" || c.status == "scheduled").ToList();
            foreach (var c in state.challenges) c.status = "cancelled";
            state.challenges = new List<Challenge>();
            Economy.RefreshFreeAgents(state, rng);
            ReplenishAi(state, rng);
        }
    }
}
