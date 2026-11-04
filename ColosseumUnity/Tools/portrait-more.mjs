// 초상화 파츠 늘리기 (Unity판): 종족마다 같은 틀에 그린 새 머리 시트 9칸 + 머리카락·수염(일부 종족은 투구·갑옷) 시트 9칸을
// Codex로 만들고, 웹판 자르기 도구(tools/portrait-parts.mjs)를 그대로 돌려 Unity Resources로 옮긴다.
// 그림 규칙은 다크 그림체 때와 같다 (art/tmp/style_dark/manifest_rest.json): 머리는 사람 시트와 같은 틀, 파츠는 그 종족 머리 시트를 첨부해 "그 머리에 맞춰".
// 얼굴은 모두 20~35세 (늙은 얼굴은 만들지 않는다).
// 사용 (저장소 루트에서):
//   node ColosseumUnity/Tools/portrait-more.mjs manifest       → ColosseumUnity/Tools/portrait-more.json, 종족 config.json의 extra 칸
//   node tools/gen-images.mjs --manifest ColosseumUnity/Tools/portrait-more.json --only heads2. --jobs 4
//   node tools/gen-images.mjs --manifest ColosseumUnity/Tools/portrait-more.json --only parts2. --jobs 4
//   node tools/gen-images.mjs --manifest ColosseumUnity/Tools/portrait-more-dots.json --jobs 4   (새 머리 시트의 기준점 지도, 머리 시트가 생긴 뒤)
//   node ColosseumUnity/Tools/portrait-more.mjs anchors [종족...]   점 지도 → anchors/*.json (파츠 점은 그 종족 기존 파츠 점을 본뜬다)
//   node ColosseumUnity/Tools/portrait-more.mjs build [종족...]     자르기 → Assets/Resources/Art/portrait2, Resources/Data/portrait2.json
//   node ColosseumUnity/Tools/portrait-more.mjs audit <종족> <출력.png>   파츠 점검표 (줄 = 파츠, 칸 = 머리)
//   node ColosseumUnity/Tools/portrait-more.mjs sample [출력.png]        무작위 조합 견본 (종족마다 한 줄, 새 머리는 빨간 밑줄)
// 웹판 data/·public/은 건드리지 않는다: 자르기 도구는 경로만 바꾼 임시 사본으로 돌린다 (art/tmp/portrait_unity).
import { execFileSync } from "node:child_process";
import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import sharp from "sharp";

const SRC = "art/source/portrait2";
const UNITY = "ColosseumUnity/Assets/Resources";
const MANIFEST = "ColosseumUnity/Tools/portrait-more.json";
const DOTS_MANIFEST = "ColosseumUnity/Tools/portrait-more-dots.json";
const TMP = "art/tmp/portrait_unity";
const DOTS = `${TMP}/dots`;

const STYLE = "Art style: simplified dark-fantasy graphic novel with gouache brush texture, exactly like the attached sheet: bold black ink outlines, flat blocky black shadow shapes, 2-3 flat tones per material with loose brush strokes, desaturated bone/rust/bronze/grey palette, warm torchlight rim from the left, grim young adult faces (aged 20-35, NO wrinkles, NO grey hair, NOT old), NOT chibi, NOT cute. No text, no watermark.";
const SHEET = "ONE square image, a 3x3 grid of equal square cells, flat solid MAGENTA background (#FF00FF) in every cell (must stay magenta, not transparent, no gradient, no frames, no grid lines).";
const HEADS = (look, list, bald) => `Portrait PARTS for a modular character creator: ${look}. Nine NEW young adult gladiators on the IDENTICAL bust template as the attached sheet in every cell: strictly front view, perfectly symmetric, centered, exactly the same head top, eye height, chin height, head size, neck and bare shoulders as the attached sheet. Make the faces clearly DIFFERENT from the attached ones (face shape, jaw, cheekbones, eye shape and size, nose, brow, mouth, skin tone, expression): ${list}. ${bald}, no helmet, no clothing, so hair, beards and armor can be layered on top later.`;
const PARTS = (label, list) => `Overlay parts that fit the attached ${label} heads. Draw ONLY the item itself, exactly where it would sit on the attached heads (same cell, same head size and position), strictly front view, centered, symmetric: do NOT draw any head, face, ears, horns, tusks, snout, nose, lips, skin or neck; every area that would be skin stays flat magenta (face openings, around beards, above armor collars). Cells: ${list}.`;
const HEAD_DOTS = "Copy the attached sheet EXACTLY: same 3x3 grid, same flat magenta background, every drawing in exactly the same position, size and shape (do not move, resize, redraw or restyle anything). The ONLY change: add small solid round marker dots (about 1.5% of the cell width, perfectly flat color, no outline, no glow) on top of the drawings. Use exactly these colors and nothing else in those colors. In EVERY cell mark this character face: one pure RED (#FF0000) dot at the very top of the skull outline (on the outline itself); one pure YELLOW (#FFFF00) dot centered between the eyebrows; one pure BLUE (#0000FF) dot in the center of EACH pupil (two dots); one pure GREEN (#00FF00) dot on the tip of the nose or snout; one pure CYAN (#00FFFF) dot at the center of the mouth; one pure WHITE (#FFFFFF) dot at the bottom of the chin or jaw outline (on the outline, centered); one pure BLACK (#000000) dot at the base of the neck, centered.";
const EMPTY = "leave this cell completely empty magenta";

