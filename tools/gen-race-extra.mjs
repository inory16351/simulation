// 종족마다 그림 초상화 두 번째 세트(정면 머리 9칸 + 정면 파츠 9칸)를 Codex로 만든다. 무작위 조합 폭을 넓히는 용도.
// 첫 세트 머리 시트를 기준 이미지로 넣어 같은 틀(머리 위치·크기)에 맞추므로, 두 세트의 머리와 파츠를 서로 섞어 쓸 수 있다.
// 사용: node tools/gen-race-extra.mjs [종족...] [--jobs 4]   → art/source/portrait2/<종족>/front_{heads,parts}_2.png
import { spawn } from "node:child_process";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";

const STYLE = "Style: very simple cute chibi game sprite art in the spirit of RimWorld mod character portraits: a big round head, tiny simple facial features, large simple dark eyes with one white highlight, thin soft dark-brown outlines, clean simple shapes. Coloring like a gentle storybook gouache painting: soft flat base colors with one soft painted shade and a subtle brush texture, warm torchlight tint from the left, muted earthy palette. NOT realistic, NOT detailed. Match the attached reference sheet's style exactly. No text, no watermark.";
const SHEET = "ONE square image 1536x1536, a 3x3 grid of equal square cells, flat solid MAGENTA background (#FF00FF) filling every cell (must stay magenta, not transparent, not black), no gradient, no shadows on the background, no grid lines.";
const FRONT = "STRICTLY FRONT VIEW, perfectly symmetric, looking straight at the viewer, centered horizontally: the head top at 12% from the top of the cell and the chin at 66%, eyes at 46%, a short neck, small rounded shoulders reaching the bottom edge spanning 60% of the cell width.";

/** 파츠 칸 순서 (첫 세트와 같다): 짧은 머리, 긴 머리, 땋은 머리, 긴 수염, 짧은 수염, 열린 투구, 닫힌 투구, 가벼운 갑옷, 무거운 갑옷 */
const RACES = {
  human: {
    look: "HUMANS",
    heads: ["dark-skinned young woman with a confident smile", "pale freckled young man", "olive-skinned scarred veteran man", "tan woman with a stern look", "very dark-skinned big-jawed man", "ruddy old man with deep wrinkles", "pale woman with a nose scar", "light-brown teenage boy with a cheeky grin", "middle-aged weathered woman"],
    genders: ["f", "m", "m", "f", "m", "m", "f", "m", "f"],
    bald: "All completely BALD and beardless (keep the eyebrows)",
    parts: ["a messy curly black hair cap", "long wavy auburn hair to the shoulders", "a high blond ponytail with a leather tie", "a thick dark-brown full beard with mustache", "a thin mustache with a small chin beard", "a bronze brimmed open-face helmet with cheek guards (face open)", "a full-face sealed iron helm with round eye holes", "a quilted linen tunic with a broad leather belt", "a chainmail shirt with leather shoulder pieces"],
  },
  elf: {
    look: "fantasy ELVES with long pointed ears, slender faces and almond-shaped eyes",
    heads: ["copper-skinned elf woman with face tattoo lines", "very pale young elf man", "dark-skinned elf man with white brows", "fair elf woman with rosy cheeks", "grey-blue-skinned stern elf woman", "golden-tan elf man with a calm smile", "old elf woman with laugh lines", "olive-skinned elf youth", "pale scarred elf man"],
    genders: ["f", "m", "m", "f", "f", "m", "f", "m", "m"],
    bald: "All completely BALD (keep the eyebrows and the long ears)",
    parts: ["short tousled copper-red hair", "very long flowing white hair", "twin side braids of dark green-black hair", null, null, "a silver circlet half-helm with a crescent crest (face open)", "a closed elven helm shaped like an owl face", "a green hooded mantle over a leather vest", "an ornate silver-green lamellar cuirass"],
  },
  dwarf: {
    look: "stout fantasy DWARVES with wide faces, big round noses and bushy eyebrows",
    heads: ["ruddy laughing woman", "pale blond-browed man", "dark-skinned old man", "tanned man with a burn scar", "grey-skinned stern woman", "rosy young man", "weathered one-eyed man", "freckled cheerful woman", "deep-brown grumpy elder"],
    genders: ["f", "m", "m", "m", "f", "m", "m", "f", "m"],
    bald: "All completely BALD and BEARDLESS (keep the bushy eyebrows)",
    parts: ["a short flat-top brown hair cut", "long wild white hair", "a crown of thick blond braids", "a forked ginger beard with braided tips reaching the chest", "a short black beard with a big curled mustache", "an open bronze dwarf helm with a cheek guard and a crest ridge (face open)", "a closed iron dwarf helm shaped like a stern face mask", "a leather apron harness with a heavy fur collar", "a gold-trimmed dwarven plate with a big round gorget"],
  },
  orc: {
    look: "fantasy ORCS with green or grey-green skin, small lower tusks, pointed ears and a heavy brow",
    heads: ["dark-green woman with nose ring", "olive young orc with big tusks", "grey-green old orc with scars", "pale-green grinning man", "deep-green fierce woman with face paint", "brown-green huge-jawed brute", "moss-green calm man", "light grey-green woman", "dark olive one-tusk man"],
    genders: ["f", "m", "m", "m", "f", "m", "m", "f", "m"],
    bald: "All completely BALD and beardless (keep the tusks and ears)",
    parts: ["short shaved sides with a red crest", "long braided grey hair", "many thin black dreadlocks tied back", "a long braided black beard", "a scruffy short brown beard", "a horned bone-and-iron open helm (face open)", "an iron bucket helm with a fanged grille", "a fur mantle with leather straps and bone beads", "a heavy black iron plate with spikes"],
  },
  minotaur: {
    look: "MINOTAURS: bull-headed people with short brown or black fur, a broad muzzle, curved horns",
    heads: ["white bull with black spots", "golden-brown young bull", "dark grey cow-woman with a gold ring", "black old bull with wide horns", "red bull with a torn ear", "cream cow-woman with flower-shaped spots", "brown bull with forward horns", "dark brown cow-woman", "silver-grey bull with a nose ring"],
    genders: ["m", "m", "f", "m", "m", "f", "m", "f", "m"],
    bald: "No hair tufts and no beard (just the short fur, horns and ears), no clothing",
    parts: ["a curly forelock tuft of dark fur", "a long braided mane with beads", null, "a long braided goat-like chin beard", null, "an open bronze helm with holes for the horns (face open)", null, "a red cloth sash with a leather shoulder strap", "a dark iron breastplate with a bull emblem"],
  },
  lizardfolk: {
    look: "LIZARDFOLK: reptile-headed people with a long scaly snout, small head frills or spines, slit-pupil eyes",
    heads: ["blue male with a white throat", "golden female with dark stripes", "black-green male with a big frill", "pale green female", "orange male with spots", "deep teal female", "grey old male with scars", "yellow-green young male", "purple-scaled female"],
    genders: ["m", "f", "m", "f", "m", "f", "m", "m", "f"],
    bald: "No clothing",
    parts: [null, null, null, null, null, "an open bronze crested helm shaped for a snout (face open)", null, "a woven reed harness with shell beads", "a bronze scale cuirass with a teal sash"],
  },
  beastkin: {
    look: "BEASTKIN: animal-headed people with fur, a short muzzle and big animal ears",
    heads: ["brown bear man", "grey rabbit woman", "black wolf man", "orange tabby cat woman", "spotted leopard man", "red panda woman", "white tiger man", "brown deer woman with small antlers", "grey raccoon man"],
    genders: ["m", "f", "m", "f", "m", "f", "m", "f", "m"],
    bald: "No clothing",
    parts: [null, null, null, null, null, "an open leather-and-bronze cap with ear holes (face open)", null, "a patched leather vest with a scarf", "a chainmail shirt with a fur collar"],
  },
};
const KINDS = ["hair_short", "hair_long", "hair_braids", "beard_full", "beard_short", "helmet_crest", "helmet_visor", "armor_harness", "armor_plate"];

