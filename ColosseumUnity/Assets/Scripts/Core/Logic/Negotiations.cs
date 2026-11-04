// 협상 답장과 영입 경쟁 (Unity판에만 있다. 웹판에는 없음): FM처럼 제안을 보내면 답이 바로 오지 않고
// 하루~사흘 뒤 서판으로 온다. 그사이 다른 루두스가 같은 검투사를 노리고 끼어들 수 있다.
// 경쟁자가 더 높은 이적료를 부르면 그쪽에 팔리고, 검투사가 경쟁자의 조건을 더 좋게 보면 그쪽과 계약한다.
// 상대가 역제안을 하면 '결정할 일'(GameState.decisions)로 남아, 다시 제안하거나 물러설 때까지 다음 날로 갈 수 없다.
// 계약 제안은 주급만이 아니라 조항 전체(ContractTerms)를 담는다. 재계약(renew)도 같은 길로 간다.
// 보낸 제안이 없으면 난수를 쓰지 않는다 → 골든 테스트 영향 없음.
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Colosseum.Core
{
    /// <summary>답을 기다리는 제안. kind: fee(이적료) | terms(이적 개인 계약) | free(무소속 계약) | renew(재계약) | youth(학당 서약)</summary>
    public class PendingBid
    {
        public string id, kind, gladiatorId, dealId;
        public double amount, sentAt, replyAt;
        public int seasons;
        /// <summary>계약 제안의 조항 전체 (Unity판). 없으면 amount·seasons만</summary>
        public ContractTerms terms;
    }

    /// <summary>같은 검투사를 노리는 다른 루두스. ludusId가 null이면 경쟁자가 없다고 정해진 것 (until까지 다시 굴리지 않는다)</summary>
    public class RivalBid
    {
        public string ludusId;
        public double fee, wage, until;
        /// <summary>답장에서 우리에게 알려 줬는가</summary>
        public bool known;
    }

    public static class Negotiations
    {
        static double Now(GameState s) => Calendar.AbsDay(s.season, s.day);

        static string Loc(double v) => v.ToString("N0", CultureInfo.InvariantCulture);

        static void Msg(GameState s, string kind, string title, string body) => StateUtil.PushMessage(s, kind, title, body);

        /// <summary>답을 기다리는 제안. terms는 합의 id로, 나머지는 검투사 id로 찾는다</summary>
        public static PendingBid Pending(GameState s, string kind, string id) =>
            s.bids?.FirstOrDefault(b => b.kind == kind && (kind == "terms" ? b.dealId == id : b.gladiatorId == id));

        /// <summary>지금 이 검투사를 노리는 경쟁 루두스 (없으면 null)</summary>
        public static RivalBid RivalOf(GameState s, string gladiatorId) =>
            s.rivals != null && s.rivals.TryGetValue(gladiatorId, out var r) && r.until >= Now(s) && r.ludusId != null ? r : null;

        /// <summary>"내일" / "서리달 9일께"</summary>
        public static string WhenLine(GameState s, PendingBid b) =>
            b.replyAt - Now(s) <= 1 ? "내일" : $"{Calendar.DateLabel(Calendar.FromAbs(b.replyAt).day)}께";

        public static string WaitLine(GameState s, PendingBid b) => $"답을 기다리는 중입니다. {WhenLine(s, b)} 답이 옵니다";

        // ── 결정할 일 (상대의 역제안) ──

        static string KeyOf(string kind, string gladiatorId, string dealId) => kind == "terms" ? dealId : gladiatorId;

        /// <summary>이 협상에 우리가 답해야 할 역제안 (없으면 null)</summary>
        public static Decision DecisionFor(GameState s, string kind, string id) =>
            s.decisions?.FirstOrDefault(d => d.kind == kind && KeyOf(d.kind, d.gladiatorId, d.dealId) == id);

        public static Decision DecisionById(GameState s, string id) => s.decisions?.FirstOrDefault(d => d.id == id);

        static Decision AddDecision(GameState s, string kind, Gladiator g, string dealId, string line)
        {
            s.decisions ??= new List<Decision>();
            s.decisions.RemoveAll(d => d.kind == kind && KeyOf(d.kind, d.gladiatorId, d.dealId) == KeyOf(kind, g.id, dealId));
            var d = new Decision { id = $"N{s.nextId++}", kind = kind, gladiatorId = g.id, dealId = dealId, line = line, at = Now(s) };
            s.decisions.Add(d);
            return d;
        }

        static void ClearDecision(GameState s, string kind, string key)
        {
            if (s.decisions == null) return;
            s.decisions.RemoveAll(d => d.kind == kind && KeyOf(d.kind, d.gladiatorId, d.dealId) == key);
            if (s.decisions.Count == 0) s.decisions = null;
        }

        /// <summary>역제안을 받아들이지 않고 물러선다. 이적 개인 계약이면 이적료 합의도 무산된다</summary>
        public static void Withdraw(GameState s, string decisionId)
        {
            var d = DecisionById(s, decisionId);
            if (d == null) return;
            s.decisions.Remove(d);
            if (s.decisions.Count == 0) s.decisions = null;
            switch (d.kind)
            {
                case "fee": Haggling.Forget(s, Transfers.FeeKey(d.gladiatorId)); break;
                case "terms": Transfers.CancelDeal(s, d.dealId); Haggling.Forget(s, Transfers.DealKey(d.dealId)); break;
                case "free": Haggling.Forget(s, Economy.WageKey(d.gladiatorId)); break;
                case "renew": Haggling.Forget(s, Contracts.RenewKey(d.gladiatorId)); break;
                case "youth": Haggling.Forget(s, Youth.YouthKey(d.gladiatorId)); break;
            }
        }

        /// <summary>결정할 일 제목 (경고줄·서판·협상 창)</summary>
        public static string DecisionTitle(GameState s, Decision d)
        {
            string name = s.gladiators.TryGetValue(d.gladiatorId, out var g) ? g.name : "?";
            return d.kind switch
            {
                "fee" => $"{name} 이적료 역제안",
                "terms" => $"{name} 이적 계약 역제안",
                "free" => $"{name} 계약 역제안",
                "renew" => $"{name} 재계약 역제안",
                "youth" => $"{name} 학당 서약 역제안",
                _ => name,
            };
        }

        // ── 보내기 ──

        /// <summary>제안을 보낸다. 같은 상대에게 답을 기다리는 제안이 있으면 더 못 보낸다. 역제안(결정할 일)이 있었다면 이 제안이 답이 된다</summary>
        public static HaggleResult Send(GameState s, Rng rng, string kind, string gladiatorId, string dealId, double amount, int seasons, ContractTerms terms = null)
        {
            var old = Pending(s, kind, kind == "terms" ? dealId : gladiatorId);
            if (old != null) return new HaggleResult { status = "error", line = WaitLine(s, old) };
            if (amount <= 0) return new HaggleResult { status = "error", line = "제안할 값을 적어야 합니다" };
            double now = Now(s);
            // 루두스는 이적료를 두고 며칠 따져 보고, 검투사는 하루이틀이면 답한다
            int days = kind == "fee" ? rng.Int(1, 3) : rng.Int(1, 2);
            var b = new PendingBid { id = $"B{s.nextId++}", kind = kind, gladiatorId = gladiatorId, dealId = dealId, amount = amount, seasons = seasons, sentAt = now, replyAt = now + days, terms = terms?.Clone() };
            (s.bids ??= new List<PendingBid>()).Add(b);
            ClearDecision(s, kind, KeyOf(kind, gladiatorId, dealId));
            // 재계약·학당 서약에는 경쟁자가 없다 (모래 시험 응시자는 마감까지 우리만 협상한다)
            if (kind != "renew" && kind != "youth") MaybeRival(s, rng, s.gladiators[gladiatorId]);
            return new HaggleResult { status = "sent", line = $"제안을 보냈습니다. {WhenLine(s, b)} 서판으로 답이 옵니다" };
        }

        static ContractTerms TermsOf(PendingBid b) => b.terms ?? new ContractTerms { wage = b.amount, seasons = Math.Max(1, b.seasons) };

        /// <summary>개인 계약의 기준 주급 (Transfers.TransferDemand, Economy.FreeAgentDemand와 같다)</summary>
        static double BaseWage(GameState s, Gladiator g, bool free, int tier) =>
            free ? Economy.FreeAgentBaseWage(g, tier) : Transfers.TransferBaseWage(s, g, tier);

        /// <summary>처음 제안할 때 경쟁자가 붙을지 정한다. 이름난 검투사일수록 노리는 데가 많다</summary>
        static void MaybeRival(GameState s, Rng rng, Gladiator g)
        {
            s.rivals ??= new Dictionary<string, RivalBid>();
            double now = Now(s);
            if (s.rivals.TryGetValue(g.id, out var old) && old.until >= now) return;
            var owner = Career.LudusOf(s, g.id);
            bool free = owner == null;
            double need = free ? g.wage * 8 : Economy.MarketValue(g, s) * 1.1;
            var cands = s.ludi.Values.Where(l => !l.isPlayer && l != owner && l.nation == s.nation && l.treasury > need && Economy.Affords(s, l, BaseWage(s, g, free, l.tier))).ToList();
            double chance = Math.Clamp(0.12 + Reputation.GladiatorStanding(g) / 160, 0.12, 0.6);
            var r = new RivalBid { until = now + 10 };
            if (cands.Count > 0 && rng.Chance(chance))
            {
                var l = rng.Pick(cands);
                r.ludusId = l.id;
                if (!free) r.fee = JsMath.Round(Transfers.AskingPrice(s, g, owner) * rng.Range(0.8, 1.1) / 50) * 50;
                r.wage = JsMath.Round(BaseWage(s, g, free, l.tier) * rng.Range(0.95, 1.25));
            }
            s.rivals[g.id] = r;
        }

        /// <summary>검투사 눈에 경쟁자 조건이 얼마나 좋아 보이나: 그 루두스 명성으로 원할 주급에 견준 비율</summary>
        static double RivalAppeal(GameState s, Gladiator g, RivalBid r, bool free)
        {
            var rival = s.ludi[r.ludusId];
            var d = Reputation.DemandFor(Reputation.GladiatorStanding(g), BaseWage(s, g, free, rival.tier), Reputation.LudusReputation(s, rival));
            return d.refuse ? 0 : r.wage / d.wage;
        }

        /// <summary>우리 제안이 검투사 눈에 얼마나 좋아 보이나 (경쟁자와 견줄 때)</summary>
        static double OurAppeal(GameState s, Gladiator g, ContractTerms t, Demand d)
        {
            if (d.refuse) return 0;
            if (!FmRules.Enabled) return t.wage / d.wage;
            return Contracts.View(s, g, t, d, s.ludi[s.playerLudusId]).Ratio;
        }

        static string RivalNote(GameState s, Gladiator g, RivalBid r)
        {
            if (r == null) return "";
            r.known = true;
            return $"\n\n{s.ludi[r.ludusId].name}도 {Josa.J(g.name, "을/를")} 노리고 있다는 소문입니다. 꾸물거리면 빼앗길 수 있습니다.";
        }

        /// <summary>매일: 답할 날이 된 제안에 답한다. 결과는 서판으로</summary>
        public static void Daily(GameState s, Rng rng)
        {
            double now = Now(s);
            if (s.rivals != null)
            {
                foreach (var k in s.rivals.Where(kv => kv.Value.until < now).Select(kv => kv.Key).ToList()) s.rivals.Remove(k);
                if (s.rivals.Count == 0) s.rivals = null;
            }
            if (s.bids == null) return;
            foreach (var b in s.bids.Where(x => x.replyAt <= now).ToList())
            {
                s.bids.Remove(b);
                if (!s.gladiators.TryGetValue(b.gladiatorId, out var g) || !g.alive) continue;
                if (b.kind == "fee") ReplyFee(s, rng, b, g);
                else if (b.kind == "terms") ReplyTerms(s, rng, b, g);
                else if (b.kind == "renew") ReplyRenew(s, rng, b, g);
                else if (b.kind == "youth") ReplyYouth(s, rng, b, g);
                else ReplyFree(s, rng, b, g);
            }
            if (s.bids.Count == 0) s.bids = null;
        }

        /// <summary>FM식이면 역제안을 결정할 일로 남긴다</summary>
        static bool Blocking => FmRules.Enabled;

        static string DecideLine => Blocking ? "\n\n답하기 전에는 다음 날로 넘어갈 수 없습니다. 다시 제안하거나 협상에서 물러나야 합니다." : "";

        static void ReplyFee(GameState s, Rng rng, PendingBid b, Gladiator g)
        {
            var owner = Career.LudusOf(s, g.id);
            var rival = RivalOf(s, g.id);
            if (owner != null && !owner.isPlayer && rival != null && rival.fee > 0)
            {
                var h = Transfers.OpenFee(s, rng, g, owner);
                // 경쟁자도 답을 기다리는 사이 값을 조금 올린다
                rival.fee = JsMath.Round(rival.fee * rng.Range(1, 1.08) / 50) * 50;
                if (rival.fee >= h.floor && rival.fee > b.amount)
                {
                    var to = s.ludi[rival.ludusId];
                    MoveToRival(s, g, owner, to, rival.fee, rival.wage);
                    Haggling.Forget(s, Transfers.FeeKey(g.id));
                    s.rivals.Remove(g.id);
                    Msg(s, "warning", $"영입 실패: {g.name}",
                        $"{Josa.J(owner.name, "이/가")} {to.name}의 이적료 {Loc(rival.fee)} D 제안을 받아들였습니다. 우리가 부른 {Loc(b.amount)} D보다 높았습니다.\n{Josa.J(g.name, "은/는")} {Josa.J(to.name, "으로/로")} 갑니다.");
                    return;
                }
            }
            var res = Transfers.ResolveFee(s, rng, g.id, b.amount);
            string who = owner?.name ?? "상대 루두스";
            switch (res.status)
            {
                case "accepted": break; // MakeDeal이 서판에 알린다
                case "countered":
                    if (Blocking)
                    {
                        var d = AddDecision(s, "fee", g, null, $"{who}: {res.line}");
                        d.counterFee = res.counter;
                        d.lastFee = b.amount;
                    }
                    Msg(s, "decision", $"이적료 역제안: {g.name}",
                        $"{Josa.J(who, "이/가")} 우리가 부른 {Loc(b.amount)} D를 거절했습니다. {res.line}" +
                        (Blocking ? DecideLine : $"\n다시 제안하려면 {g.name}의 상세 화면에서 이적료를 고쳐 보내야 합니다.") + RivalNote(s, g, rival));
                    break;
                case "walked":
                    Msg(s, "warning", $"이적 협상 결렬: {g.name}",
                        $"{who}: \"{res.line}\"\n{Haggling.LockDays}일 동안은 상대하지 않습니다.{RivalNote(s, g, rival)}");
                    // FM처럼: 더 이름난 우리 루두스로 갈 길이 막히면 그 검투사가 불만을 품는다
                    if (owner != null && !owner.isPlayer) Grievances.OnOfferRejected(s, g, s.ludi[s.playerLudusId]);
                    break;
                default:
                    Msg(s, "warning", $"이적료 제안 무효: {g.name}", res.line);
                    break;
            }
        }

        static void ReplyTerms(GameState s, Rng rng, PendingBid b, Gladiator g)
        {
            var deal = Transfers.TransferOf(s).deals.FirstOrDefault(d => d.id == b.dealId);
            if (deal == null) { Msg(s, "warning", $"계약 제안 무효: {g.name}", "이적료 합의가 이미 사라졌습니다."); return; }
            var t = TermsOf(b);
            var rival = RivalOf(s, g.id);
            if (rival != null && rival.fee > 0)
            {
                double ours = OurAppeal(s, g, t, Transfers.TransferDemand(s, g));
                if (RivalAppeal(s, g, rival, false) > ours + rng.Range(-0.08, 0.08))
                {
                    var to = s.ludi[rival.ludusId];
                    MoveToRival(s, g, s.ludi[deal.fromLudus], to, Math.Max(rival.fee, deal.fee), rival.wage);
                    Transfers.CancelDeal(s, deal.id);
                    Haggling.Forget(s, Transfers.DealKey(deal.id));
                    s.rivals.Remove(g.id);
                    Msg(s, "warning", $"영입 실패: {g.name}",
                        $"{Josa.J(g.name, "이/가")} 우리 대신 {to.name}의 조건(주급 {Loc(rival.wage)} D)을 택했습니다. 우리가 낸 주급은 {Loc(t.wage)} D였습니다.");
                    return;
                }
            }
            double fee = deal.fee;
            var res = Transfers.ResolveTerms(s, rng, b.dealId, t);
            switch (res.status)
            {
                case "accepted":
                    s.rivals?.Remove(g.id);
                    Msg(s, "info", $"영입 완료: {g.name}",
                        $"{Josa.J(g.name, "이/가")} 주급 {Loc(t.wage)} D, {t.seasons}시즌 계약에 서명했습니다. 이적료 {Loc(fee)} D를 냈습니다." + ClauseNote(t));
                    break;
                case "countered":
                    if (Blocking)
                    {
                        var d = AddDecision(s, "terms", g, deal.id, res.line);
                        d.counterTerms = res.terms;
                        d.lastTerms = t;
                    }
                    Msg(s, "decision", $"계약 역제안: {g.name}",
                        $"{Josa.J(g.name, "이/가")} 우리가 낸 조건(주급 {Loc(t.wage)} D)을 거절했습니다. {res.line}\n합의는 {Calendar.DateLabel(Calendar.FromAbs(deal.expiresAt).day)}까지 유효합니다." +
                        (Blocking ? DecideLine : "\n이적 시장 화면의 협상 탭에서 다시 제안할 수 있습니다.") + RivalNote(s, g, rival));
                    break;
                case "walked":
                    Msg(s, "warning", $"이적 무산: {g.name}", res.line);
                    break;
                default:
                    Msg(s, "warning", $"계약 제안 무효: {g.name}", res.line);
                    break;
            }
        }

        static void ReplyFree(GameState s, Rng rng, PendingBid b, Gladiator g)
        {
            if (!s.freeAgents.Contains(g.id)) { Msg(s, "warning", $"계약 제안 무효: {g.name}", $"{Josa.J(g.name, "은/는")} 이미 다른 곳과 계약했습니다."); return; }
            var t = TermsOf(b);
            var rival = RivalOf(s, g.id);
            if (rival != null)
            {
                double ours = OurAppeal(s, g, t, Economy.FreeAgentDemand(s, g));
                if (RivalAppeal(s, g, rival, true) > ours + rng.Range(-0.08, 0.08))
                {
                    var to = s.ludi[rival.ludusId];
                    MoveToRival(s, g, null, to, 0, rival.wage);
                    Haggling.Forget(s, Economy.WageKey(g.id));
                    s.rivals.Remove(g.id);
                    Msg(s, "warning", $"영입 실패: {g.name}",
                        $"{Josa.J(g.name, "이/가")} {Josa.J(to.name, "과/와")} 계약했습니다 (주급 {Loc(rival.wage)} D). 우리가 낸 주급은 {Loc(t.wage)} D였습니다.");
                    return;
                }
            }
            var res = Economy.ResolveFreeAgent(s, rng, g.id, t);
            switch (res.status)
            {
                case "accepted":
                    s.rivals?.Remove(g.id);
                    Msg(s, "info", $"영입 완료: {g.name}",
                        $"{Josa.J(g.name, "이/가")} 주급 {Loc(t.wage)} D, {t.seasons}시즌 계약에 서명했습니다. 계약금 {Loc(Economy.SigningFeeOf(t))} D를 냈습니다." + ClauseNote(t));
                    break;
                case "countered":
                    if (Blocking)
                    {
                        var d = AddDecision(s, "free", g, null, res.line);
                        d.counterTerms = res.terms;
                        d.lastTerms = t;
                    }
                    Msg(s, "decision", $"계약 역제안: {g.name}",
                        $"{Josa.J(g.name, "이/가")} 우리가 낸 조건(주급 {Loc(t.wage)} D)을 거절했습니다. {res.line}{DecideLine}{RivalNote(s, g, rival)}");
                    break;
                case "walked":
                    Msg(s, "warning", $"협상 결렬: {g.name}", $"{g.name}: \"{res.line}\"\n{Haggling.LockDays}일 동안은 상대하지 않습니다.");
                    break;
                default:
                    Msg(s, "warning", $"계약 제안 무효: {g.name}", res.line);
                    break;
            }
        }

        static void ReplyRenew(GameState s, Rng rng, PendingBid b, Gladiator g)
        {
            var t = TermsOf(b);
            var res = Contracts.ResolveRenew(s, rng, g.id, t);
            switch (res.status)
            {
                case "accepted":
                    Msg(s, "info", $"재계약: {g.name}",
                        $"{Josa.J(g.name, "이/가")} 주급 {Loc(t.wage)} D로 재계약했습니다. 계약은 시즌 {g.contract.untilSeason}까지입니다." + ClauseNote(t));
                    break;
                case "countered":
                    var d = AddDecision(s, "renew", g, null, res.line);
                    d.counterTerms = res.terms;
                    d.lastTerms = t;
                    Msg(s, "decision", $"재계약 역제안: {g.name}",
                        $"{Josa.J(g.name, "이/가")} 우리가 낸 조건(주급 {Loc(t.wage)} D)을 거절했습니다. {res.line}{DecideLine}");
                    break;
                case "walked":
                    Msg(s, "warning", $"재계약 협상 결렬: {g.name}", $"{g.name}: \"{res.line}\"\n{Haggling.LockDays}일 동안은 이야기하지 않으려 합니다. 계약이 끝나기 전에 다시 협상해야 합니다.");
                    break;
                default:
                    Msg(s, "warning", $"재계약 제안 무효: {g.name}", res.line);
                    break;
            }
        }

        static void ReplyYouth(GameState s, Rng rng, PendingBid b, Gladiator g)
        {
            var t = TermsOf(b);
            string name = g.name;
            var res = Youth.ResolveTrial(s, rng, g.id, t);
            switch (res.status)
            {
                case "accepted":
                    Msg(s, "info", $"학당 서약: {name}",
                        $"{Josa.J(name, "이/가")} 주급 {Loc(t.wage)} D, {t.seasons}시즌 학당 서약에 서명하고 학당에 들어왔습니다." +
                        (t.signingFee > 0 ? $" 계약금 {Loc(t.signingFee)} D를 냈습니다." : "") + ClauseNote(t));
                    break;
                case "countered":
                    var d = AddDecision(s, "youth", g, null, res.line);
                    d.counterTerms = res.terms;
                    d.lastTerms = t;
                    Msg(s, "decision", $"학당 서약 역제안: {name}",
                        $"{Josa.J(name, "이/가")} 우리가 낸 조건(주급 {Loc(t.wage)} D)을 거절했습니다. {res.line}{DecideLine}");
                    break;
                case "walked":
                    Msg(s, "warning", $"학당 서약 결렬: {name}", $"{name}: \"{res.line}\"\n협상이 깨져 아이는 집으로 돌아갔습니다.");
                    break;
                default:
                    Msg(s, "warning", $"학당 서약 제안 무효: {name}", res.line);
                    break;
            }
        }

        static string ClauseNote(ContractTerms t)
        {
            var c = new GladiatorContract
            {
                winBonus = t.winBonus > 0 ? t.winBonus : null, killBonus = t.killBonus > 0 ? t.killBonus : null, promoBonus = t.promoBonus > 0 ? t.promoBonus : null,
                promoExtend = t.promoExtend ? true : null, promoRaise = t.promoRaise > 0 ? t.promoRaise : null, relegCut = t.relegCut > 0 ? t.relegCut : null,
                releaseClause = t.releaseClause > 0 ? t.releaseClause : null,
            };
            string line = Contracts.ClauseLine(c);
            return line.Length > 0 ? $"\n조항: {line}" : "";
        }

        /// <summary>경쟁 루두스가 데려간다. from이 null이면 무소속</summary>
        static void MoveToRival(GameState s, Gladiator g, Ludus from, Ludus to, double fee, double wage)
        {
            var gid = g.id;
            if (from != null)
            {
                from.gladiators = from.gladiators.Where(x => x != gid).ToList();
                from.treasury += fee;
                to.treasury -= fee;
            }
            else
            {
                s.freeAgents = s.freeAgents.Where(x => x != gid).ToList();
                g.contract = new GladiatorContract { untilSeason = s.season + 1, clauses = new List<string>() };
            }
            to.gladiators.Add(gid);
            g.wage = wage;
            Career.Joined(s, g, from != null ? fee : 0);
            Career.LogCareer(s, g, from != null
                ? $"{from.name}에서 {Josa.J(to.name, "으로/로")} 이적했습니다 ({Loc(fee)} D)"
                : $"자유민으로 {Josa.J(to.name, "과/와")} 계약했습니다 (주급 {Loc(wage)} D)", "join");
        }
    }
}
