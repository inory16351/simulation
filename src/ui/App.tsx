import { useEffect, useState } from "react";
import { getNation } from "../core/data";
import { newGame } from "../core/generators/world";
import { clearSave, loadGame, saveGame } from "../core/state";
import type { GameState, Id } from "../core/types";
import { dateLabel, daysToPayday } from "../core/calendar";
import { asset, menuIconUrl } from "./assets";
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
  { id: "inbox", label: "서판", ready: true },
  { id: "overview", label: "안뜰", ready: true },
  { id: "roster", label: "막사", ready: true },
  { id: "training", label: "연무장", ready: false },
  { id: "challenge", label: "도전장", ready: false },
  { id: "match", label: "모래 마당", ready: true },
  { id: "league", label: "서열표", ready: true },
  { id: "market", label: "노예 경매", ready: false },
  { id: "staff", label: "식솔", ready: false },
  { id: "facilities", label: "증축", ready: false },
  { id: "finance", label: "금고", ready: false },
  { id: "world", label: "대륙 정세", ready: false },
  { id: "hall", label: "비석의 언덕", ready: false },
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
    if (!confirm("새 루두스를 세울까요? 지금 루두스는 사라집니다.")) return;
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
          <b>시즌 {state.season} · {dateLabel(state.day)}</b>
          <span>급료일까지 {daysToPayday(state.day)}일</span>
        </div>
        <div className="stat">
          <b>{player.treasury.toLocaleString()} D</b>
          <span>주급 합계 {weeklyWages.toLocaleString()} D</span>
        </div>
        <button className="continue" disabled title="일 단위 진행은 다음 단계(M2)에서 구현됩니다">
          다음 날로 ▶
        </button>
      </header>
      <div className="alertbar">
        개발 빌드 M1 — 날짜 진행·도전·시장은 다음 단계에서 구현됩니다. 지금은 막사 확인과 모래 마당 연습 시합(실제 사망 포함)을 해볼 수 있습니다.
      </div>
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
            </button>
          ))}
          <div className="sep" />
          <button onClick={restart}>새 루두스 세우기</button>
        </nav>
        <main className="main">
          {screen === "inbox" && <InboxScreen state={state} update={update} />}
          {screen === "overview" && <OverviewScreen state={state} nav={nav} />}
          {screen === "roster" && <RosterScreen state={state} nav={nav} />}
          {screen === "gladiator" && selected && <GladiatorScreen state={state} id={selected} nav={nav} update={update} />}
          {screen === "league" && <LeagueScreen state={state} />}
          {screen === "match" && <MatchScreen state={state} update={update} nav={nav} />}
          {!MENU.find((m) => m.id === screen)?.ready && screen !== "gladiator" && (
            <div className="empty-state" style={{ backgroundImage: `linear-gradient(rgba(20,17,15,0.35), rgba(20,17,15,0.9)), url(${asset("ui/empty.jpg")})` }}>
              <h2>{MENU.find((m) => m.id === screen)?.label}</h2>
              <p>아직 문이 닫혀 있다. 다음 개발 단계에서 열린다.</p>
            </div>
          )}
        </main>
      </div>
    </div>
  );
}
