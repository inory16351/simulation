import { logCareer, seasonRecord } from "../career";
import { BALANCE, NAMES, NATIONS, getNation } from "../data";
import { payDividend, refreshFreeAgents } from "../economy";
import { refreshStaffMarket, startingStaff } from "../staff";
import { Rng } from "../rng";
import type { GameState, Ludus, NationId } from "../types";
import { generateGladiator } from "./gladiator";

/** 시작 국가 계수 (판테온 컵 성적 3시즌치를 가정) */
export const INITIAL_COEFFICIENT: Record<NationId, number> = { aurelia: 11, grommar: 9.5, dolgar: 9, karsea: 8, kemer: 7, sylvaren: 6 };

const PERSONALITIES: NonNullable<Ludus["aiPersonality"]>[] = ["aggressive", "stable", "grudge", "merchant", "developer"];

export function newGame(seed: number, nation: NationId = "aurelia", ludusName = "모래의 집"): GameState {
  const rng = new Rng(seed);
  const state: GameState = {
    version: 9,
    seed,
    rngState: 0,
    season: 1,
    day: 1,
    nation,
    playerLudusId: "",
    ludi: {},
    gladiators: {},
    inbox: [],
    fallen: [],
    challenges: [],
    staff: {},
    relations: Object.fromEntries(NATIONS.map((n) => [n.id, { ...n.relations }])) as GameState["relations"],
    wars: [],
    contracts: [],
    staffMarket: [],
    freeAgents: [],
    market: [],
    ledger: [],
    pendingEvents: [],
    lastDisasterAt: -999,
    luckStreak: 0,
    modifiers: [],
    coefficients: {} as GameState["coefficients"],
    nextId: 1,
  };
  const newId = (prefix: string) => `${prefix}${state.nextId++}`;
  const usedNames = new Set<string>([ludusName]);
  const usedGladiatorNames = new Set<string>();
  const L = BALANCE.league;

  const addLudus = (tier: 1 | 2, isPlayer: boolean, name: string, rp: number, rosterSize: number, target: number, ludusNation: NationId = nation) => {
    const ludus: Ludus = {
      id: newId("L"),
      name,
      nation: ludusNation,
      tier,
      rp: Math.round(rp),
      treasury: isPlayer ? BALANCE.economy.startTreasury : rng.int(3000, tier === 1 ? 15000 : 7000),
      gladiators: [],
      isPlayer,
      aiPersonality: isPlayer ? undefined : rng.pick(PERSONALITIES),
      record: { wins: 0, losses: 0 },
      matchesThisSeason: 0,
      refusalsUsed: 0,
      facilities: { barracks: 1, training: 1, medical: 1, bath: 0, arena: 1, guard: 0, academy: 0 },
      staff: [],
      youth: [],
    };
    for (let i = 0; i < rosterSize; i++) {
      const g = generateGladiator(rng, {
        id: newId("G"),
        nation: ludusNation,
        target: target + rng.range(-1.5, 1.5),
        usedNames: usedGladiatorNames,
        // 플레이어 시작 로스터는 노예 위주
        status: isPlayer ? (rng.chance(0.85) ? "slave" : "free") : undefined,
      });
      state.gladiators[g.id] = g;
      ludus.gladiators.push(g.id);
    }
    state.ludi[ludus.id] = ludus;
    return ludus;
  };

  const ludusNameGen = () => {
    for (;;) {
      // "붉은 늑대 투기단" 또는 "늑대의 집" 꼴
      const n = rng.chance(0.35)
        ? `${rng.pick(NAMES.ludusAdj)} ${rng.pick(NAMES.ludusNoun)}${rng.pick(NAMES.ludusSuffixAfterAdj)}`
        : rng.pick(NAMES.ludusNoun) + rng.pick(NAMES.ludusSuffix);
      if (!usedNames.has(n)) {
        usedNames.add(n);
        return n;
      }
    }
  };

  for (let i = 0; i < L.teamsPerTier; i++) {
    addLudus(1, false, ludusNameGen(), L.rpTier1 + rng.normal(0, 60), BALANCE.economy.aiRoster, 11 - i * 0.15);
  }
  for (let i = 0; i < L.teamsPerTier - 1; i++) {
    addLudus(2, false, ludusNameGen(), L.rpTier2 + rng.normal(0, 50), BALANCE.economy.aiRoster, 8 - i * 0.15);
  }
  const player = addLudus(2, true, ludusName, L.rpTier2 - 60, BALANCE.economy.startRoster, 6.5);
  state.playerLudusId = player.id;

  // 다른 나라: 1부 리그만 실제로 돌린다 (판테온 컵 상대). 국가 계수가 높은 나라일수록 조금 더 강하다
  for (const other of NATIONS) {
    if (other.id === nation) continue;
    const strength = (INITIAL_COEFFICIENT[other.id] - 8) * 0.12;
    for (let i = 0; i < L.teamsPerTier; i++) {
      addLudus(1, false, ludusNameGen(), L.rpTier1 + rng.normal(0, 60), BALANCE.economy.aiRoster, 11 + strength - i * 0.15, other.id);
    }
  }
  state.coefficients = Object.fromEntries(NATIONS.map((n) => [n.id, [INITIAL_COEFFICIENT[n.id], INITIAL_COEFFICIENT[n.id], INITIAL_COEFFICIENT[n.id]]])) as GameState["coefficients"];

  const nationDef = getNation(nation);
  state.inbox.push({
    id: newId("M"),
    season: 1,
    day: 1,
    kind: "info",
    title: `${nationDef.league} 2부 입성`,
    body:
      `라니스타, ${ludusName}의 문이 열렸다.\n\n` +
      `금고엔 ${BALANCE.economy.startTreasury.toLocaleString()} 데나리, 막사엔 검투사 ${BALANCE.economy.startRoster}명. ` +
      `다들 급료 날만 기다린다. 돈줄이 끊기면 저 칼끝이 누구를 향할지 잊지 마라.\n\n` +
      `이번 시즌 목표: 2부 리그 2위 안에 들어 승격.\n\n` +
      `${nationDef.name}의 ${nationDef.league} 특징:\n${nationDef.ruleText.map((t) => `· ${t}`).join("\n")}`,
    read: false,
  });

  // 시즌 시작: 배당금, 경매장, 일대기 첫 줄
  for (const l of Object.values(state.ludi)) payDividend(state, l, l.isPlayer ? BALANCE.economy.promotedDividendCoef : undefined);
  refreshStaffMarket(state, rng);
  refreshFreeAgents(state, rng);
  startingStaff(state, rng, state.ludi[state.playerLudusId]);
  for (const l of Object.values(state.ludi)) {
    for (const id of l.gladiators) {
      const g = state.gladiators[id];
      seasonRecord(state, g);
      logCareer(state, g, `${l.name}에 들어왔다`, "join");
    }
  }

  state.rngState = rng.state;
  return state;
}
