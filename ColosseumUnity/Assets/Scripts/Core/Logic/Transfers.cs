// 이적 (src/core/transfers.ts): FM처럼 먼저 루두스와 이적료를 합의하고, 그다음 검투사와 개인 계약을 맺는다.
// Deal/Offer/TransferState 타입은 StateTypes.cs에 있다.
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Colosseum.Core
{
    /// <summary>bid 결과. status: accepted(deal) | countered(asking) | rejected(reason)</summary>
    public class BidResult
    {
        public string status;
        public Deal deal;
        public double asking;
        public string reason;
    }

    public static class Transfers
    {
        static void Msg(GameState s, string kind, string title, string body) =>
            StateUtil.PushMessage(s, new InboxMessage { kind = kind, title = title, body = body });

        static string Num(double v)
        {
            if (v == Math.Floor(v) && Math.Abs(v) < 1e15) return ((long)v).ToString(CultureInfo.InvariantCulture);
            return v.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>toLocaleString()</summary>
        static string Loc(double v) => v.ToString("N0", CultureInfo.InvariantCulture);

        static Gladiator Glad(GameState s, string id) => id != null && s.gladiators.TryGetValue(id, out var g) ? g : null;

        public static TransferState TransferOf(GameState s) => s.transfer ??= new TransferState();

        static double Now(GameState s) => Calendar.AbsDay(s.season, s.day);

        /// <summary>이적 시장 기간: 서리달 1~14일, 꽃달 22일 ~ 불꽃달 7일</summary>
        public static bool InWindow(GameState s) => s.day <= 14 || (s.day >= 78 && s.day <= 91);
        public const string WindowLabel = "서리달 1~14일, 꽃달 22일 ~ 불꽃달 7일";

        /// <summary>이적 시장 형편 (Unity판 전용). 열려 있으면 닫히는 날(마지막 날)까지, 닫혀 있으면 다음에 열리는 날까지 남은 날</summary>
        public static (bool open, int days, int at) WindowState(GameState s)
        {
            int d = s.day;
            if (d <= 14) return (true, 14 - d, 14);
            if (d < 78) return (false, 78 - d, 78);
            if (d <= 91) return (true, 91 - d, 91);
            return (false, Calendar.DaysPerSeason - d + 1, 1);
        }

        /// <summary>"이적 시장 열림 · 서리달 14일까지 (3일 남음)" / "이적 시장 닫힘 · 다음 개장 꽃달 22일 (20일 뒤)"</summary>
        public static string WindowLine(GameState s)
        {
            var (open, days, at) = WindowState(s);
            if (open) return $"이적 시장 열림 · {Calendar.DateLabel(at)}까지 ({(days == 0 ? "오늘 마지막 날" : $"{days}일 남음")})";
            return $"이적 시장 닫힘 · 다음 개장 {(at == 1 ? "다음 시즌 " : "")}{Calendar.DateLabel(at)} ({days}일 뒤)";
        }

        /// <summary>루두스가 부르는 값: 주전일수록, 육성형 루두스일수록 비싸다</summary>
        public static double AskingPrice(GameState s, Gladiator g, Ludus owner)
        {
            var roster = owner.gladiators.Select(id => Glad(s, id)).Where(x => x != null && x.alive).OrderByDescending(x => GladiatorRules.ComputeCA(x)).ToList();
            bool key = roster.Take(3).Any(x => x.id == g.id);
            string p = owner.aiPersonality ?? "stable";
            double persona = p switch
            {
                "merchant" => 0.9,
                "developer" => 1.3,
                "aggressive" => 1.1,
                "stable" => 1,
                "grudge" => 1.15,
                _ => double.NaN, // TS: 표에 없으면 undefined → NaN
            };
            double v = Economy.MarketValue(g, s) * (key ? 1.5 : 1) * persona * Modifiers.ModifierMult(s, "market") * NationRules.RulesOfNation(owner.nation).marketMult;
            return JsMath.Round(v / 50) * 50;
        }

        /// <summary>FM식: 전쟁 중인 나라의 루두스와는 검투사를 사고팔 수 없다. 막히면 이유</summary>
        public static string WarBlock(GameState s, Ludus other)
        {
            if (!FmRules.Enabled || other == null || !WarLogic.AtWar(s, s.nation, other.nation)) return null;
            return $"{Data.Nation(other.nation).name}과 전쟁 중이라 그 나라 루두스와는 이적 거래를 할 수 없습니다";
        }

        /// <summary>이적료 제안</summary>
        public static BidResult Bid(GameState s, string gladiatorId, double fee)
        {
            var g = s.gladiators[gladiatorId];
            var owner = Career.LudusOf(s, gladiatorId);
            if (owner == null || owner.isPlayer) return new BidResult { status = "rejected", reason = "소속 루두스가 없습니다" };
            if (WarBlock(s, owner) is string war) return new BidResult { status = "rejected", reason = war };
            if (!InWindow(s)) return new BidResult { status = "rejected", reason = $"이적 시장 기간이 아닙니다 ({WindowLabel})" };
            if (g.deployment != null) return new BidResult { status = "rejected", reason = g.deployment.kind == "dungeon" ? "던전 원정 중입니다" : "전장에 나가 있습니다" };
            if (fee > s.ludi[s.playerLudusId].treasury) return new BidResult { status = "rejected", reason = "금고에 그만한 돈이 없습니다" };
            // 중개인이 있으면 값을 조금 깎는다
            double asking = JsMath.Round(AskingPrice(s, g, owner) * StaffLogic.BuyMult(StaffLogic.AgentNego(s, s.ludi[s.playerLudusId])) / 50) * 50;
            if (fee >= asking) return new BidResult { status = "accepted", deal = MakeDeal(s, g, owner, fee) };
            if (fee >= asking * 0.7) return new BidResult { status = "countered", asking = asking };
            return new BidResult { status = "rejected", reason = $"{Josa.J(owner.name, "이/가")} 코웃음을 칩니다. \"이 값에는 못 팝니다.\"" };
        }

        internal static Deal MakeDeal(GameState s, Gladiator g, Ludus owner, double fee)
        {
            var deal = new Deal { id = $"T{s.nextId++}", gladiatorId = g.id, fromLudus = owner.id, fee = fee, expiresAt = Now(s) + 7 };
            TransferOf(s).deals.Add(deal);
            Msg(s, "info", $"이적료 합의: {g.name}",
                $"{Josa.J(owner.name, "이/가")} 이적료 {Loc(fee)} D를 받아들였습니다.\n{Calendar.DateLabel((int)Calendar.FromAbs(deal.expiresAt).day)}까지 {Josa.J(g.name, "과/와")} 개인 계약을 맺어야 합니다.");
            return deal;
        }

        // ── 흥정 (Unity판 전용) ──

        /// <summary>이적료 흥정에서 루두스 성향별 인내심</summary>
        static double OwnerPatience(Ludus owner) => (owner.aiPersonality ?? "stable") switch
        {
            "merchant" => 130, "developer" => 80, "grudge" => 70, "aggressive" => 90, _ => 100,
        };

        public static string FeeKey(string gladiatorId) => "fee:" + gladiatorId;
        public static string DealKey(string dealId) => "deal:" + dealId;

        /// <summary>이적료 제안을 보낸다. 답은 며칠 뒤 서판으로 오고, 받아들이면 이적료 합의(Deal)가 생긴다</summary>
        public static HaggleResult HaggleFee(GameState s, Rng rng, string gladiatorId, double fee)
        {
            var err = CheckFee(s, gladiatorId, fee);
            if (err != null) return new HaggleResult { status = "error", line = err };
            string key = FeeKey(gladiatorId);
            if (Haggling.IsLocked(s, key)) return new HaggleResult { status = "locked", line = Haggling.LockedLine(s, key) };
            return Negotiations.Send(s, rng, "fee", gladiatorId, null, fee, 0);
        }

        /// <summary>이적료 제안을 낼 수 있는가. 되면 null</summary>
        static string CheckFee(GameState s, string gladiatorId, double fee)
        {
            var g = s.gladiators[gladiatorId];
            var owner = Career.LudusOf(s, gladiatorId);
            if (owner == null || owner.isPlayer) return "소속 루두스가 없습니다";
            if (WarBlock(s, owner) is string war) return war;
            if (!InWindow(s)) return WindowLine(s);
            if (g.deployment != null) return g.deployment.kind == "dungeon" ? "던전 원정 중입니다" : "전장에 나가 있습니다";
            if (fee > s.ludi[s.playerLudusId].treasury) return "금고에 그만한 돈이 없습니다";
            if (TransferOf(s).deals.Any(d => d.gladiatorId == gladiatorId)) return "이미 이적료에 합의했습니다";
            return null;
        }

        /// <summary>그 루두스가 우리에게 부르는 값 (중개인이 있으면 조금 깎는다)</summary>
        public static double AskingFor(GameState s, Gladiator g, Ludus owner) =>
            JsMath.Round(AskingPrice(s, g, owner) * StaffLogic.BuyMult(StaffLogic.AgentNego(s, s.ludi[s.playerLudusId])) / 50) * 50;

        /// <summary>이적료 흥정을 열어 둔다 (답이 올 때). 마지노선은 경쟁 판정에도 쓴다</summary>
        internal static Haggle OpenFee(GameState s, Rng rng, Gladiator g, Ludus owner)
        {
            double r = StaffLogic.AgentNego(s, s.ludi[s.playerLudusId]);
            return Haggling.Open(s, rng, FeeKey(g.id), AskingFor(s, g, owner), 0.04, 0.18 + r * 0.005, OwnerPatience(owner) + r * 1.5);
        }

        /// <summary>보낸 이적료 제안에 답이 올 때 (Negotiations.Daily). 이적 시장 기간에 보낸 제안이면 기간이 끝나도 답은 온다</summary>
        internal static HaggleResult ResolveFee(GameState s, Rng rng, string gladiatorId, double fee)
        {
            var g = s.gladiators[gladiatorId];
            var owner = Career.LudusOf(s, gladiatorId);
            HaggleResult Err(string line) => new HaggleResult { status = "error", line = line };
            if (owner == null || owner.isPlayer) return Err("이미 소속이 바뀌었습니다");
            if (WarBlock(s, owner) is string war) return Err(war + ". 제안은 무효가 됐습니다");
            if (fee > s.ludi[s.playerLudusId].treasury) return Err("금고에 그만한 돈이 없어 제안이 무효가 됐습니다");
            if (TransferOf(s).deals.Any(d => d.gladiatorId == gladiatorId)) return Err("이미 이적료에 합의했습니다");
            OpenFee(s, rng, g, owner);
            var res = Haggling.Offer(s, FeeKey(gladiatorId), fee);
            if (res.Accepted) MakeDeal(s, g, owner, fee);
            return res;
        }

        /// <summary>개인 계약(주급) 제안을 보낸다. 답은 하루이틀 뒤 서판으로 온다</summary>
        public static HaggleResult HaggleTransferWage(GameState s, Rng rng, string dealId, double wage, int seasons) =>
            HaggleTransferTerms(s, rng, dealId, new ContractTerms { wage = wage, seasons = seasons });

        /// <summary>개인 계약 제안을 조항째 보낸다 (협상 창)</summary>
        public static HaggleResult HaggleTransferTerms(GameState s, Rng rng, string dealId, ContractTerms t)
        {
            double wage = t.wage;
            int seasons = t.seasons;
            var err = CheckTerms(s, dealId);
            if (err != null) return new HaggleResult { status = "error", line = err };
            string key = DealKey(dealId);
            if (Haggling.IsLocked(s, key)) return new HaggleResult { status = "locked", line = Haggling.LockedLine(s, key) };
            var deal = TransferOf(s).deals.First(d => d.id == dealId);
            if (s.ludi[s.playerLudusId].treasury < deal.fee + t.signingFee) return new HaggleResult { status = "error", line = "이적료와 계약금을 함께 낼 돈이 없습니다" };
            return Negotiations.Send(s, rng, "terms", deal.gladiatorId, dealId, wage, seasons, t);
        }

        static string CheckTerms(GameState s, string dealId)
        {
            var deal = TransferOf(s).deals.FirstOrDefault(d => d.id == dealId);
            if (deal == null) return "합의가 사라졌습니다";
            var me = s.ludi[s.playerLudusId];
            var g = s.gladiators[deal.gladiatorId];
            if (WarBlock(s, s.ludi[deal.fromLudus]) is string war) return war;
            if (me.gladiators.Count >= Economy.RosterCapacity(me)) return "막사에 빈자리가 없습니다";
            if (me.treasury < deal.fee) return "이적료를 낼 돈이 없습니다";
            var d = TransferDemand(s, g);
            if (d.refuse) return $"{g.name}: {d.reason}";
            return null;
        }

        /// <summary>보낸 개인 계약 제안에 답이 올 때. 받아들이면 이적이 끝나고, 협상이 깨지면 합의도 무산된다</summary>
        internal static HaggleResult ResolveTerms(GameState s, Rng rng, string dealId, ContractTerms terms)
        {
            double wage = terms.wage;
            var err = CheckTerms(s, dealId);
            if (err != null) return new HaggleResult { status = "error", line = err };
            var t = TransferOf(s);
            var deal = t.deals.First(x => x.id == dealId);
            var g = s.gladiators[deal.gladiatorId];
            var d = TransferDemand(s, g);
            string key = DealKey(dealId);
            HaggleResult res;
            if (FmRules.Enabled) res = Contracts.Resolve(s, rng, key, g, terms, d, s.ludi[s.playerLudusId]);
            else
            {
                Haggling.Open(s, rng, key, d.wage, 0.03, 0.12);
                res = Haggling.Offer(s, key, wage);
            }
            if (res.status == "walked")
            {
                t.deals = t.deals.Where(x => x != deal).ToList();
                Haggling.Forget(s, key);
                res.line += $" — {Josa.J(g.name, "이/가")} 협상을 깨서 이적이 무산됐습니다";
            }
            if (res.Accepted) FinishTransfer(s, deal, terms);
            return res;
        }

        /// <summary>옮길 때 바라는 주급의 바탕: 지금 주급보다 조금 더 (FM식은 몸값 모델의 주급과 견준다)</summary>
        public static double TransferBaseWage(GameState s, Gladiator g) => FmRules.Enabled
            ? Math.Max(JsMath.Round(g.wage * 1.1), Valuation.FairWage(s, g))
            : Math.Max(JsMath.Round(g.wage * 1.2), GladiatorGen.ComputeWage(g, GladiatorRules.ComputeCA(g)));

        /// <summary>개인 계약 조건: 지금 받는 주급보다 많이, 그리고 우리 명성에 맞춰</summary>
        public static Demand TransferDemand(GameState s, Gladiator g)
        {
            return Reputation.DemandFor(Reputation.GladiatorStanding(g), TransferBaseWage(s, g), Reputation.LudusReputation(s, s.ludi[s.playerLudusId]));
        }

        /// <summary>성공하면 null, 실패하면 이유</summary>
        public static string CompleteTransfer(GameState s, string dealId, double wage, int seasons, double roll)
        {
            var t = TransferOf(s);
            var deal = t.deals.FirstOrDefault(d => d.id == dealId);
            if (deal == null) return "합의가 사라졌습니다";
            var me = s.ludi[s.playerLudusId];
            var g = s.gladiators[deal.gladiatorId];
            var from = s.ludi[deal.fromLudus];
            if (me.gladiators.Count >= Economy.RosterCapacity(me)) return "막사에 빈자리가 없습니다";
            if (me.treasury < deal.fee) return "이적료를 낼 돈이 없습니다";
            var d = TransferDemand(s, g);
            if (d.refuse || !Reputation.AcceptsOffer(wage, d, roll))
            {
                t.deals = t.deals.Where(x => x != deal).ToList();
                return $"{Josa.J(g.name, "이/가")} 계약 조건을 거절해 이적이 무산됐습니다{(d.refuse ? $" ({d.reason})" : $" (바라던 주급 {Num(d.wage)} D)")}";
            }
            FinishTransfer(s, deal, new ContractTerms { wage = wage, seasons = seasons });
            return null;
        }

        static void FinishTransfer(GameState s, Deal deal, ContractTerms terms)
        {
            var t = TransferOf(s);
            var me = s.ludi[s.playerLudusId];
            var g = s.gladiators[deal.gladiatorId];
            var from = s.ludi[deal.fromLudus];
            Economy.AddMoney(s, me, -deal.fee, $"{g.name} 이적료");
            if (terms.signingFee > 0) Economy.AddMoney(s, me, -terms.signingFee, $"{g.name} 계약금");
            from.treasury += deal.fee;
            var gid = g.id;
            from.gladiators = from.gladiators.Where(x => x != gid).ToList();
            me.gladiators.Add(g.id);
            g.status = "free";
            Contracts.Apply(s, g, terms);
            g.morale = Math.Min(100, g.morale + 8);
            t.deals = t.deals.Where(x => x != deal).ToList();
            Career.SeasonRecord(s, g);
            Career.LogCareer(s, g, $"{from.name}에서 {Josa.J(me.name, "으로/로")} 이적했습니다 ({Loc(deal.fee)} D)", "join");
        }

        public static void CancelDeal(GameState s, string dealId)
        {
            var t = TransferOf(s);
            t.deals = t.deals.Where(d => d.id != dealId).ToList();
        }

        /// <summary>매일: 기한 지난 합의·제안 정리, 이적 시장 기간에는 AI가 우리 검투사에 제안을 보낸다</summary>
        public static void DailyTransfers(GameState s, Rng rng)
        {
            var t = TransferOf(s);
            double n = Now(s);
            t.deals = t.deals.Where(d => d.expiresAt >= n).ToList();
            if (FmRules.Enabled) { DailyOffersFm(s, rng); return; }
            t.offers = t.offers.Where(o => o.expiresAt >= n).ToList();
            if (!InWindow(s) || !rng.Chance(0.08)) return;
            var me = s.ludi[s.playerLudusId];
            var mine = me.gladiators.Select(id => Glad(s, id)).Where(g => g != null && g.alive && g.deployment == null).ToList();
            if (mine.Count <= 3) return;
            // 잘하는 검투사일수록 눈에 띈다
            var w = new Dictionary<string, double>();
            foreach (var g in mine) w[g.id] = Math.Max(1, GladiatorRules.ComputeCA(g) - 30) + g.popularity;
            var target = s.gladiators[rng.Weighted(w)];
            double value = Economy.MarketValue(target, s);
            var buyers = s.ludi.Values.Where(l => !l.isPlayer && l.nation == s.nation && l.treasury > value * 1.2).ToList();
            if (buyers.Count == 0 || t.offers.Any(o => o.gladiatorId == target.id)) return;
            var buyer = rng.Pick(buyers);
            double fee = JsMath.Round(value * rng.Range(0.8, 1.35) / 50) * 50;
            t.offers.Add(new Offer { id = $"O{s.nextId++}", fromLudus = buyer.id, gladiatorId = target.id, fee = fee, expiresAt = n + 5 });
            Msg(s, "info", $"이적 제안: {target.name}",
                $"{Josa.J(buyer.name, "이/가")} {target.name} 이적료로 {Loc(fee)} D를 제시했습니다.\n받을지는 이적 시장 화면에서 정할 수 있습니다. 5일 뒤에는 제안이 사라집니다.");
        }

        // ── FM식 받은 제안: 결정 전엔 다음 날로 못 감, 역제안, 이적 명단, 최소 이적료 조항 ──

        /// <summary>우리가 답해야 하는 받은 제안 (FM식)</summary>
        public static List<Offer> OpenOffers(GameState s) =>
            FmRules.Enabled ? TransferOf(s).offers.Where(o => o.status == null || o.status == "open").ToList() : new List<Offer>();

        public static bool Listed(GameState s, string gladiatorId) => s.transferList != null && s.transferList.ContainsKey(gladiatorId);

        /// <summary>이적 명단에 올린다. 희망 이적료를 걸어 두면 이적 시장 기간에 제안이 자주 온다. 본인은 서운해한다</summary>
        public static string ListForTransfer(GameState s, string gladiatorId, double price)
        {
            var me = s.ludi[s.playerLudusId];
            if (!me.gladiators.Contains(gladiatorId)) return "우리 검투사가 아닙니다";
            if (price <= 0) return "희망 이적료를 적어야 합니다";
            var g = s.gladiators[gladiatorId];
            bool was = Listed(s, gladiatorId);
            (s.transferList ??= new Dictionary<string, double>())[gladiatorId] = JsMath.Round(price / 50) * 50;
            if (!was)
            {
                // 떠나고 싶던 자는 반기고, 아니면 서운해한다
                g.morale = g.morale < 40 ? Math.Min(100, g.morale + 5) : Math.Max(0, g.morale - 6);
                Career.LogCareer(s, g, $"{me.name}의 이적 명단에 올랐습니다", "other");
            }
            return null;
        }

        public static void Unlist(GameState s, string gladiatorId)
        {
            if (s.transferList == null) return;
            s.transferList.Remove(gladiatorId);
            if (s.transferList.Count == 0) s.transferList = null;
        }

        /// <summary>매일 (FM식): 역제안 답장, 새 제안 (이적 시장 기간에만), 최소 이적료 조항</summary>
        static void DailyOffersFm(GameState s, Rng rng)
        {
            var t = TransferOf(s);
            double n = Now(s);
            var me = s.ludi[s.playerLudusId];
            // 떠난·죽은 검투사의 제안·명단은 지운다
            t.offers = t.offers.Where(o => me.gladiators.Contains(o.gladiatorId) && s.gladiators[o.gladiatorId].alive && s.ludi.ContainsKey(o.fromLudus)).ToList();
            if (s.transferList != null)
                foreach (var id in s.transferList.Keys.ToList()) if (!me.gladiators.Contains(id)) Unlist(s, id);

            // 1. 우리 역제안에 대한 답
            foreach (var o in t.offers.Where(x => x.status == "waiting" && x.replyAt <= n).ToList())
            {
                var g = s.gladiators[o.gladiatorId];
                var buyer = s.ludi[o.fromLudus];
                double ask = o.ask ?? o.fee, max = Math.Min(o.maxFee ?? o.fee, buyer.treasury);
                if (ask <= max)
                {
                    o.fee = ask;
                    Sell(s, o);
                    Msg(s, "info", $"이적 성사: {g.name}", $"{Josa.J(buyer.name, "이/가")} 우리가 부른 {Loc(ask)} D를 받아들였습니다. {Josa.J(g.name, "은/는")} {Josa.J(buyer.name, "으로/로")} 떠났습니다.");
                    continue;
                }
                int round = (o.round ?? 0) + 1;
                double raise = JsMath.Round((o.fee + (Math.Min(ask, max) - o.fee) * rng.Range(0.45, 0.9)) / 50) * 50;
                if (round <= 3 && raise > o.fee && rng.Chance(0.75))
                {
                    o.fee = raise;
                    o.round = round;
                    o.status = "open";
                    o.ask = null;
                    o.replyAt = null;
                    Msg(s, "decision", $"이적 제안 수정: {g.name}",
                        $"{Josa.J(buyer.name, "이/가")} 우리가 부른 {Loc(ask)} D는 너무 비싸다며, {Loc(raise)} D로 올려 다시 제안했습니다.\n\n받거나, 거절하거나, 다시 값을 불러야 다음 날로 넘어갈 수 있습니다.");
                }
                else
                {
                    t.offers.Remove(o);
                    Msg(s, "info", $"이적 제안 철회: {g.name}", $"{Josa.J(buyer.name, "이/가")} 우리가 부른 {Loc(ask)} D는 낼 수 없다며 제안을 거둬들였습니다.");
                }
            }

            if (!InWindow(s)) return;
            var mine = me.gladiators.Select(id => Glad(s, id)).Where(g => g != null && g.alive && g.deployment == null).ToList();

            // 2. 최소 이적료 조항: 몸값이 조항 값에 가까우면 누군가 그 값을 낸다. 막을 수 없다
            foreach (var g in mine.ToList())
            {
                if (!(g.contract?.releaseClause is double clause) || clause <= 0) continue;
                double value = Economy.MarketValue(g, s);
                if (value * 1.25 < clause || !rng.Chance(0.03 + Math.Min(0.1, (value * 1.25 - clause) / clause * 0.2))) continue;
                var buyer = Buyers(s, g, clause).FirstOrDefault();
                if (buyer == null) continue;
                var o = new Offer { id = $"O{s.nextId++}", fromLudus = buyer.id, gladiatorId = g.id, fee = clause, expiresAt = n, clause = true };
                t.offers.Add(o);
                Sell(s, o);
                Msg(s, "warning", $"최소 이적료 조항 발동: {g.name}",
                    $"{Josa.J(buyer.name, "이/가")} 계약서의 최소 이적료 {Loc(clause)} D를 그대로 냈습니다. 조항에 따라 막을 수 없었습니다.\n{Josa.J(g.name, "은/는")} {Josa.J(buyer.name, "으로/로")} 떠났습니다.");
                mine.Remove(g);
            }

            // 3. 새 제안: 이적 명단에 오른 자는 자주, 아니면 가끔 (잘하고 이름난 자일수록)
            if (mine.Count <= 3) return;
            foreach (var g in mine)
            {
                if (t.offers.Any(o => o.gladiatorId == g.id)) continue;
                bool listed = Listed(s, g.id);
                double value = Economy.MarketValue(g, s);
                double network = StaffLogic.AttrOf(s, me, "agent", "network");
                double chance = (listed ? 0.14 : 0.006 + Math.Min(0.02, Reputation.GladiatorStanding(g) / 3000)) * (1 + network * 0.03);
                if (!rng.Chance(chance)) continue;
                double fee, max;
                if (listed)
                {
                    double asking = s.transferList[g.id];
                    // 터무니없이 비싸게 걸어 두면 잘 안 온다
                    if (asking > value * 1.6 && !rng.Chance(0.25)) continue;
                    fee = Math.Min(asking, value * 1.3) * rng.Range(0.65, 0.95);
                    max = Math.Min(asking * rng.Range(0.95, 1.1), value * 1.5);
                }
                else
                {
                    fee = value * rng.Range(0.75, 1.1);
                    max = value * rng.Range(1.05, 1.45);
                }
                fee = JsMath.Round(fee / 50) * 50;
                var buyer = Buyers(s, g, fee).FirstOrDefault();
                if (buyer == null || fee <= 0) continue;
                max = Math.Max(fee, JsMath.Round(Math.Min(max, buyer.treasury * 0.9) / 50) * 50);
                t.offers.Add(new Offer { id = $"O{s.nextId++}", fromLudus = buyer.id, gladiatorId = g.id, fee = fee, expiresAt = n + 30, status = "open", maxFee = max, round = 0 });
                Msg(s, "decision", $"이적 제안: {g.name}",
                    $"{Josa.J(buyer.name, "이/가")} {g.name} 이적료로 {Loc(fee)} D를 제시했습니다." + (listed ? $" (이적 명단 희망가 {Loc(s.transferList[g.id])} D)" : "") +
                    "\n\n받거나, 거절하거나, 값을 다시 불러야(역제안) 다음 날로 넘어갈 수 있습니다.");
                break; // 하루에 한 통
            }
        }

        /// <summary>이 값을 낼 만한 루두스 (같은 나라, 돈이 있고, 검투사 격에 맞는 리그 순)</summary>
        static List<Ludus> Buyers(GameState s, Gladiator g, double fee)
        {
            var me = s.ludi[s.playerLudusId];
            double st = Reputation.GladiatorStanding(g);
            return s.ludi.Values.Where(l => !l.isPlayer && l.nation == s.nation && l.treasury > fee * 1.1 && l.gladiators.Count < 12)
                .OrderBy(l => Math.Abs(Reputation.LudusReputation(s, l) - st) + (l.tier - me.tier) * 2)
                .Take(4).ToList();
        }

        /// <summary>받은 제안에 값을 다시 부른다 (역제안). 답은 다음 날 온다</summary>
        public static string CounterOffer(GameState s, string offerId, double ask)
        {
            var o = TransferOf(s).offers.FirstOrDefault(x => x.id == offerId);
            if (o == null) return "제안이 사라졌습니다";
            if (o.clause == true) return "최소 이적료 조항에 따른 제안은 막을 수 없습니다";
            ask = JsMath.Round(ask / 50) * 50;
            if (ask <= o.fee) return AcceptOffer(s, offerId);
            o.status = "waiting";
            o.ask = ask;
            o.replyAt = Now(s) + 1;
            return null;
        }

        /// <summary>제안대로 판다 (FM식 공용: 수락·역제안 수락·조항 발동)</summary>
        static void Sell(GameState s, Offer o)
        {
            var t = TransferOf(s);
            var me = s.ludi[s.playerLudusId];
            var buyer = s.ludi[o.fromLudus];
            var g = s.gladiators[o.gladiatorId];
            double fee = JsMath.Round(o.fee * StaffLogic.SellMult(StaffLogic.AgentNego(s, me)));
            var gid = g.id;
            me.gladiators = me.gladiators.Where(x => x != gid).ToList();
            buyer.gladiators.Add(g.id);
            buyer.treasury -= fee;
            Economy.AddMoney(s, me, fee, $"{g.name} 이적료 수입");
            t.offers = t.offers.Where(x => x.gladiatorId != gid).ToList();
            Unlist(s, gid);
            g.contract = new GladiatorContract { untilSeason = s.season + 2, clauses = new List<string>() };
            Career.SeasonRecord(s, g);
            Career.LogCareer(s, g, $"{me.name}에서 {Josa.J(buyer.name, "으로/로")} 팔려 갔습니다 ({Loc(fee)} D)", "other");
            foreach (var id in me.gladiators) s.gladiators[id].morale = Math.Max(0, s.gladiators[id].morale - (g.popularity > 40 ? 4 : 1));
        }

        /// <summary>받은 제안 수락: 검투사를 넘기고 이적료를 받는다 (중개인이 있으면 조금 더 받아 낸다)</summary>
        public static string AcceptOffer(GameState s, string offerId)
        {
            var t = TransferOf(s);
            var o = t.offers.FirstOrDefault(x => x.id == offerId);
            if (o == null) return "제안이 사라졌습니다";
            if (FmRules.Enabled) { Sell(s, o); return null; }
            var me = s.ludi[s.playerLudusId];
            var buyer = s.ludi[o.fromLudus];
            var g = s.gladiators[o.gladiatorId];
            double fee = JsMath.Round(o.fee * StaffLogic.SellMult(StaffLogic.AgentNego(s, me)));
            var gid = g.id;
            me.gladiators = me.gladiators.Where(x => x != gid).ToList();
            buyer.gladiators.Add(g.id);
            buyer.treasury -= fee;
            Economy.AddMoney(s, me, fee, $"{g.name} 이적료 수입");
            t.offers = t.offers.Where(x => x != o).ToList();
            Career.LogCareer(s, g, $"{me.name}에서 {Josa.J(buyer.name, "으로/로")} 팔려 갔습니다 ({Loc(fee)} D)", "other");
            foreach (var id in me.gladiators) s.gladiators[id].morale = Math.Max(0, s.gladiators[id].morale - (g.popularity > 40 ? 4 : 1));
            return null;
        }

        public static void RejectOffer(GameState s, string offerId)
        {
            var t = TransferOf(s);
            t.offers = t.offers.Where(x => x.id != offerId).ToList();
        }

        public static string NationName(string id) => Data.Nation(id).@short;
    }
}
