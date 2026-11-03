// 판테온 컵 화면 조립 (웹판 CupScreen.tsx + styles.css .cup-groups, .bracket)
using System.Collections.Generic;
using Colosseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class CupBuilder
    {
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);

        public static ScreenBase Build(Transform main)
        {
            var rt = Rect("Screen_cup", main).Stretch(26, 20, 26, 20);
            var content = ScrollView("Scroll", rt, out var scroll, 14);
            scroll.Stretch();
            ScreenHeader(content, "판테온 컵", "국내 컵이 끝나면 열리는 대륙 대회입니다. 나라별 국내 컵 우승 6팀이 플레이오프(3팀씩 2개 조)를 치러 4팀이 오르고, 나라별 1부 1·2위 12팀과 함께 16강 조별 리그(4팀씩 4개 조)를 치릅니다. 조 상위 2팀이 8강에 오릅니다. 모든 경기는 5:5 미시오 단판이고, 리그 RP는 오가지 않습니다.");
            content.Find("Sub").GetComponent<TMP_Text>().Layout(prefH: 44);
            var (cupTrophy, cupStatus) = CompBuilder.Top(content);

            // 오늘의 컵 경기
            var todayPad = TitledPanel("Today", content, "오늘의 컵 경기", 8);
            var todayTitle = todayPad.parent.Find("Head/Text").GetComponent<TMP_Text>();
            var teams = Rect("Teams", todayPad);
            HRow(teams, 10);
            var home = Team("Home", teams, 220, 16);
            Text("Vs", teams, "vs", 14, Theme.TextDim).Layout(prefW: 24, flexW: 0);
            var away = Team("Away", teams, 220, 16);
            var lineup = Rect("Lineup", todayPad);
            HRow(lineup, 8);
            var slots = new List<TMP_Dropdown>();
            foreach (var p in new[] { "출전 1", "출전 2", "출전 3", "출전 4", "출전 5" })
            {
                var slot = Rect("Slot", lineup);
                VCol(slot, 2);
                Text("Label", slot, p, 12, Theme.TextDim);
                slots.Add(Dropdown("Pick", slot, 190));
            }
            var warn = Text("Warn", todayPad, "인원을 채우지 못하면 있는 인원으로만 싸웁니다.", 13, Neg);
            var speedRow = Rect("Speed", todayPad);
            HRow(speedRow, 4);
            Text("Label", speedRow, "중계 속도", 13, Theme.TextDim).Layout(prefW: 70, flexW: 0);
            var speeds = new List<Button>();
            foreach (var sp in MatchScreen.Speeds) speeds.Add(Button("Speed_" + sp.id, speedRow, sp.label, false, 14).Layout(prefW: 90, prefH: 30));
            var enterRow = Rect("EnterRow", todayPad);
            HRow(enterRow, 0);
            var enter = Button("Enter", enterRow, "경기장 들어가기", true, 15).Layout(prefW: 200, prefH: 40);

            // 나라 계수 | 우리 루두스
            var cols = Rect("Cols", content);
            HRow(cols, 14, 0, 0, 0, 0, TextAnchor.UpperLeft);
            var coefPad = TitledPanel("Coef", cols, "나라 계수", 0);
            coefPad.parent.Layout(minW: 0, prefW: 0, flexW: 1);
            Head(coefPad, ("나라", 0), ("최근 3시즌", 150), ("합계", 60));
            var coef = Row("CoefTemplate", coefPad);
            Cell(Team("Nation", coef, 0, 14), 0);
            Cell(Text("Recent", coef, "", 13, Theme.TextDim).rectTransform, 150);
            Cell(Text("Sum", coef, "", 14).rectTransform, 60);
            coef.gameObject.SetActive(false);
            Text("Note", coefPad, "대결에서 이기면 2점, 8강과 4강, 결승에 오를 때마다 1점씩, 우승하면 2점을 얻습니다. 출전한 루두스의 평균 점수가 그 시즌의 나라 계수입니다.", 13, Theme.TextDim).margin = new Vector4(0, 8, 0, 0);

            var ourPad = TitledPanel("Ours", cols, "우리 루두스", 0);
            ourPad.parent.Layout(minW: 0, prefW: 0, flexW: 1);
            var ourHint = Text("Hint", ourPad, "", 14, Theme.TextDim);
            var our = Row("OurTemplate", ourPad);
            Cell(Text("Date", our, "", 14).rectTransform, 100);
            Cell(Text("Stage", our, "", 14).rectTransform, 70);
            Cell(Team("Opp", our, 0, 14), 0);
            Cell(Text("Result", our, "", 14, Theme.Text, false, FontStyles.Bold).rectTransform, 60);
            our.gameObject.SetActive(false);

            // 플레이오프 두 조
            var poRow = Rect("Playoff", content);
            HRow(poRow, 14, 0, 0, 0, 0, TextAnchor.UpperLeft);
            var poPanels = new List<RectTransform>();
            for (int gi = 0; gi < 2; gi++)
            {
                var pad = TitledPanel("Po" + gi, poRow, "플레이오프", 0);
                pad.parent.Layout(minW: 0, prefW: 0, flexW: 1);
                Head(pad, ("루두스", 0), ("경기", 36), ("승", 30), ("득실", 40), ("승점", 40));
                var row = Row("RowTemplate", pad);
                Cell(Team("Team", row, 0, 13), 0);
                Cell(Text("Played", row, "", 13).rectTransform, 36);
                Cell(Text("Won", row, "", 13).rectTransform, 30);
                Cell(Text("Diff", row, "", 13).rectTransform, 40);
                Cell(Text("Pts", row, "", 13, Theme.Parchment).rectTransform, 40);
                row.gameObject.SetActive(false);
                poPanels.Add((RectTransform)pad.parent);
            }

            // 조별 순위
            var groups = Rect("Groups", content);
            HRow(groups, 14, 0, 0, 0, 0, TextAnchor.UpperLeft);
            var groupPanels = new List<RectTransform>();
            for (int gi = 0; gi < 4; gi++)
            {
                var pad = TitledPanel("Group" + gi, groups, "조", 0);
                pad.parent.Layout(minW: 0, prefW: 0, flexW: 1);
                Head(pad, ("루두스", 0), ("경기", 36), ("승", 30), ("득실", 40), ("승점", 40));
                var row = Row("RowTemplate", pad);
                Cell(Team("Team", row, 0, 13), 0);
                Cell(Text("Played", row, "", 13).rectTransform, 36);
                Cell(Text("Won", row, "", 13).rectTransform, 30);
                Cell(Text("Diff", row, "", 13).rectTransform, 40);
                Cell(Text("Pts", row, "", 13, Theme.Parchment).rectTransform, 40);
                row.gameObject.SetActive(false);
                groupPanels.Add((RectTransform)pad.parent);
            }

            // 토너먼트
            var bracketPad = TitledPanel("Bracket", content, "토너먼트", 0);
            var champ = Text("Champion", bracketPad, "", 15, Theme.Good, false, FontStyles.Bold);
            var bcols = Rect("Cols", bracketPad);
            HRow(bcols, 18, 0, 0, 0, 0, TextAnchor.UpperLeft);
            var bracketCols = new List<RectTransform>();
            for (int si = 0; si < 3; si++)
            {
                var col = Rect("Stage" + si, bcols).Layout(minW: 0, prefW: 0, flexW: 1);
                VCol(col, 8);
                Text("Title", col, "", 13, Theme.TextDim);
                Text("Empty", col, "미정", 13, Theme.TextDim);
                var tie = Panel("TieTemplate", col, Theme.Bg2);
                VCol(tie.transform, 2, 10, 10, 6, 6);
                tie.transform.Find("Fill").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                Team("Home", tie.transform, 0, 14).gameObject.AddComponent<CanvasGroup>();
                Team("Away", tie.transform, 0, 14).gameObject.AddComponent<CanvasGroup>();
                tie.gameObject.SetActive(false);
                bracketCols.Add(col);
            }

            var s = rt.gameObject.AddComponent<CupScreen>();
            s.screenId = "cup";
            Set(s, "match", main.GetComponentInChildren<MatchScreen>(true));
            Set(s, "todayPanel", todayPad.parent.gameObject);
            Set(s, "todayTitle", todayTitle);
            Set(s, "todayHome", home);
            Set(s, "todayAway", away);
            Set(s, "shortWarn", warn.gameObject);
            Set(s, "enter", enter);
            Set(s, "coefTemplate", coef);
            Set(s, "ourHint", ourHint);
            Set(s, "ourTemplate", our);
            Set(s, "groupsRow", groups.gameObject);
            Set(s, "poRow", poRow.gameObject);
            Set(s, "trophy", cupTrophy);
            Set(s, "status", cupStatus);
            SetList(s, "poPanels", poPanels);
            Set(s, "bracketPanel", bracketPad.parent.gameObject);
            Set(s, "champion", champ);
            SetList(s, "lineupSlots", slots);
            SetList(s, "speedButtons", speeds);
            SetList(s, "groupPanels", groupPanels);
            SetList(s, "bracketCols", bracketCols);
            rt.gameObject.SetActive(false);
            return s;
        }

        /// <summary>문장 + 이름 (웹판 TeamName)</summary>
        internal static RectTransform Team(string name, Transform parent, float w, float size)
        {
            var t = Rect(name, parent);
            if (w > 0) t.Layout(prefW: w, flexW: 0);
            HRow(t, 6);
            Sprite("Crest", t, "crest/aurelia", 18, 18);
            var n = Text("Name", t, "", size, Theme.Parchment).Layout(minW: 0, flexW: 1);
            n.textWrappingMode = TextWrappingModes.NoWrap;
            n.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        internal static void Head(Transform parent, params (string label, float w)[] cols)
        {
            var head = Box("Head", parent, Theme.Bg2).Layout(minH: 26, prefH: 26);
            HRow(head.transform, 8, 8, 8);
            foreach (var (label, w) in cols) Cell(Text("H", head.transform, label, 12, Theme.TextDim, false, FontStyles.Bold).rectTransform, w);
        }

        internal static RectTransform Row(string name, Transform parent)
        {
            var row = Box(name, parent, Color.clear).Layout(minH: 32, prefH: 32);
            HRow(row.transform, 8, 8, 8);
            var line = Box("Line", row.transform, new Color(Theme.Line.r, Theme.Line.g, Theme.Line.b, 0.6f));
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            line.rectTransform.Anchor(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));
            return row.rectTransform;
        }

        internal static void Cell(RectTransform rt, float w)
        {
            if (w > 0) rt.Layout(minW: w, prefW: w, flexW: 0);
            else rt.Layout(minW: 60, flexW: 1);
        }

        internal static void SetList<T>(Object target, string prop, List<T> items) where T : Object
        {
            var so = new UnityEditor.SerializedObject(target);
            var p = so.FindProperty(prop);
            p.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
