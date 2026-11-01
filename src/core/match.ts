import { absDay } from "./calendar";
import { logCareer, ludusOf, seasonRecord } from "./career";
import type { MatchResult } from "./combat/sim";
import { afterMatch } from "./condition";
import { BALANCE, getSkill } from "./data";
import { arenaTicketMult } from "./facilities";
import { aggravate } from "./injuries";
import { rulesOfNation } from "./nationRules";
import { coachMult, ratingOf } from "./staff";
import { addMoney } from "./economy";
import { modifierMult } from "./modifiers";
import type { Rng } from "./rng";
import { grow } from "./training";
import { josa } from "./josa";
import type { DeathRule, GameState, Gladiator, Id, Ludus } from "./types";

export function eloDelta(myRp: number, oppRp: number, won: boolean, k: number): number {
  const expected = 1 / (1 + Math.pow(10, (oppRp - myRp) / 400));
  return Math.round(k * ((won ? 1 : 0) - expected));
}

export interface ApplyOptions {
  rule: DeathRule;
  /** 공식 경기: RP·상금·관중 수입이 오간다 */
  ranked: boolean;
  /** 홈 루두스 (관중 수입을 더 가져간다) */
  homeLudusId?: Id;
}

export interface MatchSummary {
  rpChange: Record<Id, number>;
  money: Record<Id, number>;
  fame: Record<Id, number>;
}

/** 경기 결과를 게임 상태에 반영한다 */
export function applyMatchResult(state: GameState, r: MatchResult, opt: ApplyOptions, rng: Rng): MatchSummary {
  const rule = BALANCE.rules[opt.rule];
  const nr = rulesOfNation(ludusOf(state, r.winnerId)?.nation ?? state.nation);
  const w = state.gladiators[r.winnerId];
  const l = state.gladiators[r.loserId];
  const wl = ludusOf(state, w.id);
  const ll = ludusOf(state, l.id);
  const at = absDay(state.season, state.day);
  const summary: MatchSummary = { rpChange: {}, money: {}, fame: {} };

  w.record.wins++;
  l.record.losses++;
  w.morale = Math.min(100, w.morale + 5);
  l.morale = Math.max(0, l.morale - 5);

  // 명성(인기): 관중이 있어야 쌓인다. 사투일수록 크다
  // 주술 검투사를 떠받드는 나라도 있다
  const magicMult = (g: Gladiator) => (g.skills.active.some((id) => getSkill(id)?.magic) ? nr.magicFameMult : 1);
  const wFame = Math.round((2 + Math.max(0, r.crowdContribution[w.id]) / 10) * rule.fameMult * nr.fameMult * magicMult(w));
  const lFame = Math.round((r.crowdContribution[l.id] / 15) * rule.fameMult * nr.fameMult * magicMult(l));
  w.popularity = Math.max(0, Math.min(100, w.popularity + wFame));
  l.popularity = Math.max(0, Math.min(100, l.popularity + lFame));
  summary.fame[w.id] = wFame;
  summary.fame[l.id] = lFame;

  for (const g of [w, l]) {
    afterMatch(g, at, opt.rule);
    // 실전 경험: 훈련전·미시오·사투 모두 성장한다 (진 쪽도 배운다)
    const home = ludusOf(state, g.id);
    grow(g, rng, "match", rule.growthDays * (g === w ? 1 : 0.8) * nr.growthMult * coachMult(ratingOf(state, home, "doctore")), 1, home?.facilities.training ?? 1);
    const rec = seasonRecord(state, g);
    rec.apps++;
    if (g === w) rec.wins++;
    else rec.losses++;
    if (g.injury) aggravate(state, g, rng);
    const inj = r.injuries[g.id];
    if (inj && (!g.injury || g.injury.daysLeft < inj.daysLeft)) {
      g.injury = inj;
      rec.injuries++;
      logCareer(state, g, `${josa(opponentName(state, r, g), "과/와")}의 경기에서 ${inj.desc} (${inj.daysLeft}일)`, "injury");
    }
  }
  milestones(state, w);

  if (r.died) {
    const dead = state.gladiators[r.died];
    dead.alive = false;
    dead.injury = undefined;
    dead.diedAt = { season: state.season, day: state.day, cause: `${w.name}에게 패배` };
    w.record.kills++;
    seasonRecord(state, w).kills++;
    logCareer(state, w, `${rule.name}에서 ${dead.name}의 목숨을 거뒀다`, "milestone");
    logCareer(state, dead, `${rule.name}에서 ${w.name}에게 패해 모래 위에서 죽었다`, "death");
    const home = ludusOf(state, dead.id);
    if (home?.isPlayer) state.fallen.push(dead.id);
    if (home) {
      for (const gid of home.gladiators) {
        const g = state.gladiators[gid];
        if (g.alive && g.id !== dead.id) g.morale = Math.max(0, g.morale - BALANCE.morale.teammateDeath);
      }
      home.gladiators = home.gladiators.filter((x) => x !== dead.id);
    }
  }

  if (wl && ll) {
    wl.record.wins++;
    ll.record.losses++;
    if (opt.ranked) {
      settle(state, wl, ll, opt.rule, { homeLudusId: opt.homeLudusId, crowd: r.crowd, popularity: w.popularity + l.popularity }, summary);
      seasonRecord(state, w).earnings += summary.money[wl.id] ?? 0;
      seasonRecord(state, l).earnings += summary.money[ll.id] ?? 0;
      // 항복을 수치로 여기는 나라: 항복한 쪽은 인기와 RP를 더 잃는다
      if (nr.surrenderShame && r.reason === "surrender") {
        l.popularity = Math.max(0, l.popularity - 10);
        ll.rp -= 5;
        summary.rpChange[ll.id] -= 5;
        logCareer(state, l, "항복했다. 사람들이 등을 돌린다", "other");
      }
    }
  }
  return summary;
}

