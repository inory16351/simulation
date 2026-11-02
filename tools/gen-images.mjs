// tools/assets.json의 항목마다 Codex CLI(`codex exec`)로 이미지를 생성한다.
// 사용:
//   node tools/gen-images.mjs                  아직 없는 파일만 생성
//   node tools/gen-images.mjs --only portrait  id가 이 접두어로 시작하는 항목만
//   node tools/gen-images.mjs --force          이미 있어도 다시 생성
//   node tools/gen-images.mjs --jobs 4         동시에 실행할 codex 수 (기본 3)
//   node tools/gen-images.mjs --manifest tools/portrait-parts.json   다른 목록 사용
import { spawn } from "node:child_process";
import { existsSync, mkdirSync, readFileSync } from "node:fs";
import { dirname } from "node:path";

const args = process.argv.slice(2);
const flag = (name) => args.includes(name);
const opt = (name, def) => {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : def;
};

const manifest = JSON.parse(readFileSync(opt("--manifest", "tools/assets.json"), "utf8"));
const only = opt("--only", "");
const jobs = Number(opt("--jobs", "3"));

const todo = manifest.items.filter(
  (it) => it.id.startsWith(only) && (flag("--force") || !existsSync(it.out)),
);
console.log(`생성할 이미지 ${todo.length}개 (동시 ${jobs})`);

function buildPrompt(it) {
  const ext = it.out.endsWith(".jpg") ? "JPEG" : "PNG";
  return [
    "Use your built-in image generation tool to create ONE image, then save it to disk.",
    `Save it as a ${ext} file at exactly this path (relative to the current directory, create folders if needed): ${it.out}`,
    it.preset === "icon" || it.preset === "crest"
      ? "The background MUST be transparent (real alpha channel). Do not add any backdrop."
      : "",
    "Do not write any code, scripts, or other files.",
    "",
    `Image: ${manifest.presets[it.preset]}`,
    it.prompt.replace("{headRule}", manifest.headRule ?? ""),
    manifest.style,
    "",
    "When done, reply with only the saved file path.",
  ].join("\n");
}

function run(it) {
  return new Promise((resolve) => {
    mkdirSync(dirname(it.out), { recursive: true });
    const started = Date.now();
    // 프롬프트는 stdin으로 넘긴다 (Windows 셸 인용 문제 회피)
    // 화풍 통일: 지정된 프리셋은 전투 화풍 기준 이미지를 함께 첨부한다
    // 항목에 styleRef가 있으면 그것을 쓴다
    const ref = it.styleRef ?? manifest.styleRef;
    const useRef = ref && manifest.styleRefPresets?.includes(it.preset) && existsSync(ref);
    const cliArgs = ["exec", "-s", "workspace-write", ...(useRef ? ["-i", ref] : []), "-"];
    const child = spawn("codex", cliArgs, { shell: true });
    let log = "";
    child.stdout.on("data", (d) => (log += d));
    child.stderr.on("data", (d) => (log += d));
    child.stdin.end(buildPrompt(it));
    child.on("close", (code) => {
      const ok = existsSync(it.out);
      const sec = Math.round((Date.now() - started) / 1000);
      console.log(`${ok ? "✓" : "✗"} ${it.id} (${sec}s)${ok ? "" : ` exit ${code}\n${log.slice(-600)}`}`);
      resolve(ok);
    });
  });
}

let next = 0;
let failed = 0;
async function worker() {
  while (next < todo.length) {
    const it = todo[next++];
    if (!(await run(it))) failed++;
  }
}
await Promise.all(Array.from({ length: Math.min(jobs, todo.length) }, worker));
console.log(`완료: 성공 ${todo.length - failed}, 실패 ${failed}`);
process.exit(failed ? 1 : 0);
