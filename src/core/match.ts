import { BALANCE } from "./data";
import type { MatchResult } from "./combat/sim";
import type { GameState, Id } from "./types";

export function eloDelta(myRp: number, oppRp: number, won: boolean, k: number): number {
  const expected = 1 / (1 + Math.pow(10, (oppRp - myRp) / 400));
  return Math.round(k * ((won ? 1 : 0) - expected));
}

export interface ApplyOptions {
  ranked: boolean;
  rpMult: number;
}

/** 경기 결과를 게임 상태에 반영한다 (전적, 사망, 부상, 사기, 인기, RP). */
export function applyMatchResult(state: GameState, r: MatchResult, opt: ApplyOptions): { rpChange: Record<Id, number> } {
  const w = state.gladiators[r.winnerId];
  const l = state.gladiators[r.loserId];
  const ludusOf = (gid: Id) => Object.values(state.ludi).find((x) => x.gladiators.includes(gid));
  const wl = ludusOf(w.id);
  const ll = ludusOf(l.id);

  w.record.wins++;
  l.record.losses++;
  w.morale = Math.min(100, w.morale + 5);
  l.morale = Math.max(0, l.morale - 5);
  w.popularity = Math.min(100, w.popularity + 2 + Math.round(Math.max(0, r.crowdContribution[w.id]) / 10));
  l.popularity = Math.max(0, l.popularity + Math.round(r.crowdContribution[l.id] / 15));
  for (const g of [w, l]) {
    g.fatigue = Math.min(100, g.fatigue + 20);
    const inj = r.injuries[g.id];
    if (inj && (!g.injury || g.injury.weeksLeft < inj.weeksLeft)) g.injury = inj;
  }

  if (r.died) {
    const dead = state.gladiators[r.died];
    dead.alive = false;
    dead.injury = undefined;
    dead.diedAt = { season: state.season, week: state.week, cause: `${w.name}에게 패배` };
    w.record.kills++;
    // 동료의 죽음은 루두스 전체의 사기를 꺾는다
    const home = ludusOf(dead.id);
    if (home?.isPlayer) state.fallen.push(dead.id);
    if (home) {
      for (const gid of home.gladiators) {
        const g = state.gladiators[gid];
        if (g.alive && g.id !== dead.id) g.morale = Math.max(0, g.morale - 8);
      }
      home.gladiators = home.gladiators.filter((x) => x !== dead.id);
    }
  }

  const rpChange: Record<Id, number> = {};
  if (wl && ll) {
    wl.record.wins++;
    ll.record.losses++;
    if (opt.ranked) {
      const k = BALANCE.league.eloK * opt.rpMult;
      const dw = eloDelta(wl.rp, ll.rp, true, k);
      const dl = eloDelta(ll.rp, wl.rp, false, k);
      wl.rp += dw;
      ll.rp += dl;
      rpChange[wl.id] = dw;
      rpChange[ll.id] = dl;
    }
  }
  return { rpChange };
}
