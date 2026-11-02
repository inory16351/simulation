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
            if (sel == null || S.inbox.All(m => m.id != sel)) sel = S.inbox.FirstOrDefault()?.id;
            double treasury = S.ludi[S.playerLudusId].treasury;

            cards.Sync(S.pendingEvents.Count, (card, i) =>
            {
                var p = S.pendingEvents[i];
                var def = Events.GetEvent(p.eventId);
                card.Find("Title").GetComponent<TMP_Text>().text = $"결정이 필요하다 — {p.title}";
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
            readTitle.text = msg?.title ?? "";
            readMeta.text = msg != null ? $"시즌 {msg.season} · {Calendar.DateLabel(msg.day)}" : "";
            readBody.text = msg?.body ?? "소식이 없다.";
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
