// 금고: 배당금, 급료일, 장부, 시장 가격
import { absDay } from "./calendar";
import { BALANCE, getRace } from "./data";
import { logCareer, seasonRecord } from "./career";
import { computeCA } from "./gladiator";
import { generateGladiator } from "./generators/gladiator";
import { capacityAt, weeklyUpkeep } from "./facilities";
import { modifierMult } from "./modifiers";
import { buyMult, ratingOf, sellMult, staffWages } from "./staff";
import { rulesOfNation } from "./nationRules";
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

export function tierRanking(state: GameState, tier: 1 | 2): Ludus[] {
  return Object.values(state.ludi).filter((l) => l.tier === tier).sort((a, b) => b.rp - a.rp);
}

/** 시즌 배당금 지급. coef가 없으면 현재 순위로 계산 */
export function payDividend(state: GameState, ludus: Ludus, coef?: number): void {
  const rank = tierRanking(state, ludus.tier).findIndex((l) => l.id === ludus.id) + 1;
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
  // 파견 중인 검투사의 주급은 고용국이 낸다
  const roster = [...ludus.gladiators, ...(ludus.youth ?? [])].map((id) => state.gladiators[id]).filter((g) => g?.alive && !g.deployment);
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
      title: "급료를 다 주지 못했다",
      body: `금고가 바닥나 급료를 못 받은 자들이 있다: ${unpaidNames.join(", ")}.\n\n세 번 넘게 밀리면 훈련을 거부하고, 네 번째부터는 도망치거나 들고일어날 수 있다.`,
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

/** 노예 경매 매물 새로 고침 */
export function refreshMarket(state: GameState, rng: Rng): void {
  for (const l of state.market) {
    const g = state.gladiators[l.gladiatorId];
    if (g && !Object.values(state.ludi).some((x) => x.gladiators.includes(g.id))) delete state.gladiators[g.id];
  }
  state.market = [];
  const player = state.ludi[state.playerLudusId];
  const base = player.tier === 1 ? 9 : 6.5;
  // 전쟁 포로: 패전국 출신이 싼값에 나온다
  if (state.powPending) {
    const pow = state.powPending;
    state.powPending = undefined;
    for (let i = 0; i < 4; i++) {
      const g = generateGladiator(rng, { id: `G${state.nextId++}`, nation: pow, target: base + rng.range(-1, 2), status: "slave" });
      g.morale = rng.int(15, 35);
      if (!g.traits.includes("homesick")) g.traits.push("homesick");
      state.gladiators[g.id] = g;
      state.market.push({ gladiatorId: g.id, price: Math.round(marketValue(g) * 0.5 / 10) * 10 });
    }
  }
  for (let i = 0; i < E.marketSize; i++) {
    const g = generateGladiator(rng, {
      id: `G${state.nextId++}`,
      nation: state.nation,
      target: base + rng.range(-2, 2.5),
      status: "slave",
      usedNames: new Set(Object.values(state.gladiators).map((x) => x.name)),
    });
    state.gladiators[g.id] = g;
    state.market.push({ gladiatorId: g.id, price: Math.round(marketValue(g) * rng.range(0.85, 1.2) * modifierMult(state, "market") * rulesOfNation(state.nation).marketMult / 10) * 10 });
  }
}

export function rosterCapacity(ludus: Ludus): number {
  return capacityAt(ludus.facilities.barracks);
}

/** 중개인 실력을 반영한 실제 구매가 */
export function buyPrice(state: GameState, price: number): number {
  return Math.round(price * buyMult(ratingOf(state, state.ludi[state.playerLudusId], "agent")) / 10) * 10;
}

export function buyFromMarket(state: GameState, gladiatorId: string): string | null {
  const player = state.ludi[state.playerLudusId];
  const listing = state.market.find((m) => m.gladiatorId === gladiatorId);
  if (!listing) return "이미 팔린 매물이다.";
  const price = buyPrice(state, listing.price);
  if (player.treasury < price) return "금고가 모자란다.";
  if (player.gladiators.length >= rosterCapacity(player)) return "막사에 빈자리가 없다.";
  addMoney(state, player, -price, `${state.gladiators[gladiatorId].name} 구매`);
  player.gladiators.push(gladiatorId);
  state.market = state.market.filter((m) => m !== listing);
  const g = state.gladiators[gladiatorId];
  seasonRecord(state, g);
  logCareer(state, g, `노예 경매에서 ${player.name}에 팔려 왔다 (${price} D)`, "join");
  return null;
}

export function sellGladiator(state: GameState, gladiatorId: string): string | null {
  const player = state.ludi[state.playerLudusId];
  const g = state.gladiators[gladiatorId];
  if (g.status !== "slave") return "자유민은 팔 수 없다.";
  const price = Math.round(marketValue(g) * E.sellShare * sellMult(ratingOf(state, player, "agent")) / 10) * 10;
  player.gladiators = player.gladiators.filter((x) => x !== gladiatorId);
  player.treasury += price;
  state.ledger.unshift({ at: absDay(state.season, state.day), amount: price, desc: `${g.name} 판매` });
  logCareer(state, g, `${player.name}에서 노예 상인에게 팔려 나갔다 (${price} D)`, "other");
  return null;
}
