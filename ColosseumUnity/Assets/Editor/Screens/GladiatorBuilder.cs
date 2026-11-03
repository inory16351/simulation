// 검투사 상세 화면 조립 (웹판 GladiatorScreen.tsx + styles.css .profile-head, .kv, .attr-table)
using System.Collections.Generic;
using Colosseum.Core;
using Colosseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class GladiatorBuilder
    {
        static readonly (string id, string label, bool bar)[] KvRows =
        {
            ("Style", "스타일", false), ("Status", "신분", false), ("Wage", "주급", false), ("Ca", "현재 능력", false), ("Pa", "잠재력", false),
            ("Morale", "사기", true), ("Popularity", "인기", true), ("Fitness", "체력", true), ("Sharpness", "경기감각", true), ("Wear", "혹사도", true),
            ("Perf", "경기력", false), ("State", "상태", false), ("Record", "전적", false),
        };

        public static ScreenBase Build(Transform main)
        {
            var rt = Rect("Screen_gladiator", main).Stretch(26, 20, 26, 20);
            var content = ScrollView("Scroll", rt, out var scroll, 14);
            scroll.Stretch();

            var top = Rect("Top", content);
            HRow(top, 0);
            var back = Button("BackButton", top, "← 막사").Layout(prefW: 110, prefH: 36);

            // 신상
            var head = Panel("Profile", content);
            HRow(head.transform, 20, 14, 14, 14, 14, TextAnchor.UpperLeft);
            head.transform.Find("Fill").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var portrait = Portrait("Portrait", head.transform, 180);
            var info = Rect("Info", head.transform).Layout(flexW: 1);
            VCol(info, 4);
            var bigName = Text("BigName", info, "이름", 28, Theme.Parchment, true, FontStyles.Bold);
            var sub = Text("Sub", info, "", 15, Theme.TextDim);
            var kv = Rect("Kv", info);
            VCol(kv, 2, 0, 0, 8, 0);
            foreach (var (id, label, bar) in KvRows)
            {
                var row = Rect(id, kv).Layout(minH: 24);
                HRow(row, 8);
                Text("Label", row, label, 14, Theme.TextDim).Layout(minW: 90, prefW: 90, flexW: 0);
                if (bar) Bar("Bar", row);
                Text("Value", row, "", 15, Theme.Text).Layout(flexW: 1);
            }
            var traits = Text("Traits", info, "", 14, Theme.Text);
            traits.margin = new Vector4(0, 8, 0, 0);

            var acquire = BuildAcquire(content);

            // 탭: 정보 / 장비 / 전술 — 한 번에 하나만 보여 장비와 교범이 섞이지 않게
            var tabRow = Rect("Tabs", content);
            HRow(tabRow, 4);
            var tabs = new List<Button>();
            var roots = new List<GameObject>();
            foreach (var (id, label) in new[] { ("Info", "정보"), ("Gear", "장비"), ("Tactics", "전술") })
            {
                tabs.Add(Button("Tab_" + id, tabRow, label, false, 15).Layout(prefW: 140, prefH: 36));
                var root = Rect("Tab" + id, content);
                VCol(root, 14);
                roots.Add(root.gameObject);
            }
            var infoTab = roots[0].transform;

            // 능력치 네 묶음 + 무기 숙련 (웹판 1fr 1fr 1fr 1fr 1.2fr)
            var grid = Rect("Attrs", infoTab);
            HRow(grid, 14, 0, 0, 0, 0, TextAnchor.UpperLeft);
            var blocks = new List<RectTransform>
            {
                AttrBlock(grid, "Physical", "신체", Attrs.Physical),
                AttrBlock(grid, "Technical", "기술", Attrs.Technical),
                AttrBlock(grid, "Mental", "정신", Attrs.Mental),
                AttrBlock(grid, "Sense", "전투 감각", Attrs.Sense),
            };
            var prof = TitledPanel("Prof", grid, "무기 숙련", 2);
            prof.parent.Layout(flexW: 1.2f);
            foreach (var w in Attrs.WeaponClasses)
            {
                var row = Rect(w, prof).Layout(minH: 22);
                HRow(row, 8);
                Text("Label", row, Attrs.WeaponClassNames[w], 14).Layout(flexW: 1);
                Bar("Bar", row);
                Text("Value", row, "0", 14, Theme.Parchment, false, FontStyles.Normal, TextAlignmentOptions.MidlineRight).Layout(minW: 30, prefW: 30, flexW: 0);
            }

            // 기술: 아이콘 + 이름 칸 격자 (이름에 마우스를 올리면 발동 조건·효과 툴팁)
            var sk = TitledPanel("Skills", infoTab, "기술");
            var skGrid = Rect("Grid", sk);
            var sg = skGrid.gameObject.AddComponent<GridLayoutGroup>();
            sg.cellSize = new Vector2(250, 48);
            sg.spacing = new Vector2(8, 6);
            var skTpl = Panel("SkillTemplate", skGrid, Theme.Panel2);
            Sprite("Icon", skTpl.transform, "icon/skills/power_strike", 38, 38).rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6, -19), new Vector2(44, 19));
            Text("Name", skTpl.transform, "", 14, Theme.Parchment).rectTransform.Anchor(new Vector2(0, 0), new Vector2(1, 1), new Vector2(52, 0), new Vector2(-6, 0));
            skTpl.gameObject.SetActive(false);
            var skills = Text("Empty", sk, "", 13, Theme.TextFaint);
            var career = BuildCareer(infoTab);

            var equip = BuildEquip(roots[1].transform);
            var doctrine = BuildDoctrine(roots[2].transform);

            var s = rt.gameObject.AddComponent<GladiatorScreen>();
            s.screenId = "gladiator";
            Set(s, "backButton", back);
            Set(s, "portrait", portrait);
            Set(s, "bigName", bigName);
            Set(s, "subLine", sub);
            Set(s, "kv", kv);
            Set(s, "traits", traits);
            Set(s, "profBlock", prof);
            Set(s, "skills", skills);
            Set(s, "skillTemplate", skTpl.rectTransform);
            Set(s, "acquire", acquire);
            Set(s, "doctrine", doctrine);
            Set(s, "equip", equip);
            Set(s, "career", career);
            var so = new UnityEditor.SerializedObject(s);
            var ab = so.FindProperty("attrBlocks");
            ab.arraySize = blocks.Count;
            for (int i = 0; i < blocks.Count; i++) ab.GetArrayElementAtIndex(i).objectReferenceValue = blocks[i];
            var tp = so.FindProperty("tabButtons");
            tp.arraySize = tabs.Count;
            for (int i = 0; i < tabs.Count; i++) tp.GetArrayElementAtIndex(i).objectReferenceValue = tabs[i];
            var rp = so.FindProperty("tabRoots");
            rp.arraySize = roots.Count;
            for (int i = 0; i < roots.Count; i++) rp.GetArrayElementAtIndex(i).objectReferenceValue = roots[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            rt.gameObject.SetActive(false);
            return s;
        }

        /// <summary>영입 패널 (웹판 AcquirePanel): 정찰·관심 명단, 무소속 주급 흥정, 이적료 흥정.
        /// 스크롤 영역 안이라 드롭다운 대신 버튼(계약 기간)과 입력칸만 쓴다</summary>
        static AcquirePanel BuildAcquire(Transform parent)
        {
            var pad = TitledPanel("Acquire", parent, "영입", 8);
            var p = pad.parent.gameObject.AddComponent<AcquirePanel>();
            var top = Rect("Top", pad);
            HRow(top, 12);
            var info = Text("Info", top, "정보량", 14, Theme.TextDim);
            info.textWrappingMode = TextWrappingModes.NoWrap;
            var bar = Bar("InfoBar", top);
            var scout = Button("Scout", top, "정찰 보내기", false, 14).Layout(prefW: 140, prefH: 30);
            var shortB = Button("Short", top, "☆ 관심 명단", false, 14).Layout(prefW: 160, prefH: 30);
            var msg = Text("Msg", pad, "", 14, new Color32(0xd9, 0xa2, 0x9b, 0xff));
            var mood = Text("Mood", pad, "", 14);

            // 무소속: 주급 + 기간
            var free = Rect("Free", pad);
            VCol(free, 6);
            var freeHint = Text("Hint", free, "", 13, Theme.TextDim);
            var fr = Rect("Row", free);
            HRow(fr, 8);
            var want = Text("Want", fr, "요구 주급 0 D", 14, Theme.TextDim).Layout(prefW: 120, minW: 120);
            var wage = Input(fr, 90);
            var seasons = new List<Button>();
            for (int i = 1; i <= 4; i++) seasons.Add(Button("Season_" + i, fr, $"{i}시즌", false, 13).Layout(prefW: 64, prefH: 30));
            var sign = Button("Sign", fr, "계약 제안", true, 14).Layout(prefW: 120, prefH: 30);

            // 다른 루두스 소속: 이적료
            var club = Rect("Club", pad);
            HRow(club, 8);
            var value = Text("Value", club, "추정 가치", 14, Theme.TextDim).Layout(prefW: 160, minW: 160);
            var fee = Input(club, 110);
            var bid = Button("Bid", club, "이적료 제안", true, 14).Layout(prefW: 130, prefH: 30);
            var deal = Rect("Deal", pad);
            HRow(deal, 10);
            var dealText = Text("Text", deal, "", 14);
            var dealGo = Button("Go", deal, "개인 계약 협상", true, 14).Layout(prefW: 170, prefH: 30);

            var so = new UnityEditor.SerializedObject(p);
            void S(string prop, Object v) => so.FindProperty(prop).objectReferenceValue = v;
            S("title", pad.parent.Find("Head/Text").GetComponent<TMP_Text>());
            S("info", info); S("infoBar", bar); S("scout", scout); S("shortlist", shortB); S("msg", msg); S("mood", mood);
            S("freeRoot", free.gameObject); S("freeHint", freeHint); S("want", want); S("wage", wage); S("sign", sign);
            S("clubRoot", club.gameObject); S("value", value); S("fee", fee); S("bid", bid);
            S("dealRoot", deal.gameObject); S("dealText", dealText); S("dealGo", dealGo);
            var sp = so.FindProperty("seasons");
            sp.arraySize = seasons.Count;
            for (int i = 0; i < seasons.Count; i++) sp.GetArrayElementAtIndex(i).objectReferenceValue = seasons[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return p;
        }

        /// <summary>전투 교범 패널 (웹판 DoctrinePanel, styles.css .doctrine-*).
        /// select 자리는 버튼이고, 누르면 조건 명령 아래 선택지(Picker)가 펼쳐진다</summary>
        public static DoctrinePanel BuildDoctrine(Transform parent)
        {
            var pad = TitledPanel("Doctrine", parent, "전투 교범", 12);
            var p = pad.parent.gameObject.AddComponent<DoctrinePanel>();

            var fam = Rect("Fam", pad);
            HRow(fam, 8);
            Text("Label", fam, "교범 숙련도", 14).textWrappingMode = TextWrappingModes.NoWrap;
            var famBar = Bar("Bar", fam);
            var famValue = Text("Value", fam, "0", 14, Theme.Parchment).Layout(minW: 30, prefW: 30, flexW: 0);
            var famHint = Text("Hint", fam, $"교범을 고칠 때마다 숙련도가 {Tactics.FamiliarityChangeCost}씩 떨어집니다. 연무장 수련과 실전으로 다시 오릅니다.", 12, Theme.TextFaint).Layout(minW: 0, prefW: 0, flexW: 1);

            // 자세·거리·노리는 곳 (웹판 1.4fr 1fr 1.2fr)
            var grid = Rect("Choices", pad);
            HRow(grid, 16, 0, 0, 0, 0, TextAnchor.UpperLeft);
            var stance = ChoiceBlock(grid, "Stance", "자세", Tactics.StanceNames, 1.4f, true, out var stanceDesc);
            var range = ChoiceBlock(grid, "Range", "거리", Tactics.RangeNames, 1f, false, out _);
            var focus = ChoiceBlock(grid, "Focus", "노리는 곳", Tactics.FocusNames, 1.2f, true, out var focusDesc);

            // 노릴 높이·막을 높이 (지대 상성) + 무기 힌트
            var zones = Rect("Zones", pad);
            HRow(zones, 16, 0, 0, 0, 0, TextAnchor.UpperLeft);
            var atkZone = ChoiceBlock(zones, "AtkZone", "노릴 높이", Colosseum.Core.Combat.Exchange.ZoneNames, 1f, false, out _);
            var guardZone = ChoiceBlock(zones, "GuardZone", "막을 높이", Colosseum.Core.Combat.Exchange.ZoneNames, 1f, false, out _);
            var zoneHint = Text("ZoneHint", zones, "", 12, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.TopLeft).Layout(minW: 0, prefW: 0, flexW: 1.2f);

            // 기술 순서 (220px) | 조건 명령
            var cols = Rect("Cols", pad);
            HRow(cols, 20, 0, 0, 0, 0, TextAnchor.UpperLeft);
            var left = Rect("Order", cols).Layout(minW: 220, prefW: 220, flexW: 0);
            VCol(left, 2);
            Sublabel(left, "기술 순서");
            var order = Box("OrderTemplate", left, Color.clear).Layout(minH: 28, prefH: 28);
            HRow(order.transform, 6);
            RowLine(order.transform);
            Text("N", order.transform, "1", 14, Theme.Torch, false, FontStyles.Bold, TextAlignmentOptions.Center).Layout(minW: 18, prefW: 18, flexW: 0);
            Text("Name", order.transform, "", 14, Theme.Parchment).Layout(flexW: 1);
            Button("Up", order.transform, "▲", false, 11).Layout(minW: 30, prefW: 30, minH: 22, prefH: 22, flexW: 0);
            Button("Down", order.transform, "▼", false, 11).Layout(minW: 30, prefW: 30, minH: 22, prefH: 22, flexW: 0);
            order.gameObject.SetActive(false);
            var orderEmpty = Text("Empty", left, "익힌 기술이 없습니다.", 12, Theme.TextFaint);

            var right = Rect("Rules", cols).Layout(minW: 0, prefW: 0, flexW: 1);
            VCol(right, 6);
            Text("Label", right, $"<b>조건 명령</b>  <color={Theme.Hex(Theme.TextFaint)}><size=11>위에서부터 조건이 맞는 첫 줄만 따릅니다</size></color>", 12, Theme.BronzeHi);
            var rule = Rect("RuleTemplate", right);
            VCol(rule, 0);
            Text("Text", rule, "", 14, Theme.Parchment).Layout(minH: 24);
            var edit = Rect("Edit", rule).Layout(minH: 30, prefH: 30);
            HRow(edit, 6);
            Text("If", edit, "만약", 13, Theme.TextDim).Layout(minW: 30, prefW: 30, flexW: 0);
            Button("Cond", edit, "조건", false, 13).Layout(minW: 180, prefW: 180, minH: 28, prefH: 28, flexW: 0);
            Input(edit, 56).name = "Value";
            Text("Arrow", edit, "→", 14, Theme.TextDim, false, FontStyles.Normal, TextAlignmentOptions.Center).Layout(minW: 16, prefW: 16, flexW: 0);
            Button("Action", edit, "행동", false, 13).Layout(minW: 110, prefW: 110, minH: 28, prefH: 28, flexW: 0);
            Button("Arg", edit, "", false, 13).Layout(minW: 130, prefW: 130, minH: 28, prefH: 28, flexW: 0);
            Button("Delete", edit, "지우기", false, 12).Layout(minW: 50, prefW: 50, minH: 28, prefH: 28, flexW: 0);
            rule.gameObject.SetActive(false);

            var picker = Box("Picker", right, Theme.Bg2);
            VCol(picker.transform, 6, 10, 10, 8, 10);
            var pickerTitle = Text("Title", picker.transform, "", 13, Theme.BronzeHi, false, FontStyles.Bold);
            var options = Rect("Options", picker.transform);
            var og = options.gameObject.AddComponent<GridLayoutGroup>();
            og.cellSize = new Vector2(180, 28);
            og.spacing = new Vector2(4, 4);
            var pick = Button("PickTemplate", options, "", false, 13);
            pick.gameObject.SetActive(false);
            picker.gameObject.SetActive(false);

            var addRow = Rect("AddRow", right);
            HRow(addRow, 0);
            var add = Button("AddRule", addRow, "+ 조건 명령 추가", false, 14).Layout(prefW: 170, prefH: 30);
            var surRow = Rect("SurrenderRow", right);
            HRow(surRow, 0, 0, 0, 6, 0);
            var sur = Button("Surrender", surRow, "□ 항복하지 않고 끝까지 싸우기", false, 14).Layout(prefW: 400, prefH: 30);
            sur.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;

            var so = new UnityEditor.SerializedObject(p);
            void S(string prop, Object v) => so.FindProperty(prop).objectReferenceValue = v;
            S("famValue", famValue); S("famBar", famBar); S("famHint", famHint.gameObject);
            S("stanceGrid", stance); S("rangeGrid", range); S("focusGrid", focus); S("stanceDesc", stanceDesc); S("focusDesc", focusDesc);
            S("atkZoneGrid", atkZone); S("guardZoneGrid", guardZone); S("zoneHint", zoneHint);
            S("orderTemplate", order.rectTransform); S("orderEmpty", orderEmpty.gameObject);
            S("ruleTemplate", rule); S("picker", picker.gameObject); S("pickerTitle", pickerTitle); S("pickTemplate", pick);
            S("addRule", add); S("surrender", sur);
            so.ApplyModifiedPropertiesWithoutUndo();
            return p;
        }

        /// <summary>웹판 .seg.wrap: 값마다 버튼 하나 (이름 = 값). 반환값은 버튼들의 부모</summary>
        static RectTransform ChoiceBlock(Transform parent, string name, string label, Dictionary<string, string> names, float flex, bool hasDesc, out TMP_Text desc)
        {
            var col = Rect(name, parent).Layout(minW: 0, prefW: 0, flexW: flex);
            VCol(col, 6);
            Sublabel(col, label);
            var grid = Rect("Options", col);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(96, 28);
            gl.spacing = new Vector2(4, 4);
            foreach (var kv in names) Button(kv.Key, grid, kv.Value, false, 13);
            desc = hasDesc ? Text("Desc", col, "", 12, Theme.TextFaint) : null;
            return grid;
        }

        /// <summary>장비 칸 넷 (웹판 EquipPanel, .equip-grid 2열). 바꾸기 버튼을 누르면 아래에 후보 목록이 펼쳐진다</summary>
        static EquipPanel BuildEquip(Transform parent)
        {
            var pad = TitledPanel("Equip", parent, "장비", 10);
            pad.parent.Layout(minW: 0, prefW: 0, flexW: 1);
            var p = pad.parent.gameObject.AddComponent<EquipPanel>();
            var slots = new List<RectTransform>();
            string[] ids = Colosseum.Core.Equipment.Slots;
            for (int r = 0; r < 3; r++)
            {
                var row = Rect("Row" + r, pad);
                HRow(row, 10, 0, 0, 0, 0, TextAnchor.UpperLeft);
                for (int c = 0; c < 2; c++)
                {
                    string id = ids[r * 2 + c];
                    var box = Panel(id, row, new Color32(0x2a, 0x1c, 0x1a, 0xff));
                    box.color = new Color32(0x5a, 0x3f, 0x32, 0xff);
                    box.transform.Find("Fill").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                    box.Layout(minW: 0, prefW: 0, flexW: 1);
                    // 왼쪽 아이템 슬롯 + 오른쪽 글 (EquipPanel은 Slot, Info/Item·Stats·Change로 찾는다)
                    HRow(box.transform, 10, 10, 10, 8, 8, TextAnchor.UpperLeft);
                    // 슬롯을 누르면 장비 고르기 팝업 (ItemPicker)
                    var slotBtn = ItemSlot("Slot", box.transform, 72).gameObject.AddComponent<Button>();
                    slotBtn.targetGraphic = slotBtn.GetComponent<Image>();
                    slotBtn.GetComponent<Image>().raycastTarget = true;
                    var info = Rect("Info", box.transform);
                    info.Layout(flexW: 1);
                    VCol(info, 4);
                    Text("Name", info, GearLogic.SlotNames[id], 12, Theme.TextDim);
                    Text("Item", info, "", 15);
                    Text("Stats", info, "", 12, Theme.TextFaint);
                    Button("Change", info, "바꾸기", false, 13).Layout(minH: 28, prefH: 28);
                    slots.Add(box.rectTransform);
                }
            }

            var msg = Text("Msg", pad, "", 14, new Color32(0xd9, 0xa2, 0x9b, 0xff));

            var so = new UnityEditor.SerializedObject(p);
            void S(string prop, Object v) => so.FindProperty(prop).objectReferenceValue = v;
            S("title", pad.parent.Find("Head/Text").GetComponent<TMP_Text>());
            S("msg", msg);
            var sp = so.FindProperty("slots");
            sp.arraySize = slots.Count;
            for (int i = 0; i < slots.Count; i++) sp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return p;
        }

        // 시즌별 기록 칸 (이름, 머리글, 폭)  폭 0 = 남는 폭
        static readonly (string id, string label, float w)[] CareerCols =
        {
            ("Season", "시즌", 36), ("Ludus", "루두스", 0), ("Apps", "출전", 38), ("Wins", "승", 32), ("Losses", "패", 32), ("Kills", "살해", 38),
            ("Injuries", "부상", 38), ("Ca", "능력", 72), ("Pop", "인기", 40), ("Earnings", "수익", 92),
        };

        /// <summary>경력 패널 (웹판 CareerPanel, grid2 1.3fr 1fr): 시즌별 기록 표 + 일대기</summary>
        static CareerPanel BuildCareer(Transform parent)
        {
            var grid = Rect("Career", parent);
            HRow(grid, 14, 0, 0, 0, 0, TextAnchor.UpperLeft);
            var p = grid.gameObject.AddComponent<CareerPanel>();

            var tp = TitledPanel("Seasons", grid, "시즌별 기록", 0);
            tp.parent.Layout(prefW: 0, flexW: 1.3f);
            var head = Box("Head", tp, Theme.Bg2).Layout(minH: 28, prefH: 28);
            HRow(head.transform, 8, 8, 8);
            foreach (var (id, label, w) in CareerCols)
                CareerCell(Text(id, head.transform, label, 12, Theme.TextDim, false, FontStyles.Bold, CareerAlign(id)), w);
            var row = Box("SeasonTemplate", tp, Color.clear).Layout(minH: 30, prefH: 30);
            HRow(row.transform, 8, 8, 8);
            RowLine(row.transform);
            foreach (var (id, _, w) in CareerCols)
                CareerCell(Text(id, row.transform, "", 13, id == "Ludus" ? Theme.Parchment : Theme.Text, false, FontStyles.Normal, CareerAlign(id)), w);
            row.gameObject.SetActive(false);
            var total = Box("Total", tp, Color.clear).Layout(minH: 30, prefH: 30);
            HRow(total.transform, 8, 8, 8);
            var top = Box("TopLine", total.transform, Theme.Bronze);
            top.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            top.rectTransform.Anchor(new Vector2(0, 1), Vector2.one, new Vector2(0, -1), Vector2.zero);
            foreach (var (id, _, w) in CareerCols)
                CareerCell(Text(id, total.transform, id == "Season" ? "통산" : "", 13, Theme.Parchment, false, FontStyles.Bold, CareerAlign(id)), w);

            var lp = TitledPanel("Log", grid, "일대기", 6);
            lp.parent.Layout(minW: 0, prefW: 0, flexW: 1);
            var log = Text("Text", lp, "", 13);
            log.lineSpacing = 14;
            var empty = Text("Empty", lp, "아직 남긴 기록이 없습니다.", 13, Theme.TextFaint);
            var moreRow = Rect("MoreRow", lp);
            HRow(moreRow, 0);
            var more = Button("More", moreRow, "전부 보기", false, 13).Layout(prefW: 140, prefH: 28);

            var so = new UnityEditor.SerializedObject(p);
            void S(string prop, Object v) => so.FindProperty(prop).objectReferenceValue = v;
            S("seasonTemplate", row.rectTransform); S("totalRow", total.rectTransform);
            S("log", log); S("logEmpty", empty.gameObject); S("logMore", more);
            so.ApplyModifiedPropertiesWithoutUndo();
            return p;
        }

        static TextAlignmentOptions CareerAlign(string id) => id is "Season" or "Ludus" ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;

        static void CareerCell(TMP_Text t, float w)
        {
            t.textWrappingMode = TextWrappingModes.NoWrap;
            if (w > 0) t.Layout(minW: w, prefW: w, flexW: 0, minH: 28, prefH: 28);
            else t.Layout(minW: 100, flexW: 1, minH: 28, prefH: 28);
        }

        /// <summary>웹판 .sublabel</summary>
        static TextMeshProUGUI Sublabel(Transform parent, string text) => Text("Sublabel", parent, text, 12, Theme.BronzeHi, false, FontStyles.Bold);

        /// <summary>줄 아래 옅은 선 (웹판 .t td border-bottom)</summary>
        static void RowLine(Transform row)
        {
            var line = Box("Line", row, new Color(Theme.Line.r, Theme.Line.g, Theme.Line.b, 0.6f));
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            line.rectTransform.Anchor(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));
        }

        static TMP_InputField Input(Transform parent, float w)
        {
            var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.name = "Input";
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Theme.Bg2;
            var input = go.GetComponent<TMP_InputField>();
            foreach (var t in go.GetComponentsInChildren<TextMeshProUGUI>(true)) { t.font = Sans; t.fontSize = 14; t.color = Theme.Parchment; }
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.Layout(minW: w, prefW: w, minH: 30, prefH: 30, flexW: 0);
            return input;
        }

        static RectTransform AttrBlock(Transform grid, string name, string title, string[] ids)
        {
            var pad = TitledPanel(name, grid, title, 2);
            pad.parent.Layout(flexW: 1);
            foreach (var a in ids)
            {
                var row = Rect(a, pad).Layout(minH: 22);
                HRow(row, 8);
                Text("Label", row, Attrs.Names[a], 14).Layout(flexW: 1);
                Text("Value", row, "0", 14, Theme.Parchment, false, FontStyles.Normal, TextAlignmentOptions.MidlineRight).Layout(minW: 44, prefW: 44, flexW: 0);
            }
            return pad;
        }
    }
}
