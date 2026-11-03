import sharp from "sharp";
import { readdirSync } from "node:fs";
const A = "ColosseumUnity/Assets/Resources/Art";
const out = process.argv[2];
const groups = [
  ["portrait", ["portrait2/human/front/head_1.png","portrait2/orc/front/head_2.png","portrait2/human/front/armor_plate.png"]],
  ["items", readdirSync(`${A}/icon/items`).filter(f=>f.endsWith(".png")).slice(0,12).map(f=>`icon/items/${f}`)],
  ["menu", readdirSync(`${A}/icon/menu`).filter(f=>f.endsWith(".png")).map(f=>`icon/menu/${f}`)],
  ["crest", readdirSync(`${A}/crest`).filter(f=>f.endsWith(".png")).map(f=>`crest/${f}`)],
  ["fx", readdirSync(`${A}/fx`).filter(f=>f.endsWith(".png")).map(f=>`fx/${f}`)],
  ["rig", readdirSync(`${A}/rig`).filter(f=>f.endsWith(".png")).slice(0,16).map(f=>`rig/${f}`)],
];
const S = 128, cols = 16, comps = [];
let y = 0;
for (const [, files] of groups) {
  files.forEach((f, i) => {
    comps.push({ f, left: (i % cols) * S, top: y + Math.floor(i / cols) * S });
  });
  y += Math.ceil(files.length / cols) * S + 8;
}
const tiles = await Promise.all(comps.map(async c => ({ input: await sharp(`${A}/${c.f}`).resize(S, S, { fit: "contain", background: { r: 0, g: 0, b: 0, alpha: 0 } }).png().toBuffer(), left: c.left, top: c.top })));
await sharp({ create: { width: cols * S, height: y, channels: 4, background: "#2a211b" } }).composite(tiles).png().toFile(out);
console.log(out);
