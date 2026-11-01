// 판테온 컵 (docs/04_league.md): 여섯 나라 1부 상위 루두스 16곳이 겨루는 대륙 대회
import { absDay } from "./calendar";
import { NATIONS, getNation } from "./data";
import { addMoney } from "./economy";
import { josa } from "./josa";
import { settle, type MatchSummary } from "./match";
import type { Rng } from "./rng";
import { autoLineup, runSeries, type Series } from "./series";
import { pushMessage } from "./state";
import type { GameState, Id, Ludus, NationId } from "./types";

export type CupStage = "group" | "qf" | "sf" | "final";
export const STAGE_NAMES: Record<CupStage, string> = { group: "조별 리그", qf: "8강", sf: "4강", final: "결승" };

export interface CupTie {
  id: Id;
  stage: CupStage;
  day: number;
  home: Id;
  away: Id;
  group?: number;
  /** 이긴 판 수 [홈, 원정] */
  score?: [number, number];
  winner?: Id;
}

export interface Cup {
  season: number;
  teams: Id[];
  groups: Id[][];
  ties: CupTie[];
  /** 루두스별 계수 점수 (나라 계수에 들어간다) */
  points: Record<Id, number>;
  champion?: Id;
}

export const CUP = {
  drawDay: 15,
  groupDays: [29, 43, 57, 71, 85, 99],
  qfDay: 113,
  sfDay: 127,
  finalDay: 150,
  /** 계수 순위별 출전권 */
  slots: [4, 4, 3, 3, 1, 1],
  bouts: 3,
  prize: { entry: 5000, groupWin: 1000, qf: 4000, sf: 8000, runnerUp: 12000, winner: 25000 },
  coef: { win: 2, qf: 1, sf: 1, final: 1, champion: 2 },
};

/** 같은 조 4팀이 홈·원정 한 번씩 (6라운드) */
const ROUND_ROBIN: [number, number][][] = [
  [[0, 1], [2, 3]], [[0, 2], [1, 3]], [[0, 3], [1, 2]],
  [[1, 0], [3, 2]], [[2, 0], [3, 1]], [[3, 0], [2, 1]],
];

export const GROUP_LETTERS = ["가", "나", "다", "라"];

/** 나라 계수: 최근 3시즌 점수 합 */
export function coefficientOf(state: GameState, n: NationId): number {
  return Math.round((state.coefficients?.[n] ?? []).reduce((a, b) => a + b, 0) * 10) / 10;
}

export function coefficientRanking(state: GameState): { nation: NationId; coef: number; slots: number }[] {
  return NATIONS.map((n) => ({ nation: n.id, coef: coefficientOf(state, n.id) }))
    .sort((a, b) => b.coef - a.coef)
    .map((x, i) => ({ ...x, slots: CUP.slots[i] ?? 0 }));
}

/** 조 추첨: RP 순으로 네 단지에 나누고, 같은 나라는 되도록 다른 조로 */
export function drawCup(state: GameState, rng: Rng): Cup {
  const teams: Ludus[] = [];
  for (const { nation, slots } of coefficientRanking(state)) {
    const top = Object.values(state.ludi).filter((l) => l.nation === nation && l.tier === 1).sort((a, b) => b.rp - a.rp).slice(0, slots);
    teams.push(...top);
  }
  const sorted = [...teams].sort((a, b) => b.rp - a.rp);
  let groups: Ludus[][] = [];
  for (let attempt = 0; attempt < 40; attempt++) {
    groups = [[], [], [], []];
    for (let pot = 0; pot < 4; pot++) {
      const potTeams = rng.shuffle(sorted.slice(pot * 4, pot * 4 + 4));
      potTeams.forEach((t, i) => groups[i].push(t));
    }
    const clash = groups.some((g) => new Set(g.map((l) => l.nation)).size < g.length);
    if (!clash) break;
  }
  const ties: CupTie[] = [];
  groups.forEach((g, gi) => {
    ROUND_ROBIN.forEach((round, ri) => {
      for (const [h, a] of round) {
        if (!g[h] || !g[a]) continue;
        ties.push({ id: `T${state.nextId++}`, stage: "group", day: CUP.groupDays[ri], home: g[h].id, away: g[a].id, group: gi });
      }
    });
  });
  const cup: Cup = { season: state.season, teams: teams.map((t) => t.id), groups: groups.map((g) => g.map((l) => l.id)), ties, points: {} };
  for (const t of teams) {
    cup.points[t.id] = 0;
    addMoney(state, t, CUP.prize.entry, "판테온 컵 출전 수당");
  }
  return cup;
}

