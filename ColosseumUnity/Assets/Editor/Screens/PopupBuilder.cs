// 맨 위 오버레이 둘: 검투사 팝업 카드(GladiatorPopup)와 툴팁(TipLayer). 캔버스 마지막 자식으로 붙인다 (툴팁이 맨 위).
using Colosseum.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class PopupBuilder
    {
        public static void BuildGladiatorPopup(Transform canvas)
        {
            var root = Rect("GladiatorPopup", canvas).Stretch();
            var overlay = Rect("Overlay", root).Stretch();
            var dim = Box("Dim", overlay, new Color(0, 0, 0, 0.62f));
            ((RectTransform)dim.transform).Stretch();
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;

            var card = Panel("Card", overlay);
            var crt = (RectTransform)card.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(820, 540);
            Box("Accent", card.transform, Theme.Bronze).rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, -4), new Vector2(-1, -1));

            var portrait = Portrait("Portrait", card.transform, 230);
            ((RectTransform)portrait.transform.parent.parent).Anchor(new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -254), new Vector2(252, -24));

            var right = Rect("Right", card.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(272, -300), new Vector2(-22, -22));
            VCol(right, 8);
            var name = Text("Name", right, "이름", 28, Theme.Parchment, true, FontStyles.Bold);
            var sub = Text("Sub", right, "", 15, Theme.TextDim);
            Box("Line", right, Theme.Line).Layout(minH: 1, prefH: 1);
            var info = Text("Info", right, "", 15, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            info.lineSpacing = 18;
            Text("TraitsLabel", right, "특성", 13, Theme.BronzeHi, false, FontStyles.Bold);
            var traits = Text("Traits", right, "", 15, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.TopLeft);

            Text("GearLabel", card.transform, "장착 장비", 13, Theme.BronzeHi, false, FontStyles.Bold)
                .rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -292), new Vector2(-22, -270));
            var grid = Rect("Gear", card.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -470), new Vector2(-22, -298));
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(250, 82);
            gl.spacing = new Vector2(12, 10);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 3;
            var slots = new System.Collections.Generic.List<RectTransform>();
            foreach (var (id, label) in new[] { ("main", "주무기"), ("off", "보조"), ("helmet", "투구"), ("armor", "갑옷"), ("arms", "팔"), ("legs", "다리") })
            {
                var box = Panel(id, grid, Theme.Panel2);
                // GladiatorPopup은 box.Find("Slot"/"Item"/"Stats")로 찾는다. 왼쪽 아이템 슬롯, 오른쪽 글
                ItemSlot("Slot", box.transform, 68).Anchor(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(7, -34), new Vector2(75, 34));
                var lab = Text("Label", box.transform, label, 12, Theme.TextFaint);
                lab.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(84, -24), new Vector2(-8, -6));
                Text("Item", box.transform, "", 15, Theme.Parchment).rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(84, -48), new Vector2(-8, -24));
                Text("Stats", box.transform, "", 13, Theme.TextDim).rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(84, -74), new Vector2(-8, -50));
                slots.Add((RectTransform)box.transform);
            }

            var buttons = Rect("Buttons", card.transform).Anchor(new Vector2(0, 0), new Vector2(1, 0), new Vector2(22, 16), new Vector2(-22, 58));
            HRow(buttons, 10, align: TextAnchor.MiddleRight);
            var detail = Button("Detail", buttons, "자세히 보기", true).Layout(prefW: 160, prefH: 40);
            var close = Button("Close", buttons, "닫기").Layout(prefW: 110, prefH: 40);

            var popup = root.gameObject.AddComponent<GladiatorPopup>();
            var so = new SerializedObject(popup);
            void S(string p, Object v) => so.FindProperty(p).objectReferenceValue = v;
            S("overlay", overlay.gameObject); S("dim", dimBtn); S("portrait", portrait); S("nameText", name); S("subText", sub);
            S("info", info); S("traits", traits); S("detailButton", detail); S("closeButton", close);
            var sl = so.FindProperty("slots");
            sl.arraySize = slots.Count;
            for (int i = 0; i < slots.Count; i++) sl.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>정보 카드 (나라·루두스·대회·장비 링크): 제목·부제·스크롤 본문, 뒤로·이동·닫기</summary>
        /// <summary>장비 고르기 팝업 (ItemPicker): 후보를 아이템 슬롯 격자로</summary>
        public static void BuildItemPicker(Transform canvas)
        {
            var root = Rect("ItemPicker", canvas).Stretch();
            var overlay = Rect("Overlay", root).Stretch();
            var dim = Box("Dim", overlay, new Color(0, 0, 0, 0.62f));
            ((RectTransform)dim.transform).Stretch();
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;

            var card = Panel("Card", overlay);
            var crt = (RectTransform)card.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(860, 600);
            Box("Accent", card.transform, Theme.Bronze).rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, -4), new Vector2(-1, -1));
            var title = Text("Title", card.transform, "", 24, Theme.Parchment, true, FontStyles.Bold);
            title.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -56), new Vector2(-24, -16));
            var current = Text("Current", card.transform, "", 14, Theme.Text);
            current.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -84), new Vector2(-24, -58));
            Box("Line", card.transform, Theme.Line).rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -93), new Vector2(-24, -92));

            var content = ScrollView("Scroll", card.transform, out var scrollRoot, 0);
            scrollRoot.Anchor(Vector2.zero, Vector2.one, new Vector2(24, 92), new Vector2(-16, -104));
            Object.DestroyImmediate(content.GetComponent<VerticalLayoutGroup>());
            var gl = content.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(260, 84);
            gl.spacing = new Vector2(10, 10);
            gl.padding = new RectOffset(0, 8, 0, 8);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 3;

            // 후보 칸 원본 (비활성): 왼쪽 슬롯, 오른쪽 이름·성능
            var tile = Button("TileTemplate", content, "", false, 13);
            Object.DestroyImmediate(tile.transform.Find("Label").gameObject);
            ItemSlot("Slot", tile.transform, 68).Anchor(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, -34), new Vector2(76, 34));
            Text("Name", tile.transform, "", 14, Theme.Parchment).rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(84, -2), new Vector2(-8, -6));
            Text("Stats", tile.transform, "", 12, Theme.TextDim, false, FontStyles.Normal, TextAlignmentOptions.TopLeft)
                .rectTransform.Anchor(new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(84, 6), new Vector2(-8, -2));
            tile.gameObject.SetActive(false);
            var empty = Text("Empty", card.transform, "창고에 이 칸에 맞는 장비가 없습니다. 장비 시장이나 던전에서 구할 수 있습니다.", 15, Theme.TextDim, false, FontStyles.Normal, TextAlignmentOptions.Center);
            empty.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(24, -20), new Vector2(-24, 20));

            var msg = Text("Msg", card.transform, "", 14, new Color32(0xd9, 0xa2, 0x9b, 0xff));
            msg.rectTransform.Anchor(new Vector2(0, 0), new Vector2(1, 0), new Vector2(24, 62), new Vector2(-24, 88));
            var buttons = Rect("Buttons", card.transform).Anchor(new Vector2(0, 0), new Vector2(1, 0), new Vector2(24, 16), new Vector2(-24, 58));
            HRow(buttons, 10, align: TextAnchor.MiddleRight);
            var none = Button("None", buttons, "벗기").Layout(prefW: 140, prefH: 40);
            var close = Button("Close", buttons, "닫기").Layout(prefW: 110, prefH: 40);

            var picker = root.gameObject.AddComponent<ItemPicker>();
            var so = new SerializedObject(picker);
            void S(string p, Object v) => so.FindProperty(p).objectReferenceValue = v;
            S("overlay", overlay.gameObject); S("dim", dimBtn); S("title", title); S("current", current); S("tileTemplate", tile);
            S("empty", empty.gameObject); S("msg", msg); S("none", none); S("close", close);
            so.ApplyModifiedPropertiesWithoutUndo();
            overlay.gameObject.SetActive(false);
        }

        public static void BuildInfoPopup(Transform canvas)
        {
            var root = Rect("InfoPopup", canvas).Stretch();
            var overlay = Rect("Overlay", root).Stretch();
            var dim = Box("Dim", overlay, new Color(0, 0, 0, 0.55f));
            ((RectTransform)dim.transform).Stretch();
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;

            var card = Panel("Card", overlay);
            var crt = (RectTransform)card.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(760, 620);
            Box("Accent", card.transform, Theme.Bronze).rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, -4), new Vector2(-1, -1));

            var title = Text("Title", card.transform, "", 26, Theme.Parchment, true, FontStyles.Bold);
            title.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -58), new Vector2(-24, -18));
            var sub = Text("Sub", card.transform, "", 14, Theme.TextDim);
            sub.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -86), new Vector2(-24, -60));
            Box("Line", card.transform, Theme.Line).rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -95), new Vector2(-24, -94));
            // 장비 카드면 오른쪽 위에 큰 아이템 슬롯
            var icon = ItemSlot("Icon", card.transform, 116).GetComponent<Image>();
            icon.rectTransform.Anchor(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-140, -136), new Vector2(-24, -20));
            icon.gameObject.SetActive(false);

            var content = ScrollView("Scroll", card.transform, out var scrollRoot, 0);
            scrollRoot.Anchor(Vector2.zero, Vector2.one, new Vector2(24, 70), new Vector2(-16, -104));
            content.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 10, 0, 8);
            var body = Text("Body", content, "", 15, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            body.lineSpacing = 12;

            var buttons = Rect("Buttons", card.transform).Anchor(new Vector2(0, 0), new Vector2(1, 0), new Vector2(24, 16), new Vector2(-24, 58));
            HRow(buttons, 10);
            var back = Button("Back", buttons, "← 뒤로").Layout(prefW: 110, prefH: 40);
            Rect("Spacer", buttons).Layout(flexW: 1);
            var action = Button("Action", buttons, "이동", true).Layout(prefW: 170, prefH: 40);
            var close = Button("Close", buttons, "닫기").Layout(prefW: 110, prefH: 40);

            var popup = root.gameObject.AddComponent<InfoPopup>();
            var so = new SerializedObject(popup);
            void S(string p, Object v) => so.FindProperty(p).objectReferenceValue = v;
            S("overlay", overlay.gameObject); S("dim", dimBtn); S("title", title); S("sub", sub); S("body", body);
            S("scroll", scrollRoot.GetComponent<ScrollRect>()); S("icon", icon); S("backButton", back); S("actionButton", action); S("closeButton", close);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>예/아니오 확인 창 (ConfirmPopup): 가운데 작은 카드에 질문 한 줄, 설명, 아니오·예</summary>
        public static void BuildConfirmPopup(Transform canvas)
        {
            var root = Rect("ConfirmPopup", canvas).Stretch();
            var overlay = Rect("Overlay", root).Stretch();
            var dim = Box("Dim", overlay, new Color(0, 0, 0, 0.6f));
            ((RectTransform)dim.transform).Stretch();
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;

            var card = Panel("Card", overlay);
            var crt = (RectTransform)card.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(480, 220);
            Box("Accent", card.transform, Theme.Bronze).rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, -4), new Vector2(-1, -1));

            var title = Text("Title", card.transform, "", 24, Theme.Parchment, true, FontStyles.Bold, TextAlignmentOptions.Center);
            title.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -80), new Vector2(-24, -28));
            var body = Text("Body", card.transform, "", 15, Theme.TextDim, false, FontStyles.Normal, TextAlignmentOptions.Center);
            body.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -124), new Vector2(-24, -84));

            var buttons = Rect("Buttons", card.transform).Anchor(new Vector2(0, 0), new Vector2(1, 0), new Vector2(24, 22), new Vector2(-24, 66));
            HRow(buttons, 14, align: TextAnchor.MiddleCenter);
            var no = Button("No", buttons, "아니오").Layout(prefW: 150, prefH: 44);
            var yes = Button("Yes", buttons, "예", true).Layout(prefW: 150, prefH: 44);

            var popup = root.gameObject.AddComponent<ConfirmPopup>();
            var so = new SerializedObject(popup);
            void S(string p, Object v) => so.FindProperty(p).objectReferenceValue = v;
            S("overlay", overlay.gameObject); S("dim", dimBtn); S("title", title); S("body", body); S("yes", yes); S("no", no);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>협상 창 (NegotiationPopup): 값 판(이적 제안·역제안·이적 명단)과 계약 판(조항 열 줄)</summary>
        public static void BuildNegotiationPopup(Transform canvas)
        {
            var root = Rect("NegotiationPopup", canvas).Stretch();
            var overlay = Rect("Overlay", root).Stretch();
            var dim = Box("Dim", overlay, new Color(0, 0, 0, 0.62f));
            ((RectTransform)dim.transform).Stretch();
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;

            var card = Panel("Card", overlay);
            var crt = (RectTransform)card.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(820, 780);
            Box("Accent", card.transform, Theme.Bronze).rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, -4), new Vector2(-1, -1));

            var body = Rect("Body", card.transform).Stretch(28, 24, 28, 22);
            var vl = VCol(body, 8);
            vl.childForceExpandHeight = false;
            var title = Text("Title", body, "협상", 24, Theme.Parchment, true, FontStyles.Bold);
            title.raycastTarget = true;
            var sub = Text("Sub", body, "", 15, Theme.TextDim);
            sub.raycastTarget = true;
            Box("Line", body, Theme.Line).Layout(minH: 1, prefH: 1);
            var info = Text("Info", body, "", 15, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            info.raycastTarget = true;
            info.lineSpacing = 12;

            // 값 판
            var fee = Rect("FeeRoot", body);
            HRow(fee, 8).childForceExpandWidth = false;
            fee.Layout(minH: 42, prefH: 42, flexH: 0);
            var priceLabel = Text("PriceLabel", fee, "값", 15, Theme.BronzeHi, false, FontStyles.Bold);
            priceLabel.Layout(prefW: 120, flexW: 0);
            var price = Input(fee, 150, 38, 17);
            var minus10 = Button("Minus10", fee, "−10%", false, 14).Layout(prefW: 70, prefH: 36);
            var plus10 = Button("Plus10", fee, "+10%", false, 14).Layout(prefW: 70, prefH: 36);
            var quickA = Button("QuickA", fee, "희망가", false, 14).Layout(prefW: 110, prefH: 36);
            var quickB = Button("QuickB", fee, "몸값", false, 14).Layout(prefW: 110, prefH: 36);

            // 계약 판: 조항 한 줄씩
            var contract = Rect("ContractRoot", body);
            var cl = VCol(contract, 4);
            cl.childForceExpandHeight = false;
            var rowsData = new (string key, string label, bool toggle)[]
            {
                ("wage", "주급", false), ("seasons", "계약 기간", false), ("signing", "계약금", false), ("win", "승리 수당", false), ("kill", "킬 수당", false),
                ("promo", "승격 보너스", false), ("extend", "승격 시 1시즌 연장", true), ("raise", "승격 시 주급 인상", false), ("cut", "강등 시 주급 삭감", false), ("release", "최소 이적료 조항", false),
            };
            var rowObjs = new System.Collections.Generic.List<(string key, GameObject root, TMP_InputField input, Button minus, Button plus, Button toggle, TMP_Text hint)>();
            foreach (var (key, label, toggle) in rowsData)
            {
                var row = Box("Row_" + key, contract, new Color(1, 1, 1, 0.025f));
                row.Layout(minH: 38, prefH: 38, flexH: 0);
                HRow(row.transform, 8, 10, 10).childForceExpandWidth = false;
                Text("Label", row.transform, label, 15, Theme.Text).Layout(prefW: 190, flexW: 0);
                TMP_InputField input = null;
                Button minus = null, plus = null, tg = null;
                if (toggle) tg = Button("Toggle", row.transform, "□ 없음", false, 14).Layout(prefW: 130, prefH: 32);
                else
                {
                    minus = Button("Minus", row.transform, "−", false, 16).Layout(prefW: 34, prefH: 32);
                    input = Input(row.transform, 110, 32, 15);
                    plus = Button("Plus", row.transform, "+", false, 16).Layout(prefW: 34, prefH: 32);
                }
                var hint = Text("Hint", row.transform, "", 13, Theme.TextFaint);
                hint.Layout(flexW: 1);
                rowObjs.Add((key, row.gameObject, input, minus, plus, tg, hint));
            }

            var mood = Text("Mood", body, "", 17, Theme.Parchment);
            var note = Text("Note", body, "", 14, Theme.TextDim, false, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            note.raycastTarget = true;
            var msg = Text("Msg", body, "", 14, new Color32(0xd9, 0xa2, 0x9b, 0xff));
            Rect("Spacer", body).Layout(flexH: 1);

            var buttons = Rect("Buttons", body);
            buttons.Layout(minH: 48, prefH: 48, flexH: 0);
            HRow(buttons, 10, align: TextAnchor.MiddleRight).childForceExpandWidth = false;
            var reject = Button("Reject", buttons, "거절", false, 15).Layout(prefW: 170, prefH: 44);
            var secondary = Button("Secondary", buttons, "역제안", false, 15).Layout(prefW: 210, prefH: 44);
            var primary = Button("Primary", buttons, "수락", true, 15).Layout(prefW: 210, prefH: 44);
            var close = Button("Close", buttons, "닫기", false, 15).Layout(prefW: 110, prefH: 44);

            var popup = root.gameObject.AddComponent<NegotiationPopup>();
            var so = new SerializedObject(popup);
            void S(string p, Object v) => so.FindProperty(p).objectReferenceValue = v;
            S("overlay", overlay.gameObject); S("dim", dimBtn); S("title", title); S("sub", sub); S("info", info); S("mood", mood); S("note", note); S("msg", msg);
            S("feeRoot", fee.gameObject); S("priceLabel", priceLabel); S("price", price); S("minus10", minus10); S("plus10", plus10); S("quickA", quickA); S("quickB", quickB);
            S("contractRoot", contract.gameObject);
            S("primary", primary); S("secondary", secondary); S("reject", reject); S("close", close);
            var rows = so.FindProperty("rows");
            rows.arraySize = rowObjs.Count;
            for (int i = 0; i < rowObjs.Count; i++)
            {
                var el = rows.GetArrayElementAtIndex(i);
                var r = rowObjs[i];
                el.FindPropertyRelative("key").stringValue = r.key;
                el.FindPropertyRelative("root").objectReferenceValue = r.root;
                el.FindPropertyRelative("input").objectReferenceValue = r.input;
                el.FindPropertyRelative("minus").objectReferenceValue = r.minus;
                el.FindPropertyRelative("plus").objectReferenceValue = r.plus;
                el.FindPropertyRelative("toggle").objectReferenceValue = r.toggle;
                el.FindPropertyRelative("hint").objectReferenceValue = r.hint;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static TMP_InputField Input(Transform parent, float w, float h, float size)
        {
            var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.name = "Input";
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Theme.Bg2;
            var input = go.GetComponent<TMP_InputField>();
            foreach (var t in go.GetComponentsInChildren<TextMeshProUGUI>(true)) { t.font = Sans; t.fontSize = size; t.color = Theme.Parchment; }
            input.placeholder.GetComponent<TMP_Text>().text = "";
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.Layout(minW: w, prefW: w, minH: h, prefH: h, flexW: 0);
            return input;
        }

        public static void BuildTipLayer(Transform canvas)
        {
            var root = Rect("TipLayer", canvas).Stretch();
            var tip = Panel("Tip", root, new Color32(0x1a, 0x14, 0x10, 0xf5));
            tip.raycastTarget = false;
            var trt = (RectTransform)tip.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0, 1);
            trt.pivot = new Vector2(0, 1);
            var vl = VCol(tip.transform, 0, 12, 12, 9, 10);
            tip.transform.Find("Fill").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var fit = tip.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var text = Text("Text", tip.transform, "", 14, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            text.Layout(prefW: 340);
            vl.childForceExpandWidth = false;
            var layer = root.gameObject.AddComponent<TipLayer>();
            Set(layer, "tip", trt);
            Set(layer, "tipText", text);
        }
    }
}
