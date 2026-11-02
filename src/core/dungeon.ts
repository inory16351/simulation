// 던전 원정: 검투사 1~5명을 며칠 보내 장비를 캐 온다. 싸움은 층마다 5:5 진형 전투.
// 돈은 거의 안 남고(급료를 생각하면 손해) 명성도 없다. 목적은 장비와 경험이다. 쓰러지면 죽을 수 있다.
// 나라가 가끔 내거는 토벌 의뢰는 아주 어렵지만 명성(RP)과 현상금을 준다.
// 전투 규칙은 바뀔 수 있게 FORMATIONS와 skirmish() 한곳에 모아 둔다.
import { absDay } from "./calendar";
import { logCareer, seasonRecord } from "./career";
import { simulateMatch } from "./combat/sim";
import { ARMORS, NATIONS, WEAPONS, getNation, getWeapon } from "./data";
import { addMoney } from "./economy";
import { generateGladiator } from "./generators/gladiator";
import { computeCA } from "./gladiator";
import { itemName } from "./gear";
import { makeInjury } from "./injuries";
import { josa } from "./josa";
import type { Rng } from "./rng";
import { pushMessage } from "./state";
import { grow } from "./training";
import type { GameState, Gladiator, Id, NationId, RaceId } from "./types";

export type DungeonId = "sewer" | "mine" | "catacomb" | "magma" | "abyss";
export interface DungeonDef {
  id: DungeonId;
  name: string;
  desc: string;
  tier: number;
  floors: number;
  days: number;
  /** 몬스터 실력 (generateGladiator target) */
  level: number;
  /** 쓰러진 검투사가 죽을 기본 확률 */
  death: number;
  /** 품질 0~3 가중치 */
  loot: [number, number, number, number];
  foes: { race: RaceId; name: string }[];
  boss: { race: RaceId; name: string };
}

export const DUNGEONS: DungeonDef[] = [
  { id: "sewer", name: "쥐굴 하수도", desc: "도시 밑 하수도. 쥐인간과 도적 떼가 삽니다. 처음 가 보기 좋습니다", tier: 1, floors: 3, days: 7, level: 5, death: 0.04, loot: [60, 32, 8, 0],
    foes: [{ race: "beastkin", name: "쥐인간" }, { race: "human", name: "하수도 도적" }], boss: { race: "beastkin", name: "쥐왕" } },
  { id: "mine", name: "버려진 은광", desc: "무너진 갱도에 오크 약탈자와 광부 망령이 숨어 있습니다", tier: 2, floors: 3, days: 10, level: 7, death: 0.07, loot: [45, 38, 15, 2],
    foes: [{ race: "orc", name: "오크 약탈자" }, { race: "dwarf", name: "광부 망령" }], boss: { race: "orc", name: "갱도 두목" } },
  { id: "catacomb", name: "망자의 지하묘지", desc: "옛 검투사들이 묻힌 묘지. 죽은 자들이 아직 싸웁니다", tier: 3, floors: 4, days: 14, level: 9, death: 0.1, loot: [30, 40, 24, 6],
    foes: [{ race: "human", name: "해골 검투사" }, { race: "elf", name: "묘지 사령술사" }], boss: { race: "human", name: "무덤의 챔피언" } },
  { id: "magma", name: "용암 동굴", desc: "불타는 동굴. 도마뱀 부족과 미노타우로스 광전사가 지킵니다", tier: 4, floors: 4, days: 18, level: 11, death: 0.13, loot: [20, 38, 32, 10],
    foes: [{ race: "lizardfolk", name: "화염 도마뱀 전사" }, { race: "minotaur", name: "용암 광전사" }], boss: { race: "minotaur", name: "불의 미궁 주인" } },
  { id: "abyss", name: "심연의 신전", desc: "가장 깊은 곳. 살아 돌아온 자가 드뭅니다", tier: 5, floors: 5, days: 21, level: 13, death: 0.17, loot: [10, 32, 40, 18],
    foes: [{ race: "elf", name: "심연의 사제" }, { race: "orc", name: "타락한 전사" }, { race: "minotaur", name: "심연의 수문장" }], boss: { race: "elf", name: "잊힌 신의 대리인" } },
];
export const getDungeon = (id: DungeonId) => DUNGEONS.find((d) => d.id === id)!;
export const MAX_PARTY = 5;

