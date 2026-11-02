// 대륙 정세 화면 조립 (웹판 WorldScreen.tsx + components/WarPanel.tsx, styles.css .nation-card .nd-* .war-* .contract .pick).
// 드롭다운이 없어 탭 아래 전체를 스크롤 영역에 둔다. 나라 카드·관계·전쟁·의뢰·파견·호감도 줄은 모두 비활성 원본을 복제한다.
using Colosseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class WorldBuilder
    {
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);
        static readonly Color CardLine = new Color32(0x4a, 0x3b, 0x2c, 0xff);

        public static ScreenBase Build(Transform main)
        {
            var rt = Rect("Screen_world", main).Stretch(26, 20, 26, 20);
            VCol(rt, 12);
            ScreenHeader(rt, "대륙 정세", "");
            var sub = rt.Find("Sub").GetComponent<TMP_Text>();
            var tabRow = Rect("Tabs", rt);
            HRow(tabRow, 4);
            var tabNations = Button("Tab_nations", tabRow, "나라", false, 14).Layout(prefW: 128, prefH: 32);
            var tabWar = Button("Tab_war", tabRow, "전쟁·용병", false, 14).Layout(prefW: 140, prefH: 32);

            var content = ScrollView("Scroll", rt, out var scroll, 0);
            scroll.Layout(flexH: 1, flexW: 1);

            var s = rt.gameObject.AddComponent<WorldScreen>();
            s.screenId = "world";
            Set(s, "sub", sub);
            Set(s, "tabNations", tabNations);
            Set(s, "tabWar", tabWar);

            BuildNations(content, s);
            BuildWar(content, s);

            rt.gameObject.SetActive(false);
            return s;
        }

        // ── 나라 탭 ──
        static void BuildNations(Transform content, WorldScreen s)
        {
            var root = Rect("Nations", content);
            VCol(root, 14);
            Set(s, "nationsRoot", root.gameObject);

            // 나라 카드 여섯 장 (웹판 .nation-pick.compact: 6열)
            var cards = Rect("Cards", root);
            HRow(cards, 10);
            var card = Box("CardTemplate", cards, CardLine).Layout(minH: 60, prefH: 60, minW: 0, prefW: 0, flexW: 1);
            var fill = Box("Fill", card.transform, Theme.Bg2);
            fill.rectTransform.Stretch(1, 1, 1, 1);
            fill.raycastTarget = false;
            fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var cb = card.gameObject.AddComponent<Button>();
            cb.targetGraphic = card;
            HRow(card.transform, 10, 10, 10, 8, 8);
            Crest(card.transform, 36);
            var info = Rect("Info", card.transform).Layout(flexW: 1);
            VCol(info, 2).childAlignment = TextAnchor.MiddleLeft;
            Text("Name", info, "", 15, Theme.Parchment, true, FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
            Text("League", info, "", 11, Theme.TextDim);
            card.gameObject.SetActive(false);
            Set(s, "cardTemplate", cb);

            // 나라 설명 | 리그 특징 + 관계 (웹판 grid2 1.6fr 1fr)
            var grid = Rect("Detail", root);
            HRow(grid, 14, 0, 0, 0, 0, TextAnchor.UpperLeft);
            var np = TitledPanel("Nation", grid, "나라", 8);
            np.parent.Layout(minW: 0, prefW: 0, flexW: 1.6f);
            Set(s, "nationTitle", np.parent.Find("Head/Text").GetComponent<TMP_Text>());
            Set(s, "nationMeta", Text("Meta", np, "", 13, Theme.BronzeHi));
            var lore = Text("Lore", np, "", 15, Theme.Parchment);
            lore.lineSpacing = 30;
            Set(s, "nationLore", lore);

            var col = Rect("Side", grid).Layout(minW: 0, prefW: 0, flexW: 1);
            VCol(col, 14);
            var rp = TitledPanel("Rules", col, "리그 특징", 6);
            var rules = Text("Text", rp, "", 14);
            rules.lineSpacing = 24;
            Set(s, "rulesText", rules);
            Text("Sublabel", rp, "즐겨 쓰는 스타일", 12, Theme.BronzeHi, false, FontStyles.Bold).margin = new Vector4(0, 6, 0, 0);
            Set(s, "stylesText", Text("Styles", rp, "", 14));

            var relp = TitledPanel("Relations", col, "다른 나라와의 관계", 0);
            var rel = Row("RelTemplate", relp, 32);
            Cell(Text("Name", rel, "", 14).rectTransform, 0);
            Cell(Text("Value", rel, "", 14, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.MidlineRight).rectTransform, 50);
            Cell(Text("Label", rel, "", 14).rectTransform, 60);
            rel.gameObject.SetActive(false);
            Set(s, "relTemplate", rel);
        }

        // ── 전쟁·용병 탭 (웹판 WarPanel) ──
        static void BuildWar(Transform content, WorldScreen s)
        {
            var root = Rect("War", content);
            VCol(root, 14);
            Set(s, "warRoot", root.gameObject);
            root.gameObject.SetActive(false);

            // 진행 중인 전쟁 (웹판 .war-row: 140px 1fr 140px 110px)
            var wp = TitledPanel("Wars", root, "벌어지고 있는 전쟁", 6);
            Set(s, "warEmpty", Text("Empty", wp, "대륙은 지금 잠잠합니다. 관계가 -50 아래로 떨어진 나라끼리는 언제든 전쟁이 날 수 있습니다.", 13, Theme.TextFaint).gameObject);
            var war = Rect("WarTemplate", wp).Layout(minH: 36, prefH: 36);
            HRow(war, 10);
            var sideA = Rect("SideA", war).Layout(minW: 140, prefW: 140, flexW: 0);
            HRow(sideA, 6);
            Crest(sideA, 28);
            Text("Name", sideA, "", 15, Theme.Parchment, false, FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
            var bar = Box("Bar", war, Theme.Line).Layout(minH: 12, prefH: 12, minW: 60, flexW: 1);
            var a = Box("A", bar.transform, Theme.Torch);
            a.rectTransform.Anchor(Vector2.zero, new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(0, -1));
            var b = Box("B", bar.transform, Theme.Blood);
            b.rectTransform.Anchor(new Vector2(0.5f, 0), Vector2.one, new Vector2(0, 1), new Vector2(-1, -1));
            var sideB = Rect("SideB", war).Layout(minW: 140, prefW: 140, flexW: 0);
            HRow(sideB, 6, 0, 0, 0, 0, TextAnchor.MiddleRight);
            Text("Name", sideB, "", 15, Theme.Parchment, false, FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
            Crest(sideB, 28);
            Text("Remain", war, "", 12, Theme.TextFaint).Layout(minW: 110, prefW: 110, flexW: 0);
            war.gameObject.SetActive(false);
            Set(s, "warTemplate", war);

            // 전장에 나간 검투사
            var dp = TitledPanel("Deployed", root, "전장에 나간 검투사", 0);
            Set(s, "deployedRoot", dp.parent.gameObject);
            var dep = Row("DeployTemplate", dp, 40);
            dep.GetComponent<LayoutElement>().preferredHeight = -1;
            Cell(Text("Desc", dep, "", 14).rectTransform, 200);
            var names = Rect("Names", dep).Layout(minW: 120, flexW: 1);
            var ng = names.gameObject.AddComponent<GridLayoutGroup>();
            ng.cellSize = new Vector2(170, 30);
            ng.spacing = new Vector2(10, 4);
            ng.padding = new RectOffset(0, 0, 5, 5);
            var glad = Box("GladTemplate", names, Color.clear);
            glad.gameObject.AddComponent<Button>().targetGraphic = glad;
            HRow(glad.transform, 6);
            Portrait("Portrait", glad.transform, 24);
            Text("Name", glad.transform, "", 14, Theme.Parchment).Layout(flexW: 1).textWrappingMode = TextWrappingModes.NoWrap;
            glad.gameObject.SetActive(false);
            Cell(Text("Hint", dep, "", 12, Theme.TextFaint).rectTransform, 200);
            dep.gameObject.SetActive(false);
            Set(s, "deployTemplate", dep);

            // 용병 의뢰 게시판
            var bp = TitledPanel("Board", root, "용병 의뢰 게시판", 10);
            Set(s, "boardEmpty", Text("Empty", bp, "지금 올라온 의뢰가 없습니다. 전쟁이 나면 두 나라가 의뢰를 붙입니다.", 13, Theme.TextFaint).gameObject);
            Set(s, "contractTemplate", ContractCard(bp));

            // 나라별 호감도
            var fp = TitledPanel("Favor", root, "나라별 호감도", 0);
            var fav = Row("FavorTemplate", fp, 32);
            Cell(Text("Name", fav, "", 14).rectTransform, 160);
            Cell(Text("Value", fav, "", 14, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.MidlineRight).rectTransform, 60);
            Cell(Text("Hint", fav, "", 12, Theme.TextFaint).rectTransform, 0);
            fav.gameObject.SetActive(false);
            Set(s, "favorTemplate", fav);
        }

        /// <summary>의뢰 카드 (웹판 ContractCard, .contract): 머리 + 막힌 이유 / 파견할 검투사 고르기 + 파견 버튼</summary>
        static RectTransform ContractCard(Transform parent)
        {
            var card = Box("ContractTemplate", parent, Theme.Line);
            var fill = Box("Fill", card.transform, Theme.Bg2);
            fill.rectTransform.Stretch(1, 1, 1, 1);
            fill.raycastTarget = false;
            fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            VCol(card.transform, 8, 10, 10, 10, 10);

            var head = Rect("Head", card.transform);
            HRow(head, 10);
            Crest(head, 30);
            var info = Rect("Info", head).Layout(minW: 0, prefW: 0, flexW: 1);
            VCol(info, 2);
            Text("Name", info, "", 15, Theme.Parchment, true, FontStyles.Bold);
            Text("Desc", info, "", 12, Theme.TextFaint);
            var terms = Rect("Terms", head).Layout(minW: 320, prefW: 320, flexW: 0);
            VCol(terms, 2);
            Text("Pay", terms, "", 14, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
            Text("Detail", terms, "", 12, Theme.TextFaint, false, FontStyles.Normal, TextAlignmentOptions.MidlineRight);

            Text("Blocked", card.transform, "", 13, Neg);

            // 고르기: 체크 글자 + 초상화 + 이름 + 별 (웹판 .pick)
            var picks = Rect("Picks", card.transform);
            var pg = picks.gameObject.AddComponent<GridLayoutGroup>();
            pg.cellSize = new Vector2(280, 34);
            pg.spacing = new Vector2(6, 6);
            var pick = Box("PickTemplate", picks, Theme.Line);
            var pf = Box("Fill", pick.transform, Theme.Bg2);
            pf.rectTransform.Stretch(1, 1, 1, 1);
            pf.raycastTarget = false;
            pf.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var pb = pick.gameObject.AddComponent<Button>();
            pb.targetGraphic = pick;
            var colors = pb.colors;
            colors.disabledColor = new Color(1, 1, 1, 0.4f);
            pb.colors = colors;
            HRow(pick.transform, 6, 8, 8);
            Text("Check", pick.transform, "□", 14, Theme.Torch).Layout(minW: 14, prefW: 14, flexW: 0);
            Portrait("Portrait", pick.transform, 24);
            Text("Name", pick.transform, "", 14, Theme.Parchment).Layout(flexW: 1).textWrappingMode = TextWrappingModes.NoWrap;
            var stars = Text("Stars", pick.transform, "", 12, Theme.Torch, false, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
            stars.textWrappingMode = TextWrappingModes.NoWrap;
            pick.gameObject.SetActive(false);

            Text("Err", card.transform, "", 13, Neg);
            var sendRow = Rect("SendRow", card.transform);
            HRow(sendRow, 0);
            Button("Send", sendRow, "0명 보내기", true, 14).Layout(prefW: 130, prefH: 32);

            card.gameObject.SetActive(false);
            return card.rectTransform;
        }

        static Image Crest(Transform parent, float size)
        {
            var img = Rect("Crest", parent).gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.Layout(minW: size, prefW: size, minH: size, prefH: size, flexW: 0);
            return img;
        }

        static RectTransform Row(string name, Transform parent, float h)
        {
            var row = Box(name, parent, new Color(0, 0, 0, 0)).Layout(minH: h, prefH: h);
            HRow(row.transform, 8, 12, 12);
            var line = Box("Line", row.transform, new Color(Theme.Line.r, Theme.Line.g, Theme.Line.b, 0.6f));
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            line.rectTransform.Anchor(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));
            return row.rectTransform;
        }

        static void Cell(RectTransform rt, float w)
        {
            var t = rt.GetComponent<TMP_Text>();
            if (t != null) t.textWrappingMode = TextWrappingModes.NoWrap;
            if (w > 0) rt.Layout(minW: w, prefW: w, flexW: 0);
            else rt.Layout(minW: 80, flexW: 1);
        }
    }
}
