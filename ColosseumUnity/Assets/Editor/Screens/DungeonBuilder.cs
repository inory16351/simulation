// 던전 화면 조립 (웹판 DungeonScreen.tsx + styles.css .dungeon-card .quest .formation .expedition-log).
// 드롭다운이 없어 화면 전체를 스크롤한다. 던전 카드·진형·원정대 줄·진행 중 원정 패널은 모두 비활성 원본을 복제한다.
using Colosseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class DungeonBuilder
    {
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);
        static readonly Color QuestLine = new Color32(0x8e, 0x2a, 0x22, 0xff);
        static readonly Color CardLine = new Color32(0x6b, 0x4a, 0x34, 0xff);
        static readonly Color CardFill = new Color32(0x30, 0x1f, 0x1d, 0xff);
        static readonly Color FormLine = new Color32(0x5a, 0x3f, 0x32, 0xff);
        static readonly Color FormFill = new Color32(0x2a, 0x1c, 0x1a, 0xff);

        public static ScreenBase Build(Transform main)
        {
            var rt = Rect("Screen_dungeon", main).Stretch(26, 20, 26, 20);
            var content = ScrollView("Scroll", rt, out var scroll, 14);
            scroll.Stretch();
            ScreenHeader(content, "던전", "");
            var sub = content.Find("Sub").GetComponent<TMP_Text>();

            // 나라의 토벌 의뢰
            var qp = TitledPanel("Quests", content, "나라의 토벌 의뢰 — 아주 어렵다", 6);
            var quest = Card("QuestTemplate", qp, QuestLine, new Color(0, 0, 0, 0), 2, true);
            var qinfo = Rect("Info", quest.transform).Layout(minW: 0, flexW: 1);
            VCol(qinfo, 2);
            Text("Title", qinfo, "", 15, Theme.Parchment, false, FontStyles.Bold);
            Text("Hint", qinfo, "", 13, Theme.TextDim);
            Button("Pick", quest.transform, "이 의뢰로 꾸리기", true, 14).Layout(minW: 150, prefW: 150, prefH: 32, flexW: 0);
            quest.gameObject.SetActive(false);

            // 던전 카드
            var grid = Rect("Dungeons", content);
            HRow(grid, 10, 0, 0, 0, 0, TextAnchor.UpperLeft).childForceExpandHeight = true;
            var card = Card("CardTemplate", grid, CardLine, CardFill, 3, false);
            card.Layout(minW: 0, prefW: 0, flexW: 1);
            Text("Name", card.transform, "던전", 17, Theme.Parchment, true, FontStyles.Bold);
            Text("Desc", card.transform, "", 13, Theme.TextDim);
            Text("Meta", card.transform, "", 12);
            Text("Loot", card.transform, "", 12);
            card.gameObject.AddComponent<CanvasGroup>();
            card.gameObject.SetActive(false);

            // 원정대 | 진형과 출발
            var cols = Rect("Cols", content);
            HRow(cols, 14, 0, 0, 0, 0, TextAnchor.UpperLeft);
            var partyPad = TitledPanel("Party", cols, "원정대", 0);
            partyPad.parent.Layout(minW: 0, prefW: 0, flexW: 1);
            var partyEmpty = Text("Empty", partyPad, "보낼 검투사가 없다.", 14, Theme.TextDim).Layout(prefH: 36);
            var prow = Box("RowTemplate", partyPad, new Color(0, 0, 0, 0)).Layout(minH: 38, prefH: 38);
            prow.gameObject.AddComponent<Button>().targetGraphic = prow;
            prow.gameObject.AddComponent<CanvasGroup>();
            HRow(prow.transform, 8, 8, 8);
            var pline = Box("Line", prow.transform, new Color(Theme.Line.r, Theme.Line.g, Theme.Line.b, 0.6f));
            pline.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            pline.rectTransform.Anchor(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));
            Cell(Text("Check", prow.transform, "□", 15, Theme.Torch, false, FontStyles.Normal, TextAlignmentOptions.Center).rectTransform, 22);
            var pname = Rect("Name", prow.transform);
            Cell(pname, 170);
            HRow(pname, 8);
            Portrait("Portrait", pname, 28);
            Text("Text", pname, "이름", 15, Theme.Parchment, false, FontStyles.Bold).Layout(flexW: 1);
            Cell(Text("Ca", prow.transform, "", 13, Theme.Torch).rectTransform, 76);
            Cell(Text("Why", prow.transform, "", 13, Theme.TextDim).rectTransform, 0);
            Cell(Text("Wage", prow.transform, "", 14, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.MidlineRight).rectTransform, 64);
            foreach (var t in prow.GetComponentsInChildren<TextMeshProUGUI>(true)) t.textWrappingMode = TextWrappingModes.NoWrap;
            prow.gameObject.SetActive(false);

            var goPad = TitledPanel("Go", cols, "진형과 출발", 8);
            goPad.parent.Layout(minW: 0, prefW: 0, flexW: 1);
            var form = Card("FormationTemplate", goPad, FormLine, FormFill, 2, false);
            form.GetComponent<VerticalLayoutGroup>().spacing = 3;
            form.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(12, 12, 8, 8);
            Text("Name", form.transform, "진형", 15, Theme.Parchment, false, FontStyles.Bold);
            Text("Desc", form.transform, "", 13, Theme.TextDim);
            form.gameObject.SetActive(false);
            var wages = Text("Wages", goPad, "", 13, Theme.TextDim);
            var msg = Text("Msg", goPad, "", 14, Neg);
            var goRow = Rect("GoRow", goPad);
            HRow(goRow, 0);
            var go = Button("Start", goRow, "원정 출발", true, 15).Layout(prefW: 200, prefH: 38);

            // 진행 중 원정 (패널째 복제)
            var ap = TitledPanel("ActiveTemplate", content, "진행 중", 6);
            var party = Rect("Party", ap);
            HRow(party, 6);
            for (int i = 0; i < 5; i++) Portrait("P" + i, party, 44);
            Text("Loot", ap, "", 14);
            var log = Rect("Log", ap);
            HRow(log, 10).childForceExpandHeight = true;
            Box("Bar", log, FormLine).Layout(minW: 3, prefW: 3, flexW: 0);
            Text("Text", log, "", 12).Layout(minW: 0, flexW: 1);
            ap.parent.gameObject.SetActive(false);

            // 지난 원정
            var dp = TitledPanel("Done", content, "지난 원정", 0);
            var drow = Box("DoneTemplate", dp, new Color(0, 0, 0, 0)).Layout(minH: 38);
            HRow(drow.transform, 8, 8, 8, 6, 6);
            var dline = Box("Line", drow.transform, new Color(Theme.Line.r, Theme.Line.g, Theme.Line.b, 0.6f));
            dline.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            dline.rectTransform.Anchor(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));
            Cell(Text("Dungeon", drow.transform, "", 14).rectTransform, 200);
            Cell(Text("Result", drow.transform, "", 14).rectTransform, 150);
            Text("Loot", drow.transform, "", 13).Layout(minW: 80, minH: 26, flexW: 1);
            drow.gameObject.SetActive(false);

            var s = rt.gameObject.AddComponent<DungeonScreen>();
            s.screenId = "dungeon";
            Set(s, "sub", sub);
            Set(s, "questPanel", qp.parent.gameObject);
            Set(s, "questTemplate", (RectTransform)quest.transform);
            Set(s, "cardTemplate", card.GetComponent<Button>());
            Set(s, "partyTitle", partyPad.parent.Find("Head/Text").GetComponent<TMP_Text>());
            Set(s, "partyEmpty", partyEmpty.gameObject);
            Set(s, "partyTemplate", prow.GetComponent<Button>());
            Set(s, "formationTemplate", form.GetComponent<Button>());
            Set(s, "wages", wages);
            Set(s, "msg", msg);
            Set(s, "start", go);
            Set(s, "activeTemplate", (RectTransform)ap.parent);
            Set(s, "donePanel", dp.parent.gameObject);
            Set(s, "doneTemplate", drow.rectTransform);
            rt.gameObject.SetActive(false);
            return s;
        }

        /// <summary>테두리 + 바탕이 있는 틀 (웹판 .dungeon-card / .formation / .quest).
        /// row면 내용을 가로로 놓는 틀만(의뢰 줄), 아니면 세로로 쌓고 누를 수 있게 Button을 붙인다</summary>
        static Image Card(string name, Transform parent, Color line, Color fill, float border, bool row)
        {
            var frame = Box(name, parent, line);
            var inner = Box("Fill", frame.transform, fill);
            ((RectTransform)inner.transform).Stretch(border, border, border, border);
            inner.raycastTarget = false;
            inner.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            if (row)
            {
                HRow(frame.transform, 12, 10, 10, 8, 8);
                return frame;
            }
            VCol(frame.transform, 6, 14, 14, 12, 12);
            var b = frame.gameObject.AddComponent<Button>();
            b.targetGraphic = frame;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            colors.disabledColor = new Color(1, 1, 1, 0.6f);
            b.colors = colors;
            return frame;
        }

        static void Cell(RectTransform rt, float w)
        {
            if (w > 0) rt.Layout(minW: w, prefW: w, flexW: 0, minH: 30, prefH: 30);
            else rt.Layout(minW: 80, flexW: 1, minH: 30, prefH: 30);
        }
    }
}
