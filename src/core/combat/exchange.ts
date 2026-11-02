// 한 합의 수 싸움: 방향(상·중·하) × 동작 가위바위보, 버릇, 예측, 페인트 (docs/15_combat_v2.md 2~3장).
// sim.ts가 근접 공격마다 부른다. 숫자는 초안이다.
import type { Rng } from "../rng";
import type { Gladiator, WeaponClassId } from "../types";
import type { Focus } from "../tactics";

/** 0 상단, 1 중단, 2 하단, 3 사선 (천재성 18 변칙, 막을 수 없다) */
export type Dir = 0 | 1 | 2 | 3;
/** 0 베기, 1 찌르기, 2 내려치기 */
export type Motion = 0 | 1 | 2;
/** 0 막기, 1 흘리기, 2 피하기 */
export type Guard = 0 | 1 | 2;

export const DIR_NAMES = ["상단", "중단", "하단", "사선"] as const;
export const MOTION_NAMES = ["베기", "찌르기", "내려치기"] as const;
export const GUARD_NAMES = ["막기", "흘리기", "피하기"] as const;

/**
 * 동작 상성: +1 공격 유리, -1 방어 유리, 0 중립.
 * 막기 > 베기 > 피하기 > 내려치기 > 흘리기 > 찌르기 > 막기
 */
export const ADV: readonly (readonly number[])[] = [
  [-1, 0, 1], // 베기: 막기에 걸리고, 피하는 몸을 잡는다
  [1, -1, 0], // 찌르기: 방패 틈을 찌르고, 흘리기에 빗겨난다
  [0, 1, -1], // 내려치기: 흘릴 수 없고, 피하면 크게 헛친다
];
/** 이 동작을 이기는 방어 (ADV = -1) */
export const counterGuard = (m: Motion): Guard => m as number as Guard;
/** 이 방어를 이기는 동작 (ADV = +1) */
export const beatGuard = (g: Guard): Motion => ((g + 1) % 3) as Motion;

/** 무기군별 동작 친화 [베기, 찌르기, 내려치기]. 1보다 크면 잘 쓰고 작으면 못 쓴다 */
export const MOTION_AFFINITY: Record<WeaponClassId, [number, number, number]> = {
  sword: [1.2, 1.0, 0.5],
  greatsword: [1.2, 0.4, 1.1],
  axe: [0.6, 0.2, 1.4],
  mace: [0.4, 0.3, 1.5],
  spear: [0.3, 1.6, 0.4],
  trident_net: [0.2, 1.6, 0.3],
  dagger: [0.6, 1.5, 0.2],
  throwing: [0.5, 1.2, 0.3],
  shield: [0.3, 1.0, 1.0],
  unarmed: [0.3, 1.3, 0.7],
  focus: [0.6, 0.8, 0.8],
};
/** 친화도에 따른 명중 보정 */
export const affinityHit = (w: WeaponClassId, m: Motion) => (MOTION_AFFINITY[w][m] - 1) * 0.08;

/** 기술마다 정해진 동작 (없으면 버릇대로) */
export const SKILL_MOTION: Record<string, Motion> = {
  "skill.power_strike": 2,
  "skill.flurry": 0,
  "skill.armor_break": 2,
  "skill.pierce": 1,
  "skill.execute": 2,
  "skill.shield_bash": 1,
  "skill.charge": 1,
  "skill.horn_charge": 1,
  "skill.venom_bite": 1,
  "skill.pounce": 2,
};

/** 9칸 분포 (방향 3 × 동작/방어 3), 칸 = 방향 × 3 + 동작 */
export type Grid = number[];
export const cell = (d: number, m: number) => d * 3 + m;

export interface Habits {
  atk: Grid;
  def: Grid;
}

const FOCUS_DIR: Record<Focus, [number, number, number]> = {
  spread: [1, 1, 1], head: [2, 0.7, 0.5], body: [0.7, 2, 0.7], legs: [0.5, 0.7, 2], arm: [1.4, 1.4, 0.5],
};

/** id로 고정된 0~1 난수열 */
function seeded(id: string, salt: number): () => number {
  let h = salt | 0;
  for (const ch of id) h = (Math.imul(h, 31) + ch.charCodeAt(0)) | 0;
  return () => {
    h = (Math.imul(h, 1103515245) + 12345) | 0;
    return ((h >>> 8) & 0xffff) / 0x10000;
  };
}

const normalize = (g: Grid): Grid => {
  const s = g.reduce((a, b) => a + b, 0) || 1;
  return g.map((v) => v / s);
};

