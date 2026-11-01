import { logCareer, seasonRecord } from "../career";
import { BALANCE, NAMES, getNation } from "../data";
import { payDividend, refreshMarket } from "../economy";
import { refreshStaffMarket, startingStaff } from "../staff";
import { Rng } from "../rng";
import type { GameState, Ludus, NationId } from "../types";
import { generateGladiator } from "./gladiator";

const PERSONALITIES: NonNullable<Ludus["aiPersonality"]>[] = ["aggressive", "stable", "grudge", "merchant", "developer"];

export function newGame(seed: number, nation: NationId = "aurelia", ludusName = "모래의 집"): GameState {
  const rng = new Rng(seed);
  const state: GameState = {
    version: 5,
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
    staffMarket: [],
    market: [],
    ledger: [],
    pendingEvents: [],
    lastDisasterAt: -999,
    luckStreak: 0,
    modifiers: [],
    nextId: 1,
  };
  const newId = (prefix: string) => `${prefix}${state.nextId++}`;
  const usedNames = new Set<string>([ludusName]);
  const usedGladiatorNames = new Set<string>();
  const L = BALANCE.league;

  const addLudus = (tier: 1 | 2, isPlayer: boolean, name: string, rp: number, rosterSize: number, target: number) => {
    const ludus: Ludus = {
      id: newId("L"),
      name,
      nation,
      tier,
      rp: Math.round(rp),
      treasury: isPlayer ? BALANCE.economy.startTreasury : rng.int(3000, tier === 1 ? 15000 : 7000),
      gladiators: [],
      isPlayer,
      aiPersonality: isPlayer ? undefined : rng.pick(PERSONALITIES),
      record: { wins: 0, losses: 0 },
      matchesThisSeason: 0,
      refusalsUsed: 0,
      facilities: { barracks: 1, training: 1, medical: 1, bath: 0, arena: 1, guard: 0 },
      staff: [],
    };
    for (let i = 0; i < rosterSize; i++) {
      const g = generateGladiator(rng, {
        id: newId("G"),
        nation,
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
  refreshMarket(state, rng);
  refreshStaffMarket(state, rng);
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
