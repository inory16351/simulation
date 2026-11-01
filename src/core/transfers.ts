// 이적: FM처럼 먼저 루두스와 이적료를 합의하고, 그다음 검투사와 개인 계약을 맺는다.
import { absDay, dateLabel, fromAbs } from "./calendar";
import { ludusOf, logCareer, seasonRecord } from "./career";
import { getNation } from "./data";
import { addMoney, marketValue, rosterCapacity } from "./economy";
import { computeWage } from "./generators/gladiator";
import { computeCA } from "./gladiator";
import { josa } from "./josa";
import { modifierMult } from "./modifiers";
import { rulesOfNation } from "./nationRules";
import { acceptsOffer, demandFor, gladiatorStanding, ludusReputation, type Demand } from "./reputation";
import type { Rng } from "./rng";
import { pushMessage } from "./state";
import { buyMult, ratingOf, sellMult } from "./staff";
import type { GameState, Gladiator, Id, Ludus } from "./types";

export interface Deal {
  id: Id;
  gladiatorId: Id;
  fromLudus: Id;
  fee: number;
  /** 개인 계약 협상 기한 */
  expiresAt: number;
}

export interface Offer {
  id: Id;
  fromLudus: Id;
  gladiatorId: Id;
  fee: number;
  expiresAt: number;
}

export interface TransferState {
  deals: Deal[];
  offers: Offer[];
}

export const transferOf = (s: GameState): TransferState => (s.transfer ??= { deals: [], offers: [] });

const now = (s: GameState) => absDay(s.season, s.day);

/** 이적 시장 기간: 서리달 1~14일, 꽃달 22일 ~ 불꽃달 7일 */
export const inWindow = (s: GameState) => s.day <= 14 || (s.day >= 78 && s.day <= 91);
export const windowLabel = "서리달 1~14일, 꽃달 22일 ~ 불꽃달 7일";

/** 루두스가 부르는 값: 주전일수록, 육성형 루두스일수록 비싸다 */
export function askingPrice(s: GameState, g: Gladiator, owner: Ludus): number {
  const roster = owner.gladiators.map((id) => s.gladiators[id]).filter((x) => x?.alive).sort((a, b) => computeCA(b) - computeCA(a));
  const key = roster.slice(0, 3).some((x) => x.id === g.id);
  const p = owner.aiPersonality ?? "stable";
  const persona = { merchant: 0.9, developer: 1.3, aggressive: 1.1, stable: 1, grudge: 1.15 }[p];
  const v = marketValue(g) * (key ? 1.5 : 1) * persona * modifierMult(s, "market") * rulesOfNation(owner.nation).marketMult;
  return Math.round(v / 50) * 50;
}

export type BidResult = { status: "accepted"; deal: Deal } | { status: "countered"; asking: number } | { status: "rejected"; reason: string };

/** 이적료 제안 */
export function bid(s: GameState, gladiatorId: Id, fee: number): BidResult {
  const g = s.gladiators[gladiatorId];
  const owner = ludusOf(s, gladiatorId);
  if (!owner || owner.isPlayer) return { status: "rejected", reason: "소속 루두스가 없다" };
  if (!inWindow(s)) return { status: "rejected", reason: `이적 시장 기간이 아니다 (${windowLabel})` };
  if (g.deployment) return { status: "rejected", reason: "전장에 나가 있다" };
  if (fee > s.ludi[s.playerLudusId].treasury) return { status: "rejected", reason: "금고에 그만한 돈이 없다" };
  // 중개인이 있으면 값을 조금 깎는다
  const asking = Math.round(askingPrice(s, g, owner) * buyMult(ratingOf(s, s.ludi[s.playerLudusId], "agent")) / 50) * 50;
  if (fee >= asking) {
    const deal: Deal = { id: `T${s.nextId++}`, gladiatorId, fromLudus: owner.id, fee, expiresAt: now(s) + 7 };
    transferOf(s).deals.push(deal);
    pushMessage(s, { kind: "info", title: `이적료 합의: ${g.name}`, body: `${josa(owner.name, "이/가")} ${fee.toLocaleString()} D에 동의했다.\n${dateLabel(fromAbs(deal.expiresAt).day)}까지 ${g.name}과(와) 개인 계약을 맺어라.` });
    return { status: "accepted", deal };
  }
  if (fee >= asking * 0.7) return { status: "countered", asking };
  return { status: "rejected", reason: `${josa(owner.name, "이/가")} 코웃음을 쳤다. 이 정도로는 안 판다` };
}

/** 개인 계약 조건: 지금 받는 주급보다 많이, 그리고 우리 명성에 맞춰 */
export function transferDemand(s: GameState, g: Gladiator): Demand {
  const base = Math.max(Math.round(g.wage * 1.2), computeWage(g, computeCA(g)));
  return demandFor(gladiatorStanding(g), base, ludusReputation(s, s.ludi[s.playerLudusId]));
}