/**
 * 종족별 새 세트. set = 시트 번호(front_heads_<set>.png, front_parts_<set>.png), slot = config.json extra 자리 (set - 2).
 * heads: [성별, 설명] 9칸 (성비가 모자란 쪽을 더 넣었다). parts: [파츠 이름, 설명] 9칸 (null = 빈 칸). 이름이 종류_번호면 같은 종류의 변형으로 섞인다.
 */
const RACES = {
  human: {
    set: 4, label: "HUMAN", look: "HUMANS",
    bald: "All completely BALD and beardless (keep the eyebrows)",
    heads: [["f", "olive-skinned woman with a long narrow face, hooded eyes and a hooked nose"], ["m", "very dark-skinned man with a broad flat nose, wide cheekbones and heavy-lidded eyes"], ["f", "pale freckled woman with a round face, a small upturned nose and wide-set eyes"], ["m", "tan man with a long face, deep-set narrow eyes and a cleft chin"], ["f", "light-brown woman with a square jaw, thick straight brows and full lips"], ["m", "ruddy young man with a round boyish face, big ears and a crooked broken nose"], ["f", "brown-skinned woman with a heart-shaped face, a sharp pointed chin and almond eyes"], ["m", "pale man with gaunt hollow cheeks, a thin long nose and a scowl"], ["f", "dark-skinned woman with high cheekbones, a wide mouth and a calm hard stare"]],
    parts: [["hair_long_3", "wavy black hair to the shoulders"], ["hair_braids_3", "two thick dark-brown braids hanging in front of the shoulders"], ["hair_short_5", "short tight curly black hair"], ["hair_short_6", "slicked-back dark hair"], ["hair_long_4", "long straight blond hair with a center part"], ["beard_full_3", "a thick reddish-brown full beard with mustache"], ["beard_short_5", "a short black goatee with a thin mustache"], ["beard_short_6", "heavy dark stubble over the jaw and upper lip"], ["hair_braids_4", "a crown of braided auburn hair wrapped around the head"]],
  },
  dwarf: {
    set: 2, label: "DWARF", look: "stout fantasy DWARVES with wide faces, big round noses, heavy brows and thick necks",
    bald: "All completely BALD and BEARDLESS (keep the bushy eyebrows)",
    heads: [["f", "sunburnt dwarf woman with a broad square jaw and small deep-set eyes"], ["m", "pale dwarf man with a long face, a hooked nose and narrow eyes"], ["f", "dark-brown dwarf woman with round cheeks, wide-set eyes and a button nose"], ["m", "olive-skinned dwarf man with a heavy jaw, a flat broken nose and thick lips"], ["f", "ruddy freckled dwarf woman with high cheekbones, a sharp chin and a scowl"], ["m", "tan young dwarf man with puffy cheeks, a big bulbous nose and wide eyes"], ["f", "grey-brown dwarf woman with heavy-lidded eyes and a split eyebrow"], ["m", "deep reddish dwarf man with a wide flat face, tiny eyes and a crooked smirk"], ["f", "pale bluish dwarf woman with a long nose, a narrow face and a cold stare"]],
    parts: [["beard_full_2", "a long dark-brown beard tied into three braids with bronze rings, with mustache"], ["beard_full_3", "a bushy square blond beard with mustache"], ["beard_short_2", "a thick braided goatee with a wide mustache"], ["beard_short_3", "a huge walrus mustache with short stubble"], ["hair_short_2", "a shaved head with a short black mohawk"], ["hair_braids_2", "two thick copper braids hanging in front of the shoulders"], ["hair_long_2", "long black hair tied back loosely"], ["hair_short_3", "short curly ginger hair"], ["beard_full_4", "a wide black beard braided into one thick plait to the chest, with mustache"]],
  },
  orc: {
    set: 2, label: "ORC", look: "fantasy ORCS with green or grey-green skin, small lower tusks, pointed ears and a heavy brow",
    bald: "All completely BALD and beardless (keep the tusks and ears)",
    heads: [["f", "light-green orc woman with a narrow face, small tusks and slanted eyes"], ["m", "dark grey-green orc man with a massive square jaw and huge tusks"], ["f", "olive orc woman with a round face, a flat wide nose and a nose ring"], ["m", "yellow-green young orc man with a long face, sunken cheeks and one chipped tusk"], ["f", "deep-green orc woman with high cheekbones and a snarl"], ["m", "brown-green orc man with a broad flat face, tiny eyes and a heavy brow ridge"], ["f", "grey-green orc woman with a pointed chin and wide-set eyes"], ["m", "black-green orc man with a wide nose, thick lips and a torn ear"], ["f", "pale olive orc woman with a long jaw and a cold stare"]],
    parts: [["hair_short_2", "a tall black mohawk"], ["hair_long_2", "wild matted black hair to the shoulders"], ["hair_braids_2", "a topknot with two side braids"], ["hair_short_3", "a short buzz cut with shaved tribal lines"], ["beard_short_2", "a chin beard braided into a short spike (lips and tusks left open)"], ["beard_full_2", "a thick bushy black beard with mustache (lips and tusks left open)"], ["beard_short_3", "a drooping black mustache down to the jaw (lips and tusks left open)"], ["hair_long_3", "long straight black hair with bone beads"], ["hair_braids_3", "thick rope dreadlocks hanging down behind the shoulders"]],
  },
  elf: {
    set: 2, label: "ELF", look: "fantasy ELVES with long pointed ears, slender sharp faces and high cheekbones",
    bald: "All completely BALD and beardless (keep the eyebrows and the long ears)",
    heads: [["f", "dark-skinned elf woman with a narrow face and sleepy hooded eyes"], ["m", "pale elf man with a sharp angular face, hollow cheeks and thin lips"], ["f", "golden-tan elf woman with a soft round face and big almond eyes"], ["m", "olive-skinned elf man with a long straight nose and a square jaw"], ["f", "ash-grey-skinned elf woman with very high cheekbones and a cruel smirk"], ["m", "copper-skinned elf man with a broad face and wide-set narrow eyes"], ["f", "fair freckled elf woman with a pointed chin and an upturned nose"], ["m", "deep-brown elf man with heavy brows and a stern frown"], ["f", "pale bluish elf woman with a long face and a calm cold stare"]],
    // 긴 귀가 머리카락 사이로 나오게 한다
    parts: [["hair_long_3", "long straight raven-black hair tucked behind the long ears"], ["hair_braids_2", "a high bun of ash-blond hair with a few loose strands"], ["hair_short_2", "short spiky ash-blond hair"], ["hair_long_4", "shoulder-length wavy dark-brown hair with a few thin braids, ears poking through"], ["hair_short_3", "a short dark undercut with the top swept back"], ["hair_long_5", "long copper hair held by a leather headband, ears poking through"], ["hair_braids_3", "white hair in many thin braids tied back"], ["hair_short_4", "a cropped black pixie cut"], ["hair_long_6", "long straight black hair with blunt bangs above the eyebrows, ears poking through"]],
  },
  minotaur: {
    set: 2, label: "MINOTAUR", look: "MINOTAURS: bull-headed people with short fur, a broad muzzle with a nose ring hole, curved horns",
    bald: "No hair tufts and no beard (just the short fur, horns and ears), no clothing",
    heads: [["f", "tawny cow-woman with short lyre-shaped horns"], ["m", "jet-black bull with thick horns curving forward"], ["f", "white cow-woman with brown patches and small upturned horns"], ["m", "brindle bull with very wide straight horns"], ["f", "reddish-brown cow-woman with long curved horns and a gold nose ring"], ["m", "dun-yellow young bull with stubby horns and a very broad muzzle"], ["f", "blue-grey cow-woman with downward-curving horns"], ["m", "dark red bull with one broken horn"], ["f", "black-and-white piebald cow-woman with thin horns"]],
    parts: [["hair_short_3", "a short spiky reddish-brown forelock tuft between the horns"], ["hair_long_2", "a long wild dark mane down both sides of the neck"], ["hair_braids", "two thin beaded braids hanging from the forelock past the ears"], ["beard_short", "a short pointed chin tuft under the muzzle"], ["beard_short_2", "a braided chin beard with a bronze ring under the muzzle"], ["hair_long_3", "a long matted black mane falling over the shoulders"], ["helmet_crest_2", "an open iron brow band with a small crest between the horns (face open)"], ["armor_harness_2", "crossed leather straps with a bronze ring on the chest"], ["armor_plate_2", "a bronze scale cuirass with a fur collar"]],
    noMarks: true,
  },
  lizardfolk: {
    set: 2, label: "LIZARDFOLK", look: "LIZARDFOLK: reptile-headed people with a scaly snout, small head frills or spines, slit-pupil eyes",
    bald: "No hair, no clothing",
    heads: [["f", "emerald-green female with a short blunt snout and a red throat frill"], ["m", "rust-red male with a long narrow crocodile snout"], ["f", "sand-yellow female with brown spots and small horns behind the eyes"], ["m", "slate-grey male with a heavy broad snout and spiky cheek scales"], ["f", "turquoise female with a slender snout and a tall head fin"], ["m", "dark-brown male with a beaked turtle-like snout"], ["f", "pale pink-and-white female with a short rounded snout"], ["m", "olive-green male with a horned brow ridge"], ["f", "violet-blue female with yellow stripes along the head"]],
    parts: [["helmet_crest_3", "a feathered bone headband around the brow (face and snout open)"], ["helmet_crest_4", "a beaded leather cap with hanging teeth (face and snout open)"], ["armor_plate_2", "an iron lamellar cuirass with pauldrons"], ["armor_harness_4", "a leather crossbelt with a bronze disc on the chest"], ["armor_plate_3", "a turtle-shell breastplate tied with cords"], null, null, null, null],
    noMarks: true,
  },
  beastkin: {
    set: 2, label: "BEASTKIN", look: "BEASTKIN: animal-headed people with fur, a short muzzle and big animal ears",
    bald: "No clothing",
    heads: [["f", "red fox woman with big pointed ears"], ["m", "spotted hyena man with round ears"], ["f", "white goat woman with small curved horns and floppy ears"], ["m", "golden lion man with a short dark mane"], ["f", "grey lynx woman with tufted ears"], ["m", "black boar man with small tusks and bristly ears"], ["f", "brown otter woman with small round ears"], ["m", "grey badger man with a black-and-white striped face"], ["f", "tan jackal woman with tall pointed ears"]],
    parts: [["helmet_crest_4", "a bronze circlet with a red stone on the brow (face and ears open)"], ["helmet_crest_5", "a cloth bandana tied on the brow with the ears free (face open)"], ["armor_harness_3", "a fur cloak with a bone clasp on one shoulder"], ["armor_plate_3", "a studded leather cuirass with shoulder guards"], ["armor_harness_4", "a leather baldric across the chest with a bronze disc"], null, null, null, null],
    noMarks: true,
  },
};

