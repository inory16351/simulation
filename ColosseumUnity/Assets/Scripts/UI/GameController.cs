// 게임 상태를 쥐고 화면들에 알린다 (웹판 App.tsx의 Game: update / act / nextDay / skipAhead / 저장).
using System;
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using UnityEngine;

namespace Colosseum.UI
{
    public class GameController : MonoBehaviour
    {
        public static GameController I { get; private set; }

        public GameState State { get; private set; }
        public string Screen { get; private set; } = "inbox";
        public string SelectedGladiator { get; private set; }

        /// <summary>상태나 화면이 바뀌면 부른다</summary>
        public event Action Changed;
        /// <summary>새 게임 화면을 띄워야 할 때 (저장이 없거나 '새 루두스 세우기')</summary>
        public event Action NeedNewGame;

        /// <summary>새 게임 화면을 띄워야 할 때 말고, 로비(타이틀)를 띄워야 할 때</summary>
        public event Action NeedLobby;

        /// <summary>저장 칸 (1~Slots). 칸마다 persistentDataPath/slotN 폴더</summary>
        public const int Slots = 3;
        public int Slot { get; private set; } = 1;
        const string LastSlotKey = "colosseum.lastSlot";
        /// <summary>마지막으로 쓴 칸 (없으면 0)</summary>
        public static int LastSlot => PlayerPrefs.GetInt(LastSlotKey, 0);

        static string SlotDir(int n)
        {
            string d = System.IO.Path.Combine(Application.persistentDataPath, "slot" + n);
            System.IO.Directory.CreateDirectory(d);
            return d;
        }
        string SaveDir => SlotDir(Slot);

        /// <summary>칸에 든 게임 (로비 카드용). 없으면 null</summary>
        public static GameState Peek(int n) => SaveSystem.Load(SlotDir(n));

        /// <summary>칸이 하나뿐이던 시절의 저장을 1번 칸으로 옮긴다</summary>
        static void MigrateOldSave()
        {
            try
            {
                string old = System.IO.Path.Combine(Application.persistentDataPath, SaveSystem.FileName);
                string to = System.IO.Path.Combine(SlotDir(1), SaveSystem.FileName);
                if (System.IO.File.Exists(old) && !System.IO.File.Exists(to)) { System.IO.File.Move(old, to); PlayerPrefs.SetInt(LastSlotKey, 1); }
            }
            catch (Exception) { /* 옮기지 못해도 새로 시작할 수 있다 */ }
        }

        void Awake()
        {
            I = this;
            Data.Ensure();
        }

        void Start()
        {
            // 켜면 로비부터 (이어하기·새 게임·저장 칸·설정)
            MigrateOldSave();
            NeedLobby?.Invoke();
        }

        /// <summary>칸의 게임을 불러온다. 비었으면 그 칸으로 새 게임 화면</summary>
        public void LoadSlot(int n)
        {
            Slot = n;
            State = SaveSystem.Load(SaveDir);
            if (State == null) { NeedNewGame?.Invoke(); return; }
            PlayerPrefs.SetInt(LastSlotKey, n);
            Screen = "inbox";
            SelectedGladiator = null;
            Changed?.Invoke();
        }

        /// <summary>이 칸에 새 루두스를 세운다 (나라 고르기 화면으로)</summary>
        public void BeginNewGame(int n)
        {
            Slot = n;
            NeedNewGame?.Invoke();
        }

        public static void DeleteSlot(int n)
        {
            SaveSystem.Clear(SlotDir(n));
            if (LastSlot == n) PlayerPrefs.DeleteKey(LastSlotKey);
        }

        /// <summary>저장하고 로비로</summary>
        public void ToLobby()
        {
            if (State != null) SaveSystem.Save(SaveDir, State);
            State = null;
            NeedLobby?.Invoke();
        }

        public void NewGame(string nation, string ludusName, int startTier = 2)
        {
            PlayerPrefs.SetInt(LastSlotKey, Slot);
            SaveSystem.Clear(SaveDir);
            State = World.NewGame((double)(DateTime.Now.Ticks % 2147483648L), nation, ludusName, startTier);
            Screen = "inbox";
            Commit();
        }

        public void RequestNewGame() => NeedNewGame?.Invoke();

        /// <summary>상태를 고친다</summary>
        public void Mutate(Action<GameState> fn)
        {
            fn(State);
            Commit();
        }

        /// <summary>게임 RNG를 이어서 쓰며 상태를 고친다</summary>
        public void Act(Action<GameState, Rng> fn)
        {
            var rng = new Rng(State.rngState);
            fn(State, rng);
            State.rngState = rng.State;
            Commit();
        }

        /// <summary>복사본에서 먼저 돌려 보고, 오류가 없을 때만 진짜 상태에 반영한다 (웹판 structuredClone 검증). 오류 글을 돌려준다</summary>
        public string Try(Func<GameState, string> fn)
        {
            var err = fn(SaveSystem.FromJson(SaveSystem.ToJson(State)));
            if (err == null) Mutate(s => fn(s));
            return err;
        }

