import { describe, expect, it } from "vitest";
import { createChallenge, playChallenge, respond, todaysPlayerMatches } from "./challenge";
import { todaysPlayerTie, playTie } from "./cup";
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
    const tie = todaysPlayerTie(state);
    if (tie) playTie(state, tie.id, rng);
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
    const tiers = Object.values(s.ludi).filter((l) => l.nation === s.nation).map((l) => l.tier);
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

describe("판테온 컵", () => {
  it("16팀이 조별 리그와 토너먼트를 거쳐 우승팀이 나오고, 시즌이 끝나면 나라 계수가 바뀐다", () => {
    const s = newGame(77);
    const before = JSON.stringify(s.coefficients);
    autoplay(s, 151);
    const cup = s.cup!;
    expect(cup.teams).toHaveLength(16);
    expect(new Set(cup.teams.map((id) => s.ludi[id].nation)).size).toBe(6);
    expect(cup.ties.filter((t) => t.stage === "group")).toHaveLength(48);
    expect(cup.ties.every((t) => t.winner)).toBe(true);
    expect(cup.champion).toBeDefined();
    autoplay(s, 20);
    expect(s.season).toBe(2);
    expect(JSON.stringify(s.coefficients)).not.toBe(before);
  });

  it("단체전은 승자가 남아 다음 상대와 싸우고, 한쪽 명단이 다 쓰러지면 끝난다", async () => {
    const { runSeries, autoLineup } = await import("./series");
    const s = newGame(5);
    const [a, b] = Object.values(s.ludi).filter((l) => l.nation === s.nation && l.tier === 1);
    const la = autoLineup(s, a, 3, 1), lb = autoLineup(s, b, 3, 1);
    const r = runSeries(s, [a, b], [la, lb], { rule: "training", mode: "winner_stays" }, new Rng(1));
    expect(r.score[r.winner]).toBe(3);
    expect(r.bouts.length).toBeGreaterThanOrEqual(3);
    expect(r.bouts.length).toBeLessThanOrEqual(5);
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
      expect(Object.values(s.ludi).filter((l) => l.nation === n.id)).toHaveLength(20);
      autoplay(s, 60, { challenge: true });
      if (n.rules.suddenBanned) expect(s.challenges.some((c) => c.rule === "sudden")).toBe(false);
    }
  }, 30000); // 나라 여섯 × 70개 루두스를 60일씩 돌린다
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
    const { takeContract, CONTRACT_TYPES, eligible } = await import("./war");
    const { computeCA } = await import("./gladiator");
    const s = newGame(5, "karsea");
    s.relations.aurelia.grommar = -100;
    s.relations.grommar.aurelia = -100;
    // 전쟁은 확률로 터진다. 열흘씩 넘기며 기다린다
    for (let i = 0; i < 12 && !s.contracts.some((x) => x.status === "open" && x.type !== "duel"); i++) autoplay(s, 10);
    expect(s.wars.length).toBeGreaterThan(0);
    const me = s.ludi[s.playerLudusId];
    const c = s.contracts.find((x) => x.status === "open" && x.type !== "duel")
      ?? (() => { throw new Error("의뢰 없음"); })();
    c.minCA = 1;
    // 가장 강한 검투사를 보낸다 (잡힌 경기와 중상은 지운다)
    const g = me.gladiators.map((id) => s.gladiators[id]).sort((a, b) => computeCA(b) - computeCA(a))[0];
    s.challenges = s.challenges.filter((x) => x.fromGladiator !== g.id && x.toGladiator !== g.id);
    g.injury = undefined;
    expect(eligible(s, g, c)).toBeNull();
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

describe("장비·던전", () => {
  it("장비 시장에서 사서 착용하면 원래 장비는 창고로 간다. 양손 무기는 보조를 비운다", async () => {
    const { buyListing, equipItem, armoryOf, gearMarketOf, cannotEquip } = await import("./gear");
    const { getWeapon } = await import("./data");
    const s = newGame(11);
    const me = s.ludi[s.playerLudusId];
    me.treasury = 100000;
    expect(gearMarketOf(s).length).toBeGreaterThan(20);
    s.gearMarket!.push({ id: "Gt", item: "greataxe+2", price: 500, nation: s.nation, seller: "시험 상인" });
    expect(buyListing(s, "Gt")).toBeNull();
    expect(armoryOf(s)).toContain("greataxe+2");
    expect(getWeapon("greataxe+2")!.damage).toBeGreaterThan(getWeapon("greataxe")!.damage);
    const g = s.gladiators[me.gladiators[0]];
    g.equipment.off = "parmula";
    const oldMain = g.equipment.main;
    expect(equipItem(s, g.id, "main", "greataxe+2")).toBeNull();
    expect(g.equipment.main).toBe("greataxe+2");
    expect(g.equipment.off).toBeNull();
    expect(armoryOf(s)).toContain("parmula");
    if (oldMain !== "cestus") expect(armoryOf(s)).toContain(oldMain);
    expect(cannotEquip(g, "off", "parmula")).not.toBeNull();
  });

  it("던전 원정: 1~5명, 급료는 그대로 나가고 장비를 들고 돌아온다. 토벌 의뢰를 깨면 RP와 현상금", async () => {
    const { startExpedition, dailyDungeons, expeditionsOf, questsOf, getDungeon } = await import("./dungeon");
    const s = newGame(12);
    const me = s.ludi[s.playerLudusId];
    me.treasury = 100000;
    const party = me.gladiators.slice(0, 6);
    expect(startExpedition(s, "sewer", party, "shieldwall")).not.toBeNull();
    // 몬스터를 아주 약하게 해서 끝까지 깨게 한다
    const d = getDungeon("sewer");
    const level = d.level;
    d.level = 1;
    s.dungeonQuests = [{ id: "Qt", nation: s.nation, dungeon: "sewer", hard: 1, bounty: 3000, rp: 40, postedAt: 0, expiresAt: 99999, status: "open" }];
    const rp = me.rp;
    expect(startExpedition(s, "sewer", party.slice(0, 5), "wedge", "Qt")).toBeNull();
    expect(s.gladiators[party[0]].deployment?.kind).toBe("dungeon");
    const rng = new Rng(3);
    for (let i = 0; i < d.days + 1; i++) { s.day++; dailyDungeons(s, rng); }
    const ex = expeditionsOf(s)[0];
    expect(ex.status).toBe("done");
    expect(ex.loot.length).toBeGreaterThanOrEqual(d.floors);
    expect(s.armory).toEqual(expect.arrayContaining(ex.loot));
    expect(questsOf(s)[0].status).toBe("done");
    expect(me.rp).toBe(rp + 40);
    for (const id of party.slice(0, 5)) expect(s.gladiators[id].deployment).toBeUndefined();
    d.level = level;
  });

  it("던전 5:5 전투는 강한 쪽이 이기고, 쓰러진 자를 알려 준다", async () => {
    const { skirmish } = await import("./dungeon");
    const { generateGladiator } = await import("./generators/gladiator");
    const rng = new Rng(5);
    const strong = Array.from({ length: 5 }, (_, i) => generateGladiator(rng, { id: `s${i}`, nation: "aurelia", target: 16 }));
    const weak = Array.from({ length: 5 }, (_, i) => generateGladiator(rng, { id: `w${i}`, nation: "grommar", target: 4 }));
    let wins = 0;
    for (let i = 0; i < 6; i++) if (skirmish(new Rng(i), strong, weak, "shieldwall").won) wins++;
    expect(wins).toBeGreaterThanOrEqual(5);
    const lose = skirmish(new Rng(9), weak, strong, "wedge");
    expect(lose.won).toBe(false);
    expect(lose.downed.length).toBeGreaterThan(0);
  });
});
