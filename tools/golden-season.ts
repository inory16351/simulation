// Unity(C#) 이식 검증용: 새 게임과 자동 진행 상태를 JSON으로 저장한다 (season.test.ts의 autoplay와 같은 결정).
// C# 테스트(ColosseumUnity/Assets/Tests/EditMode/SeasonGoldenTests.cs)가 같은 진행을 하고 상태를 비교한다.
// 사용: npx tsx tools/golden-season.ts
import { mkdirSync, writeFileSync } from "node:fs";
import { createChallenge, playChallenge, respond, todaysPlayerMatches } from "../src/core/challenge";
import { playTie, todaysPlayerTie } from "../src/core/cup";
import { advanceDay } from "../src/core/day";
import { getEvent, resolveEvent } from "../src/core/events";
import { newGame } from "../src/core/generators/world";
import { Rng } from "../src/core/rng";
import type { GameState } from "../src/core/types";

const dir = process.argv[2] ?? "ColosseumUnity/Assets/Tests/Golden";
mkdirSync(dir, { recursive: true });

function autoplayDay(state: GameState, rng: Rng) {
  for (const p of [...state.pendingEvents]) {
    const def = getEvent(p.eventId);
    const i = def.choices.findIndex((c) => !c.cost || state.ludi[state.playerLudusId].treasury >= c.cost);
    resolveEvent(state, rng, p.id, i);
  }
  for (const c of state.challenges.filter((x) => x.status === "pending" && state.ludi[x.to].isPlayer)) respond(state, c, true, rng);
  for (const c of todaysPlayerMatches(state)) playChallenge(state, c, rng);
  const tie = todaysPlayerTie(state);
  if (tie) playTie(state, tie.id, rng);
  if (state.day % 4 === 0) {
    const me = state.ludi[state.playerLudusId];
    const target = Object.values(state.ludi).find((l) => !l.isPlayer && l.tier === me.tier);
    const g = me.gladiators.map((id) => state.gladiators[id]).find((x) => x.alive);
    if (target && g) createChallenge(state, me, target, g.id, "training");
  }
  return advanceDay(state);
}

const s = newGame(2024);
writeFileSync(`${dir}/world_day0.json`, JSON.stringify(s));
const rng = new Rng(4242);
const blockedLog: string[][] = [];
for (let i = 1; i <= 170 && !s.gameOver; i++) {
  blockedLog.push(autoplayDay(s, rng));
  if (i === 30) writeFileSync(`${dir}/world_day30.json`, JSON.stringify(s));
}
writeFileSync(`${dir}/world_day170.json`, JSON.stringify(s));
writeFileSync(`${dir}/world_blocked.json`, JSON.stringify(blockedLog));
console.log("saved", s.season, s.day, Object.keys(s.gladiators).length);
