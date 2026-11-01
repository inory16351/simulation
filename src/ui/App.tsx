import { useEffect, useState } from "react";
import { dateLabel, daysToPayday } from "../core/calendar";
import { todaysPlayerMatches } from "../core/challenge";
import { getNation } from "../core/data";
import { advanceDay, blockers } from "../core/day";
import { weeklyWages } from "../core/economy";
import { newGame } from "../core/generators/world";
import { Rng } from "../core/rng";
import { clearSave, loadGame, saveGame } from "../core/state";
import type { GameState, Id } from "../core/types";
import { asset, menuIconUrl } from "./assets";
import { NationCrest } from "./components/common";
import { ChallengeScreen } from "./screens/ChallengeScreen";
import { FinanceScreen } from "./screens/FinanceScreen";
import { GladiatorScreen } from "./screens/GladiatorScreen";
import { InboxScreen } from "./screens/InboxScreen";
import { LeagueScreen } from "./screens/LeagueScreen";
import { MarketScreen } from "./screens/MarketScreen";
import { MatchScreen } from "./screens/MatchScreen";
import { OverviewScreen } from "./screens/OverviewScreen";
import { RosterScreen } from "./screens/RosterScreen";
import { TrainingScreen } from "./screens/TrainingScreen";

export type ScreenId =
  | "inbox" | "overview" | "roster" | "gladiator" | "training" | "challenge" | "match" | "league"
  | "market" | "staff" | "facilities" | "finance" | "world" | "hall";

export interface Nav {
  go: (s: ScreenId) => void;
  openGladiator: (id: Id) => void;
}

/** 상태 변경 도구: update는 그대로, act는 게임 RNG를 이어서 쓴다 */
export interface Ctx {
  state: GameState;
  update: (fn: (draft: GameState) => void) => void;
  act: (fn: (draft: GameState, rng: Rng) => void) => void;
  /** 미리 계산해 둔 상태로 통째로 바꾼다 (공식 경기 중계 후 반영) */
  replace: (s: GameState) => void;
  nav: Nav;
}

const MENU: { id: ScreenId; label: string; ready: boolean }[] = [
  { id: "inbox", label: "서판", ready: true },
  { id: "overview", label: "안뜰", ready: true },
  { id: "roster", label: "막사", ready: true },
  { id: "training", label: "연무장", ready: true },
  { id: "challenge", label: "도전장", ready: true },
  { id: "match", label: "모래 마당", ready: true },
  { id: "league", label: "서열표", ready: true },
  { id: "market", label: "노예 경매", ready: true },
  { id: "staff", label: "식솔", ready: false },
  { id: "facilities", label: "증축", ready: false },
  { id: "finance", label: "금고", ready: true },
  { id: "world", label: "대륙 정세", ready: false },
  { id: "hall", label: "비석의 언덕", ready: false },
];

