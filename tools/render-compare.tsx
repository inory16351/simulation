// 기준 그림(통 이미지 3×3)과 같은 9가지 자세를 관절 인형으로 그려 나란히 놓는다
// 사용: npx tsx tools/render-compare.tsx <기준.png> <출력.png> [종족]
import { renderToStaticMarkup } from "react-dom/server";
import sharp from "sharp";
import { generateGladiator } from "../src/core/generators/gladiator";
import { Rng } from "../src/core/rng";
import type { Gladiator, RaceId } from "../src/core/types";
import { RigFighter, type Pose } from "../src/ui/components/RigFighter";

const [refPath, out, raceArg] = process.argv.slice(2);
const base = generateGladiator(new Rng(7), { id: "p", nation: "aurelia", target: 11, race: (raceArg as RaceId) ?? "human" });
const withGear = (main: string, off: string | null): Gladiator => ({ ...base, gender: "m", equipment: { ...base.equipment, main, off, armor: "leather", helmet: base.equipment.helmet } });

// 기준 그림 칸 순서와 같게
const CELLS: [Gladiator, Pose, number][] = [
  [withGear("gladius", "scutum"), "idle", 0],
  [withGear("gladius", "scutum"), "attack", 0.16],
  [withGear("gladius", "scutum"), "attack", 0.3],
  [withGear("hasta", null), "idle", 0],
  [withGear("hasta", null), "attack", 0.26],
  [withGear("greataxe", null), "attack", 0.2],
  [withGear("greataxe", null), "attack", 0.36],
  [withGear("gladius", "scutum"), "block", 0.3],
  [withGear("gladius", "scutum"), "hurt", 0.06],
];

async function inline(svg: string): Promise<string> {
  const paths = [...new Set([...svg.matchAll(/href="\/assets\/([^"]+)"/g)].map((m) => m[1]))];
  const map = new Map<string, string>();
  for (const p of paths) map.set(p, (await sharp(`public/assets/${p}`).png().toBuffer()).toString("base64"));
  return svg.replace(/href="\/assets\/([^"]+)"/g, (_, p: string) => `href="data:image/png;base64,${map.get(p)}"`);
}

const S = 300;
const tiles = [];
for (let i = 0; i < 9; i++) {
  const [g, pose, t] = CELLS[i];
  const svg = renderToStaticMarkup(
    <svg xmlns="http://www.w3.org/2000/svg" xmlnsXlink="http://www.w3.org/1999/xlink" width={S} height={S} viewBox="-60 -105 120 120">
      <rect x="-60" y="-105" width="120" height="120" fill="#ff00ff" />
      <RigFighter g={g} pose={pose} poseKey={1} hpRatio={1} fixedTime={t} weapon={null} />
    </svg>,
  );
  tiles.push({ input: await sharp(Buffer.from(await inline(svg))).png().toBuffer(), left: 3 * S + 20 + (i % 3) * S, top: Math.floor(i / 3) * S });
}
const ref = await sharp(refPath).flatten({ background: "#ff00ff" }).resize(3 * S, 3 * S).png().toBuffer();
await sharp({ create: { width: 6 * S + 20, height: 3 * S, channels: 3, background: "#000" } })
  .composite([{ input: ref, left: 0, top: 0 }, ...tiles]).png().toFile(out);
console.log("saved", out);
