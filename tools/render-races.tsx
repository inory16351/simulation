// 종족별로 무작위 그림 초상화(정면)와 전투 인형(측면)을 한 장에 그려 붙는 자리를 확인한다
// 사용: npx tsx tools/render-races.tsx <출력.png> [시드]
import { renderToStaticMarkup } from "react-dom/server";
import sharp from "sharp";
import { getNation } from "../src/core/data";
import { generateGladiator } from "../src/core/generators/gladiator";
import { paintedSpec } from "../src/core/portrait2";
import { Rng } from "../src/core/rng";
import type { NationId, RaceId } from "../src/core/types";
import { PaintedAvatar } from "../src/ui/components/PaintedAvatar";
import { RigFighter } from "../src/ui/components/RigFighter";

const [out, seedArg] = process.argv.slice(2);
const rng = new Rng(Number(seedArg ?? 3));
const RACES: [RaceId, NationId][] = [
  ["human", "aurelia"], ["elf", "sylvaren"], ["dwarf", "dolgar"], ["orc", "grommar"],
  ["minotaur", "grommar"], ["lizardfolk", "karsea"], ["beastkin", "kemer"],
];

async function inline(svg: string): Promise<string> {
  const paths = [...new Set([...svg.matchAll(/href="\/assets\/([^"]+)"/g)].map((m) => m[1]))];
  const map = new Map<string, string>();
  for (const p of paths) map.set(p, (await sharp(`public/assets/${p}`).png().toBuffer()).toString("base64"));
  return svg.replace(/href="\/assets\/([^"]+)"/g, (_, p: string) => `href="data:image/png;base64,${map.get(p)}"`);
}
const png = async (el: React.ReactElement, w: number, h: number) => {
  const svg = renderToStaticMarkup(el).replace("<svg ", `<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="${w}" height="${h}" `);
  return sharp(Buffer.from(await inline(svg))).png().toBuffer();
};

const P = 150, COLS = 6;
const tiles = [];
for (let r = 0; r < RACES.length; r++) {
  const [race, nation] = RACES[r];
  for (let c = 0; c < COLS; c++) {
    const g = generateGladiator(rng, { id: `${race}${c}`, nation, target: 10, race });
    const spec = paintedSpec(g);
    if (!spec) continue;
    if (c < COLS - 2) {
      tiles.push({ input: await png(<PaintedAvatar spec={spec} bg={getNation(nation).color} seed={c} />, P, P), left: c * (P + 4), top: r * (P + 4) });
    } else {
      const el = (
        <svg viewBox="-55 -100 110 110">
          <rect x="-55" y="-100" width="110" height="110" fill="#2a2230" />
          <RigFighter g={g} pose={c === COLS - 2 ? "idle" : "attack"} poseKey={1} hpRatio={1} fixedTime={c === COLS - 2 ? 0 : 0.26} weapon={null} />
        </svg>
      );
      tiles.push({ input: await png(el, P, P), left: c * (P + 4), top: r * (P + 4) });
    }
  }
}
await sharp({ create: { width: COLS * (P + 4), height: RACES.length * (P + 4), channels: 4, background: "#111" } }).composite(tiles).png().toFile(out);
console.log("saved", out);