/** 5:5 진형 (docs/13_tactics.md). 배율은 (주는 피해, 받는 피해) */
export type Formation = "shieldwall" | "wedge" | "flank" | "spread";
export const FORMATIONS: Record<Formation, { name: string; desc: string; dealt: number; taken: number; ranged: number; focus: "front" | "weakest" | "strongest" }> = {
  shieldwall: { name: "방패벽", desc: "전열 3·후열 2. 받는 피해가 줄지만 느리게 이깁니다", dealt: 0.9, taken: 0.8, ranged: 1, focus: "front" },
  wedge: { name: "쐐기", desc: "중앙 돌파. 약한 적부터 몰아칩니다. 받는 피해도 늘어납니다", dealt: 1.2, taken: 1.1, ranged: 1, focus: "weakest" },
  flank: { name: "포위", desc: "양 날개 전진. 수가 많을수록 강합니다", dealt: 1, taken: 1, ranged: 1, focus: "strongest" },
  spread: { name: "산개", desc: "흩어져 싸웁니다. 투척·창·주술이 강해지고 근접은 약해집니다", dealt: 0.95, taken: 1, ranged: 1.3, focus: "front" },
};

export interface Expedition {
  id: Id;
  dungeon: DungeonId;
  party: Id[];
  formation: Formation;
  startedAt: number;
  endsAt: number;
  /** 깬 층 수 */
  floor: number;
  status: "active" | "done" | "failed";
  questId?: Id;
  loot: string[];
  coins: number;
  log: string[];
  /** 다음 층에 들고 가는 체력 비율 */
  hp: Record<Id, number>;
}

export interface DungeonQuest {
  id: Id;
  nation: NationId;
  dungeon: DungeonId;
  /** 몬스터 실력 배율 (아주 어렵다) */
  hard: number;
  bounty: number;
  rp: number;
  postedAt: number;
  expiresAt: number;
  status: "open" | "taken" | "done" | "failed" | "expired";
}

const now = (s: GameState) => absDay(s.season, s.day);
export const expeditionsOf = (s: GameState) => (s.expeditions ??= []);
export const questsOf = (s: GameState) => (s.dungeonQuests ??= []);

/** 원정에 보낼 수 있는가 */
export function cannotSend(s: GameState, g: Gladiator): string | null {
  if (!g.alive) return "사망";
  if (g.deployment) return g.deployment.kind === "dungeon" ? "원정 중" : "파견 중";
  if (g.injury?.severity === "serious") return "중상";
  if (s.challenges.some((x) => x.status === "scheduled" && (x.fromGladiator === g.id || x.toGladiator === g.id))) return "경기가 잡혀 있습니다";
  return null;
}

export function startExpedition(s: GameState, dungeon: DungeonId, ids: Id[], formation: Formation, questId?: Id): string | null {
  if (!ids.length || ids.length > MAX_PARTY) return `1~${MAX_PARTY}명을 보내십시오`;
  const me = s.ludi[s.playerLudusId];
  for (const id of ids) {
    const g = s.gladiators[id];
    if (!me.gladiators.includes(id)) return "우리 검투사가 아닙니다";
    const why = cannotSend(s, g);
    if (why) return `${g.name}: ${why}`;
  }
  const q = questId ? questsOf(s).find((x) => x.id === questId) : undefined;
  if (questId && (!q || q.status !== "open")) return "의뢰가 마감됐습니다";
  const d = getDungeon(q?.dungeon ?? dungeon);
  const t = now(s);
  const ex: Expedition = {
    id: `X${s.nextId++}`, dungeon: d.id, party: ids, formation, startedAt: t, endsAt: t + d.days,
    floor: 0, status: "active", questId, loot: [], coins: 0, log: [], hp: Object.fromEntries(ids.map((id) => [id, 1])),
  };
  expeditionsOf(s).push(ex);
  if (q) q.status = "taken";
  for (const id of ids) {
    const g = s.gladiators[id];
    g.deployment = { contractId: ex.id, until: ex.endsAt, kind: "dungeon" };
    logCareer(s, g, `${d.name}${q ? " 토벌" : ""} 원정을 떠났습니다`, "other");
  }
  return null;
}

/** 원정 일정에서 n번째 층을 치르는 날 */
const floorDay = (ex: Expedition, d: DungeonDef, n: number) => ex.startedAt + Math.round((d.days * n) / d.floors);

/** 매일: 원정 층 진행, 귀환, 토벌 의뢰 만료 */
export function dailyDungeons(s: GameState, rng: Rng): void {
  const t = now(s);
  for (const ex of expeditionsOf(s)) {
    if (ex.status !== "active") continue;
    const d = getDungeon(ex.dungeon);
    while (ex.status === "active" && ex.floor < d.floors && t >= floorDay(ex, d, ex.floor + 1)) runFloor(s, rng, ex, d);
    if (t >= ex.endsAt || ex.status !== "active" || !alive(s, ex).length) finishExpedition(s, ex);
  }
  for (const q of questsOf(s)) if (q.status === "open" && t > q.expiresAt) q.status = "expired";
  // 끝난 원정·의뢰는 조금만 남긴다
  s.expeditions = expeditionsOf(s).filter((x) => x.status === "active" || t - x.endsAt < 28);
  s.dungeonQuests = questsOf(s).filter((q) => q.status === "open" || q.status === "taken" || t - q.expiresAt < 14);
}

