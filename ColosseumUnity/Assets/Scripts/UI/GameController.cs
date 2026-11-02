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

        string SaveDir => Application.persistentDataPath;

        void Awake()
        {
            I = this;
            Data.Ensure();
        }

        void Start()
        {
            State = SaveSystem.Load(SaveDir);
            if (State == null) NeedNewGame?.Invoke();
            else Changed?.Invoke();
        }

        public void NewGame(string nation, string ludusName)
        {
            SaveSystem.Clear(SaveDir);
            State = World.NewGame((double)(DateTime.Now.Ticks % 2147483648L), nation, ludusName);
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
            Go("gladiator");
        }

        /// <summary>막힌 일이 있으면 그 화면으로 안내한다</summary>
        public void GoToBlocker()
        {
            if (ChallengeLogic.TodaysPlayerMatches(State).Count > 0) Screen = "match";
            else if (CupLogic.TodaysPlayerTie(State) != null) Screen = "cup";
            else if (State.pendingEvents.Count > 0) Screen = "inbox";
        }

        public void NextDay()
        {
            var b = Day.AdvanceDay(State);
            if (b.Count > 0) GoToBlocker();
            Commit();
        }

        /// <summary>결정할 일이 생기거나 새 소식이 올 때까지 여러 날을 넘긴다</summary>
        public void SkipAhead()
        {
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
            if (p.treasury < 0) list.Add("금고가 바닥났다. 28일 안에 메우지 못하면 압류된다");
            return list;
        }
    }
}
