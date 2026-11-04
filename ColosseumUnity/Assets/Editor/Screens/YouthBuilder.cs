// 유소년 학당 화면 조립 (웹판 YouthScreen.tsx). 화면 전체가 스크롤 영역이고, 모래 시험 응시자 표(학당 서약)와 학생 표가 있다.
using Colosseum.Core;
using Colosseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class YouthBuilder
    {
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);
        // (이름, 머리글, 폭)  폭 0 = 남는 폭
        static readonly (string id, string label, float w)[] Cols =
        {
            ("Name", "이름", 200), ("Race", "종족", 70), ("Age", "나이", 150), ("Style", "스타일", 0), ("Ca", "현재", 80),
            ("Pa", "추정 잠재", 130), ("Focus", "훈련", 140), ("Intensity", "강도", 100), ("Act", "", 250),
        };
        static readonly (string id, string label, float w)[] TrialCols =
        {
            ("Name", "이름", 200), ("Race", "종족", 70), ("Age", "나이", 150), ("Style", "스타일", 0), ("Ca", "현재", 80),
            ("Pa", "추정 잠재", 130), ("Act", "", 140),
        };

        public static ScreenBase Build(Transform main)
        {
            var rt = Rect("Screen_youth", main).Stretch(26, 20, 26, 20);
            // 내용이 늘어도 넘치지 않게 화면 전체를 스크롤 영역으로
            var page = ScrollView("Scroll", rt, out var pageScroll, 14);
            pageScroll.Stretch();
            ScreenHeader(page, "유소년 학당", "");
            var sub = page.Find("Sub").GetComponent<TMP_Text>();
            var msg = Text("Msg", page, "", 14, Neg);

            // 학당이 없을 때
            var none = Panel("None", page);
            VCol(none.transform, 10, 14, 14, 14, 14);
            none.transform.Find("Fill").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Text("Text", none.transform, $"학당이 아직 없습니다. <b>증축</b>에서 유소년 학당을 지으면 매 시즌 {Youth.TrialDate}에 모래 시험이 열립니다. 후보를 골라 학당 서약을 협상합니다.", 15);
            var goRow = Rect("Buttons", none.transform);
            HRow(goRow, 0);
            var goFac = Button("GoFacilities", goRow, "증축으로", true, 15).Layout(prefW: 140, prefH: 34);

            // 모래 시험 응시자 (FM식): 골라서 학당 서약
            var trialPad = TitledPanel("Trial", page, "모래 시험 응시자", 0);
            var trialTitle = trialPad.parent.Find("Head/Text").GetComponent<TMP_Text>();
            var thead = Box("Head", trialPad, Theme.Bg2).Layout(minH: 30, prefH: 30);
            HRow(thead.transform, 6, 8, 8);
            foreach (var c in TrialCols)
            {
                var t = Text(c.id, thead.transform, c.label, 13, Theme.TextDim, false, FontStyles.Bold);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                Cell(t.rectTransform, c.w);
            }
            var trow = Rect("TrialTemplate", trialPad).Layout(minH: 40, prefH: 40);
            HRow(trow, 6, 8, 8);
            foreach (var c in TrialCols)
            {
                if (c.id == "Name")
                {
                    var nm = Box("Name", trow, new Color(0, 0, 0, 0));
                    nm.gameObject.AddComponent<Button>().targetGraphic = nm;
                    Cell(nm.rectTransform, c.w);
                    HRow(nm.transform, 8);
                    Portrait("Portrait", nm.transform, 30);
                    Text("Text", nm.transform, "이름", 15, Theme.Parchment, false, FontStyles.Bold).Layout(flexW: 1);
                }
                else if (c.id == "Act")
                {
                    var act = Rect("Act", trow);
                    Cell(act, c.w);
                    HRow(act, 6);
                    Button("Sign", act, "계약 협상", true, 14).Layout(minW: 120, prefW: 120, prefH: 30, flexW: 0);
                }
                else
                {
                    var t = Text(c.id, trow, "", c.id is "Ca" or "Pa" ? 13 : 14, c.id is "Ca" or "Pa" ? Theme.Torch : Theme.Text);
                    t.textWrappingMode = TextWrappingModes.NoWrap;
                    Cell(t.rectTransform, c.w);
                }
            }
            trow.gameObject.SetActive(false);

            // 학생 표
            var table = Panel("Table", page);
            VCol(table.transform, 0, 1, 1, 1, 1);
            table.transform.Find("Fill").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var head = Box("Head", table.transform, Theme.Bg2).Layout(minH: 30, prefH: 30);
            HRow(head.transform, 6, 8, 8);
            foreach (var c in Cols)
            {
                var t = Text(c.id, head.transform, c.label, 13, Theme.TextDim, false, FontStyles.Bold);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                Cell(t.rectTransform, c.w);
            }
            Box("Line", table.transform, Theme.Line).Layout(minH: 1, prefH: 1);
            var empty = Text("Empty", table.transform, $"아직 학생이 없습니다. 모래 시험은 매 시즌 {Youth.TrialDate}입니다.", 14, Theme.TextDim);
            empty.margin = new Vector4(12, 0, 12, 0);
            empty.Layout(minH: 40, prefH: 40);

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
                        Portrait("Portrait", name.transform, 30);
                        Text("Text", name.transform, "이름", 15, Theme.Parchment, false, FontStyles.Bold).Layout(flexW: 1);
                        break;
                    case "Focus": case "Intensity":
                        Dropdown(c.id, row, c.w);
                        break;
                    case "Act":
                        var act = Rect("Act", row);
                        Cell(act, c.w);
                        HRow(act, 6);
                        Button("Promote", act, "1군으로 올리기", true, 14).Layout(minW: 96, prefW: 96, prefH: 30, flexW: 0);
                        var rel = Button("Release", act, "내보내기", false, 14).Layout(minW: 140, prefW: 140, prefH: 30, flexW: 0);
                        rel.GetComponent<Image>().color = Theme.Blood;
                        break;
                    default:
                        var t = Text(c.id, row, "", c.id is "Ca" or "Pa" ? 13 : 14, c.id is "Ca" or "Pa" ? Theme.Torch : Theme.Text);
                        t.textWrappingMode = TextWrappingModes.NoWrap;
                        Cell(t.rectTransform, c.w);
                        break;
                }
            }
            row.gameObject.SetActive(false);

            var s = rt.gameObject.AddComponent<YouthScreen>();
            s.screenId = "youth";
            Set(s, "sub", sub);
            Set(s, "msg", msg);
            Set(s, "noneRoot", none.gameObject);
            Set(s, "goFacilities", goFac);
            Set(s, "tableRoot", table.gameObject);
            Set(s, "empty", empty.gameObject);
            Set(s, "rowTemplate", row);
            Set(s, "trialRoot", trialPad.parent.gameObject); Set(s, "trialTitle", trialTitle); Set(s, "trialTemplate", trow);
            rt.gameObject.SetActive(false);
            return s;
        }

        static void Cell(RectTransform rt, float w)
        {
            if (w > 0) rt.Layout(minW: w, prefW: w, flexW: 0);
            else rt.Layout(minW: 80, flexW: 1);
        }
    }
}
