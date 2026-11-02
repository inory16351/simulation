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
            double v = Economy.MarketValue(g) * (key ? 1.5 : 1) * persona * Modifiers.ModifierMult(s, "market") * NationRules.RulesOfNation(owner.nation).marketMult;
            return JsMath.Round(v / 50) * 50;
        }

        /// <summary>이적료 제안</summary>
        public static BidResult Bid(GameState s, string gladiatorId, double fee)
        {
            var g = s.gladiators[gladiatorId];
            var owner = Career.LudusOf(s, gladiatorId);
            if (owner == null || owner.isPlayer) return new BidResult { status = "rejected", reason = "소속 루두스가 없다" };
            if (!InWindow(s)) return new BidResult { status = "rejected", reason = $"이적 시장 기간이 아니다 ({WindowLabel})" };
            if (g.deployment != null) return new BidResult { status = "rejected", reason = g.deployment.kind == "dungeon" ? "던전 원정 중이다" : "전장에 나가 있다" };
            if (fee > s.ludi[s.playerLudusId].treasury) return new BidResult { status = "rejected", reason = "금고에 그만한 돈이 없다" };
            // 중개인이 있으면 값을 조금 깎는다
            double asking = JsMath.Round(AskingPrice(s, g, owner) * StaffLogic.BuyMult(StaffLogic.RatingOf(s, s.ludi[s.playerLudusId], "agent")) / 50) * 50;
            if (fee >= asking) return new BidResult { status = "accepted", deal = MakeDeal(s, g, owner, fee) };
            if (fee >= asking * 0.7) return new BidResult { status = "countered", asking = asking };
            return new BidResult { status = "rejected", reason = $"{Josa.J(owner.name, "이/가")} 코웃음을 쳤다. 이 정도로는 안 판다" };
        }

        static Deal MakeDeal(GameState s, Gladiator g, Ludus owner, double fee)
        {
            var deal = new Deal { id = $"T{s.nextId++}", gladiatorId = g.id, fromLudus = owner.id, fee = fee, expiresAt = Now(s) + 7 };
            TransferOf(s).deals.Add(deal);
            Msg(s, "info", $"이적료 합의: {g.name}",
                $"{Josa.J(owner.name, "이/가")} {Loc(fee)} D에 동의했다.\n{Calendar.DateLabel((int)Calendar.FromAbs(deal.expiresAt).day)}까지 {Josa.J(g.name, "과/와")} 개인 계약을 맺어라.");
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

        /// <summary>이적료를 흥정한다. 받아들이면 이적료 합의(Deal)가 생긴다</summary>
        public static HaggleResult HaggleFee(GameState s, Rng rng, string gladiatorId, double fee)
        {
            var g = s.gladiators[gladiatorId];
            var owner = Career.LudusOf(s, gladiatorId);
            HaggleResult Err(string line) => new HaggleResult { status = "error", line = line };
            if (owner == null || owner.isPlayer) return Err("소속 루두스가 없다");
            if (!InWindow(s)) return Err($"이적 시장 기간이 아니다 ({WindowLabel})");
            if (g.deployment != null) return Err(g.deployment.kind == "dungeon" ? "던전 원정 중이다" : "전장에 나가 있다");
            if (fee > s.ludi[s.playerLudusId].treasury) return Err("금고에 그만한 돈이 없다");
            if (TransferOf(s).deals.Any(d => d.gladiatorId == gladiatorId)) return Err("이미 이적료에 합의했다");
            double r = StaffLogic.RatingOf(s, s.ludi[s.playerLudusId], "agent");
            double asking = JsMath.Round(AskingPrice(s, g, owner) * StaffLogic.BuyMult(r) / 50) * 50;
            string key = FeeKey(gladiatorId);
            Haggling.Open(s, rng, key, asking, 0.04, 0.18 + r * 0.005, OwnerPatience(owner) + r * 1.5);
            var res = Haggling.Offer(s, key, fee);
            if (res.Accepted) MakeDeal(s, g, owner, fee);
            return res;
        }

        /// <summary>개인 계약(주급)을 흥정한다. 받아들이면 이적이 끝나고, 협상이 깨지면 합의도 무산된다</summary>
        public static HaggleResult HaggleTransferWage(GameState s, Rng rng, string dealId, double wage, int seasons)
        {
            var t = TransferOf(s);
            var deal = t.deals.FirstOrDefault(d => d.id == dealId);
            HaggleResult Err(string line) => new HaggleResult { status = "error", line = line };
            if (deal == null) return Err("합의가 사라졌다");
            var me = s.ludi[s.playerLudusId];
            var g = s.gladiators[deal.gladiatorId];
            if (me.gladiators.Count >= Economy.RosterCapacity(me)) return Err("막사에 빈자리가 없다");
            if (me.treasury < deal.fee) return Err("이적료를 낼 돈이 없다");
            var d = TransferDemand(s, g);
            if (d.refuse) return Err($"{g.name}: {d.reason}");
            string key = DealKey(dealId);
            Haggling.Open(s, rng, key, d.wage, 0.03, 0.12);
            var res = Haggling.Offer(s, key, wage);
            if (res.status == "walked")
            {
                t.deals = t.deals.Where(x => x != deal).ToList();
                Haggling.Forget(s, key);
                res.line += $" — {Josa.J(g.name, "이/가")} 협상을 깨서 이적이 무산됐다";
            }
            if (res.Accepted) FinishTransfer(s, deal, wage, seasons);
            return res;
        }

        /// <summary>개인 계약 조건: 지금 받는 주급보다 많이, 그리고 우리 명성에 맞춰</summary>
        public static Demand TransferDemand(GameState s, Gladiator g)
        {
            double @base = Math.Max(JsMath.Round(g.wage * 1.2), GladiatorGen.ComputeWage(g, GladiatorRules.ComputeCA(g)));
            return Reputation.DemandFor(Reputation.GladiatorStanding(g), @base, Reputation.LudusReputation(s, s.ludi[s.playerLudusId]));
        }

        /// <summary>성공하면 null, 실패하면 이유</summary>
        public static string CompleteTransfer(GameState s, string dealId, double wage, int seasons, double roll)
        {
            var t = TransferOf(s);
            var deal = t.deals.FirstOrDefault(d => d.id == dealId);
            if (deal == null) return "합의가 사라졌다";
            var me = s.ludi[s.playerLudusId];
            var g = s.gladiators[deal.gladiatorId];
            var from = s.ludi[deal.fromLudus];
            if (me.gladiators.Count >= Economy.RosterCapacity(me)) return "막사에 빈자리가 없다";
            if (me.treasury < deal.fee) return "이적료를 낼 돈이 없다";
            var d = TransferDemand(s, g);
            if (d.refuse || !Reputation.AcceptsOffer(wage, d, roll))
            {
                t.deals = t.deals.Where(x => x != deal).ToList();
                return $"{Josa.J(g.name, "이/가")} 개인 조건에 동의하지 않아 이적이 무산됐다{(d.refuse ? $" ({d.reason})" : $" (원한 주급 {Num(d.wage)} D)")}";
            }
            FinishTransfer(s, deal, wage, seasons);
            return null;
        }

        static void FinishTransfer(GameState s, Deal deal, double wage, int seasons)
        {
            var t = TransferOf(s);
            var me = s.ludi[s.playerLudusId];
            var g = s.gladiators[deal.gladiatorId];
            var from = s.ludi[deal.fromLudus];
            Economy.AddMoney(s, me, -deal.fee, $"{g.name} 이적료");
            from.treasury += deal.fee;
            var gid = g.id;
            from.gladiators = from.gladiators.Where(x => x != gid).ToList();
            me.gladiators.Add(g.id);
            g.wage = wage;
            g.status = "free";
            g.contract = new GladiatorContract { untilSeason = s.season + seasons - 1, clauses = new List<string>() };
            g.morale = Math.Min(100, g.morale + 8);
            t.deals = t.deals.Where(x => x != deal).ToList();
            Career.SeasonRecord(s, g);
            Career.LogCareer(s, g, $"{from.name}에서 {Josa.J(me.name, "으로/로")} 이적했다 ({Loc(deal.fee)} D)", "join");
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
            t.offers = t.offers.Where(o => o.expiresAt >= n).ToList();
            if (!InWindow(s) || !rng.Chance(0.08)) return;
            var me = s.ludi[s.playerLudusId];
            var mine = me.gladiators.Select(id => Glad(s, id)).Where(g => g != null && g.alive && g.deployment == null).ToList();
            if (mine.Count <= 3) return;
            // 잘하는 검투사일수록 눈에 띈다
            var w = new Dictionary<string, double>();
            foreach (var g in mine) w[g.id] = Math.Max(1, GladiatorRules.ComputeCA(g) - 30) + g.popularity;
            var target = s.gladiators[rng.Weighted(w)];
            double value = Economy.MarketValue(target);
            var buyers = s.ludi.Values.Where(l => !l.isPlayer && l.nation == s.nation && l.treasury > value * 1.2).ToList();
            if (buyers.Count == 0 || t.offers.Any(o => o.gladiatorId == target.id)) return;
            var buyer = rng.Pick(buyers);
            double fee = JsMath.Round(value * rng.Range(0.8, 1.35) / 50) * 50;
            t.offers.Add(new Offer { id = $"O{s.nextId++}", fromLudus = buyer.id, gladiatorId = target.id, fee = fee, expiresAt = n + 5 });
            Msg(s, "info", $"이적 제안: {target.name}",
                $"{Josa.J(buyer.name, "이/가")} {target.name}에게 {Loc(fee)} D를 제시했다.\n이적 시장 화면에서 받을지 정하라. 5일 뒤에는 제안이 사라진다.");
        }

        /// <summary>받은 제안 수락: 검투사를 넘기고 이적료를 받는다 (중개인이 있으면 조금 더 받아 낸다)</summary>
        public static string AcceptOffer(GameState s, string offerId)
        {
            var t = TransferOf(s);
            var o = t.offers.FirstOrDefault(x => x.id == offerId);
            if (o == null) return "제안이 사라졌다";
            var me = s.ludi[s.playerLudusId];
            var buyer = s.ludi[o.fromLudus];
            var g = s.gladiators[o.gladiatorId];
            double fee = JsMath.Round(o.fee * StaffLogic.SellMult(StaffLogic.RatingOf(s, me, "agent")));
            var gid = g.id;
            me.gladiators = me.gladiators.Where(x => x != gid).ToList();
            buyer.gladiators.Add(g.id);
            buyer.treasury -= fee;
            Economy.AddMoney(s, me, fee, $"{g.name} 이적료 수입");
            t.offers = t.offers.Where(x => x != o).ToList();
            Career.LogCareer(s, g, $"{me.name}에서 {Josa.J(buyer.name, "으로/로")} 팔려 갔다 ({Loc(fee)} D)", "other");
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
