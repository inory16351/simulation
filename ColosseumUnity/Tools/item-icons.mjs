// 게임 아이콘 그림 (Unity판): 초상화와 같은 플랫 붓그림체로 3x3 마젠타 시트를 Codex로 그리고, 칸별로 잘라 투명 PNG로 둔다.
// 묶음: items(장비, 이름 = 기본 id 또는 고유 id), menu(왼쪽 메뉴), crest(나라 문장), fx(전투 이펙트).
// 사용 (저장소 루트에서):
//   node ColosseumUnity/Tools/item-icons.mjs ref           화풍 기준 그림 → art/source/style/flat_ref.png (dark_ref + 초상화 파츠)
//   node ColosseumUnity/Tools/item-icons.mjs manifest      시트 목록 → ColosseumUnity/Tools/item-icons.json
//   node tools/gen-images.mjs --manifest ColosseumUnity/Tools/item-icons.json --jobs 3     Codex로 시트 생성
//   node ColosseumUnity/Tools/item-icons.mjs slice [묶음]  시트를 잘라 Assets/Resources/Art/<묶음 폴더>/<id>.png
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import sharp from "sharp";

const ROOT = "ColosseumUnity";
const DATA = `${ROOT}/Assets/Resources/Data`;
const SHEETS = "art/source/icon_sheets";
const ART = `${ROOT}/Assets/Resources/Art`;
const STYLE_REF = "art/source/style/flat_ref.png";
const MANIFEST = `${ROOT}/Tools/item-icons.json`;

const items = JSON.parse(readFileSync(`${DATA}/items.json`, "utf8"));
const uniques = JSON.parse(readFileSync(`${DATA}/uniques.json`, "utf8")).uniques;

// 기본 장비 그림 설명 (영어 프롬프트)
const BASE = {
  gladius: "a short Roman gladius sword with a ribbed bone grip and round pommel",
  sica: "a curved Thracian sica blade, inward-curving sword",
  longsword: "a straight double-edged longsword with a simple cross guard",
  greatsword: "a huge two-handed greatsword with a long leather grip",
  greataxe: "a massive two-handed bearded greataxe on a long haft",
  battleaxe: "a one-handed bronze battle axe",
  mace: "a flanged iron mace",
  warhammer: "a heavy two-handed war hammer with a square iron head",
  hasta: "a Roman hasta spear with a leaf-shaped iron head",
  hunting_spear: "a boar hunting spear with crossbar lugs below the blade",
  trident: "a three-pronged fishing trident",
  net: "a rolled gladiator's fishing net with lead weights",
  pugio: "a short Roman pugio dagger with a waisted blade",
  javelin: "two light throwing javelins crossed",
  cestus: "leather boxing hand-wraps (cestus)",
  knuckles: "spiked steel knuckle dusters",
  rune_charm: "a carved runestone charm on a leather cord",
  staff: "a gnarled sorcerer's staff with a glowing stone",
  loincloth: "a simple cloth loincloth with a wide belt",
  leather: "a hardened leather cuirass",
  manica: "a padded linen torso wrap with a single segmented arm guard",
  chainmail: "a chainmail shirt (lorica hamata)",
  scale: "a bronze scale armor shirt (lorica squamata)",
  muscle_cuirass: "a bronze muscle cuirass sculpted like a torso",
  plate: "heavy steel plate armor breastplate with pauldrons",
  open_helm: "an open-faced iron helmet",
  brimmed_helm: "a broad-brimmed gladiator helmet",
  sealed_helm: "a fully enclosed gladiator helmet with a grilled face visor",
  crested_helm: "a crested gladiator helmet with a tall plume",
  parmula: "a small square buckler shield",
  aspis: "a round bronze hoplite shield",
  scutum: "a large curved rectangular Roman scutum shield",
  leather_bracer: "a pair of laced leather forearm bracers",
  bronze_vambrace: "a pair of bronze forearm vambraces",
  chain_sleeve: "a chainmail arm sleeve",
  plate_vambrace: "a segmented steel arm guard (manica) from shoulder to wrist",
  leather_greaves: "a pair of wrapped leather leg greaves",
  ocrea: "a pair of bronze shin greaves (ocrea)",
  plate_greaves: "a pair of heavy steel leg plates with knee cops",
};

