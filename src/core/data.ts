import racesJson from "../../data/races.json";
import nationsJson from "../../data/nations.json";
import stylesJson from "../../data/styles.json";
import itemsJson from "../../data/items.json";
import skillsJson from "../../data/skills.json";
import traitsJson from "../../data/traits.json";
import namesJson from "../../data/names.json";
import balanceJson from "../../data/balance.json";
import type { AttrId, NationId, RaceId, WeaponClassId } from "./types";

export interface RaceDef {
  id: RaceId;
  name: string;
  color: string;
  attrMods: Partial<Record<AttrId, number>>;
  age: { debut: number; growthEnd: number; primeEnd: number; retire: number };
  growthMult: number;
  wageMult: number;
  upkeepMult: number;
  racialTrait: { name: string; desc: string };
  raceSkill: string;
}

export interface NationDef {
  id: NationId;
  name: string;
  short: string;
  color: string;
  races: Partial<Record<RaceId, number>>;
  culture: string;
  relations: Partial<Record<NationId, number>>;
}

export interface StyleDef {
  id: string;
  name: string;
  desc: string;
  weights: Partial<Record<AttrId, number>>;
  loadout: { main: string; off: string | null; armor: string; helmet: string | null };
  profs: WeaponClassId[];
  preferredRange: number;
  aggression: number;
  counters: string[];
  skills: string[];
}

export interface WeaponDef {
  id: string;
  name: string;
  class: WeaponClassId;
  damage: number;
  speed: number;
  pierce: number;
  range: number;
  reqStr: number;
  crit: number;
  blockBypass?: number;
  stun?: number;
  boundBonus?: number;
  ammo?: number;
  magicBonus?: number;
  twoHanded?: boolean;
  offhandOnly?: boolean;
}

export interface ArmorDef {
  id: string;
  name: string;
  slot: "armor" | "helmet" | "shield";
  weight?: "light" | "medium" | "heavy";
  defense?: number;
  agiPenalty?: number;
  staCost?: number;
  crowd?: number;
  accPenalty?: number;
  netResist?: number;
  showBonus?: number;
  block?: number;
}

export interface StatusApply {
  status: string;
  turns: number;
  chance: number;
}

export interface SkillDef {
  id: string;
  name: string;
  kind: "attack" | "control" | "move" | "self" | "passive";
  cost?: number;
  mana?: number;
  cd?: number;
  mult?: number;
  hits?: number;
  hitMod?: number;
  pierceAdd?: number;
  critAdd?: number;
  requireTargetHpBelow?: number;
  requireShield?: boolean;
  requireNet?: boolean;
  ranged?: boolean;
  useAmmo?: boolean;
  useGrapple?: boolean;
  closeIn?: boolean;
  fromAnyRange?: boolean;
  autoHit?: boolean;
  magic?: boolean;
  magicDamage?: number;
  apply?: StatusApply;
  self?: { status: string; turns: number };
  crowd?: number;
  enemyMomentum?: number;
  selfMomentum?: number;
  restoreSta?: number;
  race?: RaceId;
  req?: Partial<Record<AttrId, number>>;
}

export interface TraitDef {
  id: string;
  name: string;
  cat: string;
  desc: string;
  attrMods?: Partial<Record<AttrId, number>>;
  spawn: number;
  hidden?: boolean;
  negative?: boolean;
}

export const RACES = racesJson as RaceDef[];
export const NATIONS = nationsJson as NationDef[];
export const STYLES = stylesJson as StyleDef[];
export const WEAPONS = itemsJson.weapons as WeaponDef[];
export const ARMORS = itemsJson.armors as ArmorDef[];
export const SKILLS = skillsJson as SkillDef[];
export const TRAITS = traitsJson as TraitDef[];
export const NAMES = namesJson as {
  gladiator: Record<RaceId, string[]>;
  surname: Record<RaceId, string[]>;
  epithet: string[];
  ludusPrefix: string[];
  ludusSuffix: string[];
};
export const BALANCE = balanceJson;

function index<T extends { id: string }>(arr: T[]): Record<string, T> {
  return Object.fromEntries(arr.map((x) => [x.id, x]));
}

const raceIdx = index(RACES);
const nationIdx = index(NATIONS);
const styleIdx = index(STYLES);
const weaponIdx = index(WEAPONS);
const armorIdx = index(ARMORS);
const skillIdx = index(SKILLS);
const traitIdx = index(TRAITS);

export const getRace = (id: RaceId) => raceIdx[id];
export const getNation = (id: NationId) => nationIdx[id];
export const getStyle = (id: string) => styleIdx[id];
export const getWeapon = (id: string): WeaponDef | undefined => weaponIdx[id];
export const getArmor = (id: string): ArmorDef | undefined => armorIdx[id];
export const getSkill = (id: string) => skillIdx[id];
export const getTrait = (id: string) => traitIdx[id];
