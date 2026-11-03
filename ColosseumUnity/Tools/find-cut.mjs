// 잘린 아이콘 찾기: 그림 상자 가장자리에 불투명 픽셀이 길게 이어지면(직선으로 잘림) 보고한다
import sharp from "sharp";
import { readdirSync } from "node:fs";
const A = "ColosseumUnity/Assets/Resources/Art";
for (const dir of ["icon/items", "icon/menu", "crest", "fx"]) {
  for (const f of readdirSync(`${A}/${dir}`).filter((x) => x.endsWith(".png"))) {
    const { data, info } = await sharp(`${A}/${dir}/${f}`).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    const W = info.width, H = info.height, op = (x, y) => data[(y * W + x) * 4 + 3] > 128;
    let x0 = W, y0 = H, x1 = -1, y1 = -1;
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) if (op(x, y)) { x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y); }
    const run = (pts) => { let best = 0, cur = 0; for (const p of pts) { cur = p ? cur + 1 : 0; best = Math.max(best, cur); } return best; };
    const side = (n, fn) => run(Array.from({ length: n }, (_, i) => fn(i)));
    const w = x1 - x0 + 1, h = y1 - y0 + 1;
    const r = { top: side(w, (i) => op(x0 + i, y0)) / w, bottom: side(w, (i) => op(x0 + i, y1)) / w, left: side(h, (i) => op(x0, y0 + i)) / h, right: side(h, (i) => op(x1, y0 + i)) / h };
    const bad = Object.entries(r).filter(([, v]) => v > 0.12).map(([k, v]) => `${k} ${Math.round(v * 100)}%`);
    if (bad.length) console.log(`${dir}/${f}: ${bad.join(", ")}`);
  }
}
