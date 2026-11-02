// 점 지도에서 읽은 기준점을 저장한다: art/source/portrait2/<종족>/anchors/<시트>.json
// 사용: node tools/save-anchors.mjs <종족>/<시트> "<칸1>|<칸2>|...|<칸9>"
//   칸 = 공백으로 나눈 "x,y" 목록 (점이 없으면 '-'), 순서는 아래 KEYS. 좌표는 칸 512 기준.
import { mkdirSync, writeFileSync } from "node:fs";

const HEAD_KEYS = ["crown", "brow", "eyeL", "eyeR", "nose", "mouth", "chin", "neck"];
const PART_KEYS = ["crown", "brow", "nose", "mouth", "chin", "neck"];

const [target, packed] = process.argv.slice(2);
const keys = target.includes("heads") ? HEAD_KEYS : PART_KEYS;
const cells = packed.split("|").filter((s) => s !== "").map((cell) => {
  const o = {};
  cell.trim().split(/\s+/).forEach((v, i) => { if (v !== "-") o[keys[i]] = v.split(",").map(Number); });
  return o;
});
if (cells.length !== 9) throw new Error(`칸이 9개가 아니다: ${cells.length}`);
const [race, sheet] = target.split("/");
mkdirSync(`art/source/portrait2/${race}/anchors`, { recursive: true });
writeFileSync(`art/source/portrait2/${race}/anchors/${sheet}.json`, JSON.stringify({ keys, cells }, null, 1));
console.log("saved", target, cells.map((c) => Object.keys(c).length).join(","));
