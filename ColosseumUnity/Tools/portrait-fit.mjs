// 초상화 갑옷(옷) 맞추기: 어깨 끝선이 몸과 맞도록 Codex가 찍은 점으로 갑옷 자리를 다시 계산한다.
// 머리마다: 목과 어깨가 만나는 점 둘(왼·오른) + 어깨 바깥 끝점 둘. 갑옷마다: 같은 뜻의 점 넷(옷의 목둘레 양 끝, 어깨 바깥 끝).
// 점은 Codex가 시트를 다시 그리며 찍어 조금 어긋날 수 있으니, 실제 그림의 윤곽(알파 경계)에 붙여(snap) 쓴다.
// 결과는 Assets/Resources/Data/portrait2.json의 armor_* 자리(머리마다 [x,y,w,h])를 덮어쓴다. 웹판 data/는 건드리지 않는다.
// 사용 (저장소 루트에서):
//   node ColosseumUnity/Tools/portrait-fit.mjs sheets   머리·갑옷을 3×3 마젠타 시트로 → art/source/portrait_fit/, 목록 → ColosseumUnity/Tools/portrait-fit-dots.json
//   node tools/gen-images.mjs --manifest ColosseumUnity/Tools/portrait-fit-dots.json --jobs 4    Codex가 점을 찍은 시트
//   node ColosseumUnity/Tools/portrait-fit.mjs read     점 읽기 + 윤곽에 붙이기 → ColosseumUnity/Tools/portrait-fit.json
//   node ColosseumUnity/Tools/portrait-fit.mjs apply    갑옷 자리 다시 계산 → Assets/Resources/Data/portrait2.json
//   (확인) node ColosseumUnity/Tools/portrait-view.mjs <종족> <갑옷>
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import sharp from "sharp";

const RES = "ColosseumUnity/Assets/Resources";
const JSON_PATH = `${RES}/Data/portrait2.json`;
const SRC = "art/source/portrait_fit";
const MANIFEST = "ColosseumUnity/Tools/portrait-fit-dots.json";
const FIT = "ColosseumUnity/Tools/portrait-fit.json";
const CELL = 512, N = 3;

const COLORS = { NL: "pure RED (#FF0000)", NR: "pure YELLOW (#FFFF00)", SL: "pure BLUE (#0000FF)", SR: "pure GREEN (#00FF00)" };
const COMMON = "Copy the attached sheet EXACTLY: same 3x3 grid, same flat magenta background, every drawing in exactly the same position, size and shape (do not move, resize, crop, redraw differently or restyle anything). The ONLY change: add small solid round marker dots (about 1.5% of the cell width, perfectly flat color, no outline, no glow, no shading) on top of the drawings as listed. Use exactly these colors and nothing else in those colors. No text, no labels. Left/right below mean the VIEWER's left/right.";
const HEADS = `${COMMON} In EVERY cell (a head-and-shoulders bust) mark the shoulder line: one ${COLORS.NL} dot ON THE OUTLINE where the left side of the neck meets the top of the left shoulder (the inner corner of the shoulder); one ${COLORS.NR} dot at the same place on the right; one ${COLORS.SL} dot ON THE TOP OUTLINE of the left shoulder at its outer end (the shoulder tip, where the shoulder top turns down into the arm; if the shoulder runs off the cell before turning down, put it on the shoulder's top outline exactly at the cell's left edge); one ${COLORS.SR} dot at the same place on the right shoulder. Empty cells get no dots.`;
const ARMOR = `${COMMON} Every cell shows one upper-body garment or armor piece (worn on the shoulders, no person inside). Mark where it sits on the wearer: one ${COLORS.NL} dot at the left end of the neck opening on the garment's top edge (where the wearer's neck side meets the shoulder); one ${COLORS.NR} dot at the right end of the neck opening; one ${COLORS.SL} dot at the outer end of the garment's LEFT shoulder top outline (where the wearer's shoulder tip would be, the outermost top corner of the left shoulder piece); one ${COLORS.SR} dot at the same place on the right. All four dots on the garment's top outline. Empty cells get no dots.`;

const data = () => JSON.parse(readFileSync(JSON_PATH, "utf8"));
const dirOf = (race) => `${RES}/Art/portrait2/${race}/front`;

/** 시트에 넣을 칸 목록: 머리(종족별 9개씩)와 갑옷(모든 종족, 9개씩) */
function cells() {
  const j = data(), heads = [], armors = [];
  for (const [race, r] of Object.entries(j.races)) {
    r.front.heads.forEach((_, i) => heads.push({ race, name: `head_${i + 1}` }));
    for (const k of Object.keys(r.front.parts)) if (k.startsWith("armor_")) armors.push({ race, name: k });
  }
  const sheets = [];
  const byRace = {};
  for (const h of heads) (byRace[h.race] ??= []).push(h);
  for (const [race, list] of Object.entries(byRace))
    for (let k = 0; k * 9 < list.length; k++) sheets.push({ id: `heads_${race}_${k + 1}`, kind: "heads", cells: list.slice(k * 9, k * 9 + 9) });
  for (let k = 0; k * 9 < armors.length; k++) sheets.push({ id: `armor_${k + 1}`, kind: "armor", cells: armors.slice(k * 9, k * 9 + 9) });
  return sheets;
}