export interface SettleOptions {
  homeLudusId?: Id;
  /** 경기의 관중 열기 (0~100) */
  crowd: number;
  /** 두 쪽 검투사 인기 합 */
  popularity: number;
  /** 단체전: 판이 클수록 RP와 돈이 커진다 */
  rpMult?: number;
  moneyMult?: number;
  /** 컵처럼 리그 경기 수에 안 들어가는 경기 */
  noLeague?: boolean;
}

/** 루두스끼리 RP·상금·관중 수입을 정산한다 (1:1, 단체전, 컵 모두) */
export function settle(state: GameState, wl: Ludus, ll: Ludus, ruleId: DeathRule, o: SettleOptions, summary: MatchSummary): void {
  const rule = BALANCE.rules[ruleId];
  const nr = rulesOfNation(ll.nation);
  if (!o.noLeague) {
    wl.matchesThisSeason++;
    ll.matchesThisSeason++;
    const k = BALANCE.league.eloK * rule.rpMult * (o.rpMult ?? 1) * (wl.tier === 2 && ll.tier === 1 ? 1.25 : 1);
    const dw = eloDelta(wl.rp, ll.rp, true, k);
    const dl = eloDelta(ll.rp, wl.rp, false, k);
    wl.rp += dw;
    ll.rp += dl;
    summary.rpChange[wl.id] = dw;
    summary.rpChange[ll.id] = dl;
  }
  // 상금과 관중 수입. 훈련전은 관중이 없어 둘 다 없다
  const E = BALANCE.economy;
  const tier = String(Math.min(wl.tier, ll.tier)) as "1" | "2";
  const mult = o.moneyMult ?? 1;
  const purse = Math.round(E.purse[tier] * rule.purseMult * nr.purseMult * (ruleId === "sudden" ? nr.suddenPurseMult : 1) * mult);
  const crowdFactor = 0.5 + o.popularity / 200 + o.crowd / 200;
  const baseTickets = Math.round(E.ticketIncome[tier] * rule.ticketMult * crowdFactor * modifierMult(state, "ticket") * nr.ticketMult * mult);
  const homeId = o.homeLudusId ?? ll.id;
  // 홈 경기장이 클수록 관중 수입이 늘어난다
  const tickets = Math.round(baseTickets * arenaTicketMult(state.ludi[homeId]?.facilities.arena ?? 1));
  for (const lu of [wl, ll]) {
    const won = lu === wl;
    const share = (won ? purse : Math.round(purse * E.loserPurseShare)) + Math.round(tickets * (lu.id === homeId ? E.homeShare : 1 - E.homeShare));
    if (share > 0) addMoney(state, lu, share, `${rule.name} ${won ? "승리" : "패배"} 상금·관중 수입`);
    summary.money[lu.id] = (summary.money[lu.id] ?? 0) + share;
  }
}

function opponentName(state: GameState, r: MatchResult, g: Gladiator): string {
  return state.gladiators[g.id === r.winnerId ? r.loserId : r.winnerId].name;
}

function milestones(state: GameState, w: Gladiator): void {
  const wins = w.record.wins;
  if (wins === 1) logCareer(state, w, "첫 승리를 거뒀다", "milestone");
  else if (wins % 10 === 0) logCareer(state, w, `통산 ${wins}승`, "milestone");
}