// 고유 장비 그림 설명: 바탕 장비 + 이름에 맞는 특징
const UNIQUE = {
  red_tooth: "with a blood-red serrated edge like a fang",
  senate_edict: "gilded, with a laurel wreath engraved on the blade and a purple tassel",
  moon_hook: "pale silver crescent blade like a waning moon",
  thracian_vow: "blackened blade inlaid with silver coins on the hilt",
  grey_warden: "ash-grey steel with a worn tower emblem on the guard",
  oathbreaker: "a cracked blade bound with a broken chain around the hilt",
  widowmaker: "dark blade with black ribbons tied to the hilt",
  giant_spine: "a blade made of fused bone vertebrae",
  woodsman_grief: "a weathered axe with a carved weeping face on the haft",
  red_harvest: "crimson-stained blade, dripping, with a sickle-shaped beard",
  raven_beak: "a hooked black axe shaped like a raven's beak with feathers",
  skull_bell: "the mace head shaped like a skull with a small bell",
  quiet_mountain: "a grey stone hammer head like a mountain peak",
  anvil_of_kemer: "a temple anvil-shaped hammer head with gold hieroglyph bands",
  first_light: "a spear head polished bright like dawn, gold-tinted",
  legion_standard: "a spear with a tattered red legion banner and eagle finial",
  boar_bane: "boar tusks lashed below the blade",
  sea_kings_fork: "a sea-green bronze trident with coral growths",
  drowned_crown: "a barnacled trident with a sunken crown fused to the shaft, dripping water",
  spider_silk: "a pale silvery net like a spider web",
  last_word: "a slim black dagger with a whispering mouth carved on the pommel",
  brutus_kiss: "a senatorial dagger with a red-lipped gem pommel, blood on the tip",
  hawk_feather: "javelins fletched with hawk feathers",
  pit_wraps: "blood-stained grimy hand wraps",
  iron_kiss: "heavy iron knuckles shaped like lips",
  fists_of_grommar: "huge orcish knuckles studded with orc tusks",
  whispering_stone: "a smooth blue-grey runestone with faint glowing spiral",
  ash_staff: "a pale ash-wood staff with green leaves sprouting",
  sylvaren_root: "a living root staff twisting into a crown of glowing leaves",
  beggars_silk: "a ragged but rich purple silk loincloth with gold trim",
  bull_hide: "a black bull-hide cuirass with horn ornaments",
  scar_sleeve: "a torso wrap whose arm guard is studded with sharp barbs",
  thousand_rings: "fine dense chainmail with bronze rings worked in patterns",
  dragon_shed: "translucent green scale armor like shed snakeskin",
  wyrm_hide: "huge dark red dragon scales, scorched at the edges",
  heros_chest: "a gleaming muscle cuirass with a lion head on the chest",
  emperors_torso: "a golden imperial muscle cuirass with an eagle and purple sash",
  iron_tomb: "a black iron breastplate with coffin-lid rivets",
  unbroken_wall: "massive fortress-gate plate armor with portcullis bars motif",
  sun_crown: "an open helm with a sun-ray crown rim",
  wide_brim: "a wide-brimmed helm with shadowed grille",
  faceless: "a smooth faceless steel visor with no features",
  mask_of_the_dead: "a death-mask helm with a skull face visor",
  red_plume: "a crested helm with a blazing red plume",
  phoenix_crest: "a crest shaped like phoenix wings in orange-gold flame",
  lucky_buckler: "a buckler painted with a four-leaf horseshoe charm",
  eye_of_gorgon: "a round shield with a snake-haired gorgon face",
  tower_of_tears: "a tall scutum with weeping-eye paintings and tear streaks",
  last_legion: "a battered scutum with an eagle and many arrow scars",
  hunters_cuff: "a leather bracer with an arrow and wolf-fang charm",
  sun_bracer: "a polished bronze bracer embossed with a sun",
  snake_coil: "a chain sleeve coiled like a serpent with a snake head at the wrist",
  iron_arm: "a black iron segmented arm guard",
  arm_of_dolgar: "a dwarven gold-trimmed iron arm with rune knuckles",
  runners_wraps: "light leg wraps with small bronze wings",
  bronze_stride: "bronze greaves with a striding horse emblem",
  rooted_oak: "greaves carved like oak bark with roots at the feet",
  iron_pillars: "column-like iron leg plates",
};

