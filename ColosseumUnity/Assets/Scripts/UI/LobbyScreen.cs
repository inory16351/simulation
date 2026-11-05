// 아웃게임 로비 (타이틀): 아레나 도미누스. 이어하기 · 새 게임 · 불러오기(저장 칸 3개) · 설정 · 종료.
// 오른쪽 패널은 저장 칸(새 게임/불러오기 모드)이나 설정 중 하나만 보인다.
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class LobbyScreen : MonoBehaviour
    {
        [Header("메뉴")]
        [SerializeField] Button continueButton;
        [Tooltip("이어하기 아래 작은 글 (루두스·날짜)")]
        [SerializeField] TMP_Text continueSub;
        [SerializeField] Button newButton;
        [SerializeField] Button loadButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button quitButton;

        [Header("저장 칸")]
        [SerializeField] GameObject slotsPanel;
        [SerializeField] TMP_Text slotsTitle;
        [Tooltip("칸 카드 셋: Info(TMP), Buttons/Action, Buttons/Delete")]
        [SerializeField] List<RectTransform> slotCards = new();

        [Header("설정")]
        [SerializeField] GameObject settingsPanel;
        [SerializeField] Slider musicSlider;
        [SerializeField] Slider sfxSlider;
        [SerializeField] Button muteButton;
        [SerializeField] Button fullscreenButton;
        [Tooltip("해상도 버튼 (이름 = 1280x720 식)")]
        [SerializeField] List<Button> resolutionButtons = new();

        static GameController G => GameController.I;

        /// <summary>none | new | load | settings</summary>
        string mode = "none";
        /// <summary>지우기·덮어쓰기는 한 번 더 눌러야 한다 (칸 번호)</summary>
        int confirmDelete, confirmOverwrite;

        void Awake()
        {
            // 이어하기 = 불러올 칸 고르기 (마지막 칸 표시)
            continueButton.onClick.AddListener(() => Toggle("load"));
            newButton.onClick.AddListener(() => Toggle("new"));
            loadButton.onClick.AddListener(() => Toggle("load"));
            settingsButton.onClick.AddListener(() => Toggle("settings"));
            quitButton.onClick.AddListener(Quit);
            for (int i = 0; i < slotCards.Count; i++)
            {
                int n = i + 1;
                slotCards[i].Find("Buttons/Action").GetComponent<Button>().onClick.AddListener(() => SlotAction(n));
                slotCards[i].Find("Buttons/Delete").GetComponent<Button>().onClick.AddListener(() => SlotDelete(n));
            }
            musicSlider.SetValueWithoutNotify(GameAudio.MusicVolume);
            sfxSlider.SetValueWithoutNotify(GameAudio.SfxVolume);
            musicSlider.onValueChanged.AddListener(v => GameAudio.MusicVolume = v);
            sfxSlider.onValueChanged.AddListener(v => { GameAudio.SfxVolume = v; GameAudio.Sfx("hit", 0.6f); });
            muteButton.onClick.AddListener(() => { GameAudio.Muted = !GameAudio.Muted; Refresh(); });
            fullscreenButton.onClick.AddListener(() =>
            {
                Screen.fullScreenMode = Screen.fullScreenMode == FullScreenMode.Windowed ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                Refresh();
            });
            foreach (var b in resolutionButtons)
            {
                var p = b.name.Split('x');
                int w = int.Parse(p[0]), h = int.Parse(p[1]);
                b.onClick.AddListener(() => { Screen.SetResolution(w, h, Screen.fullScreenMode); Refresh(); });
            }
        }

        void OnEnable()
        {
            // 로비가 GameController.Awake보다 먼저 켜질 수 있다 (저장 요약에 나라 이름이 필요)
            Data.Ensure();
            mode = "none";
            confirmDelete = confirmOverwrite = 0;
            Refresh();
        }

        /// <summary>ESC = 열린 칸·설정 패널 닫기</summary>
        void Update()
        {
            if (mode != "none" && Input.GetKeyDown(KeyCode.Escape)) Toggle("none");
        }

        void Toggle(string m)
        {
            mode = mode == m ? "none" : m;
            confirmDelete = confirmOverwrite = 0;
            Refresh();
        }

        void SlotAction(int n)
        {
            var s = GameController.Peek(n);
            if (mode == "load") { if (s != null) G.LoadSlot(n); return; }
            // 새 게임: 찬 칸이면 한 번 더 눌러야 덮어쓴다
            if (s != null && confirmOverwrite != n) { confirmOverwrite = n; Refresh(); return; }
            G.BeginNewGame(n);
        }

        void SlotDelete(int n)
        {
            if (confirmDelete != n) { confirmDelete = n; Refresh(); return; }
            GameController.DeleteSlot(n);
            confirmDelete = 0;
            Refresh();
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>칸 한 줄 요약: 루두스 · 나라 리그 · 날짜 · 금고 · 인원</summary>
        static string Summary(GameState s, bool old = false)
        {
            if (s == null && old) return $"<color=#d9a29b>호환되지 않는 옛 저장입니다</color> <color={Theme.Hex(Theme.TextFaint)}>· 세계가 바뀌어 불러올 수 없습니다. 새 게임을 시작해야 합니다</color>";
            if (s == null) return $"<color={Theme.Hex(Theme.TextFaint)}>비어 있습니다</color>";
            var p = s.ludi[s.playerLudusId];
            var nation = Data.Nation(s.nation);
            return $"<b>{p.name}</b>  <color={Theme.Hex(Theme.TextDim)}>{nation?.@short} · {nation?.league} {p.tier}부</color>\n" +
                   $"시즌 {s.season} · {Calendar.DateLabel(s.day)} · 금고 {p.treasury:N0} D · 검투사 {p.gladiators.Count}명";
        }

        void Refresh()
        {
            var games = Enumerable.Range(1, GameController.Slots).Select(GameController.Peek).ToList();
            int last = GameController.LastSlot;
            var lastGame = last > 0 && last <= games.Count ? games[last - 1] : null;
            continueButton.interactable = lastGame != null;
            continueSub.text = lastGame != null ? $"{lastGame.ludi[lastGame.playerLudusId].name} · 시즌 {lastGame.season} {Calendar.DateLabel(lastGame.day)}" : "저장된 게임이 없습니다";
            loadButton.interactable = games.Any(g => g != null);
            Seg(newButton, mode == "new");
            Seg(loadButton, mode == "load");
            Seg(settingsButton, mode == "settings");

            slotsPanel.SetActive(mode is "new" or "load");
            settingsPanel.SetActive(mode == "settings");
            slotsTitle.text = mode == "new" ? "◆  새 루두스를 세울 칸" : "◆  불러올 칸";
            for (int i = 0; i < slotCards.Count; i++)
            {
                int n = i + 1;
                var s = games[i];
                var card = slotCards[i];
                bool old = s == null && GameController.Incompatible(n);
                card.Find("Info").GetComponent<TMP_Text>().text = $"<color={Theme.Hex(Theme.BronzeHi)}>{n}번 칸</color>{(n == last ? $"  <color={Theme.Hex(Theme.TextFaint)}>(마지막)</color>" : "")}\n{Summary(s, old)}";
                var action = card.Find("Buttons/Action").GetComponent<Button>();
                // 불러오기 모드에서 빈 칸은 버튼을 숨긴다
                action.gameObject.SetActive(mode == "new" || s != null);
                action.GetComponentInChildren<TMP_Text>().text = mode == "load" ? "불러오기" : s == null ? "여기에 세우기" : confirmOverwrite == n ? "정말 덮어쓰기" : "덮어쓰기";
                var del = card.Find("Buttons/Delete").GetComponent<Button>();
                del.gameObject.SetActive(s != null || old);
                del.GetComponentInChildren<TMP_Text>().text = confirmDelete == n ? "정말 지우기" : "지우기";
            }

            muteButton.GetComponentInChildren<TMP_Text>().text = GameAudio.Muted ? "소리 켜기" : "소리 끄기";
            fullscreenButton.GetComponentInChildren<TMP_Text>().text = Screen.fullScreenMode == FullScreenMode.Windowed ? "전체 화면으로" : "창 모드로";
            foreach (var b in resolutionButtons) Seg(b, b.name == $"{Screen.width}x{Screen.height}");
        }

        static void Seg(Button b, bool on)
        {
            b.transform.Find("Fill").GetComponent<Image>().color = on ? Theme.ContinueBg : Theme.Panel2;
            b.GetComponent<Image>().color = on ? Theme.Torch : Theme.Line;
        }
    }
}
