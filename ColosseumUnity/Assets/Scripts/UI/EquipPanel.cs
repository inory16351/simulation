// 검투사 장비 칸 (웹판 components/GearPanels.tsx의 EquipPanel): 창고에서 맞는 것을 골라 끼운다.
// 슬롯이나 '바꾸기' 버튼을 누르면 장비 고르기 팝업(ItemPicker)이 뜬다.
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class EquipPanel : MonoBehaviour
    {
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);

        [SerializeField] TMP_Text title;
        [Tooltip("칸 여섯 (이름 = Equipment.Slots: main/off/helmet/armor/arms/legs). 자식: Slot(버튼), Info/Item·Stats·Change")]
        [SerializeField] List<RectTransform> slots = new();
        [SerializeField] TMP_Text msg;

        static GameController G => GameController.I;
        static GameState S => GameController.I.State;

        string gid, msgText;
        bool editable;

        void Awake()
        {
            foreach (var box in slots)
            {
                string slot = box.name;
                box.Find("Info/Change").GetComponent<Button>().onClick.AddListener(() => Pick(slot));
                var slotBtn = box.Find("Slot").GetComponent<Button>();
                if (slotBtn != null) slotBtn.onClick.AddListener(() => Pick(slot));
            }
        }

        /// <summary>editable: 우리 검투사이고 살아 있을 때</summary>
        public void Show(Gladiator g, bool canEdit)
        {
            if (gid != g.id) { gid = g.id; msgText = null; }
            editable = canEdit;
            Draw();
        }

        bool CanChange(Gladiator g) => editable && g.deployment == null;

        List<string> Options(Gladiator g, string slot) =>
            GearLogic.ArmoryOf(S).Distinct().Where(id => GearLogic.CannotEquip(g, slot, id) == null).ToList();

        /// <summary>장비 고르기 팝업을 연다. 고르면 끼우고, 오류면 팝업에 그대로 보여 준다</summary>
        void Pick(string slot)
        {
            if (gid == null || !S.gladiators.TryGetValue(gid, out var g) || !CanChange(g) || ItemPicker.I == null) return;
            string id = gid, cur = SlotItem(g.equipment, slot);
            ItemPicker.I.Open($"{GearLogic.SlotNames[slot]} 바꾸기 — {g.name}", cur, Options(g, slot),
                string.IsNullOrEmpty(cur) ? null : slot == "main" || slot == "armor" ? "기본 장비로" : "벗기",
                item =>
                {
                    var err = G.Try(s => GearLogic.EquipItem(s, id, slot, item));
                    if (err == null) { msgText = null; Draw(); }
                    return err;
                });
        }

        static string SlotItem(Equipment e, string slot) => e.Get(slot);

        void Draw()
        {
            if (gid == null || !S.gladiators.TryGetValue(gid, out var g)) return;
            string faint = Theme.Hex(Theme.TextFaint);
            bool canChange = CanChange(g);
            title.text = "◆  장비" + (g.deployment != null ? " (파견 중)" : "");

            foreach (var box in slots)
            {
                string slot = box.name;
                string cur = SlotItem(g.equipment, slot);
                bool has = !string.IsNullOrEmpty(cur);
                T(box, "Info/Item").text = has ? Tips.Item(cur) : $"<color={faint}>비었습니다</color>";
                Widgets.SetSlot(box.Find("Slot"), cur, slot);
                var stats = T(box, "Info/Stats");
                stats.gameObject.SetActive(has);
                stats.text = has ? GearLogic.ItemStats(cur) : "";
                var change = box.Find("Info/Change").GetComponent<Button>();
                change.gameObject.SetActive(canChange);
                if (!canChange) continue;
                int n = Options(g, slot).Count;
                change.GetComponentInChildren<TMP_Text>().text = n > 0 ? $"바꾸기 ({n})" : "창고에 맞는 장비 없음";
                change.interactable = n > 0 || has;
            }

            msg.gameObject.SetActive(msgText != null);
            msg.text = msgText ?? "";
            msg.color = Neg;
        }

        static TMP_Text T(Transform row, string path) => row.Find(path).GetComponent<TMP_Text>();

    }
}