/** 칸 안에 그림을 놓는 방법: 머리는 512 그대로, 갑옷은 가운데에 맞춰 줄인다. 반환: 그림 좌표 → 칸 좌표 */
async function placeInCell(file, kind) {
  const meta = await sharp(file).metadata();
  if (kind === "heads") return { s: CELL / meta.width, ox: 0, oy: 0, w: meta.width, h: meta.height };
  const s = Math.min((CELL * 0.86) / meta.width, (CELL * 0.86) / meta.height);
  return { s, ox: (CELL - meta.width * s) / 2, oy: (CELL - meta.height * s) / 2, w: meta.width, h: meta.height };
}

const mode = process.argv[2];

if (mode === "sheets") {
  mkdirSync(SRC, { recursive: true });
  const items = [];
  for (const sh of cells()) {
    const comps = [];
    for (let i = 0; i < sh.cells.length; i++) {
      const c = sh.cells[i], file = `${dirOf(c.race)}/${c.name}.png`;
      const p = await placeInCell(file, sh.kind);
      const buf = await sharp(file).resize(Math.round(p.w * p.s), Math.round(p.h * p.s)).png().toBuffer();
      comps.push({ input: buf, left: (i % N) * CELL + Math.round(p.ox), top: Math.floor(i / N) * CELL + Math.round(p.oy) });
    }
    const out = `${SRC}/${sh.id}.png`;
    await sharp({ create: { width: CELL * N, height: CELL * N, channels: 4, background: "#FF00FF" } }).composite(comps).flatten({ background: "#FF00FF" }).png().toFile(out);
    items.push({ id: sh.id, preset: "dots", out: `${SRC}/dots/${sh.id}.png`, styleRef: out, prompt: sh.kind === "heads" ? HEADS : ARMOR, kind: sh.kind, cells: sh.cells });
  }
  writeFileSync(MANIFEST, JSON.stringify({ presets: { dots: "" }, style: "", styleRefPresets: ["dots"], items }, null, 1) + "\n");
  console.log(`시트 ${items.length}장 → ${SRC}, 목록 → ${MANIFEST}`);
} else if (mode === "read") {
  const man = JSON.parse(readFileSync(MANIFEST, "utf8"));
  const fit = existsSync(FIT) ? JSON.parse(readFileSync(FIT, "utf8")) : {};
  let ok = 0, bad = 0;
  for (const it of man.items) {
    if (!existsSync(it.out)) { console.log(`없음: ${it.out}`); continue; }
    const { data: d, info } = await sharp(it.out).removeAlpha().raw().toBuffer({ resolveWithObject: true });
    const W = info.width, H = info.height, cw = W / N, ch = H / N;
    const cls = (r, g, b) => r > 200 && g < 70 && b < 70 ? "NL" : r > 200 && g > 200 && b < 80 ? "NR" : b > 190 && r < 70 && g < 90 ? "SL" : g > 190 && r < 90 && b < 90 ? "SR" : null;
    // 칸마다 색별 덩어리 중 가장 큰 것의 중심
    const best = {};
    const seen = new Uint8Array(W * H);
    for (let p = 0; p < W * H; p++) {
      if (seen[p]) continue;
      const k = cls(d[p * 3], d[p * 3 + 1], d[p * 3 + 2]);
      if (!k) continue;
      let n = 0, sx = 0, sy = 0; const st = [p]; seen[p] = 1;
      while (st.length) {
        const q = st.pop(), x = q % W, y = (q - x) / W; n++; sx += x; sy += y;
        for (const r of [x > 0 ? q - 1 : -1, x < W - 1 ? q + 1 : -1, y > 0 ? q - W : -1, y < H - 1 ? q + W : -1])
          if (r >= 0 && !seen[r] && cls(d[r * 3], d[r * 3 + 1], d[r * 3 + 2]) === k) { seen[r] = 1; st.push(r); }
      }
      if (n < 12) continue;
      const cx = sx / n, cy = sy / n, cell = Math.min(N - 1, Math.floor(cy / ch)) * N + Math.min(N - 1, Math.floor(cx / cw));
      const key = `${cell}:${k}`;
      if (!best[key] || best[key].n < n) best[key] = { n, x: ((cx % cw) / cw) * CELL, y: ((cy % ch) / ch) * CELL };
    }
    for (let i = 0; i < it.cells.length; i++) {
      const c = it.cells[i], file = `${dirOf(c.race)}/${c.name}.png`;
      const pl = await placeInCell(file, it.kind);
      const alpha = await sharp(file).ensureAlpha().extractChannel(3).raw().toBuffer({ resolveWithObject: true });
      const pts = {};
      for (const k of ["NL", "NR", "SL", "SR"]) {
        const b = best[`${i}:${k}`];
        if (!b) continue;
        // 칸 좌표 → 그림 좌표, 그리고 가까운 윤곽(불투명인데 옆이 투명하거나 그림 끝)에 붙인다
        pts[k] = snap(alpha, (b.x - pl.ox) / pl.s, (b.y - pl.oy) / pl.s, 28 / pl.s);
      }
      const okCell = ["NL", "NR", "SL", "SR"].every((k) => pts[k]) && pts.SL[0] < pts.NL[0] && pts.NL[0] < pts.NR[0] && pts.NR[0] < pts.SR[0];
      (fit[c.race] ??= {})[c.name] = okCell ? pts : null;
      okCell ? ok++ : bad++;
      if (!okCell) console.log(`점 모자람/순서 틀림: ${c.race} ${c.name} ${JSON.stringify(pts)}`);
    }
  }
  writeFileSync(FIT, JSON.stringify(fit, null, 1) + "\n");
  console.log(`점 읽음: 맞음 ${ok}, 버림 ${bad} → ${FIT}`);
} else if (mode === "apply") {
  const j = data(), fit = JSON.parse(readFileSync(FIT, "utf8"));
  let changed = 0, kept = 0;
  for (const [race, r] of Object.entries(j.races)) {
    for (const k of Object.keys(r.front.parts).filter((k) => k.startsWith("armor_"))) {
      const a = fit[race]?.[k];
      const meta = await sharp(`${dirOf(race)}/${k}.png`).metadata();
      r.front.parts[k] = r.front.parts[k].map((old, i) => {
        const h = fit[race]?.[`head_${i + 1}`];
        if (!a || !h || !old) { kept++; return old; }
        const rect = fitArmor(h, a, meta);
        // 예전 측정이 실패해 크기 0이던 자리(갑옷이 안 보이던 머리)는 칸 크기 기준으로만 본다
        if (old[2] <= 0 || old[3] <= 0) { if (rect[2] < CELL * 0.6 || rect[2] > CELL * 2) { kept++; return old; } changed++; return rect; }
        // 너무 엉뚱하면(크기가 예전의 절반 아래·두 배 위) 점이 틀렸다고 보고 예전 자리를 둔다
        if (rect[2] < old[2] * 0.5 || rect[2] > old[2] * 2 || rect[3] < old[3] * 0.5 || rect[3] > old[3] * 2) { kept++; return old; }
        changed++;
        return rect;
      });
    }
  }
  writeFileSync(JSON_PATH, JSON.stringify(j));
  console.log(`갑옷 자리 다시 계산: ${changed}, 그대로 ${kept} → ${JSON_PATH}`);
} else console.log("sheets | read | apply");

