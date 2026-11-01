// 전투 리깅용 몸 파츠: 시트를 칸별로 자르고 관절 자리를 재서 data/rig.json과 public/assets/rig/에 쓴다.
// 팔다리는 세로로 그려져 있어 위끝·아래끝 둥근 마디의 가운데를 관절로 본다.
// 사용: node tools/rig-parts.mjs   (시트: art/source/rig/*.png, 3×3. 칸 이름은 SHEETS에)
import { existsSync, mkdirSync, writeFileSync, readFileSync } from "node:fs";
import { createRequire } from "node:module";
import sharp from "sharp";

const require = createRequire(import.meta.url);
const { load } = require("./lib/anchors.cjs");

const OUT = "public/assets/rig";
const TMP = "art/tmp/rig";
/** 시트별 칸 이름 (null은 빈칸) */
const SHEETS = [
  ["art/source/rig/body_parts.png", ["torso_tunic", "torso_leather", "torso_iron", "upper_arm", "forearm", "thigh", "shin", "shield", "cape"]],
  ["art/source/rig/body_parts2.png", ["chest_tunic", "chest_leather", "chest_iron", "hips_tunic", "hips_leather", "hips_iron", "thigh_bare", null, null]],
  ["art/source/rig/weapons.png", ["w_sword", "w_greatsword", "w_axe", "w_mace", "w_dagger", "w_spear", "w_trident", "w_javelin", "w_staff"]],
];

mkdirSync(OUT, { recursive: true });
mkdirSync(TMP, { recursive: true });

const r1 = (v) => Math.round(v * 10) / 10;
const parts = {};
for (const [sheet, names] of SHEETS) {
  if (!existsSync(sheet)) { console.log("시트 없음, 건너뜀:", sheet); continue; }
  // 마젠타 지우기 + 3×3 자르기
  const { data, info } = await sharp(sheet).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  for (let i = 0; i < data.length; i += 4) {
    const r = data[i], g = data[i + 1], b = data[i + 2];
    const mg = Math.min(r, b) - g;
    if (mg > 90) data[i + 3] = 0;
    else if (mg > 35) data[i + 3] = Math.round(255 * (1 - (mg - 35) / 55));
    if (mg > 12) { const k = Math.min(1, (mg - 12) / 40); data[i] = Math.round(r - (r - g) * 0.7 * k); data[i + 2] = Math.round(b - (b - g) * 0.9 * k); }
  }
  const keyed = await sharp(data, { raw: info }).png().toBuffer();
  const cw = Math.floor(info.width / 3), ch = Math.floor(info.height / 3);
  for (let i = 0; i < 9; i++) {
    const name = names[i];
    if (!name) continue;
    const cell = `${TMP}/${name}.png`;
    await sharp(keyed).extract({ left: (i % 3) * cw, top: Math.floor(i / 3) * ch, width: cw, height: ch }).resize(512, 512).png().toFile(cell);
    const m = await load(cell);
    const w = m.right - m.left + 1, h = m.bottom - m.top + 1;
    const buf = await sharp(await m.clean()).extract({ left: m.left, top: m.top, width: w, height: h }).png().toBuffer();
    await sharp(buf).webp({ quality: 88, alphaQuality: 90 }).toFile(`${OUT}/${name}.webp`);
    // 위끝·아래끝 마디: 끝에서 마디 반지름(그 줄 폭의 절반)만큼 안쪽, 그 줄의 가운데
    const midOf = (y) => { const [l, r] = m.row(y); return { x: (l + r) / 2, half: (r - l) / 2 }; };
    const topBand = midOf(Math.round(m.top + h * 0.06)), botBand = midOf(Math.round(m.bottom - h * 0.06));
    const top = { x: topBand.x - m.left, y: Math.min(h * 0.4, topBand.half) };
    const bottom = { x: botBand.x - m.left, y: h - Math.min(h * 0.4, botBand.half) };
    parts[name] = { w, h, top: [r1(top.x), r1(top.y)], bottom: [r1(bottom.x), r1(bottom.y)] };
  }
}

// 머리별 피부색: 정면 머리 그림의 볼 언저리 평균 (팔다리를 이 색으로 물들인다)
const P2 = JSON.parse(readFileSync("data/portrait2.json", "utf8"));
const skin = {};
for (const race of Object.keys(P2.races)) {
  skin[race] = [];
  for (let i = 0; i < P2.races[race].heads.length; i++) {
    const f = `art/tmp/portrait2/${race}/front/head_${i + 1}.png`;
    const { data: d, info: inf } = await sharp(f).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    let rs = 0, gs = 0, bs = 0, n = 0;
    // 이마 가운데: 그늘과 볼 홍조가 없다
    for (let y = Math.round(inf.height * 0.26); y < inf.height * 0.32; y++) for (let x = Math.round(inf.width * 0.44); x < inf.width * 0.56; x++) {
      const k = (y * inf.width + x) * 4; if (d[k + 3] < 250) continue; rs += d[k]; gs += d[k + 1]; bs += d[k + 2]; n++;
    }
    skin[race].push(n ? [Math.round(rs / n), Math.round(gs / n), Math.round(bs / n)] : [230, 180, 140]);
  }
}
// 팔다리 그림의 기본 피부색 (위팔 가운데)
const { data: ad, info: ai } = await sharp(`${TMP}/upper_arm.png`).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
let br = 0, bg = 0, bb = 0, bn = 0;
for (let y = 200; y < 300; y++) for (let x = 230; x < 280; x++) { const k = (y * ai.width + x) * 4; if (ad[k + 3] < 250) continue; br += ad[k]; bg += ad[k + 1]; bb += ad[k + 2]; bn++; }
const baseSkin = [Math.round(br / bn), Math.round(bg / bn), Math.round(bb / bn)];

writeFileSync("data/rig.json", JSON.stringify({ parts, skin, baseSkin }));
console.log("data/rig.json 저장", baseSkin, Object.keys(parts).length);
