// 루두스 문장(팀 아이콘): 나라마다 216개 (6x6 마젠타 시트 6장 × 6개 나라 = 1,296개).
// 시트 하나에 최대한 몰아 그린다. 칸 설명은 나라별 문양 36개 × 변형 6개(방패 모양·나눔·색)로 겹치지 않게 만든다.
// 사용 (저장소 루트에서):
//   node ColosseumUnity/Tools/team-crests.mjs manifest     시트 목록 → ColosseumUnity/Tools/team-crests.json
//   node tools/gen-images.mjs --manifest ColosseumUnity/Tools/team-crests.json --jobs 3     Codex로 시트 생성
//   node ColosseumUnity/Tools/team-crests.mjs slice [나라]  시트를 잘라 Assets/Resources/Art/crest/team/<나라>_<번호>.png (128)
//   node ColosseumUnity/Tools/team-crests.mjs list         잘라 둔 문장 목록 → Assets/Resources/Data/crests.json
import { existsSync, mkdirSync, readFileSync, readdirSync, renameSync, writeFileSync } from "node:fs";
import sharp from "sharp";

const ROOT = "ColosseumUnity";
const SHEETS = "art/source/crest_sheets";
const OUT = `${ROOT}/Assets/Resources/Art/crest/team`;
const MANIFEST = `${ROOT}/Tools/team-crests.json`;
const STYLE_REF = "art/source/style/flat_ref.png";
const N = 6; // 6x6 칸
const SIZE = 128;

// 화풍: 게임 초상화·장비 아이콘과 같은 어두운 플랫 붓그림 (첨부 기준 그림 flat_ref). 모든 시트가 같은 글을 쓴다
const STYLE = "Art style (identical on every sheet): the SAME dark-fantasy graphic novel look as the attached reference — copy its exact rendering: bold uniform black ink outline of the same weight, a few large flat colour shapes, at most 3 flat tones per area (base, one blocky near-black shadow, one thin warm highlight on the left edge) laid with loose dry gouache brush strokes, warm torchlight from the left. Colours are greyed, dirty and DARK (old bronze, dull ochre, brick red, ash grey, dark leather, faded dye) — mostly mid-to-dark values, no bright saturated colours, no clean white. Keep detail LOW: no fine engraving, no glossy metal shine, no gradients, no embossing, no photo-real rendering, NOT vector art, NOT a clean bright mobile-game badge, NOT a sticker. Each crest must still read clearly at 40px.";
const SHEET = `ONE square image, a ${N}x${N} grid of ${N * N} equal square cells, flat solid MAGENTA background (#FF00FF) in every cell (must stay magenta, no gradient, no frames, no grid lines, no shadows on the background). No text, no letters, no numbers, no labels anywhere.`;
const CELL = "Each cell: one small TEAM CREST of a gladiator school, upright, front view, centered, filling about 78% of the cell, with a clear gap of magenta between neighbouring crests. Follow each cell's silhouette, field, colours, ornament and emblem layout exactly — neighbouring crests must differ strongly in SILHOUETTE and COLOUR so that no two crests on the sheet look alike, even as tiny thumbnails. Keep every crest fully inside its own cell.";

// 공용 실루엣·나눔·장식·배치 (나라별 것을 더 붙인다)
const SHAPES = ["a heater shield", "a round shield", "a tall kite shield", "an oval shield", "a curved rectangular scutum", "a shield with a notched top", "a crescent pelta shield", "a hexagonal shield", "a hanging banner on a crossbar with a swallow-tail end", "a round medallion disk", "a square plaque with cut corners", "a tall tower shield"];
const DIVS = ["plain field", "field split vertically in two colours", "quartered field", "field with a broad horizontal band", "field with a diagonal band", "field with a chevron", "plain field with a thick contrasting border", "field divided in an X (saltire)", "field with a zigzag horizontal division", "field with a bold checkered top half"];
const ORNS = ["no ornament", "a laurel wreath around the edge", "a pair of wings behind the shield", "two crossed weapons behind the shield", "a crown on top", "a ring of spikes around the rim", "a blank ribbon scroll under it (no writing)", "big iron studs on the rim", "two short chains hanging below", "a pair of horns on top"];
const LAYOUTS = ["one large emblem", "the emblem with three small stars above it", "two of the emblem facing each other", "the emblem inside a small central roundel of another colour", "the emblem with a small crescent at the base", "the emblem cut in half by the field division, two colours swapped"];

