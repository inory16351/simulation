// 메뉴 아이콘과 국가 문장 미리보기 PNG (public/assets의 그림을 한 장에 모은다)
import sharp from "sharp";
import { NATIONS } from "../src/core/data";

const ids = ["inbox", "overview", "roster", "training", "challenge", "match", "league", "market", "youth", "staff", "facilities", "finance", "cup", "world", "hall"];
const S = 96;
const tile = async (file: string, left: number, top: number) => ({ input: await sharp(file).resize(S, S, { fit: "contain", background: "#0000" }).png().toBuffer(), left, top });
const tiles = [
  ...(await Promise.all(ids.map((id, i) => tile(`public/assets/icon/menu/${id}.webp`, i * (S + 8), 0)))),
  ...(await Promise.all(NATIONS.map((n, i) => tile(`public/assets/crest/${n.id}.webp`, i * (S + 8), S + 12)))),
];
await sharp({ create: { width: ids.length * (S + 8), height: 2 * S + 14, channels: 4, background: "#2a231e" } }).composite(tiles).png().toFile(process.argv[2]);
console.log("saved");
