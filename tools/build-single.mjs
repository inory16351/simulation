// vite build 결과(dist)를 파일 하나짜리 HTML로 묶는다. 이미지는 data URI로 넣는다.
// 사용: npm run build:single  →  release/colosseum-league.html
import { mkdirSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const dist = "dist";
const outDir = "release";
const html = readFileSync(join(dist, "index.html"), "utf8");

// public/assets 아래 이미지를 경로 → data URI로
const assets = {};
const walk = (dir, prefix = "") => {
  for (const e of readdirSync(dir, { withFileTypes: true })) {
    const p = join(dir, e.name);
    const rel = prefix + e.name;
    if (e.isDirectory()) walk(p, rel + "/");
    else if (/\.(webp|png|jpe?g|svg)$/.test(e.name)) {
      const mime = { webp: "image/webp", png: "image/png", jpg: "image/jpeg", jpeg: "image/jpeg", svg: "image/svg+xml" }[e.name.split(".").pop()];
      assets[rel] = `data:${mime};base64,${readFileSync(p).toString("base64")}`;
    }
  }
};
walk(join(dist, "assets"));
// 번들된 js/css는 이미지가 아니므로 빠진다. ui/ 아래만 남는다
const used = Object.fromEntries(Object.entries(assets).filter(([k]) => !k.startsWith("index-")));

const inlineCss = (css) => css.replace(/url\("?\/assets\/([^")]+)"?\)/g, (m, p) => (used[p] ? `url("${used[p]}")` : m));
const safeScript = (js) => js.replace(/<\/script/gi, "<\\/script");

let out = html.replace(/<link rel="stylesheet"[^>]*href="\/assets\/([^"]+)"[^>]*>/, (_, f) =>
  `<style>${inlineCss(readFileSync(join(dist, "assets", f), "utf8"))}</style>`);
out = out.replace(/<script type="module"[^>]*src="\/assets\/([^"]+)"[^>]*><\/script>/, (_, f) =>
  `<script>window.__INLINE_ASSETS__=${JSON.stringify(used)};</script>\n<script type="module">${safeScript(readFileSync(join(dist, "assets", f), "utf8"))}</script>`);
if (/src="\/assets\/|href="\/assets\//.test(out)) throw new Error("인라인하지 못한 자원이 남았다");

mkdirSync(outDir, { recursive: true });
const file = join(outDir, "colosseum-league.html");
writeFileSync(file, out);
console.log(`${file} (${(out.length / 1024).toFixed(0)} KB, 이미지 ${Object.keys(used).length}개 포함)`);
