// 정보 카드 (FM 하이퍼링크처럼): 나라·루두스·대회·장비·종족·스타일 링크를 누르면 뜬다 (GameController.ShowInfo).
// 카드 안의 링크를 누르면 같은 카드에서 넘어가고, '뒤로'로 돌아온다. 검투사 링크는 검투사 팝업 카드가 받는다.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class InfoPopup : MonoBehaviour
    {
        [Tooltip("어둡게 덮는 바탕 + 카드. 이 컴포넌트가 붙은 뿌리는 늘 켜 두고 이것만 켜고 끈다")]
        [SerializeField] GameObject overlay;
        [SerializeField] Button dim;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text sub;
        [SerializeField] TMP_Text body;
        [SerializeField] ScrollRect scroll;
        [Tooltip("장비 카드일 때 큰 아이콘")]
        [SerializeField] Image icon;
        [SerializeField] Button backButton;
        [SerializeField] Button actionButton;
        [SerializeField] Button closeButton;

        readonly List<string> stack = new();
        InfoCard card;

        void Awake()
        {
            var G = GameController.I ?? FindFirstObjectByType<GameController>();
            G.InfoRequested += Open;
            G.Changed += () => { if (overlay.activeSelf) Draw(false); };
            overlay.SetActive(false);
            dim.onClick.AddListener(Close);
            closeButton.onClick.AddListener(Close);
            backButton.onClick.AddListener(() => { if (stack.Count > 1) { stack.RemoveAt(stack.Count - 1); Draw(true); } });
            actionButton.onClick.AddListener(() => { var a = card?.action; Close(); a?.Invoke(); });
        }

        void Update()
        {
            if (overlay.activeSelf && Input.GetKeyDown(KeyCode.Escape)) { Shell.EscUsedFrame = Time.frameCount; Close(); }
        }

        void Open(string link)
        {
            if (!overlay.activeSelf) stack.Clear();
            if (stack.Count == 0 || stack[stack.Count - 1] != link) stack.Add(link);
            overlay.SetActive(true);
            transform.SetAsLastSibling();
            // 툴팁은 늘 맨 위 (검투사 팝업은 열릴 때 스스로 맨 위로 온다)
            var tips = transform.parent.GetComponentInChildren<TipLayer>(true);
            if (tips != null) tips.transform.SetAsLastSibling();
            Draw(true);
        }

        void Close()
        {
            overlay.SetActive(false);
            stack.Clear();
        }

        void Draw(bool toTop)
        {
            var s = GameController.I.State;
            card = s != null && stack.Count > 0 ? InfoCards.For(s, stack[stack.Count - 1]) : null;
            if (card == null) { Close(); return; }
            title.text = card.title;
            sub.text = card.sub ?? "";
            body.text = card.body ?? "";
            icon.gameObject.SetActive(!string.IsNullOrEmpty(card.iconItem) || card.iconSprite != null);
            Widgets.SetSlot(icon.transform, card.iconItem);
            if (card.iconSprite != null)
            {
                // 기술 카드: 슬롯 틀에 기술 그림 (등급 테두리 없음)
                var img = icon.transform.Find("Icon").GetComponent<Image>();
                img.sprite = card.iconSprite;
                img.enabled = true;
                img.color = Color.white;
            }
            backButton.gameObject.SetActive(stack.Count > 1);
            actionButton.gameObject.SetActive(card.action != null);
            if (card.action != null) actionButton.GetComponentInChildren<TMP_Text>().text = card.actionLabel;
            if (toTop)
            {
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = 1;
            }
        }
    }
}
