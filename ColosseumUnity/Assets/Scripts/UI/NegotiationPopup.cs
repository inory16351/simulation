// 협상 창 (Unity판 전용, FM 협상 화면처럼). 캔버스 위 오버레이 하나가 두 가지 판을 바꿔 보여 준다.
//  · 값 판(FeeRoot): 받은 이적 제안(수락·거절·역제안), 우리 이적료 제안과 상대 역제안, 이적 명단 희망가
//  · 계약 판(ContractRoot): 주급·기간·계약금·승리/킬 수당·승격 보너스/연장/인상·강등 삭감·최소 이적료 (무소속·이적·재계약)
// 계약 판은 조항을 고칠 때마다 검투사 반응(Contracts.View → Mood)을 바로 보여 준다. 정확한 마지노선은 숨긴다.
// 하이어라키: NegotiationPopup > Overlay > (Dim, Card > Title, Sub, Info, FeeRoot, ContractRoot, Mood, Note, Msg, Buttons)
using System;
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class NegotiationPopup : MonoBehaviour
    {
        public static NegotiationPopup I { get; private set; }

        /// <summary>계약 판의 한 줄: 값 칸 + 빼기·더하기 (켜고 끄는 조항은 toggle만)</summary>
        [Serializable]
        public class Row
        {
            public string key;
            public GameObject root;
            public TMP_InputField input;
            public Button minus, plus, toggle;
            public TMP_Text hint;
        }

        [SerializeField] GameObject overlay;
        [SerializeField] Button dim;
        [SerializeField] TMP_Text title, sub, info, mood, note, msg;
        [Header("값 판")]
        [SerializeField] GameObject feeRoot;
        [SerializeField] TMP_Text priceLabel;
        [SerializeField] TMP_InputField price;
        [SerializeField] Button minus10, plus10, quickA, quickB;
        [Header("계약 판")]
        [SerializeField] GameObject contractRoot;
        [SerializeField] List<Row> rows = new();
        [Header("버튼")]
        [SerializeField] Button primary, secondary, reject, close;

        static GameController G => GameController.I;
        static GameState S => GameController.I.State;

        /// <summary>sell | buyDecision | bid | list | contract</summary>
        string mode;
        string offerId, decisionId, gladiatorId, dealId, kind;
        ContractTerms terms;
        double quickAValue, quickBValue;
        string msgText;
        bool msgGood;

        public static bool IsOpen => I != null && I.overlay.activeSelf;

        void Awake()
        {
            I = this;
            dim.onClick.AddListener(Close);
            close.onClick.AddListener(Close);
            minus10.onClick.AddListener(() => SetPrice(Price * 0.9));
            plus10.onClick.AddListener(() => SetPrice(Price * 1.1));
            quickA.onClick.AddListener(() => SetPrice(quickAValue));
            quickB.onClick.AddListener(() => SetPrice(quickBValue));
            price.onEndEdit.AddListener(_ => Draw());
            foreach (var r in rows)
            {
                var row = r;
                if (row.minus != null) row.minus.onClick.AddListener(() => Step(row.key, -1));
                if (row.plus != null) row.plus.onClick.AddListener(() => Step(row.key, +1));
                if (row.toggle != null) row.toggle.onClick.AddListener(() => { terms.promoExtend = !terms.promoExtend; Draw(); });
                if (row.input != null) row.input.onEndEdit.AddListener(v => { SetTerm(row.key, Parse(v)); Draw(); });
            }
            primary.onClick.AddListener(() => Act(0));
            secondary.onClick.AddListener(() => Act(1));
            reject.onClick.AddListener(() => Act(2));
            overlay.SetActive(false);
        }

        void Update()
        {
            if (!overlay.activeSelf) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Shell.EscUsedFrame = Time.frameCount; Close(); }
        }

        public void Close()
        {
            overlay.SetActive(false);
            G?.Go(G.Screen);
        }

        static double Parse(string v) => double.TryParse(v.Replace(",", ""), out var x) ? Math.Max(0, x) : 0;
        double Price => Parse(price.text);
        void SetPrice(double v) { price.SetTextWithoutNotify((JsMath.Round(v / 50) * 50).ToString("0")); Draw(); }

        void Open(string m)
        {
            mode = m;
            msgText = null;
            overlay.SetActive(true);
            transform.SetAsLastSibling();
            // 툴팁은 늘 맨 위에 (검투사·루두스 링크를 누르면 그 카드가 이 창 위로 뜬다)
            var tips = transform.parent.GetComponentInChildren<TipLayer>(true);
            if (tips != null) tips.transform.SetAsLastSibling();
            Draw();
        }

        // ── 여는 길 ──

        /// <summary>받은 이적 제안 (우리 검투사를 사겠다는 제안)</summary>
        public static void OpenOffer(string id)
        {
            if (I == null) return;
            var o = Transfers.TransferOf(S).offers.FirstOrDefault(x => x.id == id);
            if (o == null) return;
            I.offerId = id;
            I.gladiatorId = o.gladiatorId;
            I.price.SetTextWithoutNotify((S.transferList != null && S.transferList.TryGetValue(o.gladiatorId, out var lp) ? Math.Max(lp, o.fee * 1.15) : o.fee * 1.25).ToString("0"));
            I.SetPrice(I.Price);
            I.Open("sell");
        }

        /// <summary>결정할 일 (상대 역제안)</summary>
        public static void OpenDecision(string id)
        {
            if (I == null) return;
            var d = Negotiations.DecisionById(S, id);
            if (d == null) return;
            I.decisionId = id;
            I.gladiatorId = d.gladiatorId;
            if (d.kind == "fee")
            {
                I.price.SetTextWithoutNotify(d.counterFee.ToString("0"));
                I.Open("buyDecision");
            }
            else
            {
                I.kind = d.kind;
                I.dealId = d.dealId;
                I.terms = (d.counterTerms ?? d.lastTerms ?? new ContractTerms()).Clone();
                I.Open("contract");
            }
        }

        /// <summary>남의 검투사에게 이적료를 제안한다 (영입 패널)</summary>
        public static void OpenBid(string gid, double fee)
        {
            if (I == null) return;
            I.gladiatorId = gid;
            I.decisionId = null;
            I.price.SetTextWithoutNotify(fee.ToString("0"));
            I.Open("bid");
        }

        /// <summary>이적 명단 희망가</summary>
        public static void OpenList(string gid)
        {
            if (I == null) return;
            I.gladiatorId = gid;
            var g = S.gladiators[gid];
            double cur = S.transferList != null && S.transferList.TryGetValue(gid, out var p) ? p : JsMath.Round(Economy.MarketValue(g, S) * 1.1 / 50) * 50;
            I.price.SetTextWithoutNotify(cur.ToString("0"));
            I.Open("list");
        }

        /// <summary>계약 협상. kind: free(무소속) | terms(이적 개인 계약, dealId 필요) | renew(재계약) | youth(모래 시험 응시자와 학당 서약)</summary>
        public static void OpenContract(string kind, string gid, string dealId = null)
        {
            if (I == null) return;
            I.kind = kind;
            I.gladiatorId = gid;
            I.dealId = dealId;
            var existing = Negotiations.DecisionFor(S, kind, kind == "terms" ? dealId : gid);
            I.decisionId = existing?.id;
            var g = S.gladiators[gid];
            var d = I.DemandNow(g);
            I.terms = existing?.counterTerms?.Clone() ?? new ContractTerms { wage = JsMath.Round(d.wage), seasons = Contracts.PreferredSeasons(g) };
            I.liveCounter = null;
            I.Open("contract");
        }

        /// <summary>다음 날로 가기 전에 정해야 할 첫 일을 연다 (받은 제안 먼저)</summary>
        public static bool OpenFirstBlocker()
        {
            var o = Transfers.OpenOffers(S).FirstOrDefault();
            if (o != null) { OpenOffer(o.id); return true; }
            var d = S.decisions?.FirstOrDefault();
            if (d != null) { OpenDecision(d.id); return true; }
            return false;
        }

        /// <summary>이적 개인 계약에서 방금 받은 역제안 (그 자리에서 이어 가는 협상)</summary>
        ContractTerms liveCounter;

        Demand DemandNow(Gladiator g) => kind switch
        {
            "free" => Economy.FreeAgentDemand(S, g),
            "terms" => Transfers.TransferDemand(S, g),
            "youth" => Youth.TrialDemand(S, g),
            _ => Contracts.RenewDemand(S, g),
        };

        // ── 계약 판 값 ──

        static readonly Dictionary<string, double> Steps = new()
        {
            ["wage"] = 5, ["seasons"] = 1, ["signing"] = 100, ["win"] = 5, ["kill"] = 10, ["promo"] = 100, ["raise"] = 5, ["cut"] = 5, ["release"] = 500,
        };

        double GetTerm(string k) => k switch
        {
            "wage" => terms.wage, "seasons" => terms.seasons, "signing" => terms.signingFee, "win" => terms.winBonus, "kill" => terms.killBonus,
            "promo" => terms.promoBonus, "raise" => terms.promoRaise, "cut" => terms.relegCut, "release" => terms.releaseClause, _ => 0,
        };

        void SetTerm(string k, double v)
        {
            switch (k)
            {
                case "wage": terms.wage = Math.Max(1, JsMath.Round(v)); break;
                case "seasons": terms.seasons = (int)Math.Max(1, Math.Min(5, v)); break;
                case "signing": terms.signingFee = JsMath.Round(v); break;
                case "win": terms.winBonus = JsMath.Round(v); break;
                case "kill": terms.killBonus = JsMath.Round(v); break;
                case "promo": terms.promoBonus = JsMath.Round(v); break;
                case "raise": terms.promoRaise = Math.Min(50, JsMath.Round(v)); break;
                case "cut": terms.relegCut = Math.Min(50, JsMath.Round(v)); break;
                case "release": terms.releaseClause = JsMath.Round(v); break;
            }
        }

        void Step(string k, int dir)
        {
            // 유소년 주급은 한 자릿수라 1 D씩
            double step = k == "wage" ? (kind == "youth" ? 1 : Math.Max(5, JsMath.Round(terms.wage * 0.05))) : Steps[k];
            SetTerm(k, Math.Max(0, GetTerm(k) + dir * step));
            Draw();
        }

        // ── 그리기 ──

        void Draw()
        {
            if (S == null || gladiatorId == null || !S.gladiators.TryGetValue(gladiatorId, out var g)) { overlay.SetActive(false); return; }
            bool contract = mode == "contract";
            feeRoot.SetActive(!contract);
            contractRoot.SetActive(contract);
            secondary.gameObject.SetActive(false);
            reject.gameObject.SetActive(false);
            primary.interactable = true;
            string faint = Theme.Hex(Theme.TextFaint), dimC = Theme.Hex(Theme.TextDim);
            double value = Economy.MarketValue(g, S);
            note.text = "";
            mood.text = "";
            switch (mode)
            {
                case "sell": DrawSell(g, value); break;
                case "buyDecision": DrawBuyDecision(g, value); break;
                case "bid": DrawBid(g, value); break;
                case "list": DrawList(g, value); break;
                default: DrawContract(g); break;
            }
            msg.gameObject.SetActive(msgText != null);
            msg.text = msgText ?? "";
            msg.color = msgGood ? Theme.Good : new Color32(0xd9, 0xa2, 0x9b, 0xff);
            note.gameObject.SetActive(note.text.Length > 0);
            mood.gameObject.SetActive(mood.text.Length > 0);
        }

        void Buttons(string a, string b = null, string c = null)
        {
            primary.GetComponentInChildren<TMP_Text>().text = a;
            secondary.gameObject.SetActive(b != null);
            if (b != null) secondary.GetComponentInChildren<TMP_Text>().text = b;
            reject.gameObject.SetActive(c != null);
            if (c != null) reject.GetComponentInChildren<TMP_Text>().text = c;
        }

        void Quick(string a, double av, string b, double bv)
        {
            quickAValue = av; quickBValue = bv;
            quickA.GetComponentInChildren<TMP_Text>().text = a;
            quickB.GetComponentInChildren<TMP_Text>().text = b;
            quickB.gameObject.SetActive(bv > 0);
        }

        void DrawSell(Gladiator g, double value)
        {
            var o = Transfers.TransferOf(S).offers.FirstOrDefault(x => x.id == offerId);
            if (o == null) { overlay.SetActive(false); return; }
            var buyer = S.ludi[o.fromLudus];
            double listed = S.transferList != null && S.transferList.TryGetValue(g.id, out var lp) ? lp : 0;
            title.text = $"이적 제안 — {Tips.Name(g)}";
            sub.text = $"사겠다는 루두스: {Tips.Ludus(buyer)} ({buyer.tier}부 · {Reputation.ReputationNames(Reputation.LudusReputation(S, buyer))})";
            info.text = $"제시한 이적료  <b>{o.fee:N0} D</b>" + ((o.round ?? 0) > 0 ? $"  <color={Theme.Hex(Theme.TextFaint)}>({o.round}번째 수정안)</color>" : "") +
                $"\n추정 몸값  {value:N0} D" + (listed > 0 ? $"\n이적 명단 희망가  {listed:N0} D" : "") +
                $"\n{Tips.Name(g)}: 주급 {g.wage:N0} D ·{(g.contract != null ? $"계약 시즌 {g.contract.untilSeason}까지" : "노예")}";
            priceLabel.text = "역제안 값";
            Quick("희망가", listed > 0 ? listed : value, "몸값", value);
            bool waiting = o.status == "waiting";
            Buttons($"수락 ({o.fee:N0} D)", "역제안 보내기", "거절");
            primary.interactable = !waiting;
            secondary.interactable = !waiting && Price > o.fee;
            reject.interactable = !waiting;
            if (waiting) note.text = $"<color={Theme.Hex(Theme.Torch)}>우리가 부른 {o.ask:N0} D에 대한 답을 기다리는 중입니다. 내일 답이 옵니다.</color>";
            else note.text = "수락하면 바로 팔립니다. 역제안은 다음 날 답이 오고, 너무 높게 부르면 제안을 거둬들일 수 있습니다.";
        }

        void DrawBuyDecision(Gladiator g, double value)
        {
            var d = Negotiations.DecisionById(S, decisionId);
            if (d == null) { overlay.SetActive(false); return; }
            var owner = Career.LudusOf(S, g.id);
            title.text = $"이적료 역제안 — {Tips.Name(g)}";
            sub.text = owner != null ? $"{Tips.Ludus(owner)}의 답" : "";
            info.text = $"우리가 부른 값  {d.lastFee:N0} D\n상대가 바라는 값  <b>{d.counterFee:N0} D</b>\n추정 몸값  {value:N0} D\n금고  {S.ludi[S.playerLudusId].treasury:N0} D";
            note.text = d.line;
            priceLabel.text = "다시 부를 값";
            Quick("상대 값", d.counterFee, "중간", JsMath.Round((d.lastFee + d.counterFee) / 2));
            Buttons("이 값으로 다시 제안", $"상대 값 받기 ({d.counterFee:N0} D)", "협상에서 물러서기");
        }

        void DrawBid(Gladiator g, double value)
        {
            var owner = Career.LudusOf(S, g.id);
            title.text = $"이적료 제안 — {Tips.Name(g)}";
            sub.text = owner != null ? $"{Tips.Ludus(owner)} 소속 · {Transfers.WindowLine(S)}" : "";
            info.text = $"추정 몸값  {value:N0} D\n금고  {S.ludi[S.playerLudusId].treasury:N0} D";
            priceLabel.text = "제안할 이적료";
            Quick("몸값", value, "몸값 ×0.8", value * 0.8);
            Buttons("제안 보내기");
            note.text = "루두스는 하루~사흘 따져 보고 서판으로 답합니다. 부르는 값 이상이면 받아들여 개인 계약 협상으로 넘어갑니다. 마지노선보다 낮으면 역제안이 오고, 너무 낮으면 협상이 깨집니다.";
            if (Transfers.MovedThisWindow(S, g))
            {
                note.text = $"<color=#d9a29b>{Transfers.MovedLine(g)}.</color>";
                primary.interactable = false;
            }
        }

        void DrawList(Gladiator g, double value)
        {
            bool listed = Transfers.Listed(S, g.id);
            title.text = $"이적 명단 — {Tips.Name(g)}";
            sub.text = listed ? $"지금 희망가 {S.transferList[g.id]:N0} D에 올라 있습니다" : "이적 명단에 올리면 이적 시장 기간에 사겠다는 제안이 자주 옵니다";
            info.text = $"추정 몸값  {value:N0} D\n주급 {g.wage:N0} D · 사기 {JsMath.Round(g.morale)}";
            priceLabel.text = "희망 이적료";
            Quick("몸값", value, "몸값 ×1.3", value * 1.3);
            Buttons(listed ? "희망가 바꾸기" : "이적 명단에 올리기", null, listed ? "명단에서 내리기" : null);
            note.text = "명단에 오르면 검투사는 서운해합니다 (떠나고 싶던 자는 반깁니다). 희망가가 몸값보다 너무 높으면 제안이 잘 오지 않습니다.";
        }

        void DrawContract(Gladiator g)
        {
            var d = DemandNow(g);
            var to = S.ludi[S.playerLudusId];
            var v = Contracts.View(S, g, terms, d, to);
            var dec = decisionId != null ? Negotiations.DecisionById(S, decisionId) : null;
            string what = kind switch { "free" => "무소속 계약", "terms" => "이적 개인 계약", "youth" => "학당 서약", _ => "재계약" };
            title.text = $"{what} — {Tips.Name(g)}";
            string pref = $"바라는 기간 {Contracts.PreferredSeasons(g)}시즌";
            // 바라는 주급은 대리인이 흘린 대략의 값만 (5 D 단위)
            // 유소년은 1 D 단위 (한 자릿수 주급을 5 D로 반올림하면 너무 거칠다)
            double unit = kind == "youth" ? 1 : 5;
            string want = d.refuse ? $"<color=#d9a29b>{d.reason}</color>" : $"바라는 주급 약 {JsMath.Round(d.wage / unit) * unit:N0} D · {pref}";
            sub.text = want;
            var deal = dealId != null ? Transfers.TransferOf(S).deals.FirstOrDefault(x => x.id == dealId) : null;
            string extra = kind switch
            {
                "terms" when deal != null => $"이적료 {deal.fee:N0} D 합의 ({Calendar.DateLabel(Calendar.FromAbs(deal.expiresAt).day)}까지)",
                "renew" => $"지금 계약: 주급 {g.wage:N0} D, 시즌 {g.contract?.untilSeason}까지{(Contracts.ClauseLine(g.contract) is string cl && cl.Length > 0 ? " · " + cl : "")}",
                "youth" => $"모래 시험 응시자 · {Calendar.DateLabel(Calendar.FromAbs(to.youthTrialUntil ?? 0).day)}까지 · 학당 정원 {to.youth.Count}/{Youth.YouthCapacity(to)}명 · {g.age}세, 데뷔까지 {Math.Max(0, Data.Race(g.race).age.debut - g.age)}년",
                _ => $"{Reputation.InterestNames[Reputation.InterestIn(S, g, Reputation.LudusReputation(S, to))]} 관심",
            };
            info.text = $"{extra}\n금고 {to.treasury:N0} D · 전적 {g.record.wins}승 {g.record.losses}패 {g.record.kills}킬 · 인기 {JsMath.Round(g.popularity)}";
            foreach (var r in rows)
            {
                if (r.toggle != null)
                {
                    r.toggle.GetComponentInChildren<TMP_Text>().text = terms.promoExtend ? "■ 있음" : "□ 없음";
                    continue;
                }
                if (r.input != null && !r.input.isFocused) r.input.SetTextWithoutNotify(GetTerm(r.key).ToString("0"));
                if (r.hint != null) r.hint.text = HintFor(r.key, g);
            }
            mood.text = $"{g.name}의 반응: <b>{Contracts.Mood(v)}</b>";
            if (kind == "terms")
            {
                // 그 자리에서 이어 가는 협상: 인내심이 바닥나면 끝난다
                if (Widgets.HaggleLine(S, Transfers.DealKey(dealId)) is string patience) mood.text += $"\n{patience}";
                note.text = $"제안하면 바로 답이 옵니다. 낮게 부를수록 인내심이 줄고, 바닥나면 협상이 끝나고, 그때 {Josa.J(g.name, "을/를")} 노리던 루두스가 있으면 그리로 떠납니다. 조건을 받아들이면 며칠 고민한 뒤 노리는 루두스들과 견줘 갈 곳을 고릅니다.";
            }
            if (dec != null) note.text = dec.line;
            var pending = Negotiations.Pending(S, kind, kind == "terms" ? dealId : gladiatorId);
            if (pending != null) note.text = $"<color={Theme.Hex(Theme.Torch)}>{Negotiations.WaitLine(S, pending)}</color>";
            Buttons("제안 보내기", dec?.counterTerms != null || liveCounter != null ? "상대 조건 그대로 제안" : null, dec != null || (kind == "terms" && pending == null) ? "협상에서 물러서기" : null);
            primary.interactable = !d.refuse && pending == null;
        }

        string HintFor(string key, Gladiator g) => key switch
        {
            "seasons" => "시즌",
            "raise" or "cut" => "%",
            "win" => $"공식전 승리마다 · 통산 {g.record.wins}승",
            "kill" => $"상대를 죽일 때마다 · 통산 {g.record.kills}킬",
            "promo" => "승격하면 한 번",
            "release" => "0 = 없음 · 이 값 이상이면 막을 수 없음",
            "signing" => "서명할 때 한 번",
            _ => "D / 주",
        };

        // ── 행동 ──

        void Say(string text, bool ok) { msgText = text; msgGood = ok; Draw(); }

        /// <summary>0 = 주 버튼, 1 = 보조 버튼, 2 = 거절·물러서기</summary>
        void Act(int which)
        {
            string err = null;
            HaggleResult res = null;
            switch (mode)
            {
                case "sell":
                {
                    string oid = offerId;
                    double ask = Price;
                    if (which == 0) err = G.Try(s => Transfers.AcceptOffer(s, oid));
                    else if (which == 1) err = G.Try(s => Transfers.CounterOffer(s, oid, ask));
                    else G.Mutate(s => Transfers.RejectOffer(s, oid));
                    if (err == null) { overlay.SetActive(false); AfterAction(); return; }
                    break;
                }
                case "buyDecision":
                {
                    var d = Negotiations.DecisionById(S, decisionId);
                    if (which == 2) { string did = decisionId; G.Mutate(s => Negotiations.Withdraw(s, did)); overlay.SetActive(false); AfterAction(); return; }
                    double fee = which == 1 ? d.counterFee : Price;
                    string gid = gladiatorId;
                    G.Act((s, rng) => res = Transfers.HaggleFee(s, rng, gid, fee));
                    break;
                }
                case "bid":
                {
                    string gid = gladiatorId;
                    double fee = Price;
                    G.Act((s, rng) => res = Transfers.HaggleFee(s, rng, gid, fee));
                    break;
                }
                case "list":
                {
                    string gid = gladiatorId;
                    double p = Price;
                    if (which == 2) G.Mutate(s => Transfers.Unlist(s, gid));
                    else err = G.Try(s => Transfers.ListForTransfer(s, gid, p));
                    if (err == null) { overlay.SetActive(false); AfterAction(); return; }
                    break;
                }
                default:
                {
                    if (which == 2 && decisionId == null)
                    {
                        // 이적 개인 계약에서 물러서기: 합의를 없던 일로
                        string gone = dealId;
                        G.Mutate(s => { Transfers.CancelDeal(s, gone); Haggling.Forget(s, Transfers.DealKey(gone)); });
                        overlay.SetActive(false);
                        AfterAction();
                        return;
                    }
                    if (which == 2) { string did = decisionId; G.Mutate(s => Negotiations.Withdraw(s, did)); overlay.SetActive(false); AfterAction(); return; }
                    var dec = decisionId != null ? Negotiations.DecisionById(S, decisionId) : null;
                    var t = (which == 1 && dec?.counterTerms != null ? dec.counterTerms : which == 1 && liveCounter != null ? liveCounter : terms).Clone();
                    string gid = gladiatorId, did2 = dealId, k = kind;
                    G.Act((s, rng) => res = k switch
                    {
                        "free" => Economy.HaggleFreeAgentTerms(s, rng, gid, t),
                        "terms" => Transfers.HaggleTransferTerms(s, rng, did2, t),
                        "youth" => Youth.HaggleTrial(s, rng, gid, t),
                        _ => Contracts.SendRenew(s, rng, gid, t),
                    });
                    break;
                }
            }
            if (res != null)
            {
                if (res.status is "sent" or "walked") { overlay.SetActive(false); AfterAction(); return; }
                if (res.status == "countered" && mode == "contract")
                {
                    // 그 자리에서 역제안: 판에는 우리가 낸 값을 두고, 상대 조건은 버튼으로 받을 수 있게
                    liveCounter = res.terms;
                }
                Say(res.line, false);
                return;
            }
            if (err != null) Say(err, false);
        }

        /// <summary>하나를 끝내면 다음으로 정할 일이 있으면 이어서 연다 (FM처럼 차례로)</summary>
        static void AfterAction()
        {
            if (FmRules.Enabled && Day.Blockers(S).Count > 0 && OpenFirstBlocker()) return;
            G.Go(G.Screen);
        }
    }
}