// 1차 시트는 녹·광택 디테일이 많아 초상화와 따로 놀았다 → 디테일을 줄이고 평평한 면을 강하게 요구한다
const STYLE = "Art style: FLAT simplified dark-fantasy graphic novel painted with gouache, matching the attached reference (the portrait panels and the cut-out helmet/armor parts on the right are the exact target look): bold uniform black ink outline around every shape, a FEW LARGE flat color shapes, at most 3 flat tones per material (base, one blocky shadow, one small warm highlight) laid with loose visible brush strokes, desaturated muted palette (bone, rust, dull bronze, iron grey, dark leather, muted red), warm torchlight from the left. Keep detail LOW: no rust speckles or grime texture, no fine engraving, no glossy metal specular shine, no smooth gradients, no photo-real rendering, no bright saturated cartoon colors, NOT chibi, NOT cute storybook. VALUES ARE DARK like the reference: mostly mid-to-dark tones, big blocky near-black shadow shapes covering about 40% of each object on the side away from the light, only thin warm highlights on the lit edge, colors greyed and dirty (old bronze, dull ochre, brick red, ash grey), rough dry-brush gouache texture visible inside the flat shapes. Grim mature illustration, NOT a clean bright mobile-game icon, NOT vector art. The silhouette must read clearly at 48px.";
const SHEET = "ONE square image, a 3x3 grid of equal square cells, flat solid MAGENTA background (#FF00FF) in every cell (must stay magenta, no gradient, no frames, no grid lines, no shadows on the background). No text, no labels.";
const ICON = "Each cell: one game inventory ICON of a single item, centered, filling about 75% of the cell, slight 3/4 view, no hands, no person, no ground. Keep every item fully inside its own cell.";

const gear = [
  ...[...items.weapons, ...items.armors].map((it) => ({ id: it.id, desc: BASE[it.id] ?? it.name })),
  ...uniques.map((u) => ({
    id: u.id,
    desc: `${BASE[u.base] ?? u.base}, ${UNIQUE[u.id] ?? ""}${u.grade >= 3 ? ", legendary, one bold distinctive feature, faint eerie glow" : ", masterwork, one clear decorative feature"}`,
  })),
];

// 왼쪽 메뉴 아이콘 (id = 메뉴 id)
const MENU = {
  inbox: "a closed wax writing tablet (codex) with a stylus",
  overview: "a stone courtyard archway gate between two columns",
  roster: "a crested gladiator helmet with a red plume",
  training: "a wooden training dummy post with a wooden sword",
  challenge: "a rolled challenge scroll pierced by a dagger",
  match: "two crossed swords over a round shield",
  league: "a stepped victory podium with a laurel wreath on top",
  cup: "a two-handled bronze trophy cup",
  market: "a coin purse beside iron shackles",
  dungeon: "a dark stone cave doorway with a heavy wooden door",
  youth: "a small wooden practice sword and a small round wooden shield",
  staff: "a ring with two big iron keys",
  facilities: "a builder hammer resting on a broken marble column capital",
  finance: "an open strongbox full of gold coins",
  world: "an unrolled parchment map with a pin",
  hall: "a stone grave stele with a laurel wreath",
};
// 나라 문장
const CREST = {
  aurelia: "golden eagle with spread wings and a laurel wreath on a deep purple field",
  grommar: "horned bull skull over two crossed axes on a frost blue-grey field, blood red streaks",
  sylvaren: "pale silver ancient tree with sprawling roots on a moss green field",
  dolgar: "anvil and hammer before a mountain on a furnace orange-brown field",
  karsea: "sea serpent coiled around an anchor on a sea teal field with waves",
  kemer: "radiant sun disk over a stepped pyramid on a desert gold field with lapis blue accents",
};
// 전투 이펙트
const FX = {
  blood_drops: "a short diagonal trail of dark red blood drops",
  blood_gush: "a gush of dark red blood spraying diagonally upward",
  blood_pool: "a flat dark red blood pool splat seen from above",
  blood_splash: "a large dark red blood splash burst",
  crit_star: "a sharp red and bone-white impact star burst, critical hit",
  dust: "a cloud of tan arena dust puffing outward",
  impact_blunt: "a blunt impact burst with orange sparks and dust",
  slash_blood: "a curved sword slash arc trailing dark red blood",
  slash_gold: "a curved bone-white sword slash arc with a warm gold edge",
};

