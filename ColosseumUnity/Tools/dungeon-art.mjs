// 던전 카드 그림 36장(3x3 시트 4장)과 스폰서 그림(4x4 시트 1장: 상단 문장 12 + 계약 도장·종류 표식 4) + 계약서 카드 바탕 1장.
// 던전 id·순서는 Assets/Scripts/Core/Logic/DungeonLogic.cs의 Dungeons 목록과 같아야 한다.
// 사용 (저장소 루트에서):
//   node ColosseumUnity/Tools/dungeon-art.mjs manifest   → ColosseumUnity/Tools/dungeon-art.json
//   node tools/gen-images.mjs --manifest ColosseumUnity/Tools/dungeon-art.json --jobs 3
//   node ColosseumUnity/Tools/dungeon-art.mjs slice      → Assets/Resources/Art/dungeon/<id>.jpg, Art/sponsor/<id>.png
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import sharp from "sharp";

const ROOT = "ColosseumUnity";
const SHEETS = "art/source/dungeon_sheets";
const OUT_D = `${ROOT}/Assets/Resources/Art/dungeon`;
const OUT_S = `${ROOT}/Assets/Resources/Art/sponsor`;
const MANIFEST = `${ROOT}/Tools/dungeon-art.json`;

const SCENE_STYLE = "Art style (identical on every panel and every sheet): the SAME dark-fantasy graphic novel look as the attached reference — bold black ink outlines, large flat colour shapes with loose dry gouache brush strokes, blocky near-black shadows, warm torchlight from the left, greyed dirty colours (old bronze, ochre, brick red, ash grey, dark leather) with ONE accent colour per scene that defines its mood. Low detail, strong silhouettes, NOT photo-real, NOT 3D render, NOT anime, no glossy effects. Every scene is DARK overall (mostly mid-to-dark values) so light text could sit below it. No people in the foreground, at most small dark silhouettes of monsters far inside. No text, no letters, no numbers, no signs with writing, no UI, no frames inside panels.";
const SCENE_SHEET = "ONE landscape image (1536x1024), a 3x3 grid of 9 equal LANDSCAPE panels. The panels are separated by solid flat MAGENTA (#FF00FF) gutters about 24px wide, and there is also a 24px solid magenta border around the whole image. Gutters must be pure magenta, perfectly straight, no shadows or gradients. Each panel is a separate painting that fills its panel edge to edge: a wide establishing view of a dangerous dungeon location, seen from its entrance looking in. Each panel must look clearly different from its neighbours (different place, shapes and accent colour).";

const ICON_STYLE = "Art style (identical on every cell): the SAME dark-fantasy graphic novel look as the attached reference — bold uniform black ink outline, a few large flat colour shapes, at most 3 flat tones per area laid with loose dry gouache brush strokes, warm torchlight from the left. Colours greyed and dark (old bronze, dull ochre, brick red, ash grey, dark leather) — no bright saturated colours, no clean white, no gradients, no glossy shine, NOT vector art, NOT a sticker. Each emblem must read clearly at 48px.";
const ICON_SHEET = "ONE square image (1024x1024), a 4x4 grid of 16 equal square cells, flat solid MAGENTA background (#FF00FF) everywhere (no gradient, no frames, no grid lines, no shadows on the background). No text, no letters, no numbers. Each cell: one emblem, front view, centered, filling about 75% of the cell, with a clear magenta gap between neighbours. Keep each emblem fully inside its own cell.";

