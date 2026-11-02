// Unity(C#) 이식 검증용 골든 데이터: 같은 시드로 TS가 낸 결과를 JSON으로 저장한다.
// C# EditMode 테스트(ColosseumUnity/Assets/Tests)가 이 파일과 결과를 비교한다.
// 사용: npx tsx tools/golden-dump.ts
import { writeFileSync, mkdirSync } from "node:fs";
import { simulateMatch } from "../src/core/combat/sim";
import { buildCommentary } from "../src/core/combat/commentary";
import { generateGladiator } from "../src/core/generators/gladiator";
import { Rng } from "../src/core/rng";

const out: Record<string, unknown> = {};

// 1) 난수열
const r = new Rng(12345);
out.rng = Array.from({ length: 20 }, () => r.next());
const r2 = new Rng(99);
out.rngNormal = Array.from({ length: 10 }, () => r2.normal(10, 2.2));

// 2) 검투사 생성
const gr = new Rng(7);
const gladiators = Array.from({ length: 12 }, (_, i) => generateGladiator(gr, { id: `g${i}`, nation: (["aurelia", "grommar", "sylvaren", "dolgar", "karsea", "kemer"] as const)[i % 6], target: 5 + (i % 10) }));
out.gladiators = gladiators;

// 3) 경기
out.matches = Array.from({ length: 12 }, (_, i) => {
  const a = gladiators[i], b = gladiators[(i * 5 + 3) % gladiators.length];
  if (a === b) return null;
  const rule = (["training", "missio", "sudden"] as const)[i % 3];
  const res = simulateMatch(a, b, { rule, seed: 1000 + i });
  return {
    a: a.id, b: b.id, rule, seed: 1000 + i,
    winnerId: res.winnerId, reason: res.reason, rounds: res.rounds, died: res.died, crowd: res.crowd,
    damageDealt: res.damageDealt, events: res.events.length,
    commentary: buildCommentary(res.events, { [a.id]: a, [b.id]: b }, 1000 + i).map((l) => l.text),
  };
}).filter(Boolean);

const dir = process.argv[2] ?? "ColosseumUnity/Assets/Tests/Golden";
mkdirSync(dir, { recursive: true });
writeFileSync(`${dir}/golden.json`, JSON.stringify(out, null, 1));
console.log("saved", `${dir}/golden.json`);