/**
 * 검투사의 버릇. 저장하지 않고 id·스타일·장비·공격성·교범에서 매번 같은 값으로 만든다.
 * 공격성이 높을수록 분포가 뾰족하다 (한두 칸에 몰려 읽히기 쉽다).
 */
export function habitsOf(g: Gladiator, weapon: WeaponClassId, opt: { focus: Focus; shield: boolean; armorAgiPenalty: number; agg: number; genius: number; blk: number; eva: number }): Habits {
  const r = seeded(g.id, 7);
  const aff = MOTION_AFFINITY[weapon];
  const fdir = FOCUS_DIR[opt.focus];
  // 공격성은 버릇을 뾰족하게, 천재성은 레퍼토리를 넓혀 고르게 만든다
  const peak = Math.max(0.4, 1.6 + (opt.agg - 10) * 0.1 - (opt.genius - 10) * 0.06);
  const atk: Grid = [];
  for (let d = 0; d < 3; d++) {
    for (let m = 0; m < 3; m++) atk.push(Math.pow(fdir[d] * aff[m] * (0.4 + r() * 1.2), peak));
  }
  const guardW = [
    (opt.shield ? 1.6 : 0.6) * (0.6 + opt.blk / 20),
    weapon === "sword" || weapon === "dagger" || weapon === "spear" ? 1.2 : 0.9,
    Math.max(0.2, 0.9 + (opt.armorAgiPenalty === 0 ? 0.3 : -0.15 * opt.armorAgiPenalty)) * (0.6 + opt.eva / 20),
  ];
  const ddir = [0.8 + r() * 0.6, 0.8 + r() * 0.6, 0.8 + r() * 0.6];
  const def: Grid = [];
  for (let d = 0; d < 3; d++) for (let k = 0; k < 3; k++) def.push(ddir[d] * guardW[k] * (0.6 + r() * 0.8));
  return { atk: normalize(atk), def: normalize(def) };
}

/** 한쪽이 상대에 대해 쌓는 관측 (이번 경기) */
export interface ReadBook {
  /** 상대 공격 칸 관측 (가중) */
  atk: Grid;
  /** 상대 방어 동작 관측 (가중) */
  guard: number[];
}
export const emptyBook = (): ReadBook => ({ atk: Array(9).fill(0), guard: [0, 0, 0] });

/** 사람마다 경기 안에서 쓰는 수 싸움 능력치 */
export interface Mind {
  read: number;
  feint: number;
  genius: number;
  adapt: number;
  dec: number;
  rea: number;
  comp: number;
  /** 이번 경기 컨디션 노이즈 (일관성) */
  form: number;
  /** 적응력: 경기가 길어질수록 더 정확히 읽는다 (sim이 합마다 갱신) */
  late: number;
}

const pickWeighted = (rng: Rng, w: number[]): number => {
  const s = w.reduce((a, b) => a + b, 0);
  let x = rng.next() * s;
  for (let i = 0; i < w.length; i++) { x -= w[i]; if (x <= 0) return i; }
  return w.length - 1;
};

export interface AttackPlan {
  dir: Dir;
  motion: Motion;
  feint: "ok" | "fail" | undefined;
  improv: boolean;
  /** 페인트가 보여 준 가짜 방향 */
  shown?: Dir;
}

/**
 * 공격자의 선택. 버릇에서 뽑되, 판단력이 상대가 막을 곳을 피해 비틀고, 천재성이 즉흥 수를 낸다.
 * `shownBook` = 상대가 지금까지 나에 대해 본 것 (내가 보여 준 버릇), `myBook` = 내가 상대 방어를 본 것.
 */