export interface GroupRow { id: Id; played: number; won: number; lost: number; boutsFor: number; boutsAgainst: number; pts: number }

export function groupTable(state: GameState, cup: Cup, gi: number): GroupRow[] {
  const rows = new Map<Id, GroupRow>(cup.groups[gi].map((id) => [id, { id, played: 0, won: 0, lost: 0, boutsFor: 0, boutsAgainst: 0, pts: 0 }]));
  for (const t of cup.ties) {
    if (t.stage !== "group" || t.group !== gi || !t.score) continue;
    const h = rows.get(t.home)!;
    const a = rows.get(t.away)!;
    h.played++; a.played++;
    h.boutsFor += t.score[0]; h.boutsAgainst += t.score[1];
    a.boutsFor += t.score[1]; a.boutsAgainst += t.score[0];
    if (t.winner === t.home) { h.won++; a.lost++; h.pts += 3; } else { a.won++; h.lost++; a.pts += 3; }
  }
  return [...rows.values()].sort((x, y) => y.pts - x.pts || (y.boutsFor - y.boutsAgainst) - (x.boutsFor - x.boutsAgainst) || (state.ludi[y.id]?.rp ?? 0) - (state.ludi[x.id]?.rp ?? 0));
}

const winnerOf = (cup: Cup, stage: CupStage, i: number) => cup.ties.filter((t) => t.stage === stage)[i]?.winner;

/** 단계가 끝났으면 다음 단계 대진을 만든다 */
function advanceBracket(state: GameState, cup: Cup): void {
  const has = (s: CupStage) => cup.ties.some((t) => t.stage === s);
  const done = (s: CupStage) => cup.ties.filter((t) => t.stage === s).every((t) => t.winner);
  const add = (stage: CupStage, day: number, pairs: [Id | undefined, Id | undefined][]) => {
    for (const [h, a] of pairs) if (h && a) cup.ties.push({ id: `T${state.nextId++}`, stage, day, home: h, away: a });
  };
  if (!has("qf") && done("group")) {
    const t = cup.groups.map((_, gi) => groupTable(state, cup, gi));
    add("qf", CUP.qfDay, [[t[0][0]?.id, t[1][1]?.id], [t[1][0]?.id, t[0][1]?.id], [t[2][0]?.id, t[3][1]?.id], [t[3][0]?.id, t[2][1]?.id]]);
    for (const g of t) for (const r of g.slice(0, 2)) reach(state, cup, r.id, "qf");
  } else if (has("qf") && !has("sf") && done("qf")) {
    add("sf", CUP.sfDay, [[winnerOf(cup, "qf", 0), winnerOf(cup, "qf", 2)], [winnerOf(cup, "qf", 1), winnerOf(cup, "qf", 3)]]);
    for (const t of cup.ties.filter((x) => x.stage === "qf")) reach(state, cup, t.winner!, "sf");
  } else if (has("sf") && !has("final") && done("sf")) {
    add("final", CUP.finalDay, [[winnerOf(cup, "sf", 0), winnerOf(cup, "sf", 1)]]);
    for (const t of cup.ties.filter((x) => x.stage === "sf")) reach(state, cup, t.winner!, "final");
  }
}

/** 다음 단계 진출: 상금과 계수 점수 */
function reach(state: GameState, cup: Cup, id: Id, stage: "qf" | "sf" | "final"): void {
  const l = state.ludi[id];
  if (!l) return;
  cup.points[id] = (cup.points[id] ?? 0) + CUP.coef[stage];
  if (stage === "qf") addMoney(state, l, CUP.prize.qf, "판테온 컵 8강 진출 상금");
  if (stage === "sf") addMoney(state, l, CUP.prize.sf, "판테온 컵 4강 진출 상금");
}

export interface PlayedTie { tie: CupTie; series: Series; summary: MatchSummary }

