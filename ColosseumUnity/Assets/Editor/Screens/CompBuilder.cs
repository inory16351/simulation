// 국내 컵·올림피카 컵 화면 조립 (Unity판 전용). 표 줄 만들기는 CupBuilder와 같다.
using System.Collections.Generic;
using Colosseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class CompBuilder
    {
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);

        /// <summary>제목 줄 아래: 큰 트로피 + 상태 글</summary>
        internal static (Image trophy, TMP_Text status) Top(Transform content)
        {
            var box = Panel("Top", content);
            HRow(box.transform, 14, 14, 14, 10, 10);
            box.transform.Find("Fill").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            box.Layout(minH: 96, prefH: 96, flexH: 0);
            var trophy = Sprite("Trophy", box.transform, "icon/trophy/pantheon", 76, 76);
            var status = Text("Status", box.transform, "", 14, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            status.Layout(flexW: 1);
            return (trophy, status);
        }

        static RectTransform NameCell(string name, Transform parent, float w, float size)
        {
            var t = CupBuilder.Team(name, parent, w, size);
            t.gameObject.AddComponent<CanvasGroup>();
            return t;
        }

        static List<TMP_Dropdown> Lineup(Transform parent, int n)
        {
            var row = Rect("Lineup", parent);
            HRow(row, 8);
            var slots = new List<TMP_Dropdown>();
            for (int i = 0; i < n; i++)
            {
                var slot = Rect("Slot", row);
                VCol(slot, 2);
                Text("Label", slot, $"출전 {i + 1}", 12, Theme.TextDim);
                slots.Add(Dropdown("Pick", slot, 190));
            }
            return slots;
        }

        static List<Button> Speed(Transform parent)
        {
            var row = Rect("Speed", parent);
            HRow(row, 4);
            Text("Label", row, "중계 속도", 13, Theme.TextDim).Layout(prefW: 70, flexW: 0);
            var speeds = new List<Button>();
            foreach (var sp in MatchScreen.Speeds) speeds.Add(Button("Speed_" + sp.id, row, sp.label, false, 14).Layout(prefW: 90, prefH: 30));
            return speeds;
        }

        // ── 국내 컵 ──

        public static ScreenBase BuildNatCup(Transform main)
        {
            var rt = Rect("Screen_natcup", main).Stretch(26, 20, 26, 20);
            var content = ScrollView("Scroll", rt, out var scroll, 14);
            scroll.Stretch();
            ScreenHeader(content, "국내 컵", "정규 리그가 끝나면 나라의 1~4부 40개 루두스가 모두 나옵니다. 40강·20강·10강은 5:5 미시오 단판, 5강은 한 번씩 맞붙어 승수로 우승을 가립니다(같으면 쓰러뜨린 상대 수, 그다음 승자승). 우승하면 몇 부든 판테온 컵 플레이오프에 나갑니다. RP는 오가지 않습니다.");
            content.Find("Sub").GetComponent<TMP_Text>().Layout(prefH: 44);
            var (trophy, status) = Top(content);

            var todayPad = TitledPanel("Today", content, "오늘의 경기", 8);
            var todayTitle = todayPad.parent.Find("Head/Text").GetComponent<TMP_Text>();
            var teams = Rect("Teams", todayPad);
            HRow(teams, 10);
            var home = NameCell("Home", teams, 300, 16);
            Text("Vs", teams, "vs", 14, Theme.TextDim).Layout(prefW: 24, flexW: 0);
            var away = NameCell("Away", teams, 300, 16);
            var slots = Lineup(todayPad, 5);
            var warn = Text("Warn", todayPad, "다섯 명을 채우지 못하면 있는 인원으로만 싸웁니다.", 13, Neg);
            var speeds = Speed(todayPad);
            var enterRow = Rect("EnterRow", todayPad);
            HRow(enterRow, 10);
            var enter = Button("Enter", enterRow, "경기장 들어가기", true, 15).Layout(prefW: 200, prefH: 40);
            var forfeit = Button("Forfeit", enterRow, "이 경기 나가지 않기 (기권)", false, 14).Layout(prefW: 220, prefH: 40);

            var cols = Rect("Cols", content);
            HRow(cols, 14, 0, 0, 0, 0, TextAnchor.UpperLeft);
            var ourPad = TitledPanel("Ours", cols, "우리 경기", 0);
            ourPad.parent.Layout(minW: 0, prefW: 0, flexW: 1);
            var ourHint = Text("Hint", ourPad, "", 14, Theme.TextDim);
            var our = CupBuilder.Row("OurTemplate", ourPad);
            CupBuilder.Cell(Text("Date", our, "", 14).rectTransform, 110);
            CupBuilder.Cell(Text("Stage", our, "", 14).rectTransform, 74);
            CupBuilder.Cell(CupBuilder.Team("Opp", our, 0, 14), 0);
            CupBuilder.Cell(Text("Result", our, "", 14, Theme.Text, false, FontStyles.Bold).rectTransform, 60);
            our.gameObject.SetActive(false);

            var leaguePad = TitledPanel("League5", cols, "5강 리그 (승수 → 쓰러뜨린 상대 수 → 승자승)", 0);
            leaguePad.parent.Layout(minW: 0, prefW: 0, flexW: 1);
            CupBuilder.Head(leaguePad, ("순위", 40), ("루두스", 0), ("경기", 40), ("승", 36), ("패", 36), ("쓰러뜨림", 70));
            var lrow = CupBuilder.Row("LeagueTemplate", leaguePad);
            CupBuilder.Cell(Text("Rank", lrow, "", 14).rectTransform, 40);
            CupBuilder.Cell(CupBuilder.Team("Team", lrow, 0, 14), 0);
            CupBuilder.Cell(Text("Played", lrow, "", 14).rectTransform, 40);
            CupBuilder.Cell(Text("Won", lrow, "", 14).rectTransform, 36);
            CupBuilder.Cell(Text("Lost", lrow, "", 14).rectTransform, 36);
            CupBuilder.Cell(Text("Downs", lrow, "", 14, Theme.Parchment, false, FontStyles.Bold).rectTransform, 70);
            lrow.gameObject.SetActive(false);

            var bracket = TitledPanel("Bracket", content, "대진", 6);
            var bar = Rect("Filter", bracket);
            HRow(bar, 4);
            var tabs = new List<Button>();
            foreach (var label in new[] { "40강", "20강", "10강", "5강 리그" }) tabs.Add(Button("Tab", bar, label, false, 14).Layout(prefW: 100, prefH: 32));
            Rect("Gap", bar).Layout(prefW: 10, flexW: 0);
            var nation = Dropdown("Nation", bar, 220, 32);
            var empty = Text("Empty", bracket, "", 14, Theme.TextDim);
            var tie = CupBuilder.Row("TieTemplate", bracket);
            CupBuilder.Cell(Text("Date", tie, "", 13, Theme.TextDim).rectTransform, 110);
            CupBuilder.Cell(NameCell("Home", tie, 0, 14), 0);
            CupBuilder.Cell(Text("Score", tie, "", 14, Theme.Parchment, false, FontStyles.Bold, TextAlignmentOptions.Center).rectTransform, 70);
            CupBuilder.Cell(NameCell("Away", tie, 0, 14), 0);
            tie.gameObject.SetActive(false);

            var s = rt.gameObject.AddComponent<NatCupScreen>();
            s.screenId = "natcup";
            Set(s, "match", main.GetComponentInChildren<MatchScreen>(true));
            Set(s, "trophy", trophy); Set(s, "status", status);
            Set(s, "todayPanel", todayPad.parent.gameObject); Set(s, "todayTitle", todayTitle); Set(s, "todayHome", home); Set(s, "todayAway", away);
            Set(s, "shortWarn", warn.gameObject); Set(s, "enter", enter); Set(s, "forfeit", forfeit);
            Set(s, "nation", nation); Set(s, "emptyHint", empty); Set(s, "tieTemplate", tie);
            Set(s, "ourHint", ourHint); Set(s, "ourTemplate", our);
            Set(s, "leaguePanel", leaguePad.parent.gameObject); Set(s, "leagueTemplate", lrow);
            CupBuilder.SetList(s, "lineupSlots", slots);
            CupBuilder.SetList(s, "speedButtons", speeds);
            CupBuilder.SetList(s, "stageTabs", tabs);
            rt.gameObject.SetActive(false);
            return s;
        }

        // ── 올림피카 컵 ──

        public static ScreenBase BuildOlympica(Transform main)
        {
            var rt = Rect("Screen_olympica", main).Stretch(26, 20, 26, 20);
            var content = ScrollView("Scroll", rt, out var scroll, 14);
            scroll.Stretch();
            ScreenHeader(content, "올림피카 컵", "4년에 한 번 열리는 국가 대항전입니다. 여섯 나라 대표(5명 + 후보 2명)가 5:5 미시오로 홈·원정 풀리그 10라운드를 치르고, 가장 많이 이긴 나라가 우승합니다. 경기는 정규 리그 사이 대표 주간에 열립니다.");
            content.Find("Sub").GetComponent<TMP_Text>().Layout(prefH: 44);
            var (trophy, status) = Top(content);

            var offerPad = TitledPanel("Offer", content, "대표팀 감독 제안", 8);
            Text("Text", offerPad, "우리 루두스의 명성이 나라에서 가장 높아 대표팀을 맡아 달라는 제안이 왔습니다. 맡으면 대표 7명을 직접 뽑고 경기마다 출전 5명을 정합니다. 사양하면 대표는 자동으로 뽑히고 결과만 받습니다.", 14);
            var offerRow = Rect("Row", offerPad);
            HRow(offerRow, 10);
            var accept = Button("Accept", offerRow, "대표팀 맡기", true, 15).Layout(prefW: 180, prefH: 40);
            var decline = Button("Decline", offerRow, "사양하기", false, 15).Layout(prefW: 140, prefH: 40);

            var todayPad = TitledPanel("Today", content, "오늘의 경기", 8);
            var todayTitle = todayPad.parent.Find("Head/Text").GetComponent<TMP_Text>();
            var teams = Rect("Teams", todayPad);
            HRow(teams, 10);
            var home = CupBuilder.Team("Home", teams, 220, 16);
            Text("Vs", teams, "vs", 14, Theme.TextDim).Layout(prefW: 24, flexW: 0);
            var away = CupBuilder.Team("Away", teams, 220, 16);
            var slots = Lineup(todayPad, 5);
            var speeds = Speed(todayPad);
            var enterRow = Rect("EnterRow", todayPad);
            HRow(enterRow, 0);
            var enter = Button("Enter", enterRow, "경기장 들어가기", true, 15).Layout(prefW: 200, prefH: 40);

            var body = Rect("Body", content);
            VCol(body, 14);
            var cols = Rect("Cols", body);
            HRow(cols, 14, 0, 0, 0, 0, TextAnchor.UpperLeft);

            var tablePad = TitledPanel("Table", cols, "순위표 (승수 → 쓰러뜨린 상대 수 → 승자승)", 0);
            tablePad.parent.Layout(minW: 0, prefW: 0, flexW: 1);
            CupBuilder.Head(tablePad, ("순위", 40), ("나라", 0), ("경기", 40), ("승", 36), ("패", 36), ("쓰러뜨림", 70));
            var trow = CupBuilder.Row("TableTemplate", tablePad);
            CupBuilder.Cell(Text("Rank", trow, "", 14).rectTransform, 40);
            CupBuilder.Cell(CupBuilder.Team("Nation", trow, 0, 14), 0);
            CupBuilder.Cell(Text("Played", trow, "", 14).rectTransform, 40);
            CupBuilder.Cell(Text("Won", trow, "", 14).rectTransform, 36);
            CupBuilder.Cell(Text("Lost", trow, "", 14).rectTransform, 36);
            CupBuilder.Cell(Text("Downs", trow, "", 14, Theme.Parchment, false, FontStyles.Bold).rectTransform, 70);
            trow.gameObject.SetActive(false);

            var squadPad = TitledPanel("Squad", cols, "대표 명단", 4);
            squadPad.parent.Layout(minW: 0, prefW: 0, flexW: 1);
            var squadNation = Dropdown("Nation", squadPad, 240, 30);
            CupBuilder.Head(squadPad, ("이름", 0), ("스타일", 90), ("평가", 96), ("소속", 120), ("상태", 44), ("", 50));
            var srow = CupBuilder.Row("SquadTemplate", squadPad);
            CupBuilder.Cell(Text("Name", srow, "", 14).rectTransform, 0);
            CupBuilder.Cell(Text("Style", srow, "", 13, Theme.TextDim).rectTransform, 90);
            var starText = Text("Stars", srow, "", 13, Theme.Torch);
            starText.textWrappingMode = TextWrappingModes.NoWrap;
            CupBuilder.Cell(starText.rectTransform, 96);
            var clubText = Text("Club", srow, "", 13);
            clubText.textWrappingMode = TextWrappingModes.NoWrap;
            clubText.overflowMode = TextOverflowModes.Ellipsis;
            CupBuilder.Cell(clubText.rectTransform, 120);
            CupBuilder.Cell(Text("State", srow, "", 13, Theme.TextDim).rectTransform, 44);
            Button("Remove", srow, "빼기", false, 12).Layout(minW: 50, prefW: 50, prefH: 26, flexW: 0);
            srow.gameObject.SetActive(false);
            var addRow = Rect("Add", squadPad);
            HRow(addRow, 8);
            var addPick = Dropdown("Pick", addRow, 340, 30);
            var addBtn = Button("AddButton", addRow, "대표로 뽑기", false, 14).Layout(prefW: 120, prefH: 30);
            var squadMsg = Text("Msg", squadPad, "", 13, Neg);

            var fixPad = TitledPanel("Fixtures", body, "일정과 결과", 0);
            var frow = CupBuilder.Row("FixtureTemplate", fixPad);
            CupBuilder.Cell(Text("Date", frow, "", 13, Theme.TextDim).rectTransform, 150);
            CupBuilder.Cell(NameCell("Home", frow, 0, 14), 0);
            CupBuilder.Cell(Text("Score", frow, "", 14, Theme.Parchment, false, FontStyles.Bold, TextAlignmentOptions.Center).rectTransform, 70);
            CupBuilder.Cell(NameCell("Away", frow, 0, 14), 0);
            frow.gameObject.SetActive(false);

            var s = rt.gameObject.AddComponent<OlympicaScreen>();
            s.screenId = "olympica";
            Set(s, "match", main.GetComponentInChildren<MatchScreen>(true));
            Set(s, "trophy", trophy); Set(s, "status", status);
            Set(s, "offerPanel", offerPad.parent.gameObject); Set(s, "accept", accept); Set(s, "decline", decline);
            Set(s, "todayPanel", todayPad.parent.gameObject); Set(s, "todayTitle", todayTitle); Set(s, "todayHome", home); Set(s, "todayAway", away);
            Set(s, "enter", enter);
            Set(s, "bodyRoot", body.gameObject); Set(s, "tableTemplate", trow);
            Set(s, "squadNation", squadNation); Set(s, "squadTemplate", srow); Set(s, "addRow", addRow.gameObject); Set(s, "addPick", addPick); Set(s, "addButton", addBtn); Set(s, "squadMsg", squadMsg);
            Set(s, "fixtureTemplate", frow);
            CupBuilder.SetList(s, "lineupSlots", slots);
            CupBuilder.SetList(s, "speedButtons", speeds);
            rt.gameObject.SetActive(false);
            return s;
        }
    }
}
