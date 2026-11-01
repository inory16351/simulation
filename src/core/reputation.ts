// 루두스 명성과 영입 의사. FM처럼 명성이 낮으면 좋은 사람이 오지 않거나 돈을 더 요구한다.
import { computeCA } from "./gladiator";
import type { GameState, Gladiator, Ludus, Staff } from "./types";

/** 루두스 명성 0~100: 리그 단계, 리그 순위, 스타들의 인기, 시설, 우승 기록 */
export function ludusReputation(state: GameState, ludus: Ludus): number {
  const same = Object.values(state.ludi).filter((l) => l.tier === ludus.tier && l.nation === ludus.nation).sort((a, b) => b.rp - a.rp);
  const rank = same.findIndex((l) => l.id === ludus.id) + 1 || same.length;
  const base = ludus.tier === 1 ? 45 : 18;
  const rankBonus = Math.max(0, (same.length - rank) * 1.6);
  const stars = ludus.gladiators.map((id) => state.gladiators[id]).filter((g) => g?.alive).map((g) => g.popularity).sort((a, b) => b - a).slice(0, 3);
  const starBonus = stars.length ? (stars.reduce((s, v) => s + v, 0) / stars.length) * 0.2 : 0;
  const facilityBonus = Object.values(ludus.facilities).reduce((s, v) => s + v, 0) * 0.6;
  return Math.round(Math.max(0, Math.min(100, base + rankBonus + starBonus + facilityBonus + (ludus.titles ?? 0) * 6)));
}

export const REPUTATION_NAMES = (r: number) =>
  r >= 80 ? "대륙의 명문" : r >= 60 ? "이름난 루두스" : r >= 40 ? "알려진 루두스" : r >= 20 ? "지방 루두스" : "무명 루두스";

/** 사람의 '격' 0~100. 루두스 명성과 비교해 올지 말지, 얼마를 원할지 정한다 */
export const staffStanding = (s: Staff) => s.rating * 5;
export const gladiatorStanding = (g: Gladiator) => Math.min(100, computeCA(g) * 0.55 + g.popularity * 0.35);

export interface Demand {
  /** 아예 오지 않는다 */
  refuse: boolean;
  /** 원하는 주급 */
  wage: number;
  reason: string;
}

/**
 * 격이 명성보다 높을수록 더 많이 요구하고, 너무 차이 나면 거절한다.
 * 명성이 격보다 높으면 조금 덜 받고도 온다.
 */
export function demandFor(standing: number, baseWage: number, reputation: number): Demand {
  const gap = standing - reputation;
  if (gap > 35) return { refuse: true, wage: 0, reason: "이런 무명 루두스에는 관심이 없다" };
  const mult = gap > 0 ? 1 + gap / 30 : Math.max(0.85, 1 + gap / 120);
  const wage = Math.round(baseWage * mult);
  const reason = gap > 20 ? "가긴 하겠지만 그만한 값을 달라" : gap > 0 ? "조금 더 쳐 달라" : "명문에 들어가는 거라면 기꺼이";
  return { refuse: false, wage, reason };
}

/** 제안을 받아들이는가: 원하는 주급 이상이면 수락, 조금 모자라면 확률 */
export function acceptsOffer(offer: number, demand: Demand, roll: number): boolean {
  if (demand.refuse) return false;
  if (offer >= demand.wage) return true;
  const short = (demand.wage - offer) / demand.wage;
  return short < 0.1 && roll < 1 - short * 8;
}
