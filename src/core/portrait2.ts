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
}

export const hasPainted = (race: string, view: "front" | "side" = "front") => (DATA.races[race]?.[view].heads.length ?? 0) > 0;

export function paintedSpec(g: Gladiator): PaintedSpec | null {
  const rd = DATA.races[g.race];
  if (!rd || !rd.front.heads.length) return null;
  const r = new Rng(g.portraitSeed ^ 0x51ed27);
  const female = g.gender === "f";
  // 머리는 성별에 맞는 것 중에서
  const pool = rd.heads.map((s, i) => [s, i] as const).filter(([s]) => s === (female ? "f" : "m")).map(([, i]) => i);
  const head = r.pick(pool.length ? pool : rd.heads.map((_, i) => i));
  const helmetItem = g.equipment.helmet ? getArmor(g.equipment.helmet) : undefined;
  const helmet = helmetItem && r.chance(0.6) ? (helmetItem.weight === "heavy" ? "helmet_visor" : "helmet_crest") : null;
  const hair = helmet ? null
    : female ? r.pick(["hair_long", "hair_braids", "hair_short"])
    : r.pick(["hair_short", "hair_short", "hair_long", null]);
  // 수염은 남자만, 그것도 다는 아니다. 면갑을 쓰면 가려져서 뺀다
  const old = ageStage(g) === "decline";
  const beard = !female && helmet !== "helmet_visor" && r.chance(old ? 0.75 : 0.45) ? r.pick(["beard_full", "beard_short"]) : null;
  const weight = getArmor(g.equipment.armor)?.weight;
  const armor = weight === "heavy" || weight === "medium" ? "armor_plate" : weight === "light" ? "armor_harness" : null;
  return { race: g.race, head, hair, beard, helmet, armor };
}

/** 한 시점에서 그릴 층 (아래부터). 얼굴 다시 그리기는 'face'로 표시 */
export interface Layer { src: string; rect: Rect; clipBottom?: number }

export function paintedLayers(spec: PaintedSpec, view: "front" | "side"): Layer[] | null {
  const vd = DATA.races[spec.race]?.[view];
  if (!vd || !vd.heads.length) return null;
  const dir = `portrait2/${spec.race}/${view}`;
  const C = DATA.cell;
  const headRect: Rect = [0, 0, C, C];
  const out: Layer[] = [{ src: `${dir}/head_${spec.head + 1}.webp`, rect: headRect }];
  const part = (name: string | null) => {
    const rect = name ? vd.parts[name]?.[spec.head] : null;
    if (name && rect) out.push({ src: `${dir}/${name}.webp`, rect });
  };
  if (view === "front") {
    part(spec.armor);
    // 갑옷이 턱을 덮지 않게 얼굴(턱 줄 위)을 다시 그린다
    if (spec.armor) out.push({ src: `${dir}/head_${spec.head + 1}.webp`, rect: headRect, clipBottom: vd.heads[spec.head].chin });
  }
  part(spec.beard);
  part(spec.hair);
  part(spec.helmet);
  return out;
}

/** 측면 머리의 목 끝 (몸에 붙는 점), 셀 좌표 */
export function sideNeck(spec: PaintedSpec): { x: number; y: number } | null {
  const h = DATA.races[spec.race]?.side.heads[spec.head];
  return h?.neckX !== undefined ? { x: h.neckX, y: h.neckY! } : null;
}