const numbered = (arr) => arr.map((x, i) => `${i + 1}) ${x ?? EMPTY}`).join("; ");
const sheet = (race, kind) => `${SRC}/${race}/front_${kind}_${RACES[race].set}.png`;

function manifest() {
  const items = [], dots = [];
  for (const [race, R] of Object.entries(RACES)) {
    const ref = `${SRC}/${race}/front_heads.png`;
    items.push({ id: `heads2.${race}`, preset: "sheet", out: sheet(race, "heads"), styleRef: ref, prompt: HEADS(R.look, numbered(R.heads.map((h) => h[1])), R.bald) });
    items.push({ id: `parts2.${race}`, preset: "sheet", out: sheet(race, "parts"), styleRef: ref, prompt: PARTS(R.label, numbered(R.parts.map((p) => p?.[1] ?? null))) });
    dots.push({ id: `dots2.${race}`, preset: "sheet", out: `${DOTS}/${race}_heads_${R.set}.png`, styleRef: sheet(race, "heads"), prompt: HEAD_DOTS });
    // portrait-parts.mjs가 읽는 세트 정보 (extra[set - 2]). 흉터·문신 칸(extra 마지막, front_parts_3)은 그대로 둔다
    const cfgFile = `${SRC}/${race}/config.json`;
    const cfg = JSON.parse(readFileSync(cfgFile, "utf8"));
    cfg.extra[R.set - 2] = { heads: R.heads.map((h) => h[0]), front: R.parts.map((p) => p?.[0] ?? null) };
    writeFileSync(cfgFile, JSON.stringify(cfg, null, 1));
  }
  writeFileSync(MANIFEST, JSON.stringify({ presets: { sheet: SHEET }, style: STYLE, styleRefPresets: ["sheet"], items }, null, 1));
  // 점 지도는 그림을 그대로 베껴야 하므로 화풍 문구 대신 짧은 규칙만 쓴다 (tools/gen-anchor-maps.mjs와 같게) → 목록을 따로 둔다
  writeFileSync(DOTS_MANIFEST, JSON.stringify({ presets: { sheet: "ONE square image the same size as the attached image, flat solid MAGENTA background (#FF00FF) where the original is magenta." }, style: "Keep the original painted style exactly. No text, no watermark.", styleRefPresets: ["sheet"], items: dots }, null, 1));
  console.log(`saved ${MANIFEST} (${items.length}), ${DOTS_MANIFEST} (${dots.length})`);
}

