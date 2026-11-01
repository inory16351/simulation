import { absDay } from "./calendar";
import type { GameState } from "./types";

/** 세계 이벤트가 건 일시 배율 (관중 수입·식비·시장 가격) */
export function modifierMult(state: GameState, kind: "ticket" | "food" | "market"): number {
  const now = absDay(state.season, state.day);
  return state.modifiers.filter((m) => m.kind === kind && m.until >= now).reduce((p, m) => p * m.mult, 1);
}
