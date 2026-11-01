// 관절 인형의 동작을 시간대별로 한 장에 그려 확인한다 (React SVG → sharp)
// 사용: npx tsx tools/render-poses.tsx <출력.png> [시드] [종족]
import { renderToStaticMarkup } from "react-dom/server";
import sharp from "sharp";
import { generateGladiator } from "../src/core/generators/gladiator";
import { Rng } from "../src/core/rng";
import type { RaceId } from "../src/core/types";
import { RigFighter, type Pose } from "../src/ui/components/RigFighter";

const [out, seedArg, raceArg] = process.argv.slice(2);
const rng = new Rng(Number(seedArg ?? 7));
const g = generateGladiator(rng, { id: "p", nation: "aurelia", target: 11, race: (raceArg as RaceId) ?? "human" });

const ROWS: [Pose, number[]][] = [
  ["idle", [0, 0.6]],
  ["attack", [0, 0.1, 0.16, 0.22, 0.28, 0.4, 0.6]],
  ["hurt", [0.06, 0.2, 0.4]],
  ["block", [0.1, 0.4]],
  ["dodge", [0.12, 0.3]],
  ["step", [0.1, 0.22, 0.34]],
  ["down", [0.15, 0.3, 0.6]],
  ["kneel", [0.5]],
  ["cheer", [0.1, 0.25]],
];

async function inline(svg: string): Promise<string> {
  const paths = [...new Set([...svg.matchAll(/href="\/assets\/([^"]+)"/g)].map((m) => m[1]))];
  const map = new Map<string, string>();
  for (const p of paths) map.set(p, (await sharp(`public/assets/${p}`).png().toBuffer()).toString("base64"));
  return svg.replace(/href="\/assets\/([^"]+)"/g, (_, p: string) => `href="data:image/png;base64,${map.get(p)}"`);
}

const CW = 120, CH = 130, cols = Math.max(...ROWS.map((r) => r[1].length));
const sword = <g><path d="M0 0 L0 -24" stroke="#c9ccd2" strokeWidth="3.2" strokeLinecap="round" /><path d="M-5 -2 L5 -2" stroke="#7a5c33" strokeWidth="2.6" strokeLinecap="round" /></g>;
const tiles = [];
for (let r = 0; r < ROWS.length; r++) {
  const [pose, times] = ROWS[r];
  for (let c = 0; c < times.length; c++) {
    const svgInner = renderToStaticMarkup(
      <svg xmlns="http://www.w3.org/2000/svg" xmlnsXlink="http://www.w3.org/1999/xlink" width={CW * 2} height={CH * 2} viewBox={`-60 -110 ${CW} ${CH}`}>
        <rect x="-60" y="-110" width={CW} height={CH} fill="#2a2230" />
        <line x1="-60" x2="60" y1="0" y2="0" stroke="#6a5a48" />
        <RigFighter g={g} pose={pose} poseKey={1} hpRatio={1} fixedTime={times[c]} weapon={sword} />
        <text x="-56" y="16" fontSize="8" fill="#ddd">{pose} {times[c]}s</text>
      </svg>,
    );
    tiles.push({ input: await sharp(Buffer.from(await inline(svgInner))).png().toBuffer(), left: c * (CW * 2 + 4), top: r * (CH * 2 + 4) });
  }
}
await sharp({ create: { width: cols * (CW * 2 + 4), height: ROWS.length * (CH * 2 + 4), channels: 4, background: "#111" } }).composite(tiles).png().toFile(out);
console.log("saved", out);
