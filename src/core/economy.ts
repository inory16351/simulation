// 금고: 배당금, 급료일, 장부, 시장 가격
import { absDay } from "./calendar";
import { BALANCE, getRace } from "./data";
import { logCareer, seasonRecord } from "./career";
import { josa } from "./josa";
import { computeCA } from "./gladiator";
import { computeWage, generateGladiator } from "./generators/gladiator";
import { capacityAt, weeklyUpkeep } from "./facilities";
import { modifierMult } from "./modifiers";
import { rulesOfNation } from "./nationRules";
import { acceptsOffer, demandFor, gladiatorStanding, ludusReputation, type Demand } from "./reputation";
import { staffWages } from "./staff";
import type { Rng } from "./rng";
import { pushMessage } from "./state";
import type { GameState, Gladiator, Ludus } from "./types";

const E = BALANCE.economy;

/** 금고 입출금. 플레이어 루두스는 장부에 남긴다 */
export function addMoney(state: GameState, ludus: Ludus, amount: number, desc: string): void {
  if (amount === 0) return;
  // 수입에는 세금이 붙는다
  const net = amount > 0 ? Math.round(amount * (1 - rulesOfNation(ludus.nation).taxRate)) : amount;
  ludus.treasury += net;
  if (ludus.isPlayer) {
    state.ledger.unshift({ at: absDay(state.season, state.day), amount: net, desc: amount > 0 ? `${desc} (세금 공제)` : desc });
    if (state.ledger.length > 300) state.ledger.pop();
  }
}

/** 한 나라 한 리그의 RP 순위 (나라를 안 주면 플레이어 나라) */
export function tierRanking(state: GameState, tier: 1 | 2, nation = state.nation): Ludus[] {
  return Object.values(state.ludi).filter((l) => l.tier === tier && l.nation === nation).sort((a, b) => b.rp - a.rp);
}

/** 시즌 배당금 지급. coef가 없으면 현재 순위로 계산 */
export function payDividend(state: GameState, ludus: Ludus, coef?: number): void {
  const rank = tierRanking(state, ludus.tier, ludus.nation).findIndex((l) => l.id === ludus.id) + 1;
  const c = coef ?? 1.5 - rank / BALANCE.league.teamsPerTier;
  const amount = Math.round(E.dividendBase[String(ludus.tier) as "1" | "2"] * c);
  ludus.treasury += amount;
  if (ludus.isPlayer) {
    state.ledger.unshift({ at: absDay(state.season, state.day), amount, desc: `시즌 ${state.season} 배당금` });
  }
}

export function weeklyWages(state: GameState, ludus: Ludus): number {
  return [...ludus.gladiators, ...(ludus.youth ?? [])].reduce((s, id) => s + (state.gladiators[id]?.wage ?? 0), 0);
}

export function weeklyFood(state: GameState, ludus: Ludus): number {
  return ludus.gladiators.reduce((s, id) => s + E.foodPerWeek * getRace(state.gladiators[id].race).upkeepMult, 0);
}

