// 여러 판을 잇는 경기: 단체전(승자 연속, 체력 이월)과 판테온 컵(삼판양승)
import { simulateMatch, type MatchResult } from "./combat/sim";
import { availability, medicalOf } from "./challenge";
import { isJaded } from "./condition";
import { computeCA } from "./gladiator";
import { applyMatchResult, type MatchSummary } from "./match";
import { rulesOfNation } from "./nationRules";
import type { Rng } from "./rng";
import type { DeathRule, GameState, Gladiator, Id, Ludus } from "./types";

export type Format = 1 | 3 | 5;
export const FORMAT_NAMES: Record<Format, string> = { 1: "1:1", 3: "3:3 단체전", 5: "5:5 단체전" };
/** 단체전은 판이 클수록 RP와 돈이 커진다 */
export const FORMAT_MULT: Record<Format, { rp: number; money: number }> = {
  1: { rp: 1, money: 1 },
  3: { rp: 1.3, money: 2.2 },
  5: { rp: 1.6, money: 3.5 },
};

export interface Bout {
  result: MatchResult;
  /** 0쪽, 1쪽 검투사 (경기 전 모습) */
  a: Gladiator;
  b: Gladiator;
  summary: MatchSummary;
  /** 이 판을 시작할 때 체력 비율 */
  startHp: [number, number];
}

export interface Series {
  bouts: Bout[];
  /** 이긴 판 수 */
  score: [number, number];
  winner: 0 | 1;
  /** 나오지 못해 진 판 */
  walkovers: [number, number];
}

/** 출전 명단을 자동으로 짠다: 강하고 상태 좋은 순 */
export function autoLineup(state: GameState, ludus: Ludus, n: number, at: number, keep: Id[] = []): Gladiator[] {
  const out = keep.map((id) => state.gladiators[id]).filter((g) => g && g.alive && ludus.gladiators.includes(g.id) && g.injury?.severity !== "serious" && !g.deployment);
  const pool = ludus.gladiators.map((id) => state.gladiators[id])
    .filter((g) => !out.includes(g) && !availability(state, g, at) && !isJaded(g))
    .sort((a, b) => computeCA(b) * (1 - b.wear / 200) - computeCA(a) * (1 - a.wear / 200));
  return [...out, ...pool].slice(0, n);
}

interface RunOptions {
  rule: DeathRule;
  /** winner_stays: 이긴 사람이 남아 다음 상대와 싸운다 (단체전). best_of: 한 판씩 짝지어 과반을 먼저 따면 이긴다 (컵) */
  mode: "winner_stays" | "best_of";
  homeLudusId?: Id;
  /** 승자가 다음 판 전에 숨을 고르며 되찾는 체력 비율 */
  breather?: number;
}

/** 두 루두스가 명단대로 여러 판을 치른다. 개인 성장·부상·죽음은 판마다, RP와 돈은 호출한 쪽에서 정산한다 */
export function runSeries(state: GameState, sides: [Ludus, Ludus], lineups: [Gladiator[], Gladiator[]], o: RunOptions, rng: Rng): Series {
  const bouts: Bout[] = [];
  const score: [number, number] = [0, 0];
  const walkovers: [number, number] = [0, 0];
  const n = Math.max(lineups[0].length, lineups[1].length);
  const fight = (a: Gladiator, b: Gladiator, hp: [number, number]): MatchResult => {
    const snapA = structuredClone(a);
    const snapB = structuredClone(b);
    const result = simulateMatch(a, b, {
      rule: o.rule, seed: rng.int(0, 2 ** 31 - 1),
      missioDeathMult: rulesOfNation(sides[1].nation).missioDeathMult,
      medicalLevel: [medicalOf(state, sides[0]), medicalOf(state, sides[1])],
      startHp: [hp[0] < 1 ? hp[0] : undefined, hp[1] < 1 ? hp[1] : undefined],
    });
    const summary = applyMatchResult(state, result, { rule: o.rule, ranked: false, homeLudusId: o.homeLudusId }, rng);
    bouts.push({ result, a: snapA, b: snapB, summary, startHp: hp });
    return result;
  };

  if (o.mode === "best_of") {
    const need = Math.floor(n / 2) + 1;
    for (let i = 0; i < n && score[0] < need && score[1] < need; i++) {
      const a = lineups[0][i];
      const b = lineups[1][i];
      if (!a || !b) {
        // 사람이 모자라면 그 판은 기권패
        if (!a) walkovers[0]++;
        if (!b) walkovers[1]++;
        if (a && !b) score[0]++;
        if (b && !a) score[1]++;
        continue;
      }
      const r = fight(a, b, [1, 1]);
      score[r.winnerId === a.id ? 0 : 1]++;
    }
  } else {
    let i = 0;
    let j = 0;
    let hp: [number, number] = [1, 1];
    while (i < lineups[0].length && j < lineups[1].length) {
      const a = lineups[0][i];
      const b = lineups[1][j];
      const r = fight(a, b, hp);
      const recover = o.breather ?? 0.2;
      if (r.winnerId === a.id) {
        score[0]++;
        hp = [Math.min(1, r.hpLeft[a.id] + recover), 1];
        j++;
      } else {
        score[1]++;
        hp = [1, Math.min(1, r.hpLeft[b.id] + recover)];
        i++;
      }
    }
    walkovers[0] = Math.max(0, n - lineups[0].length);
    walkovers[1] = Math.max(0, n - lineups[1].length);
  }
  // 단체전은 상대 명단을 다 쓰러뜨린 쪽이, 컵은 판을 더 많이 딴 쪽이 이긴다
  const winner: 0 | 1 = o.mode === "winner_stays"
    ? (lineups[1].length === 0 || (lineups[0].length > 0 && bouts.length > 0 && bouts[bouts.length - 1].result.winnerId === bouts[bouts.length - 1].a.id) ? 0 : 1)
    : score[0] >= score[1] ? (score[0] === score[1] ? (rng.chance(0.5) ? 0 : 1) : 0) : 1;
  return { bouts, score, winner, walkovers };
}
