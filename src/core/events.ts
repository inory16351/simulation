// 랜덤 이벤트 (docs/11_events.md). 정의는 data/events.json, 효과 타입만 코드로 구현한다.
import eventsJson from "../../data/events.json";
import { absDay } from "./calendar";
import { BALANCE } from "./data";
import { addMoney, refreshFreeAgents } from "./economy";
import { ageStage } from "./gladiator";
import { josa } from "./josa";
import { logCareer } from "./career";
import type { Rng } from "./rng";
import { pushMessage } from "./state";
import { grow } from "./training";
import type { GameState, Gladiator, PendingEvent } from "./types";

type Grade = "luck" | "opportunity" | "trial" | "disaster";

interface Effect {
  type: string;
  value?: number;
  min?: number;
  max?: number;
  target?: "random" | "self" | "same" | "youngest" | "lowestMorale";
  attr?: string;
  days?: number;
  desc?: string;
  serious?: boolean;
  chance?: number;
  cause?: string;
  win?: number;
  lose?: number;
  kind?: "ticket" | "food" | "market";
  mult?: number;
}

interface EventDef {
  id: string;
  scope: "ludus" | "gladiator" | "world";
  grade: Grade;
  weight: number;
  condition?: "lowMorale" | "growthStage" | "popular" | "lowDiscipline";
  title: string;
  text: string;
  choices: { label: string; cost?: number; effects: Effect[] }[];
}

export const EVENTS = eventsJson as EventDef[];
const byId = Object.fromEntries(EVENTS.map((e) => [e.id, e]));
export const getEvent = (id: string) => byId[id];

const EV = BALANCE.events;

function roster(state: GameState): Gladiator[] {
  return state.ludi[state.playerLudusId].gladiators.map((id) => state.gladiators[id]).filter((g) => g.alive);
}

function conditionMet(state: GameState, def: EventDef, g?: Gladiator): boolean {
  const r = roster(state);
  switch (def.condition) {
    case "lowMorale": return r.length > 0 && r.reduce((s, x) => s + x.morale, 0) / r.length < 40;
    case "growthStage": return !!g && ageStage(g) === "growth";
    case "popular": return !!g && g.popularity >= 45;
    case "lowDiscipline": return !!g && g.attrs.disc <= 10;
    default: return true;
  }
}

function fill(text: string, g?: Gladiator): string {
  if (!g) return text;
  return text.replace("{name}이(가)", josa(g.name, "이/가")).replace(/\{name\}/g, g.name);
}

function pickWeighted(state: GameState, rng: Rng, defs: EventDef[]): EventDef | undefined {
  if (!defs.length) return undefined;
  // 운 보정: 악재가 이어지면 행운이, 행운이 이어지면 시련이 조금 더 잘 나온다
  const s = state.luckStreak;
  const w: Record<string, number> = {};
  for (const d of defs) {
    let m = 1;
    if (d.grade === "luck") m = Math.min(2, 1 + Math.max(0, s) * 0.25);
    if (d.grade === "trial" || d.grade === "disaster") m = Math.min(2, 1 + Math.max(0, -s) * 0.25);
    w[d.id] = d.weight * m;
  }
  return byId[rng.weighted(w)];
}

/** 매일 아침 이벤트 판정 */
export function rollDailyEvents(state: GameState, rng: Rng): void {
  const now = absDay(state.season, state.day);
  const disasterOk = now - state.lastDisasterAt >= EV.disasterCooldownDays;
  const allowed = (d: EventDef) => disasterOk || d.grade !== "disaster";

  if (rng.chance(EV.ludusPerDay)) {
    const def = pickWeighted(state, rng, EVENTS.filter((d) => d.scope === "ludus" && allowed(d) && conditionMet(state, d)));
    if (def) raise(state, rng, def);
  }
  for (const g of roster(state)) {
    if (!rng.chance(EV.gladiatorPerDay)) continue;
    const def = pickWeighted(state, rng, EVENTS.filter((d) => d.scope === "gladiator" && allowed(d) && conditionMet(state, d, g)));
    if (def) raise(state, rng, def, g);
    break; // 하루에 검투사 이벤트는 하나만
  }
  if (rng.chance(EV.worldPerDay)) {
    const def = pickWeighted(state, rng, EVENTS.filter((d) => d.scope === "world" && allowed(d)));
    if (def) raise(state, rng, def);
  }
}

function raise(state: GameState, rng: Rng, def: EventDef, target?: Gladiator): void {
  if (def.grade === "disaster") state.lastDisasterAt = absDay(state.season, state.day);
  if (def.grade === "luck") state.luckStreak = Math.min(state.luckStreak, 0) - 1;
  if (def.grade === "trial" || def.grade === "disaster") state.luckStreak = Math.max(state.luckStreak, 0) + 1;
  const pending: PendingEvent = {
    id: `E${state.nextId++}`,
    eventId: def.id,
    targetId: target?.id,
    title: def.title,
    text: fill(def.text, target),
  };
  // 선택지가 하나뿐이면 바로 적용하고 알리기만 한다
  if (def.choices.length === 1) {
    const result = applyEffects(state, rng, def.choices[0].effects, target);
    pushMessage(state, { kind: "event", title: def.title, body: `${pending.text}\n\n${result}`.trim() });
  } else {
    state.pendingEvents.push(pending);
    pushMessage(state, { kind: "event", title: `${def.title} — 결정이 필요하다`, body: pending.text });
  }
}

