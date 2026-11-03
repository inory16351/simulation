// 예/아니오 확인 창: 실수로 누르면 안 되는 일(메인 메뉴로 가기 등) 앞에 묻는다. ESC = 아니오, Enter = 예.
// 하이어라키: ConfirmPopup > Overlay > (Dim, Card > Title, Body, Buttons > No, Yes)
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class ConfirmPopup : MonoBehaviour
    {
        public static ConfirmPopup I { get; private set; }

        [SerializeField] GameObject overlay;
        [SerializeField] Button dim;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text body;
        [SerializeField] Button yes;
        [SerializeField] Button no;

        Action onYes;

        public bool IsOpen => overlay.activeSelf;

        void Awake()
        {
            I = this;
            dim.onClick.AddListener(Close);
            no.onClick.AddListener(Close);
            yes.onClick.AddListener(() => { var a = onYes; Close(); a?.Invoke(); });
            overlay.SetActive(false);
        }

        void Update()
        {
            if (!overlay.activeSelf) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Shell.EscUsedFrame = Time.frameCount; Close(); }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) yes.onClick.Invoke();
        }

        /// <summary>묻고, 예를 누르면 yesAction. 확인 창이 씬에 없으면 바로 한다</summary>
        public static void Ask(string question, string detail, Action yesAction)
        {
            if (I == null) { yesAction?.Invoke(); return; }
            I.onYes = yesAction;
            I.title.text = question;
            I.body.text = detail ?? "";
            I.body.gameObject.SetActive(!string.IsNullOrEmpty(detail));
            I.overlay.SetActive(true);
            I.transform.SetAsLastSibling();
        }

        public void Close()
        {
            onYes = null;
            overlay.SetActive(false);
        }
    }
}
