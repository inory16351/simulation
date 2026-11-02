// 막사 화면 조립 (웹판 RosterScreen.tsx + styles.css table.t)
using Colosseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class RosterBuilder
    {
        public static ScreenBase Build(Transform main)
        {
            var rt = Rect("Screen_roster", main).Stretch(26, 20, 26, 20);
            var content = ScrollView("Scroll", rt, out var scroll, 14);
            scroll.Stretch();
            ScreenHeader(content, "막사", "검투사 0명");
            var sub = content.Find("Sub").GetComponent<TMP_Text>();

            var main0 = Panel("Table", content);
            VCol(main0.transform, 0, 1, 1, 1, 1);
            main0.transform.Find("Fill").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var table = GladiatorTableKit.Build(main0.transform, true);

            // 의무실
            var inf = TitledPanel("Infirmary", content, "의무실 — 부상자", 0);
            var row = Box("InjuredTemplate", inf, new Color(0, 0, 0, 0)).Layout(minH: 38, prefH: 38);
            var rb = row.gameObject.AddComponent<Button>();
            rb.targetGraphic = row;
            HRow(row.transform, 12, 4, 8);
            Portrait("Portrait", row.transform, 26);
            Text("Name", row.transform, "이름", 15, Theme.Parchment, false, FontStyles.Bold).Layout(prefW: 140);
            Text("Injury", row.transform, "✚ 부상", 13, new Color32(0xf0, 0xb3, 0xaa, 0xff)).Layout(prefW: 200);
            Bar("Bar", row.transform);
            Text("Hint", row.transform, "", 13, Theme.TextDim).Layout(flexW: 1);
            row.gameObject.SetActive(false);

            // 모래에 묻힌 자들
            var fallenPad = TitledPanel("Fallen", content, "모래에 묻힌 자들", 0);
            ((RectTransform)fallenPad).GetComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            var fallen = GladiatorTableKit.Build(fallenPad, false);

            var s = rt.gameObject.AddComponent<RosterScreen>();
            s.screenId = "roster";
            Set(s, "sub", sub);
            Set(s, "table", table);
            Set(s, "infirmary", inf.parent.gameObject);
            Set(s, "injuredTemplate", rb);
            Set(s, "fallenPanel", fallenPad.parent.gameObject);
            Set(s, "fallenTable", fallen);
            rt.gameObject.SetActive(false);
            return s;
        }
    }

    /// <summary>검투사 표 조립: 머리줄(정렬 버튼) + 줄 원본. 칸 폭은 머리줄과 줄이 같은 표를 쓴다</summary>
    public static class GladiatorTableKit
    {
        // (이름, 머리글, 폭, 정렬 키)  폭 0 = 남는 폭
        static readonly (string id, string label, float w, string sort)[] Cols =
        {
            ("Name", "이름", 210, "name"), ("Race", "종족", 70, null), ("Age", "나이", 86, "age"), ("Style", "스타일", 96, null),
            ("Ca", "현재", 80, "ca"), ("Pa", "잠재", 120, null), ("Morale", "사기", 70, "morale"), ("Fatigue", "체력", 70, null),
            ("Sharp", "감각", 70, null), ("Wear", "혹사", 70, null), ("Status", "상태", 0, null), ("Record", "전적", 56, null), ("Wage", "주급", 70, "wage"),
        };

        public static GladiatorTable Build(Transform parent, bool showPa)
        {
            var root = Rect("GladiatorTable", parent);
            VCol(root, 0);
            var head = Box("Head", root, Theme.Bg2).Layout(minH: 30, prefH: 30);
            HRow(head.transform, 0, 8, 8);
            var sorts = new System.Collections.Generic.List<Button>();
            foreach (var c in Cols)
            {
                if (c.id == "Pa" && !showPa) continue;
                var align = c.id is "Record" or "Wage" ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
                if (c.sort != null)
                {
                    var cell = Rect("Sort_" + c.sort, head.transform);
                    Cell(cell, c.w);
                    var img = cell.gameObject.AddComponent<Image>();
                    img.color = new Color(0, 0, 0, 0);
                    var b = cell.gameObject.AddComponent<Button>();
                    b.targetGraphic = img;
                    var t = Text("Label", cell, c.label, 13, Theme.TextDim, false, FontStyles.Bold, align);
                    ((RectTransform)t.transform).Stretch(0, 0, 6, 0);
                    sorts.Add(b);
                }
                else Cell(Text(c.id, head.transform, c.label, 13, Theme.TextDim, false, FontStyles.Bold, align).rectTransform, c.w);
            }
            Box("Line", root, Theme.Line).Layout(minH: 1, prefH: 1);

            var row = Box("RowTemplate", root, new Color(0, 0, 0, 0)).Layout(minH: 40, prefH: 40);
            var rb = row.gameObject.AddComponent<Button>();
            rb.targetGraphic = row;
            HRow(row.transform, 0, 8, 8);
            var rowLine = Box("Line", row.transform, new Color(Theme.Line.r, Theme.Line.g, Theme.Line.b, 0.6f));
            rowLine.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            rowLine.rectTransform.Anchor(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));
            foreach (var c in Cols)
            {
                if (c.id == "Pa" && !showPa) continue;
                switch (c.id)
                {
                    case "Name":
                        var name = Rect("Name", row.transform);
                        Cell(name, c.w);
                        HRow(name, 8);
                        Portrait("Portrait", name, 30);
                        var col = Rect("Text", name);
                        VCol(col, 0).childAlignment = TextAnchor.MiddleLeft;
                        col.Layout(flexW: 1);
                        Text("Name", col, "이름", 15, Theme.Parchment, false, FontStyles.Bold);
                        Text("Nick", col, "\"별명\"", 12, Theme.TextDim, false, FontStyles.Italic);
                        break;
                    case "Morale": case "Fatigue": case "Sharp": case "Wear":
                        var holder = Rect(c.id, row.transform);
                        Cell(holder, c.w);
                        HRow(holder, 0);
                        Bar("Bar", holder);
                        break;
                    default:
                        var align = c.id is "Record" or "Wage" ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
                        var size = c.id is "Ca" or "Pa" ? 13 : 14;
                        var t = Text(c.id, row.transform, "", size, Theme.Text, false, FontStyles.Normal, align);
                        t.textWrappingMode = TextWrappingModes.NoWrap;
                        Cell(t.rectTransform, c.w);
                        break;
                }
            }
            row.gameObject.SetActive(false);

            var table = root.gameObject.AddComponent<GladiatorTable>();
            var so = new UnityEditor.SerializedObject(table);
            so.FindProperty("showPa").boolValue = showPa;
            so.FindProperty("rowTemplate").objectReferenceValue = rb;
            var sb = so.FindProperty("sortButtons");
            sb.arraySize = sorts.Count;
            for (int i = 0; i < sorts.Count; i++) sb.GetArrayElementAtIndex(i).objectReferenceValue = sorts[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return table;
        }

        static void Cell(RectTransform rt, float w)
        {
            if (w > 0) rt.Layout(minW: w, prefW: w, flexW: 0);
            else rt.Layout(minW: 120, flexW: 1);
        }
    }
}
