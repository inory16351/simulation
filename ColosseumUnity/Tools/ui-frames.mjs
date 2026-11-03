// Codex가 그린 UI 조각 시트(art/source/ui/ui_frames.png: 위 패널, 가운데 버튼, 아래 제목 줄)를
// 색을 입힐 수 있는 9-slice 스프라이트로 바꾼다. 마젠타를 지우고 조각을 덩어리로 찾아 자른 뒤
// 명암만 남긴 밝은 회색으로 만든다 (Image.color가 테마 색을 칠한다 → 탭 선택 색 같은 기존 로직 그대로).
//   brush_frame.png  패널 전체 192x128 (9-slice 18)
//   brush_fill.png   패널 안쪽 바탕 128x128 (9-slice 8)
//   brush_button.png 버튼 256x48 (9-slice 16)
//   brush_head.png   제목 줄 320x36 (9-slice 12)
// 사용 (저장소 루트에서): node ColosseumUnity/Tools/ui-frames.mjs   (경계는 Editor/ArtImporter.cs)
import sharp from "sharp";

const SRC = "art/source/ui/ui_frames.png";
const OUT = "ColosseumUnity/Assets/Resources/Art/ui";

const { data, info } = await sharp(SRC).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
const W = info.width, H = info.height;
// 마젠타 → 투명
for (let i = 0; i < data.length; i += 4) {
  const m = Math.min(data[i], data[i + 2]) - data[i + 1];
  if (m > 90) data[i + 3] = 0;
  else if (m > 35) data[i + 3] = Math.round(255 * (1 - (m - 35) / 55));
}
// 덩어리 셋 (큰 것만): 위에서부터 패널·버튼·제목 줄
const label = new Int32Array(W * H).fill(-1), comps = [];
for (let p = 0; p < W * H; p++) {
  if (label[p] >= 0 || data[p * 4 + 3] < 128) continue;
  const k = comps.length, stack = [p];
  let n = 0, x0 = W, y0 = H, x1 = 0, y1 = 0;
  label[p] = k;
  while (stack.length) {
    const q = stack.pop(), x = q % W, y = (q - x) / W;
    n++; x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y);
    for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
      const nx = x + dx, ny = y + dy;
      if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
      const r = ny * W + nx;
      if (label[r] < 0 && data[r * 4 + 3] >= 128) { label[r] = k; stack.push(r); }
    }
  }
  comps.push({ n, x0, y0, x1, y1 });
}
const pieces = comps.sort((a, b) => b.n - a.n).slice(0, 3).sort((a, b) => a.y0 - b.y0);
if (pieces.length < 3) throw new Error(`조각이 ${pieces.length}개뿐입니다`);

/** 조각을 잘라 명암만 남긴 밝은 회색으로 (가장 밝은 곳이 0.95) */
async function gray(c, inset = 0) {
  const w = c.x1 - c.x0 + 1, h = c.y1 - c.y0 + 1;
  const ix = Math.round(w * inset), iy = Math.round(h * inset);
  const buf = await sharp(data, { raw: { width: W, height: H, channels: 4 } })
    .extract({ left: c.x0 + ix, top: c.y0 + iy, width: w - ix * 2, height: h - iy * 2 }).raw().toBuffer();
  let max = 1;
  for (let i = 0; i < buf.length; i += 4) max = Math.max(max, 0.3 * buf[i] + 0.59 * buf[i + 1] + 0.11 * buf[i + 2]);
  for (let i = 0; i < buf.length; i += 4) {
    const l = (0.3 * buf[i] + 0.59 * buf[i + 1] + 0.11 * buf[i + 2]) / max;
    // 너무 어두운 바탕도 색이 보이게 바닥을 조금 올린다
    const v = Math.round(255 * Math.min(1, 0.22 + l * 0.78) * 0.95);
    buf[i] = buf[i + 1] = buf[i + 2] = v;
  }
  return sharp(buf, { raw: { width: w - ix * 2, height: h - iy * 2, channels: 4 } });
}

const [panel, button, head] = pieces;
await (await gray(panel)).resize(192, 128, { fit: "fill" }).png().toFile(`${OUT}/brush_frame.png`);
await (await gray(panel, 0.14)).resize(128, 128, { fit: "fill" }).png().toFile(`${OUT}/brush_fill.png`);
await (await gray(button)).resize(256, 48, { fit: "fill" }).png().toFile(`${OUT}/brush_button.png`);
await (await gray(head)).resize(320, 36, { fit: "fill" }).png().toFile(`${OUT}/brush_head.png`);
console.log(`패널 ${panel.x1 - panel.x0}x${panel.y1 - panel.y0}, 버튼 ${button.x1 - button.x0}x${button.y1 - button.y0}, 제목 줄 ${head.x1 - head.x0}x${head.y1 - head.y0} → ${OUT}/brush_*.png`);
