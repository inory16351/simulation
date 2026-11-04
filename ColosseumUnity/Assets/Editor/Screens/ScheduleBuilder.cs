// 일정 화면 조립 (Unity판 전용): 왼쪽 달력(한 달 28일 = 7 × 4칸), 오른쪽 고른 날의 일정과 도전장·연습전 보내기. 화면 전체가 스크롤 영역이다.
using System.Collections.Generic;
using Colosseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class ScheduleBuilder
    {
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);
        const float CellW = 116, CellH = 100, Gap = 6;

        public static ScreenBase Build(Transform main)
        {
            var rt = Rect("Screen_schedule", main).Stretch(26, 20, 26, 20);
            // 내용이 늘어도 넘치지 않게 화면 전체를 스크롤 영역으로
            var page = ScrollView("Scroll", rt, out var pageScroll, 10);
            pageScroll.Stretch();
            ScreenHeader(page, "일정", "날짜를 누르면 그날 일정이 보입니다. 정규 리그 기간에는 고른 날로 도전장을, 휴식기에는 컵에서 탈락한 팀에게 연습전을 보낼 수 있습니다.");

            var cols = Rect("Cols", page).Layout(flexH: 1);
            HRow(cols, 14, 0, 0, 0, 0, TextAnchor.UpperLeft);

            // ── 달력 ──
            var left = Rect("Calendar", cols).Layout(minW: 0, prefW: 0, flexW: 1);
            VCol(left, 8);
            var bar = Rect("MonthBar", left);
            HRow(bar, 8);
            var prev = Button("Prev", bar, "◀ 지난달", false, 14).Layout(minW: 110, prefW: 110, prefH: 32, flexW: 0);
            var title = Text("Month", bar, "", 20, Theme.Parchment, true, FontStyles.Bold, TextAlignmentOptions.Center).Layout(flexW: 1, prefH: 32);
            var next = Button("Next", bar, "다음 달 ▶", false, 14).Layout(minW: 110, prefW: 110, prefH: 32, flexW: 0);

            var gridRt = Rect("Grid", left).Layout(minH: CellH * 4 + Gap * 3, prefH: CellH * 4 + Gap * 3, prefW: CellW * 7 + Gap * 6);
            var grid = gridRt.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(CellW, CellH);
            grid.spacing = new Vector2(Gap, Gap);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 7;
            grid.childAlignment = TextAnchor.UpperCenter;
            var cell = Cell(gridRt);

            Text("Legend", left,
                $"<color={Theme.Hex(Theme.Torch)}>■</color> 우리 경기   <color={Theme.Hex(Theme.Good)}>■</color> 연습전   <color={Theme.Hex(Theme.TextDim)}>■</color> 대회·시장 일정   " +
                $"밝은 칸 = 정규 리그 기간, 어두운 칸 = 휴식기. 7일마다 급료일입니다.", 13, Theme.TextDim);

            // ── 고른 날 ──
            var pad = TitledPanel("Day", cols, "고른 날", 6);
            pad.parent.Layout(minW: 440, prefW: 440, flexW: 0);
            var dayTitle = Text("Title", pad, "", 20, Theme.Parchment, true, FontStyles.Bold).Layout(prefH: 28);
            var period = Text("Period", pad, "", 13, Theme.TextDim);
            var events = Text("Events", pad, "", 14, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.TopLeft).Layout(minH: 90, prefH: 90);
            events.raycastTarget = true;
            Box("Sep", pad, Theme.Line).Layout(minH: 1, prefH: 1);
            var formTitle = Text("FormTitle", pad, "이날 도전장 보내기", 15, Theme.Parchment, false, FontStyles.Bold);
            var block = Text("BlockHint", pad, "", 13, Theme.TextDim);

            var body = Rect("Form", pad);
            VCol(body, 8);
            var tf = Field(body, "상대 루두스");
            var targetDd = Dropdown("Target", tf, 0);
            targetDd.Layout(minH: 30, prefH: 30);
            var mf = Field(body, "우리 출전자 (단체전이면 주장)");
            var mineDd = Dropdown("Mine", mf, 0);
            mineDd.Layout(minH: 30, prefH: 30);
            var ff = Field(body, "형식");
            var fmtButtons = Seg(ff, "Format", "1:1", "3:3 단체전", "5:5 단체전");
            var rf = Field(body, "생사 규칙");
            var ruleButtons = Seg(rf, "Rule", "훈련전", "미시오", "사투");
            var ruleHint = Text("Hint", rf, "", 13, Theme.TextDim);
            var err = Text("Error", body, "", 14, Neg);
            var sendRow = Rect("SendRow", body);
            HRow(sendRow, 0);
            var send = Button("Send", sendRow, "도전장 보내기", true, 15).Layout(prefW: 200, prefH: 38);

            var s = rt.gameObject.AddComponent<ScheduleScreen>();
            s.screenId = "schedule";
            Set(s, "prevMonth", prev); Set(s, "nextMonth", next); Set(s, "monthTitle", title);
            Set(s, "cellTemplate", cell);
            Set(s, "dayTitle", dayTitle); Set(s, "dayPeriod", period); Set(s, "dayEvents", events);
            Set(s, "formTitle", formTitle); Set(s, "blockHint", block); Set(s, "formBody", body.gameObject);
            Set(s, "target", targetDd); Set(s, "mine", mineDd);
            Set(s, "ruleHint", ruleHint); Set(s, "error", err); Set(s, "send", send);
            CupBuilder.SetList(s, "formatButtons", fmtButtons);
            CupBuilder.SetList(s, "ruleButtons", ruleButtons);
            rt.gameObject.SetActive(false);
            return s;
        }

        /// <summary>하루 칸 원본: 테두리(칸 자체) > 바탕(Fill), 오늘 표시 띠, 날짜 숫자, 급료일 꼬리표, 일정 세 줄</summary>
        static RectTransform Cell(Transform grid)
        {
            var frame = Box("CellTemplate", grid, Theme.Line);
            var b = frame.gameObject.AddComponent<Button>();
            b.targetGraphic = frame;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
            b.colors = colors;
            var fill = Box("Fill", frame.transform, Theme.Panel2);
            fill.rectTransform.Stretch(1, 1, 1, 1);
            fill.raycastTarget = false;
            var today = Box("Today", frame.transform, Theme.Torch);
            today.rectTransform.Anchor(new Vector2(0, 1), Vector2.one, new Vector2(1, -4), new Vector2(-1, -1));
            today.raycastTarget = false;
            var num = Text("Num", frame.transform, "1", 15, Theme.Parchment, false, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            num.rectTransform.Anchor(new Vector2(0, 1), Vector2.one, new Vector2(7, -24), new Vector2(-7, -5));
            var tag = Text("Tag", frame.transform, "", 11, Theme.TextDim, false, FontStyles.Normal, TextAlignmentOptions.TopRight);
            tag.rectTransform.Anchor(new Vector2(0, 1), Vector2.one, new Vector2(7, -22), new Vector2(-7, -6));
            var lines = Text("Lines", frame.transform, "", 11, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            lines.rectTransform.Stretch(7, 26, 5, 4);
            lines.textWrappingMode = TextWrappingModes.NoWrap;
            lines.overflowMode = TextOverflowModes.Ellipsis;
            lines.lineSpacing = -8;
            frame.gameObject.SetActive(false);
            return frame.rectTransform;
        }

        static RectTransform Field(Transform parent, string label)
        {
            var f = Rect("Field", parent);
            VCol(f, 4);
            Text("Label", f, label, 13, Theme.TextDim);
            return f;
        }

        static List<Button> Seg(Transform parent, string name, params string[] labels)
        {
            var row = Rect(name, parent);
            HRow(row, 4);
            var list = new List<Button>();
            foreach (var l in labels) list.Add(Button(name + "_" + list.Count, row, l, false, 14).Layout(prefW: 120, prefH: 32));
            return list;
        }
    }
}