/** 급료일 처리. 돈이 모자라면 주급이 높은 순으로 주고, 못 받은 검투사는 미납이 쌓인다 */
export function payday(state: GameState, ludus: Ludus): void {
  // 용병 파견 중인 검투사의 주급은 고용국이 낸다 (던전 원정은 루두스가 낸다)
  const roster = [...ludus.gladiators, ...(ludus.youth ?? [])].map((id) => state.gladiators[id]).filter((g) => g?.alive && (!g.deployment || g.deployment.kind === "dungeon"));
  if (!ludus.isPlayer) {
    // AI 루두스는 추상적으로 처리: 급료와 식비를 내고, 상금 외 부수입을 조금 번다
    ludus.treasury += ludus.tier === 1 ? 400 : 150;
    ludus.treasury -= weeklyWages(state, ludus) + weeklyFood(state, ludus);
    return;
  }
  const food = Math.round(weeklyFood(state, ludus) * modifierMult(state, "food"));
  addMoney(state, ludus, -food, "식비");
  const upkeep = weeklyUpkeep(ludus);
  if (upkeep) addMoney(state, ludus, -upkeep, "시설 유지비");
  const staffPay = staffWages(state, ludus);
  if (staffPay) addMoney(state, ludus, -staffPay, "식솔 급료");
  const unpaidNames: string[] = [];
  for (const g of [...roster].sort((a, b) => b.wage - a.wage)) {
    // 밀린 급료까지 같이 갚는다
    const owed = g.wage * (1 + g.unpaidWeeks);
    if (ludus.treasury >= owed) {
      addMoney(state, ludus, -owed, `${g.name} 주급${g.unpaidWeeks ? ` (밀린 ${g.unpaidWeeks}회 포함)` : ""}`);
      if (g.unpaidWeeks > 0) {
        // 밀린 급료를 받아도 떨어진 사기는 절반만 돌아온다
        const lost = BALANCE.morale.unpaid.slice(1, g.unpaidWeeks + 1).reduce((s, v) => s + v, 0);
        g.morale = Math.min(100, g.morale + lost / 2);
        g.unpaidWeeks = 0;
      } else g.morale = Math.min(100, g.morale + BALANCE.morale.paidBonus + ludus.facilities.barracks * 0.5);
    } else {
      g.unpaidWeeks++;
      const greed = g.traits.includes("greedy") ? 1.5 : 1;
      const loyal = 1 - g.hidden.loyalty / 40;
      const cold = g.race === "lizardfolk" ? 0.5 : 1;
      const drop = BALANCE.morale.unpaid[Math.min(g.unpaidWeeks, BALANCE.morale.unpaid.length - 1)] * greed * loyal * cold;
      g.morale = Math.max(0, g.morale - drop);
      unpaidNames.push(`${g.name}(${g.unpaidWeeks}회)`);
    }
  }
  if (unpaidNames.length) {
    pushMessage(state, {
      kind: "warning",
      title: "급료를 다 주지 못했습니다",
      body: `금고가 바닥나 급료를 못 받은 자들이 있습니다: ${unpaidNames.join(", ")}.\n\n세 번 넘게 밀리면 훈련을 거부하고, 네 번째부터는 도망치거나 들고일어날 수 있습니다.`,
    });
  }
}

/** 시장 가격 */
export function marketValue(g: Gladiator): number {
  const ca = computeCA(g);
  const potentialBonus = Math.max(0, g.hidden.pa - ca) * 4;
  const ageMult = { youth: 0.8, growth: 1.1, prime: 1.0, decline: 0.6 }[stageOf(g)];
  return Math.round((ca * ca * 0.12 + potentialBonus + g.popularity * 5 + 150) * ageMult / 10) * 10;
}

function stageOf(g: Gladiator) {
  const r = getRace(g.race).age;
  return g.age < r.debut ? "youth" : g.age <= r.growthEnd ? "growth" : g.age <= r.primeEnd ? "prime" : "decline";
}

export function rosterCapacity(ludus: Ludus): number {
  return capacityAt(ludus.facilities.barracks);
}

/** 무소속 검투사를 하나 만들어 명단에 올린다 */
function addFreeAgent(state: GameState, rng: Rng, nation: GameState["nation"], target: number, used: Set<string>, pow = false): void {
  const g = generateGladiator(rng, { id: `G${state.nextId++}`, nation, target, status: "free", usedNames: used });
  g.popularity = Math.min(80, g.popularity + rng.int(0, 20));
  g.wage = Math.round(computeWage(g, computeCA(g)) * (pow ? 0.5 : 1) * modifierMult(state, "market") * rulesOfNation(state.nation).marketMult);
  if (pow) {
    g.morale = rng.int(15, 35);
    if (!g.traits.includes("homesick")) g.traits.push("homesick");
  }
  state.gladiators[g.id] = g;
  state.freeAgents.push(g.id);
}

