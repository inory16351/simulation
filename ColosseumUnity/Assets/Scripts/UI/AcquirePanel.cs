// 영입 패널 (웹판 components/AcquirePanel.tsx): 남의 검투사 상세 화면에서 정찰, 관심 명단, 무소속 영입, 이적료 제안.
// Unity판은 주급·이적료 모두 흥정(Haggling)이다: 여러 번 제안할 수 있지만 상대 인내심이 바닥나면 한동안 상대하지 않는다.
// 제안은 보내 두면 하루~사흘 뒤 서판으로 답이 온다 (Negotiations). 그동안 버튼은 "답 기다림"으로 잠긴다.
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class AcquirePanel : MonoBehaviour
    {
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text info;
        [SerializeField] RectTransform infoBar;
        [SerializeField] Button scout;
        [SerializeField] Button shortlist;
        [SerializeField] TMP_Text msg;
        [Tooltip("흥정 상태 줄 (인내심·역제안)")]
        [SerializeField] TMP_Text mood;
        [Header("무소속")]
        [SerializeField] GameObject freeRoot;
        [SerializeField] TMP_Text freeHint;
        [SerializeField] TMP_Text want;
        [SerializeField] TMP_InputField wage;
        [Tooltip("1~4시즌 순서")]
        [SerializeField] List<Button> seasons = new();
        [SerializeField] Button sign;
        [Header("다른 루두스 소속")]
        [SerializeField] GameObject clubRoot;
        [SerializeField] TMP_Text value;
        [SerializeField] TMP_InputField fee;
        [SerializeField] Button bid;
        [SerializeField] GameObject dealRoot;
        [SerializeField] TMP_Text dealText;
        [SerializeField] Button dealGo;

        static GameController G => GameController.I;
        static GameState S => GameController.I.State;

        string gid, msgText;
        int years = 2;
        bool good;

        void Awake()
        {
            for (int i = 0; i < seasons.Count; i++) { int y = i + 1; seasons[i].onClick.AddListener(() => { years = y; Draw(); }); }
            scout.onClick.AddListener(() =>
            {
                string id = gid;
                // 우리 검투사면 이 버튼은 '재계약 협상'
                if (Mine) { NegotiationPopup.OpenContract("renew", id); return; }
                Say(G.Try(d => Scouting.AssignScout(d, id)));
            });
            shortlist.onClick.AddListener(() =>
            {
                string id = gid;
                // 우리 검투사면 이 버튼은 '이적 명단'
                if (Mine) { NegotiationPopup.OpenList(id); return; }
                G.Mutate(d =>
                {
                    d.shortlist ??= new List<string>();
                    if (d.shortlist.Contains(id)) d.shortlist = d.shortlist.Where(x => x != id).ToList();
                    else d.shortlist.Add(id);
                });
            });
            sign.onClick.AddListener(Sign);
            bid.onClick.AddListener(Bid);
            dealGo.onClick.AddListener(() =>
            {
                // FM식: 합의가 있으면 바로 개인 계약 협상 창
                var deal = Transfers.TransferOf(S).deals.FirstOrDefault(x => x.gladiatorId == gid);
                if (FmRules.Enabled && deal != null) NegotiationPopup.OpenContract("terms", gid, deal.id);
                else G.Go("market");
            });
        }

        /// <summary>지금 보는 검투사가 우리 검투사인가 (FM식: 계약·이적 명단 패널로 쓴다)</summary>
        bool Mine => FmRules.Enabled && gid != null && S.ludi[S.playerLudusId].gladiators.Contains(gid);

        /// <summary>검투사가 바뀌면 입력칸을 기본값으로 채운다. 우리 검투사면 FM식 계약 패널(재계약·이적 명단)</summary>
        public void Show(Gladiator g)
        {
            var owner = Career.LudusOf(S, g.id);
            bool free = S.freeAgents.Contains(g.id);
            bool mine = FmRules.Enabled && owner != null && owner.isPlayer && g.alive;
            bool canAcquire = g.alive && (free || (owner != null && !owner.isPlayer));
            gameObject.SetActive(canAcquire || mine);
            if (mine)
            {
                if (gid != g.id) { gid = g.id; msgText = null; }
                DrawMine(g);
                return;
            }
            if (!canAcquire) return;
            if (gid != g.id)
            {
                gid = g.id;
                msgText = null;
                years = 2;
                // 상대가 다시 부른 값이 있으면 그 값부터 채운다 (역제안은 서판으로 온다)
                double wageCounter = Haggling.Get(S, Economy.WageKey(g.id))?.counter ?? 0;
                double feeCounter = Haggling.Get(S, Transfers.FeeKey(g.id))?.counter ?? 0;
                wage.SetTextWithoutNotify((wageCounter > 0 ? wageCounter : Economy.FreeAgentDemand(S, g).wage).ToString());
                fee.SetTextWithoutNotify((feeCounter > 0 ? feeCounter : JsMath.Round(Economy.MarketValue(g, GameController.I.State) / 50) * 50).ToString());
            }
            Draw();
        }

        void Say(string text, bool ok = false) { msgText = text; good = ok; Draw(); }

        void Sign()
        {
            string id = gid;
            // FM식: 조항까지 거는 계약 협상 창
            if (FmRules.Enabled) { NegotiationPopup.OpenContract("free", id); return; }
            int y = years;
            double w = double.TryParse(wage.text, out var v) ? v : 0;
            HaggleResult res = null;
            G.Act((d, rng) => res = Economy.HaggleFreeAgent(d, rng, id, w, y));
            Say(res.line, res.status == "sent");
        }

        void Bid()
        {
            string id = gid;
            double f = double.TryParse(fee.text, out var v) ? v : 0;
            if (FmRules.Enabled) { NegotiationPopup.OpenBid(id, f); return; }
            HaggleResult res = null;
            G.Act((d, rng) => res = Transfers.HaggleFee(d, rng, id, f));
            Say(res.line, res.status == "sent");
        }

        /// <summary>우리 검투사: 계약 요약, 재계약 협상, 이적 명단 (FM식)</summary>
        void DrawMine(Gladiator g)
        {
            title.text = "◆  계약 · 이적";
            var c = g.contract;
            string faint = Theme.Hex(Theme.TextFaint);
            string clauses = Contracts.ClauseLine(c);
            info.text = g.status == "slave"
                ? $"노예 · 주급 {g.wage:N0} D <color={faint}>(계약 없이 루두스에 매여 있습니다)</color>"
                : $"주급 {g.wage:N0} D · 계약 시즌 {c?.untilSeason}까지{(c != null && c.untilSeason <= S.season ? $" <color=#d9a29b>(이번 시즌 만료)</color>" : "")}{(clauses.Length > 0 ? $"\n<color={faint}>{clauses}</color>" : "")}";
            infoBar.gameObject.SetActive(false);
            bool listed = Transfers.Listed(S, g.id);
            scout.gameObject.SetActive(g.status != "slave" && c != null);
            scout.interactable = true;
            var renewPending = Negotiations.Pending(S, "renew", g.id);
            scout.GetComponentInChildren<TMP_Text>().text = renewPending != null ? "재계약 답 기다림" : "재계약 협상";
            scout.interactable = renewPending == null;
            shortlist.GetComponentInChildren<TMP_Text>().text = listed ? $"이적 명단 ({S.transferList[g.id]:N0} D)" : "이적 명단에 올리기";
            freeRoot.SetActive(false);
            clubRoot.SetActive(false);
            dealRoot.SetActive(false);
            string line = Widgets.BidLine(S, Contracts.RenewKey(g.id), renewPending, g.id);
            var offer = Transfers.TransferOf(S).offers.FirstOrDefault(o => o.gladiatorId == g.id);
            if (offer != null) line = (line != null ? line + "\n" : "") + $"<color={Theme.Hex(Theme.Torch)}>{Tips.Ludus(S.ludi[offer.fromLudus])}에서 이적료 {offer.fee:N0} D를 제시했습니다</color>";
            mood.gameObject.SetActive(line != null);
            mood.text = line ?? "";
            msg.gameObject.SetActive(msgText != null);
            msg.text = msgText ?? "";
        }

        void Draw()
        {
            if (gid == null || !S.gladiators.TryGetValue(gid, out var g)) return;
            if (Mine) { DrawMine(g); return; }
            infoBar.gameObject.SetActive(true);
            scout.gameObject.SetActive(true);
            var owner = Career.LudusOf(S, g.id);
            bool free = S.freeAgents.Contains(g.id);
            title.text = "◆  " + (free ? "무소속 — 영입 협상" : $"{Tips.Ludus(owner)} 소속 — 이적");

            double k = Scouting.KnowledgeOf(S, g);
            bool scouting = Scouting.ScoutingOf(S).assignments.Contains(g.id);
            info.text = $"정보량 {JsMath.Round(k)}";
            Widgets.SetBar(infoBar, k, 100, Theme.BronzeHi);
            scout.interactable = !scouting && k < 100;
            scout.GetComponentInChildren<TMP_Text>().text = scouting ? "정찰 중" : k >= 100 ? "정찰 끝" : "정찰 보내기";
            bool shortOn = S.shortlist?.Contains(g.id) == true;
            shortlist.GetComponentInChildren<TMP_Text>().text = shortOn ? "★ 관심 명단에서 빼기" : "☆ 관심 명단";

            msg.gameObject.SetActive(msgText != null);
            msg.text = msgText ?? "";
            msg.color = good ? Theme.Good : new Color32(0xd9, 0xa2, 0x9b, 0xff);

            string key = free ? Economy.WageKey(g.id) : Transfers.FeeKey(g.id);
            var pending = Negotiations.Pending(S, free ? "free" : "fee", g.id);
            string line = Widgets.BidLine(S, key, pending, g.id);
            mood.gameObject.SetActive(line != null);
            mood.text = line ?? "";
            // 답을 기다리는 동안에는 다시 못 보낸다
            bool locked = Haggling.IsLocked(S, key) || pending != null;

            freeRoot.SetActive(free);
            if (free)
            {
                var d = Economy.FreeAgentDemand(S, g);
                freeHint.text = d.refuse
                    ? $"<color=#d9a29b>거절 · {d.reason}</color> <color={Theme.Hex(Theme.TextFaint)}>루두스 명성이 오르면 협상에 응합니다</color>"
                    : FmRules.Enabled ? $"주급·기간에 계약금·수당·승격 조항까지 걸어 협상합니다. {d.reason}"
                    : $"계약금은 제안 주급의 4주치로, {Economy.SigningFee(d.wage):N0} D 안팎입니다. {d.reason}";
                want.text = $"요구 주급 {d.wage} D";
                for (int i = 0; i < seasons.Count; i++)
                {
                    bool on = years == i + 1;
                    seasons[i].transform.Find("Fill").GetComponent<Image>().color = on ? Theme.ContinueBg : Theme.Panel2;
                    seasons[i].GetComponent<Image>().color = on ? Theme.Torch : Theme.Line;
                }
                sign.interactable = !d.refuse && !locked;
                sign.GetComponentInChildren<TMP_Text>().text = pending != null ? "답 기다림" : FmRules.Enabled ? "계약 협상" : "계약 제안";
            }

            bool club = owner != null && !owner.isPlayer;
            var deal = Transfers.TransferOf(S).deals.FirstOrDefault(x => x.gladiatorId == g.id);
            clubRoot.SetActive(club && deal == null);
            dealRoot.SetActive(club && deal != null);
            if (club && deal == null)
            {
                value.text = $"추정 가치 {JsMath.Round(Economy.MarketValue(g, GameController.I.State) / 50) * 50:N0} D";
                bool open = Transfers.InWindow(S);
                bid.interactable = open && !locked;
                bid.GetComponentInChildren<TMP_Text>().text = pending != null ? "답 기다림" : FmRules.Enabled ? "이적료 협상" : "이적료 제안";
                if (!open && msgText == null) { msg.gameObject.SetActive(true); msg.text = $"{Tips.Window(Transfers.WindowLine(S))} · 그때까지는 이적료를 제안할 수 없습니다"; msg.color = Theme.TextDim; }
            }
            if (deal != null) dealText.text = $"이적료 {deal.fee:N0} D에 합의했습니다. 이제 개인 계약을 협상합니다.";
        }
    }
}
