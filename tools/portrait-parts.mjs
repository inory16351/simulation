// 그림 초상화 파츠 빌드: 시트를 칸별로 자르고(마젠타 배경 제거), 붙을 자리를 재서
// 머리×파츠마다 놓을 사각형을 미리 계산해 data/portrait2.json과 public/assets/portrait2/에 쓴다.
// 사용: node tools/portrait-parts.mjs [종족...]   (종족을 주면 그 종족만)
// 시트 위치: art/source/portrait2/<종족>/{front,side}_{heads,parts}.png (3×3 격자)
import { mkdirSync, existsSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";
import sharp from "sharp";

const require = createRequire(import.meta.url);
const { headAnchors, partAnchors, sideHeadAnchors, sidePartAnchors } = require("./lib/anchors.cjs");

const CELL = 512;
const SRC = "art/source/portrait2";
const OUT = "public/assets/portrait2";
const TMP = "art/tmp/portrait2";

/** 종족별 시트 구성. 머리 칸 순서대로 성별('-' = 뽑지 않음, 예: 늙은 얼굴), 파츠 칸 순서대로 종류 */
const RACES = {
  human: {
    heads: ["f", "m", "m", "f", "m", "-", "f", "m", "-"],
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
    if (mg > 75) data[i + 3] = 0;
    else if (mg > 30) data[i + 3] = Math.round(255 * (1 - (mg - 30) / 45));
    if (mg > 4) { const k = Math.min(1, (mg - 4) / 20); data[i] = Math.round(r - (r - g) * 0.75 * k); data[i + 2] = Math.round(b - (b - g) * k); }
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

/**
 * 정면: 시트에 그려진 자리 그대로 놓는다. 파츠는 머리 시트와 같은 틀 위에 그려 받았으므로,
 * 칸 안의 위치가 곧 붙을 자리다. 머리마다 정수리·가운데·폭이 틀 평균과 조금씩 다르니 그만큼만 옮기고 늘린다.
 * (예전엔 얼굴 구멍 폭을 재서 맞췄는데, 코 가리개·볏이 가운데를 막으면 구멍을 작게 재서 투구가 몇 배로 커졌다)
 */
function placeFrontSheet(a, h, ref) {
  const s = h.headW / ref.headW;
  return [r1(h.cx + (a.left - ref.cx) * s), r1(h.top + (a.top - ref.top) * s), r1(a.w * s), r1(a.h * s)];
}

/**
 * 시트 자리를 믿는 파츠: 투구는 틀 위 제자리에 그려져 온다.
 * 머리카락은 placeHair, 수염·갑옷은 시트에서 위아래로 밀려 그려지는 일이 잦아 코·목 위치를 재서 맞춘다 (placeFront)
 */
const SHEET_KINDS = new Set(["helmet_open", "helmet"]);

/**
 * 머리카락: 크기는 시트 그대로(틀 기준으로 그려 와서 폭이 맞다), 가로는 질량 중심을 얼굴 가운데에,
 * 세로는 정수리 조금 위에 머리카락 꼭대기를 맞춘다. 시트에선 머리카락이 아래로 밀려 그려지는 일이 잦고,
 * 얼굴 구멍 폭으로 재면 옆머리가 얼굴을 덮는 긴 머리에서 몇 배로 커진다.
 */
function placeHair(a, h, ref) {
  const s = h.headW / ref.headW;
  return [r1(h.cx - (a.massCx - a.left) * s), r1(h.top - h.headW * 0.05), r1(a.w * s), r1(a.h * s)];
}

/** 기준점 지도(tools/gen-anchor-maps.mjs → tools/save-anchors.mjs)에서 읽은 칸별 점. 없으면 null */
function loadDots(sheetFile) {
  if (!sheetFile) return null;
  const f = sheetFile.replace(/([^/]+).png$/, "anchors/$1.json");
  return existsSync(f) ? JSON.parse(readFileSync(f, "utf8")).cells : null;
}
/** 눈으로 확인하고 손본 점 지도(JSON에 checked: true)는 재서 맞춘 자리와 비교하지 않고 그대로 믿는다 */
function dotsChecked(sheetFile) {
  const f = sheetFile?.replace(/([^/]+).png$/, "anchors/$1.json");
  return !!f && existsSync(f) && JSON.parse(readFileSync(f, "utf8")).checked === true;
}
/** 파츠 종류별로 맞출 점 둘 */
const DOT_PAIR = { hair: ["crown", "brow"], hair_cap: ["crown", "brow"], helmet_open: ["crown", "brow"], helmet: ["crown", "brow"], beard: ["nose", "mouth"], armor: ["neck", "chin"], mark: ["brow", "nose"] };
const eyeDist = (d) => (d?.eyeL && d?.eyeR ? d.eyeR[0] - d.eyeL[0] : null);
const mid = (d, [p, q]) => (d?.[p] && d?.[q] ? [(d[p][0] + d[q][0]) / 2, (d[p][1] + d[q][1]) / 2] : null);

/**
 * 점 맞추기: 파츠에 찍힌 점 둘의 가운데를 머리의 같은 점 둘의 가운데에 놓는다.
 * 크기는 그 머리의 눈 사이 거리 ÷ 파츠를 그린 틀(첫 세트 머리)의 평균 눈 사이 거리. 점이 모자라면 null
 */
function placeByDots(name, a, h, partDots, refEye) {
  const pair = DOT_PAIR[kindOf(name)];
  const pm = mid(partDots, pair), hm = mid(h.dots, pair);
  const e = eyeDist(h.dots);
  if (!pm || !hm || !e || !refEye) return null;
  // 파츠 점은 그림 모델이 짐작해 찍은 것이라 틀릴 수 있다: 가운데 줄에서 벗어났거나 위아래가 뒤집혔으면 버린다
  const [p, q] = pair;
  // (흉터·문신은 얼굴 한쪽에만 있을 수 있어 가운데 검사를 하지 않는다)
  if ((kindOf(name) !== "mark" && Math.abs(pm[0] - a.massCx) > a.coreW * 0.2) || partDots[p][1] >= partDots[q][1]) return null;
  // 흉터·문신은 점 사이 거리로 크기를 정한다 (시트에 크게 그려 온 것을 점 간격으로 줄일 수 있게)
  const s = kindOf(name) === "mark" ? (h.dots[q][1] - h.dots[p][1]) / (partDots[q][1] - partDots[p][1]) : e / refEye;
  return [r1(hm[0] - (pm[0] - a.left) * s), r1(hm[1] - (pm[1] - a.top) * s), r1(a.w * s), r1(a.h * s)];
}

/**
 * 수염 위아래 나누기: 턱 길이가 머리마다 달라 통째로 놓으면 턱선이 어긋난다.
 * 입 줄에서 잘라 위(콧수염·구레나룻)는 코 점에, 아래(턱수염)는 입~턱 점 사이에 세로로 늘려 맞춘다.
 * 파츠·머리 점이 다 있을 때만. 반환: { hi, lo } 사각형과 자를 줄(파츠 잘라 낸 그림 기준 y). 안 되면 null
 */
const BEARD_OVERLAP = 6;
function splitBeard(a, h, pd, refEye, refJaw) {
  const e = eyeDist(h.dots);
  if (!pd?.nose || !pd?.mouth || !pd?.chin || !h.dots?.nose || !h.dots?.mouth || !h.dots?.chin || !e || !refEye) return null;
  if (!(pd.nose[1] < pd.mouth[1] && pd.mouth[1] < pd.chin[1])) return null;
  // 가로는 턱 폭에 맞춘다 (턱이 넓은 머리에서 구레나룻이 얼굴 안쪽에 떠 보이지 않게). 턱 폭을 못 쟀으면 눈 사이 거리
  const se = e / refEye, s = h.jawW && refJaw ? h.jawW / refJaw : se;
  const cut = Math.round(pd.mouth[1] - a.top);
  if (cut <= BEARD_OVERLAP || cut >= a.h - BEARD_OVERLAP) return null;
  const sy = Math.min(1.5 * se, Math.max(0.7 * se, (h.dots.chin[1] - h.dots.mouth[1]) / (pd.chin[1] - pd.mouth[1])));
  const hi = [r1(h.dots.nose[0] - (pd.nose[0] - a.left) * s), r1(h.dots.nose[1] - (pd.nose[1] - a.top) * se), r1(a.w * s), r1(cut * se)];
  const lo = [r1(h.dots.chin[0] - (pd.chin[0] - a.left) * s), r1(h.dots.mouth[1] - BEARD_OVERLAP * sy), r1(a.w * s), r1((a.h - cut + BEARD_OVERLAP) * sy)];
  return { hi, lo, cut };
}

/** 점 맞추기가 재서 맞춘 자리와 너무 다르면(머리 폭의 25% 넘게) 점이 틀렸다고 보고 재서 맞춘 자리를 쓴다 */
function pickPlacement(byDots, measured, h) {
  if (!byDots) return measured;
  const far = Math.abs(byDots[0] - measured[0]) > h.headW * 0.25 || Math.abs(byDots[1] - measured[1]) > h.headW * 0.25 || Math.abs(byDots[2] / measured[2] - 1) > 0.3;
  return far ? measured : byDots;
}

/** 정면 머리들의 틀 평균 */
function templateOf(heads) {
  const avg = (k) => heads.reduce((p, h) => p + h[k], 0) / heads.length;
  return { top: avg("top"), cx: avg("cx"), headW: avg("headW") };
}

/** 정면(수염·갑옷): 머리 기준선과 파츠 기준점으로 놓을 사각형 */
/** 주둥이가 길어 수염이 코보다 한참 아래서 시작하는 종족 (눈 사이 거리 배율) */
const NOSE_DROP = { minotaur: 0.75 };

function placeFront(name, a, h, ref, race) {
  const brow = h.eyeY - h.eyeGap * 0.42, hairline = h.eyeY - h.eyeGap * 0.62, nose = h.eyeY + h.eyeGap * (0.42 + (NOSE_DROP[race] ?? 0));
  let p;
  switch (kindOf(name)) {
    case "hair": p = { s: (h.faceW * 1.02) / a.openW, px: a.massCx, py: a.openTop, hx: h.cx, hy: hairline }; break;
    case "hair_cap": p = { s: (h.headW * 1.08) / a.coreW, px: a.massCx, py: a.bangs, hx: h.cx, hy: hairline + h.eyeGap * 0.12 }; break;
    case "helmet_open": p = { s: (h.faceW * 0.98) / a.openW, px: a.massCx, py: a.openTop, hx: h.cx, hy: brow }; break;
    case "helmet": p = { s: (h.headW * 1.12) / a.coreW, px: a.massCx, py: a.top + a.h, hx: h.cx, hy: h.chin + h.eyeGap * 0.12 }; break;
    // 수염: 크기는 시트 그대로 (자기 폭으로 맞추면 가는 염소수염이 얼굴만큼 커진다), 콧수염 윗가장자리를 코 밑에
    case "beard": p = { s: h.headW / ref.headW, px: a.massCx, py: a.mustacheTop, hx: h.cx, hy: nose }; break;
    case "armor": p = { s: (h.shW * 1.1) / a.coreW, px: a.massCx, py: a.top, hx: h.shCx, hy: h.neckBase + (name === "armor_plate" ? -h.eyeGap * 0.12 : h.eyeGap * 0.02) }; break;
  }
  return [r1(p.hx - (p.px - a.left) * p.s), r1(p.hy - (p.py - a.top) * p.s), r1(a.w * p.s), r1(a.h * p.s)];
}

/** 측면(오른쪽을 봄): 정수리·뒤통수·이마 앞·눈·턱으로 맞춘다 */
function placeSide(name, a, h) {
  let p;
  switch (kindOf(name)) {
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

// 다른 종족은 tools/gen-race.mjs가 남긴 config.json을 읽는다
for (const race of readdirSync(SRC)) {
  const cfg = `${SRC}/${race}/config.json`;
  if (!existsSync(cfg)) continue;
  const c = JSON.parse(readFileSync(cfg, "utf8"));
  // 사람은 첫 세트가 위에 있고, config.json에는 두 번째 세트(extra)만 있다
  RACES[race] = RACES[race] ? { ...RACES[race], extra: c.extra } : c;
}

/** 'hair_short_2' 같은 두 번째 세트 파츠도 같은 방법으로 잰다 */
/** 'mark_...'는 얼굴 위 흉터·문신: 미간·코 점으로 맞춘다 */
const kindOf = (name) => (name.startsWith("mark_") ? "mark" : KIND[name.replace(/_\d+$/, "")]);

/** 머리 그림에서 y줄의 불투명 폭 (입 높이에서 재면 턱 폭) */
async function rowWidthAt(file, y) {
  const { data, info } = await sharp(file).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const yy = Math.max(0, Math.min(info.height - 1, Math.round(y)));
  let l = -1, r = -1;
  for (let x = 0; x < info.width; x++) if (data[(yy * info.width + x) * 4 + 3] > 128) { if (l < 0) l = x; r = x; }
  return l < 0 ? null : r - l;
}

// 종족 이름을 주면 그 종족만 다시 빌드하고, 나머지는 지금 data/portrait2.json 것을 그대로 둔다
const only = process.argv.slice(2);
const json = only.length && existsSync("data/portrait2.json") ? JSON.parse(readFileSync("data/portrait2.json", "utf8")) : { cell: CELL, races: {} };
for (const [race, cfg] of Object.entries(RACES)) {
  if (only.length && !only.includes(race)) continue;
  const entry = { heads: cfg.heads, front: { heads: [], parts: {} }, side: { heads: [], parts: {} } };
  for (const view of ["front", "side"]) {
    const headsSheet = `${SRC}/${race}/${view}_heads.png`, partsSheet = `${SRC}/${race}/${view}_parts.png`;
    if (!existsSync(headsSheet)) { console.log(`건너뜀: ${race} ${view} (머리 시트 없음)`); continue; }
    // 파츠가 하나도 없는 종족(리자드폴크·수인의 측면)은 머리만 있어도 된다
    const hasParts = existsSync(partsSheet);
    const dir = `${TMP}/${race}/${view}`; mkdirSync(dir, { recursive: true });
    const outDir = `${OUT}/${race}/${view}`; mkdirSync(outDir, { recursive: true });
    // 세트: 첫 세트 + (정면만) 같은 틀로 그린 추가 세트들. 머리는 이어 붙이고, 파츠는 모든 머리에 맞춰 놓는다
    const sets = [{ heads: headsSheet, parts: hasParts ? partsSheet : null, names: cfg[view], genders: null }];
    if (view === "front") {
      (cfg.extra ?? []).forEach((x, n) => {
        const h2 = `${SRC}/${race}/front_heads_${n + 2}.png`, p2 = `${SRC}/${race}/front_parts_${n + 2}.png`;
        if (existsSync(h2)) sets.push({ heads: h2, parts: existsSync(p2) ? p2 : null, names: x.front, genders: x.heads });
        else if (existsSync(p2)) sets.push({ heads: null, parts: p2, names: x.front, genders: null });
      });
    }
    const heads = [];
    const partJobs = [];
    for (let si = 0; si < sets.length; si++) {
      const set = sets[si];
      const tag = si ? `_s${si + 1}` : "";
      if (set.heads) {
        const headFiles = await sliceSheet(set.heads, `${dir}/head${tag}`);
        const headDots = view === "front" ? loadDots(set.heads) : null;
        for (let i = 0; i < 9; i++) {
          const h = view === "front" ? await headAnchors(headFiles[i]) : await sideHeadAnchors(headFiles[i]);
          h.dots = headDots?.[i];
          if (h.dots?.mouth) h.jawW = await rowWidthAt(headFiles[i], h.dots.mouth[1]);
          h.set = si;
          heads.push(h);
          await sharp(headFiles[i]).webp({ quality: 88, alphaQuality: 90 }).toFile(`${outDir}/head_${heads.length}.webp`);
          // 정면 머리는 턱 줄(갑옷 위로 다시 그릴 얼굴 부분)을, 측면은 목 끝(몸에 붙는 점)을 기록
          entry[view].heads.push(view === "front"
            ? { chin: r1(h.dots?.chin ? h.dots.chin[1] + 4 : h.chin + h.eyeGap * 0.08) }
            : { neckX: r1(h.neckX), neckY: r1(h.neckBottom), chin: r1(h.chin) });
        }
        if (si && set.genders) entry.heads = [...entry.heads, ...set.genders];
      }
      if (set.parts) partJobs.push({ files: await sliceSheet(set.parts, `${dir}/part${tag}`), names: set.names, dots: view === "front" ? loadDots(set.parts) : null, checked: view === "front" && dotsChecked(set.parts) });
    }
    for (const job of partJobs) {
      for (let i = 0; i < 9; i++) {
        const name = job.names[i];
        if (!name) continue;
        const k = kindOf(name) === "hair_cap" ? "hair" : kindOf(name);
        const a = view === "front" ? await partAnchors(job.files[i], k) : await sidePartAnchors(job.files[i], k);
        await sharp(a.buf).webp({ quality: 88, alphaQuality: 90 }).toFile(`${outDir}/${name}.webp`);
        const ref = templateOf(heads);
        // 파츠는 첫 세트 머리 틀 위에 그려 받았다: 그 머리들의 평균 눈 사이 거리가 크기 기준
        const firstEyes = heads.filter((h) => h.set === 0).map((h) => eyeDist(h.dots)).filter(Boolean);
        const refEye = firstEyes.length ? firstEyes.reduce((x, y) => x + y, 0) / firstEyes.length : null;
        // 수염: 나눌 수 있으면 위(이름 그대로)·아래('_lo') 두 장으로 쓴다
        if (view === "front" && kindOf(name) === "beard") {
          const jaws = heads.filter((h) => h.set === 0 && h.jawW).map((h) => h.jawW);
          const refJaw = jaws.length === heads.filter((h) => h.set === 0).length ? jaws.reduce((x, y) => x + y, 0) / jaws.length : null;
          const splits = heads.map((h) => splitBeard(a, h, job.dots?.[i], refEye, refJaw));
          if (splits.every(Boolean)) {
            const cut = splits[0].cut;
            await sharp(a.buf).extract({ left: 0, top: 0, width: a.w, height: cut }).webp({ quality: 88, alphaQuality: 90 }).toFile(`${outDir}/${name}.webp`);
            await sharp(a.buf).extract({ left: 0, top: cut - BEARD_OVERLAP, width: a.w, height: a.h - cut + BEARD_OVERLAP }).webp({ quality: 88, alphaQuality: 90 }).toFile(`${outDir}/${name}_lo.webp`);
            entry[view].parts[name] = splits.map((x) => x.hi);
            entry[view].parts[`${name}_lo`] = splits.map((x) => x.lo);
            continue;
          }
        }
        const old = (h) => (SHEET_KINDS.has(kindOf(name)) || kindOf(name) === "mark" ? placeFrontSheet(a, h, ref) : kindOf(name).startsWith("hair") ? placeHair(a, h, ref) : placeFront(name, a, h, ref, race));
        // 갑옷은 목 점이 자주 빗나가 늘 재서 맞춘다 (머리 쪽 턱 점은 얼굴 다시 그리기에 쓴다)
        entry[view].parts[name] = heads.map((h) => (view !== "front" ? placeSide(name, a, h)
          : kindOf(name) === "armor" ? old(h)
          : job.checked ? placeByDots(name, a, h, job.dots?.[i], refEye) ?? old(h)
          : pickPlacement(placeByDots(name, a, h, job.dots?.[i], refEye), old(h), h)));
      }
    }
    console.log(`${race} ${view}: 머리 ${heads.length}, 파츠 ${Object.keys(entry[view].parts).length}`);
  }
  json.races[race] = entry;
}
writeFileSync("data/portrait2.json", JSON.stringify(json));
console.log("data/portrait2.json 저장");
