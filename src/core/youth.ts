// 유소년 학당 (docs/07_youth.md): 입학, 훈련, 견습 투기, 1군 승격
import { absDay } from "./calendar";
import { logCareer, seasonRecord } from "./career";
import { simulateMatch } from "./combat/sim";
import { getNation, getRace } from "./data";
import { academyPaCap } from "./facilities";
import { generateGladiator } from "./generators/gladiator";
import { ageStage, computeCA } from "./gladiator";
import { josa } from "./josa";
import type { Rng } from "./rng";
import { pushMessage } from "./state";
import { grow } from "./training";
import type { GameState, Gladiator, Ludus, RaceId } from "./types";

export const youthCapacity = (ludus: Ludus) => (ludus.facilities.academy ? ludus.facilities.academy * 3 + 3 : 0);
export const APPRENTICE_INTERVAL = 28;

/** 시즌 시작(서리달 1일) 입학 */
export function intake(state: GameState, rng: Rng, ludus: Ludus): Gladiator[] {
  const lvl = ludus.facilities.academy;
  if (!lvl) return [];
  const room = youthCapacity(ludus) - ludus.youth.length;
  const count = Math.min(room, lvl + 1);
  if (count <= 0) return [];
  // 황금 세대: 드물게 그해 입학생 전원의 그릇이 크다
  const golden = rng.chance(0.05);
  const used = new Set(Object.values(state.gladiators).map((g) => g.name));
  const out: Gladiator[] = [];
  for (let i = 0; i < count; i++) {
    const race = rng.weighted<RaceId>(getNation(ludus.nation).races);
    const r = getRace(race).age;
    const g = generateGladiator(rng, {
      id: `G${state.nextId++}`, nation: ludus.nation, race, status: "free",
      target: rng.range(2.5, 4.5), age: r.debut - rng.int(1, 4), paCap: academyPaCap(lvl), usedNames: used,
    });
    if (golden) g.hidden.pa = Math.min(200, g.hidden.pa + rng.int(20, 40));
    g.wage = rng.int(2, 5);
    g.training = { focus: "physical", intensity: "normal" };
    state.gladiators[g.id] = g;
    ludus.youth.push(g.id);
    seasonRecord(state, g);
    logCareer(state, g, `${ludus.name} 유소년 학당에 들어왔습니다 (${g.age}세)`, "join");
    out.push(g);
  }
  if (ludus.isPlayer && out.length) {
    pushMessage(state, {
      kind: "info",
      title: golden ? "황금 세대가 입학했습니다!" : `유소년 학당 입학생 ${out.length}명`,
      body: `${out.map((g) => `${g.name} (${getRace(g.race).name}, ${g.age}세)`).join("\n")}\n\n${golden ? "교관들이 술렁입니다. 올해 아이들은 그릇이 다릅니다." : "유소년 학당 화면에서 볼 수 있습니다."}`,
    });
  }
  return out;
}

/** 매일: 유소년 훈련 (학당 단계가 훈련 시설 역할) */
export function dailyYouth(state: GameState, rng: Rng, ludus: Ludus): void {
  for (const id of ludus.youth) {
    const g = state.gladiators[id];
    if (!g?.alive || g.training.focus === "rest") continue;
    const intensity = g.training.intensity === "high" ? 1.3 : g.training.intensity === "low" ? 0.7 : 1;
    grow(g, rng, g.training.focus === "doctrine" ? "mental" : g.training.focus, 1, intensity, ludus.facilities.academy);
  }
}

/** 28일마다: 견습 투기. 목검 시합이라 죽지 않고, 실전 경험과 소문이 남는다 */
export function apprenticeBouts(state: GameState, rng: Rng, ludus: Ludus): void {
  const kids = ludus.youth.map((id) => state.gladiators[id]).filter((g) => g?.alive);
  if (!kids.length) return;
  const lines: string[] = [];
  let wins = 0;
  for (const g of kids) {
    const r = getRace(g.race).age;
    const opp = generateGladiator(rng, { id: `tmp`, nation: ludus.nation, target: rng.range(2.5, 5.5), age: r.debut - 2, status: "free" });
    opp.name = `${rng.pick(["옆 학당", "이웃 학당", "도시 학당"])}의 ${opp.name}`;
    const res = simulateMatch(g, opp, { rule: "training", seed: rng.int(0, 2 ** 31 - 1), noDeath: true });
    const won = res.winnerId === g.id;
    if (won) wins++;
    grow(g, rng, "match", won ? 8 : 6, 1, ludus.facilities.academy);
    g.popularity = Math.min(100, g.popularity + (won ? 2 : 0));
    const rec = seasonRecord(state, g);
    rec.apps++;
    if (won) rec.wins++; else rec.losses++;
    lines.push(`${g.name}: ${won ? "승리" : "패배"}`);
  }
  // 잘하는 유망주는 소문이 난다
  const star = kids.slice().sort((a, b) => computeCA(b) - computeCA(a))[0];
  if (ludus.isPlayer) {
    pushMessage(state, {
      kind: "match",
      title: `견습 투기 ${wins}승 ${kids.length - wins}패`,
      body: `${lines.join("\n")}\n\n가장 눈에 띈 아이: ${star.name}. ${star.popularity >= 15 ? "다른 루두스에서도 이름을 묻기 시작했습니다." : ""}`,
    });
  }
}

/** 데뷔 나이가 된 유소년을 1군으로 올린다 */
export function promoteYouth(state: GameState, ludus: Ludus, id: string): string | null {
  const g = state.gladiators[id];
  if (ageStage(g) === "youth") return "아직 어립니다";
  ludus.youth = ludus.youth.filter((x) => x !== id);
  ludus.gladiators.push(id);
  g.wage = Math.max(8, Math.round(computeCA(g) * 0.5));
  // 학당에서 자란 아이는 루두스에 대한 정이 깊다
  g.hidden.loyalty = Math.min(20, g.hidden.loyalty + 3);
  if (!g.traits.includes("academy")) g.traits.push("academy");
  logCareer(state, g, `${ludus.name} 1군으로 올라갔습니다`, "milestone");
  return null;
}

export function releaseYouth(state: GameState, ludus: Ludus, id: string): void {
  ludus.youth = ludus.youth.filter((x) => x !== id);
  const g = state.gladiators[id];
  logCareer(state, g, `${ludus.name} 학당에서 쫓겨났습니다`, "other");
}

/** 시즌 끝: 데뷔 나이가 된 아이들을 알린다 (AI는 자동 승격) */
export function seasonEndYouth(state: GameState, ludus: Ludus): void {
  const ready = ludus.youth.map((id) => state.gladiators[id]).filter((g) => g && ageStage(g) !== "youth");
  if (!ready.length) return;
  if (!ludus.isPlayer) {
    for (const g of ready) promoteYouth(state, ludus, g.id);
    return;
  }
  pushMessage(state, {
    kind: "info",
    title: `졸업할 나이가 된 아이 ${ready.length}명`,
    body: `${ready.map((g) => `${josa(g.name, "이/가")} 데뷔할 나이가 됐습니다`).join("\n")}\n\n유소년 학당 화면에서 1군으로 올리거나 내보내십시오.`,
  });
}

export const isApprenticeDay = (state: GameState) => absDay(state.season, state.day) % APPRENTICE_INTERVAL === 0;
