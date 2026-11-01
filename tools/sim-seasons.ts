// UI 없이 대량 경기를 돌려 전투 밸런스 통계를 출력한다.
// 사용: npm run sim -- [경기 수]
import { STYLES } from "../src/core/data";
import { simulateMatch } from "../src/core/combat/sim";
import { generateGladiator } from "../src/core/generators/gladiator";
import { computeCA } from "../src/core/gladiator";
import { Rng } from "../src/core/rng";
import type { DeathRule, Gladiator } from "../src/core/types";

const N = Number(process.argv[2] ?? 4000);
const rng = new Rng(2026);
const pool: Gladiator[] = Array.from({ length: 400 }, (_, i) =>
  generateGladiator(rng, { id: `g${i}`, nation: rng.pick(["aurelia", "grommar", "sylvaren", "dolgar", "karsea", "kemer"] as const), target: rng.range(5, 15) }),
);

const styleStats: Record<string, { w: number; n: number }> = {};
const raceStats: Record<string, { w: number; n: number }> = {};
const reasons: Record<string, number> = { ko: 0, surrender: 0, timeout: 0 };
const deaths: Record<DeathRule, { d: number; n: number }> = { training: { d: 0, n: 0 }, missio: { d: 0, n: 0 }, sudden: { d: 0, n: 0 } };
let rounds = 0;
let upsets = 0, decided = 0;

for (let i = 0; i < N; i++) {
  const a = rng.pick(pool), b = rng.pick(pool);
  if (a === b) continue;
  const rule = rng.pick(["training", "missio", "sudden"] as const);
  const r = simulateMatch(a, b, { rule, seed: i });
  rounds += r.rounds;
  reasons[r.reason]++;
  deaths[rule].n++;
  if (r.died) deaths[rule].d++;
  for (const g of [a, b]) {
    const won = r.winnerId === g.id ? 1 : 0;
    (styleStats[g.style] ??= { w: 0, n: 0 }).n++;
    styleStats[g.style].w += won;
    (raceStats[g.race] ??= { w: 0, n: 0 }).n++;
    raceStats[g.race].w += won;
  }
  const caA = computeCA(a), caB = computeCA(b);
  if (Math.abs(caA - caB) >= 20) {
    decided++;
    const favored = caA > caB ? a.id : b.id;
    if (r.winnerId !== favored) upsets++;
  }
}

const pct = (x: number) => `${(x * 100).toFixed(1)}%`;
console.log(`경기 ${N}회, 평균 ${(rounds / N).toFixed(1)}라운드`);
console.log("종료 사유:", Object.fromEntries(Object.entries(reasons).map(([k, v]) => [k, pct(v / N)])));
console.log("사망률:", Object.fromEntries(Object.entries(deaths).map(([k, v]) => [k, pct(v.d / Math.max(1, v.n))])));
console.log(`CA 20 이상 차이 이변율: ${pct(upsets / Math.max(1, decided))}`);
console.log("\n스타일 승률");
for (const s of STYLES) {
  const st = styleStats[s.id];
  if (st) console.log(`  ${s.name.padEnd(8)} ${pct(st.w / st.n)} (${st.n})`);
}
console.log("\n종족 승률");
for (const [k, st] of Object.entries(raceStats)) console.log(`  ${k.padEnd(10)} ${pct(st.w / st.n)} (${st.n})`);
