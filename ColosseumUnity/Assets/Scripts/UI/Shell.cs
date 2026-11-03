// 앱 골격: 상단 바, 경고줄, 왼쪽 메뉴, 화면 전환 (웹판 App.tsx Game). 오브젝트는 SceneBuilder가 만든다.
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    /// <summary>화면 하나. 메뉴 id와 같은 이름의 screenId를 갖고, 보일 때마다 Refresh로 다시 그린다</summary>
    public abstract class ScreenBase : MonoBehaviour
    {
        public string screenId;
        protected GameController G => GameController.I;
        protected GameState S => GameController.I.State;
        public abstract void Refresh();
    }

    public class Shell : MonoBehaviour
    {
        public static readonly (string id, string label)[] Menu =
        {
            ("inbox", "서판"), ("overview", "안뜰"), ("roster", "막사"), ("strategy", "전략"), ("training", "연무장"), ("challenge", "도전장"),
            ("match", "모래 마당"), ("league", "서열표"), ("cup", "판테온 컵"), ("market", "이적 시장"), ("dungeon", "던전"),
            ("youth", "유소년 학당"), ("staff", "식솔"), ("facilities", "증축"), ("finance", "금고"), ("world", "대륙 정세"), ("hall", "비석의 언덕"),
        };

        [Header("상단 바")]
        [SerializeField] Image crest;
        [SerializeField] TMP_Text clubName;
        [SerializeField] TMP_Text clubSub;
        [SerializeField] TMP_Text dateText;
        [SerializeField] TMP_Text paydayText;
        [SerializeField] TMP_Text treasuryText;
        [SerializeField] TMP_Text wagesText;
        [SerializeField] Button skipButton;
        [SerializeField] Button nextButton;
        [SerializeField] TMP_Text nextLabel;
        [Header("경고줄")]
        [SerializeField] GameObject alertBar;
        [SerializeField] TMP_Text alertText;
        [SerializeField] Button alertButton;
        [Header("메뉴")]
        [SerializeField] List<MenuButton> menuButtons = new();
        [SerializeField] Button restartButton;
        [Header("화면")]
        [SerializeField] List<ScreenBase> screens = new();
        [SerializeField] GameObject gameOverPanel;
        [SerializeField] TMP_Text gameOverText;
        [Header("새 게임")]
        [SerializeField] GameObject gameRoot;
        [SerializeField] GameObject newGameRoot;
        [Tooltip("로비(타이틀) 화면")]
        [SerializeField] GameObject lobbyRoot;

        /// <summary>이 프레임에 팝업이 ESC로 닫혔으면 그 번호 (같은 ESC로 로비까지 가지 않게)</summary>
        public static int EscUsedFrame = -1;

        /// <summary>ESC = 저장하고 로비로. 팝업은 각자 Update에서 먼저 닫히므로 LateUpdate에서 본다</summary>
        void LateUpdate()
        {
            if (!Input.GetKeyDown(KeyCode.Escape) || EscUsedFrame == Time.frameCount) return;
            if (ConfirmPopup.I != null && ConfirmPopup.I.IsOpen) return;
            if (!gameRoot.activeInHierarchy || GameController.I.State == null) return;
            var sel = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            if (sel != null && sel.GetComponentInParent<TMP_Dropdown>()?.IsExpanded == true) return;
            AskToLobby();
        }

        /// <summary>메인 메뉴로 가기 전에 묻는다 (ESC·사이드바 버튼 공통)</summary>
        static void AskToLobby() =>
            ConfirmPopup.Ask("메인 메뉴로 이동하시겠습니까?", "지금까지 진행한 내용은 저장됩니다.", () => GameController.I.ToLobby());

        /// <summary>스페이스 키 = 다음 날로 버튼. 글 입력·드롭다운 펼침 중에는 막는다</summary>
        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Space)) return;
            if (ConfirmPopup.I != null && ConfirmPopup.I.IsOpen) return;
            if (!gameRoot.activeInHierarchy || !nextButton.gameObject.activeInHierarchy || !nextButton.interactable) return;
            var sel = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            if (sel != null && (sel.GetComponent<TMP_InputField>()?.isFocused == true || sel.GetComponentInParent<TMP_Dropdown>()?.IsExpanded == true)) return;
            nextButton.onClick.Invoke();
        }

        void Awake()
        {
            var g = GetComponentInParent<GameController>() ?? FindFirstObjectByType<GameController>();
            g.Changed += Refresh;
            g.NeedNewGame += ShowNewGame;
            g.NeedLobby += ShowLobby;
            skipButton.onClick.AddListener(() => GameController.I.SkipAhead());
            nextButton.onClick.AddListener(() => GameController.I.NextDay());
            alertButton.onClick.AddListener(() => { GameController.I.GoToBlocker(); GameController.I.Go(GameController.I.Screen); });
            // 사이드바 맨 아래: 저장하고 로비로
            restartButton.onClick.AddListener(AskToLobby);
            foreach (var m in menuButtons)
            {
                var id = m.id;
                m.button.onClick.AddListener(() => GameController.I.Go(id));
            }
        }

        void ShowNewGame()
        {
            gameRoot.SetActive(false);
            if (lobbyRoot != null) lobbyRoot.SetActive(false);
            newGameRoot.SetActive(true);
        }

        void ShowLobby()
        {
            gameRoot.SetActive(false);
            newGameRoot.SetActive(false);
            lobbyRoot.SetActive(true);
        }

        void Refresh()
        {
            var G = GameController.I;
            var s = G.State;
            if (s == null) return;
            gameRoot.SetActive(true);
            newGameRoot.SetActive(false);
            if (lobbyRoot != null) lobbyRoot.SetActive(false);
            var p = G.Player;
            var nation = Data.Nation(s.nation);
            var tierLudi = s.ludi.Values.Where(l => l.tier == p.tier && l.nation == s.nation).OrderByDescending(l => l.rp).ToList();
            int rank = tierLudi.FindIndex(l => l.id == p.id) + 1;
            crest.sprite = Resources.Load<Sprite>($"Art/crest/{s.nation}");
            clubName.text = p.name;
            clubSub.text = $"{nation.league} {p.tier}부 · {rank}위 · RP {p.rp}";
            dateText.text = $"시즌 {s.season} · {Calendar.DateLabel(s.day)}";
            paydayText.text = $"급료일까지 {Calendar.DaysToPayday(s.day)}일";
            treasuryText.text = $"{p.treasury:N0} D";
            treasuryText.color = p.treasury < 0 ? Theme.Blood : Theme.Parchment;
            wagesText.text = $"주급 합계 {Economy.WeeklyWages(s, p):N0} D";

            var blocked = Day.Blockers(s);
            skipButton.interactable = blocked.Count == 0;
            nextButton.interactable = s.gameOver == null;
            nextLabel.text = blocked.Count > 0 ? "처리할 일이 있습니다" : "다음 날로 ▶";

            var alerts = G.Alerts();
            alertBar.SetActive(alerts.Count > 0);
            alertText.text = string.Join(" · ", alerts);

            int unread = s.inbox.Count(m => !m.read);
            int incoming = s.challenges.Count(c => c.status == "pending" && c.to == p.id);
            bool matchToday = ChallengeLogic.TodaysPlayerMatches(s).Count > 0;
            foreach (var m in menuButtons)
            {
                bool active = G.Screen == m.id || (G.Screen == "gladiator" && m.id == G.ReturnScreen);
                m.bg.color = active ? Theme.Panel2 : new Color(0, 0, 0, 0);
                m.accent.enabled = active;
                m.label.color = active ? Theme.Parchment : Theme.Text;
                m.label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
                string badge = m.id == "inbox" && unread > 0 ? unread.ToString()
                    : m.id == "challenge" && incoming > 0 ? incoming.ToString()
                    : m.id == "match" && matchToday ? "!" : null;
                m.badge.SetActive(badge != null);
                if (badge != null) m.badgeText.text = badge;
            }

            gameOverPanel.SetActive(s.gameOver != null);
            if (s.gameOver != null) gameOverText.text = s.gameOver;
            foreach (var sc in screens)
            {
                bool show = sc.screenId == G.Screen;
                sc.gameObject.SetActive(show);
                if (show) sc.Refresh();
            }
        }
    }
}
