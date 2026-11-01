// 스카우팅: 다른 루두스·무소속 검투사에 대한 '정보'. 정찰할수록 능력과 잠재력 추정 범위가 좁아진다.
import { computeCA } from "./gladiator";
import { pushMessage } from "./state";
import { ratingOf } from "./staff";
import type { GameState, Gladiator, Id } from "./types";

export interface ScoutingState {
  /** 검투사별 정보량 0~100 */
  knowledge: Record<Id, number>;
  /** 지금 정찰 중인 대상 */
  assignments: Id[];
}

export const scoutingOf = (s: GameState): ScoutingState => (s.scouting ??= { knowledge: {}, assignments: [] });

export function isMine(s: GameState, g: Gladiator): boolean {
  const me = s.ludi[s.playerLudusId];
  return me.gladiators.includes(g.id) || (me.youth ?? []).includes(g.id) || s.fallen.includes(g.id);
}

/** 정보량: 우리 검투사는 다 안다. 유명한 검투사는 소문만으로도 어느 정도 안다 */
export function knowledgeOf(s: GameState, g: Gladiator): number {
  if (isMine(s, g)) return 100;
  const k = scoutingOf(s).knowledge[g.id] ?? 0;
  return Math.max(k, Math.min(60, g.popularity * 0.6));
}

/** 동시에 정찰할 수 있는 대상 수: 정찰꾼이 없으면 1명, 있으면 실력에 따라 2~4명 */
export function scoutSlots(s: GameState): number {
  const r = ratingOf(s, s.ludi[s.playerLudusId], "scout");
  return r ? 2 + Math.floor(r / 8) : 1;
}

export function assignScout(s: GameState, id: Id): string | null {
  const sc = scoutingOf(s);
  if (sc.assignments.includes(id)) return null;
  if (sc.assignments.length >= scoutSlots(s)) return `정찰은 동시에 ${scoutSlots(s)}명까지 맡길 수 있다`;
  sc.assignments.push(id);
  return null;
}

export function unassignScout(s: GameState, id: Id): void {
  const sc = scoutingOf(s);
  sc.assignments = sc.assignments.filter((x) => x !== id);
}

/** 매일: 정찰 진행 */
export function dailyScouting(s: GameState): void {
  const sc = scoutingOf(s);
  const r = ratingOf(s, s.ludi[s.playerLudusId], "scout");
  const gain = 4 + r * 0.7;
  for (const id of [...sc.assignments]) {
    const g = s.gladiators[id];
    if (!g || !g.alive) { unassignScout(s, id); continue; }
    sc.knowledge[id] = Math.min(100, (sc.knowledge[id] ?? 0) + gain);
    if (sc.knowledge[id] >= 100) {
      unassignScout(s, id);
      pushMessage(s, { kind: "info", title: `정찰 보고서: ${g.name}`, body: `${g.name} 정찰이 끝났다. 이제 능력과 잠재력이 꽤 정확히 보인다.` });
    }
  }
}

/** 시드로 정해진 치우침: 범위의 가운데가 진짜 값에서 살짝 비껴 있다 (진짜 값은 항상 범위 안) */
function skew(seed: number, salt: number): number {
  const x = Math.sin(seed * 12.9898 + salt * 78.233) * 43758.5453;
  return (x - Math.floor(x)) * 2 - 1;
}

export interface Range { lo: number; hi: number; mid: number }

function range(value: number, spread: number, seed: number, salt: number, min: number, max: number): Range {
  if (spread <= 0) return { lo: value, hi: value, mid: value };
  const off = skew(seed, salt) * spread * 0.5;
  const lo = Math.max(min, Math.round(value + off - spread));
  const hi = Math.min(max, Math.round(value + off + spread));
  return { lo, hi, mid: Math.round((lo + hi) / 2) };
}

/** 능력치 하나의 추정 범위 (1~20) */
export function attrRange(s: GameState, g: Gladiator, value: number, salt: number): Range {
  const k = knowledgeOf(s, g);
  const spread = isMine(s, g) ? 0 : Math.round(4 * (1 - k / 100));
  return range(value, spread, g.portraitSeed, salt, 1, 20);
}

/** 현재 능력(CA) 추정 */
export function caRange(s: GameState, g: Gladiator): Range {
  const k = knowledgeOf(s, g);
  return range(computeCA(g), isMine(s, g) ? 0 : Math.round(24 * (1 - k / 100)), g.portraitSeed, 1, 1, 200);
}

/** 잠재력(PA) 추정: 정찰꾼 실력과 정보량에 따라 */
export function paRange(s: GameState, g: Gladiator): Range {
  const r = ratingOf(s, s.ludi[s.playerLudusId], "scout");
  const k = knowledgeOf(s, g);
  const base = isMine(s, g) ? Math.max(3, 30 - r * 1.35) : Math.max(5, 45 - r * 2);
  const spread = Math.round(base * (1 - (isMine(s, g) ? 0 : k / 100) * 0.7));
  const ca = computeCA(g);
  const rr = range(g.hidden.pa, spread, g.portraitSeed, 2, ca, 200);
  return { ...rr, lo: Math.max(ca, rr.lo) };
}
