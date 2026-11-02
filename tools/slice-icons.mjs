// 동화책 그림체 시트(tools/storybook.json)를 문장·메뉴 아이콘 PNG로 자른다. 자른 뒤 optimize-images.mjs로 webp를 만든다.
// 사용: node tools/slice-icons.mjs
import { existsSync, mkdirSync } from "node:fs";
import sharp from "sharp";

const CRESTS = ["aurelia", "dolgar", "karsea", "kemer", "grommar", "sylvaren"];
const ICONS = [
  "inbox", "overview", "roster", "training", "challenge",
  "match", "league", "market", "youth", "staff",
  "facilities", "finance", "cup", "world", "hall",
];
const SIZE = 256;

/** 마젠타를 지우고 칸별로 잘라, 칸 가장자리로 넘어온 이웃 그림 조각을 걷어 낸 뒤 꽉 차게 가운데 놓는다 */
async function slice(sheet, cols, rows, outs) {
  const { data, info } = await sharp(sheet).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  for (let i = 0; i < data.length; i += 4) {
    const r = data[i], g = data[i + 1], b = data[i + 2];
    const m = Math.min(r, b) - g;
    if (m > 90) data[i + 3] = 0;
    else if (m > 35) data[i + 3] = Math.round(255 * (1 - (m - 35) / 55));
    if (m > 12) { const k = Math.min(1, (m - 12) / 40); data[i] = Math.round(r - (r - g) * 0.7 * k); data[i + 2] = Math.round(b - (b - g) * 0.9 * k); }
  }
  const cw = Math.floor(info.width / cols), ch = Math.floor(info.height / rows);
  const band = Math.round(Math.min(cw, ch) * 0.045);
  for (let n = 0; n < outs.length; n++) {
    const cx = (n % cols) * cw, cy = Math.floor(n / cols) * ch;
    const cell = Buffer.alloc(cw * ch * 4);
    for (let y = 0; y < ch; y++) {
      for (let x = 0; x < cw; x++) {
        const s = ((cy + y) * info.width + cx + x) * 4, d = (y * cw + x) * 4;
        data.copy(cell, d, s, s + 4);
        // 시트 바깥 테두리가 아닌 칸 경계 근처는 비운다
        const edge = (x < band && cx > 0) || (x >= cw - band && cx + cw < info.width) || (y < band && cy > 0) || (y >= ch - band && cy + ch < info.height);
        if (edge) cell[d + 3] = 0;
      }
    }
    const trimmed = await sharp(cell, { raw: { width: cw, height: ch, channels: 4 } }).trim({ threshold: 8 }).png().toBuffer();
    mkdirSync(outs[n].replace(/\/[^/]+$/, ""), { recursive: true });
    await sharp(trimmed).resize(SIZE, SIZE, { fit: "contain", background: { r: 0, g: 0, b: 0, alpha: 0 } }).png().toFile(outs[n]);
  }
  console.log(`${sheet}: ${outs.length}개`);
}

await slice("art/source/sheets/crests.png", 3, 2, CRESTS.map((n) => `art/source/crest/${n}.png`));
// 따로 다시 그린 문장(sheets/crest_<나라>.png)이 있으면 시트 칸 대신 쓴다
for (const n of CRESTS) {
  if (existsSync(`art/source/sheets/crest_${n}.png`)) await slice(`art/source/sheets/crest_${n}.png`, 1, 1, [`art/source/crest/${n}.png`]);
}
await slice("art/source/sheets/icons.png", 5, 3, ICONS.map((n) => `art/source/icon/menu/${n}.png`));
