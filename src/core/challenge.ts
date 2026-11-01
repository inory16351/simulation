// 도전 시스템 (docs/04_league.md): 신청, 응답, 거절 페널티, 경기 일정, AI 루두스끼리의 경기
import { absDay, dateLabel, fromAbs } from "./calendar";
import { ludusOf } from "./career";
import { simulateMatch, type MatchResult } from "./combat/sim";
import { isJaded } from "./condition";
import { BALANCE } from "./data";
import { ageStage, computeCA } from "./gladiator";
import { josa } from "./josa";
import { applyMatchResult, type MatchSummary } from "./match";
import type { Rng } from "./rng";
import { rulesOfNation } from "./nationRules";
import { pushMessage } from "./state";
import { ratingOf } from "./staff";
import type { Challenge, DeathRule, GameState, Gladiator, Ludus } from "./types";
import { autoLineup, FORMAT_MULT, FORMAT_NAMES, runSeries, type Format, type Series } from "./series";
import { settle } from "./match";

const L = BALANCE.league;
const RULE_NAME = (r: DeathRule) => BALANCE.rules[r].name;

export const now = (state: GameState) => absDay(state.season, state.day);

/** 사투 생존에 쓰는 의료 수준: 의무실 단계 + 메디쿠스 실력 */
export const medicalOf = (state: GameState, l: Ludus) => l.facilities.medical + ratingOf(state, l, "medicus") / 8;

/** 정규 기간에만 도전 경기를 치른다 */
export const inRegularPeriod = (state: GameState) => state.day >= 15 && state.day <= 154;

/** 오늘 출전할 수 있는가 (쉬는 기간·부상·예약된 경기 확인) */
export function availability(state: GameState, g: Gladiator, at = now(state)): string | null {
  if (!g.alive) return "사망";
  if (g.deployment) return "용병 파견 중";
  if (ageStage(g) === "youth") return "아직 어리다";
  if (g.injury?.severity === "serious") return "중상";
  if (g.lastFightAt !== undefined && at - g.lastFightAt < L.restDays) return `휴식 중 (${L.restDays - (at - g.lastFightAt)}일)`;
  const booked = state.challenges.find((c) => c.status === "scheduled" && (c.fromGladiator === g.id || c.toGladiator === g.id) && c.matchAt !== at);
  if (booked) return `경기 예정 (${dateLabel(fromAbs(booked.matchAt!).day)})`;
  return null;
}

/** 이 루두스가 상대에게 도전할 수 있는가 */
export function canChallenge(state: GameState, from: Ludus, to: Ludus): string | null {
  if (from.id === to.id) return "자기 자신";
  if (from.nation !== to.nation) return "다른 나라";
  if (!inRegularPeriod(state)) return "정규 기간(서리달 15일 ~ 그림자달 14일)이 아니다";
  if (from.tier !== to.tier && Math.abs(from.rp - to.rp) > L.challengeRange) return `다른 리그는 RP ±${L.challengeRange} 안에서만`;
  const week = now(state) - 6;
  const sent = state.challenges.filter((c) => c.from === from.id && c.createdAt >= week).length;
  if (sent >= L.challengesPerWeek) return `일주일에 ${L.challengesPerWeek}번까지만 신청할 수 있다`;
  if (state.challenges.some((c) => c.from === from.id && c.to === to.id && (c.status === "pending" || c.status === "scheduled"))) return "이미 도전 중";
  return null;
}

export function createChallenge(
  state: GameState, from: Ludus, to: Ludus, fromGladiator: string, rule: DeathRule, toGladiator?: string, stake = 0, format: Format = 1,
): Challenge | string {
  const err = canChallenge(state, from, to);
  if (err) return err;
  if (rule === "sudden" && rulesOfNation(from.nation).suddenBanned) return "이 나라 리그에서는 사투가 금지돼 있다";
  const g = state.gladiators[fromGladiator];
  const busy = availability(state, g);
  if (busy) return `${g.name}: ${busy}`;
  if (format > 1 && autoLineup(state, from, format, now(state) + L.matchDelayMin).length < format) return `${FORMAT_NAMES[format]}에 내보낼 검투사가 ${format}명이 안 된다`;
  const c: Challenge = {
    id: `C${state.nextId++}`, from: from.id, to: to.id, fromGladiator, toGladiator, rule, stake,
    createdAt: now(state), respondBy: now(state) + L.respondDays, status: "pending",
    ...(format > 1 ? { format } : {}),
  };
  state.challenges.push(c);
  return c;
}

