// 그림 초상화 파츠 빌드: 시트를 칸별로 자르고(마젠타 배경 제거), 붙을 자리를 재서
// 머리×파츠마다 놓을 사각형을 미리 계산해 data/portrait2.json과 public/assets/portrait2/에 쓴다.
// 사용: node tools/portrait-parts.mjs
// 시트 위치: art/source/portrait2/<종족>/{front,side}_{heads,parts}.png (3×3 격자)
import { mkdirSync, existsSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";
import sharp from "sharp";

const require = createRequire(import.meta.url);
const { headAnchors, partAnchors, sideHeadAnchors, sidePartAnchors } = require("./lib/anchors.cjs");

const CELL = 512;
const SRC = "art/source/portrait2";
const OUT = "public/assets/portrait2";
const TMP = "art/tmp/portrait2";

/** 종족별 시트 구성. 머리 칸 순서대로 성별, 파츠 칸 순서대로 종류 */
const RACES = {
  human: {
    heads: ["f", "m", "m", "m", "m", "f", "m", "m", "f"],
    front: ["hair_short", "hair_long", "hair_braids", "beard_full", "beard_short", "helmet_crest", "helmet_visor", "armor_harness", "armor_plate"],
    side: ["hair_short", "hair_long", "hair_braids", "beard_full", "beard_short", "helmet_crest", "helmet_visor", null, null],
  },
};

/** 파츠 종류 → 재는 방법 */
const KIND = {
  hair_short: "hair_cap", hair_long: "hair", hair_braids: "hair",
  helmet_crest: "helmet_open", helmet_visor: "helmet",
  beard_full: "beard", beard_short: "beard",
  armor_harness: "armor", armor_plate: "armor",
};

/** 마젠타를 지우고 3×3 칸을 512 정사각형으로 (자르기만, 가운데 맞추기는 하지 않는다 → 템플릿 위치 유지) */
async function sliceSheet(file, outPrefix) {
  const { data, info } = await sharp(file).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  for (let i = 0; i < data.length; i += 4) {
    const r = data[i], g = data[i + 1], b = data[i + 2];
    const mg = Math.min(r, b) - g;
    if (mg > 90) data[i + 3] = 0;
    else if (mg > 35) data[i + 3] = Math.round(255 * (1 - (mg - 35) / 55));
    if (mg > 12) { const k = Math.min(1, (mg - 12) / 40); data[i] = Math.round(r - (r - g) * 0.7 * k); data[i + 2] = Math.round(b - (b - g) * 0.9 * k); }
  }
  const keyed = await sharp(data, { raw: info }).png().toBuffer();
  const cw = Math.floor(info.width / 3), ch = Math.floor(info.height / 3);
  const files = [];
  for (let i = 0; i < 9; i++) {
    const f = `${outPrefix}_${i + 1}.png`;
    await sharp(keyed).extract({ left: (i % 3) * cw, top: Math.floor(i / 3) * ch, width: cw, height: ch }).resize(CELL, CELL).png().toFile(f);
    files.push(f);
  }
  return files;
}

const r1 = (v) => Math.round(v * 10) / 10;

/** 정면: 머리 기준선과 파츠 기준점으로 놓을 사각형 */
function placeFront(name, a, h) {
  const brow = h.eyeY - h.eyeGap * 0.42, hairline = h.eyeY - h.eyeGap * 0.62, nose = h.eyeY + h.eyeGap * 0.42;
  let p;
  switch (KIND[name]) {
    case "hair": p = { s: (h.faceW * 1.02) / a.openW, px: a.massCx, py: a.openTop, hx: h.cx, hy: hairline }; break;
    case "hair_cap": p = { s: (h.headW * 1.08) / a.coreW, px: a.massCx, py: a.bangs, hx: h.cx, hy: hairline + h.eyeGap * 0.12 }; break;
    case "helmet_open": p = { s: (h.faceW * 0.98) / a.openW, px: a.massCx, py: a.openTop, hx: h.cx, hy: brow }; break;
    case "helmet": p = { s: (h.headW * 1.12) / a.coreW, px: a.massCx, py: a.top + a.h, hx: h.cx, hy: h.chin + h.eyeGap * 0.12 }; break;
    case "beard": p = { s: (h.faceW * (name === "beard_full" ? 0.74 : 0.7)) / a.coreW, px: a.massCx, py: a.mustacheTop, hx: h.cx, hy: nose }; break;
    case "armor": p = { s: (h.shW * 1.1) / a.coreW, px: a.massCx, py: a.top, hx: h.shCx, hy: h.neckBase + (name === "armor_plate" ? -h.eyeGap * 0.12 : h.eyeGap * 0.02) }; break;
  }
  return [r1(p.hx - (p.px - a.left) * p.s), r1(p.hy - (p.py - a.top) * p.s), r1(a.w * p.s), r1(a.h * p.s)];
}

/** 측면(오른쪽을 봄): 정수리·뒤통수·이마 앞·눈·턱으로 맞춘다 */
function placeSide(name, a, h) {
  let p;
  switch (KIND[name]) {
    // 머리카락: 폭을 머리 길이(뒤통수~이마)에 맞추고, 앞 가장자리를 이마 앞에, 윗가장자리를 정수리 조금 위에
    case "hair": case "hair_cap": p = { s: (h.headLen * 1.12) / a.coreW, px: a.front, py: a.top, hx: h.front + h.headLen * 0.02, hy: h.top - h.headLen * 0.06 }; break;
    // 투구: 깃털 때문에 질량 중심은 못 쓴다. 앞 가장자리를 얼굴 앞보다 조금 앞에, 아랫가장자리를 턱 아래에
    case "helmet_open": p = { s: (h.headLen * 1.42) / a.coreW, px: a.front, py: a.top + a.h, hx: h.front + h.headLen * 0.06, hy: h.chin + h.headLen * 0.06 }; break;
    case "helmet": p = { s: (h.headLen * 1.3) / a.coreW, px: a.front, py: a.top + a.h, hx: h.front + h.headLen * 0.1, hy: h.chin + h.headLen * 0.12 }; break;
    // 수염: 앞 가장자리를 얼굴 앞에, 윗가장자리를 코 밑에
    case "beard": p = { s: (h.headLen * (name === "beard_full" ? 0.62 : 0.55)) / a.coreW, px: a.front, py: a.top, hx: h.front - h.headLen * 0.02, hy: h.nose }; break;
    default: return null;
  }
  return [r1(p.hx - (p.px - a.left) * p.s), r1(p.hy - (p.py - a.top) * p.s), r1(a.w * p.s), r1(a.h * p.s)];
}

const json = { cell: CELL, races: {} };
for (const [race, cfg] of Object.entries(RACES)) {
  const entry = { heads: cfg.heads, front: { heads: [], parts: {} }, side: { heads: [], parts: {} } };
  for (const view of ["front", "side"]) {
    const headsSheet = `${SRC}/${race}/${view}_heads.png`, partsSheet = `${SRC}/${race}/${view}_parts.png`;
    if (!existsSync(headsSheet) || !existsSync(partsSheet)) { console.log(`건너뜀: ${race} ${view} (시트 없음)`); continue; }
    const dir = `${TMP}/${race}/${view}`; mkdirSync(dir, { recursive: true });
    const outDir = `${OUT}/${race}/${view}`; mkdirSync(outDir, { recursive: true });
    const headFiles = await sliceSheet(headsSheet, `${dir}/head`);
    const partFiles = await sliceSheet(partsSheet, `${dir}/part`);
    const heads = [];
    for (let i = 0; i < 9; i++) {
      const h = view === "front" ? await headAnchors(headFiles[i]) : await sideHeadAnchors(headFiles[i]);
      heads.push(h);
      await sharp(headFiles[i]).webp({ quality: 88, alphaQuality: 90 }).toFile(`${outDir}/head_${i + 1}.webp`);
      // 정면 머리는 턱 줄(갑옷 위로 다시 그릴 얼굴 부분)을, 측면은 목 끝(몸에 붙는 점)을 기록
      entry[view].heads.push(view === "front"
        ? { chin: r1(h.chin + h.eyeGap * 0.08) }
        : { neckX: r1(h.neckX), neckY: r1(h.neckBottom), chin: r1(h.chin) });
    }
    const names = cfg[view];
    for (let i = 0; i < 9; i++) {
      const name = names[i];
      if (!name) continue;
      const k = KIND[name] === "hair_cap" ? "hair" : KIND[name];
      const a = view === "front" ? await partAnchors(partFiles[i], k) : await sidePartAnchors(partFiles[i], k);
      await sharp(a.buf).webp({ quality: 88, alphaQuality: 90 }).toFile(`${outDir}/${name}.webp`);
      const place = heads.map((h) => (view === "front" ? placeFront(name, a, h) : placeSide(name, a, h)));
      entry[view].parts[name] = place;
    }
    console.log(`${race} ${view}: 머리 ${heads.length}, 파츠 ${Object.keys(entry[view].parts).length}`);
  }
  json.races[race] = entry;
}
writeFileSync("data/portrait2.json", JSON.stringify(json));
console.log("data/portrait2.json 저장");
