// 생성된 초상화 파츠(art/source/portrait/parts)의 투명 여백을 잘라 webp로 만들고,
// 파츠 목록과 크기를 data/portrait-parts.json에 쓴다. 웹과 Unity가 같은 목록을 읽는다.
// 사용: node tools/build-portrait-parts.mjs
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname } from "node:path";
import sharp from "sharp";

const manifest = JSON.parse(readFileSync("tools/portrait-parts.json", "utf8"));

// 코는 얼굴형 피부색에 맞춘 변형을 미리 만든다 (웹·Unity 공통으로 파일만 고르면 된다)
const TONES = {
  pale: { brightness: 1.22, saturation: 0.22 },
  dark: { brightness: 0.55, saturation: 0.85 },
  dusk: { brightness: 0.78, saturation: 0.15, tint: { r: 120, g: 140, b: 175 } },
  grey: { brightness: 0.95, saturation: 0.45 },
};
const parts = [];
for (const it of manifest.items) {
  if (!existsSync(it.out)) continue;
  const rel = it.out.replace("art/source/portrait/parts/", "").replace(/\.png$/, ".webp");
  const out = `public/assets/portrait/parts/${rel}`;
  mkdirSync(dirname(out), { recursive: true });
  const { data, info } = await sharp(it.out).trim({ threshold: 8 }).png().toBuffer({ resolveWithObject: true });
  await sharp(data).webp({ quality: 88, alphaQuality: 95 }).toFile(out);
  const tones = {};
  if (it.slot === "nose" && it.tone) {
    for (const [tone, m] of Object.entries(TONES)) {
      if (tone === it.tone) continue;
      const tOut = out.replace(/.webp$/, `@${tone}.webp`);
      let img = sharp(data).modulate({ brightness: m.brightness, saturation: m.saturation });
      if (m.tint) img = img.tint(m.tint);
      await img.webp({ quality: 88, alphaQuality: 95 }).toFile(tOut);
      tones[tone] = tOut.replace("public/assets/", "");
    }
  }
  parts.push({
    id: it.id,
    slot: it.slot,
    file: `portrait/parts/${rel}`,
    w: info.width,
    h: info.height,
    ...(it.races ? { races: it.races } : {}),
    ...(it.gender ? { gender: it.gender } : {}),
    ...(it.tags ? { tags: it.tags } : {}),
    ...(it.weight ? { weight: it.weight } : {}),
    ...(it.fit ? { fit: it.fit } : {}),
    ...(it.tone ? { tone: it.tone } : {}),
    ...(Object.keys(tones).length ? { tones } : {}),
  });
}
writeFileSync("data/portrait-parts.json", JSON.stringify(parts, null, 2) + "\n");
const bySlot = parts.reduce((m, p) => ((m[p.slot] = (m[p.slot] ?? 0) + 1), m), {});
console.log(`파츠 ${parts.length}개`, bySlot);
