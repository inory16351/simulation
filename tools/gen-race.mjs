// 한 종족의 그림 초상화 시트 넷(정면 머리·정면 파츠·측면 머리·측면 파츠)을 Codex로 차례로 만든다.
// 앞 단계 시트를 다음 단계의 기준 이미지로 넣어 같은 얼굴·같은 자리에 맞춘다.
// 사용: node tools/gen-race.mjs <종족> [--from 단계번호]   → art/source/portrait2/<종족>/*.png
import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, writeFileSync } from "node:fs";

const race = process.argv[2];
const fi = process.argv.indexOf("--from");
const from = fi > 0 ? Number(process.argv[fi + 1]) : 1;
const DIR = `art/source/portrait2/${race}`;
mkdirSync(DIR, { recursive: true });
mkdirSync("art/tmp", { recursive: true });

const STYLE = "Style: very simple cute chibi game sprite art in the spirit of RimWorld mod character portraits: a big round head, tiny simple facial features, large simple dark eyes with one white highlight, thin soft dark-brown outlines, clean simple shapes. Coloring like a gentle storybook gouache painting: soft flat base colors with one soft painted shade and a subtle brush texture, warm torchlight tint from the left, muted earthy palette. NOT realistic, NOT detailed. Match the attached reference sheet's style exactly. No text, no watermark.";
const SHEET = "ONE square image 1536x1536, a 3x3 grid of equal square cells, flat solid MAGENTA background (#FF00FF) filling every cell (must stay magenta, not transparent, not black), no gradient, no shadows on the background, no grid lines.";
const FRONT = "STRICTLY FRONT VIEW, perfectly symmetric, looking straight at the viewer, centered horizontally: the head top at 12% from the top of the cell and the chin at 66%, eyes at 46%, a short neck, small rounded shoulders reaching the bottom edge spanning 60% of the cell width.";
const SIDE = "STRICT SIDE PROFILE facing RIGHT, head upright, centered horizontally, head top at 12% of the cell and chin at 62%, only a short neck stub below ending at 72% (NO shoulders, NO body), one eye visible.";

/** 종족별 머리 9칸 설명(순서 = 성별 배열 순서)과 파츠 9칸 (null이면 빈칸) */
const RACES = {
  elf: {
    look: "fantasy ELVES with long pointed ears, slender faces and almond-shaped eyes",
    heads: ["pale silver-eyed young woman", "fair-skinned calm young man", "dusky-skinned elf woman", "pale stern man", "olive-skinned woman with freckles", "ash-grey-skinned man with a scar", "golden-tan woman", "very old elf man with white brows", "pale fierce woman"],
    genders: ["f", "m", "f", "m", "f", "m", "f", "m", "f"],
    parts: ["short swept silver-blond hair", "long straight black hair falling to the shoulders", "a long single braid with small leaves woven in", null, null, "an elegant elven half-helm with swept leaf-shaped wings (face open)", "a sleek closed elven helm with a narrow eye slit", "a leather shoulder harness with small leaf-shaped green-bronze pauldrons", "a fitted elven scale cuirass with a green sash"],
    bald: "All completely BALD (keep the eyebrows and the long ears)",
  },
  dwarf: {
    look: "stout fantasy DWARVES with wide faces, big round noses and bushy eyebrows",
    heads: ["ruddy grumpy man", "tanned cheerful man", "dark-skinned stern man", "pale freckled woman", "old man with white bushy brows", "weathered scarred man", "rosy-cheeked woman", "grizzled one-browed man", "olive-skinned determined woman"],
    genders: ["m", "m", "m", "f", "m", "m", "f", "m", "f"],
    parts: ["short spiky ginger hair", "long thick brown hair to the shoulders", "two thick red braids with metal rings", "a huge braided black beard with mustache reaching the chest", "a short bushy grey beard with mustache", "a round iron dwarven helm with a nose guard and two small horns (face open)", "a heavy closed dwarven helm with a grille", "a leather harness with heavy steel pauldrons", "a thick riveted steel breastplate with a rune belt"],
    bald: "All completely BALD and BEARDLESS (keep the bushy eyebrows)",
  },
  orc: {
    look: "fantasy ORCS with green or grey-green skin, small lower tusks, pointed ears and a heavy brow",
    heads: ["olive-green grinning brute", "dark-green scarred warrior", "grey-green stern man", "light-green fierce woman", "old mossy-green man with a broken tusk", "jade-green young man", "dark olive woman with war paint", "grey-skinned one-eyed brute", "bright-green proud woman"],
    genders: ["m", "m", "m", "f", "m", "m", "f", "m", "f"],
    parts: ["a black mohawk crest of hair", "long wild black hair to the shoulders", "a tall black topknot with braids", null, "a short pointed black goatee", "a spiked iron open helm (face open)", "a brutal iron full helm with tusk holes and a grille", "a crude leather harness with spiked bone pauldrons", "a rough riveted iron plate with a fur collar"],
    bald: "All completely BALD and beardless (keep the tusks and ears)",
  },
  minotaur: {
    look: "MINOTAURS: bull-headed people with short brown or black fur, a broad muzzle, curved horns and a brass nose ring on some",
    heads: ["brown bull with a nose ring", "black bull with long horns", "red-brown young cow-woman with small horns", "grey old bull with chipped horn", "tan bull with a scar", "dark brown bull with short horns", "white-and-brown cow-woman", "black bull with a gold nose ring", "light brown cow-woman"],
    genders: ["m", "m", "f", "m", "m", "m", "f", "m", "f"],
    parts: ["a shaggy forelock tuft of fur between the horns", "a long shaggy mane down the neck", null, "a shaggy bull chin beard", null, null, null, "a leather harness with bronze pauldrons", "a heavy bronze breastplate with a red sash"],
    bald: "No hair tufts and no beard (just the short fur, horns and ears), no clothing",
  },
  lizardfolk: {
    look: "LIZARDFOLK: reptile-headed people with a long scaly snout, small head frills or spines, slit-pupil eyes",
    heads: ["green-scaled male with a small crest", "teal female with a yellow throat", "brown desert male with spines", "blue-green female", "dark green old male with a scar", "sandy yellow male", "red-orange female with a frill", "grey-green male with a broken spine", "emerald female"],
    genders: ["m", "f", "m", "f", "m", "m", "f", "m", "f"],
    parts: [null, null, null, null, null, null, null, "a leather harness with bronze pauldrons", "a scale-mail cuirass with a red sash"],
    bald: "No clothing",
  },
  beastkin: {
    look: "BEASTKIN: animal-headed people with fur, a short muzzle and big animal ears",
    heads: ["grey wolf man", "red fox woman", "tabby cat man", "golden lion man with a small mane", "black panther woman", "brown jackal man", "white snow-fox woman", "striped tiger man", "spotted hyena woman"],
    genders: ["m", "f", "m", "m", "f", "m", "f", "m", "f"],
    parts: [null, null, null, null, null, null, null, "a leather harness with bronze pauldrons", "a bronze breastplate with a red sash"],
    bald: "No clothing",
  },
};

