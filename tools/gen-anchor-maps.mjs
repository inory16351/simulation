// 초상화 시트마다 '기준점 지도'를 Codex로 만든다: 같은 시트를 그대로 다시 그리고, 파츠가 붙을 자리에 색 점을 찍는다.
// 점 지도는 위치 재기에만 쓰고, 게임 그림은 원래 시트를 그대로 쓴다 (tools/portrait-parts.mjs가 점을 읽는다).
// 사용: node tools/gen-anchor-maps.mjs [종족...] [--jobs 5] [--force]
//   → art/source/portrait2/<종족>/anchors/<원래 시트 이름>.png
import { spawn } from "node:child_process";
import { existsSync, mkdirSync, readdirSync, writeFileSync } from "node:fs";

/** 점 색 (배경 마젠타·피부·머리색과 겹치지 않게 아주 진한 색만) */
export const DOTS = {
  crown: "pure RED (#FF0000)",
  brow: "pure YELLOW (#FFFF00)",
  eye: "pure BLUE (#0000FF)",
  nose: "pure GREEN (#00FF00)",
  mouth: "pure CYAN (#00FFFF)",
  chin: "pure WHITE (#FFFFFF)",
  neck: "pure BLACK (#000000)",
};

const COMMON = "Copy the attached sheet EXACTLY: same 3x3 grid, same flat magenta background, every drawing in exactly the same position, size and shape as in the attached image (do not move, resize, redraw differently or restyle anything). The ONLY change: add small solid round marker dots (about 1.5% of the cell width, perfectly flat color, no outline, no glow, no shading) on top of the drawings as listed below. Use exactly these colors and nothing else in those colors. No text, no labels.";

const HEADS = `${COMMON} In EVERY cell mark this character's face: one ${DOTS.crown} dot at the very top of the skull outline (the crown, on the outline itself); one ${DOTS.brow} dot centered between the eyebrows; one ${DOTS.eye} dot in the center of EACH pupil (two dots); one ${DOTS.nose} dot on the tip of the nose or snout; one ${DOTS.mouth} dot at the center of the mouth; one ${DOTS.chin} dot at the bottom of the chin or jaw outline (on the outline itself, centered); one ${DOTS.neck} dot at the base of the neck where it meets the shoulders, centered.`;

/** 파츠 종류별로 찍을 점: 이 파츠를 쓴 사람의 그 자리가 어디에 오는가 */
const PART_RULE = {
  hair: `one ${DOTS.crown} dot where the TOP of the wearer's skull would touch (inside the hair, on the centerline) and one ${DOTS.brow} dot where the wearer's eyebrow center would be (on the centerline, usually in the empty face area below the bangs)`,
  helmet: `one ${DOTS.crown} dot where the TOP of the wearer's skull touches the inside of the helmet (on the centerline) and one ${DOTS.brow} dot where the wearer's eyebrow center would be (on the centerline; for a closed helmet put it on the helmet surface at brow height)`,
  beard: `one ${DOTS.nose} dot where the wearer's nose tip would be (on the centerline just above the mustache) and one ${DOTS.mouth} dot where the wearer's mouth center would be (on the centerline)`,
  armor: `one ${DOTS.neck} dot where the base of the wearer's neck would be (centered in the neck opening, at the collar line) and one ${DOTS.chin} dot where the wearer's chin would be (on the centerline above the collar, in the empty area)`,
};
const KINDS = ["hair", "hair", "hair", "beard", "beard", "helmet", "helmet", "armor", "armor"];
const partsPrompt = (cells) => `${COMMON} Each cell holds one overlay item made for a bust template. In each NON-EMPTY cell mark where the wearer's face sits relative to that item: ${cells.map((k, i) => `cell ${i + 1} (${k}): ${k ? PART_RULE[k] : "leave empty"}`).join("; ")}.`;

const STYLE = "Keep the original painted style exactly. No text, no watermark.";
const SHEET = "ONE square image the same size as the attached image, flat solid MAGENTA background (#FF00FF) where the original is magenta.";

const args = process.argv.slice(2);
const ji = args.indexOf("--jobs");
const jobs = ji >= 0 ? Number(args[ji + 1]) : 5;
const force = args.includes("--force");
const races = args.filter((a, i) => !a.startsWith("--") && args[i - 1] !== "--jobs");
const SRC = "art/source/portrait2";
mkdirSync("art/tmp", { recursive: true });

const tasks = [];
for (const race of races.length ? races : readdirSync(SRC)) {
  for (const f of ["front_heads.png", "front_heads_2.png", "front_parts.png", "front_parts_2.png"]) {
    const src = `${SRC}/${race}/${f}`;
    if (!existsSync(src)) continue;
    const out = `${SRC}/${race}/anchors/${f}`;
    if (existsSync(out) && !force) continue;
    mkdirSync(`${SRC}/${race}/anchors`, { recursive: true });
    tasks.push({ id: `${race}.${f.replace(".png", "")}`, ref: src, out, prompt: f.includes("heads") ? HEADS : partsPrompt(KINDS) });
  }
}

function run(t) {
  const manifest = { presets: { sheet: SHEET }, style: STYLE, styleRef: t.ref, styleRefPresets: ["sheet"], items: [{ id: t.id, preset: "sheet", out: t.out, prompt: t.prompt }] };
  const mf = `art/tmp/anchor_${t.id}.json`;
  writeFileSync(mf, JSON.stringify(manifest, null, 1));
  return new Promise((resolve) => {
    const child = spawn("node", ["tools/gen-images.mjs", "--manifest", mf, "--jobs", "1", "--force"], { stdio: "inherit" });
    child.on("close", () => resolve(existsSync(t.out)));
  });
}

let next = 0, failed = 0;
await Promise.all(Array.from({ length: Math.min(jobs, tasks.length) }, async () => {
  while (next < tasks.length) { const t = tasks[next++]; if (!(await run(t))) { failed++; console.log(`✗ ${t.id}`); } }
}));
console.log(`끝: ${tasks.length - failed}/${tasks.length}`);
process.exit(failed ? 1 : 0);
