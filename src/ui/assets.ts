import { ageStage } from "../core/gladiator";
import type { Gladiator, NationId, RaceId } from "../core/types";

const BASE = import.meta.env.BASE_URL;
export const asset = (path: string) => `${BASE}assets/${path}`;

// 생성된 초상화 4종 중 성별에 맞는 변형. 3번은 노장, 4번은 어린 얼굴로 그렸다.
const VARIANTS: Record<RaceId, { m: number[]; f: number[]; old: number; young: number }> = {
  human: { m: [1, 3, 4], f: [2], old: 3, young: 4 },
  orc: { m: [1, 3, 4], f: [2], old: 3, young: 4 },
  minotaur: { m: [1, 2, 3, 4], f: [2, 4], old: 3, young: 4 },
  elf: { m: [1, 3, 4], f: [2, 4], old: 3, young: 4 },
  dwarf: { m: [1, 3, 4], f: [2], old: 3, young: 4 },
  lizardfolk: { m: [1, 2, 3, 4], f: [2, 4], old: 3, young: 4 },
  beastkin: { m: [1, 4], f: [2, 3], old: 3, young: 4 },
};

export function portraitUrl(g: Gladiator): string {
  const v = VARIANTS[g.race];
  const pool = v[g.gender ?? "m"];
  const stage = ageStage(g);
  let n: number;
  if (stage === "decline" && pool.includes(v.old)) n = v.old;
  else if ((stage === "growth" || stage === "youth") && pool.includes(v.young) && g.portraitSeed % 2 === 0) n = v.young;
  else {
    const rest = pool.filter((x) => x !== v.old || stage === "decline");
    n = rest[g.portraitSeed % rest.length];
  }
  return asset(`portrait/${g.race}/base_${n}.png`);
}

export const crestUrl = (n: NationId) => asset(`crest/${n}.png`);
export const menuIconUrl = (id: string) => asset(`icon/menu/${id}.png`);