// 나라별 분위기: 고유 실루엣·장식, 색 짝 10개, 문양 36개
const NATIONS = {
  aurelia: {
    mood: "imperial Roman style",
    shapes: ["a legion scutum with a central boss", "an eagle-topped standard plaque", "a laurel-framed round clipeus"],
    orns: ["an eagle head on top", "a pair of crossed fasces behind"],
    colors: ["deep purple and old gold", "crimson and bone", "dark red and bronze", "faded purple and ivory", "black and gold", "bone and brick red", "olive green and gold", "ash grey and crimson", "dark blue and bronze", "ochre and black"],
    motifs: ["an eagle with spread wings", "a laurel wreath", "a roaring lion head", "a she-wolf", "a crossed pair of gladius swords", "a marble column", "a legion standard topped with an eagle", "a chariot wheel", "a crown of laurel and a dagger", "a trident and net", "a galley prow", "a war elephant", "a bull", "a horse head", "a rising sun", "a torch", "a winged helmet", "a coin with a profile head", "a boar", "a pair of crossed spears", "a stag", "a griffin", "a scorpion", "an owl", "a vine with grapes", "a capricorn goat-fish", "a lightning bolt clutched by a fist", "a centurion helmet with a crest", "a triumphal arch", "an oak branch", "a serpent around a staff", "a crowned eagle head", "a pegasus", "a hammer and tongs", "a lyre", "a gladiator helmet with a grille visor"],
  },
  grommar: {
    mood: "savage orcish frost-tribe style",
    shapes: ["a ragged hide shield stretched on a wooden frame", "a round shield made of nailed planks", "a jagged shield with tusk points"],
    orns: ["bones lashed across the top", "feathers and beads hanging from the rim"],
    colors: ["frost blue-grey and blood red", "black and bone", "ash grey and rust red", "dark teal and bone", "blood red and black", "iron grey and pale ice blue", "mud brown and bone", "dark green and rust red", "charcoal and ochre", "dirty white and black"],
    motifs: ["a horned bull skull", "two crossed axes", "a howling wolf head", "a mammoth with tusks", "a clenched fist", "a pair of boar tusks", "a cleaver", "a bear paw with claws", "a spiked club", "a snarling orc face", "a raven", "a jagged mountain peak", "a broken chain", "a ram skull", "a snow leopard", "a war drum", "a bloody hand print", "a spear through a skull", "a crescent moon with fangs", "a thunderbird", "a totem pole head", "a serrated sword", "a frost giant hammer", "a wolf paw print", "a battle horn", "a burning brazier", "a crossed pair of bones", "a shaman staff with feathers", "an eye with a scar", "a yak head", "a spiked helmet", "a lightning-split tree", "a hanging skull banner", "a pair of crossed sickles", "a vulture", "a snarling warg"],
  },
  sylvaren: {
    mood: "elven forest style",
    shapes: ["a leaf-shaped shield", "a tall slender almond shield", "a shield framed by twisting branches"],
    orns: ["vines curling around the rim", "a crescent moon on top"],
    colors: ["moss green and pale silver", "midnight blue and silver", "dark green and old gold", "deep teal and ivory", "autumn brown and pale gold", "black and moonlight silver", "plum purple and silver", "rust orange and dark green", "slate grey and pale green", "wine red and bone"],
    motifs: ["an ancient tree with roots", "a leaping stag", "a crescent moon", "an oak leaf", "an owl with spread wings", "a longbow and arrow", "a fox", "a unicorn head", "a lily flower", "a hawk", "a star of eight points", "a coiled vine", "a mushroom ring", "a swan", "a hare", "a dragonfly", "a pinecone", "a bear standing", "a butterfly", "a wolf under the moon", "a waterfall", "a crossed pair of elven blades", "a holly branch", "a moth", "a heron", "a crown of leaves", "a lynx", "a feather", "a willow tree", "a crossed pair of arrows", "an antler crown", "a snake among leaves", "a sun through branches", "a nightingale", "a rose with thorns", "a boar in the forest"],
  },
  dolgar: {
    mood: "dwarven mountain-forge style",
    shapes: ["a square iron plaque riveted at the corners", "a thick round shield with a huge boss", "an anvil-shaped shield"],
    orns: ["a heavy iron frame with big bolts", "two hammers crossed above"],
    colors: ["furnace orange and dark brown", "iron grey and ember red", "black and copper", "dark brown and old gold", "slate blue and bronze", "rust red and steel grey", "ochre and charcoal", "dark green and copper", "bone and iron black", "deep red and gold"],
    motifs: ["an anvil", "a war hammer", "a mountain with a gate", "a pickaxe", "a bearded dwarf face", "a forge flame", "a cog wheel", "a mine cart", "a crossed pair of pickaxes", "a stone tower", "a cave bear", "a lantern", "a gemstone", "a horned helmet", "a battle axe", "a ram", "a stone fist", "a coal-fired furnace", "a key", "a crown over a mountain", "a drake", "a pair of tongs", "a shield boss with rivets", "a goat head", "a hammer striking sparks", "a mattock", "a stone bridge", "a beer tankard", "a badger", "a mountain eagle", "a chain link", "a runed stone without letters", "an ingot stack", "a crossed hammer and axe", "a cave salamander", "a boar with iron tusks"],
  },
  karsea: {
    mood: "seafaring coastal style",
    shapes: ["a round shield with a rope rim", "a shield shaped like a ship sail", "a scallop-shell shaped shield"],
    orns: ["a coiled rope around the rim", "an anchor hanging below"],
    colors: ["sea teal and sand", "navy blue and bone", "dark teal and coral red", "storm grey and gold", "deep blue and bronze", "black and sea green", "faded red and navy", "sand and dark brown", "slate and pale teal", "ochre and deep blue"],
    motifs: ["a sea serpent around an anchor", "a kraken", "a sailing ship", "a trident", "a shark", "a gull", "a crab", "a whale tail", "a lighthouse", "a seashell", "a mermaid silhouette", "a crossed pair of oars", "a compass rose", "a dolphin", "a seahorse", "a hammerhead shark", "an octopus", "a ship wheel", "a harpoon", "a wave curling", "a pirate cutlass", "a turtle", "a sea eagle with a fish", "a pearl in a clam", "a manta ray", "a crossed pair of anchors", "a storm cloud with lightning", "a fish skeleton", "a swordfish", "a lobster", "a coral branch", "a sea lion", "a galleon prow figure", "a jellyfish", "a pelican", "a coiled rope with a hook"],
  },
  kemer: {
    mood: "ancient desert kingdom style",
    shapes: ["a tall rounded-top shield of hide", "a shield inside a winged sun frame", "a trapezoid temple-gate plaque"],
    orns: ["outspread falcon wings behind", "a sun disk on top"],
    colors: ["desert gold and lapis blue", "terracotta and gold", "black and gold", "sand and turquoise", "deep red and lapis blue", "ivory and dark blue", "ochre and black", "dark green and gold", "rust red and sand", "lapis blue and copper"],
    motifs: ["a sun disk with rays", "a stepped pyramid", "a scarab beetle", "a jackal head", "a cobra", "a falcon", "a crook and flail", "a lion with a human face", "a lotus flower", "a crocodile", "a ram-headed sphinx", "a palm tree", "a camel", "an eye with markings", "a winged sun", "an obelisk", "an ibis", "a vulture with spread wings", "a crossed pair of khopesh swords", "a desert scorpion", "a hippopotamus", "a cat", "a feather of truth", "a reed boat", "a crescent dune", "a hawk-headed figure", "a sun barge", "a lion", "a papyrus plant", "a horned cow head", "a serpent with wings", "a chariot", "a spear and round shield", "a desert fox", "an oasis pool", "a starry sky with three stars"],
  },
};

