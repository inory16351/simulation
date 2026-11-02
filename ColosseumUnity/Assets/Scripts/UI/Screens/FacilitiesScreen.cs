// 증축 (웹판 FacilitiesScreen.tsx): 시설 일곱 곳의 단계·효과와 증축 버튼. 한 번에 한 곳만 공사한다.
using System.Collections.Generic;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class FacilitiesScreen : ScreenBase
    {
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);

        [SerializeField] TMP_Text sub;
        [SerializeField] GameObject buildingRoot;
        [SerializeField] TMP_Text buildingText;
        [SerializeField] TMP_Text msgText;
        [Tooltip("시설 패널 내용 칸 (FacilitiesLogic.FacilitiesConst 순서)")]
        [SerializeField] List<RectTransform> facilityPads = new();

        string msg;

        public override void Refresh()
        {
            var me = S.ludi[S.playerLudusId];
            sub.text = $"공사는 한 번에 한 곳만 할 수 있습니다. 시설마다 급료일에 유지비가 나가며, 지금은 매주 {StateUtil.Num(FacilitiesLogic.WeeklyUpkeep(me))} D가 나갑니다.";
            string building = FacilitiesLogic.ConstructionLabel(S, me);
            buildingRoot.SetActive(building != null);
            buildingText.text = building != null ? "■ " + building : "";
            msgText.gameObject.SetActive(msg != null);
            msgText.text = msg ?? "";
            msgText.color = Neg;

            var defs = FacilitiesLogic.FacilitiesConst;
            for (int i = 0; i < facilityPads.Count && i < defs.Count; i++) Bind(facilityPads[i], defs[i], me);
        }

        void Bind(RectTransform pad, FacilityDef f, Ludus me)
        {
            int lvl = FacilitiesLogic.Level(me.facilities, f.id);
            bool hasNext = lvl < FacilitiesLogic.MaxLevel;
            var (cost, days) = hasNext ? FacilitiesLogic.UpgradeCost(f.id, lvl + 1) : (0.0, 0);
            bool busy = me.construction != null;
            bool inProgress = me.construction?.facility == f.id;

            // 공사 중인 시설은 테두리를 횃불색으로 (웹판 .facility.building)
            pad.parent.GetComponent<Image>().color = inProgress ? Theme.Torch : Theme.Line;
            for (int k = 0; k < FacilitiesLogic.MaxLevel; k++)
            {
                var pip = pad.Find("Levels/Pip" + k);
                if (pip == null) continue;
                bool on = k < lvl;
                pip.GetComponent<Image>().color = on ? Theme.Bronze : Theme.Line;
                pip.Find("Fill").GetComponent<Image>().color = on ? Theme.Torch : Theme.Panel2;
            }
            T(pad, "Levels/Level").text = $"{lvl}단계";
            T(pad, "Now").text = "지금: " + (lvl > 0 ? f.effect(lvl) : "아직 없습니다");
            var next = T(pad, "Next");
            next.gameObject.SetActive(hasNext);
            next.text = hasNext ? "다음: " + f.effect(lvl + 1) : "";

            var btn = pad.Find("Buttons/Upgrade").GetComponent<Button>();
            btn.gameObject.SetActive(hasNext);
            pad.Find("Buttons/Max").gameObject.SetActive(!hasNext);
            if (!hasNext) return;
            btn.interactable = !busy && me.treasury >= cost;
            btn.GetComponentInChildren<TMP_Text>().text = inProgress ? "공사 중" : $"{(lvl > 0 ? "증축" : "건설")} · {cost:N0} D · {days}일";
            string id = f.id;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                msg = G.Try(d => FacilitiesLogic.StartUpgrade(d, d.ludi[d.playerLudusId], id));
                Refresh();
            });
        }

        static TMP_Text T(Transform row, string path) => row.Find(path).GetComponent<TMP_Text>();
    }
}