/** 플레이어가 고른 선택지를 적용한다 */
export function resolveEvent(state: GameState, rng: Rng, pendingId: string, choiceIdx: number): string {
  const p = state.pendingEvents.find((x) => x.id === pendingId);
  if (!p) return "";
  const def = byId[p.eventId];
  const choice = def.choices[choiceIdx];
  const player = state.ludi[state.playerLudusId];
  if (choice.cost && player.treasury < choice.cost) return "금고가 모자란다.";
  state.pendingEvents = state.pendingEvents.filter((x) => x !== p);
  const target = p.targetId ? state.gladiators[p.targetId] : undefined;
  const result = applyEffects(state, rng, choice.effects, target);
  pushMessage(state, { kind: "event", title: `${def.title}: ${choice.label}`, body: result || "별일 없이 지나갔다." });
  return result;
}

function applyEffects(state: GameState, rng: Rng, effects: Effect[], self?: Gladiator): string {
  const player = state.ludi[state.playerLudusId];
  const lines: string[] = [];
  let last: Gladiator | undefined = self;
  const now = absDay(state.season, state.day);

  const resolveTarget = (t: Effect["target"]): Gladiator | undefined => {
    const r = roster(state);
    if (!r.length) return undefined;
    switch (t) {
      case "self": return self ?? last;
      case "same": return last;
      case "youngest": return [...r].sort((a, b) => a.age - b.age)[0];
      case "lowestMorale": return [...r].sort((a, b) => a.morale - b.morale)[0];
      default: return rng.pick(r);
    }
  };

  for (const e of effects) {
    const g = e.target ? resolveTarget(e.target) : undefined;
    if (g) last = g;
    switch (e.type) {
      case "gold": {
        const v = e.value ?? rng.int(e.min!, e.max!);
        addMoney(state, player, v, "사건");
        lines.push(v >= 0 ? `금고 +${v} D` : `금고 ${v} D`);
        break;
      }
      case "goldPercent": {
        const v = Math.round(player.treasury * e.value!);
        addMoney(state, player, v, "사건");
        lines.push(`금고 ${v} D`);
        break;
      }
      case "moraleAll":
        roster(state).forEach((x) => (x.morale = Math.max(0, Math.min(100, x.morale + e.value!))));
        lines.push(`모두의 사기 ${e.value! > 0 ? "+" : ""}${e.value}`);
        break;
      case "morale":
        if (g) { g.morale = Math.max(0, Math.min(100, g.morale + e.value!)); lines.push(`${g.name} 사기 ${e.value! > 0 ? "+" : ""}${e.value}`); }
        break;
      case "attr":
        if (g) {
          const a = e.attr as keyof Gladiator["attrs"];
          g.attrs[a] = Math.max(1, Math.min(20, g.attrs[a] + e.value!));
          lines.push(`${g.name} ${e.attr} ${e.value! > 0 ? "+" : ""}${e.value}`);
        }
        break;
      case "growth":
        if (g) { grow(g, rng, "match", e.days!, 1, player.facilities.training); lines.push(`${josa(g.name, "이/가")} 한층 성장했다`); logCareer(state, g, "깨달음을 얻었다", "milestone"); }
        break;
      case "pa":
        if (g) { g.hidden.pa = Math.min(200, g.hidden.pa + rng.int(e.min!, e.max!)); lines.push(`${g.name}의 그릇이 커졌다`); logCareer(state, g, "숨은 재능이 깨어났다", "milestone"); }
        break;
      case "popularity":
        if (g) { g.popularity = Math.min(100, g.popularity + e.value!); lines.push(`${g.name} 인기 +${e.value}`); }
        break;
      case "fatigue":
        if (g) g.fatigue = Math.min(100, g.fatigue + e.value!);
        break;
      case "fatigueAll":
        roster(state).forEach((x) => (x.fatigue = Math.min(100, x.fatigue + e.value!)));
        lines.push(`모두 지쳤다 (피로 +${e.value})`);
        break;
      case "injury":
        if (g && !g.injury) {
          g.injury = { severity: e.serious ? "serious" : "light", daysLeft: e.days!, desc: e.desc! };
          lines.push(`${g.name}: ${e.desc} (${e.days}일)`);
          logCareer(state, g, `${e.desc}으로 앓아누웠다`, "injury");
        }
        break;
      case "deathChance":
        if (g && rng.chance(e.chance!)) {
          g.alive = false;
          g.diedAt = { season: state.season, day: state.day, cause: e.cause! };
          player.gladiators = player.gladiators.filter((x) => x !== g.id);
          state.fallen.push(g.id);
          logCareer(state, g, `${e.cause}으로 죽었다`, "death");
          lines.push(`${josa(g.name, "이/가")} 끝내 숨을 거뒀다.`);
        }
        break;
      case "loyalty":
        if (g) g.hidden.loyalty = Math.min(20, g.hidden.loyalty + e.value!);
        break;
      case "wageRaise":
        roster(state).forEach((x) => (x.wage = Math.round(x.wage * (1 + e.value!))));
        lines.push(`모든 주급 +${Math.round(e.value! * 100)}%`);
        break;
      case "gamble":
        if (rng.chance(e.chance!)) { if (e.win) { addMoney(state, player, e.win, "사건"); lines.push(`운이 따랐다. +${e.win} D`); } else lines.push("무사히 넘어갔다."); }
        else if (e.lose) { addMoney(state, player, e.lose, "사건"); lines.push(`운이 없었다. ${e.lose} D`); }
        else lines.push("헛돈만 썼다.");
        break;
      case "modifier":
        state.modifiers.push({ kind: e.kind!, mult: e.mult!, until: now + e.days! });
        break;
      case "refreshMarket":
        refreshFreeAgents(state, rng);
        break;
    }
  }
  return lines.join("\n");
}
