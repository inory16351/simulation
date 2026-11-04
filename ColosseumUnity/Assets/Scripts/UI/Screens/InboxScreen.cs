// 전령의 서판 (웹판 InboxScreen.tsx): 결정할 일(이벤트 카드)과 받은 소식 목록·본문.
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class InboxScreen : ScreenBase
    {
        [Header("이벤트 카드 (원본은 하이어라키에 비활성)")]
        [SerializeField] RectTransform eventCardTemplate;
        [Header("소식 목록")]
        [SerializeField] Button itemTemplate;
        [Header("본문")]
        [SerializeField] TMP_Text readTitle;
        [SerializeField] TMP_Text readMeta;
        [SerializeField] TMP_Text readBody;

        TemplateList<RectTransform> cards;
        TemplateList<Button> items;
        string sel;

        void Awake()
        {
            cards = new TemplateList<RectTransform>(eventCardTemplate);
            items = new TemplateList<Button>(itemTemplate);
        }

        public override void Refresh()
        {
            // 다음 날로 버튼이 읽지 않은 소식을 열어 달라고 했으면 그것을
            if (G.InboxFocus != null) { sel = G.InboxFocus; G.InboxFocus = null; }
            if (sel == null || S.inbox.All(m => m.id != sel)) sel = S.inbox.FirstOrDefault()?.id;
            double treasury = S.ludi[S.playerLudusId].treasury;

            // 결정할 일: 사건 카드 + (FM식) 받은 이적 제안·협상 역제안
            var offers = Transfers.OpenOffers(S);
            var decisions = S.decisions?.ToList() ?? new System.Collections.Generic.List<Decision>();
            int events = S.pendingEvents.Count;
            bool offerCard = OlympicaLogic.Current(S)?.offer == true;
            cards.Sync(events + offers.Count + decisions.Count + (offerCard ? 1 : 0), (card, i) =>
            {
                if (i >= events + offers.Count + decisions.Count) { BindOlympica(card); return; }
                if (i >= events) { BindDeal(card, i - events < offers.Count ? offers[i - events] : null, i - events < offers.Count ? null : decisions[i - events - offers.Count]); return; }
                var p = S.pendingEvents[i];
                var def = Events.GetEvent(p.eventId);
                card.Find("Title").GetComponent<TMP_Text>().text = $"결정을 내려야 합니다 — {p.title}";
                card.Find("Text").GetComponent<TMP_Text>().text = p.text;
                var choiceTemplate = card.Find("Choices/ChoiceTemplate").GetComponent<Button>();
                var choices = ChoiceList(card, choiceTemplate);
                choices.Sync(def.choices.Count, (b, ci) =>
                {
                    var c = def.choices[ci];
                    b.GetComponentInChildren<TMP_Text>().text = c.label;
                    b.interactable = !(c.cost is double cost && cost != 0 && treasury < cost);
                    b.onClick.RemoveAllListeners();
                    string pid = p.id;
                    int idx = ci;
                    b.onClick.AddListener(() => G.Act((d, rng) => Events.ResolveEvent(d, rng, pid, idx)));
                });
            });

            items.Sync(S.inbox.Count, (b, i) =>
            {
                var m = S.inbox[i];
                var title = b.transform.Find("Title").GetComponent<TMP_Text>();
                title.text = m.title;
                title.fontStyle = m.read ? FontStyles.Normal : FontStyles.Bold;
                title.color = m.kind == "death" ? new Color32(0xe0, 0x8b, 0x80, 0xff) : Theme.Parchment;
                b.transform.Find("Meta").GetComponent<TMP_Text>().text = $"시즌 {m.season} · {Calendar.DateLabel(m.day)}";
                bool on = m.id == sel;
                b.GetComponent<Image>().color = on ? Theme.Panel2 : new Color(0, 0, 0, 0);
                b.transform.Find("Accent").gameObject.SetActive(on);
                b.onClick.RemoveAllListeners();
                string id = m.id;
                b.onClick.AddListener(() => Open(id));
            });

            var msg = S.inbox.FirstOrDefault(m => m.id == sel);
            readTitle.text = msg != null ? Tips.AutoLink(S, msg.title) : "";
            readMeta.text = msg != null ? $"시즌 {msg.season} · {Calendar.DateLabel(msg.day)}" : "";
            readBody.text = msg != null ? Tips.AutoLink(S, msg.body) + (msg.replay != null ? $"\n\n<link=\"{msg.replay}\"><color={Theme.Hex(Theme.Torch)}><u>▶ 이 전투 보기</u></color></link>" : "") : "소식이 없습니다.";
        }

        /// <summary>올림피카 컵 대표팀 감독 제안 카드</summary>
        void BindOlympica(RectTransform card)
        {
            card.Find("Title").GetComponent<TMP_Text>().text = $"결정을 내려야 합니다 — {Tips.Nation(S.nation)} 대표팀 감독 제안";
            card.Find("Text").GetComponent<TMP_Text>().text = $"{Tips.Olympica()}에서 대표팀을 맡아 달라고 합니다. 맡으면 대표 7명을 직접 뽑고 경기마다 출전 5명을 정합니다.";
            var choices = ChoiceList(card, card.Find("Choices/ChoiceTemplate").GetComponent<Button>());
            var acts = new (string label, System.Action act)[]
            {
                ("대표팀 맡기", () => { G.Mutate(OlympicaLogic.Accept); G.Go("olympica"); }),
                ("사양하기", () => G.Mutate(OlympicaLogic.Decline)),
            };
            choices.Sync(acts.Length, (b, ci) =>
            {
                b.GetComponentInChildren<TMP_Text>().text = acts[ci].label;
                b.interactable = true;
                b.onClick.RemoveAllListeners();
                var a = acts[ci].act;
                b.onClick.AddListener(() => a());
            });
        }

        /// <summary>받은 이적 제안 / 협상 역제안 카드: 협상 창을 열거나 바로 답한다</summary>
        void BindDeal(RectTransform card, Offer o, Decision d)
        {
            var choiceTemplate = card.Find("Choices/ChoiceTemplate").GetComponent<Button>();
            var choices = ChoiceList(card, choiceTemplate);
            var acts = new System.Collections.Generic.List<(string label, System.Action act)>();
            if (o != null)
            {
                var g = S.gladiators[o.gladiatorId];
                card.Find("Title").GetComponent<TMP_Text>().text = $"결정을 내려야 합니다 — 이적 제안: {Tips.Name(g)}";
                card.Find("Text").GetComponent<TMP_Text>().text = $"{Tips.Ludus(S.ludi[o.fromLudus])}에서 이적료 {o.fee:N0} D를 제시했습니다. 받거나, 거절하거나, 값을 다시 불러야 다음 날로 넘어갈 수 있습니다.";
                string oid = o.id;
                acts.Add(("협상 창 열기", () => NegotiationPopup.OpenOffer(oid)));
                acts.Add(($"수락 ({o.fee:N0} D)", () => G.Mutate(s => Transfers.AcceptOffer(s, oid))));
                acts.Add(("거절", () => G.Mutate(s => Transfers.RejectOffer(s, oid))));
            }
            else
            {
                card.Find("Title").GetComponent<TMP_Text>().text = $"결정을 내려야 합니다 — {Negotiations.DecisionTitle(S, d)}";
                card.Find("Text").GetComponent<TMP_Text>().text = $"{d.line}\n다시 제안하거나 협상에서 물러서야 다음 날로 넘어갈 수 있습니다.";
                string did = d.id;
                acts.Add(("협상 창 열기", () => NegotiationPopup.OpenDecision(did)));
                acts.Add(("협상에서 물러서기", () => G.Mutate(s => Negotiations.Withdraw(s, did))));
            }
            choices.Sync(acts.Count, (b, ci) =>
            {
                b.GetComponentInChildren<TMP_Text>().text = acts[ci].label;
                b.interactable = true;
                b.onClick.RemoveAllListeners();
                var a = acts[ci].act;
                b.onClick.AddListener(() => a());
            });
        }

        void Open(string id)
        {
            sel = id;
            G.Mutate(d =>
            {
                var m = d.inbox.FirstOrDefault(x => x.id == id);
                if (m != null) m.read = true;
            });
        }

        // 카드마다 선택지 목록을 하나씩 들고 있는다
        readonly System.Collections.Generic.Dictionary<RectTransform, TemplateList<Button>> choiceLists = new();
        TemplateList<Button> ChoiceList(RectTransform card, Button template)
        {
            if (!choiceLists.TryGetValue(card, out var l)) choiceLists[card] = l = new TemplateList<Button>(template);
            return l;
        }
    }
}
