// 연무장 화면 조립 (웹판 TrainingScreen.tsx). 드롭다운 펼침 목록이 잘리지 않게 스크롤 영역은 쓰지 않는다.
using Colosseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class TrainingBuilder
    {
        // (이름, 머리글, 폭)  폭 0 = 남는 폭
        static readonly (string id, string label, float w)[] Cols =
        {
            ("Name", "검투사", 200), ("Style", "스타일", 110), ("Focus", "훈련 내용", 150), ("Intensity", "강도", 110), ("Fitness", "체력", 76),
            ("Sharp", "경기감각", 104), ("Wear", "혹사도", 104), ("Doctrine", "교범 숙련", 76), ("Perf", "경기력", 64), ("Status", "상태", 0),
        };

        public static ScreenBase Build(Transform main)
        {
            var rt = Rect("Screen_training", main).Stretch(26, 20, 26, 20);
            VCol(rt, 14);
            ScreenHeader(rt, "연무장", "훈련 성과는 매일 조금씩 쌓입니다. 강도를 높이면 빨리 성장하지만 쉽게 지치고 다칩니다. 훈련전과 미시오, 사투 같은 실전에서도 능력치가 오릅니다.");

            var bulk = TitledPanel("Bulk", rt, "한꺼번에 지시");
            var br = Rect("Row", bulk);
            HRow(br, 10);
            Text("Hint", br, "모두에게", 14, Theme.TextDim).Layout(prefW: 70, flexW: 0);
            var allFocus = Dropdown("AllFocus", br, 160);
            var allInt = Dropdown("AllIntensity", br, 120);

            var table = Panel("Table", rt);
            VCol(table.transform, 0, 1, 1, 1, 1);
            table.transform.Find("Fill").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var head = Box("Head", table.transform, Theme.Bg2).Layout(minH: 30, prefH: 30);
            HRow(head.transform, 6, 8, 8);
            foreach (var c in Cols)
                Cell(Text(c.id, head.transform, c.label, 13, Theme.TextDim, false, FontStyles.Bold, c.id == "Perf" ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft).rectTransform, c.w);
            Box("Line", table.transform, Theme.Line).Layout(minH: 1, prefH: 1);

            var row = Rect("RowTemplate", table.transform).Layout(minH: 40, prefH: 40);
            HRow(row, 6, 8, 8);
            var line = Box("Line", row, new Color(Theme.Line.r, Theme.Line.g, Theme.Line.b, 0.6f));
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            line.rectTransform.Anchor(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));
            foreach (var c in Cols)
            {
                switch (c.id)
                {
                    case "Name":
                        var name = Box("Name", row, new Color(0, 0, 0, 0));
                        name.gameObject.AddComponent<Button>().targetGraphic = name;
                        Cell(name.rectTransform, c.w);
                        HRow(name.transform, 8);
                        Portrait("Portrait", name.transform, 28);
                        Text("Text", name.transform, "이름", 15, Theme.Parchment, false, FontStyles.Bold).Layout(flexW: 1);
                        break;
                    case "Focus": case "Intensity":
                        Dropdown(c.id, row, c.w);
                        break;
                    case "Fitness": case "Sharp": case "Wear":
                        var holder = Rect(c.id, row);
                        Cell(holder, c.w);
                        HRow(holder, 6);
                        Bar("Bar", holder);
                        if (c.id != "Fitness") Text("Value", holder, "0", 13, Theme.Text).Layout(flexW: 1);
                        break;
                    default:
                        var t = Text(c.id, row, "", 14, Theme.Text, false, FontStyles.Normal, c.id == "Perf" ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft);
                        t.textWrappingMode = TextWrappingModes.NoWrap;
                        Cell(t.rectTransform, c.w);
                        break;
                }
            }
            row.gameObject.SetActive(false);

            var s = rt.gameObject.AddComponent<TrainingScreen>();
            s.screenId = "training";
            Set(s, "allFocus", allFocus);
            Set(s, "allIntensity", allInt);
            Set(s, "rowTemplate", row);
            rt.gameObject.SetActive(false);
            return s;
        }

        static void Cell(RectTransform rt, float w)
        {
            if (w > 0) rt.Layout(minW: w, prefW: w, flexW: 0);
            else rt.Layout(minW: 120, flexW: 1);
        }
    }
}