// 던전 36곳: id, 장면 설명 (DungeonLogic.Dungeons 순서)
export const DUNGEONS = [
  ["sewer", "a vaulted city sewer tunnel with murky green water, rat nests of rags and bones on the ledges, a rusty grate; accent sickly green"],
  ["mine", "an abandoned silver mine shaft with broken timber supports, a toppled mine cart, faint pale silver veins in the rock, ghostly blue lantern; accent pale silver"],
  ["catacomb", "underground catacombs with walls of stacked skulls, burial niches, old gladiator helmets on graves, candles; accent bone white and cold violet"],
  ["magma", "a volcanic cave with rivers of glowing lava, black basalt pillars, a horned skull totem; accent glowing orange"],
  ["abyss", "the gate of a sunken temple at the edge of a bottomless pit, colossal broken columns, faint purple light rising from below; accent deep violet"],
  ["bandit", "a hillside bandit cave with a palisade of sharpened stakes, campfire, stolen crates, crude tents; accent campfire orange"],
  ["cellar", "a huge underground wine cellar, rows of giant barrels, spilled red wine on the floor, broken stairs; accent wine red"],
  ["drowned", "a half-sunken sea fortress at low tide, barnacled walls, seaweed, tide pools in a flooded courtyard; accent sea green"],
  ["forge", "a cold dwarven mountain forge hall, a giant anvil, dead furnaces, hanging chains and racks of old weapons; accent dull copper"],
  ["oldarena", "a ruined ancient colosseum overgrown with weeds, blood-stained sand, broken statues of champions, a rusted portcullis; accent dried blood red"],
  ["thorn", "a forest sanctuary of giant twisted thorn vines forming arches, an elven altar stone, glowing moths; accent dark emerald"],
  ["frost", "a frozen royal treasure vault, ice-covered gold piles, frozen guards' armour in the ice walls, icicles; accent icy blue"],
  ["spire", "the shattered base of a collapsed wizard tower, floating rock fragments, broken glass jars, a strange green-blue glow; accent arcane teal"],
  ["kings", "a desert valley of royal tombs carved into cliffs, giant seated king statues, gold glinting in dark doorways; accent tomb gold"],
  ["smuggler", "a sea cave smugglers' den with a hidden jetty, small boats, barrels and nets, lanterns on ropes; accent lantern yellow"],
  ["kennel", "an abandoned fighting-dog kennel and pit, iron cages, chains, a sand pit with claw marks; accent rusty brown"],
  ["quarry", "a collapsed marble quarry with huge cut blocks, rope cranes, a dusty pit with a cave mouth; accent dusty marble white"],
  ["ruinvilla", "a burnt-down noble villa, charred columns, a scorched mosaic floor, a dry fountain, smoke; accent ember orange"],
  ["swamp", "a poisonous swamp shrine on stilts, twisted mangrove roots, hanging charms, glowing toxic water; accent toxic yellow-green"],
  ["barrow", "a grassy burial mound with a stone doorway, standing stones, a dark passage lined with old shields; accent mossy green"],
  ["prison", "an abandoned prison block, rows of rusted cell doors, hanging cages, manacles on the walls; accent cold iron grey"],
  ["wreck", "a ship graveyard of rotting wrecks on black rocks under a stormy sky, broken masts, a gaping hull entrance; accent storm blue"],
  ["bazaar", "an underground black market tunnel with tattered awnings, hanging cages and lamps, shadowy stalls; accent smoky purple"],
  ["hive", "a beast warren of tunnels dug in red earth, bones and fur nests, claw-scratched walls, a glowing den; accent earthy red"],
  ["garrison", "a fallen frontier fortress gatehouse, broken walls, burning banners, siege debris, war drums; accent banner crimson"],
  ["aqueduct", "the dark underground channels below a collapsed aqueduct, arches over rushing water, a huge iron sluice gate; accent dark teal"],
  ["labyrinth", "the entrance of a bull-headed labyrinth, high stone walls, bull horns carved over the gate, scattered bones; accent bronze"],
  ["monastery", "a silent mountain monastery cloister in snow, empty prayer halls, hanging bells, robed shadows; accent pale ash"],
  ["bonepit", "a giant pit filled with bones, crude bone totems, chains hanging over the edge, vultures; accent bone yellow"],
  ["storm", "a mountain peak shrine struck by lightning, broken bronze rods, swirling dark clouds, rain; accent electric pale blue"],
  ["obsidian", "a black obsidian fortress on a cliff, jagged glassy walls, red cracks of heat, iron spikes; accent smouldering red"],
  ["dragon", "a dragon's nest in a ruined mountain hall, enormous scales and claw marks, piles of gold and eggs; accent dragon green and gold"],
  ["necropolis", "a necropolis city of the dead, rows of mausoleums, ghostly green lights in windows, a black gate; accent spectral green"],
  ["titan", "the tomb of a fallen titan, a colossal stone hand and broken helmet half-buried, a doorway in its chest; accent weathered bronze"],
  ["crimson", "a hellish crimson arena deep underground, red sand, blood fountains, chains and burning braziers around; accent deep crimson"],
  ["void", "a meteor crater with a fallen glowing star stone, crystals, twisted cultist totems, starry night sky; accent cosmic blue-violet"],
];

// 스폰서 문장 12 (Patrons.SponsorKinds 순서) + 계약 도장과 종류 표식 4
const SPONSOR_ICONS = [
  ["salt", "a merchant guild emblem: a sack of salt with a balance scale"],
  ["wine", "a merchant emblem: an amphora with grape vines"],
  ["grain", "a granary emblem: a bundle of wheat sheaves over a sack"],
  ["bronze", "a bronze workshop emblem: a cast bronze helmet with a hammer"],
  ["ship", "a shipping guild emblem: a merchant galley with a full sail"],
  ["spice", "a spice house emblem: a spice jar with peppers and cinnamon sticks"],
  ["horse", "a horse breeder emblem: a rearing horse head in a horseshoe"],
  ["marble", "a marble quarry emblem: a cut marble block with a chisel"],
  ["bank", "a banker family emblem: a stack of gold coins with a key"],
  ["olive", "an olive oil house emblem: an oil jar with an olive branch"],
  ["slaver", "a slave trader guild emblem: broken iron shackles with a coin"],
  ["arms", "an armourer emblem: a crossed sword and spear over a round shield"],
  ["seal", "a red wax seal stamp pressed on a ribbon, with a laurel pattern (no letters)"],
  ["steady", "a heavy iron-bound strongbox full of coins"],
  ["balanced", "a bronze balance scale with coins in both pans"],
  ["bonus", "a golden laurel wreath around a victory palm"],
];

