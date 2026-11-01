import type { Doctrine } from "./tactics";

export type Id = string;

export const PHYSICAL = ["str", "agi", "end", "tou", "rea", "rec"] as const;
export const TECHNICAL = ["acc", "eva", "blk", "grp", "mag"] as const;
export const MENTAL = ["grit", "comp", "dec", "show", "agg", "disc", "lead"] as const;
export const ATTRS = [...PHYSICAL, ...TECHNICAL, ...MENTAL] as const;
export type AttrId = (typeof ATTRS)[number];

export const ATTR_NAMES: Record<AttrId, string> = {
  str: "근력", agi: "민첩", end: "지구력", tou: "강인함", rea: "반응", rec: "회복력",
  acc: "정확도", eva: "회피", blk: "막기", grp: "제압", mag: "마력",
  grit: "투지", comp: "침착", dec: "판단력", show: "쇼맨십", agg: "공격성", disc: "규율", lead: "리더십",
};

export const WEAPON_CLASSES = [
  "sword", "greatsword", "axe", "mace", "spear", "trident_net", "dagger", "throwing", "shield", "unarmed", "focus",
] as const;
export type WeaponClassId = (typeof WEAPON_CLASSES)[number];

export const WEAPON_CLASS_NAMES: Record<WeaponClassId, string> = {
  sword: "한손검", greatsword: "대검", axe: "도끼", mace: "둔기", spear: "창", trident_net: "삼지창·그물",
  dagger: "단검", throwing: "투척", shield: "방패", unarmed: "맨손", focus: "주술 촉매",
};

export type RaceId = "human" | "orc" | "minotaur" | "elf" | "dwarf" | "lizardfolk" | "beastkin";
export type NationId = "aurelia" | "grommar" | "sylvaren" | "dolgar" | "karsea" | "kemer";
export type DeathRule = "training" | "missio" | "sudden";
export type Status = "slave" | "free" | "freedman";

export interface Hidden {
  pa: number;
  consistency: number;
  bigStage: number;
  injuryProne: number;
  loyalty: number;
  ambition: number;
  greed: number;
}

export interface Injury {
  severity: "light" | "serious";
  weeksLeft: number;
  desc: string;
}

export interface Gladiator {
  id: Id;
  name: string;
  nickname?: string;
  gender: "m" | "f";
  race: RaceId;
  nation: NationId;
  age: number;
  status: Status;
  style: string;
  attrs: Record<AttrId, number>;
  prof: Record<WeaponClassId, number>;
  hidden: Hidden;
  traits: string[];
  skills: { active: string[]; passive: string[] };
  equipment: { main: string; off: string | null; armor: string; helmet: string | null };
  wage: number;
  unpaidWeeks: number;
  morale: number;
  popularity: number;
  fatigue: number;
  injury?: Injury;
  record: { wins: number; losses: number; kills: number };
  alive: boolean;
  diedAt?: { season: number; day: number; cause: string };
  doctrine: Doctrine;
  portraitSeed: number;
}

export interface Ludus {
  id: Id;
  name: string;
  nation: NationId;
  tier: 1 | 2;
  rp: number;
  treasury: number;
  gladiators: Id[];
  isPlayer: boolean;
  aiPersonality?: "aggressive" | "stable" | "grudge" | "merchant" | "developer";
  record: { wins: number; losses: number };
}

export interface InboxMessage {
  id: Id;
  season: number;
  day: number;
  kind: "info" | "match" | "event" | "warning" | "death";
  title: string;
  body: string;
  read: boolean;
}

export interface GameState {
  version: number;
  seed: number;
  rngState: number;
  season: number;
  /** 시즌 안의 날짜 1~168 */
  day: number;
  nation: NationId;
  playerLudusId: Id;
  ludi: Record<Id, Ludus>;
  gladiators: Record<Id, Gladiator>;
  inbox: InboxMessage[];
  /** 플레이어 루두스에서 죽은 검투사 (명예의 전당·로스터 표시용) */
  fallen: Id[];
  nextId: number;
}
