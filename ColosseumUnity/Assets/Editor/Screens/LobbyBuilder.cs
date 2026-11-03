// 로비(타이틀) 조립: 배경 그림 위 왼쪽에 제목 '아레나 도미누스'와 메뉴, 오른쪽에 저장 칸 패널 / 설정 패널 (LobbyScreen).
using System.Collections.Generic;
using Colosseum.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class LobbyBuilder
    {
        public static GameObject Build(Transform canvas)
        {
            var root = Rect("LobbyRoot", canvas).Stretch();
            CoverBackground(root, "ui/bg_main", new Color32(12, 9, 7, 110), new Color32(12, 9, 7, 235), 0.45f, true);
            // 비네트: 가장자리를 어둡게 눌러 로고·메뉴로 시선을 모은다 (Tools/ui-fx.mjs)
            var vignette = Sprite("Vignette", root, "ui/vignette", 0, 0);
            vignette.preserveAspect = false;
            vignette.gameObject.GetComponent<LayoutElement>().ignoreLayout = true;
            vignette.rectTransform.Stretch();

            // ── 왼쪽: 제목과 메뉴 ──
            var left = Rect("Left", root).Anchor(new Vector2(0, 0), new Vector2(0, 1), new Vector2(110, 70), new Vector2(720, -60));
            // 버튼이 칸 폭까지 늘어나지 않게 (폭은 각자 prefW)
            VCol(left, 10).childForceExpandWidth = false;
            // 로고 (영어 ARENA DOMINUS 글자형, Codex 그림: art/source/ui/logo_a.png → ui/logo.png) + 뒤에 번진 그림자
            var logoHolder = Rect("LogoHolder", left).Layout(minW: 560, prefW: 560, minH: 272, prefH: 272, flexW: 0);
            // 그림자 그림은 로고보다 사방 60px(1200 기준) 크다 → 560 폭에서 28. 아래로 조금 내려 깐다
            var logoShadow = Sprite("LogoShadow", logoHolder, "ui/logo_shadow", 0, 0);
            logoShadow.gameObject.GetComponent<LayoutElement>().ignoreLayout = true;
            logoShadow.rectTransform.Stretch(-28, -18, -28, -42);
            logoShadow.color = new Color(1, 1, 1, 0.95f);
            var logo = Sprite("Logo", logoHolder, "ui/logo", 0, 0);
            logo.gameObject.GetComponent<LayoutElement>().ignoreLayout = true;
            logo.rectTransform.Stretch();
            Text("Tagline", left, "모래 위에 루두스를 세우고, 검투사를 길러 판테온의 정점에 오르십시오.", 18, Theme.TextDim).Layout(prefW: 600, minH: 60);

            var cont = MenuButton(left, "Continue", "이어하기", true, 22, 74);
            var contLabel = cont.transform.Find("Label").GetComponent<TMP_Text>();
            ((RectTransform)contLabel.transform).Stretch(16, 6, 16, 26);
            var contSub = Text("Sub", cont.transform, "", 13, Theme.ContinueText, false, FontStyles.Normal, TextAlignmentOptions.Center);
            ((RectTransform)contSub.transform).Anchor(new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 8), new Vector2(-16, 30));
            var newB = MenuButton(left, "New", "새 게임", false, 19, 54);
            var loadB = MenuButton(left, "Load", "불러오기", false, 19, 54);
            var setB = MenuButton(left, "Settings", "설정", false, 19, 54);
            var quitB = MenuButton(left, "Quit", "종료", false, 19, 54);

            var ver = Text("Version", root, "아레나 도미누스 · 개발판", 13, Theme.TextFaint);
            ver.rectTransform.Anchor(Vector2.zero, Vector2.zero, new Vector2(110, 30), new Vector2(600, 56));

            // ── 오른쪽: 저장 칸 ──
            var slotsHolder = ShadowHolder(root, "SlotsHolder", new Vector2(-860, -260), new Vector2(-110, 260));
            var slotsPad = TitledPanel("Slots", slotsHolder, "저장 칸", 10);
            var slotsFrame = ((RectTransform)slotsPad.parent).Stretch();
            var slotsTitle = slotsFrame.Find("Head/Text").GetComponent<TMP_Text>();
            var cards = new List<RectTransform>();
            for (int i = 1; i <= GameController.Slots; i++)
            {
                var card = Panel("Slot" + i, slotsPad, Theme.Panel2).Layout(minH: 120, prefH: 120);
                HRow(card.transform, 14, 18, 14, 10, 10);
                card.transform.Find("Fill").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                Text("Info", card.transform, "", 16, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.MidlineLeft).Layout(minW: 0, prefW: 0, flexW: 1);
                var btns = Rect("Buttons", card.transform).Layout(minW: 170, prefW: 170, flexW: 0);
                VCol(btns, 6, 0, 0, 14, 0);
                Button("Action", btns, "불러오기", true, 15).Layout(minH: 40, prefH: 40);
                Button("Delete", btns, "지우기", false, 13).Layout(minH: 30, prefH: 30);
                cards.Add(card.rectTransform);
            }
            slotsHolder.gameObject.SetActive(false);

            // ── 오른쪽: 설정 ──
            var setHolder = ShadowHolder(root, "SettingsHolder", new Vector2(-860, -230), new Vector2(-110, 230));
            var setPad = TitledPanel("SettingsPanel", setHolder, "설정", 14);
            var setFrame = ((RectTransform)setPad.parent).Stretch();
            Sublabel(setPad, "소리");
            var music = SliderRow(setPad, "음악");
            var sfx = SliderRow(setPad, "효과음");
            var muteRow = Rect("MuteRow", setPad);
            HRow(muteRow, 8);
            var mute = Button("Mute", muteRow, "소리 끄기", false, 14).Layout(prefW: 160, prefH: 34);
            Sublabel(setPad, "화면");
            var scrRow = Rect("ScreenRow", setPad);
            HRow(scrRow, 8);
            var full = Button("Fullscreen", scrRow, "전체 화면으로", false, 14).Layout(prefW: 160, prefH: 34);
            var res = new List<Button>();
            foreach (var r in new[] { "1280x720", "1600x900", "1920x1080" }) res.Add(Button(r, scrRow, r.Replace("x", " × "), false, 14).Layout(prefW: 130, prefH: 34));
            setHolder.gameObject.SetActive(false);

            var s = root.gameObject.AddComponent<LobbyScreen>();
            var so = new SerializedObject(s);
            void S(string p, Object v) => so.FindProperty(p).objectReferenceValue = v;
            S("continueButton", cont); S("continueSub", contSub); S("newButton", newB); S("loadButton", loadB); S("settingsButton", setB); S("quitButton", quitB);
            S("slotsPanel", slotsHolder.gameObject); S("slotsTitle", slotsTitle);
            S("settingsPanel", setHolder.gameObject); S("musicSlider", music); S("sfxSlider", sfx); S("muteButton", mute); S("fullscreenButton", full);
            var cp = so.FindProperty("slotCards");
            cp.arraySize = cards.Count;
            for (int i = 0; i < cards.Count; i++) cp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            var rp = so.FindProperty("resolutionButtons");
            rp.arraySize = res.Count;
            for (int i = 0; i < res.Count; i++) rp.GetArrayElementAtIndex(i).objectReferenceValue = res[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        /// <summary>메뉴 버튼: 뒤에 번진 그림자를 깐 묶음 (버튼은 묶음 안에 꽉 차게)</summary>
        static Button MenuButton(Transform parent, string name, string label, bool primary, float size, float h)
        {
            var holder = Rect(name + "Holder", parent).Layout(minW: 380, prefW: 380, minH: h, prefH: h, flexW: 0);
            SoftShadow(holder, 16, 22, 10);
            var b = Button(name, holder, label, primary, size);
            ((RectTransform)b.transform).Stretch();
            return b;
        }

        /// <summary>오른쪽 패널 묶음: 앵커 자리 + 뒤에 그림자</summary>
        static RectTransform ShadowHolder(Transform parent, string name, Vector2 offMin, Vector2 offMax)
        {
            var holder = Rect(name, parent).Anchor(new Vector2(1, 0.5f), new Vector2(1, 0.5f), offMin, offMax);
            SoftShadow(holder, 30, 40, 16);
            return holder;
        }

        /// <summary>번진 그림자 (Tools/ui-fx.mjs soft_shadow, 9-slice): 옆으로 spread, 아래로 drop만큼 더 크게</summary>
        static void SoftShadow(Transform holder, float spread, float drop, float up)
        {
            var sh = Sprite("Shadow", holder, "ui/soft_shadow", 0, 0);
            sh.type = Image.Type.Sliced;
            sh.preserveAspect = false;
            sh.gameObject.GetComponent<LayoutElement>().ignoreLayout = true;
            sh.rectTransform.Stretch(-spread, -up, -spread, -drop);
            sh.color = new Color(1, 1, 1, 0.9f);
        }

        static void Sublabel(Transform parent, string label) => Text("Label", parent, label, 13, Theme.BronzeHi, false, FontStyles.Bold);

        /// <summary>이름 + 슬라이더 한 줄 (TMP 기본 슬라이더를 테마 색으로)</summary>
        static Slider SliderRow(Transform parent, string label)
        {
            var row = Rect(label, parent).Layout(minH: 30, prefH: 30);
            HRow(row, 12);
            Text("Name", row, label, 15).Layout(minW: 70, prefW: 70, flexW: 0);
            var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.name = "Slider";
            go.transform.SetParent(row, false);
            go.GetComponent<RectTransform>().Layout(minW: 320, prefW: 320, minH: 20, prefH: 20, flexW: 0);
            go.transform.Find("Background").GetComponent<Image>().color = Theme.Bg2;
            go.transform.Find("Fill Area/Fill").GetComponent<Image>().color = Theme.Torch;
            go.transform.Find("Handle Slide Area/Handle").GetComponent<Image>().color = Theme.Parchment;
            return go.GetComponent<Slider>();
        }
    }
}
