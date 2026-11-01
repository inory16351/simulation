// 메뉴 아이콘과 국가 문장 미리보기 PNG
import { renderToStaticMarkup } from "react-dom/server";
import sharp from "sharp";
import { NATIONS } from "../src/core/data";
import { Crest, MenuIcon } from "../src/ui/components/Icons";

const ids = ["inbox", "overview", "roster", "training", "challenge", "match", "league", "market", "staff", "facilities", "finance", "world", "hall"];
const S = 96;
const tiles = [
  ...ids.map((id, i) => ({ svg: renderToStaticMarkup(<MenuIcon id={id} size={S} />), left: i * (S + 8), top: 0 })),
  ...NATIONS.map((n, i) => ({ svg: renderToStaticMarkup(<Crest nation={n.id} color={n.color} size={S} />), left: i * (S + 8), top: S + 12 })),
].map((t) => ({ input: Buffer.from(t.svg.replace("<svg ", '<svg xmlns="http://www.w3.org/2000/svg" ')), left: t.left, top: t.top }));
await sharp({ create: { width: ids.length * (S + 8), height: 2 * S + 14, channels: 4, background: "#2a231e" } }).composite(tiles).png().toFile(process.argv[2]);
console.log("saved");
