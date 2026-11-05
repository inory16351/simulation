// 초상화 확인용: 종족의 모든 정면 머리에 파츠 하나를 얹어 격자로 그린다 (Unity Resources 기준, Portrait2.Layers와 같은 순서)
// 사용(저장소 루트): node ColosseumUnity/Tools/portrait-view.mjs <race> <part> [out.png] [json]
import sharp from "sharp";
import { readFileSync } from "node:fs";
const [race, part, out = `${process.env.TEMP}/pv_${race}_${part}.png`, jsonPath] = process.argv.slice(2);
const RES = "C:/Project/simulation/ColosseumUnity/Assets/Resources";
const j = JSON.parse(readFileSync(jsonPath ?? `${RES}/Data/portrait2.json`, "utf8"));
const f = j.races[race].front, C = j.cell, T = 200, cols = 6;
const dir = `${RES}/Art/portrait2/${race}/front`;
const n = f.heads.length, rows = Math.ceil(n / cols);
const tiles = [];
for (let i = 0; i < n; i++) {
  const comps = [{ input: `${dir}/head_${i + 1}.png`, left: 0, top: 0 }];
  const rect = f.parts[part]?.[i];
  const isArmor = part.startsWith("armor");
  async function put(name, r) {
    const w = Math.max(1, Math.round(r[2])), h = Math.max(1, Math.round(r[3]));
    let buf = await sharp(`${dir}/${name}.png`).resize(w, h, { fit: "fill" }).png().toBuffer();
    let left = Math.round(r[0]), top = Math.round(r[1]);
    // 칸 밖은 잘라 낸다
    const cl = Math.max(0, -left), ct = Math.max(0, -top), cw = Math.min(w - cl, C - left - cl), ch = Math.min(h - ct, C - top - ct);
    if (cw <= 0 || ch <= 0) return;
    buf = await sharp(buf).extract({ left: cl, top: ct, width: cw, height: ch }).png().toBuffer();
    if (name.startsWith("mark_")) {
      // 게임과 같이 머리 실루엣 안에만 (PortraitView MarkMask)
      const layer = await sharp({ create: { width: C, height: C, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } } }).composite([{ input: buf, left: left + cl, top: top + ct }]).png().toBuffer();
      const masked = await sharp(layer).composite([{ input: `${dir}/head_${i + 1}.png`, blend: "dest-in" }]).png().toBuffer();
      const chin = Math.round(f.heads[i].chin);
      comps.push({ input: await sharp(masked).extract({ left: 0, top: 0, width: C, height: Math.min(C, chin) }).png().toBuffer(), left: 0, top: 0 });
      return;
    }
    comps.push({ input: buf, left: left + cl, top: top + ct });
  }
  if (rect) await put(part, rect);
  if (rect && isArmor) {
    const chin = Math.round(f.heads[i].chin);
    const top = await sharp(`${dir}/head_${i + 1}.png`).extract({ left: 0, top: 0, width: C, height: Math.min(C, chin) }).png().toBuffer();
    comps.push({ input: top, left: 0, top: 0 });
  }
  const img = await sharp({ create: { width: C, height: C, channels: 4, background: "#2a231e" } }).composite(comps).png().toBuffer();
  tiles.push({ input: await sharp(img).resize(T, T).png().toBuffer(), left: (i % cols) * T, top: Math.floor(i / cols) * T });
}
await sharp({ create: { width: cols * T, height: rows * T, channels: 4, background: "#000" } }).composite(tiles).png().toFile(out);
console.log(out);
