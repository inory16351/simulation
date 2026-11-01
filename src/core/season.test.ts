import { describe, expect, it } from "vitest";
import { createChallenge, playChallenge, respond, todaysPlayerMatches } from "./challenge";
import { advanceDay } from "./day";
import { getEvent, resolveEvent } from "./events";
import { newGame } from "./generators/world";
import { Rng } from "./rng";
import type { GameState } from "./types";

/** 플레이어 결정을 자동으로 내리며 n일을 진행한다 */
function autoplay(state: GameState, days: number, opts: { challenge?: boolean } = {}) {
  const rng = new Rng(4242);
  for (let i = 0; i < days && !state.gameOver; i++) {
    for (const p of [...state.pendingEvents]) {
      // 낼 수 있는 첫 선택지를 고른다
      const def = getEvent(p.eventId);
      const i = def.choices.findIndex((c) => !c.cost || state.ludi[state.playerLudusId].treasury >= c.cost);
      resolveEvent(state, rng, p.id, i);
    }
    for (const c of state.challenges.filter((x) => x.status === "pending" && state.ludi[x.to].isPlayer)) respond(state, c, true, rng);
    for (const c of todaysPlayerMatches(state)) playChallenge(state, c, rng);
    if (opts.challenge && state.day % 4 === 0) {
      const me = state.ludi[state.playerLudusId];
      const target = Object.values(state.ludi).find((l) => !l.isPlayer && l.tier === me.tier);
      const g = me.gladiators.map((id) => state.gladiators[id]).find((x) => x.alive);
      if (target && g) createChallenge(state, me, target, g.id, "training");
    }
    const blocked = advanceDay(state);
    expect(blocked.filter((b) => !b.includes("파산") && !b.includes("반란"))).toEqual([]);
  }
}

describe("시즌 진행", () => {
  it("한 시즌을 끝까지 돌리고 다음 시즌으로 넘어간다", () => {
    const s = newGame(2024);
    autoplay(s, 170, { challenge: true });
    expect(s.season).toBe(2);
    const tiers = Object.values(s.ludi).map((l) => l.tier);
    expect(tiers.filter((t) => t === 1)).toHaveLength(10);
    // AI 루두스끼리도 경기를 치러 RP가 움직였다
    const rps = Object.values(s.ludi).map((l) => l.rp);
    expect(new Set(rps).size).toBeGreaterThan(10);
  });

  it("급료를 못 주면 미납이 쌓이고 사기가 떨어진다", () => {
    const s = newGame(7);
    const me = s.ludi[s.playerLudusId];
    // 이벤트로 돈이 들어와도 급료를 못 줄 만큼 빚을 지운다
    me.treasury = -10000;
    const before = me.gladiators.map((id) => s.gladiators[id].morale);
    autoplay(s, 7);
    const g = s.gladiators[me.gladiators[0]];
    expect(g.unpaidWeeks).toBeGreaterThan(0);
    expect(me.gladiators.map((id) => s.gladiators[id].morale).reduce((a, b) => a + b, 0)).toBeLessThan(before.reduce((a, b) => a + b, 0));
  });

  it("경기를 뛰면 일대기와 시즌 기록이 남는다", () => {
    const s = newGame(99);
    autoplay(s, 60, { challenge: true });
    const me = s.ludi[s.playerLudusId];
    const played = [...me.gladiators, ...s.fallen].map((id) => s.gladiators[id]).filter((g) => g.history[0]?.apps > 0);
    expect(played.length).toBeGreaterThan(0);
    expect(played[0].career.length).toBeGreaterThan(1);
  });
});

describe("이벤트 데이터", () => {
  it("모든 이벤트에 돈 없이 고를 수 있는 선택지가 있다 (진행이 막히지 않게)", async () => {
    const { EVENTS } = await import("./events");
    for (const e of EVENTS) expect(e.choices.some((c) => !c.cost), e.id).toBe(true);
  });
});
