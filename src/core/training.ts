// 연무장 훈련과 성장. 하루치 훈련, 실전 경험치를 같은 성장 함수로 처리한다.
import { BALANCE, getRace, getStyle } from "./data";
import { ageStage, computeCA } from "./gladiator";
import { makeInjury } from "./injuries";
import type { Rng } from "./rng";
import { MENTAL, PHYSICAL, SENSE, type Gladiator, type TrainingFocus, type TrainingIntensity, type WeaponClassId } from "./types";

const T = BALANCE.training;

export const FOCUS_NAMES: Record<TrainingFocus, string> = {
  physical: "체력 단련", weapon: "무기술", defense: "방어", grapple: "격투", mental: "정신 수양", sense: "실전 감각",
  magic: "주술", show: "쇼맨십", doctrine: "교범 수련", rest: "휴식",
};
export const FOCUS_DESC: Record<TrainingFocus, string> = {
  physical: "근력·민첩·지구력·강인함·반응·회복력",
  weapon: "주무기 숙련과 정확도",
  defense: "회피·막기·방패 숙련",
  grapple: "제압·맨손 숙련",
  mental: "투지·침착·판단력·규율",
  sense: "예측력·기만·천재성·적응력·팀워크·타이밍·마무리 본능·위압감 (대련)",
  magic: "마력·주술 촉매 숙련 (마력이 있어야 효과)",
  show: "쇼맨십",
  doctrine: "전투 교범 숙련도",
  rest: "성장 없음. 피로·혹사 회복이 빠르다",
};
export const INTENSITY_NAMES: Record<TrainingIntensity, string> = { low: "가볍게", normal: "보통", high: "강하게", extreme: "극한" };

type Key = string; // 능력치 id 또는 "prof:무기군"

function focusTargets(g: Gladiator, focus: TrainingFocus | "match"): Record<Key, number> {
  const style = getStyle(g.style);
  const t: Record<Key, number> = {};
  const add = (k: Key, w = 1) => (t[k] = (t[k] ?? 0) + w);
  switch (focus) {
    case "physical": PHYSICAL.forEach((a) => add(a, 1 + (style.weights[a] ?? 0))); break;
    case "weapon": style.profs.forEach((p) => add(`prof:${p}`, 3)); add("acc", 2); break;
    case "defense": add("eva", 2); add("blk", 2); add("prof:shield", 1); break;
    case "grapple": add("grp", 3); add("prof:unarmed", 2); break;
    case "mental": ["grit", "comp", "dec", "disc"].forEach((a) => add(a)); break;
    case "sense": SENSE.forEach((a) => add(a)); break;
    case "magic": if (g.attrs.mag > 1) { add("mag", 3); add("prof:focus", 1); } break;
    case "show": add("show", 3); break;
    case "match":
      // 실전은 스타일 핵심 능력치와 주무기, 정신력을 고르게 키운다
      for (const [a, w] of Object.entries(style.weights)) add(a, w ?? 1);
      style.profs.forEach((p) => add(`prof:${p}`, 2));
      add("grit"); add("comp"); add("dec");
      // 실전에서만 제대로 느는 감각
      add("read", 0.6); add("adapt", 0.6); add("timing", 0.6); add("kill", 0.4);
      break;
  }
  return t;
}

function bump(g: Gladiator, key: Key, delta: number): void {
  if (key.startsWith("prof:")) {
    const p = key.slice(5) as WeaponClassId;
    g.prof[p] = Math.max(1, Math.min(20, g.prof[p] + delta));
  } else {
    const a = key as keyof Gladiator["attrs"];
    g.attrs[a] = Math.max(1, Math.min(20, g.attrs[a] + delta));
  }
}

/**
 * 성장 적용. days는 '보통 강도 훈련 며칠분'인지.
 * 쇠퇴기에는 신체가 떨어지고 정신은 조금 오른다.
 */
export function grow(g: Gladiator, rng: Rng, focus: TrainingFocus | "match", days: number, intensityMult = 1, facilityLevel = 1): void {
  const stage = ageStage(g);
  const race = getRace(g.race);
  const ca = computeCA(g);
  let amount = T.dailyBase * days * intensityMult * (1 + (facilityLevel - 1) * T.facilityBonus);
  amount *= 0.7 + g.attrs.disc / 40;
  amount *= race.growthMult;
  if (g.traits.includes("ambitious")) amount *= 1.15;
  if (g.traits.includes("prodigy") && stage === "growth") amount *= 1.3;

  if (stage === "decline") {
    // 쇠퇴: 신체 능력치는 떨어지고, 노련함이 쌓인다
    const loss = amount * Math.abs(T.ageMult.decline) * (g.traits.includes("veteran") ? 0.5 : 1);
    accumulate(g, rng, Object.fromEntries(PHYSICAL.map((a) => [a, 1])), -loss);
    accumulate(g, rng, Object.fromEntries(MENTAL.slice(0, 3).map((a) => [a, 1])), amount * 0.3);
    return;
  }
  const paRoom = Math.max(0, Math.min(1, (g.hidden.pa - ca) / 40));
  if (paRoom <= 0) return;
  amount *= T.ageMult[stage] * Math.max(0.1, paRoom);
  const targets = focusTargets(g, focus);
  if (Object.keys(targets).length) accumulate(g, rng, targets, amount);
}

function accumulate(g: Gladiator, rng: Rng, targets: Record<Key, number>, amount: number): void {
  // 성장량을 목표 능력치들에 가중치대로 나눠 누적한다
  const total = Object.values(targets).reduce((s, w) => s + w, 0);
  for (const [k, w] of Object.entries(targets)) {
    const share = amount * (w / total) * (0.6 + rng.next() * 0.8);
    const v = (g.growth[k] ?? 0) + share;
    if (v >= 1) { bump(g, k, 1); g.growth[k] = v - 1; }
    else if (v <= -1) { bump(g, k, -1); g.growth[k] = v + 1; }
    else g.growth[k] = v;
  }
}

/** 하루치 훈련. 훈련 중 부상이 나면 설명을 돌려준다 */
export function dailyTraining(g: Gladiator, rng: Rng, facilityLevel: number, nationGrowth = 1): string | undefined {
  const plan = g.training;
  // 급료가 세 번 넘게 밀리면 태업한다
  if (plan.focus === "rest" || g.injury?.severity === "serious" || g.unpaidWeeks >= 3) return undefined;
  const intensity = T.intensity[plan.intensity];
  if (plan.focus === "doctrine") {
    g.doctrine.familiarity = Math.min(100, g.doctrine.familiarity + T.familiarityPerDay * intensity.growth);
  } else {
    // 교범 숙련은 평소 훈련으로도 조금씩 오른다
    g.doctrine.familiarity = Math.min(100, g.doctrine.familiarity + 0.3);
    grow(g, rng, plan.focus, 1, intensity.growth * nationGrowth, facilityLevel);
  }
  if (plan.intensity === "extreme") g.morale = Math.max(0, g.morale - 0.4);
  const injuryChance = intensity.injury * (g.traits.includes("injury_prone") ? 1.5 : 1) * (1 - (facilityLevel - 1) * 0.05);
  if (!g.injury && rng.chance(injuryChance)) {
    g.injury = makeInjury(rng, "minor", rng.pick(["arm", "leg", "torso"] as const));
    return g.injury.desc;
  }
  return undefined;
}