const isOwned = (state: GameState, id: string) => Object.values(state.ludi).some((l) => l.gladiators.includes(id) || l.youth?.includes(id));

/** 무소속 검투사 명단 새로 고침 (이적 시장이 열릴 때). 실력 폭이 넓고, 협상해야 온다 */
export function refreshFreeAgents(state: GameState, rng: Rng): void {
  for (const id of state.freeAgents ?? []) if (!isOwned(state, id)) delete state.gladiators[id];
  state.freeAgents = [];
  const used = new Set(Object.values(state.gladiators).map((x) => x.name));
  const base = state.ludi[state.playerLudusId].tier === 1 ? 9 : 7;
  // 전쟁 포로: 패전국 출신이 싼 주급에 풀린다
  if (state.powPending) {
    for (let i = 0; i < 4; i++) addFreeAgent(state, rng, state.powPending, base + rng.range(-1, 2), used, true);
    state.powPending = undefined;
  }
  for (let i = 0; i < 24; i++) addFreeAgent(state, rng, state.nation, base + rng.range(-3, 6), used);
}

/** 매주: 몇 명은 다른 데로 가고 몇 명이 새로 풀린다 */
export function weeklyFreeAgents(state: GameState, rng: Rng): void {
  const used = new Set(Object.values(state.gladiators).map((x) => x.name));
  for (let i = 0; i < 2 && state.freeAgents.length > 12; i++) {
    const id = state.freeAgents.splice(rng.int(0, state.freeAgents.length - 1), 1)[0];
    if (!isOwned(state, id)) delete state.gladiators[id];
  }
  const base = state.ludi[state.playerLudusId].tier === 1 ? 9 : 7;
  for (let i = 0; i < 2; i++) addFreeAgent(state, rng, state.nation, base + rng.range(-3, 6), used);
}

/** 방출: 무소속이 된다 */
export function releaseGladiator(state: GameState, id: string): void {
  const player = state.ludi[state.playerLudusId];
  const g = state.gladiators[id];
  player.gladiators = player.gladiators.filter((x) => x !== id);
  g.contract = undefined;
  state.freeAgents.push(id);
  logCareer(state, g, `${player.name}에서 방출됐습니다`, "other");
}

/** 우리 명성에서 이 검투사가 원하는 주급 */
export function freeAgentDemand(state: GameState, g: Gladiator): Demand {
  return demandFor(gladiatorStanding(g), g.wage, ludusReputation(state, state.ludi[state.playerLudusId]));
}

/** 계약금 = 제안 주급 4주치 */
export const signingFee = (offer: number) => offer * 4;

export function signFreeAgent(state: GameState, id: string, offer: number, seasons: number, roll: number): string | null {
  const player = state.ludi[state.playerLudusId];
  const g = state.gladiators[id];
  if (!state.freeAgents.includes(id)) return "이미 다른 곳과 계약했습니다";
  if (player.gladiators.length >= rosterCapacity(player)) return "막사에 빈자리가 없습니다";
  const d = freeAgentDemand(state, g);
  if (d.refuse) return `${g.name}: ${d.reason}`;
  if (player.treasury < signingFee(offer)) return "계약금이 모자랍니다";
  if (!acceptsOffer(offer, d, roll)) {
    state.freeAgents = state.freeAgents.filter((x) => x !== id);
    return `${josa(g.name, "이/가")} 제안을 거절하고 다른 루두스로 갔습니다 (바라던 주급 ${d.wage} D)`;
  }
  addMoney(state, player, -signingFee(offer), `${g.name} 계약금`);
  g.wage = offer;
  g.contract = { untilSeason: state.season + seasons - 1, clauses: [] };
  g.status = "free";
  state.freeAgents = state.freeAgents.filter((x) => x !== id);
  player.gladiators.push(id);
  seasonRecord(state, g);
  logCareer(state, g, `자유민으로 ${josa(player.name, "과/와")} ${seasons}시즌 계약했습니다 (주급 ${offer} D)`, "join");
  return null;
}