/** 점 지도 → 머리 기준점. 머리카락·투구 점은 그 종족 기존 파츠 시트에서 같은 종류 칸의 점을 빌리고(같은 틀 위에 그렸다), 수염 점은 같은 칸 첫 세트 머리의 코·입·턱 점 */
function anchors(races) {
  for (const race of races) {
    const R = RACES[race];
    const map = `${DOTS}/${race}_heads_${R.set}.png`;
    if (!existsSync(map)) { console.log(`점 지도 없음: ${race}`); continue; }
    const packed = execFileSync("node", ["tools/read-dots.mjs", map, "heads"], { encoding: "utf8" }).trim().split("\n").pop();
    execFileSync("node", ["tools/save-anchors.mjs", `${race}/front_heads_${R.set}`, packed], { stdio: "inherit" });
    const old = JSON.parse(readFileSync(`${SRC}/${race}/anchors/front_parts.json`, "utf8"));
    const cfg = JSON.parse(readFileSync(`${SRC}/${race}/config.json`, "utf8"));
    const baseNames = cfg.front ?? ["hair_short", "hair_long", "hair_braids", "beard_full", "beard_short", "helmet_crest", "helmet_visor", "armor_harness", "armor_plate"];
    const kind = (n) => n?.replace(/_\d+$/, "").replace(/_(short|long|braids|full|crest|visor|harness|plate)$/, "");
    // 새 파츠 시트는 첫 세트 머리 시트(front_heads.png)의 같은 칸 머리에 맞춰 그렸다
    const baseHeads = JSON.parse(readFileSync(`${SRC}/${race}/anchors/front_heads.json`, "utf8")).cells;
    const cells = R.parts.map((p, cell) => {
      if (!p || kind(p[0]) === "armor") return {};
      // 수염: 그 칸 머리의 코·입·턱 점을 그대로 쓴다 → 자르기 도구가 입 줄에서 나눠 코·입~턱에 맞춘다.
      // (점이 없으면 가운데 줄 첫 픽셀을 코 밑에 붙이는데, 입·엄니 자리를 비운 수염은 그게 턱수염이라 코까지 올라갔다)
      if (kind(p[0]) === "beard") {
        const h = baseHeads[cell];
        return h?.nose && h?.mouth && h?.chin ? { nose: h.nose, mouth: h.mouth, chin: h.chin } : {};
      }
      // 같은 종류(hair_short ↔ hair_short_3)가 먼저, 없으면 같은 갈래(머리카락·투구)
      const has = (i) => !!old.cells[i]?.crown;
      let i = baseNames.findIndex((n, j) => n && n.replace(/_\d+$/, "") === p[0].replace(/_\d+$/, "") && has(j));
      if (i < 0) i = baseNames.findIndex((n, j) => n && kind(n) === kind(p[0]) && has(j));
      return i >= 0 ? { ...old.cells[i] } : {};
    });
    // checked: 점을 그대로 믿는다. 어깨까지 늘어진 땋은 머리처럼 정수리에서 떨어진 파츠는 재서 맞추면 정수리로 끌려 올라간다
    writeFileSync(`${SRC}/${race}/anchors/front_parts_${R.set}.json`, JSON.stringify({ checked: true, keys: old.keys, cells }, null, 1));
    console.log("parts anchors", race, cells.map((c) => Object.keys(c).length).join(","));
  }
}