/** 대결 한 번: 세 판 중 두 판을 먼저 따면 이긴다 (미시오) */
export function playTie(state: GameState, tieId: Id, rng: Rng, lineup?: Id[]): PlayedTie | string {
  const cup = state.cup;
  const tie = cup?.ties.find((t) => t.id === tieId);
  if (!cup || !tie) return "그런 경기가 없다";
  if (tie.winner) return "이미 치른 경기";
  const home = state.ludi[tie.home];
  const away = state.ludi[tie.away];
  const at = absDay(state.season, state.day);
  const lh = autoLineup(state, home, CUP.bouts, at, home.isPlayer ? lineup : undefined);
  const la = autoLineup(state, away, CUP.bouts, at, away.isPlayer ? lineup : undefined);
  const series = runSeries(state, [home, away], [lh, la], { rule: "missio", mode: "best_of", homeLudusId: home.id }, rng);
  tie.score = series.score;
  const [w, l] = series.winner === 0 ? [home, away] : [away, home];
  tie.winner = w.id;
  const summary: MatchSummary = { rpChange: {}, money: {}, fame: {} };
  const crowd = series.bouts.reduce((x, b) => x + b.result.crowd, 0) / Math.max(1, series.bouts.length);
  settle(state, w, l, "missio", { homeLudusId: home.id, crowd: crowd + 20, popularity: 120, moneyMult: 2, noLeague: true }, summary);
  cup.points[w.id] = (cup.points[w.id] ?? 0) + CUP.coef.win;
  if (tie.stage === "group") addMoney(state, w, CUP.prize.groupWin, "판테온 컵 조별 리그 승리 수당");
  if (tie.stage === "final") {
    cup.champion = w.id;
    cup.points[w.id] += CUP.coef.champion;
    addMoney(state, w, CUP.prize.winner, "판테온 컵 우승 상금");
    addMoney(state, l, CUP.prize.runnerUp, "판테온 컵 준우승 상금");
    w.titles = (w.titles ?? 0) + 1;
    pushMessage(state, {
      kind: "info",
      title: `판테온 컵 우승: ${w.name}`,
      body: `${getNation(w.nation).name}의 ${josa(w.name, "이/가")} 결승에서 ${l.name}을(를) ${Math.max(...series.score)}:${Math.min(...series.score)}로 꺾고 판테온 컵을 들어 올렸다.`,
    });
  }
  const me = state.playerLudusId;
  if (w.id === me || l.id === me) {
    const won = w.id === me;
    const opp = won ? l : w;
    pushMessage(state, {
      kind: "match",
      title: `판테온 컵 ${STAGE_NAMES[tie.stage]}: ${won ? "승리" : "패배"}`,
      body: `${opp.name}(${getNation(opp.nation).name})와의 대결 ${series.score[home.id === me ? 0 : 1]}:${series.score[home.id === me ? 1 : 0]}.` +
        `\n수입 ${(summary.money[me] ?? 0).toLocaleString()} D (세전)` +
        (tie.stage !== "group" && !won ? "\n여기서 탈락했다." : ""),
    });
  }
  advanceBracket(state, cup);
  return { tie, series, summary };
}

/** 오늘 플레이어가 치를 컵 경기 */
export function todaysPlayerTie(state: GameState): CupTie | undefined {
  const me = state.playerLudusId;
  return state.cup?.ties.find((t) => t.day === state.day && !t.winner && (t.home === me || t.away === me));
}

/** 매일: 추첨, 다음 단계 대진, AI끼리의 컵 경기 */
export function dailyCup(state: GameState, rng: Rng): void {
  if (state.day === CUP.drawDay) {
    state.cup = drawCup(state, rng);
    const mine = state.cup.teams.includes(state.playerLudusId);
    pushMessage(state, {
      kind: "info",
      title: mine ? "판테온 컵에 나간다!" : "판테온 컵 조 추첨",
      body: (mine ? `우리 루두스가 판테온 컵 출전권을 얻었다. 출전 수당 ${CUP.prize.entry.toLocaleString()} D가 들어왔다.\n` : "") +
        `여섯 나라 1부 상위 16곳이 네 조로 나뉘었다. 조별 리그는 ${CUP.groupDays.length}라운드, 상위 두 팀이 8강에 오른다.` +
        `\n대결마다 세 명씩 나와 미시오로 한 판씩 붙고, 두 판을 먼저 따면 이긴다.`,
    });
  }
  const cup = state.cup;
  if (!cup || cup.season !== state.season) return;
  advanceBracket(state, cup);
  const me = state.playerLudusId;
  for (const t of cup.ties) {
    if (t.day !== state.day || t.winner || t.home === me || t.away === me) continue;
    playTie(state, t.id, rng);
  }
}

/** 시즌 끝: 나라별 평균 점수를 계수에 넣고 오래된 시즌은 뺀다 */
export function closeCupSeason(state: GameState): void {
  const cup = state.cup;
  if (!cup || cup.season !== state.season) return;
  for (const n of NATIONS) {
    const teams = cup.teams.filter((id) => state.ludi[id]?.nation === n.id);
    const score = teams.length ? teams.reduce((s, id) => s + (cup.points[id] ?? 0), 0) / teams.length : 0;
    const hist = [...(state.coefficients[n.id] ?? []), Math.round(score * 10) / 10];
    state.coefficients[n.id] = hist.slice(-3);
  }
}
