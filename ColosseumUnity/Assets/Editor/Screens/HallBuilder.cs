// 비석의 언덕 화면 조립 (Unity판 전용): 탭 셋(연혁·역대 우승·비석), 탭마다 표 + 줄 원본(하이어라키에 비활성)
using System.Collections.Generic;
using Colosseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class HallBuilder
    {
        // (이름, 머리글, 폭)  폭 0 = 남는 폭
        static readonly (string id, string label, float w)[] ClubCols =
        {
            ("Season", "시즌", 80), ("League", "리그", 150), ("Rank", "순위", 80), ("Record", "승-패", 70), ("Rp", "RP", 70), ("Result", "결과", 120), ("Trophies", "우승컵", 0),
        };
        static readonly (string id, string label, float w)[] HonourCols = { ("Season", "시즌", 90), ("Winner", "우승", 0), ("RunnerUp", "준우승", 0) };
        static readonly (string id, string label, float w)[] GraveCols =
        {
            ("Name", "이름", 0), ("Race", "종족", 80), ("Age", "나이", 60), ("Record", "전적", 170), ("When", "죽은 날", 130), ("Cause", "사인", 220),
        };

        public static ScreenBase Build(Transform main)
        {
            var rt = Rect("Screen_hall", main).Stretch(26, 20, 26, 20);
            VCol(rt, 12);
            ScreenHeader(rt, "비석의 언덕", "지난 시즌들이 새겨지는 곳입니다. 루두스마다 시즌 성적과 우승컵, 대회마다 역대 우승, 모래 위에서 죽은 우리 검투사들의 비석이 남습니다.");
            var tabRow = Rect("Tabs", rt);
            HRow(tabRow, 4);
            var tabs = new List<Button>();
            foreach (var l in new[] { "연혁", "역대 우승", "비석" })
                tabs.Add(Button("Tab_" + tabs.Count, tabRow, l, false, 14).Layout(prefW: 128, prefH: 32));
            var body = Rect("Body", rt).Layout(flexH: 1, flexW: 1);
            var s = rt.gameObject.AddComponent<HallScreen>();
            s.screenId = "hall";
            var roots = new List<GameObject>();

            // ── 연혁 ──
            var club = Tab(body, "Club", roots);
            var bar = Rect("Pick", club).Layout(flexH: 0);
            HRow(bar, 8);
            Text("Label", bar, "루두스", 14, Theme.TextDim).Layout(prefW: 56, flexW: 0);
            Set(s, "clubPick", Dropdown("Club", bar, 300, 32));
            var cab = TitledPanel("Cabinet", club, "우승컵 진열장", 4);
            ((RectTransform)cab.parent).Layout(flexH: 0);
            var icons = Rect("Icons", cab);
            HRow(icons, 10);
            var iconT = Rect("IconTemplate", icons).Layout(prefW: 72, flexW: 0);
            VCol(iconT, 2).childAlignment = TextAnchor.UpperCenter;
            Sprite("Icon", iconT, "icon/trophy/pantheon", 64, 64);
            Text("Count", iconT, "", 13, Theme.Parchment, false, FontStyles.Bold, TextAlignmentOptions.Center);
            iconT.gameObject.SetActive(false);
            Set(s, "cabinetIconTemplate", iconT);
            Set(s, "cabinet", Text("Text", cab, "", 15, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.TopLeft));
            var (clubEmpty, clubRow) = Table(club, "Seasons", ClubCols, "아직 끝난 시즌이 없습니다. 시즌이 끝나면 성적이 여기에 새겨집니다.");
            Set(s, "clubEmpty", clubEmpty);
            Set(s, "clubTemplate", clubRow);

            // ── 역대 우승 ──
            var hon = Tab(body, "Honours", roots);
            var hbar = Rect("Pick", hon).Layout(flexH: 0);
            HRow(hbar, 8);
            Text("Label", hbar, "대회", 14, Theme.TextDim).Layout(prefW: 56, flexW: 0);
            Set(s, "compPick", Dropdown("Comp", hbar, 300, 32));
            var (honEmpty, honRow) = Table(hon, "Winners", HonourCols, "아직 우승한 루두스가 없습니다.");
            Set(s, "honourEmpty", honEmpty);
            Set(s, "honourTemplate", honRow);

            // ── 비석 ──
            var graves = Tab(body, "Graves", roots);
            var gt = Text("Title", graves, "", 15, Theme.Parchment, true, FontStyles.Bold).Layout(minH: 28, flexH: 0);
            Set(s, "graveTitle", gt);
            var (graveEmpty, graveRow) = Table(graves, "Graves", GraveCols, "아직 모래 위에서 잃은 검투사가 없습니다.");
            Set(s, "graveEmpty", graveEmpty);
            Set(s, "graveTemplate", graveRow);

            var so = new UnityEditor.SerializedObject(s);
            var tb = so.FindProperty("tabButtons");
            tb.arraySize = tabs.Count;
            for (int i = 0; i < tabs.Count; i++) tb.GetArrayElementAtIndex(i).objectReferenceValue = tabs[i];
            var tr = so.FindProperty("tabRoots");
            tr.arraySize = roots.Count;
            for (int i = 0; i < roots.Count; i++) tr.GetArrayElementAtIndex(i).objectReferenceValue = roots[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            rt.gameObject.SetActive(false);
            return s;
        }

        static RectTransform Tab(Transform body, string name, List<GameObject> roots)
        {
            var t = Rect(name, body).Stretch();
            VCol(t, 10).childForceExpandHeight = false;
            roots.Add(t.gameObject);
            return t;
        }

        /// <summary>머리글 + 스크롤 줄 목록. 줄 원본은 비활성, '없음' 안내 글 포함</summary>
        static (GameObject empty, RectTransform row) Table(Transform parent, string name, (string id, string label, float w)[] cols, string emptyText)
        {
            var table = Panel(name, parent);
            table.Layout(flexH: 1);
            VCol(table.transform, 0, 1, 1, 1, 1).childForceExpandHeight = false;
            table.transform.Find("Fill").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var head = Box("Head", table.transform, Theme.Bg2).Layout(minH: 30, prefH: 30, flexH: 0);
            HRow(head.transform, 10, 12, 12);
            foreach (var c in cols) Cell(Text(c.id, head.transform, c.label, 13, Theme.TextDim, false, FontStyles.Bold).rectTransform, c.w);
            Box("Line", table.transform, Theme.Line).Layout(minH: 1, prefH: 1, flexH: 0);
            var content = ScrollView("Scroll", table.transform, out var scrollRoot);
            scrollRoot.Layout(flexH: 1);
            var empty = Text("Empty", content, emptyText, 14, Theme.TextDim).Layout(minH: 40);
            var row = Box("RowTemplate", content, Color.clear).Layout(minH: 34, prefH: 34);
            HRow(row.transform, 10, 12, 12);
            var line = Box("Line", row.transform, new Color(Theme.Line.r, Theme.Line.g, Theme.Line.b, 0.6f));
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            line.rectTransform.Anchor(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));
            foreach (var c in cols)
            {
                var t = Text(c.id, row.transform, "", 14, c.id is "Season" or "Name" or "Winner" ? Theme.Parchment : Theme.Text);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.overflowMode = TextOverflowModes.Ellipsis;
                Cell(t.rectTransform, c.w);
            }
            // 이름 칸의 링크(검투사 팝업·종족 툴팁)는 줄 바탕을 레이캐스트로 잡아 TipLayer가 읽는다
            row.raycastTarget = true;
            row.gameObject.SetActive(false);
            return (empty.gameObject, row.rectTransform);
        }

        static void Cell(RectTransform rt, float w)
        {
            if (w > 0) rt.Layout(minW: w, prefW: w, flexW: 0);
            else rt.Layout(minW: 120, flexW: 1);
        }
    }
}
