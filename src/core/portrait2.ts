// 그림 초상화(파츠 조립) 사양. 같은 검투사는 늘 같은 얼굴이 나온다.
// 파츠 이미지와 놓을 자리는 tools/portrait-parts.mjs가 data/portrait2.json으로 만든다.
import PORTRAIT2 from "../../data/portrait2.json";
import { getArmor } from "./data";
import { ageStage } from "./gladiator";
import { Rng } from "./rng";
import type { Gladiator } from "./types";

type Rect = [number, number, number, number];
interface ViewData { heads: { chin?: number; neckX?: number; neckY?: number }[]; parts: Record<string, (Rect | null)[]> }
interface RaceData { heads: ("f" | "m")[]; front: ViewData; side: ViewData }
const DATA = PORTRAIT2 as unknown as { cell: number; races: Record<string, RaceData> };

export const PORTRAIT_CELL = DATA.cell;

export interface PaintedSpec {
  race: string;
  /** 0부터 */
  head: number;
  hair: string | null;
  beard: string | null;
  helmet: string | null;
  armor: string | null;
  /** 얼굴에 그리는 흉터·문신 ('mark_...' 파츠), 아래부터 그린다 */
  marks: string[];
  /** 머리카락·수염 색 (CSS filter). 없으면 그림 그대로 */
  tint?: string;
}

/** 자연스러운 머리색 바꾸기: 먼저 흑백으로 빼고 다시 물들여서, 원래 그림 색과 상관없이 같은 색이 나온다 */
export const HAIR_TINTS: Record<string, string> = {
  black: "grayscale(1) brightness(0.45)",
  dark: "grayscale(1) sepia(0.9) saturate(1.2) brightness(0.55)",
  brown: "grayscale(1) sepia(1) saturate(1.5) brightness(0.8)",
  auburn: "grayscale(1) sepia(1) saturate(3) hue-rotate(-14deg) brightness(0.85)",
  blond: "grayscale(1) sepia(1) saturate(1.8) brightness(1.35)",
  grey: "grayscale(1) brightness(1.45) contrast(0.9)",
};

export const hasPainted = (race: string, view: "front" | "side" = "front") => (DATA.races[race]?.[view].heads.length ?? 0) > 0;

export function paintedSpec(g: Gladiator): PaintedSpec | null {
  const rd = DATA.races[g.race];
  if (!rd || !rd.front.heads.length) return null;
  const r = new Rng(g.portraitSeed ^ 0x51ed27);
  const female = g.gender === "f";
  // 머리는 성별에 맞는 것 중에서 ('-'로 표시한 머리는 뽑지 않는다)
  const pool = rd.heads.map((s, i) => [s, i] as const).filter(([s]) => s === (female ? "f" : "m")).map(([, i]) => i);
  const head = r.pick(pool.length ? pool : rd.heads.map((_, i) => i));
  // 이 종족에 그림이 있는 파츠만 고른다. 같은 종류의 다른 세트('hair_short_2')도 후보에 넣는다
  const variants = (kind: string) => Object.keys(rd.front.parts).filter((k) => k === kind || (k.startsWith(kind + "_") && /^\d+$/.test(k.slice(kind.length + 1))));
  const has = (n: string | null) => !!n && variants(n).length > 0;
  const pickVariant = (kind: string | null) => { const v = kind ? variants(kind) : []; return v.length > 1 ? r.pick(v) : v[0] ?? null; };
  const only = (list: (string | null)[]) => { const ok = list.filter((n) => n === null || has(n)); return ok.length ? pickVariant(r.pick(ok)) : null; };
  // 투구는 반쯤만 쓴 모습으로 그린다 (얼굴과 머리 모양도 보이게). 밀폐 투구는 닫힌 투구, 나머지는 열린 투구 그림
  const helmetBase = r.chance(0.5) ? getArmor(g.equipment.helmet ?? "")?.id.split("+")[0] : undefined;
  const helmetWant = !helmetBase ? null : helmetBase === "sealed_helm" ? "helmet_visor" : "helmet_crest";
  const helmet = pickVariant(has(helmetWant) ? helmetWant : has("helmet_crest") && helmetWant ? "helmet_crest" : null);
  const hair = helmet ? null
    : female ? only(["hair_long", "hair_braids", "hair_short"])
    : only(["hair_short", "hair_short", "hair_long", "hair_braids", null]);
  // 수염은 남자만, 그것도 다는 아니다. 면갑을 쓰면 가려져서 뺀다
  const old = ageStage(g) === "decline";
  const beard = !female && !helmet?.startsWith("helmet_visor") && r.chance(old ? 0.75 : g.race === "dwarf" ? 0.85 : 0.45) ? only(["beard_full", "beard_short"]) : null;
  const weight = getArmor(g.equipment.armor)?.weight;
  const armorWant = weight === "heavy" || weight === "medium" ? "armor_plate" : weight === "light" && g.equipment.armor.split("+")[0] !== "loincloth" ? "armor_harness" : null;
  const armor = pickVariant(armorWant);
  // 머리색: 늙으면 희끗해지고, 나머지는 그림 그대로이거나 자연스러운 색 중 하나
  const tint = old && r.chance(0.7) ? "grey"
    : r.weighted({ none: 3, black: 2, dark: 2, brown: 2, auburn: 1, blond: 1.2 });
  return { race: g.race, head, hair, beard, helmet, armor, marks: pickMarks(g, Object.keys(rd.front.parts)), tint: tint === "none" ? undefined : HAIR_TINTS[tint] };
}

