// 전투 장면의 한 순간(타격 직후)을 PNG로 렌더링해 연출을 확인한다 (React SVG → sharp)
// 사용: npx tsx tools/render-stage.tsx <출력.png> [시드]
import { writeFileSync } from "node:fs";
import { renderToStaticMarkup } from "react-dom/server";
import sharp from "sharp";
import { simulateMatch, type CombatEvent } from "../src/core/combat/sim";
import { generateGladiator } from "../src/core/generators/gladiator";
import { Rng } from "../src/core/rng";
import type { RaceId } from "../src/core/types";
import { ArenaStage } from "../src/ui/components/ArenaStage";

const [out, seedArg] = process.argv.slice(2);
const rng = new Rng(Number(seedArg ?? 3));
// 셋째 인자로 종족을 고정할 수 있다 (예: human)
const race = process.argv[4] as RaceId | undefined;
const a = generateGladiator(rng, { id: "a", nation: "aurelia", target: 11, race });
const b = generateGladiator(rng, { id: "b", nation: "grommar", target: 11, race });
const r = simulateMatch(a, b, { rule: "missio", seed: Number(seedArg ?? 3) });

// 장면별로 '이 이벤트까지' 잘라 보여 준다: 가벼운 타격, 세게 맞음, 치명타, 회피
const pickIdx = (pred: (e: CombatEvent) => boolean) => r.events.findIndex(pred);
const shots = [
  pickIdx((e) => e.t === "attack" && e.hit && !e.blocked && !e.crit && e.damage / e.maxHp <= 0.12),
  pickIdx((e) => e.t === "attack" && e.hit && !e.blocked && !e.crit && e.damage / e.maxHp > 0.12),
  pickIdx((e) => e.t === "attack" && e.crit),
  pickIdx((e) => e.t === "attack" && !e.hit),
].filter((i) => i >= 0);

// 웹용 자산(webp)을 PNG data URI로 바꾼다 (librsvg는 webp를 못 읽는다)
const cache = new Map<string, string>();
async function inline(svg: string): Promise<string> {
  const paths = [...new Set([...svg.matchAll(/href="\/assets\/([^"]+)"/g)].map((m) => m[1]))];
  for (const p of paths) {
    if (!cache.has(p)) cache.set(p, (await sharp(`public/assets/${p}`).png().toBuffer()).toString("base64"));
  }
  return svg.replace(/href="\/assets\/([^"]+)"/g, (_, p: string) => `href="data:image/png;base64,${cache.get(p)}"`);
}

const W = 800, H = 320;
const tiles = [];
for (let i = 0; i < shots.length; i++) {
  const html = renderToStaticMarkup(<ArenaStage a={a} b={b} events={r.events.slice(0, shots[i] + 1)} crowd />);
  // 치명타 번쩍임(0.18초)은 정지 화면에선 화면을 하얗게 덮으니 뺀다
  const svg = (await inline(html.slice(html.indexOf("<svg"), html.lastIndexOf("</svg>") + 6).replace(/<rect class="impact-frame"[^>]*(\/>|><\/rect>)/g, "")))
    .replace("<svg ", `<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="${W}" height="${H}" `);
  if (process.env.DUMP) writeFileSync(`art/tmp/stage_${i}.svg`, svg);
  tiles.push({ input: await sharp(Buffer.from(svg)).png().toBuffer(), left: 0, top: i * (H + 8) });
}
await sharp({ create: { width: W, height: shots.length * (H + 8), channels: 4, background: "#0e0b09" } }).composite(tiles).png().toFile(out);
console.log("saved", out, shots);