const mode = process.argv[2];
if (mode === "manifest") {
  const items = [];
  for (let k = 0; k < DUNGEONS.length / 9; k++) {
    const cells = DUNGEONS.slice(k * 9, k * 9 + 9);
    items.push({
      id: `dungeon_${k + 1}`, preset: "scene", out: `${SHEETS}/dungeons_${k + 1}.png`, styleRef: "art/source/style/dark_ref.png",
      prompt: "Panels left-to-right, top-to-bottom (3 per row):\n" + cells.map(([, d], i) => `${i + 1}. ${d}`).join("\n"),
      cells: cells.map(([id]) => id),
    });
  }
  items.push({
    id: "sponsor_icons", preset: "icons", out: `${SHEETS}/sponsor_icons.png`, styleRef: "art/source/style/flat_ref.png",
    prompt: "Every emblem is a round bronze-rimmed trade-guild medallion or badge (roman merchant style). Cells left-to-right, top-to-bottom (4 per row):\n" + SPONSOR_ICONS.map(([, d], i) => `${i + 1}. ${d}`).join("\n"),
    cells: SPONSOR_ICONS.map(([id]) => id),
  });
  items.push({
    id: "sponsor_card", preset: "card", out: `${SHEETS}/sponsor_card.png`, styleRef: "art/source/style/flat_ref.png",
    prompt: "ONE portrait image (1024x1536) on a flat solid MAGENTA (#FF00FF) background. In the middle, filling about 90% of the height: one tall roman sponsorship CONTRACT — a sheet of dark aged parchment (dull tan, stained, darker toward the edges) held by two dark wooden scroll rods at top and bottom with bronze end caps. The parchment centre is plain and EMPTY (no writing, no lines, no symbols), with only a faint thin bronze-ink border drawn near its edge. Front view, perfectly upright, symmetric. Keep the parchment fairly dark (mid tan, not white).",
    cells: ["card"],
  });
  writeFileSync(MANIFEST, JSON.stringify({
    presets: { scene: SCENE_SHEET, icons: ICON_SHEET, card: "" },
    style: "", styleRefPresets: ["scene", "icons", "card"],
    items: items.map((it) => ({ ...it, prompt: it.prompt + "\n\n" + (it.preset === "scene" ? SCENE_STYLE : ICON_STYLE) })),
  }, null, 2) + "\n");
  console.log(`시트 ${items.length}장 → ${MANIFEST}`);
} else if (mode === "slice") {
  const manifest = JSON.parse(readFileSync(MANIFEST, "utf8"));
  mkdirSync(OUT_D, { recursive: true });
  mkdirSync(OUT_S, { recursive: true });
  for (const it of manifest.items) {
    if (!existsSync(it.out)) { console.log(`없음: ${it.out}`); continue; }
    if (it.preset === "scene") await sliceScenes(it);
    else if (it.preset === "icons") await sliceIcons(it, 4, 160);
    else await sliceCard(it);
  }
} else console.log("manifest | slice");

function isMag(r, g, b) { return r > 170 && b > 170 && g < 110 && Math.abs(r - b) < 90; }

/** 마젠타 줄 사이 패널을 찾아 자른다. 못 찾으면 3등분 */
async function sliceScenes(it) {
  const { data, info } = await sharp(it.out).removeAlpha().raw().toBuffer({ resolveWithObject: true });
  const W = info.width, H = info.height, C = info.channels;
  const magAt = (x, y) => { const i = (y * W + x) * C; return isMag(data[i], data[i + 1], data[i + 2]); };
  const runs = (len, other, at) => {
    const gut = [];
    for (let a = 0; a < len; a++) {
      let m = 0;
      for (let b = 0; b < other; b += 3) if (at(a, b)) m++;
      gut.push(m / Math.ceil(other / 3) > 0.6);
    }
    const out = [];
    let s = -1;
    for (let a = 0; a <= len; a++) {
      const g = a === len || gut[a];
      if (!g && s < 0) s = a;
      if (g && s >= 0) { if (a - s > len / 8) out.push([s, a]); s = -1; }
    }
    return out;
  };
  let cols = runs(W, H, (x, y) => magAt(x, y));
  let rows = runs(H, W, (y, x) => magAt(x, y));
  if (cols.length !== 3) { console.log(`${it.id}: 세로 칸 ${cols.length}개 → 3등분`); cols = [0, 1, 2].map((i) => [Math.round((W * i) / 3), Math.round((W * (i + 1)) / 3)]); }
  if (rows.length !== 3) { console.log(`${it.id}: 가로 칸 ${rows.length}개 → 3등분`); rows = [0, 1, 2].map((i) => [Math.round((H * i) / 3), Math.round((H * (i + 1)) / 3)]); }
  let n = 0;
  for (let r = 0; r < 3; r++)
    for (let c = 0; c < 3; c++) {
      const id = it.cells[r * 3 + c];
      if (!id) continue;
      const inset = 6;
      const left = cols[c][0] + inset, top = rows[r][0] + inset;
      const w = cols[c][1] - cols[c][0] - inset * 2, h = rows[r][1] - rows[r][0] - inset * 2;
      await sharp(it.out).removeAlpha().extract({ left, top, width: w, height: h })
        .resize(480, 300, { fit: "cover", position: "centre" }).jpeg({ quality: 86 }).toFile(`${OUT_D}/${id}.jpg`);
      n++;
    }
  console.log(`${it.id}: 던전 그림 ${n}장`);
}

