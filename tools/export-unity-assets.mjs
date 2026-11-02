// 웹판 그림을 Unity 프로젝트로 내보낸다. Unity는 webp를 못 읽으므로 PNG로 바꾼다.
// 사용: node tools/export-unity-assets.mjs   (그림을 다시 만든 뒤 매번 돌린다)
//   public/assets/<경로>.webp → ColosseumUnity/Assets/Resources/Art/<경로>.png
//   public/assets/audio/*     → ColosseumUnity/Assets/Audio/
// 초상화 시트 원본(portrait2/<종족>/front_*.webp 등)은 게임에서 안 쓰므로 뺀다.
import { copyFileSync, existsSync, mkdirSync, readdirSync, statSync } from "node:fs";
import { dirname, join, relative } from "node:path";
import sharp from "sharp";

const SRC = "public/assets";
const ART = "ColosseumUnity/Assets/Resources/Art";
const AUDIO = "ColosseumUnity/Assets/Audio";
const FOLDERS = ["portrait2", "icon", "crest", "fx", "ui", "rig"];

function* walk(dir) {
  for (const name of readdirSync(dir)) {
    const p = join(dir, name);
    if (statSync(p).isDirectory()) yield* walk(p);
    else yield p;
  }
}

/** portrait2 바로 아래 종족 폴더의 시트 원본은 건너뛴다 (front/, side/ 하위만 쓴다) */
const unusedSheet = (rel) => /^portrait2[\\/][^\\/]+[\\/][^\\/]+\.webp$/.test(rel);

let n = 0, skipped = 0;
for (const folder of FOLDERS) {
  const root = join(SRC, folder);
  if (!existsSync(root)) continue;
  for (const file of walk(root)) {
    const rel = relative(SRC, file);
    if (!file.endsWith(".webp") || unusedSheet(rel)) { skipped++; continue; }
    const out = join(ART, rel).replace(/\.webp$/, ".png");
    if (existsSync(out) && statSync(out).mtimeMs >= statSync(file).mtimeMs) continue;
    mkdirSync(dirname(out), { recursive: true });
    await sharp(file).png({ compressionLevel: 9 }).toFile(out);
    n++;
  }
}
mkdirSync(AUDIO, { recursive: true });
for (const f of readdirSync(join(SRC, "audio"))) copyFileSync(join(SRC, "audio", f), join(AUDIO, f));
// 게임 데이터도 같이 맞춘다 (data/*.json → Resources/Data)
const DATA = "ColosseumUnity/Assets/Resources/Data";
mkdirSync(DATA, { recursive: true });
for (const f of readdirSync("data").filter((f) => f.endsWith(".json"))) copyFileSync(join("data", f), join(DATA, f));
console.log(`PNG ${n}개 내보냄, ${skipped}개 건너뜀, 데이터 JSON 동기화`);
