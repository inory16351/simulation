// 금고: 배당금, 급료일, 장부, 시장 가격 (src/core/economy.ts)
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Colosseum.Core
{
    public static class Economy
    {
        static JObject E => Bal.Economy;

        /// <summary>금고 입출금. 플레이어 루두스는 장부에 남긴다</summary>
        public static void AddMoney(GameState state, Ludus ludus, double amount, string desc)
        {
            if (amount == 0) return;
            // 수입에는 세금이 붙는다
            double net = amount > 0 ? JsMath.Round(amount * (1 - NationRules.RulesOfNation(ludus.nation).taxRate)) : amount;
            ludus.treasury += net;
            if (ludus.isPlayer)
            {
                state.ledger.Insert(0, new LedgerEntry { at = Calendar.AbsDay(state.season, state.day), amount = net, desc = amount > 0 ? $"{desc} (세금 공제)" : desc });
                if (state.ledger.Count > 300) state.ledger.RemoveAt(state.ledger.Count - 1);
            }
        }

        /// <summary>한 나라 한 리그의 RP 순위 (나라를 안 주면 플레이어 나라)</summary>
        public static List<Ludus> TierRanking(GameState state, int tier, string nation = null)
        {
            nation ??= state.nation;
            return state.ludi.Values.Where(l => l.tier == tier && l.nation == nation).OrderByDescending(l => l.rp).ToList();
        }

        /// <summary>시즌 배당금 지급. coef가 없으면 현재 순위로 계산</summary>
        public static double PayDividend(GameState state, Ludus ludus, double? coef = null, int? playedTier = null)
        {
            int rank = TierRanking(state, ludus.tier, ludus.nation).FindIndex(l => l.id == ludus.id) + 1;
            double teams = FmRules.TeamsPerTier;
            double c = coef ?? (FmRules.Enabled ? FmRules.DividendCoef(rank, (int)teams) : 1.5 - rank / teams);
            double amount = JsMath.Round((FmRules.Enabled ? FmRules.TierMoney("dividendBaseFm", playedTier ?? ludus.tier) : (double)E["dividendBase"][ludus.tier.ToString()]) * c);
            ludus.treasury += amount;
            if (ludus.isPlayer)
                state.ledger.Insert(0, new LedgerEntry { at = Calendar.AbsDay(state.season, state.day), amount = amount, desc = $"시즌 {state.season} 배당금" });
            return amount;
        }

        static IEnumerable<string> RosterIds(Ludus ludus)
        {
            foreach (var id in ludus.gladiators) yield return id;
            if (ludus.youth != null) foreach (var id in ludus.youth) yield return id;
        }

        public static double WeeklyWages(GameState state, Ludus ludus)
        {
            double s = 0;
            foreach (var id in RosterIds(ludus))
                s = s + (state.gladiators.TryGetValue(id, out var g) && g != null ? g.wage : 0);
            return s;
        }

        public static double WeeklyFood(GameState state, Ludus ludus)
        {
            double s = 0;
            double food = (double)E["foodPerWeek"];
            foreach (var id in ludus.gladiators)
                s = s + food * Data.Race(state.gladiators[id].race).upkeepMult;
            return s;
        }

        /// <summary>급료일에 주급을 받는 검투사. 용병 파견 중인 검투사의 주급은 고용국이 낸다 (던전 원정은 루두스가 낸다)</summary>
        static List<Gladiator> PayRoster(GameState state, Ludus ludus)
        {
            var roster = new List<Gladiator>();
            foreach (var id in RosterIds(ludus))
                if (state.gladiators.TryGetValue(id, out var g) && g != null && g.alive && (g.deployment == null || g.deployment.kind == "dungeon")) roster.Add(g);
            return roster;
        }

        /// <summary>다음 급료일에 나갈 돈 (Unity판 전용). Payday와 같은 계산이라 그때까지 상태가 그대로면 꼭 이만큼 나간다</summary>
        public class PayBill
        {
            public double wages, arrears, food, upkeep, staff;
            /// <summary>그날 먼저 들어오는 하부 리그 지원금 (세금 뺀 값)</summary>
            public double grant;
            /// <summary>밀린 급료를 뺀, 매주 나가는 돈</summary>
            public double Weekly => wages + food + upkeep + staff;
            public double Total => Weekly + arrears;
            /// <summary>금고에서 실제로 줄어드는 돈 (나갈 돈 − 지원금)</summary>
            public double Net => Total - grant;
        }

        public static PayBill NextPayBill(GameState state, Ludus ludus)
        {
            var b = new PayBill
            {
                food = JsMath.Round(WeeklyFood(state, ludus) * Modifiers.ModifierMult(state, "food")),
                upkeep = FacilitiesLogic.WeeklyUpkeep(ludus),
                staff = StaffLogic.StaffWages(state, ludus),
                grant = ludus.isPlayer ? JsMath.Round((FmRules.LowerLeagueGrant(ludus.tier) + Patrons.WeeklyOf(ludus)) * (1 - NationRules.RulesOfNation(ludus.nation).taxRate)) : 0,
            };
            if (double.IsNaN(b.upkeep)) b.upkeep = 0;
            if (double.IsNaN(b.staff)) b.staff = 0;
            foreach (var g in PayRoster(state, ludus))
            {
                b.wages += g.wage;
                b.arrears += g.wage * g.unpaidWeeks;
            }
            return b;
        }

        /// <summary>FM식: AI 루두스의 주간 부수입 (후원·관중 외 수입)</summary>
        public static double AiSideIncome(int tier) => tier switch { 1 => 650, 2 => 330, 3 => 190, _ => 110 };

        /// <summary>FM식: 루두스가 한 주에 벌 거라고 보는 돈 (부수입 + 경기 상금·관중 + 시즌 배당을 주로 나눔, 세금 뺌)</summary>
        public static double ExpectedWeeklyIncome(GameState s, Ludus l)
        {
            int t = l.tier;
            // 한 경기 몫: 승패 반반이면 상금의 65%, 관중은 홈·원정 반반이면 관중 수입의 약 1/3. 세금은 경기 수입에만 붙는다
            double match = (FmRules.TierMoney("purseFm", t) * 0.65 + FmRules.TierMoney("ticketIncomeFm", t) * 0.33) * (1 - NationRules.RulesOfNation(l.nation).taxRate);
            double weeks = Calendar.DaysPerSeason / 7.0;
            // 주 2경기 몫 (BalanceSim aifin으로 잰 AI 실제 수입에 맞춤, 컵 상금·수상은 뺀 보수적인 값)
            double sponsor = l.sponsor != null ? l.sponsor.weekly : AiSideIncome(t) * (l.isPlayer ? 0 : 1);
            double side = l.isPlayer ? (FmRules.LowerLeagueGrant(t) + Patrons.WeeklyOf(l)) * (1 - NationRules.RulesOfNation(l.nation).taxRate) : sponsor;
            return side + match * 2 + FmRules.TierMoney("dividendBaseFm", t) * 1.05 / weeks;
        }

        /// <summary>FM식 주급 예산: 이 루두스가 감당할 수 있는 한 주 주급 총액 (유소년 포함).
        /// 기대 수입의 85%에서 식비·식솔 급료·시설 유지비를 빼고, 금고가 석 달치 수입보다 넉넉하면 남는 돈을 두 시즌에 나눠 더 쓴다. 금고가 비었으면 그만큼 줄인다</summary>
        public static double WageBudget(GameState s, Ludus l)
        {
            double income = ExpectedWeeklyIncome(s, l);
            // FM 세계: AI도 시설 유지비·식솔 급료를 낸다
            double fixedCost = WeeklyFood(s, l) + (l.isPlayer || FmRules.Enabled ? FacilitiesLogic.WeeklyUpkeep(l) + StaffLogic.StaffWages(s, l) : 0);
            double reserve = income * 12;
            double extra = l.treasury > reserve ? (l.treasury - reserve) / 48 : l.treasury < 0 ? l.treasury / 24 : 0;
            return Math.Max(0, JsMath.Round(income * 0.85 - fixedCost + extra));
        }

        /// <summary>새 주급(주당 추가분)을 더해도 예산 안인가. 예산을 넘으면 AI는 영입·재계약·인상을 포기한다 (웹판 규칙에선 늘 참)</summary>
        public static bool Affords(GameState s, Ludus l, double extraWeekly) =>
            !FmRules.Enabled || WeeklyWages(s, l) + extraWeekly <= WageBudget(s, l);

        /// <summary>급료일 처리. 돈이 모자라면 주급이 높은 순으로 주고, 못 받은 검투사는 미납이 쌓인다</summary>
        public static void Payday(GameState state, Ludus ludus)
        {
            var roster = PayRoster(state, ludus);
            if (!ludus.isPlayer)
            {
                // AI 루두스는 추상적으로 처리: 급료와 식비를 내고, 상금 외 부수입을 조금 번다
                // FM 세계: 정액 부수입 대신 스폰서 (Patrons, 유저와 같은 규칙)
                ludus.treasury += FmRules.Enabled ? (ludus.sponsor != null ? ludus.sponsor.weekly : AiSideIncome(ludus.tier) * 0.5) : ludus.tier == 1 ? 400 : 150;
                ludus.treasury -= WeeklyWages(state, ludus) + WeeklyFood(state, ludus);
                // FM 세계: AI도 유저와 같은 고정비 (시설 유지비·식솔 급료)
                if (FmRules.Enabled) ludus.treasury -= FacilitiesLogic.WeeklyUpkeep(ludus) + StaffLogic.StaffWages(state, ludus);
                return;
            }
            // 하부 리그 지원금을 먼저 받고 나서 낸다
            double grant = FmRules.LowerLeagueGrant(ludus.tier);
            if (grant > 0) AddMoney(state, ludus, grant, $"{FmRules.TierName(ludus.tier)} 하부 리그 지원금");
            double sponsorPay = Patrons.WeeklyOf(ludus);
            if (sponsorPay > 0) AddMoney(state, ludus, sponsorPay, $"{ludus.sponsor.name} 스폰서");
            double food = JsMath.Round(WeeklyFood(state, ludus) * Modifiers.ModifierMult(state, "food"));
            AddMoney(state, ludus, -food, "식비");
            double upkeep = FacilitiesLogic.WeeklyUpkeep(ludus);
            if (upkeep != 0 && !double.IsNaN(upkeep)) AddMoney(state, ludus, -upkeep, "시설 유지비");
            double staffPay = StaffLogic.StaffWages(state, ludus);
            if (staffPay != 0 && !double.IsNaN(staffPay)) AddMoney(state, ludus, -staffPay, "식솔 급료");
            var unpaidNames = new List<string>();
            var unpaid = (JArray)Bal.Morale["unpaid"];
            foreach (var g in roster.OrderByDescending(x => x.wage).ToList())
            {
                // 밀린 급료까지 같이 갚는다
                double owed = g.wage * (1 + g.unpaidWeeks);
                if (ludus.treasury >= owed)
                {
                    AddMoney(state, ludus, -owed, $"{g.name} 주급{(g.unpaidWeeks != 0 ? $" (밀린 {StateUtil.Num(g.unpaidWeeks)}회 포함)" : "")}");
                    if (g.unpaidWeeks > 0)
                    {
                        // 밀린 급료를 받아도 떨어진 사기는 절반만 돌아온다
                        double lost = 0;
                        int end = (int)Math.Min(g.unpaidWeeks + 1, unpaid.Count);
                        for (int i = 1; i < end; i++) lost = lost + (double)unpaid[i];
                        g.morale = Math.Min(100, g.morale + lost / 2);
                        g.unpaidWeeks = 0;
                    }
                    else g.morale = Math.Min(100, g.morale + (double)Bal.Morale["paidBonus"] + ludus.facilities.barracks * 0.5);
                }
                else
                {
                    g.unpaidWeeks++;
                    double greed = g.traits.Contains("greedy") ? 1.5 : 1;
                    double loyal = 1 - g.hidden.loyalty / 40.0;
                    double cold = g.race == "lizardfolk" ? 0.5 : 1;
                    double drop = (double)unpaid[(int)Math.Min(g.unpaidWeeks, unpaid.Count - 1)] * greed * loyal * cold;
                    g.morale = Math.Max(0, g.morale - drop);
                    unpaidNames.Add($"{g.name}({StateUtil.Num(g.unpaidWeeks)}회)");
                }
            }
            if (unpaidNames.Count > 0)
            {
                StateUtil.PushMessage(state, "warning", "급료를 다 주지 못했습니다",
                    $"금고가 바닥나 급료를 못 받은 자들이 있습니다: {string.Join(", ", unpaidNames)}.\n\n세 번 넘게 밀리면 훈련을 거부하고, 네 번째부터는 도망치거나 들고일어날 수 있습니다.");
            }
        }

        /// <summary>시장 가격</summary>
        public static double MarketValue(Gladiator g, GameState s = null)
        {
            if (FmRules.Enabled) return Valuation.MarketValue(s, g);
            double ca = GladiatorRules.ComputeCA(g);
            double potentialBonus = Math.Max(0, g.hidden.pa - ca) * 4;
            double ageMult = StageOf(g) switch { "youth" => 0.8, "growth" => 1.1, "prime" => 1.0, _ => 0.6 };
            return JsMath.Round((ca * ca * 0.12 + potentialBonus + g.popularity * 5 + 150) * ageMult / 10) * 10;
        }

        public static string StageOf(Gladiator g)
        {
            var r = Data.Race(g.race).age;
            return g.age < r.debut ? "youth" : g.age <= r.growthEnd ? "growth" : g.age <= r.primeEnd ? "prime" : "decline";
        }

        /// <summary>보유 인원 한도. FM식은 막사 제한이 없다 (주급으로만 제한, 공식 경기는 등록 15명)</summary>
        public static int RosterCapacity(Ludus ludus) => FmRules.Enabled ? int.MaxValue : FacilitiesLogic.CapacityAt(ludus.facilities.barracks);

        /// <summary>무소속 검투사를 하나 만들어 명단에 올린다</summary>
        public static void AddFreeAgent(GameState state, Rng rng, string nation, double target, HashSet<string> used, bool pow = false)
        {
            var g = GladiatorGen.Generate(rng, new GenOptions { id = $"G{state.nextId++}", nation = nation, target = target, status = "free", usedNames = used });
            g.popularity = Math.Min(80, g.popularity + rng.Int(0, 20));
            g.wage = JsMath.Round((FmRules.Enabled ? Valuation.FairWage(state, g) : GladiatorGen.ComputeWage(g, GladiatorRules.ComputeCA(g))) * (pow ? 0.5 : 1) * Modifiers.ModifierMult(state, "market") * NationRules.RulesOfNation(state.nation).marketMult);
            if (pow)
            {
                g.morale = rng.Int(15, 35);
                if (!g.traits.Contains("homesick")) g.traits.Add("homesick");
            }
            state.gladiators[g.id] = g;
            state.freeAgents.Add(g.id);
            MarkFree(state, g);
        }

        /// <summary>FM 세계: 무소속이 된 날을 적는다 (SelfValue가 무소속 기간을 잰다)</summary>
        public static void MarkFree(GameState state, Gladiator g)
        {
            if (FmRules.Enabled && g != null) g.freeSince = Calendar.AbsDay(state.season, state.day);
        }

        public static bool IsOwned(GameState state, string id) =>
            state.ludi.Values.Any(l => l.gladiators.Contains(id) || (l.youth != null && l.youth.Contains(id)));

        static HashSet<string> UsedNames(GameState state) => StateUtil.UsedNames(state);

        /// <summary>무소속 명단의 실력 기준 (우리 리그 단계에 맞춘다)</summary>
        static double FreeAgentTarget(GameState state)
        {
            int tier = state.ludi[state.playerLudusId].tier;
            if (!FmRules.Enabled) return tier == 1 ? 9 : 7;
            return FmRules.RosterTarget(tier) - 0.5;
        }

        /// <summary>무소속 검투사 명단 새로 고침 (이적 시장이 열릴 때). 실력 폭이 넓고, 협상해야 온다</summary>
        public static void RefreshFreeAgents(GameState state, Rng rng)
        {
            var del = new HashSet<string>();
            foreach (var id in state.freeAgents ?? new List<string>()) if (!IsOwned(state, id)) del.Add(id);
            StateUtil.RemoveOrdered(state.gladiators, del);
            state.freeAgents = new List<string>();
            var used = UsedNames(state);
            double b = FreeAgentTarget(state);
            // 전쟁 포로: 패전국 출신이 싼 주급에 풀린다
            if (state.powPending != null)
            {
                for (int i = 0; i < 4; i++) AddFreeAgent(state, rng, state.powPending, b + rng.Range(-1, 2), used, true);
                state.powPending = null;
            }
            for (int i = 0; i < 24; i++) AddFreeAgent(state, rng, state.nation, b + rng.Range(-3, 6), used);
        }

        /// <summary>매주: 몇 명은 다른 데로 가고 몇 명이 새로 풀린다</summary>
        public static void WeeklyFreeAgents(GameState state, Rng rng)
        {
            var used = UsedNames(state);
            for (int i = 0; i < 2 && state.freeAgents.Count > 12; i++)
            {
                int at = rng.Int(0, state.freeAgents.Count - 1);
                string id = state.freeAgents[at];
                state.freeAgents.RemoveAt(at);
                if (!IsOwned(state, id)) StateUtil.RemoveOrdered(state.gladiators, id);
            }
            double b = FreeAgentTarget(state);
            for (int i = 0; i < 2; i++) AddFreeAgent(state, rng, state.nation, b + rng.Range(-3, 6), used);
        }

        /// <summary>방출: 무소속이 된다</summary>
        public static void ReleaseGladiator(GameState state, string id)
        {
            var player = state.ludi[state.playerLudusId];
            var g = state.gladiators[id];
            player.gladiators = player.gladiators.Where(x => x != id).ToList();
            g.contract = null;
            state.freeAgents.Add(id);
            MarkFree(state, g);
            Career.LogCareer(state, g, $"{player.name}에서 방출됐습니다", "other");
        }

        /// <summary>무소속이 바라는 주급의 바탕: FM식은 옮겨 갈 루두스의 부에서 받을 만한 주급, 웹판은 지금 주급</summary>
        public static double FreeAgentBaseWage(Gladiator g, int tier) => FmRules.Enabled ? Valuation.FairWageAt(g, tier) : g.wage;

        /// <summary>우리 명성에서 이 검투사가 원하는 주급</summary>
        public static Demand FreeAgentDemand(GameState state, Gladiator g)
        {
            var me = state.ludi[state.playerLudusId];
            return SelfValue.Apply(state, g, me, Reputation.DemandFor(Reputation.GladiatorStanding(g), FreeAgentBaseWage(g, me.tier), Reputation.LudusReputation(state, me)));
        }

        /// <summary>계약금 = 제안 주급 4주치 (웹판)</summary>
        public static double SigningFee(double offer) => offer * 4;

        /// <summary>계약 조건의 계약금: FM식은 협상 창에서 정한 값, 웹판은 주급 4주치</summary>
        public static double SigningFeeOf(ContractTerms t) => FmRules.Enabled ? t.signingFee : SigningFee(t.wage);

        /// <summary>실패하면 이유, 성공하면 null</summary>
        public static string SignFreeAgent(GameState state, string id, double offer, int seasons, double roll)
        {
            var player = state.ludi[state.playerLudusId];
            state.gladiators.TryGetValue(id, out var g);
            if (!state.freeAgents.Contains(id)) return "이미 다른 곳과 계약했습니다";
            if (player.gladiators.Count >= RosterCapacity(player)) return "막사에 빈자리가 없습니다";
            var d = FreeAgentDemand(state, g);
            if (d.refuse) return $"{g.name}: {d.reason}";
            if (player.treasury < SigningFee(offer)) return "계약금이 모자랍니다";
            if (!Reputation.AcceptsOffer(offer, d, roll))
            {
                state.freeAgents = state.freeAgents.Where(x => x != id).ToList();
                return $"{Josa.J(g.name, "이/가")} 제안을 거절하고 다른 루두스로 갔습니다 (바라던 주급 {StateUtil.Num(d.wage)} D)";
            }
            JoinFreeAgent(state, id, new ContractTerms { wage = offer, seasons = seasons, signingFee = SigningFee(offer) });
            return null;
        }

        public static string WageKey(string id) => "wage:" + id;

        /// <summary>무소속 검투사와 주급 흥정 (Unity판 전용). 깨지면 한동안 상대하지 않는다</summary>
        public static HaggleResult HaggleFreeAgent(GameState state, Rng rng, string id, double offer, int seasons) =>
            HaggleFreeAgentTerms(state, rng, id, new ContractTerms { wage = offer, seasons = seasons });

        /// <summary>무소속 검투사에게 조항째 계약을 제안한다 (협상 창)</summary>
        public static HaggleResult HaggleFreeAgentTerms(GameState state, Rng rng, string id, ContractTerms t)
        {
            double offer = t.wage;
            int seasons = t.seasons;
            var player = state.ludi[state.playerLudusId];
            state.gladiators.TryGetValue(id, out var g);
            HaggleResult Err(string line) => new HaggleResult { status = "error", line = line };
            if (!state.freeAgents.Contains(id)) return Err("이미 다른 곳과 계약했습니다");
            if (Transfers.MovedThisWindow(state, g)) return Err(Transfers.MovedLine(g));
            if (player.gladiators.Count >= RosterCapacity(player)) return Err("막사에 빈자리가 없습니다");
            var d = FreeAgentDemand(state, g);
            if (d.refuse) return Err($"{g.name}: {d.reason}");
            // FM 세계: 리그 수준이 한참 아래면 협상 전에 올 마음부터 (이번 기간 고정)
            if (!SelfValue.Interested(state, g, player, offer / Math.Max(1, d.wage))) return Err(SelfValue.RefusalLine(g, player));
            if (player.treasury < SigningFeeOf(t)) return Err("계약금이 모자랍니다");
            string key = WageKey(id);
            if (Haggling.IsLocked(state, key)) return new HaggleResult { status = "locked", line = Haggling.LockedLine(state, key) };
            // 답은 바로 오지 않는다: 하루이틀 뒤 서판으로 온다 (Negotiations)
            return Negotiations.Send(state, rng, "free", id, null, offer, seasons, t);
        }

        /// <summary>보낸 주급 제안에 답이 올 때 (Negotiations.Daily)</summary>
        internal static HaggleResult ResolveFreeAgent(GameState state, Rng rng, string id, ContractTerms t)
        {
            double offer = t.wage;
            var player = state.ludi[state.playerLudusId];
            var g = state.gladiators[id];
            HaggleResult Err(string line) => new HaggleResult { status = "error", line = line };
            if (player.gladiators.Count >= RosterCapacity(player)) return Err("막사에 빈자리가 없어 계약하지 못했습니다");
            var d = FreeAgentDemand(state, g);
            if (d.refuse) return Err(d.reason);
            if (player.treasury < SigningFeeOf(t)) return Err("계약금이 모자라 계약하지 못했습니다");
            string key = WageKey(id);
            HaggleResult res;
            if (FmRules.Enabled) res = Contracts.Resolve(state, rng, key, g, t, d, player);
            else
            {
                Haggling.Open(state, rng, key, d.wage, 0.03, 0.12);
                res = Haggling.Offer(state, key, offer);
            }
            if (res.Accepted) JoinFreeAgent(state, id, t);
            return res;
        }

        static void JoinFreeAgent(GameState state, string id, ContractTerms t)
        {
            double offer = t.wage;
            int seasons = t.seasons;
            var player = state.ludi[state.playerLudusId];
            var g = state.gladiators[id];
            AddMoney(state, player, -SigningFeeOf(t), $"{g.name} 계약금");
            Contracts.Apply(state, g, t);
            g.status = "free";
            state.freeAgents = state.freeAgents.Where(x => x != id).ToList();
            player.gladiators.Add(id);
            Career.Joined(state, g, 0);
            Career.LogCareer(state, g, $"자유민으로 {Josa.J(player.name, "과/와")} {seasons}시즌 계약했습니다 (주급 {StateUtil.Num(offer)} D)", "join");
        }
    }
}
