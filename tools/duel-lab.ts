// 수 싸움 밸런스 실험: 능력치 차이별 이변 확률, 전투 감각·버릇이 승률을 얼마나 움직이는지 본다.
// 사용: npx tsx tools/duel-lab.ts [쌍마다 경기 수]
import { simulateMatch, type CombatEvent } from "../src/core/combat/sim";
import { generateGladiator } from "../src/core/generators/gladiator";
import { computeCA } from "../src/core/gladiator";
import { Rng } from "../src/core/rng";
import { SENSE, type AttrId, type Gladiator } from "../src/core/types";

const N = Number(process.argv[2] ?? 300);
const rng = new Rng(77);
const STYLES = ["murmillo", "secutor", "retiarius", "thraex", "hoplomachus", "dimachaerus", "crusher"] as const;

const gen = (id: string, target: number, style: string) => generateGladiator(rng, { id, nation: "aurelia", target, style: style as never, race: "human" });
const withAttrs = (g: Gladiator, mods: Partial<Record<AttrId, number>>): Gladiator => ({ ...g, attrs: { ...g.attrs, ...mods } });
const winRate = (a: Gladiator, b: Gladiator, n = N) => {
  let w = 0;
  for (let i = 0; i < n; i++) if (simulateMatch(a, b, { rule: "training", seed: i * 31 + 7, noDeath: true }).winnerId === a.id) w++;
  return w / n;
};
const pct = (x: number) => `${Math.round(x * 100)}%`;

// 1) 합 결과 분포와 경기 길이
const outcomes: Record<string, number> = {};
let rounds = 0, matches = 0;
const reasons: Record<string, number> = {};
for (let i = 0; i < 400; i++) {
  const a = gen(`a${i}`, rng.range(6, 14), rng.pick([...STYLES])), b = gen(`b${i}`, rng.range(6, 14), rng.pick([...STYLES]));
  const r = simulateMatch(a, b, { rule: "missio", seed: i, noDeath: true });
  rounds += r.rounds; matches++;
  reasons[r.reason] = (reasons[r.reason] ?? 0) + 1;
  for (const e of r.events as CombatEvent[]) if (e.t === "exchange") outcomes[e.outcome] = (outcomes[e.outcome] ?? 0) + 1;
}
const totalEx = Object.values(outcomes).reduce((x, y) => x + y, 0);
console.log("합 결과:", Object.entries(outcomes).map(([k, v]) => `${k} ${pct(v / totalEx)}`).join(", "));
console.log("평균 턴:", (rounds / matches).toFixed(1), "종료:", JSON.stringify(reasons));

// 2) 능력치 차이별 약자 승률 (같은 스타일, 여러 쌍 평균)
console.log("\n능력치 차이별 약자 승률 (CA 차이)");
for (const gap of [1, 2, 3, 4, 6]) {
  let sum = 0, caGap = 0;
  const pairs = 12;
  for (let p = 0; p < pairs; p++) {
    const style = STYLES[p % STYLES.length];
    const strong = gen(`s${gap}_${p}`, 10 + gap / 2, style), weak = gen(`w${gap}_${p}`, 10 - gap / 2, style);
    caGap += computeCA(strong) - computeCA(weak);
    sum += winRate(weak, strong, Math.round(N / 3));
  }
  console.log(`  목표 차 ${gap}: CA 차 평균 ${Math.round(caGap / pairs)} → 약자 승률 ${pct(sum / pairs)}`);
}

// 3) 전투 감각만 바꿨을 때 (나머지 능력치 동일)
console.log("\n전투 감각 효과 (같은 검투사, 감각만 다름)");
const base = gen("base", 10, "thraex");
const lowSense = Object.fromEntries(SENSE.map((a) => [a, 5]));
const highSense = Object.fromEntries(SENSE.map((a) => [a, 16]));
for (const a of ["read", "feint", "genius", "adapt", "timing", "kill", "aura"] as const) {
  const hi = withAttrs({ ...base, id: "hi" }, { ...lowSense, [a]: 18 });
  const lo = withAttrs({ ...base, id: "lo" }, lowSense);
  console.log(`  ${a} 18 vs 5: 승률 ${pct(winRate(hi, lo))}`);
}
const sharp = withAttrs({ ...base, id: "sharp" }, highSense);
const dull = withAttrs({ ...base, id: "dull" }, lowSense);
console.log(`  감각 전부 16 vs 5: ${pct(winRate(sharp, dull))}`);

// 4) 이변: 신체·기술이 약해도 감각이 높으면 강자를 잡을 수 있는가
console.log("\n약자(목표 8, 감각 17) vs 강자(목표 12, 감각 6)");
let up = 0;
for (let p = 0; p < 7; p++) {
  const style = STYLES[p];
  const weak = withAttrs(gen(`uw${p}`, 8, style), { ...Object.fromEntries(SENSE.map((a) => [a, 17])), dec: 15 });
  const strong = withAttrs(gen(`us${p}`, 12, style), { ...Object.fromEntries(SENSE.map((a) => [a, 6])), agg: 16 });
  const r = winRate(weak, strong, Math.round(N / 3));
  up += r;
  console.log(`  ${style}: CA ${computeCA(weak)} vs ${computeCA(strong)} → 약자 ${pct(r)}`);
}
console.log(`  평균 ${pct(up / 7)}`);

// 5) 버릇: 공격성이 높아 버릇이 뾰족하면 예측력 높은 상대에게 읽힌다
const reader = withAttrs({ ...base, id: "reader" }, { read: 17, adapt: 17 });
const bull = withAttrs({ ...base, id: "bull" }, { agg: 19 });
const calm = withAttrs({ ...base, id: "calm" }, { agg: 4 });
console.log(`\n예측력·적응력 17 vs 공격성 19(뾰족한 버릇): ${pct(winRate(reader, bull))}`);
console.log(`예측력·적응력 17 vs 공격성 4(고른 버릇): ${pct(winRate(reader, calm))}`);