const alive = (s: GameState, ex: Expedition) => ex.party.filter((id) => s.gladiators[id]?.alive && s.gladiators[id].deployment?.contractId === ex.id);

/** 한 층: 몬스터 무리와 5:5 진형 전투. 이기면 전리품, 지면 쓰러진 자를 끌고 물러난다 */
function runFloor(s: GameState, rng: Rng, ex: Expedition, d: DungeonDef): void {
  const quest = ex.questId ? questsOf(s).find((q) => q.id === ex.questId) : undefined;
  const boss = ex.floor + 1 === d.floors;
  const party = alive(s, ex).map((id) => s.gladiators[id]);
  const foes = spawnFoes(rng, d, boss, quest?.hard ?? 1, ex.floor);
  const res = skirmish(rng, party, foes, ex.formation, ex.hp);
  const lines: string[] = [`${ex.floor + 1}층${boss ? " (우두머리)" : ""}: ${foes.map((f) => f.name).join(", ")}`];

  for (const g of party) {
    grow(g, rng, "match", 6 + d.tier * 2, 1, 1);
    seasonRecord(s, g);
  }
  // 쓰러진 검투사: 죽거나 다친다. 죽을 확률은 실력 차이와 의뢰 난이도에 따라
  for (const id of res.downed) {
    const g = s.gladiators[id];
    const margin = (computeCA(g) - d.level * 10 * (quest?.hard ?? 1)) / 120;
    const p = Math.max(0.01, d.death * (quest ? 1.5 : 1) * (1 - margin) * (res.won ? 0.6 : 1.2));
    if (rng.chance(p)) {
      killInDungeon(s, g, d);
      lines.push(`${josa(g.name, "이/가")} 쓰러져 다시 일어나지 못했습니다.`);
    } else {
      g.injury = makeInjury(rng, rng.chance(0.35) ? "moderate" : "minor", rng.pick(["arm", "leg", "torso", "head"] as const));
      seasonRecord(s, g).injuries++;
      lines.push(`${g.name}: ${g.injury.desc}`);
    }
  }
  if (res.won) {
    ex.floor++;
    for (const id of Object.keys(res.hp)) ex.hp[id] = Math.min(1, res.hp[id] + 0.3);
    const drops = rollLoot(rng, d, boss, quest ? 1 : 0);
    ex.loot.push(...drops);
    const coins = rng.int(10, 30) * d.tier;
    ex.coins += coins;
    lines.push(`승리. 전리품: ${drops.map(itemName).join(", ")} · 동전 ${coins} D`);
    for (const g of party) if (res.kills[g.id]) { g.record.kills += res.kills[g.id]; }
  } else {
    lines.push("패퇴. 남은 자들이 동료를 메고 물러났습니다.");
    ex.status = "failed";
  }
  ex.log.push(...lines);
  pushMessage(s, {
    kind: lines.some((l) => l.includes("일어나지 못했습니다")) ? "death" : "info",
    title: `원정 소식: ${d.name} ${ex.floor + (res.won ? 0 : 1)}층`,
    body: lines.join("\n"),
  });
}

function spawnFoes(rng: Rng, d: DungeonDef, boss: boolean, hard: number, floor: number): Gladiator[] {
  const n = Math.min(5, 3 + Math.floor(floor / 2) + (boss ? 1 : 0));
  const out: Gladiator[] = [];
  for (let i = 0; i < n; i++) {
    const kind = boss && i === 0 ? d.boss : rng.pick(d.foes);
    const lvl = Math.min(20, Math.round((d.level + floor * 0.6 + (boss && i === 0 ? 2 : 0)) * hard));
    const g = generateGladiator(rng, { id: `M${i}_${rng.int(0, 1e9)}`, nation: rng.pick(NATIONS).id, target: lvl, race: kind.race });
    g.name = kind.name;
    out.push(g);
  }
  return out;
}

interface Skirmish { won: boolean; downed: Id[]; hp: Record<Id, number>; kills: Record<Id, number> }

const isRanged = (g: Gladiator) => {
  const w = getWeapon(g.equipment.main);
  return !!w && (w.range > 0 || w.class === "focus");
};