/** 마젠타 → 투명 (crest 슬라이서와 같은 식) */
function keyMagenta(data) {
  for (let i = 0; i < data.length; i += 4) {
    const r = data[i], g = data[i + 1], bl = data[i + 2];
    const m = Math.min(r, bl) - g;
    if (m > 90) data[i + 3] = 0;
    else if (m > 35) data[i + 3] = Math.round(255 * (1 - (m - 35) / 55));
    if (m > 12) {
      const k = Math.min(1, (m - 12) / 40);
      data[i] = Math.round(r - (r - g) * 0.7 * k);
      data[i + 2] = Math.round(bl - (bl - g) * 0.9 * k);
    }
  }
}

async function sliceIcons(it, N, SIZE) {
  const { data, info } = await sharp(it.out).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const W = info.width, H = info.height;
  keyMagenta(data);
  let n = 0;
  for (let i = 0; i < N * N; i++) {
    const id = it.cells[i];
    if (!id) continue;
    const cx0 = Math.round((W * (i % N)) / N), cy0 = Math.round((H * Math.floor(i / N)) / N);
    const cw = Math.round(W / N), ch = Math.round(H / N);
    let x0 = W, y0 = H, x1 = -1, y1 = -1;
    for (let y = cy0; y < Math.min(H, cy0 + ch); y++)
      for (let x = cx0; x < Math.min(W, cx0 + cw); x++)
        if (data[(y * W + x) * 4 + 3] >= 24) { x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y); }
    if (x1 < 0) { console.log(`빈 칸: ${it.id} ${i + 1}`); continue; }
    const w = x1 - x0 + 1, h = y1 - y0 + 1, cut = Buffer.alloc(w * h * 4);
    for (let y = 0; y < h; y++) data.copy(cut, y * w * 4, ((y0 + y) * W + x0) * 4, ((y0 + y) * W + x0 + w) * 4);
    const inner = Math.round(SIZE * 0.92), scale = inner / Math.max(w, h);
    const rw = Math.max(1, Math.round(w * scale)), rh = Math.max(1, Math.round(h * scale));
    const resized = await sharp(cut, { raw: { width: w, height: h, channels: 4 } }).resize(rw, rh, { kernel: "lanczos3" }).png().toBuffer();
    await sharp({ create: { width: SIZE, height: SIZE, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } } })
      .composite([{ input: resized, left: Math.round((SIZE - rw) / 2), top: Math.round((SIZE - rh) / 2) }])
      .png({ palette: true, quality: 92, compressionLevel: 9, effort: 8 }).toFile(`${OUT_S}/${id}.png`);
    n++;
  }
  console.log(`${it.id}: 문장 ${n}개`);
}

async function sliceCard(it) {
  const { data, info } = await sharp(it.out).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const W = info.width, H = info.height;
  keyMagenta(data);
  let x0 = W, y0 = H, x1 = -1, y1 = -1;
  for (let y = 0; y < H; y++)
    for (let x = 0; x < W; x++)
      if (data[(y * W + x) * 4 + 3] >= 24) { x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y); }
  const w = x1 - x0 + 1, h = y1 - y0 + 1, cut = Buffer.alloc(w * h * 4);
  for (let y = 0; y < h; y++) data.copy(cut, y * w * 4, ((y0 + y) * W + x0) * 4, ((y0 + y) * W + x0 + w) * 4);
  await sharp(cut, { raw: { width: w, height: h, channels: 4 } }).resize({ width: 360 }).png({ compressionLevel: 9 }).toFile(`${OUT_S}/card.png`);
  console.log(`계약서 바탕 ${w}x${h}`);
}
