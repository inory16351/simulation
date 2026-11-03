// 다크 그래픽노블 그림체 초상화 시트 프롬프트와 종족 config.json. ChatGPT(Chrome)나 Codex로 생성할 때 쓴다.
// 사용:
//   node tools/gen-race-dark.mjs prompt <종족> heads|parts   프롬프트를 찍는다 (ChatGPT에 붙여 넣기)
//   node tools/gen-race-dark.mjs config <종족>               art/source/portrait2/<종족>/config.json을 새 시트에 맞게 쓴다
// 시트: front_heads.png(대머리 9), front_parts.png(아래 parts 순서), front_parts_3.png(흉터·문신, 사람 것을 복사)
// 기준 그림: art/source/portrait2/human/front_heads.png(그림체·칸 배치·머리 크기). 파츠는 같은 대화에서 그 종족 머리를 만든 뒤 "그 머리에 맞춰" 요청한다.
// 얼굴은 모두 20~35세 젊은 어른 (늙은 얼굴은 만들지 않는다)
import { existsSync, readFileSync, writeFileSync } from "node:fs";

const STYLE = "Art style: simplified dark-fantasy graphic novel with gouache brush texture, exactly like the attached human sheet: bold black ink outlines, flat blocky black shadow shapes, 2-3 flat tones per material with loose brush strokes, desaturated bone/rust/bronze/grey palette, warm torchlight rim from the left, grim young adult faces (aged 20-35, NO wrinkles, NO grey hair, NOT old), NOT chibi, NOT cute.";
const SHEET = "ONE square image, a 3x3 grid of equal square cells, flat solid MAGENTA background (#FF00FF) in every cell (must stay magenta, no gradient, no frames, no grid lines). No text.";
const FRONT = "IDENTICAL bust template to the attached human sheet in every cell: strictly front view, perfectly symmetric, centered, head top at 14% of the cell height, eyes at 38%, chin at 60%, thick neck, bare shoulders reaching the bottom edge spanning 80% of the cell width, same head size as the human sheet.";
const ITEM = "Draw ONLY the item itself, exactly where it would sit on those busts, strictly front view, centered, symmetric: do NOT draw any head, face, ears, horns, tusks, snout, skin or neck; every area that would be skin stays flat magenta (face openings, around beards, above armor collars). A closed helm must cover the whole head including the ears.";