export function App() {
  const [state, setState] = useState<GameState>(() => loadGame() ?? newGame(Date.now() % 2 ** 31));
  const [screen, setScreen] = useState<ScreenId>("inbox");
  const [selected, setSelected] = useState<Id | null>(null);

  useEffect(() => saveGame(state), [state]);

  const update = (fn: (draft: GameState) => void) => {
    setState((prev) => {
      const draft = structuredClone(prev);
      fn(draft);
      return draft;
    });
  };
  const act = (fn: (draft: GameState, rng: Rng) => void) =>
    update((d) => {
      const rng = new Rng(d.rngState);
      fn(d, rng);
      d.rngState = rng.state;
    });

  const nav: Nav = {
    go: (s) => setScreen(s),
    openGladiator: (id) => {
      setSelected(id);
      setScreen("gladiator");
    },
  };
  const ctx: Ctx = { state, update, act, replace: setState, nav };

  const player = state.ludi[state.playerLudusId];
  const tierLudi = Object.values(state.ludi).filter((l) => l.tier === player.tier).sort((a, b) => b.rp - a.rp);
  const rank = tierLudi.findIndex((l) => l.id === player.id) + 1;
  const unread = state.inbox.filter((m) => !m.read).length;
  const incoming = state.challenges.filter((c) => c.status === "pending" && c.to === player.id).length;
  const nation = getNation(state.nation);
  const blocked = blockers(state);

  /** 막힌 일이 있으면 그 화면으로 안내한다 */
  const goToBlocker = (s: GameState) => {
    if (todaysPlayerMatches(s).length) setScreen("match");
    else if (s.pendingEvents.length) setScreen("inbox");
  };

  const nextDay = () => {
    const draft = structuredClone(state);
    const b = advanceDay(draft);
    if (b.length) goToBlocker(draft);
    setState(draft);
  };

  /** 결정할 일이 생기거나 새 소식이 올 때까지 여러 날을 넘긴다 */
  const skipAhead = () => {
    const draft = structuredClone(state);
    const startInbox = draft.inbox.length;
    for (let i = 0; i < 28; i++) {
      if (advanceDay(draft).length) break;
      if (blockers(draft).length || draft.inbox.length !== startInbox) break;
    }
    goToBlocker(draft);
    setState(draft);
  };

  const restart = () => {
    if (!confirm("새 루두스를 세울까요? 지금 루두스는 사라집니다.")) return;
    clearSave();
    setState(newGame(Date.now() % 2 ** 31));
    setScreen("inbox");
  };

  const unpaid = player.gladiators.filter((id) => state.gladiators[id].unpaidWeeks > 0).length;
  const alerts = [
    ...blocked,
    ...(unpaid ? [`급료가 밀린 검투사 ${unpaid}명`] : []),
    ...(player.treasury < 0 ? ["금고가 마이너스다. 28일 안에 메우지 못하면 압류된다"] : []),
  ];

  return (
    <div className="app">
      <header className="topbar">
        <NationCrest nation={state.nation} size={36} />
        <div>
          <div className="club">{player.name}</div>
          <div className="sub">
            {nation.short} {player.tier}부 · {rank}위 · RP {player.rp}
          </div>
        </div>
        <div className="spacer" />
        <div className="stat">
          <b>시즌 {state.season} · {dateLabel(state.day)}</b>
          <span>급료일까지 {daysToPayday(state.day)}일</span>
        </div>
        <div className="stat">
          <b className={player.treasury < 0 ? "neg" : ""}>{player.treasury.toLocaleString()} D</b>
          <span>주급 합계 {weeklyWages(state, player).toLocaleString()} D</span>
        </div>
        <button className="btn" onClick={skipAhead} disabled={blocked.length > 0} title="결정할 일이나 새 소식이 생길 때까지 진행">
          일정까지 ▶▶
        </button>
        <button className="continue" onClick={nextDay} disabled={!!state.gameOver}>
          {blocked.length ? "처리할 일이 있다" : "다음 날로 ▶"}
        </button>
      </header>
      {alerts.length > 0 ? (
        <div className="alertbar" onClick={() => goToBlocker(state)} style={{ cursor: blocked.length ? "pointer" : undefined }}>
          {alerts.join(" · ")}
        </div>
      ) : <div />}
      <div className="body">
        <nav className="sidebar">
          {MENU.map((m) => (
            <button
              key={m.id}
              className={`${screen === m.id || (screen === "gladiator" && m.id === "roster") ? "active" : ""} ${m.ready ? "" : "soon"}`}
              onClick={() => setScreen(m.id)}
            >
              <span className="menu-label">
                <img src={menuIconUrl(m.id)} alt="" onError={(e) => (e.currentTarget.style.visibility = "hidden")} />
                {m.label}
              </span>
              {m.id === "inbox" && unread > 0 && <span className="badge">{unread}</span>}
              {m.id === "challenge" && incoming > 0 && <span className="badge">{incoming}</span>}
              {m.id === "match" && todaysPlayerMatches(state).length > 0 && <span className="badge">!</span>}
            </button>
          ))}
          <div className="sep" />
          <button onClick={restart}>새 루두스 세우기</button>
        </nav>
        <main className="main">
          {state.gameOver && (
            <div className="panel gameover">
              <h3>루두스를 잃었다</h3>
              <div className="pad">
                <p>{state.gameOver}</p>
                <button className="btn primary" onClick={restart}>새 루두스 세우기</button>
              </div>
            </div>
          )}
          {screen === "inbox" && <InboxScreen {...ctx} />}
          {screen === "overview" && <OverviewScreen state={state} nav={nav} />}
          {screen === "roster" && <RosterScreen state={state} nav={nav} />}
          {screen === "gladiator" && selected && <GladiatorScreen state={state} id={selected} nav={nav} update={update} />}
          {screen === "training" && <TrainingScreen {...ctx} />}
          {screen === "challenge" && <ChallengeScreen {...ctx} />}
          {screen === "league" && <LeagueScreen state={state} />}
          {screen === "match" && <MatchScreen {...ctx} />}
          {screen === "market" && <MarketScreen {...ctx} />}
          {screen === "finance" && <FinanceScreen {...ctx} />}
          {!MENU.find((m) => m.id === screen)?.ready && screen !== "gladiator" && (
            <div className="empty-state" style={{ backgroundImage: `linear-gradient(rgba(20,17,15,0.35), rgba(20,17,15,0.9)), url(${asset("ui/empty.webp")})` }}>
              <h2>{MENU.find((m) => m.id === screen)?.label}</h2>
              <p>아직 문이 닫혀 있다. 다음 개발 단계에서 열린다.</p>
            </div>
          )}
        </main>
      </div>
    </div>
  );
}
