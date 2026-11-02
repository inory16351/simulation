// 전투 교범: 검투사 개인 전술. 상세 규칙은 docs/13_tactics.md
import type { Gladiator } from "./types";

export type Stance = "assault" | "balanced" | "bulwark" | "harass" | "showman" | "blitz";
export type RangePref = "style" | "close" | "mid" | "far";
export type Focus = "spread" | "head" | "body" | "legs" | "arm";

export type RuleCond =
  | { type: "myHpBelow"; value: number }
  | { type: "oppHpBelow"; value: number }
  | { type: "staBelow"; value: number }
  | { type: "oppDisabled" }
  | { type: "roundAtLeast"; value: number };

export type RuleAction =
  | { type: "stance"; stance: Stance }
  | { type: "skill"; skill: string }
  | { type: "rest" }
  | { type: "close" };

export interface DoctrineRule {
  cond: RuleCond;
  action: RuleAction;
}

export interface Doctrine {
  stance: Stance;
  range: RangePref;
  focus: Focus;
  /** 액티브 스킬 우선순위 (앞이 먼저) */
  order: string[];
  rules: DoctrineRule[];
  allowSurrender: boolean;
  /** 교범 숙련도 0~100 */
  familiarity: number;
}

export const STANCE_NAMES: Record<Stance, string> = {
  assault: "맹공", balanced: "균형", bulwark: "철벽", harass: "견제", showman: "쇼맨십", blitz: "속전속결",
};
export const STANCE_DESC: Record<Stance, string> = {
  assault: "쉴 새 없이 몰아칩니다. 방어는 뒷전",
  balanced: "상황을 보며 싸웁니다",
  bulwark: "막고 버티며 상대가 지치길 기다립니다. 받는 피해 감소",
  harass: "거리를 지키며 치고 빠집니다",
  showman: "관중을 홀립니다. 미시오에서 목숨을 건질 보험",
  blitz: "비싼 기술을 초반에 몰아 씁니다",
};
export const RANGE_NAMES: Record<RangePref, string> = {
  style: "스타일 기본", close: "파고들기", mid: "중거리 유지", far: "멀리서 견제",
};
export const FOCUS_NAMES: Record<Focus, string> = {
  spread: "고르게", head: "머리", body: "몸통", legs: "다리", arm: "무기 든 팔",
};
export const FOCUS_DESC: Record<Focus, string> = {
  spread: "효과 없음",
  head: "명중 -15%, 치명타 +10%, 기절 확률",
  body: "명중 +10%, 안정적인 피해",
  legs: "명중 -5%, 맞히면 30% 확률로 다리 부상(민첩 저하)",
  arm: "명중 -10%, 맞히면 30% 확률로 팔 부상(피해 -25%)",
};

export const COND_NAMES: Record<RuleCond["type"], string> = {
  myHpBelow: "내 HP가 N% 이하",
  oppHpBelow: "상대 HP가 N% 이하",
  staBelow: "내 스태미나가 N% 이하",
  oppDisabled: "상대가 묶이거나 넘어지면",
  roundAtLeast: "N라운드 이후",
};
export const ACTION_NAMES: Record<RuleAction["type"], string> = {
  stance: "자세 변경",
  skill: "기술 최우선",
  rest: "숨 고르기",
  close: "거리 좁히기",
};

export function describeRule(r: DoctrineRule, skillName: (id: string) => string): string {
  const c = r.cond;
  const cond =
    c.type === "myHpBelow" ? `내 HP ${c.value}% 이하`
    : c.type === "oppHpBelow" ? `상대 HP ${c.value}% 이하`
    : c.type === "staBelow" ? `내 스태미나 ${c.value}% 이하`
    : c.type === "oppDisabled" ? "상대가 묶이거나 넘어짐"
    : `${c.value}라운드 이후`;
  const a = r.action;
  const act =
    a.type === "stance" ? `${STANCE_NAMES[a.stance]} 자세로`
    : a.type === "skill" ? `${skillName(a.skill)} 최우선`
    : a.type === "rest" ? "숨 고르기"
    : "거리 좁히기";
  return `${cond} → ${act}`;
}

/** 스타일에 맞는 기본 교범 */
export function defaultDoctrine(g: Pick<Gladiator, "style" | "skills">, familiarity = 60): Doctrine {
  const order = [...g.skills.active];
  const base: Doctrine = { stance: "balanced", range: "style", focus: "spread", order, rules: [], allowSurrender: true, familiarity };
  const has = (s: string) => order.includes(s);
  switch (g.style) {
    case "murmillo":
    case "hoplomachus":
      base.stance = "bulwark";
      base.focus = "body";
      base.rules.push({ cond: { type: "oppHpBelow", value: 35 }, action: { type: "stance", stance: "assault" } });
      break;
    case "secutor":
      base.stance = "assault";
      base.range = "close";
      break;
    case "retiarius":
      base.stance = "harass";
      base.range = "mid";
      base.focus = "legs";
      if (has("skill.pierce")) base.rules.push({ cond: { type: "oppDisabled" }, action: { type: "skill", skill: "skill.pierce" } });
      break;
    case "thraex":
      base.focus = "arm";
      break;
    case "pankratiast":
      base.stance = "assault";
      base.range = "close";
      base.focus = "body";
      break;
    case "bestiarius":
      base.stance = "harass";
      base.range = "mid";
      break;
    case "crusher":
      base.stance = "assault";
      base.focus = "head";
      if (has("skill.execute")) base.rules.push({ cond: { type: "oppHpBelow", value: 25 }, action: { type: "skill", skill: "skill.execute" } });
      break;
    case "runeblade":
      base.stance = "blitz";
      break;
    case "sorcerer":
      base.stance = "harass";
      base.range = "far";
      break;
  }
  base.rules.push({ cond: { type: "staBelow", value: 20 }, action: { type: "rest" } });
  return base;
}

/** 교범을 바꾸면 숙련도가 떨어진다 */
export const FAMILIARITY_CHANGE_COST = 30;