/** tools/의 도구를 출력 경로만 바꿔 임시 사본으로 돌린다 (웹판 data/·public/을 건드리지 않게) */
function runPatched(tool, args) {
  mkdirSync(TMP, { recursive: true });
  const json = `${TMP}/portrait2.json`;
  if (!existsSync(json)) copyFileSync(`${UNITY}/Data/portrait2.json`, json);
  const src = readFileSync(`tools/${tool}`, "utf8")
    .replaceAll('"./lib/anchors.cjs"', '"../../tools/lib/anchors.cjs"')
    .replaceAll('"public/assets/portrait2"', `"${TMP}/out"`)
    .replaceAll("`public/assets/portrait2/", `\`${TMP}/out/`)
    .replaceAll('"data/portrait2.json"', `"${json}"`);
  const tmpFile = `ColosseumUnity/Tools/.${tool}.tmp.mjs`;
  writeFileSync(tmpFile, src);
  try { execFileSync("node", [tmpFile, ...args], { stdio: "inherit" }); } finally { rmSync(tmpFile); }
}

async function samePixels(a, b) {
  if (!existsSync(b)) return false;
  const [x, y] = await Promise.all([a, b].map((f) => sharp(f).ensureAlpha().raw().toBuffer({ resolveWithObject: true })));
  return x.info.width === y.info.width && x.info.height === y.info.height && x.data.equals(y.data);
}

const BEARD_OVERLAP = 6;
const isBeard = (n) => /^beard_/.test(n ?? "");

/** 잘라 둔 칸 그림에서 칸 가장자리에 닿은 작은 덩어리(옆 칸에서 묻어 온 부스러기)만 지운다.
 * 떨어진 조각(콧수염·수염 자국)은 남기고, 칸 끝까지 내려오는 긴 수염처럼 큰 덩어리는 가장자리에 닿아도 남긴다 */
