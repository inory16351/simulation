// 검투사 일대기: 시즌별 기록과 주요 사건
import { computeCA } from "./gladiator";
import type { CareerEvent, GameState, Gladiator, Ludus, SeasonRecord } from "./types";

export function ludusOf(state: GameState, gid: string): Ludus | undefined {
  return Object.values(state.ludi).find((l) => l.gladiators.includes(gid));
}

/** 이번 시즌 기록 줄을 찾고 없으면 만든다 */
export function seasonRecord(state: GameState, g: Gladiator): SeasonRecord {
  let rec = g.history.find((h) => h.season === state.season);
  if (!rec) {
    const l = ludusOf(state, g.id);
    const ca = computeCA(g);
    rec = {
      season: state.season, ludus: l?.name ?? "무소속", tier: l?.tier ?? 0,
      apps: 0, wins: 0, losses: 0, kills: 0, caStart: ca, caEnd: ca, popularity: g.popularity, injuries: 0, earnings: 0,
    };
    g.history.push(rec);
  }
  return rec;
}

export function logCareer(state: GameState, g: Gladiator, text: string, kind: CareerEvent["kind"] = "other"): void {
  g.career.push({ season: state.season, day: state.day, text, kind });
  if (g.career.length > 200) g.career.shift();
}

/** 시즌이 끝날 때 기록을 마감한다 */
export function closeSeason(state: GameState, g: Gladiator): void {
  const rec = seasonRecord(state, g);
  rec.caEnd = computeCA(g);
  rec.popularity = g.popularity;
}