/** parts: [파츠 이름, 그림 설명] 9칸 (null = 빈 칸). 이름은 종류_번호면 같은 종류의 변형으로 무작위에 섞인다 */
const RACES = {
  elf: {
    look: "fantasy ELVES with long pointed ears, slender sharp faces and high cheekbones",
    heads: ["copper-skinned elf woman", "very pale young elf man", "dark-skinned elf man with white eyebrows", "fair elf woman with a hard stare", "grey-blue-skinned stern elf woman", "golden-tan elf man", "olive-skinned elf woman", "light-brown young elf man", "pale elf man with a split lip"],
    genders: ["f", "m", "m", "f", "f", "m", "f", "m", "m"],
    bald: "All completely BALD and beardless (keep the eyebrows and the long ears)",
    // 5칸(정수리에서 늘어진 한 가닥 땋은 머리)은 얼굴 가운데를 덮어 뺐다
    parts: [["hair_short", "short tousled copper-red hair"], ["hair_long", "very long straight white hair falling past the shoulders, with gaps where the ears poke through"], ["hair_braids", "twin side braids of black hair"], ["hair_long_2", "long silver hair tied in a high ponytail"], null, ["helmet_crest", "a tarnished silver open half-helm with a crescent crest (face open, ear slots)"], ["helmet_visor", "a closed elven war helm that fully covers the head and both long ears with swept-back ear guards, narrow eye slit, down to the jaw"], ["armor_harness", "a dark green hooded mantle over a leather vest (hood down)"], ["armor_plate", "a tarnished silver-green lamellar cuirass with pauldrons"]],
  },
  dwarf: {
    look: "stout fantasy DWARVES with wide faces, big round noses, heavy brows and thick necks",
    heads: ["ruddy dwarf woman with a fierce grin", "pale young dwarf man", "dark-skinned dwarf man", "tanned dwarf man with a burn mark", "grey-skinned stern dwarf woman", "rosy young dwarf man", "olive-skinned one-eyed dwarf man", "freckled dwarf woman", "deep-brown scowling dwarf man"],
    genders: ["f", "m", "m", "m", "f", "m", "m", "f", "m"],
    bald: "All completely BALD and BEARDLESS (keep the bushy eyebrows)",
    parts: [["hair_short", "a short flat-top brown haircut"], ["hair_long", "long wild copper hair to the shoulders"], ["hair_braids", "a crown of thick blond braids"], ["beard_full", "a huge forked ginger beard with braided tips reaching the chest, with mustache"], ["beard_short", "a short black beard with a big curled mustache"], ["helmet_crest", "an open bronze dwarf helm with a crest ridge (face open, no nose guard)"], ["helmet_visor", "a closed iron dwarf helm shaped like a stern face mask covering the whole head and jaw"], ["armor_harness", "a leather apron harness with a heavy fur collar"], ["armor_plate", "a heavy gold-trimmed dwarven plate armor with a big round gorget"]],
  },
  orc: {
    look: "fantasy ORCS with green or grey-green skin, small lower tusks, pointed ears and a heavy brow",
    heads: ["dark-green orc woman with a nose ring", "olive young orc man with big tusks", "grey-green orc man with a torn ear", "pale-green orc man baring tusks", "deep-green fierce orc woman", "brown-green huge-jawed orc man", "moss-green calm orc man", "light grey-green orc woman", "dark olive one-tusked orc man"],
    genders: ["f", "m", "m", "m", "f", "m", "m", "f", "m"],
    bald: "All completely BALD and beardless (keep the tusks and ears)",
    parts: [["hair_short", "shaved sides with a red crest of hair along the top"], ["hair_long", "long braided black hair to the shoulders"], ["hair_braids", "many thin black dreadlocks tied back"], ["beard_full", "a long braided black beard with a mustache (lips and tusks left open)"], ["beard_short", "a scruffy short brown beard with mustache (tusks left open)"], ["helmet_crest", "a horned bone-and-iron open helm (face open)"], ["helmet_visor", "a closed iron bucket helm with a fanged grille that covers the whole head, ears and jaw"], ["armor_harness", "a fur mantle with leather straps and bone beads"], ["armor_plate", "a heavy black iron plate armor with spiked pauldrons"]],
  },
  minotaur: {
    look: "MINOTAURS: bull-headed people with short brown or black fur, a broad muzzle with a nose ring hole, curved horns",
    heads: ["white bull with black spots", "golden-brown young bull", "dark grey cow-woman with a gold ring", "black bull with wide horns", "red bull with a torn ear", "cream cow-woman with spots", "brown bull with forward horns", "dark brown cow-woman", "silver-grey young bull with a nose ring"],
    genders: ["m", "m", "f", "m", "m", "f", "m", "f", "m"],
    bald: "No hair tufts and no beard (just the short fur, horns and ears), no clothing",
    parts: [["hair_short", "a curly forelock tuft of dark fur between the horns"], ["hair_long", "a long braided mane with beads hanging down both sides of the neck"], ["hair_short_2", "a shaggy black forelock"], ["beard_full", "a long braided goat-like chin beard under the muzzle"], ["beard_full_2", "a thick shaggy dewlap beard under the muzzle"], ["helmet_crest", "an open bronze helm with holes for the horns (face open)"], ["helmet_visor", "a battered iron half-mask helm covering the brow and top of the muzzle with horn holes"], ["armor_harness", "a red cloth sash with a leather shoulder strap"], ["armor_plate", "a dark iron breastplate with a bull emblem and pauldrons"]],
    // 주둥이에 안 맞는 흉터·문신
    noMarks: ["mark_scar_lip", "mark_tattoo_cheeks", "mark_tattoo_chin"],
  },
  lizardfolk: {
    look: "LIZARDFOLK: reptile-headed people with a long scaly snout, small head frills or spines, slit-pupil eyes",
    heads: ["blue male with a white throat", "golden female with dark stripes", "black-green male with a big frill", "pale green female", "orange male with spots", "deep teal female", "grey male with scars", "yellow-green young male", "purple-scaled female"],
    genders: ["m", "f", "m", "f", "m", "f", "m", "m", "f"],
    bald: "No hair, no clothing",
    parts: [["helmet_crest", "a bronze crested open helm shaped for a snout (face and snout open)"], ["helmet_crest_2", "a leather war cap with bone spikes (face open)"], ["helmet_visor", "a tarnished iron half-helm covering the brow ridge with eye holes"], ["armor_harness", "a woven reed harness with shell beads"], ["armor_plate", "a bronze scale cuirass with a teal sash"], ["armor_harness_2", "a leather shoulder guard and chest straps with teeth trophies"], ["armor_harness_3", "a beaded bone necklace with a feather collar"], null, null],
    noMarks: ["mark_scar_lip", "mark_tattoo_cheeks", "mark_tattoo_chin"],
  },
  beastkin: {
    look: "BEASTKIN: animal-headed people with fur, a short muzzle and big animal ears",
    heads: ["brown bear man", "grey rabbit woman", "black wolf man", "orange tabby cat woman", "spotted leopard man", "red panda woman", "white tiger man", "brown deer woman with small antlers", "grey raccoon man"],
    genders: ["m", "f", "m", "f", "m", "f", "m", "f", "m"],
    bald: "No clothing",
    parts: [["helmet_crest", "an open leather-and-bronze cap with ear holes (face open)"], ["helmet_crest_2", "a dented iron skullcap with ear slots and a nose bar (face open)"], ["helmet_crest_3", "a cloth head wrap tied at the back with the ears free (face open)"], ["armor_harness", "a patched leather vest with a scarf"], ["armor_plate", "a chainmail shirt with a fur collar"], ["armor_plate_2", "a bronze breastplate with leather pauldrons"], ["armor_harness_2", "a rope-and-bone harness across the chest"], null, null],
    noMarks: ["mark_scar_lip", "mark_tattoo_cheeks", "mark_tattoo_chin"],
  },
};

