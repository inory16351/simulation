// 증축 화면 조립 (웹판 FacilitiesScreen.tsx + styles.css .facility-grid .fac-levels .fac-effect).
// 시설은 고정 일곱 곳이라 빌더가 바로 만든다 (3열). 드롭다운이 없어 화면 전체를 스크롤한다.
using System.Collections.Generic;
using Colosseum.Core;
using Colosseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class FacilitiesBuilder
    {
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);

        public static ScreenBase Build(Transform main)
        {
            var rt = Rect("Screen_facilities", main).Stretch(26, 20, 26, 20);
            var content = ScrollView("Scroll", rt, out var scroll, 14);
            scroll.Stretch();
            ScreenHeader(content, "증축", "");
            var sub = content.Find("Sub").GetComponent<TMP_Text>();
            sub.Layout(prefH: -1);

            var s = rt.gameObject.AddComponent<FacilitiesScreen>();
            s.screenId = "facilities";
            Set(s, "sub", sub);

            // 공사 중 줄
            var bp = Panel("Building", content);
            VCol(bp.transform, 0, 13, 13, 10, 10);
            bp.transform.Find("Fill").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Set(s, "buildingText", Text("Text", bp.transform, "", 15, Theme.Torch));
            Set(s, "buildingRoot", bp.gameObject);
            Set(s, "msgText", Text("Msg", content, "", 14, Neg));

            // 시설 (3열, 웹판 .facility-grid). 모자란 칸은 빈 자리로 채워 폭을 맞춘다
            var defs = FacilitiesLogic.FacilitiesConst;
            var pads = new List<Object>();
            for (int i = 0; i < defs.Count; i += 3)
            {
                var line = Rect("Line" + i / 3, content);
                var h = HRow(line, 12, 0, 0, 0, 0, TextAnchor.UpperLeft);
                h.childForceExpandWidth = true;
                h.childForceExpandHeight = true;
                for (int c = 0; c < 3; c++)
                {
                    if (i + c < defs.Count) pads.Add(FacilityPanel(line, defs[i + c]));
                    else Rect("Spacer", line).Layout(minW: 0, prefW: 0, flexW: 1);
                }
            }
            var so = new UnityEditor.SerializedObject(s);
            var p = so.FindProperty("facilityPads");
            p.arraySize = pads.Count;
            for (int i = 0; i < pads.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = pads[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            rt.gameObject.SetActive(false);
            return s;
        }

        /// <summary>시설 패널 하나 (웹판 .panel.facility). 반환값은 내용 칸</summary>
        static RectTransform FacilityPanel(Transform line, FacilityDef def)
        {
            var pad = TitledPanel("Fac_" + def.id, line, def.name, 6);
            pad.parent.Layout(minW: 0, prefW: 0, flexW: 1);
            // 단계 칸 (웹판 .fac-levels i)
            var lv = Rect("Levels", pad);
            HRow(lv, 4);
            for (int k = 0; k < FacilitiesLogic.MaxLevel; k++)
            {
                var pip = Box("Pip" + k, lv, Theme.Line).Layout(minW: 18, prefW: 18, minH: 8, prefH: 8, flexW: 0);
                var fill = Box("Fill", pip.transform, Theme.Panel2);
                fill.rectTransform.Stretch(1, 1, 1, 1);
                fill.raycastTarget = false;
                pip.raycastTarget = false;
            }
            Rect("Gap", lv).Layout(minW: 2, prefW: 2, flexW: 0);
            Text("Level", lv, "0단계", 14, Theme.Parchment, false, FontStyles.Bold);
            Text("Desc", pad, def.desc, 13, Theme.TextDim);
            Text("Now", pad, "", 14, Theme.Parchment);
            Text("Next", pad, "", 14, Theme.TextDim);
            var br = Rect("Buttons", pad);
            HRow(br, 8);
            Button("Upgrade", br, "짓기", true, 14).Layout(minW: 240, prefW: 240, prefH: 32, flexW: 0);
            Text("Max", br, "최고 단계", 13, Theme.Text);
            return pad;
        }
    }
}
