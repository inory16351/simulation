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

            Text("PracticeSub", root, "연습 시합은 RP와 상금이 오가지 않습니다. 대신 성장과 피로, 혹사는 실전과 똑같이 쌓입니다. 생사 규칙도 실전과 같아서 죽으면 끝입니다.", 15, Theme.TextDim);

            var cols = Rect("Setup", root);
            HRow(cols, 14, 0, 0, 0, 0, TextAnchor.UpperLeft);
            var minePad = TitledPanel("Mine", cols, "우리 검투사", 0);
            minePad.parent.Layout(minW: 0, prefW: 0, flexW: 1);
            var mineEmpty = Text("Empty", minePad, "싸울 수 있는 검투사가 없습니다.", 14, Theme.TextDim);
            var mineRow = PickRow("MineTemplate", minePad);

            var oppPad = TitledPanel("Opp", cols, "상대", 6);
            oppPad.parent.Layout(minW: 0, prefW: 0, flexW: 1);
            Text("Label", oppPad, "루두스", 13, Theme.TextDim);
            var oppLudus = Dropdown("Ludus", oppPad, 0);
            oppLudus.Layout(minH: 30, prefH: 30);
            var oppEmpty = Text("Empty", oppPad, "싸울 수 있는 검투사가 없습니다.", 14, Theme.TextDim);
            var oppRow = PickRow("OppTemplate", oppPad);

            var condPad = TitledPanel("Cond", cols, "조건", 8);
            condPad.parent.Layout(minW: 0, prefW: 0, flexW: 1);
            Text("RuleLabel", condPad, "생사 규칙", 13, Theme.TextDim);
            var ruleRow = Rect("Rules", condPad);
            HRow(ruleRow, 4);
            var rules = new List<Button>();
            foreach (var r in new[] { "training", "missio", "sudden" }) rules.Add(Button("Rule_" + r, ruleRow, r, false, 14).Layout(prefW: 100, prefH: 32));
            var ruleHint = Text("RuleHint", condPad, "", 12, Theme.TextFaint);
            var doc = Rect("Doctrine", condPad);
            VCol(doc, 4);
            Text("Label", doc, "전투 교범", 13, Theme.TextDim);
            var docRow = Rect("Row", doc);
            HRow(docRow, 8);
            var docTags = Text("Tags", docRow, "", 14, Theme.Text).Layout(flexW: 1);
            var docEdit = Button("Edit", docRow, "교범 고치기", false, 13).Layout(prefW: 100, minW: 100, prefH: 28, flexW: 0);
            Text("StanceLabel", doc, "이번 경기만 자세 바꾸기", 13, Theme.TextDim);
            var stance = Dropdown("Stance", doc, 0);
            stance.Layout(minH: 30, prefH: 30);
            var start = Button("Start", condPad, "경기 시작", true, 16).Layout(prefH: 42, minH: 42);

            Set(s, "setupRoot", root.gameObject);
            SetList(s, "speedButtons", speeds);
            Set(s, "officialPanel", off.parent.gameObject);
            Set(s, "officialTemplate", single);
            Set(s, "teamTemplate", team);
            Set(s, "mineTemplate", mineRow);
            Set(s, "mineEmpty", mineEmpty);
            Set(s, "oppLudus", oppLudus);
            Set(s, "oppTemplate", oppRow);
            Set(s, "oppEmpty", oppEmpty);
            SetList(s, "ruleButtons", rules);
            Set(s, "ruleHint", ruleHint);
            Set(s, "doctrineField", doc.gameObject);
            Set(s, "doctrineTags", docTags);
            Set(s, "doctrineEdit", docEdit);
            Set(s, "stance", stance);
            Set(s, "start", start);
        }

        static Button PickRow(string name, Transform parent)
        {
            var row = Box(name, parent, Clear).Layout(minH: 38, prefH: 38);
            var b = row.gameObject.AddComponent<Button>();
            b.targetGraphic = row;
            HRow(row.transform, 8, 4, 8);
            Portrait("Portrait", row.transform, 28);
            Text("Name", row.transform, "", 15, Theme.Parchment, false, FontStyles.Bold).Layout(flexW: 1);
            Text("Style", row.transform, "", 13, Theme.TextDim).Layout(prefW: 96, flexW: 0);
            Text("Stars", row.transform, "", 13, Theme.Torch).Layout(prefW: 60, flexW: 0);
            row.gameObject.SetActive(false);
            return b;
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

            var btns = Rect("Buttons", root);
            HRow(btns, 8);
            var skip = Button("Skip", btns, "결과 보기", false, 15).Layout(prefW: 130, prefH: 38);
            var close = Button("Close", btns, "한 판 더", true, 15).Layout(prefW: 130, prefH: 38);
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
            root.gameObject.SetActive(false);
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
