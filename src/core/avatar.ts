// 초상화 사양: 실크송·림월드풍 단순 캐릭터. 그림은 UI(SVG)가 그리고, 여기서는 무엇을 그릴지만 정한다.
// Unity도 같은 사양으로 같은 캐릭터를 그릴 수 있다.
import { getArmor } from "./data";
import { ageStage } from "./gladiator";
import { Rng } from "./rng";
import type { Gladiator, PortraitParts, RaceId } from "./types";

export const HAIR_STYLES = ["tuft", "short", "long", "pony", "mohawk", "bun", "braids", "messy"] as const;
export const EYE_STYLES = ["round", "fierce", "sleepy", "wide"] as const;
export const MOUTH_STYLES = ["line", "smile", "grit", "frown", "open"] as const;
export const BEARD_STYLES = ["stubble", "short", "full", "braided", "goatee"] as const;
export const HELMET_STYLES = ["crest", "horned", "cap", "hood"] as const;

export type HairStyle = (typeof HAIR_STYLES)[number];
export type EyeStyle = (typeof EYE_STYLES)[number];

export interface AvatarSpec {
  race: RaceId;
  /** 종족 머리 변형 (사자/자칼 등) */
  variant: number;
  skin: string;
  hair: HairStyle | null;
  hairColor: string;
  eyes: EyeStyle;
  eyeColor: string;
  mouth: (typeof MOUTH_STYLES)[number] | null;
  beard: (typeof BEARD_STYLES)[number] | null;
  helmet: (typeof HELMET_STYLES)[number] | null;
  armor: "light" | "medium" | "heavy" | "robe";
  cloth: string;
  oneEyed: boolean;
  female: boolean;
  old: boolean;
}

export const SKINS: Record<RaceId, string[]> = {
  human: ["#f3d6bb", "#e2b28c", "#b97d52", "#7a4b2e"],
  elf: ["#f2ebe2", "#d7dceb", "#a9b6cf"],
  dwarf: ["#eab896", "#d39a73", "#b9805b"],
  orc: ["#8aa45a", "#738a4b", "#98987a"],
  minotaur: ["#3a2a22", "#6c4630", "#8b7865"],
  lizardfolk: ["#5f8a52", "#a08a40", "#4f7a74"],
  // 수인은 생김새(0: 사자, 1: 자칼)에 따라 털색이 다르다 → BEAST_SKINS
  beastkin: ["#d4a356", "#b0855a", "#c98b3e", "#2e2724", "#8a6a4a"],
};

export const HAIR_COLORS = ["#211915", "#4a2f1f", "#8a5a2b", "#d1ad5f", "#a8331f", "#e6e0d2"];
const EYE_COLORS = ["#2a1c14", "#3a5a7a", "#4a6a3a", "#6a3a1a"];
const CLOTH = ["#7a1e1a", "#3d4f6b", "#5b2a4e", "#4a5a3a", "#6b4a2a", "#2f2f35"];

/** 머리카락을 따로 그리지 않는 종족 */
const BEAST_SKINS = [["#d4a356", "#b0855a", "#c98b3e"], ["#2e2724", "#8a6a4a", "#5a4a3e"]];

const NO_HAIR = new Set<RaceId>(["minotaur", "lizardfolk", "beastkin"]);
const NO_HELMET = new Set<RaceId>(["minotaur", "lizardfolk", "beastkin"]);
const NO_MOUTH = new Set<RaceId>(["minotaur", "lizardfolk", "beastkin"]);

export function makeAvatar(g: Gladiator, seed = g.portraitSeed): AvatarSpec {
  const r = new Rng(seed ^ 0x2545f491);
  const old = ageStage(g) === "decline";
  const female = g.gender === "f";
  const weight = getArmor(g.equipment.armor)?.weight ?? "light";
  const armor = g.style === "sorcerer" || g.style === "runeblade" && r.chance(0.5) ? "robe" : weight;
  const canBeard = !female && !NO_HAIR.has(g.race) && g.race !== "elf";
  const variant = r.int(0, 1);
  return {
    race: g.race,
    variant,
    skin: g.race === "beastkin" ? r.pick(BEAST_SKINS[variant]) : r.pick(SKINS[g.race]),
    hair: NO_HAIR.has(g.race) ? null
      : r.chance(female ? 0.97 : 0.82) ? r.pick(female ? (["long", "pony", "bun", "braids"] as HairStyle[]) : HAIR_STYLES) : null,
    hairColor: old ? "#e6e0d2" : r.pick(HAIR_COLORS.slice(0, 5)),
    eyes: g.race === "orc" || g.race === "minotaur" ? (r.chance(0.6) ? "fierce" : "round") : old ? "sleepy" : r.pick(EYE_STYLES),
    eyeColor: r.pick(EYE_COLORS),
    mouth: NO_MOUTH.has(g.race) ? null : r.pick(MOUTH_STYLES),
    beard: g.race === "dwarf" && !female ? r.pick(["full", "braided"] as const)
      : canBeard && r.chance(old ? 0.8 : 0.4) ? r.pick(BEARD_STYLES) : null,
    helmet: g.equipment.helmet && !NO_HELMET.has(g.race) && r.chance(0.3) ? r.pick(HELMET_STYLES) : null,
    armor,
    cloth: r.pick(CLOTH),
    oneEyed: g.traits.includes("one_eyed"),
    female,
    old,
  };
}

/** 플레이어가 고친 부분을 덮어쓴 최종 사양 */
export function avatarOf(g: Gladiator): AvatarSpec {
  const base = makeAvatar(g);
  const o = g.portrait;
  if (!o) return base;
  return { ...base, ...(o as Partial<AvatarSpec>) };
}

/** (개발용) 고를 수 있는 항목 */
export function avatarOptions(g: Gladiator): { key: keyof PortraitParts & keyof AvatarSpec; label: string; values: (string | null)[] }[] {
  const out: { key: keyof PortraitParts & keyof AvatarSpec; label: string; values: (string | null)[] }[] = [
    { key: "skin", label: "피부", values: SKINS[g.race] },
    { key: "eyes", label: "눈", values: [...EYE_STYLES] },
  ];
  if (!NO_MOUTH.has(g.race)) out.push({ key: "mouth", label: "입", values: [null, ...MOUTH_STYLES] });
  if (!NO_HAIR.has(g.race)) {
    out.push({ key: "hair", label: "머리", values: [null, ...HAIR_STYLES] });
    out.push({ key: "hairColor", label: "머리색", values: HAIR_COLORS });
  }
  if (!NO_HAIR.has(g.race) && g.race !== "elf") out.push({ key: "beard", label: "수염", values: [null, ...BEARD_STYLES] });
  if (!NO_HELMET.has(g.race)) out.push({ key: "helmet", label: "투구", values: [null, ...HELMET_STYLES] });
  out.push({ key: "cloth", label: "옷 색", values: CLOTH });
  if (g.race === "beastkin" || g.race === "minotaur" || g.race === "lizardfolk") out.push({ key: "variant", label: "생김새", values: ["0", "1"] });
  return out;
}
