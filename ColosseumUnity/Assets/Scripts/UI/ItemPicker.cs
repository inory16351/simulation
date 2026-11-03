// 장비 고르기 팝업: 슬롯을 누르면 창고에서 그 칸에 맞는 장비를 아이템 슬롯 격자로 보여 주고, 누르면 끼운다.
// 하이어라키: ItemPicker > Overlay > (Dim, Card > Title, Current, Scroll/Grid > TileTemplate(비활성), Empty, Msg, Buttons > None, Close)
using System;
using System.Collections.Generic;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class ItemPicker : MonoBehaviour
    {
        public static ItemPicker I { get; private set; }

        [SerializeField] GameObject overlay;
        [SerializeField] Button dim;
        [SerializeField] TMP_Text title;
        [Tooltip("지금 낀 장비 한 줄")]
        [SerializeField] TMP_Text current;
        [Tooltip("자식: Slot(UiKit.ItemSlot), Name, Stats")]
        [SerializeField] Button tileTemplate;
        [SerializeField] GameObject empty;
        [SerializeField] TMP_Text msg;
        [Tooltip("벗기 / 기본 장비로")]
        [SerializeField] Button none;
        [SerializeField] Button close;

        TemplateList<Button> tiles;
        Func<string, string> onPick;
        Action reopen;

        void Awake()
        {
            I = this;
            tiles = new TemplateList<Button>(tileTemplate);
            dim.onClick.AddListener(Close);
            close.onClick.AddListener(Close);
            overlay.SetActive(false);
        }

        /// <summary>
        /// 후보 목록을 띄운다. pick(id 또는 null=벗기)은 오류 글을 돌려주면 팝업을 닫지 않고 보여 준다.
        /// noneLabel이 null이면 벗기 버튼을 숨긴다.
        /// </summary>
        public void Open(string heading, string cur, IList<string> options, string noneLabel, Func<string, string> pick)
        {
            onPick = pick;
            title.text = heading;
            string faint = Theme.Hex(Theme.TextFaint);
            current.text = string.IsNullOrEmpty(cur) ? $"<color={faint}>지금: 비었습니다</color>" : $"<color={faint}>지금:</color> {Widgets.ItemLabel(cur)}  <color={faint}>— {GearLogic.ItemStats(cur)}</color>";
            empty.SetActive(options.Count == 0);
            tiles.Sync(options.Count, (b, j) =>
            {
                string id = options[j];
                Widgets.SetSlot(b.transform.Find("Slot"), id);
                b.transform.Find("Name").GetComponent<TMP_Text>().text = $"{Widgets.ItemLabel(id)}\n<size=80%>{Widgets.GradeTag(id)}</size>";
                b.transform.Find("Stats").GetComponent<TMP_Text>().text = GearLogic.ItemStats(id);
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => Choose(id));
            });
            none.gameObject.SetActive(noneLabel != null);
            if (noneLabel != null) none.GetComponentInChildren<TMP_Text>().text = noneLabel;
            none.onClick.RemoveAllListeners();
            none.onClick.AddListener(() => Choose(null));
            msg.text = "";
            overlay.SetActive(true);
            overlay.transform.SetAsLastSibling();
        }

        void Choose(string id)
        {
            string err = onPick?.Invoke(id);
            if (err != null) { msg.text = err; return; }
            Close();
        }

        public void Close() => overlay.SetActive(false);

        void Update()
        {
            if (overlay.activeSelf && Input.GetKeyDown(KeyCode.Escape)) { Shell.EscUsedFrame = Time.frameCount; Close(); }
        }
    }
}
