// 금고 화면 조립 (웹판 FinanceScreen.tsx + styles.css .grid3 .big-num table.t). 장부 표만 스크롤한다.
using Colosseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class FinanceBuilder
    {
        // 폭 0 = 남는 폭
        static readonly (string id, string label, float w)[] Cols = { ("Date", "날짜", 200), ("Desc", "내용", 0), ("Amount", "금액", 140) };

        public static ScreenBase Build(Transform main)
        {
            var rt = Rect("Screen_finance", main).Stretch(26, 20, 26, 20);
            VCol(rt, 14);
            ScreenHeader(rt, "금고", "급료는 7일마다 나간다. 수입에는 세금 10%가 붙는다.");

            var s = rt.gameObject.AddComponent<FinanceScreen>();
            s.screenId = "finance";

            // 요약 패널 셋 (grid3)
            var grid = Rect("Grid", rt).Layout(flexH: 0);
            var gh = HRow(grid, 14, 0, 0, 0, 0, TextAnchor.UpperLeft);
            gh.childForceExpandWidth = true;
            gh.childForceExpandHeight = true;
            Summary(s, grid, "Treasury", "금고", "treasury", "treasuryHint");
            Summary(s, grid, "Weekly", "매주 나가는 돈", "weekly", "weeklyHint");
            Summary(s, grid, "Runway", "버틸 수 있는 기간", "runway", "runwayHint");

            // 밀린 급료
            var up = TitledPanel("Unpaid", rt, "밀린 급료", 6);
            Set(s, "unpaidRoot", up.parent.gameObject);
            Set(s, "unpaidTags", Text("Tags", up, "", 13));
            Text("Hint", up, "다음 급료일에 금고가 넉넉하면 밀린 몫까지 한꺼번에 나간다.", 13, Theme.TextDim);

            // 장부
            var lp = TitledPanel("Ledger", rt, "장부", 0);
            lp.Layout(flexH: 1);
            ((RectTransform)lp.parent).Layout(flexH: 1);
            var head = Box("Head", lp, Theme.Bg2).Layout(minH: 28, prefH: 28);
            HRow(head.transform, 8, 8, 8);
            foreach (var (id, label, w) in Cols)
                Cell(Text("H", head.transform, label, 13, Theme.TextDim, false, FontStyles.Bold,
                    id == "Amount" ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft).rectTransform, w);
            var content = ScrollView("Scroll", lp, out var scroll);
            scroll.Layout(flexH: 1);
            Set(s, "empty", Text("Empty", content, "장부가 비었다.", 14, Theme.TextDim).Layout(prefH: 36).gameObject);

            var row = Box("EntryTemplate", content, new Color(0, 0, 0, 0)).Layout(minH: 34, prefH: 34);
            HRow(row.transform, 8, 8, 8);
            var line = Box("Line", row.transform, new Color(Theme.Line.r, Theme.Line.g, Theme.Line.b, 0.6f));
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            line.rectTransform.Anchor(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));
            Cell(Text("Date", row.transform, "", 13, Theme.TextDim).rectTransform, Cols[0].w);
            Cell(Text("Desc", row.transform, "", 14).rectTransform, Cols[1].w);
            Cell(Text("Amount", row.transform, "", 14, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.MidlineRight).rectTransform, Cols[2].w);
            row.gameObject.SetActive(false);
            Set(s, "entryTemplate", row.rectTransform);

            rt.gameObject.SetActive(false);
            return s;
        }

        /// <summary>요약 패널 하나: 큰 숫자(웹판 .big-num) + 설명</summary>
        static void Summary(FinanceScreen s, Transform grid, string name, string title, string numProp, string hintProp)
        {
            var pad = TitledPanel(name, grid, title, 4);
            pad.parent.Layout(minW: 0, prefW: 0, flexW: 1);
            Set(s, numProp, Text("Num", pad, "0 D", 26, Theme.Parchment, false, FontStyles.Bold));
            Set(s, hintProp, Text("Hint", pad, "", 13, Theme.TextDim));
        }

        static void Cell(RectTransform rt, float w)
        {
            if (w > 0) rt.Layout(minW: w, prefW: w, flexW: 0, minH: 30, prefH: 30);
            else rt.Layout(minW: 120, flexW: 1, minH: 30, prefH: 30);
        }
    }
}
