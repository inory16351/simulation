// 초상화 파츠 합성 미리보기. 배치 규칙(src/core/portraitLayout.ts)을 그대로 써서 PNG로 합성한다.
// 사용: npx tsx tools/preview-portrait.ts <출력.png> slot=파일 slot=파일 ...
import sharp from "sharp";
import { layoutPortrait, SLOT_ORDER, type PartMeta, type PartSlot } from "../src/core/portraitLayout";

const [outFile, ...pairs] = process.argv.slice(2);
const SIZE = 512;
const files = Object.fromEntries(pairs.map((p) => p.split("="))) as Partial<Record<PartSlot, string>>;

const trimmed: Partial<Record<PartSlot, { buf: Buffer; meta: PartMeta }>> = {};
for (const slot of SLOT_ORDER) {
  const f = files[slot];
  if (!f) continue;
  const { data, info } = await sharp(f).trim().png().toBuffer({ resolveWithObject: true });
  trimmed[slot] = { buf: data, meta: { w: info.width, h: info.height } };
}
const layout = layoutPortrait(Object.fromEntries(Object.entries(trimmed).map(([k, v]) => [k, v!.meta])));
const layers = [];
for (const slot of SLOT_ORDER) {
  const box = layout[slot];
  const t = trimmed[slot];
  if (!box || !t) continue;
  const w = Math.round(box.w * SIZE), h = Math.round(box.h * SIZE);
  const input = await sharp(t.buf).resize(w, h, { fit: "fill" }).png().toBuffer();
  // 캔버스 밖으로 나가는 부분은 잘라낸다
  const left = Math.round(box.x * SIZE), top = Math.round(box.y * SIZE);
  const cropL = Math.max(0, -left), cropT = Math.max(0, -top);
  const cw = Math.min(w - cropL, SIZE - Math.max(0, left)), ch = Math.min(h - cropT, SIZE - Math.max(0, top));
  const piece = await sharp(input).extract({ left: cropL, top: cropT, width: cw, height: ch }).toBuffer();
  layers.push({ input: piece, left: Math.max(0, left), top: Math.max(0, top) });
}
await sharp({ create: { width: SIZE, height: SIZE, channels: 4, background: "#1a1512" } }).composite(layers).png().toFile(outFile);
console.log("saved", outFile);
