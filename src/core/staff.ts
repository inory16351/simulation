// 식솔: 독토레(교관), 메디쿠스(의원), 정찰꾼, 중개인
import { NAMES, getNation } from "./data";
import { acceptsOffer, demandFor, ludusReputation, staffStanding, type Demand } from "./reputation";
import { josa } from "./josa";
import type { Rng } from "./rng";
import type { GameState, Gladiator, Ludus, RaceId, Staff, StaffRole } from "./types";

export const ROLE_NAMES: Record<StaffRole, string> = {
  doctore: "독토레", medicus: "메디쿠스", scout: "정찰꾼", agent: "중개인",
};
export const ROLE_DESC: Record<StaffRole, string> = {
  doctore: "수석 교관. 검투사가 훈련과 실전에서 더 빨리 크고, 교범도 더 빨리 익힙니다",
  medicus: "의원. 부상을 빨리 낫게 하고, 사투에서 목숨을 건질 확률을 높입니다",
  scout: "정찰꾼. 검투사의 숨은 그릇(잠재력)을 더 정확히 봅니다",
  agent: "중개인. 이적료를 깎아 사고, 팔 때는 더 받아 냅니다",
};

/** 역할마다 한 명만 둔다 */
export const ROLES: StaffRole[] = ["doctore", "medicus", "scout", "agent"];

export function staffOf(state: GameState, ludus: Ludus, role: StaffRole): Staff | undefined {
  return (ludus.staff ?? []).map((id) => state.staff?.[id]).find((s) => s?.role === role);
}

export const ratingOf = (state: GameState, ludus: Ludus | undefined, role: StaffRole) =>
  ludus ? staffOf(state, ludus, role)?.rating ?? 0 : 0;

/** 독토레: 성장 배율 (없으면 0.85, 능력 20이면 1.15) */
export const coachMult = (rating: number) => 0.85 + rating * 0.015;
/** 메디쿠스: 부상 회복 배율 */
export const healMult = (rating: number) => 1 + rating * 0.03;
/** 중개인: 구매가 배율 */
export const buyMult = (rating: number) => 1 - rating * 0.01;
export const sellMult = (rating: number) => 1 + rating * 0.01;

/** 정찰꾼 실력에 따른 잠재력 추정 범위 (±CA) */
export function paSpread(state: GameState, g: Gladiator): number {
  const player = state.ludi[state.playerLudusId];
  const r = ratingOf(state, player, "scout");
  const mine = player.gladiators.includes(g.id);
  return mine ? Math.max(3, 30 - r * 1.35) : Math.max(5, 45 - r * 2);
}

export function staffWages(state: GameState, ludus: Ludus): number {
  return (ludus.staff ?? []).reduce((s, id) => s + (state.staff?.[id]?.wage ?? 0), 0);
}

function makeStaff(state: GameState, rng: Rng, role: StaffRole, rating: number): Staff {
  const races = getNation(state.nation).races;
  const race = rng.weighted<RaceId>(races);
  const female = new Set(NAMES.female);
  const gender = rng.chance(0.25) ? "f" : "m";
  const pool = NAMES.gladiator[race].filter((n) => female.has(n) === (gender === "f"));
  const s: Staff = {
    id: `S${state.nextId++}`,
    name: `${rng.pick(pool)} ${rng.pick(NAMES.surname[race])}`,
    race, gender,
    age: rng.int(30, 60),
    role, rating,
    wage: Math.round(10 + rating * rating * 0.6),
    portraitSeed: rng.int(0, 1_000_000),
  };
  state.staff[s.id] = s;
  return s;
}

/** 식솔 후보 새로 고침 (이적 시장이 열릴 때) */
export function refreshStaffMarket(state: GameState, rng: Rng): void {
  for (const id of state.staffMarket) delete state.staff[id];
  state.staffMarket = [];
  for (const role of ROLES) {
    for (let i = 0; i < 9; i++) {
      const rating = Math.max(1, Math.min(20, Math.round(rng.range(2, 20))));
      state.staffMarket.push(makeStaff(state, rng, role, rating).id);
    }
  }
}

/** 새 게임: 낡은 독토레 한 명으로 시작 */
export function startingStaff(state: GameState, rng: Rng, ludus: Ludus): void {
  ludus.staff.push(makeStaff(state, rng, "doctore", 6).id);
}

/** 우리 명성에서 이 사람이 원하는 조건 */
export function staffDemand(state: GameState, s: Staff): Demand {
  return demandFor(staffStanding(s), s.wage, ludusReputation(state, state.ludi[state.playerLudusId]));
}

/** 협상해서 고용한다. 제안 주급이 모자라면 거절당하고, 거절하면 이번 시즌엔 다시 안 만난다 */
export function hireStaff(state: GameState, id: string, offer: number, roll: number): string | null {
  const player = state.ludi[state.playerLudusId];
  const s = state.staff[id];
  if (!state.staffMarket.includes(id)) return "이미 떠난 사람입니다";
  if (staffOf(state, player, s.role)) return `${ROLE_NAMES[s.role]} 자리가 이미 차 있습니다. 먼저 내보내십시오`;
  const d = staffDemand(state, s);
  if (d.refuse) return `${s.name}: ${d.reason}`;
  if (!acceptsOffer(offer, d, roll)) {
    state.staffMarket = state.staffMarket.filter((x) => x !== id);
    return `${josa(s.name, "이/가")} 제안을 거절하고 떠났습니다 (바라던 주급 ${d.wage} D)`;
  }
  s.wage = offer;
  state.staffMarket = state.staffMarket.filter((x) => x !== id);
  player.staff.push(id);
  return null;
}

export function fireStaff(state: GameState, id: string): void {
  const player = state.ludi[state.playerLudusId];
  player.staff = player.staff.filter((x) => x !== id);
  delete state.staff[id];
}