/** 경기에 내보낼 검투사를 고른다 (가장 강하고 상태가 좋은 쪽) */
export function pickFighter(state: GameState, ludus: Ludus, rule: DeathRule, at: number): Gladiator | undefined {
  const pool = ludus.gladiators.map((id) => state.gladiators[id]).filter((g) => !availability(state, g, at) && !isJaded(g));
  if (!pool.length) return undefined;
  // 사투에는 아끼는 최강자 대신 두 번째로 강한 자를 내보내는 루두스가 많다
  const sorted = pool.sort((a, b) => computeCA(b) * (1 - b.wear / 200) - computeCA(a) * (1 - a.wear / 200));
  return rule === "sudden" && sorted.length > 2 ? sorted[1] : sorted[0];
}

function schedule(state: GameState, c: Challenge, rng: Rng, defender: Gladiator): void {
  c.toGladiator = defender.id;
  c.status = "scheduled";
  // 두 검투사의 다른 경기와 휴식 기간이 겹치지 않는 날로 잡는다
  const others = state.challenges
    .filter((x) => x !== c && x.status === "scheduled" && [x.fromGladiator, x.toGladiator].some((id) => id === c.fromGladiator || id === defender.id))
    .map((x) => x.matchAt!);
  let day = now(state) + rng.int(L.matchDelayMin, L.matchDelayMax);
  while (others.some((d) => Math.abs(d - day) < L.restDays)) day++;
  c.matchAt = day;
}

/** 도전을 받거나 거절한다. 거절은 시즌당 무료 횟수를 넘으면 RP를 잃는다 */
export function respond(state: GameState, c: Challenge, accept: boolean, rng: Rng, defenderId?: string): string | null {
  const to = state.ludi[c.to];
  const from = state.ludi[c.from];
  if (accept) {
    const defender = defenderId ? state.gladiators[defenderId] : pickFighter(state, to, c.rule, now(state) + L.matchDelayMin);
    if (!defender) return "내보낼 검투사가 없다";
    const busy = availability(state, defender);
    if (busy) return `${defender.name}: ${busy}`;
    schedule(state, c, rng, defender);
    const msgTarget = from.isPlayer ? from : to.isPlayer ? to : null;
    if (msgTarget) {
      const mine = from.isPlayer ? state.gladiators[c.fromGladiator] : defender;
      const theirs = from.isPlayer ? defender : state.gladiators[c.fromGladiator];
      pushMessage(state, {
        kind: "match",
        title: c.format ? `${FORMAT_NAMES[c.format]} ${RULE_NAME(c.rule)} 확정: ${from.isPlayer ? to.name : from.name}` : `${RULE_NAME(c.rule)} 확정: ${mine.name} vs ${theirs.name}`,
        body: `상대 루두스: ${from.isPlayer ? to.name : from.name}\n날짜: ${dateLabel(fromAbs(c.matchAt!).day)}\n우리: ${mine.name} / 상대: ${theirs.name}`,
      });
    }
    return null;
  }
  c.status = "declined";
  to.refusalsUsed++;
  // 사투 도전은 거절해도 페널티가 절반, 약한 상대를 피하면 두 배
  let penalty = 0;
  if (to.refusalsUsed > L.freeRefusals) {
    penalty = L.refusalPenalty * (c.rule === "sudden" ? 0.5 : 1) * (from.rp < to.rp ? 2 : 1);
    to.rp -= Math.round(penalty);
  }
  if (from.isPlayer) {
    pushMessage(state, { kind: "info", title: `${josa(to.name, "이/가")} 도전을 거절했다`, body: `${RULE_NAME(c.rule)} 신청이 거절됐다.${penalty ? ` 그들은 RP ${Math.round(penalty)}를 잃었다.` : ""}` });
  }
  if (to.isPlayer) {
    pushMessage(state, {
      kind: "warning",
      title: "도전을 거절했다",
      body: `${from.name}의 ${RULE_NAME(c.rule)} 도전을 거절했다. 이번 시즌 거절 ${to.refusalsUsed}회 (무료 ${L.freeRefusals}회).${penalty ? `\nRP -${Math.round(penalty)}. 관중이 '겁쟁이 루두스'라고 수군댄다.` : ""}`,
    });
  }
  return null;
}

/** AI가 받은 도전에 응답할지 정한다 */
function aiWantsToAccept(state: GameState, c: Challenge, rng: Rng): boolean {
  const to = state.ludi[c.to];
  const challenger = state.gladiators[c.fromGladiator];
  const best = pickFighter(state, to, c.rule, now(state) + L.matchDelayMin);
  if (!best) return false;
  const p = to.aiPersonality ?? "stable";
  let chance = { aggressive: 0.85, stable: 0.6, grudge: 0.8, merchant: 0.65, developer: 0.6 }[p];
  if (c.rule === "sudden") chance *= p === "aggressive" || p === "grudge" ? 0.8 : 0.35;
  const diff = computeCA(best) - computeCA(challenger);
  chance += diff / 100;
  // 무료 거절을 다 쓰면 웬만하면 받는다
  if (to.refusalsUsed >= L.freeRefusals) chance += 0.25;
  return rng.chance(Math.max(0.05, Math.min(0.97, chance)));
}

