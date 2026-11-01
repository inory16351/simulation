// 초상화 예시를 PNG 한 장으로 렌더링한다 (React SVG → sharp)
// 사용: npx tsx tools/render-avatars.tsx <출력.png> [시드]
import { renderToStaticMarkup } from "react-dom/server";
import sharp from "sharp";
import { makeAvatar } from "../src/core/avatar";
import { getNation } from "../src/core/data";
import { generateGladiator } from "../src/core/generators/gladiator";
import { Rng } from "../src/core/rng";
import type { NationId, RaceId } from "../src/core/types";
import { AvatarSvg } from "../src/ui/components/Avatar";

const [out, seedArg] = process.argv.slice(2);
const rng = new Rng(Number(seedArg ?? 7));
const ROWS: [RaceId, NationId][] = [
  ["human", "aurelia"], ["elf", "sylvaren"], ["dwarf", "dolgar"], ["orc", "grommar"],
  ["minotaur", "grommar"], ["lizardfolk", "karsea"], ["beastkin", "kemer"],
];
const SIZE = 200, COLS = 8;
const tiles = [];
for (let r = 0; r < ROWS.length; r++) {
  for (let c = 0; c < COLS; c++) {
    const [race, nation] = ROWS[r];
    const g = generateGladiator(rng, { id: `${race}${c}`, nation, target: 9, race });
    const svg = renderToStaticMarkup(
      <AvatarSvg spec={makeAvatar(g)} bg={getNation(nation).color} seed={g.portraitSeed % 997}
        scars={c === 7 ? 3 : 0} injured={c === 6 ? "serious" : undefined} />,
    ).replace("<svg ", `<svg xmlns="http://www.w3.org/2000/svg" width="${SIZE}" height="${SIZE}" `);
    tiles.push({ input: Buffer.from(svg), left: c * (SIZE + 8), top: r * (SIZE + 8) });
  }
}
await sharp({ create: { width: COLS * (SIZE + 8), height: ROWS.length * (SIZE + 8), channels: 4, background: "#0e0b09" } })
  .composite(tiles).png().toFile(out);
console.log("saved", out);
