// 전령의 서판 화면 조립 (웹판 InboxScreen.tsx + styles.css .inbox, .event-card)
using Colosseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class InboxBuilder
    {
        public static ScreenBase Build(Transform main)
        {
            var rt = Rect("Screen_inbox", main).Stretch(26, 20, 26, 20);
            var col = VCol(rt, 12);
            col.childForceExpandHeight = false;
            ScreenHeader(rt, "전령의 서판", "전령이 가져온 소식과 경기 결과가 여기 쌓인다.");

            // 결정할 일: 카드 원본 (비활성)
            var events = Rect("Events", rt);
            VCol(events, 10);
            var card = Panel("EventCardTemplate", events, Theme.Panel);
            VCol(card.transform, 8, 16, 16, 12, 14);
            Text("Title", card.transform, "결정이 필요하다", 15, Theme.Torch, false, FontStyles.Bold);
            Text("Text", card.transform, "", 16, Theme.Text);
            var choices = Rect("Choices", card.transform);
            HRow(choices, 10);
            Button("ChoiceTemplate", choices, "선택지").Layout(prefW: 220, prefH: 40);
            choices.Find("ChoiceTemplate").gameObject.SetActive(false);
            card.gameObject.SetActive(false);

            // 목록 + 본문
            var body = Rect("Body", rt).Layout(flexH: 1, minH: 420);
            HRow(body, 14).childForceExpandHeight = true;
            var listPanel = Panel("List", body).Layout(minW: 380, prefW: 380, flexW: 0);
            var content = ScrollView("Scroll", listPanel.transform, out var scroll);
            scroll.Stretch(1, 1, 1, 1);
            var item = Box("ItemTemplate", content, new Color(0, 0, 0, 0)).Layout(minH: 58, prefH: 58);
            var btn = item.gameObject.AddComponent<Button>();
            btn.targetGraphic = item;
            var accent = Box("Accent", item.transform, Theme.Torch);
            accent.rectTransform.Anchor(Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(4, 0));
            accent.gameObject.SetActive(false);
            Text("Title", item.transform, "소식", 16, Theme.Parchment).rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -32), new Vector2(-10, -8));
            Text("Meta", item.transform, "시즌 1 · 서리달 1일", 13, Theme.TextFaint).rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -52), new Vector2(-10, -32));
            Box("Line", item.transform, Theme.Line).rectTransform.Anchor(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));
            item.gameObject.SetActive(false);

            var read = Panel("Read", body).Layout(flexW: 1);
            var readContent = ScrollView("Scroll", read.transform, out var readScroll, 8);
            readScroll.Stretch(1, 1, 1, 1);
            readContent.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(20, 20, 18, 18);
            var rTitle = Text("Title", readContent, "", 22, Theme.Parchment, true, FontStyles.Bold);
            var rMeta = Text("Meta", readContent, "", 13, Theme.TextFaint);
            var rBody = Text("Body", readContent, "", 16, Theme.Text);
            rBody.lineSpacing = 30;

            var s = rt.gameObject.AddComponent<InboxScreen>();
            s.screenId = "inbox";
            Set(s, "eventCardTemplate", card.rectTransform);
            Set(s, "itemTemplate", btn);
            Set(s, "readTitle", rTitle);
            Set(s, "readMeta", rMeta);
            Set(s, "readBody", rBody);
            rt.gameObject.SetActive(false);
            return s;
        }
    }
}
