// 증축: 시설 건설·유지비·효과 (docs/06_staff_facilities.md)
import { absDay, dateLabel, fromAbs } from "./calendar";
import { addMoney } from "./economy";
import { pushMessage } from "./state";
import type { FacilityId, GameState, Ludus } from "./types";

export interface FacilityDef {
  id: FacilityId;
  name: string;
  desc: string;
  /** 레벨별 효과 설명 (0~5) */
  effect: (lvl: number) => string;
}

export const MAX_LEVEL = 5;

export const FACILITIES: FacilityDef[] = [
  { id: "barracks", name: "숙소", desc: "검투사가 먹고 자는 곳", effect: (l) => `정원 ${capacityAt(l)}명 · 사기 +${l * 0.5}/주` },
  { id: "training", name: "훈련 마당", desc: "모래를 깐 연습장과 허수아비", effect: (l) => `훈련 성장 +${(l - 1) * 10}% · 훈련 부상 -${(l - 1) * 5}%` },
  { id: "medical", name: "의무실", desc: "의원과 약초, 붕대", effect: (l) => `부상 회복 +${(l - 1) * 20}% · 사투 사망 -${l * 2}%p` },
  { id: "bath", name: "목욕탕", desc: "뜨거운 물과 마사지", effect: (l) => `피로 회복 +${l * 2}/일 · 혹사 회복 +${(l * 0.3).toFixed(1)}/일` },
  { id: "arena", name: "홈 경기장", desc: "우리 루두스의 작은 투기장", effect: (l) => `홈 관중 수입 ×${arenaTicketMult(l).toFixed(2)}` },
  { id: "academy", name: "유소년 학당", desc: "고아와 노예 아이를 검투사로 키우는 학당", effect: (l) => l ? `매 시즌 입학 ${l + 1}명 · 잠재력 상한 ★${(academyPaCap(l) / 40).toFixed(1)} · 정원 ${l * 3 + 3}명` : "없음" },
  { id: "guard", name: "경비 초소", desc: "탈주와 반란을 막는 경비대", effect: (l) => `탈주 -${l * 10}% · 반란 -${l * 3}%p` },
];

export const getFacility = (id: FacilityId) => FACILITIES.find((f) => f.id === id)!;

export const capacityAt = (lvl: number) => [8, 8, 12, 16, 20, 24][lvl] ?? 8;
export const arenaTicketMult = (lvl: number) => 0.7 + lvl * 0.15;
export const academyPaCap = (lvl: number) => [0, 120, 140, 160, 180, 200][lvl] ?? 0;

/** 다음 레벨로 올리는 비용·기간 */
export function upgradeCost(id: FacilityId, toLevel: number): { cost: number; days: number } {
  const base = [0, 1000, 2500, 6000, 12000, 25000][toLevel] ?? 0;
  const mult = id === "arena" ? 1.5 : 1;
  return { cost: Math.round(base * mult), days: [0, 10, 14, 21, 28, 42][toLevel] ?? 0 };
}

/** 주간 유지비 (급료일에 나간다) */
export function weeklyUpkeep(ludus: Ludus): number {
  return Object.values(ludus.facilities).reduce((s, lvl) => s + [0, 10, 20, 40, 70, 110][lvl], 0);
}

export function startUpgrade(state: GameState, ludus: Ludus, id: FacilityId): string | null {
  if (ludus.construction) return "이미 공사 중인 시설이 있습니다";
  const lvl = ludus.facilities[id];
  if (lvl >= MAX_LEVEL) return "이미 최고 단계입니다";
  const { cost, days } = upgradeCost(id, lvl + 1);
  if (ludus.treasury < cost) return "금고에 돈이 모자랍니다";
  addMoney(state, ludus, -cost, `${getFacility(id).name} 증축`);
  ludus.construction = { facility: id, toLevel: lvl + 1, doneAt: absDay(state.season, state.day) + days };
  return null;
}

/** 매일: 공사가 끝났는지. 완공된 것을 돌려준다 */
export function dailyConstruction(state: GameState): { ludus: Ludus; facility: FacilityId; level: number }[] {
  const now = absDay(state.season, state.day);
  const done: { ludus: Ludus; facility: FacilityId; level: number }[] = [];
  for (const l of Object.values(state.ludi)) {
    const c = l.construction;
    if (!c || c.doneAt > now) continue;
    l.facilities[c.facility] = c.toLevel;
    l.construction = undefined;
    done.push({ ludus: l, facility: c.facility, level: c.toLevel });
    if (l.isPlayer) {
      const f = getFacility(c.facility);
      pushMessage(state, { kind: "info", title: `${f.name} ${c.toLevel}단계 완공`, body: `${f.name} 공사가 끝났습니다.\n${f.effect(c.toLevel)}` });
    }
  }
  return done;
}

export function constructionLabel(state: GameState, ludus: Ludus): string | null {
  const c = ludus.construction;
  if (!c) return null;
  const left = c.doneAt - absDay(state.season, state.day);
  return `${getFacility(c.facility).name} ${c.toLevel}단계 공사 중 · ${left}일 남음 (${dateLabel(fromAbs(c.doneAt).day)} 완공)`;
}
