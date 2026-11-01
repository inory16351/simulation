import { useEffect, useState } from "react";
import { getNation } from "../core/data";
import { newGame } from "../core/generators/world";
import { clearSave, loadGame, saveGame } from "../core/state";
import type { GameState, Id } from "../core/types";
import { NationCrest } from "./components/common";
import { GladiatorScreen } from "./screens/GladiatorScreen";
import { InboxScreen } from "./screens/InboxScreen";
import { LeagueScreen } from "./screens/LeagueScreen";
import { MatchScreen } from "./screens/MatchScreen";
import { OverviewScreen } from "./screens/OverviewScreen";
import { RosterScreen } from "./screens/RosterScreen";

export type ScreenId =
  | "inbox" | "overview" | "roster" | "gladiator" | "training" | "challenge" | "match" | "league"
  | "market" | "staff" | "facilities" | "finance" | "world" | "hall";

export interface Nav {
  go: (s: ScreenId) => void;
  openGladiator: (id: Id) => void;
}

const MENU: { id: ScreenId; label: string; ready: boolean }[] = [
  { id: "inbox", label: "받은편지함", ready: true },
  { id: "overview", label: "개요", ready: true },
  { id: "roster", label: "로스터", ready: true },
  { id: "training", label: "훈련", ready: false },
  { id: "challenge", label: "도전", ready: false },
  { id: "match", label: "친선 경기", ready: true },
  { id: "league", label: "일정·리그", ready: true },
  { id: "market", label: "시장", ready: false },
  { id: "staff", label: "직원", ready: false },
  { id: "facilities", label: "시설", ready: false },
  { id: "finance", label: "재정", ready: false },
  { id: "world", label: "세계", ready: false },
  { id: "hall", label: "명예의 전당", ready: false },
];

export function App() {
  const [state, setState] = useState<GameState>(() => loadGame() ?? newGame(Date.now() % 2 ** 31));
  const [screen, setScreen] = useState<ScreenId>("inbox");
  const [selected, setSelected] = useState<Id | null>(null);

  useEffect(() => saveGame(state), [state]);

  /** 상태를 복제해서 변경한 뒤 반영한다 (코어 함수는 상태를 직접 수정하므로) */
  const update = (fn: (draft: GameState) => void) => {
    setState((prev) => {
      const draft = structuredClone(prev);
      fn(draft);
      return draft;
    });
  };

  const nav: Nav = {
    go: (s) => setScreen(s),
    openGladiator: (id) => {
      setSelected(id);
      setScreen("gladiator");
    },
  };

  const player = state.ludi[state.playerLudusId];
  const tierLudi = Object.values(state.ludi).filter((l) => l.tier === player.tier).sort((a, b) => b.rp - a.rp);
  const rank = tierLudi.findIndex((l) => l.id === player.id) + 1;
  const unread = state.inbox.filter((m) => !m.read).length;
  const weeklyWages = player.gladiators.reduce((s, id) => s + state.gladiators[id].wage, 0);
  const nation = getNation(state.nation);

  const restart = () => {
    if (!confirm("새 게임을 시작할까요? 현재 진행은 사라집니다.")) return;
    clearSave();
    setState(newGame(Date.now() % 2 ** 31));
    setScreen("inbox");
  };

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
          <b>시즌 {state.season} · {state.week}주차</b>
          <span>정규 기간은 3주차부터</span>
        </div>
        <div className="stat">
          <b>{player.treasury.toLocaleString()} D</b>
          <span>주급 합계 {weeklyWages.toLocaleString()} D</span>
        </div>
        <button className="continue" disabled title="주간 진행은 다음 단계(M2)에서 구현됩니다">
          계속 ▶
        </button>
      </header>
      <div className="alertbar">
        개발 빌드 M1 — 주간 진행·도전·시장은 다음 단계에서 구현됩니다. 지금은 로스터 확인과 친선 경기(실제 사망 포함)를 해볼 수 있습니다.
      </div>
      <div className="body">
        <nav className="sidebar">
          {MENU.map((m) => (
            <button
              key={m.id}
              className={`${screen === m.id || (screen === "gladiator" && m.id === "roster") ? "active" : ""} ${m.ready ? "" : "soon"}`}
              onClick={() => setScreen(m.id)}
            >
              {m.label}
              {m.id === "inbox" && unread > 0 && <span className="badge">{unread}</span>}
            </button>
          ))}
          <div className="sep" />
          <button onClick={restart}>새 게임</button>
        </nav>
        <main className="main">
          {screen === "inbox" && <InboxScreen state={state} update={update} />}
          {screen === "overview" && <OverviewScreen state={state} nav={nav} />}
          {screen === "roster" && <RosterScreen state={state} nav={nav} />}
          {screen === "gladiator" && selected && <GladiatorScreen state={state} id={selected} nav={nav} />}
          {screen === "league" && <LeagueScreen state={state} />}
          {screen === "match" && <MatchScreen state={state} update={update} nav={nav} />}
          {!MENU.find((m) => m.id === screen)?.ready && screen !== "gladiator" && (
            <div className="empty-state">
              <h2>{MENU.find((m) => m.id === screen)?.label}</h2>
              <p>이 화면은 다음 개발 단계에서 구현됩니다.</p>
            </div>
          )}
        </main>
      </div>
    </div>
  );
}