async function cleanCell(file) {
  const { data, info } = await sharp(file).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const W = info.width, H = info.height, lab = new Int32Array(W * H).fill(-1);
  const edge = [];
  let total = 0;
  for (let i = 0; i < W * H; i++) if (data[i * 4 + 3] > 0) total++;
  for (let i = 0, id = 0; i < W * H; i++) {
    if (lab[i] >= 0 || data[i * 4 + 3] === 0) continue;
    const st = [i], px = []; lab[i] = id; let touches = false;
    while (st.length) {
      const p = st.pop(); px.push(p);
      const x = p % W, y = (p - x) / W;
      if (x < 3 || y < 3 || x >= W - 3 || y >= H - 3) touches = true;
      for (const q of [x > 0 ? p - 1 : -1, x < W - 1 ? p + 1 : -1, y > 0 ? p - W : -1, y < H - 1 ? p + W : -1]) if (q >= 0 && lab[q] < 0 && data[q * 4 + 3] > 0) { lab[q] = id; st.push(q); }
    }
    if (touches && px.length < total * 0.1) edge.push(...px);
    id++;
  }
  for (const p of edge) data[p * 4 + 3] = 0;
  let l = W, t = H, r = -1, b = -1;
  for (let i = 0; i < W * H; i++) if (data[i * 4 + 3] > 0) { const x = i % W, y = (i - x) / W; l = Math.min(l, x); t = Math.min(t, y); r = Math.max(r, x); b = Math.max(b, y); }
  const img = sharp(data, { raw: info });
  return { left: l, top: t, w: r - l + 1, h: b - t + 1, buf: await img.extract({ left: l, top: t, width: r - l + 1, height: b - t + 1 }).png().toBuffer() };
}

/** 머리 그림에서 y줄의 불투명 폭 (입 높이에서 재면 턱 폭) */
async function rowWidth(file, y) {
  const { data, info } = await sharp(file).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const yy = Math.max(0, Math.min(info.height - 1, Math.round(y)));
  let l = -1, r = -1;
  for (let x = 0; x < info.width; x++) if (data[(yy * info.width + x) * 4 + 3] > 128) { if (l < 0) l = x; r = x; }
  return l < 0 ? null : r - l;
}

/**
 * 새 세트 수염을 다시 놓는다. 자르기 도구는 큰 덩어리에서 떨어진 조각을 부스러기로 지워
 * 염소수염 위 콧수염·수염 자국이 사라지고, 남은 덩어리가 입 줄에서 안 나뉘어 엉뚱한 데 붙었다.
 * 새 파츠는 첫 세트 머리 시트의 같은 칸 머리에 맞춰 그렸으므로: 칸 그림을 그대로(가장자리 부스러기만 지우고) 쓰고,
 * 그 머리의 입 줄에서 위(콧수염)는 코 점에, 아래(턱수염)는 입~턱 점 사이에 머리마다 맞춘다 (자르기 도구의 splitBeard와 같은 셈).
 * 입 위에 아무것도 없으면 통째로 입~턱에, 입 아래가 없으면 통째로 코에 맞춘다.
 */
