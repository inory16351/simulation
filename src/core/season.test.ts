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
    expect(played.some((g) => g.career.length > 1)).toBe(true);
  });
});

describe("이벤트 데이터", () => {
  it("모든 이벤트에 돈 없이 고를 수 있는 선택지가 있다 (진행이 막히지 않게)", async () => {
    const { EVENTS } = await import("./events");
    for (const e of EVENTS) expect(e.choices.some((c) => !c.cost), e.id).toBe(true);
  });
});

describe("나라별 리그", () => {
  it("여섯 나라 모두 새 게임과 60일 진행이 된다", async () => {
    const { NATIONS } = await import("./data");
    for (const n of NATIONS) {
      const s = newGame(11, n.id, "시험 루두스");
      expect(Object.values(s.ludi).every((l) => l.nation === n.id)).toBe(true);
      autoplay(s, 60, { challenge: true });
      if (n.rules.suddenBanned) expect(s.challenges.some((c) => c.rule === "sudden")).toBe(false);
    }
  });
});

describe("부상", () => {
  it("부상 중에는 능력치가 떨어지고, 영구 손상은 다 나은 뒤 남는다", async () => {
    const { makeInjury, onHealed } = await import("./injuries");
    const { effectiveAttr } = await import("./gladiator");
    const s = newGame(3);
    const g = s.gladiators[s.ludi[s.playerLudusId].gladiators[0]];
    const rng = new Rng(1);
    g.injury = { ...makeInjury(rng, "severe", "arm"), type: "severed_fingers", desc: "손가락이 잘림" };
    const accInjured = effectiveAttr(g, "acc");
    const accBase = g.attrs.acc;
    expect(accInjured).toBeLessThan(Math.min(20, accBase + 5));
    onHealed(s, g, rng, 1, 0, true);
    expect(g.attrs.acc).toBe(Math.max(1, accBase - 2));
  });
});

describe("유소년 학당", () => {
  it("학당을 지으면 아이들이 들어오고, 견습 투기로 자라며 데뷔 나이가 되면 승격할 수 있다", async () => {
    const { intake, apprenticeBouts, promoteYouth } = await import("./youth");
    const { computeCA, ageStage } = await import("./gladiator");
    const s = newGame(21);
    const me = s.ludi[s.playerLudusId];
    me.facilities.academy = 3;
    const rng = new Rng(9);
    const kids = intake(s, rng, me);
    expect(kids.length).toBe(4);
    expect(kids.every((k) => ageStage(k) === "youth")).toBe(true);
    const before = kids.map(computeCA).reduce((a, b) => a + b, 0);
    for (let i = 0; i < 40; i++) apprenticeBouts(s, rng, me);
    expect(kids.every((k) => k.alive)).toBe(true);
    expect(kids.map(computeCA).reduce((a, b) => a + b, 0)).toBeGreaterThan(before);
    const k = kids[0];
    expect(promoteYouth(s, me, k.id)).not.toBeNull();
    k.age = 30;
    expect(promoteYouth(s, me, k.id)).toBeNull();
    expect(me.gladiators).toContain(k.id);
  });
});

describe("용병·전쟁", () => {
  it("관계가 나쁘면 전쟁이 나고, 의뢰를 받아 파견하면 보수를 받고 돌아온다", async () => {
    const { takeContract, CONTRACT_TYPES } = await import("./war");
    const { computeCA } = await import("./gladiator");
    const s = newGame(5, "karsea");
    s.relations.aurelia.grommar = -100;
    s.relations.grommar.aurelia = -100;
    autoplay(s, 40);
    expect(s.wars.length).toBeGreaterThan(0);
    const me = s.ludi[s.playerLudusId];
    const c = s.contracts.find((x) => x.status === "open" && x.type !== "duel")
      ?? (() => { throw new Error("의뢰 없음"); })();
    c.minCA = 1;
    const g = me.gladiators.map((id) => s.gladiators[id]).sort((a, b) => computeCA(b) - computeCA(a))[0];
    const before = me.treasury;
    expect(takeContract(s, c.id, [g.id])).toBeNull();
    expect(g.deployment).toBeDefined();
    expect(me.treasury).toBeGreaterThan(before);
    autoplay(s, c.days + 2);
    expect(c.status).toBe("done");
    expect(g.deployment).toBeUndefined();
    expect(CONTRACT_TYPES[c.type].name.length).toBeGreaterThan(0);
  });
});

describe("이적·스카우팅", () => {
  it("이적료 합의 뒤 개인 계약을 맺으면 검투사가 옮겨 온다", async () => {
    const { bid, completeTransfer, transferDemand, askingPrice } = await import("./transfers");
    const s = newGame(8);
    s.day = 3;
    const me = s.ludi[s.playerLudusId];
    me.treasury = 100000;
    me.facilities.barracks = 5;
    const seller = Object.values(s.ludi).find((l) => !l.isPlayer && l.tier === 2)!;
    const g = s.gladiators[seller.gladiators[0]];
    const asking = askingPrice(s, g, seller);
    expect(bid(s, g.id, Math.round(asking * 0.3)).status).toBe("rejected");
    const r = bid(s, g.id, asking * 2);
    expect(r.status).toBe("accepted");
    if (r.status !== "accepted") return;
    const d = transferDemand(s, g);
    expect(completeTransfer(s, r.deal.id, d.refuse ? 1 : d.wage, 2, 0)).toBe(d.refuse ? expect.any(String) : null);
    if (!d.refuse) expect(me.gladiators).toContain(g.id);
  });

  it("정찰하면 추정 범위가 좁아진다", async () => {
    const { caRange, assignScout, dailyScouting } = await import("./scouting");
    const s = newGame(8);
    const other = Object.values(s.ludi).find((l) => !l.isPlayer)!;
    const g = s.gladiators[other.gladiators[0]];
    g.popularity = 0;
    const before = caRange(s, g);
    assignScout(s, g.id);
    for (let i = 0; i < 30; i++) dailyScouting(s);
    const after = caRange(s, g);
    expect(after.hi - after.lo).toBeLessThan(before.hi - before.lo);
  });
});