function pickRule(rng: Rng, ludus: Ludus): DeathRule {
  const p = ludus.aiPersonality ?? "stable";
  const w = {
    aggressive: { training: 3, missio: 4, sudden: 1.2 },
    stable: { training: 7, missio: 3, sudden: 0.1 },
    grudge: { training: 3, missio: 4, sudden: 1.5 },
    merchant: { training: 4, missio: 5, sudden: 0.5 },
    developer: { training: 8, missio: 2, sudden: 0.1 },
  }[p];
  const nr = rulesOfNation(ludus.nation);
  const sudden = nr.suddenBanned ? 0 : w.sudden * nr.aiSuddenMult;
  return rng.weighted<DeathRule>({ ...w, sudden });
}

/** 매일: AI 응답, 기한 지난 도전 처리, AI의 도전, AI끼리의 경기 */
export function dailyChallenges(state: GameState, rng: Rng): void {
  const t = now(state);
  const player = state.ludi[state.playerLudusId];

  for (const c of state.challenges.filter((x) => x.status === "pending")) {
    const to = state.ludi[c.to];
    if (!to.isPlayer) {
      // AI는 다음 날 답한다
      if (t > c.createdAt) respond(state, c, aiWantsToAccept(state, c, rng), rng);
    } else if (t > c.respondBy) {
      respond(state, c, false, rng);
    }
  }

  if (!inRegularPeriod(state)) return;

  // AI가 플레이어에게 도전장을 보낸다
  const rivals = Object.values(state.ludi).filter((l) => !l.isPlayer && !canChallenge(state, l, player));
  for (const ai of rivals) {
    if (!rng.chance(L.aiChallengePlayerChance / 3)) continue;
    const rule = pickRule(rng, ai);
    const g = pickFighter(state, ai, rule, t);
    if (!g) continue;
    const roll = rng.next();
    const format: Format = roll < 0.08 ? 5 : roll < 0.25 ? 3 : 1;
    let c = createChallenge(state, ai, player, g.id, rule, undefined, 0, format);
    if (typeof c === "string" && format > 1) c = createChallenge(state, ai, player, g.id, rule);
    if (typeof c === "string") continue;
    const fmt = c.format ? `${FORMAT_NAMES[c.format]} ` : "";
    pushMessage(state, {
      kind: "match",
      title: `${ai.name}의 도전장 (${fmt}${RULE_NAME(rule)})`,
      body: `${c.format ? `${ai.name}이(가) ${FORMAT_NAMES[c.format]}을 걸어왔다. 선봉은 ${g.name}.` : `${ai.name}의 ${josa(g.name, "이/가")} ${RULE_NAME(rule)}을 걸어왔다.`}\n${dateLabel(fromAbs(c.respondBy).day)}까지 답하지 않으면 거절한 것으로 친다.\n\n도전장 화면에서 받을지 정하라.`,
    });
    break;
  }

  // AI 루두스끼리의 경기 (바로 치르고 결과만 반영)
  const groups = new Map<string, Ludus[]>();
  for (const l of Object.values(state.ludi)) {
    if (l.isPlayer) continue;
    const key = `${l.nation}-${l.tier}`;
    groups.set(key, [...(groups.get(key) ?? []), l]);
  }
  for (const ludi of groups.values()) {
    for (const a of ludi) {
      if (!rng.chance(L.aiMatchChancePerDay)) continue;
      const opponents = ludi.filter((b) => b !== a);
      const b = rng.pick(opponents);
      const rule = pickRule(rng, a);
      if (rng.chance(0.15)) {
        const la = autoLineup(state, a, 3, t);
        const lb = autoLineup(state, b, 3, t);
        if (la.length === 3 && lb.length === 3) {
          teamSettle(state, a, b, runSeries(state, [a, b], [la, lb], { rule, mode: "winner_stays", homeLudusId: b.id }, rng), rule, 3, b.id);
          continue;
        }
      }
      const ga = pickFighter(state, a, rule, t);
      const gb = pickFighter(state, b, rule, t);
      if (!ga || !gb) continue;
      const r = simulateMatch(ga, gb, { rule, seed: rng.int(0, 2 ** 31 - 1), missioDeathMult: rulesOfNation(a.nation).missioDeathMult, medicalLevel: [medicalOf(state, a), medicalOf(state, b)] });
      applyMatchResult(state, r, { rule, ranked: true, homeLudusId: b.id }, rng);
    }
  }
}

