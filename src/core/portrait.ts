// 초상화 파츠 고르기. 새 검투사가 생길 때 종족·성별·나이·장비에 맞춰 무작위로 조합한다.
import partsJson from "../../data/portrait-parts.json";
import { getArmor } from "./data";
import { ageStage } from "./gladiator";
import { RACE_FACE, DEFAULT_FACE, SLOT_ORDER, layoutPortrait, type Box, type PartSlot } from "./portraitLayout";
import { Rng } from "./rng";
import type { Gladiator, PortraitParts } from "./types";

export interface PartDef {
  id: string;
  slot: PartSlot;
  file: string;
  w: number;
  h: number;
  races?: string[];
  gender?: "m" | "f";
  tags?: string[];
  weight?: "light" | "medium" | "heavy";
}

export const PARTS = partsJson as PartDef[];
const byId = Object.fromEntries(PARTS.map((p) => [p.id, p]));
export const getPart = (id: string | null | undefined) => (id ? byId[id] : undefined);

/** 얼굴에 입·머리카락을 따로 붙이지 않는 종족 (주둥이·갈기가 머리 그림에 있다) */
const NO_MOUTH = new Set(["minotaur", "lizardfolk", "beastkin"]);
const NO_HAIR = new Set(["minotaur", "lizardfolk", "beastkin"]);

/** 이 검투사가 쓸 수 있는 슬롯별 파츠 목록 */
export function allowedParts(g: Pick<Gladiator, "race" | "gender">, slot: PartSlot): PartDef[] {
  if (slot === "mouth" && NO_MOUTH.has(g.race)) return [];
  if (slot === "hair" && NO_HAIR.has(g.race)) return [];
  return PARTS.filter(
    (p) => p.slot === slot && (!p.races || p.races.includes(g.race)) && (!p.gender || p.gender === g.gender),
  );
}

export const OPTIONAL_SLOTS: PartSlot[] = ["hair", "beard", "helmet"];

export function pickPortrait(g: Gladiator, seed = g.portraitSeed): PortraitParts {
  const rng = new Rng(seed ^ 0x51ed270b);
  const old = ageStage(g) === "decline";
  const pick = (slot: PartSlot, prefer?: (p: PartDef) => boolean): string | null => {
    const list = allowedParts(g, slot);
    if (!list.length) return null;
    const preferred = prefer ? list.filter(prefer) : [];
    if (preferred.length && rng.chance(0.75)) return rng.pick(preferred).id;
    // 노인용 파츠는 노장에게만
    const normal = list.filter((p) => old || !p.tags?.includes("old"));
    return rng.pick(normal.length ? normal : list).id;
  };

  const out: PortraitParts = {};
  const weight = getArmor(g.equipment.armor)?.weight ?? "light";
  out.body = pick("body", (p) =>
    g.style === "sorcerer" ? p.id === "body.robe"
    : (g.race === "minotaur" || g.race === "orc") && weight === "heavy" ? p.id === "body.fur_plate"
    : p.weight === weight && p.id !== "body.robe");
  out.head = pick("head");
  out.eyes = g.traits.includes("one_eyed")
    ? allowedParts(g, "eyes").find((p) => p.tags?.includes("one_eyed"))?.id ?? pick("eyes")
    : pick("eyes", (p) => (g.race === "lizardfolk" ? p.id === "eyes.reptile" : old ? !!p.tags?.includes("old") : !p.tags?.includes("one_eyed")));
  out.mouth = pick("mouth", (p) => (g.race === "orc" ? !!p.races?.includes("orc") : true));
  out.hair = rng.chance(g.gender === "f" ? 0.95 : 0.8) ? pick("hair", (p) => (old ? !!p.tags?.includes("old") : !p.tags?.includes("old"))) : null;
  out.beard = g.gender === "m" && rng.chance(g.race === "dwarf" ? 0.95 : 0.45) ? pick("beard", (p) => (old ? !!p.tags?.includes("old") : !p.tags?.includes("old"))) : null;
  out.helmet = g.equipment.helmet && rng.chance(0.35) ? pick("helmet") : null;
  return out;
}

/** 저장된 선택이 있으면 쓰고, 없으면 시드로 조합한다 */
export function portraitOf(g: Gladiator): PortraitParts {
  return g.portrait ?? pickPortrait(g);
}

export interface PlacedPart {
  slot: PartSlot;
  part: PartDef;
  box: Box;
}

export function placePortrait(g: Gladiator): PlacedPart[] {
  const sel = portraitOf(g);
  const metas: Partial<Record<PartSlot, PartDef>> = {};
  for (const [slot, id] of Object.entries(sel) as [PartSlot, string | null][]) {
    const p = getPart(id);
    if (p) metas[slot] = p;
  }
  // 투구를 쓰면 머리카락은 가린다
  if (metas.helmet) delete metas.hair;
  if (!metas.head) return [];
  const layout = layoutPortrait(metas, RACE_FACE[g.race] ?? DEFAULT_FACE);
  return SLOT_ORDER.filter((slot) => layout[slot]).map((slot) => ({ slot, part: metas[slot]!, box: layout[slot]! }));
}
