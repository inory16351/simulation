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
            if (FmRules.Enabled)
            {
                int offers = Transfers.OpenOffers(state).Count;
                if (offers > 0) o.Add($"답할 이적 제안 {offers}건");
                int decisions = state.decisions?.Count ?? 0;
                if (decisions > 0) o.Add($"답할 협상 {decisions}건");
                if (NatCupLogic.TodaysPlayerTie(state) != null) o.Add($"오늘 {NatCupLogic.Name(state.nation)} 경기");
                if (OlympicaLogic.Current(state)?.offer == true) o.Add("대표팀 감독 제안");
                if (OlympicaLogic.TodaysPlayerTie(state) != null) o.Add($"오늘 {OlympicaLogic.Name} 경기");
            }
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
            Training.Today = at;
            var player = state.ludi[state.playerLudusId];
            // FM식: 모든 루두스에 유소년 학당이 있다 (옛 저장)
            if (FmRules.Enabled) foreach (var l in state.ludi.Values) if (l.facilities.academy == 0) l.facilities.academy = 1;
            // FM식은 노예·자유민 구분이 없다: 옛 저장의 노예는 계약 검투사로 (주급은 받을 만한 값의 80%, 1~2시즌)
            if (FmRules.Enabled)
                foreach (var g in state.gladiators.Values)
                {
                    if (g.status != "slave") continue;
                    g.status = "free";
                    if (!g.alive) continue;
                    g.wage = Math.Max(g.wage, JsMath.Round(Valuation.FairWage(state, g) * 0.8));
                    g.contract ??= new GladiatorContract { untilSeason = state.season + rng.Int(0, 1), clauses = new List<string>() };
                }
            // 등록 명단 정리 (떠난 사람 빼고, 새로 온 사람은 자리가 있으면 등록)
            Registration.Daily(state);
            // 유소년 입단 테스트 마감
            if (FmRules.Enabled) Youth.DailyTrial(state);

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
            if (FmRules.Enabled && (state.day == 120 || state.day == 150))
            {
                var exp = Contracts.Expiring(state);
                if (exp.Count > 0)
                    Msg(state, "warning", $"계약 만료가 다가옵니다 ({exp.Count}명)",
                        $"이번 시즌이 끝나면 계약이 끝나는 검투사: {string.Join(", ", exp.Select(g => g.name))}.\n\n시즌이 끝나기 전에 검투사 상세 화면의 '재계약'으로 협상하지 않으면 무소속으로 떠납니다.");
            }
            if (state.day == 15) Msg(state, "info", "정규 기간 시작", "오늘부터 도전장을 주고받을 수 있습니다. 시즌 동안 최소 8경기는 치러야 합니다.");
            // FM처럼 유소년 입단 예고(4주 전)와 시즌 중반 입단 테스트 (첫 시즌도 이날 처음 열린다)
            if (FmRules.Enabled && state.day == Youth.PreviewDay) Youth.Preview(state, rng, player);
            if (FmRules.Enabled && state.day == Youth.TrialDay) Youth.Trial(state, rng, player);

            // 2. 급료일 (무소속 명단도 이때 조금씩 바뀐다)
            if (Calendar.IsPayday(state.day))
            {
                foreach (var l in state.ludi.Values.ToList()) Economy.Payday(state, l);
                Economy.WeeklyFreeAgents(state, rng);
                FreeAgency.Weekly(state, rng);
                GearLogic.RefreshGearMarket(state, rng, state.gearMarket == null || state.gearMarket.Count == 0);
                DungeonLogic.WeeklyQuests(state, rng);
                DungeonLogic.WeeklyRotation(state, rng);
                // 기술 습득 판정 (기술 테이블, 주 1회)
                SkillBook.WeeklyLearn(state, rng);
            }

            // 3. 훈련과 회복 (모든 루두스)
            foreach (var l in state.ludi.Values.ToList())
            {
                // FM식: 우리 독토레의 '동기 부여'가 사기를 매일 조금씩 움직인다 (10이 기준)
                double motiv = FmRules.Enabled && l.isPlayer && StaffLogic.StaffOf(state, l, "doctore") is Staff doc ? (StaffLogic.Attr(doc, "motiv") - 10) * 0.02 : 0;
                // JS for-of는 처음 잡은 배열을 길이를 매번 보며 훑는다
                var ids = l.gladiators;
                for (int i = 0; i < ids.Count; i++)
                {
                    var g = state.gladiators[ids[i]];
                    if (!g.alive) continue;
                    // 전장에 나간 검투사는 루두스에서 훈련하지 않는다
                    if (g.deployment != null) continue;
                    if (!l.isPlayer) AiTrainingPlan(g);
                    if (motiv != 0) g.morale = Math.Max(0, Math.Min(100, g.morale + motiv));
                    string injured = Training.DailyTraining(g, rng, l.facilities.training, NationRules.RulesOfNation(l.nation).growthMult * StaffLogic.CoachMultFor(state, l, g.training?.focus), StaffLogic.InjuryMultOf(state, l));
                    var healed = Condition.DailyCondition(g, at, l.facilities.medical, l.facilities.bath, StaffLogic.HealMultOf(state, l));
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
            if (FmRules.Enabled && state.day == FmRules.RegularEnd + 1) LeagueEnd(state);
            ChallengeLogic.DailyChallenges(state, rng);
            OlympicaLogic.Daily(state, rng);
            NatCupLogic.Daily(state, rng);
            CupLogic.DailyCup(state, rng);
            WarLogic.DailyWar(state, rng);
            DungeonLogic.DailyDungeons(state, rng);
            // 답장은 합의 기한 정리보다 먼저: 기한 안에 보낸 제안은 답을 받는다
            Negotiations.Daily(state, rng);
            Grievances.Daily(state, rng);
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
            double aiRoster = FmRules.AiRoster;
            foreach (var l in state.ludi.Values.ToList())
            {
                if (l.isPlayer) continue;
                while (l.gladiators.Count < aiRoster)
                {
                    var g = GladiatorGen.Generate(rng, new GenOptions { id = $"G{state.nextId++}", nation = l.nation, target = (FmRules.Enabled ? FmRules.RosterTarget(l.tier) : l.tier == 1 ? 10.5 : 7.5) + rng.Range(-1.5, 1.5), usedNames = used });
                    state.gladiators[g.id] = g;
                    l.gladiators.Add(g.id);
                    // FM식: 새로 뽑힌 검투사도 그 부에서 받을 만한 주급 (웹판 셈은 아래 부에 너무 비싸다)
                    if (FmRules.Enabled) g.wage = Valuation.FairWage(state, g);
                }
            }
        }

        static List<Gladiator> AliveOf(GameState state, Ludus l) =>
            l.gladiators.Select(id => state.gladiators[id]).Where(g => g.alive).ToList();

        /// <summary>FM식 시즌 끝: AI 루두스 자유민의 계약 만료. 아끼는 주전(능력 상위)은 대개 재계약하고,
        /// 루두스보다 격이 높거나 불만인 검투사는 떠나기 쉽다. 떠난 검투사 id를 돌려준다 (무소속 명단에 넣을 것)</summary>
        static List<string> AiContracts(GameState state, Rng rng)
        {
            var gone = new List<string>();
            foreach (var l in state.ludi.Values)
            {
                if (l.isPlayer) continue;
                double rep = Reputation.LudusReputation(state, l);
                var ranked = AliveOf(state, l).OrderByDescending(g => GladiatorRules.ComputeCA(g)).ToList();
                for (int rank = 0; rank < ranked.Count; rank++)
                {
                    var g = ranked[rank];
                    if (g.status == "slave") continue;
                    // 계약서 없는 자유민(새로 뽑힌 검투사·옛 저장)은 1~3시즌 계약을 준다
                    if (g.contract == null) { g.contract = new GladiatorContract { untilSeason = state.season + rng.Int(0, 2), clauses = new List<string>() }; continue; }
                    if (g.contract.untilSeason >= state.season) continue;
                    bool wanted = rank < 3 || (rank < 6 && GladiatorRules.AgeStage(g) != "decline");
                    double leave = (Reputation.GladiatorStanding(g) > rep + 12 ? 0.45 : 0.1) + (g.grievance != null ? 0.3 : 0) - g.hidden.loyalty / 100.0;
                    double newWage = Math.Max(g.wage, JsMath.Round(Valuation.FairWage(state, g) * rng.Range(0.9, 1.05)));
                    // 주급 예산: 올려 줄 여유가 없으면 지금 주급으로만 붙잡는다 (주전 셋은 대개 받아들이고, 나머지는 떠나기 쉽다). 지금 주급도 버거우면 주전 셋 말고는 내보낸다
                    if (wanted && !Economy.Affords(state, l, newWage - g.wage))
                    {
                        newWage = g.wage;
                        bool core = rank < 3;
                        if (!Economy.Affords(state, l, 0) && !core) wanted = false;
                        else leave += core ? 0.15 : 0.4;
                    }
                    if (wanted && !rng.Chance(Math.Max(0, leave)))
                    {
                        g.contract = new GladiatorContract { untilSeason = state.season + Contracts.PreferredSeasons(g) - 1, clauses = new List<string>() };
                        g.wage = newWage;
                        if (g.grievanceKind == "raise") { g.grievance = null; g.grievanceKind = null; g.grievanceUntil = null; }
                        continue;
                    }
                    l.gladiators = l.gladiators.Where(x => x != g.id).ToList();
                    g.contract = null;
                    g.grievance = null; g.grievanceKind = null; g.grievanceUntil = null;
                    Career.LogCareer(state, g, $"계약이 끝나 {Josa.J(l.name, "을/를")} 떠났습니다", "other");
                    gone.Add(g.id);
                }
            }
            return gone;
        }

        /// <summary>FM식: 독토레의 '규율'이 탈주·반란을 누른다 (규율 20이면 절반)</summary>
        static double DisciplineMult(GameState state, Ludus l) =>
            FmRules.Enabled ? 1 - StaffLogic.AttrOf(state, l, "doctore", "disc") * 0.025 : 1;

        /// <summary>탈주와 반란 (docs/05_economy.md)</summary>
        public static void Unrest(GameState state, Rng rng, Ludus player)
        {
            double U(string k) => (double)Bal.Unrest[k];
            var roster = AliveOf(state, player);
            if (roster.Count == 0) return;

            foreach (var g in roster)
            {
                if (g.unpaidWeeks < 4 || g.traits.Contains("loyal_hound")) continue;
                double p = (g.unpaidWeeks - 3) * U("desertBase") * (1 - g.hidden.loyalty / 25.0) * (1 - player.facilities.guard * 0.1) * DisciplineMult(state, player);
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
            chance *= DisciplineMult(state, player);
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

        /// <summary>FM식: 정규 리그 종료. 최소 경기 수 페널티를 매기고 순위를 굳힌다 (승강과 판테온 컵 출전은 이 순위로)</summary>
        static void LeagueEnd(GameState state)
        {
            foreach (var l in state.ludi.Values)
            {
                double missing = Math.Max(0, L("minMatches") - l.matchesThisSeason);
                if (missing != 0) l.rp -= missing * L("minMatchPenalty");
            }
            var me = state.ludi[state.playerLudusId];
            var table = Economy.TierRanking(state, me.tier, me.nation);
            int rank = table.FindIndex(l => l.isPlayer) + 1;
            string pantheon = me.tier == 1 && rank <= 2 ? "\n1부 2위 안에 들어 판테온 컵 본선에 바로 나갑니다." : me.tier == 1 && rank == 3 ? "\n1부 3위입니다. 1·2위 루두스가 국내 컵까지 우승하면 판테온 컵 플레이오프에 나갑니다." : "";
            Msg(state, "info", $"정규 리그 종료 — {me.tier}부 {rank}위",
                $"이번 시즌 정규 리그가 끝나 순위가 정해졌습니다. {me.tier}부 {rank}위로, 승강과 시즌 배당은 이 순위를 따릅니다.{pantheon}\n\n이제 국내 컵과 판테온 컵이 이어집니다. 컵에서 일찍 떨어지거나 나가지 않으면 던전 원정이나 훈련에 시간을 쓸 수 있습니다.");
            Awards.LeagueEnd(state);
        }

        /// <summary>시즌 종료: 승강, 페널티, 나이, RP 회귀, 배당금</summary>
        public static void EndSeason(GameState state, Rng rng)
        {
            var player = state.ludi[state.playerLudusId];
            // 시즌 시상 (나라별·세계 올해의 검투사)은 기록을 닫기 전에
            Awards.SeasonEnd(state);
            foreach (var g in state.gladiators.Values.ToList()) if (g.alive) Career.CloseSeason(state, g);

            // 최소 경기 수 페널티 (FM식은 정규 리그가 끝날 때 이미 매겼다)
            foreach (var l in state.ludi.Values.ToList())
            {
                if (FmRules.Enabled) break;
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
            List<Ludus> promoted, relegated;
            Dictionary<string, int> finalRank;
            var playedTier = new Dictionary<string, int>();
            if (!FmRules.Enabled)
            {
                var t2 = Economy.TierRanking(state, 2);
                int promote = (int)L("promote"), relegate = (int)L("relegate");
                promoted = t2.Take(promote).ToList();
                // slice(-n)
                relegated = t1.Skip(Math.Max(0, t1.Count - relegate)).ToList();
                finalRank = new Dictionary<string, int>();
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
            }
            else
            {
                // FM식: 모든 나라 1~4부. 부마다 위 2개는 한 부 올라가고 아래 2개는 한 부 내려간다
                promoted = new List<Ludus>();
                relegated = new List<Ludus>();
                finalRank = new Dictionary<string, int>();
                int promote = (int)L("promote"), relegate = (int)L("relegate");
                foreach (var n in Data.Nations)
                {
                    var tables = new List<Ludus>[FmRules.MaxTier + 2];
                    for (int t = 1; t <= FmRules.MaxTier; t++)
                    {
                        tables[t] = Economy.TierRanking(state, t, n.id);
                        for (int i = 0; i < tables[t].Count; i++) finalRank[tables[t][i].id] = i + 1;
                    }
                    for (int t = 1; t < FmRules.MaxTier; t++)
                    {
                        if (tables[t].Count == 0 || tables[t + 1].Count == 0) continue;
                        relegated.AddRange(tables[t].Skip(Math.Max(0, tables[t].Count - relegate)));
                        promoted.AddRange(tables[t + 1].Take(promote));
                    }
                }
                Honours.RecordSeason(state, promoted, relegated);
                int oldTier = player.tier;
                foreach (var l in state.ludi.Values) playedTier[l.id] = l.tier;
                var upFromMine = promoted.Where(l => l.nation == state.nation && l.tier == oldTier).ToList();
                var downFromMine = relegated.Where(l => l.nation == state.nation && l.tier == oldTier).ToList();
                foreach (var l in promoted) { l.tier--; l.rp = Math.Max(l.rp, FmRules.RpBase(l.tier) - 80); }
                foreach (var l in relegated) { l.tier++; l.rp = Math.Min(l.rp, FmRules.RpBase(l.tier) + 80); }
                int myRank = finalRank[player.id];
                bool up = promoted.Contains(player), down = relegated.Contains(player);
                Msg(state, "info",
                    $"시즌 {state.season} 종료 — {(up ? $"{player.tier}부로 승격!" : down ? $"{player.tier}부로 강등" : $"{oldTier}부 {myRank}위")}",
                    $"{Josa.J(player.name, "은/는")} {oldTier}부 {myRank}위로 시즌을 마쳤습니다." +
                    (up ? $"\n다음 시즌은 {player.tier}부에서 싸웁니다." : down ? $"\n다음 시즌은 {player.tier}부로 내려갑니다." : "") +
                    $"\n\n{oldTier}부에서 올라간 루두스: {(upFromMine.Count > 0 ? string.Join(", ", upFromMine.Select(l => l.name)) : "없음")}" +
                    $"\n{oldTier}부에서 내려간 루두스: {(downFromMine.Count > 0 ? string.Join(", ", downFromMine.Select(l => l.name)) : "없음")}" +
                    "\n\n모든 나라의 순위와 승강 기록은 서열표와 비석의 언덕에서 볼 수 있습니다.");
                Contracts.SeasonEnd(state, up, down);
            }

            CupLogic.CloseCupSeason(state);
            state.season++;
            state.day = 1;
            foreach (var l in state.ludi.Values.ToList())
            {
                double @base = FmRules.Enabled ? FmRules.RpBase(l.tier) : l.tier == 1 ? L("rpTier1") : L("rpTier2");
                l.rp = JsMath.Round(l.rp - (l.rp - @base) * L("seasonRegression"));
                l.matchesThisSeason = 0;
                l.refusalsUsed = 0;
                // 다른 나라 루두스는 자기 나라 순위대로 배당을 받는다
                double? coef;
                if (FmRules.Enabled)
                {
                    // FM식: 배당은 뛴 리그와 그 순위에 비례한다 (승격·강등과 무관)
                    int r = finalRank[l.id];
                    int tp = playedTier[l.id];
                    double paid = Economy.PayDividend(state, l, FmRules.DividendCoef(r, (int)L("teamsPerTier")), tp);
                    if (l.isPlayer) Msg(state, "info", $"시즌 배당금 {Loc(paid)} D", $"지난 시즌 {tp}부 {r}위 몫으로 배당금 {Loc(paid)} D를 받았습니다. 순위가 높고 리그가 높을수록 많이 받습니다.");
                    continue;
                }
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
            // FM식: 계약이 끝나 나간 검투사는 무소속 명단을 새로 만든 뒤에 넣는다 (RefreshFreeAgents가 지우지 않게)
            var released = new List<string>();
            {
                var me = state.ludi[state.playerLudusId];
                foreach (var id in me.gladiators.ToList())
                {
                    state.gladiators.TryGetValue(id, out var g);
                    if (g?.contract == null || g.contract.untilSeason >= state.season) continue;
                    if (FmRules.Enabled)
                    {
                        // FM식: 재계약하지 않은 자유민은 계약이 끝나면 무소속으로 떠난다
                        me.gladiators = me.gladiators.Where(x => x != id).ToList();
                        g.contract = null;
                        released.Add(id);
                        Career.LogCareer(state, g, $"계약이 끝나 {Josa.J(me.name, "을/를")} 떠났습니다", "other");
                        Msg(state, "warning", $"{g.name} 떠남", $"계약이 끝났는데 재계약하지 않아 무소속이 됐습니다. 다시 데려오려면 이적 시장의 무소속 명단에서 협상해야 합니다.");
                        continue;
                    }
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
            if (FmRules.Enabled) released.AddRange(AiContracts(state, rng));
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
            if (released.Count > 0)
            {
                state.freeAgents.AddRange(released.Where(id => !state.freeAgents.Contains(id)));
                // 우리 나라에서 풀린 쓸 만한 검투사를 알려 준다 (FM 계약 만료 선수 목록처럼)
                var notable = released.Select(id => state.gladiators[id]).Where(g => g.alive && g.nation == state.nation && !state.ludi[state.playerLudusId].gladiators.Contains(g.id))
                    .OrderByDescending(g => GladiatorRules.ComputeCA(g)).Take(5).ToList();
                if (notable.Count > 0)
                    Msg(state, "info", $"계약 만료로 풀린 검투사 {released.Count}명",
                        $"재계약하지 않아 무소속이 된 검투사들이 이적 시장 무소속 명단에 올랐습니다. 우리 나라에서 눈에 띄는 검투사:\n" +
                        string.Join("\n", notable.Select(g => $"· {g.name} ({g.age}세, {Data.Style(g.style)?.name})")) +
                        "\n\n이적료 없이 주급 협상만으로 데려올 수 있습니다.");
            }
            ReplenishAi(state, rng);
        }
    }
}
