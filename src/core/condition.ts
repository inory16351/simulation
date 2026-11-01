// 검투사 컨디션: 사기·피로(체력)·경기감각·혹사도·급료 미납을 경기력 배율 하나로 모은다.
import { BALANCE } from "./data";
import type { DeathRule, Gladiator } from "./types";

const M = BALANCE.morale;

export interface PerfBreakdown {
  total: number;
  parts: { label: string; value: number }[];
}

/** 경기력 배율 (1 = 100%). 전투 시뮬레이션의 명중·피해에 곱해진다 */
export function performance(g: Gladiator): PerfBreakdown {
  const parts: { label: string; value: number }[] = [];
  const m = g.morale;
  const moraleMod = m >= 90 ? 0.1 : m >= 70 ? 0.05 : m >= 50 ? 0 : m >= 30 ? -0.1 : m >= 10 ? -0.25 : -0.4;
  if (moraleMod) parts.push({ label: "사기", value: moraleMod });
  const unpaid = M.unpaidPerf[Math.min(g.unpaidWeeks, M.unpaidPerf.length - 1)];
  if (unpaid) parts.push({ label: "급료 미납", value: -unpaid });
  if (g.sharpness < 40) parts.push({ label: "경기감각", value: -(40 - g.sharpness) * 0.004 });
  else if (g.sharpness >= 85) parts.push({ label: "경기감각", value: 0.03 });
  if (g.wear > 50) parts.push({ label: "혹사", value: -(g.wear - 50) * 0.004 });
  if (g.fatigue >= 70) parts.push({ label: "피로", value: -0.15 });
  if (g.injury?.severity === "light") parts.push({ label: "부상", value: -0.15 });
  const total = Math.max(0.4, 1 + parts.reduce((s, p) => s + p.value, 0));
  return { total, parts };
}

/** 탈진: 혹사도가 너무 높아 쉬게 해야 하는 상태 */
export const isJaded = (g: Gladiator) => g.wear >= 85;

/** 경기 직후 컨디션 변화 */
export function afterMatch(g: Gladiator, at: number, rule: DeathRule): void {
  const gap = g.lastFightAt === undefined ? 30 : at - g.lastFightAt;
  const r = BALANCE.rules[rule];
  g.fatigue = Math.min(100, g.fatigue + BALANCE.training.fightFatigue * r.fatigueMult);
  // 일주일 안에 다시 뛰면 혹사가 크게 쌓인다
  g.wear = Math.min(100, g.wear + (12 + Math.max(0, 7 - gap) * 5) * r.fatigueMult);
  g.sharpness = Math.min(100, g.sharpness + (rule === "training" ? 10 : 16));
  g.lastFightAt = at;
}

/** 하루치 회복과 감각 변화 */
export function dailyCondition(g: Gladiator, at: number, medicalLevel: number, bathLevel = 0, healMult = 1): void {
  const T = BALANCE.training;
  const resting = g.training.focus === "rest";
  const recover = T.fatigueRecoveryBase + g.attrs.rec * T.fatigueRecoveryPerRec + (resting ? T.restRecoveryBonus : 0) + bathLevel * 2;
  const load = resting ? 0 : T.intensity[g.training.intensity].fatigue;
  g.fatigue = Math.max(0, Math.min(100, g.fatigue + load - recover));
  g.wear = Math.max(0, g.wear - (1.2 + (resting ? 1.5 : 0) + bathLevel * 0.3));
  const idle = g.lastFightAt === undefined ? 99 : at - g.lastFightAt;
  if (idle > 5) g.sharpness = Math.max(0, g.sharpness - 1.2);
  else if (!resting) g.sharpness = Math.min(100, g.sharpness + 0.1);
  if (g.injury) {
    const speed = (g.race === "lizardfolk" ? 2 : 1) * (1 + (medicalLevel - 1) * 0.2) * healMult;
    g.injury.daysLeft -= speed;
    if (g.injury.daysLeft <= 0) g.injury = undefined;
  }
  // 사기는 평소 수준(60) 쪽으로 천천히 돌아간다
  const drift = BALANCE.morale.dailyDrift * (g.race === "lizardfolk" ? 0.5 : 1);
  if (g.morale > 62) g.morale -= drift;
  else if (g.morale < 58 && g.unpaidWeeks === 0) g.morale += drift;
}