const list = (arr, empty) => arr.map((x, i) => `${i + 1}) ${x ?? empty}`).join("; ");
const EMPTY = "leave this cell completely empty magenta";

export function prompt(race, sheet) {
  const R = RACES[race];
  if (sheet === "heads") return `Create an image. ${SHEET} ${STYLE} Portrait PARTS for a modular character creator: ${R.look}. ${FRONT} Nine different young adult gladiators: ${list(R.heads)}. ${R.bald}, no helmet, so hair, beards and armor can be layered on top later.`;
  if (sheet === "parts") return `Great. Now the matching overlay parts for THESE ${race} heads you just made (fit them exactly to those heads: same head size and position). ${SHEET} Same art style. ${ITEM} Cells: ${list(R.parts.map((p) => p?.[1] ?? null), EMPTY)}.`;
  throw new Error("시트 이름: heads|parts");
}

/** 사람 흉터·문신 시트(front_parts_3)를 같이 쓴다 (front_parts_2 자리는 비워 둔다) */
const MARKS = ["mark_scar_brow", "mark_scar_nose", "mark_scar_lip", "mark_scar_forehead", "mark_scar_claw", "mark_tattoo_forehead", "mark_tattoo_cheeks", "mark_tattoo_chin", "mark_paint_eyes"];

export function config(race) {
  const R = RACES[race];
  const file = `art/source/portrait2/${race}/config.json`;
  const old = existsSync(file) ? JSON.parse(readFileSync(file, "utf8")) : {};
  return { heads: R.genders, front: R.parts.map((p) => p?.[0] ?? null), side: old.side, extra: [{ heads: [], front: [] }, { heads: [], front: MARKS.map((m) => (R.noMarks?.includes(m) ? null : m)) }] };
}

const [cmd, race, sheet] = process.argv.slice(2);
if (cmd === "prompt") console.log(prompt(race, sheet));
else if (cmd === "config") { writeFileSync(`art/source/portrait2/${race}/config.json`, JSON.stringify(config(race), null, 1)); console.log("saved", race); }
