// 이적 시장 화면 조립 (웹판 MarketScreen.tsx + GearPanels.tsx, styles.css .seg .search-filters .negotiate).
// 검색 조건 드롭다운은 스크롤 영역 밖에 두고, 결과 표만 스크롤한다 (펼침 목록이 RectMask2D에 잘리지 않게).
using System.Collections.Generic;
using Colosseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class MarketBuilder
    {
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);
        static readonly (string id, string label, float w)[] ResultCols =
        {
            ("Name", "이름", 180), ("Race", "종족", 64), ("Age", "나이", 46), ("Style", "스타일", 86), ("Owner", "소속", 0),
            ("Ca", "추정 능력", 140), ("Pa", "추정 잠재", 146), ("Interest", "관심도", 76), ("Info", "정보", 72), ("Wage", "주급", 70), ("Value", "추정 가치", 92), ("Act", "", 132),
        };

        public static ScreenBase Build(Transform main)
        {
            var rt = Rect("Screen_market", main).Stretch(26, 20, 26, 20);
            VCol(rt, 12);
            ScreenHeader(rt, "이적 시장", "");
            var sub = rt.Find("Sub").GetComponent<TMP_Text>();
            var tabRow = Rect("Tabs", rt);
            HRow(tabRow, 4);
            var tabs = new List<Button>();
            foreach (var l in new[] { "선수 찾기", "관심 명단", "협상·제안", "우리 검투사", "장비 시장", "창고" })
                tabs.Add(Button("Tab_" + tabs.Count, tabRow, l, false, 14).Layout(prefW: 128, prefH: 32));
            var body = Rect("Body", rt).Layout(flexH: 1, flexW: 1);

            var s = rt.gameObject.AddComponent<MarketScreen>();
            s.screenId = "market";
            Set(s, "sub", sub);
            SetList(s, "tabButtons", tabs);

            var roots = new List<Object>();

            // ── 선수 찾기 ──
            var search = Tab(body, "Search", roots);
            var fp = TitledPanel("Filters", search, "검색 조건", 8);
            var f1 = FilterRow(fp);
            Set(s, "scope", Filter(f1, "대상", 130));
            Set(s, "nation", Filter(f1, "나라", 130));
            Set(s, "race", Filter(f1, "종족", 110));
            Set(s, "style", Filter(f1, "스타일", 120));
            Set(s, "nameFilter", Input(Field(f1, "이름"), 140, false));
            var f2 = FilterRow(fp);
            Set(s, "maxAge", Input(Field(f2, "나이 ≤"), 80, true));
            Set(s, "minCa", Filter(f2, "추정 능력 ≥", 110));
            Set(s, "minPa", Filter(f2, "추정 잠재 ≥", 110));
            Set(s, "maxWage", Input(Field(f2, "주급 ≤"), 80, true));
            Set(s, "minInterest", Filter(f2, "이적 관심도 ≥", 120));
            Set(s, "sortBy", Filter(f2, "정렬", 130));
            ResultTable(search, s, "search");

            // ── 관심 명단 ──
            ResultTable(Tab(body, "Shortlist", roots), s, "short");

            // ── 협상·제안 ──
            var deals = Tab(body, "Deals", roots);
            Set(s, "dealsMsg", Text("Msg", deals, "", 14, Neg));
            var dp = TitledPanel("Deals", deals, "이적료 합의 — 개인 계약 협상", 0);
            Set(s, "dealEmpty", Text("Empty", dp, "진행 중인 협상이 없습니다. 다른 루두스 검투사의 상세 화면에서 이적료를 제안하면 협상을 시작합니다.", 14, Theme.TextDim).gameObject);
            var deal = Row("DealTemplate", dp, 58);
            NameCell(deal, 200, true);
            var dinfo = Rect("Info", deal);
            dinfo.Layout(minW: 120, flexW: 1);
            VCol(dinfo, 2).childAlignment = TextAnchor.MiddleLeft;
            Text("Desc", dinfo, "", 14);
            Text("Mood", dinfo, "", 13, Theme.TextDim);
            Cell(Text("Refuse", deal, "", 13, Neg).rectTransform, 320);
            var nego = Rect("Negotiate", deal);
            HRow(nego, 6);
            nego.Layout(minW: 420, prefW: 420, flexW: 0, minH: 30, prefH: 30);
            Text("Want", nego, "요구 주급 0 D", 13, Theme.TextDim).Layout(prefW: 100, minW: 100);
            Input(nego, 90, true).name = "Wage";
            Dropdown("Seasons", nego, 90);
            Button("Offer", nego, "계약 제안", true, 14).Layout(prefW: 110, minW: 110, prefH: 30);
            deal.gameObject.SetActive(false);
            Set(s, "dealTemplate", deal);

            var op = TitledPanel("Offers", deals, "받은 이적 제안", 0);
            Set(s, "offerEmpty", Text("Empty", op, "받은 제안이 없습니다.", 14, Theme.TextDim).gameObject);
            var offer = Row("OfferTemplate", op, 44);
            NameCell(offer, 200, false);
            Cell(Text("From", offer, "", 14).rectTransform, 0);
            Cell(Text("Fee", offer, "", 14, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.MidlineRight).rectTransform, 110);
            Button("Accept", offer, "팔기", true, 14).Layout(minW: 76, prefW: 76, prefH: 30, flexW: 0);
            Button("Reject", offer, "거절", false, 14).Layout(minW: 76, prefW: 76, prefH: 30, flexW: 0);
            offer.gameObject.SetActive(false);
            Set(s, "offerTemplate", offer);

            // ── 우리 검투사 ──
            var mine = Tab(body, "Mine", roots);
            var mp = TitledPanel("Mine", mine, "우리 검투사 — 방출", 0);
            Grow(mp);
            Text("Hint", mp, "검투사를 방출하면 한 푼도 받지 못하고 무소속으로 풀어 줍니다. 이적료를 받으려면 이적 시장 기간에 다른 루두스의 제안을 기다립니다.", 13, Theme.TextDim).Layout(prefH: 30);
            var mineContent = ScrollView("Scroll", mp, out var mineScroll);
            mineScroll.Layout(flexH: 1);
            var mrow = Row("MineTemplate", mineContent, 40);
            NameCell(mrow, 220, false);
            Cell(Text("Style", mrow, "", 14).rectTransform, 100);
            Cell(Text("Wage", mrow, "", 14, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.MidlineRight).rectTransform, 110);
            Cell(Text("Value", mrow, "", 14, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.MidlineRight).rectTransform, 160);
            Rect("Gap", mrow).Layout(flexW: 1);
            var rel = Button("Release", mrow, "방출", false, 14).Layout(minW: 200, prefW: 200, prefH: 30, flexW: 0);
            rel.GetComponent<Image>().color = Theme.Blood;
            mrow.gameObject.SetActive(false);
            Set(s, "mineTemplate", mrow);

            // ── 장비 시장 ──
            var gear = Tab(body, "Gear", roots);
            var gp = TitledPanel("Filters", gear, "매물 찾기", 8);
            var g1 = FilterRow(gp);
            Set(s, "gearKind", Filter(g1, "종류", 100));
            Set(s, "gearQuality", Filter(g1, "품질 ≥", 100));
            Set(s, "gearNation", Filter(g1, "파는 나라", 130));
            Set(s, "gearMaxPrice", Input(Field(g1, "가격 ≤"), 90, true));
            Set(s, "gearName", Input(Field(g1, "이름"), 140, false));
            Set(s, "gearSort", Filter(g1, "정렬", 130));
            HagglePanel(gear, s);
            var gearRow = ItemTable(gear, s, "gear", "매물", 148, ("Seller", "판매자", 230), ("Price", "가격", 100));
            Button("Haggle", gearRow, "흥정", false, 14).Layout(minW: 70, prefW: 70, prefH: 30, flexW: 0);
            Button("Buy", gearRow, "사기", true, 14).Layout(minW: 70, prefW: 70, prefH: 30, flexW: 0);

            // ── 창고 ──
            var armory = Tab(body, "Armory", roots);
            ConsignPanel(armory, s);
            var crow = ItemTable(armory, s, "consigned", "내놓은 장비", 100, ("Price", "내놓은 값", 100), ("Via", "판매 방식", 70), ("Chance", "팔릴 확률", 100));
            Button("Withdraw", crow, "거둬들이기", false, 14).Layout(minW: 100, prefW: 100, prefH: 30, flexW: 0);
            var arow = ItemTable(armory, s, "armory", "창고", 168, ("Sell", "파는 값", 100));
            Button("Consign", arow, "내놓기", false, 14).Layout(minW: 90, prefW: 90, prefH: 30, flexW: 0);
            Button("SellButton", arow, "팔기", false, 14).Layout(minW: 70, prefW: 70, prefH: 30, flexW: 0);

            var so = new UnityEditor.SerializedObject(s);
            var p = so.FindProperty("tabRoots");
            p.arraySize = roots.Count;
            for (int i = 0; i < roots.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = roots[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            rt.gameObject.SetActive(false);
            return s;
        }

        /// <summary>탭 하나: Body를 꽉 채우는 세로 칸</summary>
        static RectTransform Tab(Transform body, string name, List<Object> roots)
        {
            var t = Rect(name, body).Stretch();
            VCol(t, 12);
            roots.Add(t.gameObject);
            if (roots.Count > 1) t.gameObject.SetActive(false);
            return t;
        }

        /// <summary>선수 결과 표 (웹판 ResultTable). 칸 이름 접두어: search / short</summary>
        static void ResultTable(Transform parent, MarketScreen s, string key)
        {
            var pad = TitledPanel("Results", parent, "0명", 0);
            Grow(pad);
            Set(s, key + "Title", pad.parent.Find("Head/Text").GetComponent<TMP_Text>());
            Set(s, key + "Msg", Text("Msg", pad, "", 14, Neg));
            Head(pad, ResultCols);
            var content = ScrollView("Scroll", pad, out var scroll);
            scroll.Layout(flexH: 1);
            Set(s, key + "Empty", Text("Empty", content, "조건에 맞는 검투사가 없습니다.", 14, Theme.TextDim).Layout(prefH: 36).gameObject);
            var row = Row(key + "Template", content, 40);
            foreach (var c in ResultCols)
            {
                switch (c.id)
                {
                    case "Name": NameCell(row, c.w, true); break;
                    case "Info":
                        var holder = Rect("Info", row);
                        Cell(holder, c.w);
                        HRow(holder, 0);
                        Bar("Bar", holder);
                        break;
                    case "Act":
                        Button("Star", row, "☆", false, 15).Layout(minW: 36, prefW: 36, prefH: 30, flexW: 0);
                        Button("Scout", row, "정찰", false, 13).Layout(minW: 88, prefW: 88, prefH: 30, flexW: 0);
                        break;
                    default:
                        var right = c.id is "Wage" or "Value";
                        var t = Text(c.id, row, "", c.id is "Ca" or "Pa" ? 13 : 14, c.id is "Ca" or "Pa" ? Theme.Torch : Theme.Text, false, FontStyles.Normal,
                            right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft);
                        t.textWrappingMode = TextWrappingModes.NoWrap;
                        Cell(t.rectTransform, c.w);
                        break;
                }
            }
            row.gameObject.SetActive(false);
            Set(s, key + "Template", row);
        }

        /// <summary>장비 표: 장비·종류·성능 + 추가 칸. 줄 원본을 돌려주고 버튼은 부른 쪽이 붙인다</summary>
        static RectTransform ItemTable(Transform parent, MarketScreen s, string key, string title, float btnW, params (string id, string label, float w)[] extra)
        {
            // 내놓은 장비 표는 몇 줄 안 되니 스크롤 없이 패널 높이만큼만
            bool consigned = key == "consigned";
            var pad = TitledPanel(consigned ? "Consigned" : "Items", parent, title, 0);
            var cols = new List<(string, string, float)> { ("Slot", "", 40), ("Item", "장비", 220), ("Grade", "등급", 70), ("Kind", "종류", 70), ("Stats", "성능", 0) };
            cols.AddRange(extra);
            cols.Add(("Btn", "", btnW));
            RectTransform content;
            if (consigned)
            {
                Set(s, "consignedRoot", pad.parent.gameObject);
                Head(pad, cols.ToArray());
                content = pad;
            }
            else
            {
                Grow(pad);
                Set(s, key + "Title", pad.parent.Find("Head/Text").GetComponent<TMP_Text>());
                Set(s, key + "Msg", Text("Msg", pad, "", 14, Neg));
                Head(pad, cols.ToArray());
                content = ScrollView("Scroll", pad, out var scroll);
                scroll.Layout(flexH: 1);
                var empty = key == "gear" ? "조건에 맞는 매물이 없습니다." : "창고가 비었습니다. 장비는 장비 시장에서 사거나 던전 원정과 사투에서 얻습니다.";
                Set(s, key + "Empty", Text("Empty", content, empty, 14, Theme.TextDim).Layout(prefH: 36).gameObject);
            }
            var row = Row(key + "Template", content, 48);
            ItemSlot("Slot", row, 40).Layout(minW: 40, prefW: 40, minH: 40, prefH: 40, flexW: 0);
            Cell(Text("Item", row, "", 14).rectTransform, 220);
            Cell(Text("Grade", row, "", 13).rectTransform, 70);
            Cell(Text("Kind", row, "", 14).rectTransform, 70);
            Cell(Text("Stats", row, "", 13, Theme.TextDim).rectTransform, 0);
            foreach (var (id, _, w) in extra)
                Cell(Text(id, row, "", 14, Theme.Text, false, FontStyles.Normal, id is "Price" or "Sell" ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft).rectTransform, w);
            row.gameObject.SetActive(false);
            Set(s, key + "Template", row);
            return row;
        }

        /// <summary>장비 흥정 패널 (장비 시장 탭, 매물 줄의 흥정 버튼으로 열린다)</summary>
        static void HagglePanel(Transform parent, MarketScreen s)
        {
            var pad = TitledPanel("Haggle", parent, "흥정", 8);
            ((RectTransform)pad.parent).GetComponent<Image>().color = Theme.Torch;
            Set(s, "haggleRoot", pad.parent.gameObject);
            Set(s, "haggleTitle", pad.parent.Find("Head/Text").GetComponent<TMP_Text>());
            Set(s, "haggleStatus", Text("Status", pad, "", 14));
            var cashRow = Rect("Cash", pad);
            HRow(cashRow, 14);
            Set(s, "haggleCash", Input(Field(cashRow, "현금"), 110, true));
            Set(s, "haggleTotal", Text("Total", cashRow, "", 15, Theme.Parchment).Layout(flexW: 1));
            Text("TradeLabel", pad, "맞교환할 창고 장비 — 눌러서 넣고 뺍니다", 13, Theme.TextDim);
            var content = ScrollView("Trade", pad, out var scroll, 2);
            scroll.Layout(minH: 96, prefH: 96);
            Set(s, "tradeEmpty", Text("Empty", content, "창고가 비었습니다.", 13, Theme.TextFaint).Layout(prefH: 26).gameObject);
            var tpl = Button("TradeTemplate", content, "장비", false, 14).Layout(prefH: 28, minH: 28);
            tpl.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
            tpl.gameObject.SetActive(false);
            Set(s, "tradeTemplate", tpl);
            var btns = Rect("Buttons", pad);
            HRow(btns, 8);
            Set(s, "haggleOffer", Button("Offer", btns, "제안", true, 15).Layout(prefW: 160, prefH: 34));
            Set(s, "haggleClose", Button("Close", btns, "취소", false, 14).Layout(prefW: 110, prefH: 34));
            pad.parent.gameObject.SetActive(false);
        }

        /// <summary>위탁 판매 패널 (창고 탭, 줄의 내놓기 버튼으로 열린다)</summary>
        static void ConsignPanel(Transform parent, MarketScreen s)
        {
            var pad = TitledPanel("Consign", parent, "내놓기", 8);
            ((RectTransform)pad.parent).GetComponent<Image>().color = Theme.Torch;
            Set(s, "consignRoot", pad.parent.gameObject);
            Set(s, "consignTitle", pad.parent.Find("Head/Text").GetComponent<TMP_Text>());
            var priceRow = Rect("Price", pad);
            HRow(priceRow, 14);
            Set(s, "consignPrice", Input(Field(priceRow, "내놓을 값"), 110, true));
            Text("D", priceRow, "D", 14, Theme.TextDim);
            Set(s, "consignHint", Text("Hint", pad, "", 14));
            var btns = Rect("Buttons", pad);
            HRow(btns, 8);
            Set(s, "consignAgent", Button("Agent", btns, "중개인에게 맡기기", true, 15).Layout(prefW: 190, prefH: 34));
            Set(s, "consignSelf", Button("Self", btns, "직접 팔기", false, 15).Layout(prefW: 160, prefH: 34));
            Set(s, "consignClose", Button("Close", btns, "취소", false, 14).Layout(prefW: 110, prefH: 34));
            pad.parent.gameObject.SetActive(false);
        }

        /// <summary>초상화 + 이름. click이면 누르면 검투사 화면으로</summary>
        static void NameCell(RectTransform row, float w, bool click)
        {
            var name = Rect("Name", row);
            Cell(name, w);
            HRow(name, 8);
            if (click)
            {
                var img = name.gameObject.AddComponent<Image>();
                img.color = new Color(0, 0, 0, 0);
                name.gameObject.AddComponent<Button>().targetGraphic = img;
            }
            Portrait("Portrait", name, 28);
            Text("Text", name, "이름", 15, Theme.Parchment, false, FontStyles.Bold).Layout(flexW: 1);
        }

        /// <summary>패널이 남는 높이를 다 먹게 한다 (pad = TitledPanel 반환값)</summary>
        static void Grow(RectTransform pad)
        {
            pad.Layout(flexH: 1);
            ((RectTransform)pad.parent).Layout(flexH: 1);
        }

        static RectTransform FilterRow(Transform parent)
        {
            var r = Rect("Row", parent);
            HRow(r, 16);
            return r;
        }

        /// <summary>웹판 .search-filters label: 작은 이름표 + 입력</summary>
        static RectTransform Field(Transform parent, string label)
        {
            var f = Rect("Field", parent);
            HRow(f, 6);
            Text("Label", f, label, 13, Theme.TextDim).textWrappingMode = TextWrappingModes.NoWrap;
            return f;
        }

        static TMP_Dropdown Filter(Transform row, string label, float w) => Dropdown("Select", Field(row, label), w);

        static TMP_InputField Input(Transform parent, float w, bool number)
        {
            var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.name = "Input";
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Theme.Bg2;
            var input = go.GetComponent<TMP_InputField>();
            foreach (var t in go.GetComponentsInChildren<TextMeshProUGUI>(true)) { t.font = Sans; t.fontSize = 14; t.color = Theme.Parchment; }
            input.placeholder.GetComponent<TMP_Text>().text = number ? "제한 없음" : "";
            input.placeholder.GetComponent<TMP_Text>().color = Theme.TextFaint;
            if (number) input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.Layout(minW: w, prefW: w, minH: 30, prefH: 30, flexW: 0);
            return input;
        }

        static void Head(Transform parent, params (string id, string label, float w)[] cols)
        {
            var head = Box("Head", parent, Theme.Bg2).Layout(minH: 28, prefH: 28);
            HRow(head.transform, 8, 8, 8);
            foreach (var (id, label, w) in cols)
            {
                var right = id is "Wage" or "Value" or "Price" or "Sell" or "Fee";
                var t = Text("H", head.transform, label, 13, Theme.TextDim, false, FontStyles.Bold, right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft);
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