const R = RACES[race];
if (!R) { console.error("모르는 종족:", race); process.exit(1); }
const list = (arr, empty) => arr.map((x, i) => `${i + 1}) ${x ?? empty}`).join(", ");

const steps = [
  { out: `${DIR}/front_heads.png`, ref: "art/source/portrait2/human/front_heads.png",
    prompt: `Portrait PARTS for a modular character creator: ${R.look}. Every cell shows the SAME bust template, ${FRONT} IDENTICAL position, size and head outline in every cell. Vary only colors and tiny features/expression: ${list(R.heads)}. ${R.bald}, no helmet, so hair, beards and armor can be layered on top later. Keep the same proportions and framing as the attached human reference.` },
  { out: `${DIR}/front_parts.png`, ref: `${DIR}/front_heads.png`,
    prompt: `Overlay PARTS that fit EXACTLY on the bust template in the attached reference sheet (same cell size, same front view, same head position and size). In each cell draw ONLY the item, symmetric and front-facing, exactly where it sits on that template - do NOT draw the head, face, skin, ears, horns or neck (leave those areas magenta): ${list(R.parts, "leave this cell completely empty magenta")}.` },
  { out: `${DIR}/side_heads.png`, ref: `${DIR}/front_heads.png`,
    prompt: `Side-view versions of the SAME nine characters as the attached reference sheet, in the SAME cell order and style. Every cell uses the SAME template: ${SIDE} IDENTICAL position and size in every cell. ${R.bald}, no helmet.` },
  { out: `${DIR}/side_parts.png`, ref: `${DIR}/side_heads.png`,
    prompt: `Overlay PARTS that fit EXACTLY on the side-profile head template in the attached reference sheet (same cell size, STRICT SIDE PROFILE facing RIGHT, same head position and size). In each cell draw ONLY the item in side view, exactly where it sits on that head - do NOT draw the head, face, skin, ears, horns or neck: ${list(R.parts.slice(0, 7), "leave this cell completely empty magenta")}, 8) leave empty magenta, 9) leave empty magenta.` },
];

for (let i = from - 1; i < steps.length; i++) {
  const st = steps[i];
  // 파츠가 하나도 없는 종족은 측면 파츠를 건너뛴다
  if (i === 3 && R.parts.slice(0, 7).every((p) => !p)) { console.log("측면 파츠 없음 — 건너뜀"); continue; }
  const manifest = { presets: { sheet: SHEET }, style: STYLE, styleRef: st.ref, styleRefPresets: ["sheet"], items: [{ id: `${race}.${i + 1}`, preset: "sheet", out: st.out, prompt: st.prompt }] };
  const mf = `art/tmp/gen_${race}_${i + 1}.json`;
  writeFileSync(mf, JSON.stringify(manifest, null, 1));
  console.log(`[${race}] ${i + 1}/4 ${st.out}`);
  const r = spawnSync("node", ["tools/gen-images.mjs", "--manifest", mf, "--jobs", "1", "--force"], { stdio: "inherit", shell: true });
  if (r.status !== 0 || !existsSync(st.out)) { console.error(`[${race}] ${i + 1}단계 실패`); process.exit(1); }
}
// 이 종족의 머리 성별과 파츠 목록 (portrait-parts.mjs가 읽는다)
writeFileSync(`${DIR}/config.json`, JSON.stringify({
  heads: R.genders,
  front: R.parts.map((p, i) => (p ? ["hair_short", "hair_long", "hair_braids", "beard_full", "beard_short", "helmet_crest", "helmet_visor", "armor_harness", "armor_plate"][i] : null)),
  side: R.parts.slice(0, 7).map((p, i) => (p ? ["hair_short", "hair_long", "hair_braids", "beard_full", "beard_short", "helmet_crest", "helmet_visor"][i] : null)).concat([null, null]),
}, null, 1));
console.log(`[${race}] 끝`);