export function completeTransfer(s: GameState, dealId: Id, wage: number, seasons: number, roll: number): string | null {
  const t = transferOf(s);
  const deal = t.deals.find((d) => d.id === dealId);
  if (!deal) return "합의가 사라졌다";
  const me = s.ludi[s.playerLudusId];
  const g = s.gladiators[deal.gladiatorId];
  const from = s.ludi[deal.fromLudus];
  if (me.gladiators.length >= rosterCapacity(me)) return "막사에 빈자리가 없다";
  if (me.treasury < deal.fee) return "이적료를 낼 돈이 없다";
  const d = transferDemand(s, g);
  if (d.refuse || !acceptsOffer(wage, d, roll)) {
    t.deals = t.deals.filter((x) => x !== deal);
    return `${josa(g.name, "이/가")} 개인 조건에 동의하지 않아 이적이 무산됐다${d.refuse ? ` (${d.reason})` : ` (원한 주급 ${d.wage} D)`}`;
  }
  addMoney(s, me, -deal.fee, `${g.name} 이적료`);
  from.treasury += deal.fee;
  from.gladiators = from.gladiators.filter((x) => x !== g.id);
  me.gladiators.push(g.id);
  g.wage = wage;
  g.status = "free";
  g.contract = { untilSeason: s.season + seasons - 1, clauses: [] };
  g.morale = Math.min(100, g.morale + 8);
  t.deals = t.deals.filter((x) => x !== deal);
  seasonRecord(s, g);
  logCareer(s, g, `${from.name}에서 ${me.name}으로 이적했다 (${deal.fee.toLocaleString()} D)`, "join");
  return null;
}

export function cancelDeal(s: GameState, dealId: Id): void {
  const t = transferOf(s);
  t.deals = t.deals.filter((d) => d.id !== dealId);
}

/** 매일: 기한 지난 합의·제안 정리, 이적 시장 기간에는 AI가 우리 검투사에 제안을 보낸다 */
export function dailyTransfers(s: GameState, rng: Rng): void {
  const t = transferOf(s);
  const n = now(s);
  t.deals = t.deals.filter((d) => d.expiresAt >= n);
  t.offers = t.offers.filter((o) => o.expiresAt >= n);
  if (!inWindow(s) || !rng.chance(0.08)) return;
  const me = s.ludi[s.playerLudusId];
  const mine = me.gladiators.map((id) => s.gladiators[id]).filter((g) => g?.alive && !g.deployment);
  if (mine.length <= 3) return;
  // 잘하는 검투사일수록 눈에 띈다
  const w: Record<string, number> = {};
  for (const g of mine) w[g.id] = Math.max(1, computeCA(g) - 30) + g.popularity;
  const target = s.gladiators[rng.weighted(w)];
  const value = marketValue(target);
  const buyers = Object.values(s.ludi).filter((l) => !l.isPlayer && l.nation === s.nation && l.treasury > value * 1.2);
  if (!buyers.length || t.offers.some((o) => o.gladiatorId === target.id)) return;
  const buyer = rng.pick(buyers);
  const fee = Math.round(value * rng.range(0.8, 1.35) / 50) * 50;
  t.offers.push({ id: `O${s.nextId++}`, fromLudus: buyer.id, gladiatorId: target.id, fee, expiresAt: n + 5 });
  pushMessage(s, {
    kind: "info",
    title: `이적 제안: ${target.name}`,
    body: `${josa(buyer.name, "이/가")} ${target.name}에게 ${fee.toLocaleString()} D를 제시했다.\n이적 시장 화면에서 받을지 정하라. 5일 뒤에는 제안이 사라진다.`,
  });
}

/** 받은 제안 수락: 검투사를 넘기고 이적료를 받는다 (중개인이 있으면 조금 더 받아 낸다) */
export function acceptOffer(s: GameState, offerId: Id): string | null {
  const t = transferOf(s);
  const o = t.offers.find((x) => x.id === offerId);
  if (!o) return "제안이 사라졌다";
  const me = s.ludi[s.playerLudusId];
  const buyer = s.ludi[o.fromLudus];
  const g = s.gladiators[o.gladiatorId];
  const fee = Math.round(o.fee * sellMult(ratingOf(s, me, "agent")));
  me.gladiators = me.gladiators.filter((x) => x !== g.id);
  buyer.gladiators.push(g.id);
  buyer.treasury -= fee;
  addMoney(s, me, fee, `${g.name} 이적료 수입`);
  t.offers = t.offers.filter((x) => x !== o);
  logCareer(s, g, `${me.name}에서 ${buyer.name}으로 팔려 갔다 (${fee.toLocaleString()} D)`, "other");
  for (const id of me.gladiators) s.gladiators[id].morale = Math.max(0, s.gladiators[id].morale - (g.popularity > 40 ? 4 : 1));
  return null;
}

export function rejectOffer(s: GameState, offerId: Id): void {
  const t = transferOf(s);
  t.offers = t.offers.filter((x) => x.id !== offerId);
}

export const nationName = (id: Ludus["nation"]) => getNation(id).short;