async function placeNewBeards(race, entry) {
  const R = RACES[race];
  const dir = `art/tmp/portrait2/${race}/front`, out = `${TMP}/out/${race}/front`;
  const names = R.parts.map((p) => p?.[0]);
  if (!names.some(isBeard)) return;
  // 머리 순서는 자르기 도구와 같다: 첫 세트(head_N), 그다음 세트 번호 순(head_sK_N)
  const files = readdirSync(dir).filter((f) => /^head_(s\d+_)?\d+\.png$/.test(f));
  const setOf = (f) => Number(f.match(/^head_s(\d+)_/)?.[1] ?? 1), cellOf = (f) => Number(f.match(/(\d+)\.png$/)[1]);
  files.sort((a, b) => setOf(a) - setOf(b) || cellOf(a) - cellOf(b));
  const dotsOf = {};
  const heads = [];
  for (const f of files) {
    const k = setOf(f);
    dotsOf[k] ??= JSON.parse(readFileSync(`${SRC}/${race}/anchors/front_heads${k > 1 ? "_" + k : ""}.json`, "utf8")).cells;
    const d = dotsOf[k][cellOf(f) - 1];
    heads.push({ d, jaw: d?.mouth ? await rowWidth(`${dir}/${f}`, d.mouth[1]) : null });
  }
  if (heads.length !== entry.front.heads.length) { console.log(`${race}: 머리 수가 달라 새 수염을 다시 놓지 않음`); return; }
  const eye = (d) => (d?.eyeL && d?.eyeR ? d.eyeR[0] - d.eyeL[0] : null);
  const r1 = (v) => Math.round(v * 10) / 10;
  for (const [cell, name] of names.entries()) {
    if (!isBeard(name)) continue;
    const src = dotsOf[1][cell];
    if (!src?.nose || !src?.mouth || !src?.chin || !eye(src)) continue;
    const a = await cleanCell(`${dir}/part_s${R.set}_${cell + 1}.png`);
    const srcJaw = await rowWidth(`${dir}/head_${cell + 1}.png`, src.mouth[1]);
    const cut = Math.round(src.mouth[1] - a.top);
    const mode = cut <= BEARD_OVERLAP ? "lo" : cut >= a.h - BEARD_OVERLAP ? "hi" : "split";
    const his = [], los = [];
    heads.forEach((h, i) => {
      const d = h.d;
      if (!d?.nose || !d?.mouth || !d?.chin || !eye(d)) { his.push(entry.front.parts[name]?.[i] ?? [0, 0, 1, 1]); los.push(entry.front.parts[`${name}_lo`]?.[i] ?? [0, 0, 1, 1]); return; }
      const se = eye(d) / eye(src), s = h.jaw && srcJaw ? h.jaw / srcJaw : se;
      const sy = Math.min(1.5 * se, Math.max(0.7 * se, (d.chin[1] - d.mouth[1]) / (src.chin[1] - src.mouth[1])));
      const x0 = r1(d.nose[0] - (src.nose[0] - a.left) * s), w = r1(a.w * s);
      if (mode === "hi") his.push([x0, r1(d.nose[1] - (src.nose[1] - a.top) * se), w, r1(a.h * se)]);
      else if (mode === "lo") his.push([r1(d.chin[0] - (src.chin[0] - a.left) * s), r1(d.mouth[1] + (a.top - src.mouth[1]) * sy), w, r1(a.h * sy)]);
      else {
        his.push([x0, r1(d.nose[1] - (src.nose[1] - a.top) * se), w, r1(cut * se)]);
        los.push([r1(d.chin[0] - (src.chin[0] - a.left) * s), r1(d.mouth[1] - BEARD_OVERLAP * sy), w, r1((a.h - cut + BEARD_OVERLAP) * sy)]);
      }
    });
    const webp = (img) => img.webp({ quality: 88, alphaQuality: 90 });
    if (mode === "split") {
      await webp(sharp(a.buf).extract({ left: 0, top: 0, width: a.w, height: cut })).toFile(`${out}/${name}.webp`);
      await webp(sharp(a.buf).extract({ left: 0, top: cut - BEARD_OVERLAP, width: a.w, height: a.h - cut + BEARD_OVERLAP })).toFile(`${out}/${name}_lo.webp`);
      entry.front.parts[`${name}_lo`] = los;
    } else {
      await webp(sharp(a.buf)).toFile(`${out}/${name}.webp`);
      delete entry.front.parts[`${name}_lo`];
      for (const f of [`${out}/${name}_lo.webp`, `${UNITY}/Art/portrait2/${race}/front/${name}_lo.png`, `${UNITY}/Art/portrait2/${race}/front/${name}_lo.png.meta`]) if (existsSync(f)) rmSync(f);
    }
    entry.front.parts[name] = his;
    console.log(`${race} ${name}: 새 수염 다시 놓음 (${mode === "split" ? "입 줄에서 나눔" : mode === "lo" ? "입 아래만" : "입 위만"})`);
  }
}

async function build(races) {
  mkdirSync(TMP, { recursive: true });
  copyFileSync(`${UNITY}/Data/portrait2.json`, `${TMP}/portrait2.json`);
  runPatched("portrait-parts.mjs", races);
  const built = JSON.parse(readFileSync(`${TMP}/portrait2.json`, "utf8"));
  const unity = JSON.parse(readFileSync(`${UNITY}/Data/portrait2.json`, "utf8"));
  for (const race of races) {
    await placeNewBeards(race, built.races[race]);
    unity.races[race] = built.races[race];
    let wrote = 0;
    for (const view of ["front", "side"]) {
      const dir = `${TMP}/out/${race}/${view}`;
      if (!existsSync(dir)) continue;
      for (const f of readdirSync(dir).filter((f) => f.endsWith(".webp"))) {
        const out = `${UNITY}/Art/portrait2/${race}/${view}/${f.replace(".webp", ".png")}`;
        const png = await sharp(`${dir}/${f}`).png().toBuffer();
        const tmpPng = `${TMP}/cmp.png`;
        writeFileSync(tmpPng, png);
        if (await samePixels(tmpPng, out)) continue;
        writeFileSync(out, png);
        wrote++;
      }
    }
    // 시트 원본도 Unity 폴더에 같이 둔다 (기존과 같게)
    for (const kind of ["heads", "parts"]) if (existsSync(sheet(race, kind))) copyFileSync(sheet(race, kind), `${UNITY}/Art/portrait2/${race}/front_${kind}_${RACES[race].set}.png`);
    console.log(`${race}: PNG ${wrote}장 새로 씀`);
  }
  writeFileSync(`${UNITY}/Data/portrait2.json`, JSON.stringify(unity));
  console.log(`${UNITY}/Data/portrait2.json 저장`);
}

