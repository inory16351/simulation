// 안뜰 화면 조립 (웹판 OverviewScreen.tsx + styles.css .grid3, .panel)
using Colosseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class OverviewBuilder
    {
        public static ScreenBase Build(Transform main)
        {
            var rt = Rect("Screen_overview", main).Stretch(26, 20, 26, 20);
            VCol(rt, 14);
            ScreenHeader(rt, "루두스", "안뜰 — 루두스의 오늘");
            var title = rt.Find("Title").GetComponent<TMP_Text>();

            // 요약 패널 셋 (grid3)
            var grid = Rect("Grid", rt);
            var gh = HRow(grid, 14);
            gh.childForceExpandWidth = true;
            gh.childForceExpandHeight = false;
            gh.childAlignment = TextAnchor.UpperLeft;

            var money = TitledPanel("Finance", grid, "재정");
            money.parent.Layout(flexW: 1);
            var treasury = Text("Treasury", money, "0 D", 24, Theme.Parchment, false, FontStyles.Bold);
            var wages = Text("Wages", money, "주급 합계 0 D/주", 15, Theme.TextDim);
            var runway = Text("Runway", money, "", 15, Theme.Text);

            var league = TitledPanel("League", grid, "리그");
            league.parent.Layout(flexW: 1);
            var rank = Text("Rank", league, "1부 1위", 24, Theme.Parchment, false, FontStyles.Bold);
            var gap = Text("Gap", league, "", 15, Theme.TextDim);
            var row = Rect("ButtonRow", league);
            HRow(row, 0);
            var leagueBtn = Button("LeagueButton", row, "서열표 보기").Layout(prefW: 140, prefH: 36);

            var house = TitledPanel("Barracks", grid, "숙소", 8);
            house.parent.Layout(flexW: 1);
            var mrow = Rect("Morale", house);
            HRow(mrow, 8);
            Text("Label", mrow, "평균 사기", 15).Layout(prefW: 70);
            var bar = Bar("Bar", mrow);
            var morale = Text("Value", mrow, "0", 15).Layout(flexW: 1);
            var fit = Text("Fit", house, "출전 가능 0 / 0명", 15);
            var injured = Text("Injured", house, "부상자 0명", 15);

            // 주력 검투사
            var bestPad = TitledPanel("Best", rt, "주력 검투사");
            var bestRow = Rect("Row", bestPad);
            HRow(bestRow, 18, 0, 0, 0, 0, TextAnchor.UpperLeft);
            var card = Box("BestTemplate", bestRow, new Color(0, 0, 0, 0));
            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = card;
            VCol(card.transform, 4).childAlignment = TextAnchor.UpperCenter;
            card.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;
            card.Layout(prefW: 130);
            Portrait("Portrait", card.transform, 110);
            Text("Name", card.transform, "이름", 15, Theme.Parchment, false, FontStyles.Bold, TextAlignmentOptions.Center).Layout(prefW: 130);
            Text("Stars", card.transform, "★★★", 13, Theme.Torch, false, FontStyles.Normal, TextAlignmentOptions.Center).Layout(prefW: 130);
            card.gameObject.SetActive(false);

            var s = rt.gameObject.AddComponent<OverviewScreen>();
            s.screenId = "overview";
            Set(s, "title", title);
            Set(s, "treasury", treasury);
            Set(s, "wages", wages);
            Set(s, "runway", runway);
            Set(s, "rank", rank);
            Set(s, "rpGap", gap);
            Set(s, "leagueButton", leagueBtn);
            Set(s, "moraleBar", bar);
            Set(s, "morale", morale);
            Set(s, "fit", fit);
            Set(s, "injured", injured);
            Set(s, "bestTemplate", btn);
            rt.gameObject.SetActive(false);
            return s;
        }
    }
}
