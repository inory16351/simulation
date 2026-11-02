// 게임과 같은 무작위 규칙(paintedSpec)으로 그림 초상화 예시를 PNG 한 장에 모은다. 머리색 필터는 빼고 그린다.
// 사용: npx tsx tools/render-painted.tsx <종족> <출력.png> [시드]
import sharp from "sharp";
import { generateGladiator } from "../src/core/generators/gladiator";
import { getRace } from "../src/core/data";
import { paintedLayers, paintedSpec, PORTRAIT_CELL } from "../src/core/portrait2";
import { Rng } from "../src/core/rng";
import type { NationId, RaceId } from "../src/core/types";

const NATION: Record<string, NationId> = { human: "aurelia", elf: "sylvaren", dwarf: "dolgar", orc: "grommar", minotaur: "grommar", lizardfolk: "karsea", beastkin: "kemer" };
const [race, out, seedArg] = process.argv.slice(2) as [RaceId, string, string?];
getRace(race);
const rng = new Rng(Number(seedArg ?? 11));
const S = 200, COLS = 8, ROWS = 4, k = S / PORTRAIT_CELL;
const tiles = [];
for (let n = 0; n < COLS * ROWS; n++) {
  const g = generateGladiator(rng, { id: `${race}${n}`, nation: NATION[race], target: 9, race });
  // 흉터가 나오게 전적을 섞는다
  g.record = { wins: rng.int(0, 25), losses: rng.int(0, 10), kills: 0 };
  const spec = paintedSpec(g);
  const layers = spec ? paintedLayers(spec, "front") ?? [] : [];
  const comps = [];
  for (const l of layers) {
    const file = `public/assets/${l.src}`;
    const [x, y, w, h] = l.rect.map((v) => v * k);
    let img = sharp(file).resize(Math.max(1, Math.round(w)), Math.max(1, Math.round(h)), { fit: "fill" });
    let buf = await img.png().toBuffer();
    if (l.clipBottom) buf = await sharp(buf).extract({ left: 0, top: 0, width: Math.round(w), height: Math.round(l.clipBottom * k) }).png().toBuffer();
    let top = y;
    if (l.clipTop) { const t = Math.round(l.clipTop * k); buf = await sharp(buf).extract({ left: 0, top: t, width: Math.round(w), height: Math.round(h) - t }).png().toBuffer(); top = y + t; }
    // 칸 밖으로 나가는 부분은 잘라 낸다
    const L = Math.round(x), T = Math.round(top), meta = await sharp(buf).metadata();
    const ex = { left: Math.max(0, -L), top: Math.max(0, -T) };
    const cw = Math.min(meta.width! - ex.left, S - Math.max(0, L)), ch = Math.min(meta.height! - ex.top, S - Math.max(0, T));
    if (cw <= 0 || ch <= 0) continue;
    comps.push({ input: await sharp(buf).extract({ ...ex, width: cw, height: ch }).png().toBuffer(), left: Math.max(0, L), top: Math.max(0, T) });
  }
  const tile = await sharp({ create: { width: S, height: S, channels: 4, background: "#2a1d22" } }).composite(comps).png().toBuffer();
  tiles.push({ input: tile, left: (n % COLS) * (S + 6), top: Math.floor(n / COLS) * (S + 6) });
}
await sharp({ create: { width: COLS * (S + 6), height: ROWS * (S + 6), channels: 4, background: "#000" } }).composite(tiles).png().toFile(out);
console.log("saved", out);
