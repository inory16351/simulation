import injuriesJson from "../../data/injuries.json";
import { getRace, getStyle, getTrait } from "./data";
import { ATTRS, type AttrId, type Gladiator } from "./types";

/** 부상 종류별 능력치 저하 */
const INJURY_PENALTY = Object.fromEntries((injuriesJson as { id: string; penalty: Partial<Record<AttrId, number>> }[]).map((i) => [i.id, i.penalty]));

/** 특성·부상 보정을 포함한 실제 능력치 (1~20으로 제한) */
export function effectiveAttr(g: Gladiator, a: AttrId): number {
  let v = g.attrs[a];
  if (g.injury?.type) v += INJURY_PENALTY[g.injury.type]?.[a] ?? 0;
  for (const t of g.traits) v += getTrait(t)?.attrMods?.[a] ?? 0;
  return Math.max(1, Math.min(20, v));
}

/** 스타일 가중치로 계산한 현재 능력 CA (1~200) */
export function computeCA(g: Pick<Gladiator, "attrs" | "prof" | "style">): number {
  const style = getStyle(g.style);
  let wsum = 0;
  let total = 0;
  for (const a of ATTRS) {
    const w = 1 + (style.weights[a] ?? 0) * 2;
    wsum += w;
    total += g.attrs[a] * w;
  }
  // 주 무기군 숙련을 함께 반영
  for (const p of style.profs) {
    wsum += 4;
    total += g.prof[p] * 4;
  }
  const avg = total / wsum;
  return Math.max(1, Math.min(200, Math.round(((avg - 1) / 19) * 200)));
}

/** CA/PA → 별점 (0.5 단위, 0.5~5) */
export function stars(value: number): number {
  return Math.max(0.5, Math.min(5, Math.round((value / 40) * 2) / 2));
}

export type AgeStage = "youth" | "growth" | "prime" | "decline";

export function ageStage(g: Pick<Gladiator, "age" | "race">): AgeStage {
  const r = getRace(g.race).age;
  if (g.age < r.debut) return "youth";
  if (g.age <= r.growthEnd) return "growth";
  if (g.age <= r.primeEnd) return "prime";
  return "decline";
}

export const AGE_STAGE_NAMES: Record<AgeStage, string> = {
  youth: "유년기",
  growth: "성장기",
  prime: "전성기",
  decline: "쇠퇴기",
};

export function canFight(g: Gladiator): boolean {
  return g.alive && g.injury?.severity !== "serious" && ageStage(g) !== "youth";
}
