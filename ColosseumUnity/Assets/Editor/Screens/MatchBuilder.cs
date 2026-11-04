// 모래 마당 화면 조립 (웹판 MatchScreen.tsx + styles.css .official-row, .match-setup, .arena, .feed, .cut).
// 준비 화면(SetupRoot)과 중계 화면(PlayRoot)을 둘 다 하이어라키에 두고 켜고 끈다.
using System.Collections.Generic;
using Colosseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class MatchBuilder
    {
        static readonly Color Clear = new Color(0, 0, 0, 0);

        public static ScreenBase Build(Transform main)
        {
            var rt = Rect("Screen_match", main).Stretch(26, 20, 26, 20);
            var s = rt.gameObject.AddComponent<MatchScreen>();
            s.screenId = "match";
            BuildSetup(rt, s);
            BuildPlay(rt, s);
            rt.gameObject.SetActive(false);
            return s;
        }

        // ───────── 준비 화면 ─────────

        static void BuildSetup(Transform rt, MatchScreen s)
        {
            var root = Rect("SetupRoot", rt).Stretch();
            VCol(root, 12);
            ScreenHeader(root, "모래 마당", null);

            var speedRow = Rect("SpeedRow", root);
            HRow(speedRow, 8);
            Text("Label", speedRow, "중계 속도", 13, Theme.TextDim).Layout(prefW: 70, flexW: 0);
            var speeds = new List<Button>();
            foreach (var sp in MatchScreen.Speeds) speeds.Add(Button("Speed_" + sp.id, speedRow, sp.label, false, 14).Layout(prefW: 90, prefH: 30));

            // 오늘의 공식 경기
            var off = TitledPanel("Official", root, "오늘의 공식 경기", 8);
            var single = Rect("OfficialTemplate", off).Layout(minH: 60);
            HRow(single, 14);
            var me = Rect("Me", single).Layout(prefW: 220, flexW: 0);
            HRow(me, 10);
            Portrait("Portrait", me, 48);
            Text("Name", me, "", 16, Theme.Parchment, false, FontStyles.Bold).Layout(flexW: 1);
            var vs = Rect("Vs", single).Layout(flexW: 1);
            VCol(vs, 2).childAlignment = TextAnchor.MiddleCenter;
            Text("Rule", vs, "", 14, Theme.Text, false, FontStyles.Bold, TextAlignmentOptions.Center);
            Text("Opp", vs, "", 13, Theme.TextDim, false, FontStyles.Normal, TextAlignmentOptions.Center);
            var them = Rect("Them", single).Layout(prefW: 220, flexW: 0);
            HRow(them, 10).childAlignment = TextAnchor.MiddleRight;
            Text("Name", them, "", 16, Theme.Parchment, false, FontStyles.Bold, TextAlignmentOptions.MidlineRight).Layout(flexW: 1);
            Portrait("Portrait", them, 48);
            Button("Enter", single, "경기장 들어가기", true, 15).Layout(prefW: 180, minW: 180, prefH: 40, flexW: 0);
            single.gameObject.SetActive(false);

            var team = Rect("TeamTemplate", off).Layout(minH: 64);
            HRow(team, 14);
            var info = Rect("Info", team).Layout(flexW: 1);
            VCol(info, 2);
            Text("Rule", info, "", 14, Theme.Text, false, FontStyles.Bold);
            Text("Hint", info, "", 13, Theme.TextDim);
            // 이 경기 전술: 팀 방침 · 집중 대상(상대 명단) · 역할은 전략 화면에서
            var tactic = Rect("Tactic", info);
            HRow(tactic, 6, 0, 0, 4, 0);
            Text("Label", tactic, "전술", 12, Theme.BronzeHi, false, FontStyles.Bold).Layout(minW: 32, prefW: 32, flexW: 0);
            Dropdown("Policy", tactic, 130, 26);
            Dropdown("Focus", tactic, 220, 26);
            Button("Roles", tactic, "역할 정하기", false, 12).Layout(minW: 100, prefW: 100, prefH: 26, flexW: 0);
            var lineup = Rect("Lineup", team);
            HRow(lineup, 6);
            for (int i = 0; i < 5; i++)
            {
                var slot = Rect("Slot" + i, lineup);
                VCol(slot, 2);
                Text("Label", slot, "", 12, Theme.TextDim);
                Dropdown("Pick", slot, 150);
            }
            Button("Enter", team, "경기장 들어가기", true, 15).Layout(prefW: 180, minW: 180, prefH: 40, flexW: 0);
            team.gameObject.SetActive(false);

            // 연습 시합은 없다: 정식으로 수락된 경기만 치른다
            var noMatch = Text("NoMatch", root, "오늘 치를 경기가 없습니다. 도전장을 보내 상대가 받아들이면, 잡힌 날에 이곳에서 경기를 치릅니다.", 15, Theme.TextDim);

            Set(s, "setupRoot", root.gameObject);
            Set(s, "noMatch", noMatch);
            SetList(s, "speedButtons", speeds);
            Set(s, "officialPanel", off.parent.gameObject);
            Set(s, "officialTemplate", single);
            Set(s, "teamTemplate", team);
        }

        // ───────── 중계 화면 ─────────

        static void BuildPlay(Transform rt, MatchScreen s)
        {
            var root = Rect("PlayRoot", rt).Stretch();
            VCol(root, 10);
            var title = Text("Title", root, "", 26, Theme.Parchment, true, FontStyles.Bold).Layout(prefH: 36);
            var sub = Text("Sub", root, "", 15, Theme.TextDim).Layout(prefH: 24);

            var arena = Panel("Arena", root).Layout(flexH: 1);
            VCol(arena.transform, 10, 14, 14, 14, 14);
            arena.transform.Find("Fill").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var head = Rect("Head", arena.transform).Layout(minH: 76);
            CoverBackground(head, "ui/arena_provincial", new Color32(12, 9, 7, 89), new Color32(12, 9, 7, 217), 0.4f);
            HRow(head, 12);
            var a = FighterBox("FighterA", head, false);
            Rect("Gap1", head).Layout(flexW: 1);
            var crowd = Text("Crowd", head, "", 13, Theme.Torch, false, FontStyles.Normal, TextAlignmentOptions.Center).Layout(prefW: 120, flexW: 0);
            Rect("Gap2", head).Layout(flexW: 1);
            var b = FighterBox("FighterB", head, true);

            var stage = ArenaStageKit.Build(arena.transform, 340);
            var feedBox = Box("Feed", arena.transform, new Color32(0x12, 0x0f, 0x0d, 0xff)).Layout(flexH: 1, minH: 120);
            var content = ScrollView("Scroll", feedBox.transform, out var scrollRt, 3);
            scrollRt.Stretch(12, 10, 12, 10);
            var line = Text("LineTemplate", content, "", 15, Theme.Text);

            // 연출: 컷 글자, 붉은 번쩍임 (레이아웃 밖, 경기장 전체를 덮는다)
            var cut = Box("Cut", arena.transform, new Color(0, 0, 0, 0.72f));
            cut.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            cut.rectTransform.Stretch();
            var cutText = Text("Label", cut.transform, "", 64, Theme.Torch, true, FontStyles.Bold, TextAlignmentOptions.Center);
            cutText.rectTransform.Stretch();
            cut.gameObject.SetActive(false);
            var flash = Box("Flash", arena.transform, new Color(0.6f, 0.05f, 0.03f, 0.35f));
            flash.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            flash.rectTransform.Stretch();
            flash.raycastTarget = false;
            flash.gameObject.SetActive(false);

            // 결과창: 경기가 끝나면 경기장 위에 뜬다 (평점 매니저 기록, 줄 원본은 비활성)
            var res = Panel("Result", arena.transform, Theme.Bg2);
            res.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            res.rectTransform.Stretch(30, 20, 30, 20);
            VCol(res.transform, 4, 22, 22, 18, 18).childForceExpandHeight = false;
            res.transform.Find("Fill").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Text("Title", res.transform, "경기 결과", 22, Theme.Parchment, true, FontStyles.Bold).Layout(prefH: 32);
            var mvp = Text("Mvp", res.transform, "", 15, Theme.Text).Layout(prefH: 30);
            mvp.raycastTarget = true;
            var rhead = Box("Head", res.transform, Theme.Panel2).Layout(minH: 28, prefH: 28);
            HRow(rhead.transform, 8, 10, 10);
            foreach (var (id, label, w) in ResultCols) ResultCell(Text(id, rhead.transform, label, 12, Theme.TextDim, false, FontStyles.Bold, id == "Name" ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight), w);
            var rrow = Box("RowTemplate", res.transform, Color.clear).Layout(minH: 30, prefH: 30);
            HRow(rrow.transform, 8, 10, 10);
            foreach (var (id, _, w) in ResultCols) ResultCell(Text(id, rrow.transform, "", 14, id == "Name" ? Theme.Parchment : Theme.Text, false, FontStyles.Normal, id == "Name" ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight), w);
            rrow.gameObject.SetActive(false);
            Text("Hint", res.transform, "평점은 6.0이 보통입니다. 이기고, 쓰러뜨리고, 많이 깎고, 덜 맞고, 잘 막고 피할수록 오릅니다.", 12, Theme.TextFaint).Layout(prefH: 24);
            res.gameObject.SetActive(false);

            var btns = Rect("Buttons", root);
            HRow(btns, 8);
            var skip = Button("Skip", btns, "결과 보기", false, 15).Layout(prefW: 130, prefH: 38);
            var close = Button("Close", btns, "다음으로", true, 15).Layout(prefW: 130, prefH: 38);
            var roster = Button("Roster", btns, "막사로", false, 15).Layout(prefW: 110, prefH: 38);

            Set(s, "playRoot", root.gameObject);
            Set(s, "playTitle", title);
            Set(s, "playSub", sub);
            Set(s, "fighterA", a);
            Set(s, "fighterB", b);
            Set(s, "crowdValue", crowd);
            Set(s, "stage", stage);
            Set(s, "feedScroll", scrollRt.GetComponent<ScrollRect>());
            Set(s, "lineTemplate", line);
            Set(s, "cutOverlay", cut.gameObject);
            Set(s, "cutText", cutText);
            Set(s, "flash", flash);
            Set(s, "skipButton", skip);
            Set(s, "closeButton", close);
            Set(s, "rosterButton", roster);
            Set(s, "resultPanel", res.gameObject);
            Set(s, "resultMvp", mvp);
            Set(s, "resultTemplate", rrow.rectTransform);
            root.gameObject.SetActive(false);
        }

        // 결과창 칸 (이름, 머리글, 폭)  폭 0 = 남는 폭
        static readonly (string id, string label, float w)[] ResultCols =
        {
            ("Name", "검투사", 0), ("Rating", "평점", 60), ("Dealt", "입힌 피해", 86), ("Taken", "받은 피해", 86), ("Acc", "명중", 80),
            ("Block", "막기", 56), ("Dodge", "피하기", 60), ("Crit", "치명타", 60), ("Downs", "쓰러뜨림", 72),
        };

        static void ResultCell(TMP_Text t, float w)
        {
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            if (w > 0) t.Layout(minW: w, prefW: w, flexW: 0, minH: 28, prefH: 28);
            else t.Layout(minW: 120, flexW: 1, minH: 28, prefH: 28);
        }

        /// <summary>웹판 .fighter: 초상화 + 이름·스타일·체력 막대. 오른쪽 선수는 거울로</summary>
        static RectTransform FighterBox(string name, Transform parent, bool right)
        {
            var box = Rect(name, parent).Layout(prefW: 320, flexW: 0);
            HRow(box, 10).childAlignment = right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            var align = right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            PortraitView pv = null;
            if (!right) pv = Portrait("Portrait", box, 64);
            var info = Rect("Info", box).Layout(flexW: 1);
            VCol(info, 3).childAlignment = right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            info.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;
            Text("Name", info, "", 18, Theme.Parchment, false, FontStyles.Bold, align);
            Text("Sub", info, "", 12, Theme.TextDim, false, FontStyles.Normal, align);
            var hpBar = Bar("Hp", info, 220, 10);
            hpBar.GetComponent<Image>().color = new Color32(0x2a, 0x0d, 0x0b, 0xff);
            var fill = hpBar.Find("Fill").GetComponent<Image>();
            fill.color = new Color32(0xc2, 0x3a, 0x2c, 0xff);
            ((RectTransform)fill.transform).anchorMax = new Vector2(1, 1);
            if (right) pv = Portrait("Portrait", box, 64);
            pv.transform.parent.parent.gameObject.AddComponent<CanvasGroup>();
            return box;
        }

        static void SetList(Object target, string prop, List<Button> items)
        {
            var so = new UnityEditor.SerializedObject(target);
            var p = so.FindProperty(prop);
            p.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