/**
 * 5:5 진형 전투. 한 교환마다 살아 있는 아군이 차례로 진형이 고른 적과 1:1로 붙고(기존 1:1 전투 엔진),
 * 남은 체력을 다음 대결로 이어 간다. 같은 적에게 여럿이 붙으면 자연스레 협공이 된다.
 * 진형 배율은 대결에서 깎인 체력에 곱한다.
 */
export function skirmish(rng: Rng, party: Gladiator[], foes: Gladiator[], formation: Formation, startHp: Record<Id, number> = {}): Skirmish {
  const F = FORMATIONS[formation];
  const hp: Record<Id, number> = {};
  for (const g of [...party, ...foes]) hp[g.id] = startHp[g.id] ?? 1;
  const kills: Record<Id, number> = {};
  const downed: Id[] = [];
  const up = (list: Gladiator[]) => list.filter((g) => hp[g.id] > 0.02);
  // 방패벽·산개는 근접을 앞에, 원거리를 뒤에 세운다
  const order = (list: Gladiator[]) => [...list].sort((a, b) => Number(isRanged(a)) - Number(isRanged(b)));

  for (let round = 0; round < 12 && up(party).length && up(foes).length; round++) {
    const allies = order(up(party));
    for (let i = 0; i < allies.length; i++) {
      const a = allies[i];
      const targets = up(foes);
      if (!targets.length || hp[a.id] <= 0.02) continue;
      const foe = F.focus === "weakest" ? targets.reduce((m, x) => (hp[x.id] < hp[m.id] ? x : m))
        : F.focus === "strongest" ? targets.reduce((m, x) => (computeCA(x) > computeCA(m) ? x : m))
        : targets[i % targets.length];
      const r = simulateMatch(a, foe, { rule: "training", noDeath: true, seed: rng.int(0, 2 ** 31 - 1), startHp: [hp[a.id] < 1 ? hp[a.id] : undefined, hp[foe.id] < 1 ? hp[foe.id] : undefined] });
      // 포위: 아군이 많을수록, 산개: 원거리면 더 세게
      const outnumber = formation === "flank" ? 1 + 0.12 * Math.max(0, up(party).length - targets.length) : 1;
      const dealt = F.dealt * outnumber * (isRanged(a) ? F.ranged : formation === "spread" ? 0.9 : 1);
      const lostA = (hp[a.id] - r.hpLeft[a.id]) * F.taken;
      const lostF = (hp[foe.id] - r.hpLeft[foe.id]) * dealt;
      hp[a.id] = r.winnerId === a.id ? Math.max(0.03, hp[a.id] - lostA) : 0;
      hp[foe.id] = r.winnerId === foe.id ? Math.max(0.03, hp[foe.id] - lostF) : 0;
      if (hp[foe.id] <= 0.02) kills[a.id] = (kills[a.id] ?? 0) + 1;
      if (hp[a.id] <= 0.02) downed.push(a.id);
    }
    // 적의 반격: 아군이 다 붙지 못한 남는 적은 후열을 노린다
    const spare = up(foes).slice(allies.length);
    for (const foe of spare) {
      const t = order(up(party)).reverse()[0];
      if (!t) break;
      const r = simulateMatch(foe, t, { rule: "training", noDeath: true, seed: rng.int(0, 2 ** 31 - 1), startHp: [hp[foe.id] < 1 ? hp[foe.id] : undefined, hp[t.id] < 1 ? hp[t.id] : undefined] });
      hp[t.id] = r.winnerId === t.id ? Math.max(0.03, hp[t.id] - (hp[t.id] - r.hpLeft[t.id]) * F.taken) : 0;
      hp[foe.id] = r.winnerId === foe.id ? Math.max(0.03, hp[foe.id] - (hp[foe.id] - r.hpLeft[foe.id]) * F.dealt) : 0;
      if (hp[t.id] <= 0.02) downed.push(t.id);
    }
  }
  const won = up(foes).length === 0 && up(party).length > 0;
  const partyHp = Object.fromEntries(party.map((g) => [g.id, hp[g.id]]));
  return { won, downed: [...new Set(downed)], hp: partyHp, kills };
}

const LOOT_POOL = [...WEAPONS.map((w) => w.id), ...ARMORS.map((a) => a.id)].filter((id) => id !== "loincloth" && id !== "cestus");

/** 층 전리품: 1~2개, 우두머리는 하나 더 (한 단계 위 품질). bonus는 토벌 의뢰의 품질 가산 */
export function rollLoot(rng: Rng, d: DungeonDef, boss: boolean, bonus = 0): string[] {
  const roll = (extra: number) => {
    const w = d.loot;
    const q = Math.min(3, Number(rng.weighted({ "0": w[0], "1": w[1], "2": w[2], "3": w[3] })) + extra + bonus);
    const base = rng.pick(LOOT_POOL);
    return q ? `${base}+${q}` : base;
  };
  const out = [roll(0)];
  if (rng.chance(0.45)) out.push(roll(0));
  if (boss) out.push(roll(1));
  return out;
}