const args = process.argv.slice(2);
const ji = args.indexOf("--jobs");
const jobs = ji >= 0 ? Number(args[ji + 1]) : 4;
const races = args.filter((a, i) => !a.startsWith("--") && args[i - 1] !== "--jobs");
const list = (arr, empty) => arr.map((x, i) => `${i + 1}) ${x ?? empty}`).join(", ");
mkdirSync("art/tmp", { recursive: true });

const tasks = [];
for (const race of races.length ? races : Object.keys(RACES)) {
  const R = RACES[race];
  const DIR = `art/source/portrait2/${race}`;
  const ref = `${DIR}/front_heads.png`;
  if (!R || !existsSync(ref)) { console.log(`건너뜀: ${race}`); continue; }
  tasks.push({ id: `${race}.heads2`, out: `${DIR}/front_heads_2.png`, ref,
    prompt: `Portrait PARTS for a modular character creator: ${R.look}. Nine NEW characters on EXACTLY the same bust template as the attached reference sheet: ${FRONT} IDENTICAL position, size and head outline as the reference in every cell. Vary only colors and small features/expression: ${list(R.heads)}. ${R.bald}, no helmet, so hair, beards and armor can be layered on top later.` });
  if (R.parts.some(Boolean)) {
    tasks.push({ id: `${race}.parts2`, out: `${DIR}/front_parts_2.png`, ref,
      prompt: `Overlay PARTS that fit EXACTLY on the bust template in the attached reference sheet (same cell size, same front view, same head position and size). In each cell draw ONLY the item, symmetric and front-facing, exactly where it sits on that template - do NOT draw the head, face, skin, ears, horns or neck (leave those areas magenta): ${list(R.parts, "leave this cell completely empty magenta")}.` });
  }
  // 두 번째 세트의 머리 성별과 파츠 이름 (portrait-parts.mjs가 읽는다)
  const cfgFile = `${DIR}/config.json`;
  const cfg = existsSync(cfgFile) ? JSON.parse(readFileSync(cfgFile, "utf8")) : {};
  cfg.extra = [{ heads: R.genders, front: R.parts.map((p, i) => (p ? `${KINDS[i]}_2` : null)) }];
  writeFileSync(cfgFile, JSON.stringify(cfg, null, 1));
}

function run(t) {
  const manifest = { presets: { sheet: SHEET }, style: STYLE, styleRef: t.ref, styleRefPresets: ["sheet"], items: [{ id: t.id, preset: "sheet", out: t.out, prompt: t.prompt }] };
  const mf = `art/tmp/gen_${t.id}.json`;
  writeFileSync(mf, JSON.stringify(manifest, null, 1));
  return new Promise((resolve) => {
    const child = spawn("node", ["tools/gen-images.mjs", "--manifest", mf, "--jobs", "1", "--force"], { stdio: "inherit" });
    child.on("close", () => resolve(existsSync(t.out)));
  });
}

let next = 0, failed = 0;
await Promise.all(Array.from({ length: Math.min(jobs, tasks.length) }, async () => {
  while (next < tasks.length) { const t = tasks[next++]; if (!(await run(t))) { failed++; console.log(`✗ ${t.id}`); } }
}));
console.log(`끝: ${tasks.length - failed}/${tasks.length}`);
process.exit(failed ? 1 : 0);
