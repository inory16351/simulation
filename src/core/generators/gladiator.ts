import { BALANCE, NAMES, TRAITS, getArmor, getNation, getRace, STYLES, getStyle } from "../data";
import { computeCA } from "../gladiator";
import { defaultDoctrine } from "../tactics";
import type { Rng } from "../rng";
import {
  ATTRS,
  WEAPON_CLASSES,
  type AttrId,
  type Gladiator,
  type NationId,
  type RaceId,
  type Status,
  type WeaponClassId,
} from "../types";

export interface GenOptions {
  id: string;
  nation: NationId;
  /** 목표 평균 능력치 (1~20). CA 대략 (target-1)/19*200 */
  target: number;
  race?: RaceId;
  style?: string;
  status?: Status;
  /** 이미 쓰인 이름. 겹치면 성을 붙이고, 생성한 이름을 추가한다 */
  usedNames?: Set<string>;
}

function uniqueName(rng: Rng, raceId: RaceId, gender: "m" | "f", used?: Set<string>): string {
  const female = new Set(NAMES.female);
  const pool = NAMES.gladiator[raceId].filter((n) => female.has(n) === (gender === "f"));
  let name = rng.pick(pool);
  for (let i = 0; used?.has(name) && i < 30; i++) {
    name = `${rng.pick(pool)} ${rng.pick(NAMES.surname[raceId])}`;
  }
  used?.add(name);
  return name;
}

const clamp = (v: number, lo: number, hi: number) => Math.max(lo, Math.min(hi, v));

export function generateGladiator(rng: Rng, opt: GenOptions): Gladiator {
  const nation = getNation(opt.nation);
  const raceId = opt.race ?? rng.weighted<RaceId>(nation.races);
  const race = getRace(raceId);
  const style = getStyle(opt.style ?? pickStyle(rng, raceId));

  // 나이: 데뷔 ~ 전성기 끝 사이에서 성장기 쪽으로 치우치게
  const ageSpan = race.age.primeEnd + 2 - race.age.debut;
  const age = race.age.debut + Math.floor(Math.pow(rng.next(), 1.4) * ageSpan);

  const attrs = {} as Record<AttrId, number>;
  for (const a of ATTRS) {
    const styleBoost = (style.weights[a] ?? 0) * 0.8;
    const v = rng.normal(opt.target + styleBoost, 2.2) + (race.attrMods[a] ?? 0);
    attrs[a] = clamp(Math.round(v), 1, 20);
  }
  // 마력은 대부분 낮게. 마법 스타일이나 엘프만 높게 시작
  const magical = style.weights.mag !== undefined || raceId === "elf";
  if (!magical) attrs.mag = clamp(Math.round(rng.range(0, 6)) + (race.attrMods.mag ?? 0), 1, 20);

  const prof = {} as Record<WeaponClassId, number>;
  for (const w of WEAPON_CLASSES) {
    const main = style.profs.includes(w);
    prof[w] = clamp(Math.round(main ? rng.normal(opt.target + 1.5, 2) : rng.range(1, opt.target * 0.6)), 1, 20);
  }

  const traits = pickTraits(rng, rng.chance(0.55) ? 2 : 1);
  const gender = rng.chance(0.25) ? "f" : "m";

  const g: Gladiator = {
    id: opt.id,
    name: uniqueName(rng, raceId, gender, opt.usedNames),
    gender,
    race: raceId,
    nation: opt.nation,
    age,
    status: opt.status ?? (rng.chance(0.7) ? "slave" : "free"),
    style: style.id,
    attrs,
    prof,
    hidden: {
      pa: 0,
      consistency: rng.int(4, 18),
      bigStage: rng.int(3, 18),
      injuryProne: rng.int(2, 16),
      loyalty: rng.int(4, 18),
      ambition: rng.int(3, 18),
      greed: rng.int(3, 18),
    },
    traits,
    skills: { active: pickSkills(rng, style.skills, raceId, opt.target), passive: [] },
    doctrine: undefined as unknown as Gladiator["doctrine"],
    equipment: fixLoadout(raceId, { ...style.loadout }),
    wage: 0,
    unpaidWeeks: 0,
    morale: rng.int(50, 75),
    popularity: clamp(Math.round(rng.normal(opt.target * 2, 8)), 0, 60),
    fatigue: 0,
    record: { wins: 0, losses: 0, kills: 0 },
    alive: true,
    portraitSeed: rng.int(0, 1_000_000),
    training: { focus: "physical", intensity: "normal" },
    growth: {},
    sharpness: rng.int(45, 80),
    wear: rng.int(0, 15),
    history: [],
    career: [],
  };

  g.doctrine = defaultDoctrine(g, rng.int(40, 90));
  const ca = computeCA(g);
  const stageGap = age <= race.age.growthEnd ? rng.range(15, 70) : rng.range(0, 20);
  g.hidden.pa = clamp(Math.round(ca + stageGap), ca, 200);
  g.wage = computeWage(g, ca);
  if (g.popularity >= 40 && rng.chance(0.5)) g.nickname = rng.pick(NAMES.epithet);
  return g;
}

function pickStyle(rng: Rng, raceId: RaceId): string {
  const weights: Record<string, number> = {};
  for (const s of STYLES) weights[s.id] = 1;
  // 종족별 어울리는 스타일 가중치
  const affinity: Record<RaceId, string[]> = {
    human: ["murmillo", "secutor", "thraex", "retiarius"],
    orc: ["crusher", "secutor", "pankratiast"],
    minotaur: ["crusher", "pankratiast"],
    elf: ["thraex", "runeblade", "sorcerer", "dimachaerus"],
    dwarf: ["murmillo", "crusher", "hoplomachus"],
    lizardfolk: ["retiarius", "hoplomachus", "dimachaerus"],
    beastkin: ["bestiarius", "dimachaerus", "thraex"],
  };
  for (const s of affinity[raceId]) weights[s] += 4;
  if (raceId === "minotaur" || raceId === "orc") {
    weights.sorcerer = 0.1;
    weights.runeblade = 0.2;
  }
  return rng.weighted(weights);
}

function pickTraits(rng: Rng, n: number): string[] {
  const pool: Record<string, number> = {};
  for (const t of TRAITS) if (t.spawn > 0) pool[t.id] = t.spawn;
  const out: string[] = [];
  while (out.length < n) {
    const t = rng.weighted(pool);
    delete pool[t];
    out.push(t);
  }
  return out;
}

function pickSkills(rng: Rng, styleSkills: string[], raceId: RaceId, target: number): string[] {
  // 실력이 높을수록 스킬을 많이 안다 (최대 4칸)
  const count = clamp(Math.round(target / 5), 1, 3);
  const out = styleSkills.slice(0, count);
  if (target >= 9 && rng.chance(0.5)) out.push(getRace(raceId).raceSkill);
  return out.slice(0, 4);
}

function fixLoadout(raceId: RaceId, l: Gladiator["equipment"]): Gladiator["equipment"] {
  // 미노타우로스는 경량 방어구 착용 불가 (거구)
  if (raceId === "minotaur" && getArmor(l.armor)?.weight === "light") l.armor = "chainmail";
  return l;
}

export function computeWage(g: Gladiator, ca: number): number {
  const w = BALANCE.economy.wage;
  const race = getRace(g.race);
  const base = g.status === "slave" ? w.slaveMin + (ca / 200) * (w.slaveMax - w.slaveMin) * 3 : ca * w.perCa;
  return Math.max(w.slaveMin, Math.round(base * race.wageMult * (1 + g.popularity / 200)));
}
