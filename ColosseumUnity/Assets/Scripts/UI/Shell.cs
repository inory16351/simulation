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
            ("inbox", "서판"), ("overview", "안뜰"), ("roster", "막사"), ("training", "연무장"), ("challenge", "도전장"),
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

        void Awake()
        {
            var g = GetComponentInParent<GameController>() ?? FindFirstObjectByType<GameController>();
            g.Changed += Refresh;
            g.NeedNewGame += ShowNewGame;
            skipButton.onClick.AddListener(() => GameController.I.SkipAhead());
            nextButton.onClick.AddListener(() => GameController.I.NextDay());
            alertButton.onClick.AddListener(() => { GameController.I.GoToBlocker(); GameController.I.Go(GameController.I.Screen); });
            restartButton.onClick.AddListener(() => GameController.I.RequestNewGame());
            foreach (var m in menuButtons)
            {
                var id = m.id;
                m.button.onClick.AddListener(() => GameController.I.Go(id));
            }
        }

        void ShowNewGame()
        {
            gameRoot.SetActive(false);
            newGameRoot.SetActive(true);
        }

        void Refresh()
        {
            var G = GameController.I;
            var s = G.State;
            if (s == null) return;
            gameRoot.SetActive(true);
            newGameRoot.SetActive(false);
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
            nextLabel.text = blocked.Count > 0 ? "처리할 일이 있다" : "다음 날로 ▶";

            var alerts = G.Alerts();
            alertBar.SetActive(alerts.Count > 0);
            alertText.text = string.Join(" · ", alerts);

            int unread = s.inbox.Count(m => !m.read);
            int incoming = s.challenges.Count(c => c.status == "pending" && c.to == p.id);
            bool matchToday = ChallengeLogic.TodaysPlayerMatches(s).Count > 0;
            foreach (var m in menuButtons)
            {
                bool active = G.Screen == m.id || (G.Screen == "gladiator" && m.id == "roster");
                m.bg.color = active ? Theme.Panel2 : new Color(0, 0, 0, 0);
                m.accent.enabled = active;
                m.label.color = active ? Theme.Parchment : m.id == "hall" ? Theme.TextFaint : Theme.Text;
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
