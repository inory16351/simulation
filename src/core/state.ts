import { Rng } from "./rng";
import type { GameState, InboxMessage } from "./types";

/** 게임 상태의 RNG에서 시드 하나를 뽑고 상태를 전진시킨다. */
export function drawSeed(state: GameState): number {
  const r = new Rng(state.rngState);
  const s = r.int(0, 2 ** 31 - 1);
  state.rngState = r.state;
  return s;
}

export function pushMessage(state: GameState, msg: Omit<InboxMessage, "id" | "season" | "day" | "read">): void {
  state.inbox.unshift({ ...msg, id: `M${state.nextId++}`, season: state.season, day: state.day, read: false });
}

const SAVE_KEY = "colosseum-league.save.v6";

export function saveGame(state: GameState): void {
  try {
    localStorage.setItem(SAVE_KEY, JSON.stringify(state));
  } catch {
    // 저장 공간이 없거나 막혀 있어도 게임은 계속 진행한다
  }
}

export function loadGame(): GameState | null {
  try {
    const raw = localStorage.getItem(SAVE_KEY);
    if (!raw) return null;
    const s = JSON.parse(raw) as GameState;
    return s.version === 6 ? s : null;
  } catch {
    return null;
  }
}

export function clearSave(): void {
  try {
    localStorage.removeItem(SAVE_KEY);
  } catch {
    // 무시
  }
}