// 기술 아이콘: Tables/Skill.csv의 key마다 동작을 상징하는 문장(紋章)처럼
const SKILL = {
  power_strike: "a heavy sword coming down with a thick impact arc",
  flurry: "two crossing quick slash arcs from one short sword",
  armor_break: "an axe splitting a cracked breastplate",
  pierce: "a spear tip punching through a plate with sparks",
  execute: "a raised sword over a kneeling silhouette, grim",
  shield_bash: "a round shield smashing forward with impact lines",
  javelin: "a javelin flying with motion streaks",
  charge: "a charging warrior silhouette with dust trail",
  flourish: "a sword raised high with a laurel and cheering hands",
  cleave: "a greatsword chopping straight down splitting the ground",
  crushing_blow: "a flanged mace hitting with a bone crack starburst",
  hamstring: "a dagger slicing behind a knee",
  bleeding_cut: "a sword edge with dripping red drops",
  low_sweep: "a trident shaft sweeping at ankles",
  double_thrust: "a spear with two thrust arrows",
  haymaker: "a clenched wrapped fist with impact burst",
  net_throw: "a thrown fishing net spreading open",
  trip: "a foot hooking another foot",
  grapple: "two arms locked around a torso",
  sand_throw: "a hand flinging sand into the air",
  disarm: "a sword flicking another sword out of a hand",
  iron_wall: "a tall shield planted like a wall",
  catch_breath: "a breath cloud with a calm bowed head silhouette",
  war_roar: "a roaring open mouth with sound waves",
  second_wind: "a fallen gladiator rising with a warm glow",
  focus_stance: "a narrowed eye over a sword guard",
  bloodlust: "a red eye with blood drops",
  parry: "a sword deflecting another blade with a curved arc",
  disengage: "footprints stepping backward with an arrow",
  taunt: "a beckoning hand with crowd lines",
  salute: "a raised arm salute toward arena stands",
  rally: "a raised banner with a horn",
  flame_weapon: "a sword wreathed in flame",
  ward_rune: "a glowing rune circle shield",
  curse_weak: "a withered hand with purple wisps",
  lightning: "a lightning bolt striking down",
  adapt: "a chameleon-like swirl around a human mask",
  war_cry: "an orc tusked mouth shouting",
  horn_charge: "bull horns lowered charging",
  wind_step: "a light elven boot with wind swirls",
  earth_stance: "a dwarf boot rooted into stone",
  venom_bite: "a reptile fang dripping green venom",
  pounce: "a clawed beast leaping",
  unbreakable: "a cracked but standing iron helmet",
  crowd_hand: "a crowd thumbs-up gesture hand",
  sword_way: "a single upright gladius with a halo of steel",
  heavy_hands: "a mace head with heavy weight lines",
  axe_master: "two crossed axes over an anvil",
  spear_wall: "three spear tips in a row",
  shield_master: "a shield with a laurel wreath rim",
  iron_skin: "an armored forearm with dents",
  veteran: "a scarred old helmet with many notches",
  killer_instinct: "a skull with a predator eye",
  iron_lungs: "a chest with lungs and steady breath",
  crowd_darling: "a rose thrown onto the sand",
  riposte: "a sword returning along a curved counter arrow",
  counter_bash: "a shield pushing back with a rebound arrow",
  sidestep: "a dodging figure leaving an afterimage",
  spite: "a bleeding fist striking back",
  cover_ally: "a shield held over a fallen ally",
};
// 상태 아이콘: Tables/Status.csv (작은 둥근 딱지처럼 읽히게)
const STATUS = {
  stun: "stars circling a dazed head", prone: "a body lying flat on sand", bound: "a figure wrapped in a net", grappled: "a grabbing hand clamp",
  blinded: "an eye covered with sand", poison: "a green drop with a skull", bleed: "three red blood drops", weakened: "a drooping broken arm",
  armor_break: "a cracked breastplate", exposed: "an open gap in a guard with a target mark", hobbled: "a bandaged leg", arm_wound: "a bandaged arm",
  guard: "a raised shield", riposte: "a curved counter arrow on a blade", evade_up: "a ducking silhouette with motion lines", flame: "a flame tongue",
  ward: "a glowing rune ring", adapt: "a swirl of changing arrows", wind_step: "wind swirls", earth_stance: "a rooted stone foot", enraged: "a red angry face with steam",
};
const STRATEGY_MENU = { strategy: "a sand-table tactics board with three tokens and arrows" };
const COMP_MENU = {
  natcup: "a small two-handled bronze cup on a square plinth",
  olympica: "a laurel wreath encircling six tiny shields",
};
// 일정 메뉴 · 수상 메달 (Unity판 일정 화면, Logic/Awards.cs)
const EXTRA = {
  "menu/schedule": "a bronze wax-tablet calendar: a small hinged wooden tablet with a grid of carved day squares and one square marked by a red seal, simple bold shape readable as a tiny 26px menu icon",
  award: "an award medal: a round gold medallion stamped with a laurel wreath around a pair of crossed gladius swords, hanging from a short dark-red ribbon",
};
const TROPHY = {
  pantheon: "the grand continental trophy: a tall ornate golden chalice with two big curled handles, a temple-pillar stem and a small winged victory figure on the lid, the most splendid trophy of all",
  olympica: "the national-team trophy: a broad silver and gold laurel crown resting on a stepped marble pedestal with six small heraldic shields around its base",
  natcup_aurelia: "a golden two-handled cup topped by a spread-winged eagle, red enamel band",
  natcup_dolgar: "a heavy iron and bronze cup shaped like a dwarven forge crucible with hammer-shaped handles and engraved runes",
  natcup_karsea: "a pearl-white and teal cup shaped like a seashell on a coral stem with ship-anchor handles",
  natcup_kemer: "a slender golden cup with a sun disk on top and handles shaped like jackal heads, lapis blue inlay",
  natcup_grommar: "a brutal black iron cup with horned handles and a bone skull on its front, dented and blood-red rim",
  natcup_sylvaren: "a slim silver goblet whose stem is a twisting tree with silver leaves as handles, green gem",
  league_1: "a large gold shield-shaped plate on a stand with a crown on top, league champion prize",
  league_2: "a medium silver shield-shaped plate on a stand with laurel leaves",
  league_3: "a small bronze shield-shaped plate on a stand with a single star",
  league_4: "a small dark iron shield-shaped plate on a wooden stand, humble and battered",
};
const csvKeys = (file) => readFileSync(file, "utf8").split(/\r?\n/).slice(3).filter(Boolean).map((l) => l.split(",")[1]);
const skillList = csvKeys(`${ROOT}/Tables/Skill.csv`).map((id) => ({ id, desc: SKILL[id] ?? id }));
const statusList = csvKeys(`${ROOT}/Tables/Status.csv`).map((k) => k.replace(/([a-z])([A-Z])/g, "$1_$2").toLowerCase()).map((id) => ({ id, desc: STATUS[id] ?? id }));