/**
 * 흉터·문신. 위 파츠와 다른 난수열을 써서 기존 얼굴 고르기가 바뀌지 않게 한다.
 * 문신은 처음부터 정해지고(검투사의 25%), 흉터는 싸운 횟수가 늘수록 생기며 한 번 생기면 남는다.
 */
function pickMarks(g: Gladiator, parts: string[]): string[] {
  const r = new Rng(g.portraitSeed ^ 0x3a7c15);
  const scars = parts.filter((k) => k.startsWith("mark_scar")), tattoos = parts.filter((k) => k.startsWith("mark_") && !k.startsWith("mark_scar"));
  const tattooRoll = r.next(), scarRoll = r.next();
  const tattoo = tattoos.length ? r.pick(tattoos) : null, scar = scars.length ? r.pick(scars) : null;
  const fights = g.record.wins + g.record.losses;
  const out: string[] = [];
  if (tattoo && tattooRoll < 0.25) out.push(tattoo);
  if (scar && scarRoll < Math.min(0.65, 0.12 + fights * 0.025)) out.push(scar);
  return out;
}

/** 한 시점에서 그릴 층 (아래부터). 얼굴 다시 그리기는 'face'로 표시 */
export interface Layer { src: string; rect: Rect; clipTop?: number; clipBottom?: number; filter?: string }

/** 닫힌 투구가 머리를 통째로 감싸는 종족: 투구 아래쪽 위로는 머리 그림을 지운다 (투구 옆으로 귀가 삐져나오지 않게) */
const VISOR_HIDES_HEAD = new Set(["human", "elf", "orc", "dwarf"]);

export function paintedLayers(spec: PaintedSpec, view: "front" | "side"): Layer[] | null {
  const vd = DATA.races[spec.race]?.[view];
  if (!vd || !vd.heads.length) return null;
  const dir = `portrait2/${spec.race}/${view}`;
  const C = DATA.cell;
  const headRect: Rect = [0, 0, C, C];
  const visor = view === "front" && spec.helmet?.startsWith("helmet_visor") && VISOR_HIDES_HEAD.has(spec.race) ? vd.parts[spec.helmet]?.[spec.head] : null;
  const out: Layer[] = [{ src: `${dir}/head_${spec.head + 1}.webp`, rect: headRect, clipTop: visor ? visor[1] + visor[3] * 0.7 : undefined }];
  const part = (name: string | null, filter?: string) => {
    const rect = name ? vd.parts[name]?.[spec.head] : null;
    if (name && rect) out.push({ src: `${dir}/${name}.webp`, rect, filter });
  };
  if (view === "front") {
    part(spec.armor);
    // 갑옷이 턱을 덮지 않게 얼굴(턱 줄 위)을 다시 그린다 (머리를 감싸는 투구면 필요 없다)
    if (spec.armor && !visor) out.push({ src: `${dir}/head_${spec.head + 1}.webp`, rect: headRect, clipBottom: vd.heads[spec.head].chin });
  }
  if (!visor) for (const m of spec.marks ?? []) part(m);
  part(spec.beard, spec.tint);
  // 위아래로 나눠 맞춘 수염의 아래(턱수염) 조각
  if (spec.beard) part(`${spec.beard}_lo`, spec.tint);
  part(spec.hair, spec.tint);
  part(spec.helmet);
  return out;
}

/** 측면 머리의 목 끝 (몸에 붙는 점), 셀 좌표 */
export function sideNeck(spec: PaintedSpec): { x: number; y: number } | null {
  const h = DATA.races[spec.race]?.side.heads[spec.head];
  return h?.neckX !== undefined ? { x: h.neckX, y: h.neckY! } : null;
}