/**
 * 무작위 조합 견본: 종족마다 한 줄, Unity PNG·JSON으로 Portrait2.Layers처럼 겹친다 (머리 → 갑옷 → 턱 위 얼굴 다시 → 수염 → 머리카락).
 * 새 세트 머리는 빨간 밑줄을 긋는다. 시드 고정이라 같은 그림이 나온다
 */
async function sample(out, n = 12) {
  const D = JSON.parse(readFileSync(`${UNITY}/Data/portrait2.json`, "utf8"));
  const S = 128, k = S / D.cell;
  let seed = 7;
  const rnd = () => ((seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648);
  const pick = (a) => a[Math.floor(rnd() * a.length)];
  const tiles = [];
  const raceList = Object.keys(D.races);
  for (const [row, race] of raceList.entries()) {
    const R = D.races[race], v = R.front, dir = `${UNITY}/Art/portrait2/${race}/front`;
    const names = Object.keys(v.parts).filter((p) => !p.endsWith("_lo"));
    const of = (kind) => names.filter((p) => p === kind || p.startsWith(kind + "_") && /^\d+$/.test(p.slice(kind.length + 1)));
    // 이 도구 전 머리 수: 사람 18 (두 세트), 나머지 9
    const before = race === "human" ? 18 : 9, firstNew = R.heads.length > before ? before : R.heads.length;
    for (let c = 0; c < n; c++) {
      const usable = R.heads.map((g, i) => [g, i]).filter(([g]) => g !== "-");
      const [g, head] = c < n / 2 ? pick(usable.filter(([, i]) => i >= firstNew).length ? usable.filter(([, i]) => i >= firstNew) : usable) : pick(usable);
      const hair = pick([...of("hair_short"), ...of("hair_long"), ...of("hair_braids"), null]);
      const beard = g === "m" && rnd() < 0.6 ? pick([...of("beard_full"), ...of("beard_short"), null]) : null;
      const armor = pick([...of("armor_harness"), ...of("armor_plate"), null]);
      const headPng = await sharp(`${dir}/head_${head + 1}.png`).resize(S, S).png().toBuffer();
      const comps = [{ input: headPng, left: 0, top: 0 }];
      const place = async (name) => {
        const r = name && v.parts[name]?.[head];
        if (!r) return;
        const pw = Math.max(1, Math.round(r[2] * k)), ph = Math.max(1, Math.round(r[3] * k)), L = Math.round(r[0] * k), T = Math.round(r[1] * k);
        const img = await sharp(`${dir}/${name}.png`).resize(pw, ph, { fit: "fill" }).png().toBuffer();
        const ext = { left: Math.max(0, -L), top: Math.max(0, -T) };
        const w = Math.min(pw - ext.left, S - Math.max(0, L)), h = Math.min(ph - ext.top, S - Math.max(0, T));
        if (w > 0 && h > 0) comps.push({ input: await sharp(img).extract({ ...ext, width: w, height: h }).png().toBuffer(), left: Math.max(0, L), top: Math.max(0, T) });
      };
      await place(armor);
      if (armor) comps.push({ input: await sharp(headPng).extract({ left: 0, top: 0, width: S, height: Math.round(v.heads[head].chin * k) }).png().toBuffer(), left: 0, top: 0 });
      await place(beard);
      if (beard) await place(beard + "_lo");
      await place(hair);
      if (head >= firstNew) comps.push({ input: await sharp({ create: { width: S, height: 4, channels: 4, background: "#d02020" } }).png().toBuffer(), left: 0, top: S - 4 });
      tiles.push({ input: await sharp({ create: { width: S, height: S, channels: 4, background: "#2a1d22" } }).composite(comps).png().toBuffer(), left: c * (S + 4), top: row * (S + 4) });
    }
  }
  await sharp({ create: { width: n * (S + 4), height: raceList.length * (S + 4), channels: 4, background: "#000" } }).composite(tiles).png().toFile(out);
  console.log("saved", out);
}

const [cmd, ...rest] = process.argv.slice(2);
const races = rest.length ? rest : Object.keys(RACES);
if (cmd === "manifest") manifest();
else if (cmd === "anchors") anchors(races);
else if (cmd === "build") await build(races);
else if (cmd === "audit") {
  copyFileSync(`${UNITY}/Data/portrait2.json`, `${TMP}/portrait2.json`);
  runPatched("render-part-audit.mjs", rest);
} else if (cmd === "sample") await sample(rest[0] ?? `${TMP}/sample.png`);
else console.log("사용: manifest | anchors [종족...] | build [종족...] | audit <종족> <출력.png> | sample [출력.png]");
