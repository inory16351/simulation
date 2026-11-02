// 파츠 점검표 PNG: 종족 하나에 대해, 줄 = 파츠 하나, 칸 = 뽑히는 머리 전부. 갑옷은 얼굴 다시 그리기까지 흉내 낸다
// 사용: node tools/render-part-audit.mjs <종족> <출력.png>
import sharp from "sharp";
import { readFileSync } from "node:fs";
const D = JSON.parse(readFileSync("data/portrait2.json", "utf8"));
const race = process.argv[2], out = process.argv[3];
const v = D.races[race].front;
const S = 140, C = 512, k = S / C;
// 뽑히는 머리(성별이 '-'가 아닌 것)만
const usable = D.races[race].heads.map((g, i) => [g, i]).filter(([g]) => g !== "-").map(([, i]) => i);
const HEADS = usable;
// '_lo'(수염 아래 조각)는 줄을 따로 두지 않고 위 조각과 같이 그린다
const parts = Object.keys(v.parts).filter((n) => !n.endsWith("_lo")).sort();
const dir = `public/assets/portrait2/${race}/front`;
async function place(file, rect) {
  const pw = Math.max(1, Math.round(rect[2] * k)), ph = Math.max(1, Math.round(rect[3] * k));
  const img = await sharp(file).resize(pw, ph, { fit: "fill" }).png().toBuffer();
  const L = Math.round(rect[0] * k), T = Math.round(rect[1] * k);
  const ext = { left: Math.max(0, -L), top: Math.max(0, -T) };
  const w = Math.min(pw - ext.left, S - Math.max(0, L)), h = Math.min(ph - ext.top, S - Math.max(0, T));
  if (w <= 0 || h <= 0) return null;
  return { input: await sharp(img).extract({ ...ext, width: w, height: h }).png().toBuffer(), left: Math.max(0, L), top: Math.max(0, T) };
}
const tiles = [];
for (let r = 0; r < parts.length; r++) {
  for (let c = 0; c < HEADS.length; c++) {
    const hi = HEADS[c];
    const head = await sharp(`${dir}/head_${hi + 1}.webp`).resize(S, S).png().toBuffer();
    // 닫힌 투구는 게임처럼 투구 아래쪽 위로 머리를 지운다 (portrait2.ts VISOR_HIDES_HEAD)
    const vr = parts[r].startsWith("helmet_visor") && ["human", "elf", "orc", "dwarf"].includes(race) ? v.parts[parts[r]][hi] : null;
    const cut = vr ? Math.round((vr[1] + vr[3] * 0.7) * k) : 0;
    const comps = [cut ? { input: await sharp(head).extract({ left: 0, top: cut, width: S, height: S - cut }).png().toBuffer(), left: 0, top: cut } : { input: head, left: 0, top: 0 }];
    const p = await place(`${dir}/${parts[r]}.webp`, v.parts[parts[r]][hi]);
    if (p) comps.push(p);
    if (v.parts[parts[r] + "_lo"]) { const lo = await place(`${dir}/${parts[r]}_lo.webp`, v.parts[parts[r] + "_lo"][hi]); if (lo) comps.push(lo); }
    if (parts[r].startsWith("armor")) {
      const chin = Math.round(v.heads[hi].chin * k);
      comps.push({ input: await sharp(head).extract({ left: 0, top: 0, width: S, height: chin }).png().toBuffer(), left: 0, top: 0 });
    }
    tiles.push({ input: await sharp({ create: { width: S, height: S, channels: 4, background: "#2a1d22" } }).composite(comps).png().toBuffer(), left: 150 + c * (S + 4), top: r * (S + 4) });
  }
  const label = Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="146" height="${S}"><text x="4" y="${S / 2}" font-size="15" fill="#fff" font-family="sans-serif">${parts[r]}</text></svg>`);
  tiles.push({ input: label, left: 0, top: r * (S + 4) });
}
await sharp({ create: { width: 150 + HEADS.length * (S + 4), height: parts.length * (S + 4), channels: 4, background: "#000" } }).composite(tiles).png().toFile(out);
console.log("saved", race, parts.length);