// 묶음별 출력 폴더(Resources/Art 아래)·최종 크기·칸 설명. clean = 옆 칸 조각 지우기 (이펙트는 흩어진 방울이 본체라 끈다)
const GROUPS = {
  items: { dir: "icon/items", size: 512, clean: true, cell: ICON, list: gear },
  menu: { dir: "icon/menu", size: 64, clean: true, cell: `${ICON} Very simple bold shape, readable as a tiny 26px menu icon.`, list: Object.entries(MENU).map(([id, desc]) => ({ id, desc })) },
  crest: { dir: "crest", size: 128, clean: true, cell: "Each cell: one heraldic shield crest, upright, front view, centered, filling about 80% of the cell, dull bronze rim, flat painted field. Keep every crest fully inside its own cell.", list: Object.entries(CREST).map(([id, desc]) => ({ id, desc: `a heraldic shield: ${desc}` })) },
  skills: { dir: "icon/skills", size: 128, clean: true, cell: "Each cell: one square SKILL ICON that shows the action as a simple bold emblem (weapon or body part plus a motion arc or symbol), centered, filling about 75% of the cell, no frame, no circle background, no text. Keep every icon fully inside its own cell.", list: skillList },
  status: { dir: "icon/status", size: 64, clean: true, cell: "Each cell: one tiny STATUS EFFECT ICON as a very simple bold symbol, centered, filling about 70% of the cell, no frame, no background, no text. Must read at 20px. Keep every icon fully inside its own cell.", list: statusList },
  menux: { dir: "icon/menu", size: 64, clean: true, cell: `${ICON} Very simple bold shape, readable as a tiny 26px menu icon.`, list: Object.entries(STRATEGY_MENU).map(([id, desc]) => ({ id, desc })) },
  menuy: { dir: "icon/menu", size: 64, clean: true, cell: `${ICON} Very simple bold shape, readable as a tiny 26px menu icon.`, list: Object.entries(COMP_MENU).map(([id, desc]) => ({ id, desc })) },
  trophy: { dir: "icon/trophy", size: 256, clean: true, cell: "Each cell: one TROPHY object, upright, front view, centered, filling about 80% of the cell, no pedestal shadow, no people, no text, no letters. Keep every trophy fully inside its own cell.", list: Object.entries(TROPHY).map(([id, desc]) => ({ id, desc })) },
  extra: { dir: "icon", size: 128, clean: true, cell: `${ICON} Very simple bold shape.`, list: Object.entries(EXTRA).map(([id, desc]) => ({ id, desc })) },
  fx: { dir: "fx", size: 256, clean: false, cell: "Each cell: one 2D combat effect sprite, centered, filling about 75% of the cell, no ground, no characters, edges made of loose gouache brush strokes. Keep every effect fully inside its own cell.", list: Object.entries(FX).map(([id, desc]) => ({ id, desc })) },
};

