// art/source의 원본 이미지를 웹용 webp로 줄여 public/assets에 쓴다.
// 원본(고해상도)은 Unity 이식 때 다시 쓰기 위해 art/source에 그대로 둔다.
// 사용: node tools/optimize-images.mjs
import { existsSync, mkdirSync, readdirSync, statSync } from "node:fs";
import { dirname, join, relative } from "node:path";
import sharp from "sharp";

const SRC = "art/source";
const OUT = "public/assets";

// 경로 접두어별 최대 크기와 품질
const RULES = [
  { prefix: "portrait/parts/", max: 512, quality: 88 },
  { prefix: "portrait/", max: 256, quality: 82 },
  { prefix: "icon/", max: 64, quality: 90 },
  { prefix: "crest/", max: 128, quality: 90 },
  { prefix: "fx/", max: 256, quality: 85 },
  { prefix: "ui/", max: 1920, quality: 72 },
  { prefix: "style/", max: 1280, quality: 75 },
  { prefix: "", max: 1024, quality: 80 },
];

function walk(dir) {
  return readdirSync(dir).flatMap((f) => {
    const p = join(dir, f);
    return statSync(p).isDirectory() ? walk(p) : [p];
  });
}

let count = 0;
let before = 0;
let after = 0;
for (const file of walk(SRC).filter((f) => /\.(png|jpe?g)$/i.test(f))) {
  const rel = relative(SRC, file).replace(/\\/g, "/");
  const out = join(OUT, rel.replace(/\.(png|jpe?g)$/i, ".webp"));
  if (existsSync(out) && statSync(out).mtimeMs >= statSync(file).mtimeMs) continue;
  const rule = RULES.find((r) => rel.startsWith(r.prefix));
  mkdirSync(dirname(out), { recursive: true });
  await sharp(file)
    .resize(rule.max, rule.max, { fit: "inside", withoutEnlargement: true })
    .webp({ quality: rule.quality, alphaQuality: 90 })
    .toFile(out);
  before += statSync(file).size;
  after += statSync(out).size;
  count++;
}
console.log(`변환 ${count}개: ${(before / 1e6).toFixed(1)}MB → ${(after / 1e6).toFixed(2)}MB`);