/** 오늘 플레이어가 치러야 할 경기 */
export function todaysPlayerMatches(state: GameState): Challenge[] {
  const t = now(state);
  return state.challenges.filter(
    (c) => c.status === "scheduled" && c.matchAt === t && (state.ludi[c.from].isPlayer || state.ludi[c.to].isPlayer),
  );
}

export interface PlayedMatch {
  challenge: Challenge;
  result: MatchResult | null;
  a: Gladiator;
  b: Gladiator;
  summary?: MatchSummary;
  forfeit?: string;
  /** 단체전 */
  series?: Series;
}

/** 단체전 결과를 루두스끼리 정산한다 */
export function teamSettle(state: GameState, a: Ludus, b: Ludus, s: Series, rule: DeathRule, format: Format, homeId: string): MatchSummary {
  const summary: MatchSummary = { rpChange: {}, money: {}, fame: {} };
  const [wl, ll] = s.winner === 0 ? [a, b] : [b, a];
  const crowd = s.bouts.reduce((x, bt) => x + bt.result.crowd, 0) / Math.max(1, s.bouts.length);
  const pop = s.bouts.reduce((x, bt) => x + bt.a.popularity + bt.b.popularity, 0) / Math.max(1, s.bouts.length);
  wl.record.wins++;
  ll.record.losses++;
  settle(state, wl, ll, rule, { homeLudusId: homeId, crowd, popularity: pop, rpMult: FORMAT_MULT[format].rp, moneyMult: FORMAT_MULT[format].money }, summary);
  return summary;
}

/** 예정된 경기를 치른다. 출전자가 못 나오면 대체 선수, 그마저 없으면 기권패 */
export function playChallenge(state: GameState, c: Challenge, rng: Rng): PlayedMatch {
  const t = now(state);
  const from = state.ludi[c.from];
  const to = state.ludi[c.to];
  const resolve = (ludus: Ludus, id?: string) => {
    const g = id ? state.gladiators[id] : undefined;
    if (g && g.alive && ludusOf(state, g.id) === ludus && g.injury?.severity !== "serious") return g;
    return pickFighter(state, ludus, c.rule, t);
  };
  if (c.format && c.format > 1) {
    c.status = "done";
    const la = autoLineup(state, from, c.format, t, c.fromLineup ?? [c.fromGladiator]);
    const lb = autoLineup(state, to, c.format, t, c.toLineup ?? (c.toGladiator ? [c.toGladiator] : []));
    if (!la.length || !lb.length) {
      const loser = !la.length ? from : to;
      loser.rp -= 25;
      loser.matchesThisSeason++;
      return { challenge: c, result: null, a: la[0] ?? lb[0], b: lb[0] ?? la[0], forfeit: `${loser.name} 기권 (출전 가능한 검투사가 없다)` };
    }
    const snaps = [structuredClone(la[0]), structuredClone(lb[0])];
    const series = runSeries(state, [from, to], [la, lb], { rule: c.rule, mode: "winner_stays", homeLudusId: to.id }, rng);
    const summary = teamSettle(state, from, to, series, c.rule, c.format, to.id);
    return { challenge: c, result: series.bouts[series.bouts.length - 1]?.result ?? null, a: snaps[0], b: snaps[1], summary, series };
  }
  const a = resolve(from, c.fromGladiator);
  const b = resolve(to, c.toGladiator);
  c.status = "done";
  if (!a || !b) {
    // 기권: 나오지 못한 쪽이 RP를 잃는다
    const loser = !a ? from : to;
    loser.rp -= 15;
    loser.matchesThisSeason++;
    return { challenge: c, result: null, a: a ?? b!, b: b ?? a!, forfeit: `${loser.name} 기권 (출전 가능한 검투사가 없다)` };
  }
  const snapA = structuredClone(a);
  const snapB = structuredClone(b);
  const result = simulateMatch(a, b, { rule: c.rule, seed: rng.int(0, 2 ** 31 - 1), missioDeathMult: rulesOfNation(to.nation).missioDeathMult, medicalLevel: [medicalOf(state, from), medicalOf(state, to)] });
  const summary = applyMatchResult(state, result, { rule: c.rule, ranked: true, homeLudusId: to.id }, rng);
  return { challenge: c, result, a: snapA, b: snapB, summary };
}

/** AI끼리 잡힌 경기는 날짜가 되면 바로 치른다 */
export function playAiScheduled(state: GameState, rng: Rng): void {
  const t = now(state);
  for (const c of state.challenges) {
    if (c.status !== "scheduled" || c.matchAt !== t) continue;
    if (state.ludi[c.from].isPlayer || state.ludi[c.to].isPlayer) continue;
    playChallenge(state, c, rng);
  }
}