function cellsFor(nation, sheet) {
  const def = NATIONS[nation];
  const shapes = [...SHAPES, ...def.shapes, ...def.shapes]; // 나라 고유 실루엣이 더 자주 나온다
  const orns = [...ORNS, ...def.orns];
  return def.motifs.map((motif, m) => {
    // 모든 축을 다른 보폭으로 섞어 같은 칸·같은 줄끼리 겹치지 않게
    const k = sheet * 36 + m;
    const shape = shapes[(k * 7 + sheet) % shapes.length];
    const div = DIVS[(k * 3 + Math.floor(m / N)) % DIVS.length];
    const col = def.colors[(k * 7 + sheet * 3) % def.colors.length];
    const orn = orns[(k * 5 + sheet * 2) % orns.length];
    const lay = LAYOUTS[(m + sheet * 5) % LAYOUTS.length];
    return { id: `${nation}_${String(k + 1).padStart(3, "0")}`, desc: `${shape}; ${div} in ${col}; ${orn}; ${lay}: ${motif}` };
  });
}

const mode = process.argv[2];
if (mode === "manifest") {
  const items = [];
  for (const nation of Object.keys(NATIONS))
    for (let k = 0; k < 6; k++) {
      const cells = cellsFor(nation, k);
      items.push({
        id: `crest_${nation}_${k + 1}`,
        preset: "crest",
        out: `${SHEETS}/${nation}_${k + 1}.png`,
        prompt: `All crests on this sheet share a ${NATIONS[nation].mood}.\nCells left-to-right, top-to-bottom (${N} per row):\n` + cells.map((c, i) => `${i + 1}. ${c.desc}`).join("\n"),
        cells: cells.map((c) => c.id),
      });
    }
  writeFileSync(MANIFEST, JSON.stringify({ presets: { crest: `${SHEET} ${CELL}` }, style: STYLE, styleRef: STYLE_REF, styleRefPresets: ["crest"], items }, null, 2) + "\n");
  console.log(`시트 ${items.length}장, 문장 ${items.length * 36}개 → ${MANIFEST}`);
} else if (mode === "slice") {
  const manifest = JSON.parse(readFileSync(MANIFEST, "utf8"));
  const only = process.argv[3];
  mkdirSync(OUT, { recursive: true });
  let done = 0;
  for (const it of manifest.items.filter((x) => !only || x.id.includes(only))) {
    if (!existsSync(it.out)) { console.log(`없음: ${it.out}`); continue; }
    const { data, info } = await sharp(it.out).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    const W = info.width, H = info.height;
    // 마젠타 → 투명 (item-icons.mjs와 같은 식)
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
    // 덩어리(8방향 연결)마다 중심이 있는 칸에 붙인다
    const label = new Int32Array(W * H).fill(-1), comps = [];
    for (let p = 0; p < W * H; p++) {
      if (label[p] >= 0 || data[p * 4 + 3] < 24) continue;
      const k = comps.length, stack = [p];
      label[p] = k;
      let n = 0, sx = 0, sy = 0, bx0 = W, by0 = H, bx1 = 0, by1 = 0;
      while (stack.length) {
        const q = stack.pop();
        const x = q % W, y = (q - x) / W;
        n++; sx += x; sy += y;
        if (x < bx0) bx0 = x; if (x > bx1) bx1 = x; if (y < by0) by0 = y; if (y > by1) by1 = y;
        for (let dy = -1; dy <= 1; dy++)
          for (let dx = -1; dx <= 1; dx++) {
            const nx = x + dx, ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
            const r = ny * W + nx;
            if (label[r] < 0 && data[r * 4 + 3] >= 24) { label[r] = k; stack.push(r); }
          }
      }
      // 이웃 문장과 맞닿아 한 덩어리가 됐으면(칸 1.25개보다 넓다) 픽셀마다 제 칸으로 나눈다
      const split = bx1 - bx0 > (W / N) * 1.25 || by1 - by0 > (H / N) * 1.25;
      comps.push({ n, split, cell: Math.min(N - 1, Math.floor((sy / n) / (H / N))) * N + Math.min(N - 1, Math.floor((sx / n) / (W / N))) });
    }
    // 픽셀마다 속한 칸과 조각(덩어리×칸)
    const pixCell = new Int16Array(W * H).fill(-1), subN = new Map();
    for (let p = 0; p < W * H; p++) {
      const k = label[p];
      if (k < 0) continue;
      const x = p % W, y = (p - x) / W;
      const c = comps[k].split ? Math.min(N - 1, Math.floor(y / (H / N))) * N + Math.min(N - 1, Math.floor(x / (W / N))) : comps[k].cell;
      pixCell[p] = c;
      const key = k * N * N + c;
      subN.set(key, (subN.get(key) ?? 0) + 1);
    }
    const maxOf = Array(N * N).fill(0);
    for (const [key, n] of subN) maxOf[key % (N * N)] = Math.max(maxOf[key % (N * N)], n);
    const keepPix = (p) => { const c = pixCell[p]; return c >= 0 && subN.get(label[p] * N * N + c) >= maxOf[c] * 0.02; };
    const cellArea = (W / N) * (H / N);
    for (let i = 0; i < N * N; i++) {
      const id = it.cells[i];
      if (!id) continue;
      // 칸이 비었거나 부스러기뿐이면 건너뛴다
      if (maxOf[i] < cellArea * 0.08) { console.log(`빈 칸: ${it.id} ${i + 1}`); continue; }
      let x0 = W, y0 = H, x1 = -1, y1 = -1;
      for (let p = 0; p < W * H; p++) {
        if (pixCell[p] !== i || !keepPix(p)) continue;
        const x = p % W, y = (p - x) / W;
        x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y);
      }
      const w = x1 - x0 + 1, h = y1 - y0 + 1, cut = Buffer.alloc(w * h * 4);
      for (let y = 0; y < h; y++)
        for (let x = 0; x < w; x++) {
          const p = (y0 + y) * W + x0 + x;
          if (pixCell[p] !== i || !keepPix(p)) continue;
          data.copy(cut, (y * w + x) * 4, p * 4, p * 4 + 4);
        }
      const inner = Math.round(SIZE * 0.92), scale = inner / Math.max(w, h);
      const rw = Math.max(1, Math.round(w * scale)), rh = Math.max(1, Math.round(h * scale));
      const resized = await sharp(cut, { raw: { width: w, height: h, channels: 4 } }).resize(rw, rh, { kernel: "lanczos3" }).png().toBuffer();
      await sharp({ create: { width: SIZE, height: SIZE, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } } })
        .composite([{ input: resized, left: Math.round((SIZE - rw) / 2), top: Math.round((SIZE - rh) / 2) }])
        .png({ palette: true, quality: 92, compressionLevel: 9, effort: 8 }).toFile(`${OUT}/${id}.png`);
      done++;
    }
  }
  console.log(`잘라 넣은 문장 ${done}개`);
} else if (mode === "list") {
  // 게임이 읽는 목록: 나라별 문장 id
  const files = existsSync(OUT) ? readdirSync(OUT).filter((f) => f.endsWith(".png")).map((f) => f.slice(0, -4)).sort() : [];
  const byNation = {};
  for (const id of files) (byNation[id.split("_")[0]] ??= []).push(id);
  const path = `${ROOT}/Assets/Resources/Data/crests.json`;
  writeFileSync(path + ".tmp", JSON.stringify(byNation, null, 1) + "\n");
  renameSync(path + ".tmp", path);
  console.log(Object.entries(byNation).map(([n, l]) => `${n} ${l.length}`).join(" · ") + ` → ${path}`);
} else {
  console.log("사용: node ColosseumUnity/Tools/team-crests.mjs manifest | slice [나라] | list");
}