export function planAttack(rng: Rng, me: Mind, habits: Habits, shownBook: ReadBook, myBook: ReadBook, weapon: WeaponClassId, fixedMotion: Motion | undefined, oppRead: number, feintBias: number): AttackPlan {
  let c = pickWeighted(rng, habits.atk);
  let dir = Math.floor(c / 3) as Dir;
  let motion = (fixedMotion ?? (c % 3)) as Motion;
  let improv = false;

  if (rng.chance(me.genius * 0.005)) {
    // 즉흥: 상대가 거의 못 본 칸
    improv = true;
    const seen = shownBook.atk.map((v) => v + rng.next() * 0.5);
    c = seen.indexOf(Math.min(...seen));
    dir = Math.floor(c / 3) as Dir;
    if (fixedMotion === undefined) motion = (c % 3) as Motion;
    if (me.genius >= 18 && rng.chance(0.3)) dir = 3;
  } else if (rng.chance(Math.max(0.05, Math.min(0.5, 0.15 + (me.dec - oppRead) * 0.02 + me.feint * 0.01)))) {
    // 판단: 많이 보여 준 방향은 막힐 테니 다른 방향으로, 상대가 즐겨 쓰는 방어를 이기는 동작으로.
    // 상대 예측력이 높으면 비튼 수까지 읽히므로 시도 자체가 줄어든다
    const shownDir = [0, 1, 2].map((d) => shownBook.atk[cell(d, 0)] + shownBook.atk[cell(d, 1)] + shownBook.atk[cell(d, 2)]);
    const total = shownDir.reduce((a, b) => a + b, 0);
    const top = shownDir.indexOf(Math.max(...shownDir));
    if (total >= 2 && dir === top) {
      const others = [0, 1, 2].filter((d) => d !== top);
      dir = others[pickWeighted(rng, others.map((d) => shownDir[d] + 0.5))] as Dir;
    }
    const gTotal = myBook.guard.reduce((a, b) => a + b, 0);
    if (fixedMotion === undefined && gTotal >= 2) {
      const fav = beatGuard(myBook.guard.indexOf(Math.max(...myBook.guard)) as Guard);
      if (MOTION_AFFINITY[weapon][fav] >= 0.5) motion = fav;
    }
  }

  let feint: AttackPlan["feint"];
  let shown: Dir | undefined;
  if (dir !== 3 && rng.chance(me.feint * 0.012 + feintBias)) {
    const p = Math.max(0.1, Math.min(0.9, 0.45 + (me.feint - oppRead) * 0.04 + (me.genius >= 10 ? 0.08 : 0)));
    feint = rng.chance(p) ? "ok" : "fail";
    if (feint === "ok") {
      const others = [0, 1, 2].filter((d) => d !== dir);
      shown = others[rng.int(0, 1)] as Dir;
    }
  }
  return { dir, motion, feint, improv, shown };
}

export interface GuardPlan {
  dir: Dir;
  guard: Guard;
}

/**
 * 방어자의 예측. 상대 공격 관측(book)으로 분포를 추정하고, 흔들림이 작을수록 가장 유력한 칸을 막는다.
 * `pressure` = 위압감·위기에서 오는 흔들림.
 */
export function planGuard(rng: Rng, me: Mind, habits: Habits, book: ReadBook, oppFeint: number, pressure: number, fake?: Dir): GuardPlan {
  // 속았다: 보여 준 방향을 막는다
  if (fake !== undefined) {
    const k = pickWeighted(rng, [0, 1, 2].map((g) => habits.def[cell(fake, g)]));
    return { dir: fake, guard: k as Guard };
  }
  const beta = Math.max(0.2, Math.min(6, 1.2 + (me.read - oppFeint) * 0.12 + me.read * 0.04 + me.form + me.late - pressure));
  const est = book.atk.map((v) => v + 1);
  const dirW = [0, 1, 2].map((d) => est[cell(d, 0)] + est[cell(d, 1)] + est[cell(d, 2)]);
  const habitDir = [0, 1, 2].map((d) => habits.def[cell(d, 0)] + habits.def[cell(d, 1)] + habits.def[cell(d, 2)]);
  // 판단력이 낮으면 읽은 것보다 제 버릇대로 막는다
  const useRead = rng.chance(Math.min(1, 0.35 + me.dec * 0.035 + me.read * 0.01));
  const dir = (useRead ? pickWeighted(rng, dirW.map((v) => Math.pow(v, beta))) : pickWeighted(rng, habitDir)) as Dir;
  let guard: Guard;
  if (useRead && rng.chance(Math.min(0.95, 0.35 + me.dec * 0.03 + me.read * 0.01))) {
    const motionW = [0, 1, 2].map((m) => Math.pow(est[cell(dir, m)], beta));
    guard = counterGuard(pickWeighted(rng, motionW) as Motion);
  } else {
    guard = pickWeighted(rng, [0, 1, 2].map((g) => habits.def[cell(dir, g)])) as Guard;
  }
  return { dir, guard };
}

export type Outcome = "clean" | "late" | "parry" | "neutral" | "break";

/** 방향이 맞았는지, 동작 상성은 어떤지로 판정표를 고른다 */
export function outcomeOf(rng: Rng, a: AttackPlan, g: GuardPlan, defRea: number): Outcome {
  if (a.dir === 3 || a.dir !== g.dir) return rng.chance(defRea * 0.01) ? "late" : "clean";
  const adv = ADV[a.motion][g.guard];
  return adv < 0 ? "parry" : adv > 0 ? "break" : "neutral";
}

/** 관측 가중치: 적응력이 높을수록 한 번 본 것을 크게 기억한다 */
export const learnWeight = (adapt: number) => 0.4 + adapt * 0.12;