function killInDungeon(s: GameState, g: Gladiator, d: DungeonDef): void {
  const me = s.ludi[s.playerLudusId];
  g.alive = false;
  g.injury = undefined;
  g.deployment = undefined;
  g.diedAt = { season: s.season, day: s.day, cause: `${d.name}에서 전사` };
  me.gladiators = me.gladiators.filter((x) => x !== g.id);
  s.fallen.push(g.id);
  logCareer(s, g, `${d.name} 깊은 곳에서 쓰러져 돌아오지 못했습니다`, "death");
  for (const other of me.gladiators) s.gladiators[other].morale = Math.max(0, s.gladiators[other].morale - 5);
}

/** 귀환: 전리품은 창고로, 동전은 금고로. 토벌 의뢰를 끝까지 깼으면 현상금과 명성 */
function finishExpedition(s: GameState, ex: Expedition): void {
  const d = getDungeon(ex.dungeon);
  const me = s.ludi[s.playerLudusId];
  const cleared = ex.floor >= d.floors;
  if (ex.status === "active") ex.status = cleared ? "done" : "failed";
  ex.endsAt = Math.min(ex.endsAt, now(s));
  for (const id of ex.party) {
    const g = s.gladiators[id];
    if (g?.deployment?.contractId === ex.id) g.deployment = undefined;
  }
  (s.armory ??= []).push(...ex.loot);
  if (ex.coins) addMoney(s, me, ex.coins, `${d.name} 원정 동전`);
  const q = ex.questId ? questsOf(s).find((x) => x.id === ex.questId) : undefined;
  const lines = [`${d.floors}층 중 ${ex.floor}층을 깼습니다.`, ex.loot.length ? `가져온 장비: ${ex.loot.map(itemName).join(", ")}` : "가져온 장비가 없습니다."];
  if (q) {
    if (cleared) {
      q.status = "done";
      addMoney(s, me, q.bounty, `${getNation(q.nation).short} 토벌 현상금`);
      me.rp += q.rp;
      me.favor ??= {};
      me.favor[q.nation] = Math.min(100, (me.favor[q.nation] ?? 0) + 10);
      for (const id of ex.party) {
        const g = s.gladiators[id];
        if (g?.alive) { g.popularity = Math.min(100, g.popularity + 6); logCareer(s, g, `${d.name} 토벌에 성공했습니다`, "milestone"); }
      }
      lines.push(`토벌 성공! 현상금 ${q.bounty.toLocaleString()} D, RP +${q.rp}, ${getNation(q.nation).short} 호감 +10.`);
    } else {
      q.status = "failed";
      lines.push("토벌에 실패했습니다.");
    }
  }
  pushMessage(s, { kind: "info", title: `원정 귀환: ${d.name}`, body: lines.join("\n") + "\n\n장비는 막사 → 검투사 화면에서 착용할 수 있습니다." });
}

/** 급료일마다: 가끔 나라가 던전 토벌 의뢰를 낸다 (어렵다) */
export function weeklyQuests(s: GameState, rng: Rng): void {
  if (questsOf(s).some((q) => q.status === "open") || !rng.chance(0.18)) return;
  const d = DUNGEONS[rng.int(2, DUNGEONS.length - 1)];
  const nation = rng.chance(0.6) ? s.nation : rng.pick(NATIONS).id;
  const t = now(s);
  const q: DungeonQuest = {
    id: `Q${s.nextId++}`, nation, dungeon: d.id, hard: rng.range(1.3, 1.5),
    bounty: Math.round((d.tier * 1500 * rng.range(0.9, 1.3)) / 100) * 100,
    rp: d.tier * 12 + rng.int(0, 10), postedAt: t, expiresAt: t + 14, status: "open",
  };
  questsOf(s).push(q);
  pushMessage(s, {
    kind: "warning",
    title: `토벌 의뢰: ${d.name}`,
    body: `${josa(getNation(nation).name, "이/가")} ${d.name}의 괴물을 소탕할 루두스를 찾습니다. 평소보다 훨씬 강한 놈들이 들끓습니다.\n현상금은 ${q.bounty.toLocaleString()} D이고 RP도 ${q.rp} 오릅니다. 14일 안에 떠나야 합니다.\n\n던전 화면에서 받을 수 있습니다.`,
  });
}
