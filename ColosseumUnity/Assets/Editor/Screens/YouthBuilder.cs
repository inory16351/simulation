// 유소년 학당 화면 조립 (웹판 YouthScreen.tsx). 줄마다 훈련 드롭다운이 있어 스크롤 영역은 쓰지 않는다 (정원 최대 18명).
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

        public static ScreenBase Build(Transform main)
        {
            var rt = Rect("Screen_youth", main).Stretch(26, 20, 26, 20);
            VCol(rt, 14);
            ScreenHeader(rt, "유소년 학당", "");
            var sub = rt.Find("Sub").GetComponent<TMP_Text>();
            var msg = Text("Msg", rt, "", 14, Neg);

            // 학당이 없을 때
            var none = Panel("None", rt);
            VCol(none.transform, 10, 14, 14, 14, 14);
            none.transform.Find("Fill").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Text("Text", none.transform, "학당이 아직 없습니다. <b>증축</b>에서 유소년 학당을 지으면 매 시즌 서리달 1일에 아이들이 들어옵니다. 처음 지으면 바로 첫 입학생을 받습니다.", 15);
            var goRow = Rect("Buttons", none.transform);
            HRow(goRow, 0);
            var goFac = Button("GoFacilities", goRow, "증축으로", true, 15).Layout(prefW: 140, prefH: 34);

            // 학생 표
            var table = Panel("Table", rt);
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
            var empty = Text("Empty", table.transform, "아직 학생이 없습니다. 다음 입학일은 서리달 1일입니다.", 14, Theme.TextDim);
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
