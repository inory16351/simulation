// 종족별로 무작위 검투사를 만들어 실제 게임과 같은 규칙으로 초상화를 합성한 뒤 한 장에 모은다.
// 사용: npx tsx tools/preview-sheet.ts <출력.png> [종족당 장수]
import sharp from "sharp";
import { generateGladiator } from "../src/core/generators/gladiator";
import { placePortrait } from "../src/core/portrait";
import { Rng } from "../src/core/rng";
import type { RaceId } from "../src/core/types";

const [outFile, perRaceArg] = process.argv.slice(2);
const PER = Number(perRaceArg ?? 6);
const SIZE = 220;
const RACES: RaceId[] = ["human", "elf", "dwarf", "orc", "minotaur", "lizardfolk", "beastkin"];
const rng = new Rng(77);

async function compose(seed: number, race: RaceId): Promise<Buffer> {
  const g = generateGladiator(rng, { id: `p${seed}`, nation: "aurelia", target: 9, race });
  g.portraitSeed = seed;
  const layers = [];
  for (const { part, box } of placePortrait(g)) {
    const w = Math.max(1, Math.round(box.w * SIZE)), h = Math.max(1, Math.round(box.h * SIZE));
    const left = Math.round(box.x * SIZE), top = Math.round(box.y * SIZE);
    const img = await sharp(`public/assets/${part.file}`).resize(w, h, { fit: "fill" }).png().toBuffer();
    const cropL = Math.max(0, -left), cropT = Math.max(0, -top);
    const cw = Math.min(w - cropL, SIZE - Math.max(0, left)), ch = Math.min(h - cropT, SIZE - Math.max(0, top));
    if (cw <= 0 || ch <= 0) continue;
    layers.push({ input: await sharp(img).extract({ left: cropL, top: cropT, width: cw, height: ch }).toBuffer(), left: Math.max(0, left), top: Math.max(0, top) });
  }
  return sharp({ create: { width: SIZE, height: SIZE, channels: 4, background: "#221a15" } }).composite(layers).png().toBuffer();
}

const tiles: { input: Buffer; left: number; top: number }[] = [];
for (let r = 0; r < RACES.length; r++) {
  for (let i = 0; i < PER; i++) tiles.push({ input: await compose(r * 1000 + i * 37 + 5, RACES[r]), left: i * (SIZE + 6), top: r * (SIZE + 6) });
}
await sharp({ create: { width: PER * (SIZE + 6), height: RACES.length * (SIZE + 6), channels: 4, background: "#0e0b09" } })
  .composite(tiles).png().toFile(outFile);
console.log("saved", outFile);