/** 가장 가까운 윤곽 점 (반경 r 안). 없으면 원래 점 */
function snap(alpha, x, y, r) {
  const { data: a, info } = alpha, W = info.width, H = info.height;
  const op = (px, py) => px >= 0 && py >= 0 && px < W && py < H && a[py * W + px] > 128;
  let best = null, bd = Infinity;
  const x0 = Math.round(x), y0 = Math.round(y), R = Math.ceil(r);
  for (let py = y0 - R; py <= y0 + R; py++)
    for (let px = x0 - R; px <= x0 + R; px++) {
      if (!op(px, py)) continue;
      const edge = !op(px, py - 1) || !op(px - 1, py) || !op(px + 1, py) || px === 0 || px === W - 1;
      if (!edge) continue;
      const dd = (px - x) ** 2 + (py - y) ** 2;
      if (dd < bd && dd <= r * r) { bd = dd; best = [px, py]; }
    }
  return best ?? [Math.round(x), Math.round(y)];
}

/**
 * 갑옷 놓기: 가로는 어깨 끝 둘의 간격과 가운데를 맞추고, 세로는 '목 줄 → 어깨 끝 줄' 높이를 맞춰 늘린 뒤 목 줄을 몸의 목 줄에 놓는다.
 * 세로 배율은 가로 배율의 0.75~1.35배 안 (옷이 지나치게 찌그러지지 않게). 목둘레는 3px 위로 올려 살이 비치지 않게
 */
function fitArmor(h, a, meta) {
  const mid = (p, q) => [(p[0] + q[0]) / 2, (p[1] + q[1]) / 2];
  const sx = (h.SR[0] - h.SL[0]) / Math.max(1, a.SR[0] - a.SL[0]);
  const hN = mid(h.NL, h.NR), hS = mid(h.SL, h.SR), aN = mid(a.NL, a.NR), aS = mid(a.SL, a.SR);
  const dyA = aS[1] - aN[1], dyH = hS[1] - hN[1];
  let sy = dyA > 6 && dyH > 6 ? dyH / dyA : sx;
  sy = Math.min(sx * 1.35, Math.max(sx * 0.75, sy));
  const x = hS[0] - aS[0] * sx, y = hN[1] - aN[1] * sy - 3;
  const r1 = (v) => Math.round(v * 10) / 10;
  return [r1(x), r1(y), r1(meta.width * sx), r1(meta.height * sy)];
}
