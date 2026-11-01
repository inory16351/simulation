// 격자 시트(단색 마젠타 배경)를 칸별로 잘라 투명 배경 파츠 PNG로 저장한다.
// 사용: node tools/slice-sheet.mjs <시트.png> <열> <행> <출력1.png,출력2.png,...> [magenta|cyan]   ('-'는 건너뜀)
import { mkdirSync } from "node:fs";
import { dirname } from "node:path";
import sharp from "sharp";

const [input, colsArg, rowsArg, outsArg, keyArg = "magenta"] = process.argv.slice(2);
// 배경색: 마젠타(R·B 높고 G 낮음) 또는 청록(G·B 높고 R 낮음)
const cols = Number(colsArg), rows = Number(rowsArg);
const outs = outsArg.split(",");

const { data, info } = await sharp(input).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
const { width: W, height: H } = info;

// 마젠타에 가까운 픽셀을 투명하게, 가장자리는 반투명으로 하고 분홍 번짐을 뺀다
for (let i = 0; i < data.length; i += 4) {
  const r = data[i], g = data[i + 1], b = data[i + 2];
  const magenta = keyArg === "cyan" ? Math.min(g, b) - r : Math.min(r, b) - g; // 배경색에 가까울수록 큼
  if (magenta > 90) data[i + 3] = 0;
  else if (magenta > 35) data[i + 3] = Math.round(255 * (1 - (magenta - 35) / 55));
  // 번진 분홍기를 걷어 낸다: 빨강·파랑이 초록보다 많이 높으면 그 차이를 줄인다
  if (magenta > 12) {
    const k = Math.min(1, (magenta - 12) / 40);
    if (keyArg === "cyan") {
      data[i + 1] = Math.round(g - (g - r) * 0.7 * k);
      data[i + 2] = Math.round(b - (b - r) * 0.9 * k);
    } else {
      data[i] = Math.round(r - (r - g) * 0.7 * k);
      data[i + 2] = Math.round(b - (b - g) * 0.9 * k);
    }
  }
}
const keyed = sharp(data, { raw: { width: W, height: H, channels: 4 } });
const keyedBuf = await keyed.png().toBuffer();

const cw = Math.floor(W / cols), ch = Math.floor(H / rows);
let n = 0;
for (let r = 0; r < rows; r++) {
  for (let c = 0; c < cols; c++) {
    const out = outs[r * cols + c];
    if (!out || out === "-") continue;
    mkdirSync(dirname(out), { recursive: true });
    const cell = await sharp(keyedBuf).extract({ left: c * cw, top: r * ch, width: cw, height: ch }).png().toBuffer();
    const trimmed = await sharp(cell).trim({ threshold: 1 }).png().toBuffer();
    // 다른 파츠와 같은 512 캔버스 가운데에 놓는다
    const meta = await sharp(trimmed).metadata();
    const scale = Math.min(1, 460 / Math.max(meta.width, meta.height));
    const resized = await sharp(trimmed).resize(Math.round(meta.width * scale), Math.round(meta.height * scale)).png().toBuffer();
    const m2 = await sharp(resized).metadata();
    await sharp({ create: { width: 512, height: 512, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } } })
      .composite([{ input: resized, left: Math.round((512 - m2.width) / 2), top: Math.round((512 - m2.height) / 2) }])
      .png().toFile(out);
    n++;
  }
}
console.log(`잘라낸 파츠 ${n}개 (${W}x${H}, 칸 ${cw}x${ch})`);
