// 점 지도 PNG(3×3)에서 색 점을 칸별로 읽어 save-anchors.mjs 형식 문자열로 찍는다. tools/chatgpt-dots.js의 로컬판.
// 사용: node tools/read-dots.mjs <점지도.png> heads|parts   → 출력 문자열을 save-anchors.mjs에 그대로 넘긴다
import sharp from "sharp";

const [file, mode] = process.argv.slice(2);
const { data: d, info } = await sharp(file).removeAlpha().raw().toBuffer({ resolveWithObject: true });
const W = info.width, H = info.height, N = 3;
const cls = (r, g, b) => r > 215 && g < 55 && b < 55 ? "R" : r > 215 && g > 215 && b < 70 ? "Y" : b > 200 && r < 60 && g < 90 ? "B" : g > 200 && r < 90 && b < 90 ? "G" : g > 200 && b > 200 && r < 80 ? "C" : r > 245 && g > 245 && b > 245 ? "W" : r < 22 && g < 22 && b < 22 ? "K" : null;
const lab = new Uint8Array(W * H), seen = new Uint8Array(W * H), dots = [];
for (let i = 0; i < W * H; i++) { const k = cls(d[i * 3], d[i * 3 + 1], d[i * 3 + 2]); if (k) lab[i] = k.charCodeAt(0); }
for (let i = 0; i < W * H; i++) {
  if (!lab[i] || seen[i]) continue;
  const k = lab[i]; let n = 0, sx = 0, sy = 0; const st = [i]; seen[i] = 1;
  while (st.length) { const p = st.pop(); n++; const px = p % W, py = (p - px) / W; sx += px; sy += py;
    for (const q of [px > 0 ? p - 1 : -1, px < W - 1 ? p + 1 : -1, py > 0 ? p - W : -1, py < H - 1 ? p + W : -1]) if (q >= 0 && !seen[q] && lab[q] === k) { seen[q] = 1; st.push(q); } }
  // 점은 둥근 작은 덩어리: 너무 작거나(잡티) 너무 크면(검은 그림자·흰 면) 버린다
  if (n >= 20 && n < 1500) dots.push([String.fromCharCode(k), sx / n, sy / n, n]);
}
const cw = W / N, ch = H / N, S = 512 / cw;
const KEYS = mode === "heads" ? ["crown", "brow", "eyeL", "eyeR", "nose", "mouth", "chin", "neck"] : ["crown", "brow", "nose", "mouth", "chin", "neck"];
const MAP = { R: "crown", Y: "brow", G: "nose", C: "mouth", W: "chin", K: "neck" };
const out = [];
for (let c = 0; c < 9; c++) {
  const x0 = (c % 3) * cw, y0 = Math.floor(c / 3) * ch;
  const ins = dots.filter((t) => t[1] >= x0 && t[1] < x0 + cw && t[2] >= y0 && t[2] < y0 + ch).map((t) => [t[0], Math.round((t[1] - x0) * S), Math.round((t[2] - y0) * S), t[3]]);
  const o = {};
  // 가운데 줄 가까이 있는 점만 (머리카락·그림자의 검정이 잡히는 것 막기)
  for (const k of "RYGCWK") { const l = ins.filter((t) => t[0] === k && Math.abs(t[1] - 256) < 90).sort((a, b) => b[3] - a[3]); if (l[0]) o[MAP[k]] = [l[0][1], l[0][2]]; }
  if (o.neck && o.chin && o.neck[1] <= o.chin[1] + 50) delete o.neck;
  const e = ins.filter((t) => t[0] === "B").sort((a, b) => b[3] - a[3]).slice(0, 2).sort((a, b) => a[1] - b[1]);
  if (e.length === 2) { o.eyeL = [e[0][1], e[0][2]]; o.eyeR = [e[1][1], e[1][2]]; }
  out.push(KEYS.map((k) => (o[k] ? o[k].join(",") : "-")).join(" "));
}
console.log(out.join("|"));