        /// <summary>미리 계산해 둔 상태로 통째로 바꾼다 (공식 경기 중계가 끝난 뒤)</summary>
        public void Replace(GameState next)
        {
            State = next;
            Commit();
        }

        void Commit()
        {
            SaveSystem.Save(SaveDir, State);
            Changed?.Invoke();
        }

        public void Go(string screen)
        {
            Screen = screen;
            Changed?.Invoke();
        }

        public void OpenGladiator(string id)
        {
            SelectedGladiator = id;
            if (Screen != "gladiator") ReturnScreen = Screen;
            Go("gladiator");
        }

        /// <summary>검투사 상세를 열기 전 화면 (상세의 뒤로 버튼이 돌아간다)</summary>
        public string ReturnScreen { get; private set; } = "roster";

        public void Back() => Go(ReturnScreen);

        /// <summary>팝업 카드를 띄워 달라는 요청 (GladiatorPopup이 받는다)</summary>
        public event Action<string> PopupRequested;

        /// <summary>어느 화면에서든 검투사를 누르면 부른다: 팝업 카드</summary>
        public void ShowGladiator(string id)
        {
            if (id != null && State != null && State.gladiators.ContainsKey(id)) PopupRequested?.Invoke(id);
        }

        /// <summary>정보 카드를 띄워 달라는 요청 (InfoPopup이 받는다)</summary>
        public event Action<string> InfoRequested;

        /// <summary>나라·루두스·대회·장비·종족·스타일 링크를 누르면 부른다 (link = "nation:aurelia" 같은 TMP 링크 id)</summary>
        public void ShowInfo(string link)
        {
            if (State != null && InfoCards.For(State, link) != null) InfoRequested?.Invoke(link);
        }

        /// <summary>비석의 언덕 연혁 탭에서 이 루두스를 열어 달라는 요청 (HallScreen이 읽고 지운다)</summary>
        public string HallFocus { get; set; }

        public void OpenHall(string ludusId)
        {
            HallFocus = ludusId;
            Go("hall");
        }

        /// <summary>막힌 일이 있으면 그 화면으로 안내한다. 받은 이적 제안·협상 역제안이면 협상 창을 연다</summary>
        public void GoToBlocker()
        {
            if (ChallengeLogic.TodaysPlayerMatches(State).Count > 0) Screen = "match";
            else if (CupLogic.TodaysPlayerTie(State) != null) Screen = "cup";
            else if (NatCupLogic.TodaysPlayerTie(State) != null) Screen = "natcup";
            else if (OlympicaLogic.TodaysPlayerTie(State) != null || OlympicaLogic.Current(State)?.offer == true) Screen = "olympica";
            else if (State.pendingEvents.Count > 0) Screen = "inbox";
            else if (Transfers.OpenOffers(State).Count > 0 || (State.decisions?.Count ?? 0) > 0)
            {
                Screen = "inbox";
                NegotiationPopup.OpenFirstBlocker();
            }
        }

        /// <summary>서판에서 열어 줄 소식 (InboxScreen이 읽고 지운다)</summary>
        public string InboxFocus { get; set; }

        /// <summary>읽지 않은 소식 중 가장 오래된 것 (FM처럼 차례로 읽는다)</summary>
        public InboxMessage NextUnread() => State.inbox.LastOrDefault(m => !m.read);

        /// <summary>읽지 않은 소식이 있으면 서판에서 그것부터 연다. 열었으면 true</summary>
        bool ReadNextUnread()
        {
            var m = NextUnread();
            if (m == null) return false;
            m.read = true;
            InboxFocus = m.id;
            Screen = "inbox";
            Commit();
            return true;
        }

        /// <summary>다음 날로 (스페이스 키도 같다). 읽지 않은 소식이 있으면 그것부터 차례로 연다</summary>
        public void NextDay()
        {
            if (ReadNextUnread()) return;
            if (Day.Blockers(State).Count > 0) { GoToBlocker(); Changed?.Invoke(); return; }
            var b = Day.AdvanceDay(State);
            if (b.Count > 0) GoToBlocker();
            Commit();
        }

        /// <summary>결정할 일이 생기거나 새 소식이 올 때까지 여러 날을 넘긴다</summary>
        public void SkipAhead()
        {
            if (ReadNextUnread()) return;
            int startInbox = State.inbox.Count;
            for (int i = 0; i < 28; i++)
            {
                if (Day.AdvanceDay(State).Count > 0) break;
                if (Day.Blockers(State).Count > 0 || State.inbox.Count != startInbox) break;
            }
            GoToBlocker();
            Commit();
        }

        public Ludus Player => State.ludi[State.playerLudusId];

        /// <summary>상단 경고줄: 막힌 일 + 급료 미납 + 금고 바닥</summary>
        public List<string> Alerts()
        {
            var p = Player;
            var list = new List<string>(Day.Blockers(State));
            int unpaid = p.gladiators.Count(id => State.gladiators[id].unpaidWeeks > 0);
            if (unpaid > 0) list.Add($"급료가 밀린 검투사 {unpaid}명");
            if (p.treasury < 0) list.Add("금고가 바닥났습니다. 28일 안에 적자를 메우지 못하면 루두스를 빼앗깁니다");
            return list;
        }
    }
}
