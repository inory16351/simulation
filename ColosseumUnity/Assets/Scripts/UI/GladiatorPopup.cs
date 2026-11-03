// 검투사 팝업 카드: 어느 화면에서든 검투사를 누르면 뜬다 (GameController.ShowGladiator).
// 큰 초상화, 신상, 현재·잠재(남의 검투사는 정찰한 만큼), 전적, 특성, 장착 장비 네 칸. '자세히'는 상세 화면으로.
using System;
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class GladiatorPopup : MonoBehaviour
    {
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);

        [Tooltip("어둡게 덮는 바탕 + 카드. 이 컴포넌트가 붙은 뿌리는 늘 켜 두고 이것만 켜고 끈다")]
        [SerializeField] GameObject overlay;
        [SerializeField] Button dim;
        [SerializeField] PortraitView portrait;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text subText;
        [SerializeField] TMP_Text info;
        [SerializeField] TMP_Text traits;
        [Tooltip("칸 여섯 (이름 = Equipment.Slots). 자식: Item, Stats")]
        [SerializeField] List<RectTransform> slots = new();
        [SerializeField] Button detailButton;
        [SerializeField] Button closeButton;

        string gid;

        void Awake()
        {
            var G = GameController.I ?? FindFirstObjectByType<GameController>();
            G.PopupRequested += Open;
            G.Changed += () => { if (overlay.activeSelf) Draw(); };
            overlay.SetActive(false);
            dim.onClick.AddListener(Close);
            closeButton.onClick.AddListener(Close);
            detailButton.onClick.AddListener(() => { string id = gid; Close(); GameController.I.OpenGladiator(id); });
        }

        void Update()
        {
            if (overlay.activeSelf && Input.GetKeyDown(KeyCode.Escape)) { Shell.EscUsedFrame = Time.frameCount; Close(); }
        }

        void Open(string id)
        {
            gid = id;
            overlay.SetActive(true);
            transform.SetAsLastSibling();
            // 툴팁은 늘 맨 위에
            var tips = transform.parent.GetComponentInChildren<TipLayer>(true);
            if (tips != null) tips.transform.SetAsLastSibling();
            Draw();
        }

        void Close() => overlay.SetActive(false);

        void Draw()
        {
            var s = GameController.I.State;
            if (s == null || gid == null || !s.gladiators.TryGetValue(gid, out var g)) { Close(); return; }
            var owner = s.ludi.Values.FirstOrDefault(l => l.gladiators.Contains(g.id));
            bool isMine = owner?.isPlayer ?? s.fallen.Contains(g.id);
            string dim = Theme.Hex(Theme.TextDim), faint = Theme.Hex(Theme.TextFaint), torch = Theme.Hex(Theme.Torch);

            portrait.Show(g);
            nameText.text = g.name + (string.IsNullOrEmpty(g.nickname) ? "" : $" <size=65%><color={dim}>\"{g.nickname}\"</color></size>");
            subText.text = $"{Tips.Race(g.race)} · {Tips.Style(g.style)} · {g.age}세 <color={faint}>({GladiatorRules.AgeStageNames[GladiatorRules.AgeStage(g)]})</color> · {Tips.Nation(g.nation, true)} 출신";

            int ca = GladiatorRules.ComputeCA(g);
            string caText = isMine ? $"<color={torch}>{Widgets.Stars(ca)}</color> <color={faint}>(CA {ca})</color>"
                : RangeStars(Scouting.CaRange(s, g));
            var pr = Scouting.PaRange(s, g);
            string state = !g.alive ? $"<color={Theme.Hex(Neg)}>사망 — {g.diedAt?.cause}</color>"
                : g.injury != null ? $"<color=#f0b3aa>{GladiatorTable.InjuryLabel(g)}</color>" : $"<color={Theme.Hex(Theme.Good)}>출전 가능</color>";
            info.text =
                $"<color={dim}>소속</color>  {(owner != null ? (owner.isPlayer ? $"<b>{Tips.Ludus(owner)}</b>" : Tips.Ludus(owner)) : "무소속")}\n" +
                $"<color={dim}>현재</color>  {caText}    <color={dim}>잠재</color>  {RangeStars(pr)}\n" +
                $"<color={dim}>전적</color>  {g.record.wins}승 {g.record.losses}패 · 살해 {g.record.kills}    <color={dim}>인기</color>  {g.popularity}\n" +
                $"<color={dim}>상태</color>  {state}    <color={dim}>주급</color>  {g.wage} D";

            var race = Data.Race(g.race);
            var tags = new List<string> { Tips.Racial(g.race, Theme.Hex(Theme.Parchment)) };
            foreach (var t in g.traits)
            {
                var def = Data.Trait(t);
                if (def == null) continue;
                if (def.hidden && !isMine) tags.Add($"<color={faint}>?</color>");
                else tags.Add(Tips.Trait(def.id, Theme.Hex(def.negative ? Neg : def.hidden ? Theme.TextFaint : Theme.Text)));
            }
            traits.text = string.Join("   ", tags);

            var style = Data.Style(g.style);
            foreach (var box in slots)
            {
                string slot = box.name;
                string cur = SlotItem(g.equipment, slot);
                string def = style?.loadout != null ? SlotItem(new Equipment { main = style.loadout.main, armor = style.loadout.armor }, slot) : null;
                var item = box.Find("Item").GetComponent<TMP_Text>();
                var stats = box.Find("Stats").GetComponent<TMP_Text>();
                Widgets.SetSlot(box.Find("Slot"), !string.IsNullOrEmpty(cur) ? cur : def, slot);
                if (!string.IsNullOrEmpty(cur)) { item.text = Tips.Item(cur); stats.text = GearLogic.ItemStats(cur); }
                else if (!string.IsNullOrEmpty(def)) { item.text = $"<color={faint}>기본</color> {Tips.Item(def)}"; stats.text = GearLogic.ItemStats(def); }
                else { item.text = $"<color={faint}>비었습니다</color>"; stats.text = ""; }
            }
        }

        static string SlotItem(Equipment e, string slot) => e.Get(slot);

        static string RangeStars(ScoutRange r)
        {
            string a = Widgets.Stars(r.lo), b = Widgets.Stars(r.hi);
            return $"<color={Theme.Hex(Theme.Torch)}>{a}</color>" + (b != a ? $"<color={Theme.Hex(Theme.TextFaint)}>~{b}</color>" : "");
        }
    }
}