function sheets() {
  const out = [];
  for (const [g, grp] of Object.entries(GROUPS))
    for (let i = 0, k = 1; i < grp.list.length; i += 9, k++) out.push({ g, id: `${g}_${String(k).padStart(2, "0")}`, cells: grp.list.slice(i, i + 9) });
  return out;
}

const mode = process.argv[2];
if (mode === "ref") {
  // 화풍 기준: 왼쪽 dark_ref, 오른쪽 실제 게임 초상화 파츠(투구·갑옷)
  const P = `${ART}/portrait2/human/front`;
  const parts = ["helmet_crest.png", "helmet_visor.png", "armor_plate.png", "armor_harness.png"];
  const tiles = await Promise.all(parts.map(async (p, i) => ({
    input: await sharp(`${P}/${p}`).trim({ threshold: 1 }).resize(300, 300, { fit: "contain", background: { r: 0, g: 0, b: 0, alpha: 0 } }).png().toBuffer(),
    left: 1000 + (i % 2) * 320, top: 10 + Math.floor(i / 2) * 320,
  })));
  await sharp({ create: { width: 1650, height: 660, channels: 4, background: "#16110e" } })
    .composite([{ input: "art/source/style/dark_ref.png", left: 0, top: 4 }, ...tiles]).png().toFile(STYLE_REF);
  console.log(STYLE_REF);
} else if (mode === "manifest") {
  const list = sheets();
  const manifest = {
    presets: Object.fromEntries(Object.entries(GROUPS).map(([g, grp]) => [g, `${SHEET} ${grp.cell}`])),
    style: STYLE,
    styleRef: STYLE_REF,
    styleRefPresets: Object.keys(GROUPS),
    items: list.map(({ g, id, cells }) => ({
      id,
      preset: g,
      out: `${SHEETS}/${id}.png`,
      prompt: "Cells left-to-right, top-to-bottom:\n" + cells.map((c, i) => `${i + 1}. ${c.desc}`).join("\n") +
        (cells.length < 9 ? `\n${cells.length + 1}-9. leave empty (flat magenta)` : ""),
      cells: cells.map((c) => c.id),
    })),
  };
  writeFileSync(MANIFEST, JSON.stringify(manifest, null, 2) + "\n");
  console.log(`시트 ${list.length}장, 그림 ${list.reduce((n, s) => n + s.cells.length, 0)}개 → ${MANIFEST}`);
} else if (mode === "slice") {
  const manifest = JSON.parse(readFileSync(MANIFEST, "utf8"));
  const only = process.argv[3];
  const sheetsToCut = manifest.items.filter((it) => !only || it.preset === only);
  const done = [];
  // 고정 칸으로 자르면 칸 밖으로 삐져나온 부분이 잘린다 → 그림 덩어리 단위로 나눈다.
  // 마젠타를 투명하게 한 뒤 덩어리(8방향 연결)마다 중심이 있는 칸에 통째로 붙이고, 칸마다 그 덩어리들만 512 캔버스에 놓는다.
  for (const it of sheetsToCut) {
    if (!existsSync(it.out)) { console.log(`없음: ${it.out}`); continue; }
    const grp = GROUPS[it.preset], OUT = `${ART}/${grp.dir}`;
    mkdirSync(OUT, { recursive: true });
    const { data, info } = await sharp(it.out).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    const W = info.width, H = info.height;
    // 마젠타 → 투명 (tools/slice-sheet.mjs와 같은 식, 가장자리 분홍 번짐 제거)
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
    // 덩어리 나누기
    const label = new Int32Array(W * H).fill(-1), comps = [];
    for (let p = 0; p < W * H; p++) {
      if (label[p] >= 0 || data[p * 4 + 3] < 24) continue;
      const k = comps.length, stack = [p];
      label[p] = k;
      let n = 0, sx = 0, sy = 0;
      while (stack.length) {
        const q = stack.pop();
        const x = q % W, y = (q - x) / W;
        n++; sx += x; sy += y;
        for (let dy = -1; dy <= 1; dy++)
          for (let dx = -1; dx <= 1; dx++) {
            const nx = x + dx, ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
            const r = ny * W + nx;
            if (label[r] < 0 && data[r * 4 + 3] >= 24) { label[r] = k; stack.push(r); }
          }
      }
      comps.push({ n, cell: Math.min(2, Math.floor((sy / n) / (H / 3))) * 3 + Math.min(2, Math.floor((sx / n) / (W / 3))) });
    }
    // 칸별 가장 큰 덩어리 기준으로, 아주 작은 부스러기(1% 미만)는 버린다. 이펙트는 흩어진 방울이 본체라 다 둔다
    const maxOf = Array(9).fill(0);
    for (const c of comps) maxOf[c.cell] = Math.max(maxOf[c.cell], c.n);
    const keep = comps.map((c) => !grp.clean || c.n >= maxOf[c.cell] * 0.01);
    for (let i = 0; i < 9; i++) {
      const id = it.cells[i];
      if (!id) continue;
      let x0 = W, y0 = H, x1 = -1, y1 = -1;
      for (let p = 0; p < W * H; p++) {
        const k = label[p];
        if (k < 0 || comps[k].cell !== i || !keep[k]) continue;
        const x = p % W, y = (p - x) / W;
        x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y);
      }
      if (x1 < 0) { console.log(`빈 칸: ${it.id} ${i + 1} (${id})`); continue; }
      const w = x1 - x0 + 1, h = y1 - y0 + 1, cut = Buffer.alloc(w * h * 4);
      for (let y = 0; y < h; y++)
        for (let x = 0; x < w; x++) {
          const p = (y0 + y) * W + x0 + x, k = label[p];
          if (k < 0 || comps[k].cell !== i || !keep[k]) continue;
          data.copy(cut, (y * w + x) * 4, p * 4, p * 4 + 4);
        }
      // 다른 파츠와 같은 512 캔버스 가운데에 (긴 변 460)
      const scale = Math.min(1, 460 / Math.max(w, h));
      const rw = Math.max(1, Math.round(w * scale)), rh = Math.max(1, Math.round(h * scale));
      const resized = await sharp(cut, { raw: { width: w, height: h, channels: 4 } }).resize(rw, rh).png().toBuffer();
      await sharp({ create: { width: 512, height: 512, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } } })
        .composite([{ input: resized, left: Math.round((512 - rw) / 2), top: Math.round((512 - rh) / 2) }])
        .png().toFile(`${OUT}/${id}.png`);
      done.push({ file: `${OUT}/${id}.png`, grp });
    }
  }
  // 슬라이서는 512 캔버스로 내보낸다 → 묶음 크기로 줄인다 (메뉴 64, 문장 128, 이펙트 256)
  for (const { file, grp } of done) {
    if (!existsSync(file) || grp.size >= 512) continue;
    await sharp(file).resize(grp.size, grp.size, { kernel: "lanczos3" }).png().toFile(file + ".tmp");
    (await import("node:fs")).renameSync(file + ".tmp", file);
  }
  console.log(`잘라 넣은 그림 ${done.length}개`);
} else {
  console.log("사용: node ColosseumUnity/Tools/item-icons.mjs ref | manifest | slice [items|menu|crest|fx]");
}
