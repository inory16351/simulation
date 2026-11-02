// 검투사 장비 칸 (웹판 components/GearPanels.tsx의 EquipPanel): 창고에서 맞는 것을 골라 끼운다.
// 스크롤 영역 안이라 select 대신 칸의 '바꾸기' 버튼으로 아래에 후보 목록(Picker)을 펼친다.
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
        [Tooltip("칸 넷 (이름 = main/off/armor/helmet). 자식: Item, Stats, Change")]
        [SerializeField] List<RectTransform> slots = new();
        [Header("후보 목록")]
        [SerializeField] GameObject picker;
        [SerializeField] TMP_Text pickerTitle;
        [SerializeField] GameObject pickerEmpty;
        [SerializeField] Button pickTemplate;
        [Tooltip("벗기 / 기본 장비로")]
        [SerializeField] Button pickNone;
        [SerializeField] Button pickClose;
        [SerializeField] TMP_Text msg;

        static GameController G => GameController.I;
        static GameState S => GameController.I.State;

        TemplateList<Button> pickRows;
        string gid, pickSlot, msgText;
        bool editable;

        void Awake()
        {
            pickRows = new TemplateList<Button>(pickTemplate);
            foreach (var box in slots)
            {
                string slot = box.name;
                box.Find("Change").GetComponent<Button>().onClick.AddListener(() =>
                {
                    pickSlot = pickSlot == slot ? null : slot;
                    msgText = null;
                    Draw();
                });
            }
            pickNone.onClick.AddListener(() => Equip(null));
            pickClose.onClick.AddListener(() => { pickSlot = null; Draw(); });
        }

        /// <summary>editable: 우리 검투사이고 살아 있을 때</summary>
        public void Show(Gladiator g, bool canEdit)
        {
            if (gid != g.id) { gid = g.id; pickSlot = null; msgText = null; }
            editable = canEdit;
            Draw();
        }

        void Equip(string item)
        {
            string id = gid, slot = pickSlot;
            if (slot == null) return;
            pickSlot = null;
            msgText = G.Try(s => GearLogic.EquipItem(s, id, slot, item));
            if (msgText != null) pickSlot = slot;
            Draw();
        }

        static string SlotItem(Equipment e, string slot) => slot switch
        {
            "main" => e.main, "off" => e.off, "armor" => e.armor, "helmet" => e.helmet, _ => null,
        };

        void Draw()
        {
            if (gid == null || !S.gladiators.TryGetValue(gid, out var g)) return;
            string faint = Theme.Hex(Theme.TextFaint);
            bool canChange = editable && g.deployment == null;
            if (!canChange) pickSlot = null;
            title.text = "◆  장비" + (g.deployment != null ? " (파견 중)" : "");

            var armory = GearLogic.ArmoryOf(S).Distinct().ToList();
            List<string> Options(string slot) => armory.Where(id => GearLogic.CannotEquip(g, slot, id) == null).ToList();

            foreach (var box in slots)
            {
                string slot = box.name;
                string cur = SlotItem(g.equipment, slot);
                bool has = !string.IsNullOrEmpty(cur);
                T(box, "Item").text = has ? Widgets.ItemLabel(cur) : $"<color={faint}>비었다</color>";
                var stats = T(box, "Stats");
                stats.gameObject.SetActive(has);
                stats.text = has ? GearLogic.ItemStats(cur) : "";
                var change = box.Find("Change").GetComponent<Button>();
                change.gameObject.SetActive(canChange);
                if (!canChange) continue;
                int n = Options(slot).Count;
                change.GetComponentInChildren<TMP_Text>().text = n > 0 ? $"바꾸기 ({n})" : "창고에 맞는 장비 없음";
                change.interactable = n > 0 || has;
                Seg(change, pickSlot == slot);
            }

            picker.SetActive(pickSlot != null);
            if (pickSlot != null)
            {
                string slot = pickSlot;
                string cur = SlotItem(g.equipment, slot);
                pickerTitle.text = $"{GearLogic.SlotNames[slot]} 바꾸기";
                var opts = Options(slot);
                pickerEmpty.SetActive(opts.Count == 0);
                pickRows.Sync(opts.Count, (b, j) =>
                {
                    string item = opts[j];
                    b.GetComponentInChildren<TMP_Text>().text = $"{Widgets.GradeTag(item)}   {Widgets.ItemLabel(item)}  <color={faint}>— {GearLogic.ItemStats(item)}</color>";
                    b.onClick.RemoveAllListeners();
                    b.onClick.AddListener(() => Equip(item));
                });
                pickNone.gameObject.SetActive(!string.IsNullOrEmpty(cur));
                pickNone.GetComponentInChildren<TMP_Text>().text = slot == "main" || slot == "armor" ? "기본 장비로" : "벗기";
            }

            msg.gameObject.SetActive(msgText != null);
            msg.text = msgText ?? "";
            msg.color = Neg;
        }

        static TMP_Text T(Transform row, string path) => row.Find(path).GetComponent<TMP_Text>();

        static void Seg(Button b, bool on)
        {
            b.transform.Find("Fill").GetComponent<Image>().color = on ? Theme.ContinueBg : Theme.Panel2;
            b.GetComponent<Image>().color = on ? Theme.Torch : Theme.Line;
        }
    }
}
