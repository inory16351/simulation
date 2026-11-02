// 부상: 부위별 종류, 부상 중 능력치 저하, 회복 후 영구 손상, 다친 채 출전 시 악화
import injuriesJson from "../../data/injuries.json";
import { logCareer, seasonRecord } from "./career";
import { getTrait } from "./data";
import type { Rng } from "./rng";
import { pushMessage } from "./state";
import { ATTR_NAMES, type AttrId, type GameState, type Gladiator, type Injury } from "./types";

export type InjuryTier = "minor" | "moderate" | "severe";
export type BodyArea = "head" | "eye" | "arm" | "leg" | "torso";

interface InjuryDef {
  id: string;
  name: string;
  part: BodyArea;
  tier: InjuryTier;
  days: [number, number];
  penalty: Partial<Record<AttrId, number>>;
  permanent: number;
  perm?: { attrs?: Partial<Record<AttrId, number>>; trait?: string };
}

export const INJURIES = injuriesJson as InjuryDef[];
const byId = Object.fromEntries(INJURIES.map((i) => [i.id, i]));
export const getInjury = (id?: string) => (id ? byId[id] : undefined);

export const TIER_NAMES: Record<InjuryTier, string> = { minor: "경상", moderate: "중상", severe: "치명상" };
export const PART_NAMES: Record<BodyArea, string> = { head: "머리", eye: "눈", arm: "팔", leg: "다리", torso: "몸통" };

/** 전투 부위 → 부상 부위 */
export function areaOf(hitPart?: string): BodyArea {
  switch (hitPart) {
    case "head": case "neck": return "head";
    case "arm": case "shoulder": return "arm";
    case "thigh": case "leg": return "leg";
    default: return "torso";
  }
}

/** 부상 하나를 만든다. 단계와 부위에 맞는 종류를 고른다 */
export function makeInjury(rng: Rng, tier: InjuryTier, area: BodyArea): Injury {
  let pool = INJURIES.filter((i) => i.tier === tier && (i.part === area || (area === "head" && i.part === "eye")));
  if (!pool.length) pool = INJURIES.filter((i) => i.tier === tier);
  const def = rng.pick(pool);
  const days = rng.int(def.days[0], def.days[1]);
  return {
    severity: tier === "minor" ? "light" : "serious",
    daysLeft: days,
    total: days,
    desc: def.name,
    type: def.id,
    tier,
  };
}

/** 부상 중 능력치 저하 */
export function injuryPenalty(g: Gladiator, a: AttrId): number {
  if (!g.injury) return 0;
  return getInjury(g.injury.type)?.penalty[a] ?? 0;
}

/** 다친 채로 출전하면 악화될 수 있다 */
export function aggravate(state: GameState, g: Gladiator, rng: Rng): void {
  const inj = g.injury;
  if (!inj || !rng.chance(0.3)) return;
  inj.daysLeft = Math.round(inj.daysLeft * 1.6 + 3);
  inj.total = Math.max(inj.total ?? 0, inj.daysLeft);
  logCareer(state, g, `부상(${inj.desc})을 안고 싸우다 상처가 덧났습니다`, "injury");
}

/**
 * 부상이 다 나을 때: 영구 손상 판정.
 * 의무실 단계와 메디쿠스 실력이 확률을 낮춘다.
 */
export function onHealed(state: GameState, g: Gladiator, rng: Rng, medicalLevel: number, medicusRating: number, mine: boolean): void {
  const def = getInjury(g.injury?.type);
  if (!def || !def.permanent || !def.perm) return;
  const chance = def.permanent === 1 ? 1 : def.permanent * (1 - (medicalLevel - 1) * 0.08) * (1 - medicusRating * 0.025) * (g.race === "dwarf" ? 0.6 : 1);
  if (!rng.chance(Math.max(0, chance))) return;
  const parts: string[] = [];
  for (const [a, v] of Object.entries(def.perm.attrs ?? {})) {
    const k = a as AttrId;
    g.attrs[k] = Math.max(1, g.attrs[k] + (v ?? 0));
    parts.push(`${ATTR_NAMES[k]} ${v}`);
  }
  if (def.perm.trait && !g.traits.includes(def.perm.trait)) {
    g.traits.push(def.perm.trait);
    if (g.traits.length > 6) g.traits.shift();
    parts.push(getTrait(def.perm.trait)?.name ?? def.perm.trait);
  }
  seasonRecord(state, g);
  logCareer(state, g, `${def.name}의 후유증이 남았습니다 (${parts.join(", ")})`, "injury");
  if (mine) {
    pushMessage(state, {
      kind: "warning",
      title: `${g.name}: 부상 후유증`,
      body: `${g.name}의 부상(${def.name})은 나았지만 후유증이 남았습니다.\n${def.perm.trait ? `새 특성: 「${getTrait(def.perm.trait)?.name}」` : "능력치가 영구히 떨어졌습니다."}`,
    });
  }
}

/** 영구 손상 위험 (UI 표시용) */
export function permanentRisk(g: Gladiator): number {
  return getInjury(g.injury?.type)?.permanent ?? 0;
}
