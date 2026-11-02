// 식솔 화면 조립 (웹판 StaffScreen.tsx + styles.css .staff-grid .staff-card .search-filters .negotiate).
// 자리 패널 넷은 고정이라 빌더가 바로 만든다. 검색 조건 드롭다운은 스크롤 밖, 후보 표만 스크롤한다.
using System.Collections.Generic;
using Colosseum.Core;
using Colosseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class StaffBuilder
    {
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);
        // 폭 0 = 남는 폭
        static readonly (string label, float w)[] Cols = { ("이름", 250), ("자리", 90), ("실력", 130), ("협상", 380), ("흥정", 0) };

        public static ScreenBase Build(Transform main)
        {
            var rt = Rect("Screen_staff", main).Stretch(26, 20, 26, 20);
            VCol(rt, 12);
            ScreenHeader(rt, "식솔", "");
            var sub = rt.Find("Sub").GetComponent<TMP_Text>();
            sub.Layout(prefH: -1);

            var s = rt.gameObject.AddComponent<StaffScreen>();
            s.screenId = "staff";
            Set(s, "sub", sub);

            // 자리 넷 (2열, 웹판 .staff-grid)
            var grid = Rect("Roles", rt).Layout(flexH: 0);
            VCol(grid, 12);
            var pads = new List<Object>();
            for (int r = 0; r < 2; r++)
            {
                var line = Rect("Line" + r, grid).Layout(minH: 116, prefH: 116, flexH: 0);
                var h = HRow(line, 12, 0, 0, 0, 0, TextAnchor.UpperLeft);
                h.childForceExpandWidth = true;
                h.childForceExpandHeight = true;
                for (int c = 0; c < 2; c++) pads.Add(RolePanel(line, StaffLogic.Roles[r * 2 + c]));
            }
            SetList(s, "rolePads", pads);

            // 식솔 찾기
            var sp = TitledPanel("Search", rt, "식솔 찾기", 8);
            sp.Layout(flexH: 1);
            ((RectTransform)sp.parent).Layout(flexH: 1);
            var f = Rect("Filters", sp);
            HRow(f, 16);
            Set(s, "roleFilter", Dropdown("Select", Field(f, "자리"), 120));
            Set(s, "ratingFilter", Dropdown("Select", Field(f, "실력 ≥"), 110));
            var willing = Button("Willing", f, "◇ 올 마음이 있는 사람만", false, 14).Layout(minW: 220, prefW: 220, prefH: 30, flexW: 0);
            Set(s, "willingToggle", willing);
            var rep = Text("Rep", f, "", 13, Theme.TextDim).Layout(minW: 0, flexW: 1);
            Set(s, "repHint", rep);
            Set(s, "msgText", Text("Msg", sp, "", 14, Neg));

            Head(sp);
            var content = ScrollView("Scroll", sp, out var scroll);
            scroll.Layout(flexH: 1);
            Set(s, "empty", Text("Empty", content, "조건에 맞는 사람이 없습니다. 후보는 이적 시장이 열릴 때 바뀝니다.", 14, Theme.TextDim).Layout(prefH: 36).gameObject);

            // 후보 줄 원본
            var row = Row("CandidateTemplate", content, 44);
            var name = Rect("Name", row);
            Cell(name, Cols[0].w);
            HRow(name, 8);
            Portrait("Portrait", name, 30);
            Text("Text", name, "이름", 15, Theme.Parchment, false, FontStyles.Bold).Layout(flexW: 1);
            Text("Age", name, "", 13, Theme.TextDim).Layout(flexW: 0);
            Cell(Text("Role", row, "", 14).rectTransform, Cols[1].w);
            var rating = Rect("Rating", row);
            Cell(rating, Cols[2].w);
            HRow(rating, 6);
            Bar("Bar", rating);
            Text("Value", rating, "", 14);
            var nego = Rect("Nego", row);
            Cell(nego, Cols[3].w);
            HRow(nego, 6);
            Text("Want", nego, "요구 주급 0 D", 13, Theme.TextDim).Layout(minW: 100, prefW: 100, flexW: 0);
            Input(nego, 90).name = "Wage";
            Button("Offer", nego, "고용 제안", true, 14).Layout(minW: 110, prefW: 110, prefH: 30, flexW: 0);
            var refuse = Text("Refuse", nego, "", 13, Neg);
            refuse.Layout(minW: 0, flexW: 1);
            Cell(Text("Mood", row, "", 13).rectTransform, Cols[4].w);
            row.gameObject.SetActive(false);
            Set(s, "rowTemplate", row);

            rt.gameObject.SetActive(false);
            return s;
        }

        /// <summary>자리 패널 하나 (웹판 .panel > .staff-card). 반환값은 내용 칸</summary>
        static RectTransform RolePanel(Transform line, string role)
        {
            var pad = TitledPanel("Role_" + role, line, StaffLogic.RoleNames[role], 8);
            pad.parent.Layout(minW: 0, prefW: 0, flexW: 1);
            Text("Desc", pad, StaffLogic.RoleDesc[role], 13, Theme.TextDim);
            var card = Rect("Card", pad);
            HRow(card, 12);
            Portrait("Portrait", card, 44);
            var info = Rect("Info", card);
            info.Layout(minW: 0, flexW: 1);
            VCol(info, 2);
            Text("Name", info, "", 15, Theme.Parchment, false, FontStyles.Bold);
            var rr = Rect("Rating", info);
            HRow(rr, 6);
            Text("Label", rr, "실력", 14);
            Bar("Bar", rr);
            Text("Value", rr, "", 14);
            Text("Hint", info, "", 13, Theme.TextDim);
            var fire = Button("Fire", card, "내보내기", false, 14).Layout(minW: 170, prefW: 170, prefH: 32, flexW: 0);
            fire.GetComponent<Image>().color = Theme.Blood;
            Text("Empty", pad, "비어 있습니다.", 14, Theme.TextDim);
            return pad;
        }

        /// <summary>웹판 .search-filters label: 작은 이름표 + 입력</summary>
        static RectTransform Field(Transform parent, string label)
        {
            var f = Rect("Field", parent);
            HRow(f, 6);
            Text("Label", f, label, 13, Theme.TextDim).textWrappingMode = TextWrappingModes.NoWrap;
            return f;
        }

        static TMP_InputField Input(Transform parent, float w)
        {
            var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.name = "Input";
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Theme.Bg2;
            var input = go.GetComponent<TMP_InputField>();
            foreach (var t in go.GetComponentsInChildren<TextMeshProUGUI>(true)) { t.font = Sans; t.fontSize = 14; t.color = Theme.Parchment; }
            input.placeholder.GetComponent<TMP_Text>().text = "주급";
            input.placeholder.GetComponent<TMP_Text>().color = Theme.TextFaint;
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.Layout(minW: w, prefW: w, minH: 30, prefH: 30, flexW: 0);
            return input;
        }

        static void Head(Transform parent)
        {
            var head = Box("Head", parent, Theme.Bg2).Layout(minH: 28, prefH: 28);
            HRow(head.transform, 8, 8, 8);
            foreach (var (label, w) in Cols)
            {
                var t = Text("H", head.transform, label, 13, Theme.TextDim, false, FontStyles.Bold);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                Cell(t.rectTransform, w);
            }
        }

        static RectTransform Row(string name, Transform parent, float h)
        {
            var row = Box(name, parent, new Color(0, 0, 0, 0)).Layout(minH: h, prefH: h);
            HRow(row.transform, 8, 8, 8);
            var line = Box("Line", row.transform, new Color(Theme.Line.r, Theme.Line.g, Theme.Line.b, 0.6f));
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            line.rectTransform.Anchor(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));
            return row.rectTransform;
        }

        static void Cell(RectTransform rt, float w)
        {
            if (w > 0) rt.Layout(minW: w, prefW: w, flexW: 0, minH: 30, prefH: 30);
            else rt.Layout(minW: 120, flexW: 1, minH: 30, prefH: 30);
        }

        static void SetList(Object target, string prop, List<Object> items)
        {
            var so = new UnityEditor.SerializedObject(target);
            var p = so.FindProperty(prop);
            p.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
