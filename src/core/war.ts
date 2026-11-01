// 나라 사이 전쟁과 용병 의뢰 (docs/08_mercenary.md)
import { absDay } from "./calendar";
import { logCareer, seasonRecord } from "./career";
import { simulateMatch } from "./combat/sim";
import { NATIONS, getNation, getTrait } from "./data";
import { addMoney } from "./economy";
import { generateGladiator } from "./generators/gladiator";
import { computeCA } from "./gladiator";
import { josa } from "./josa";
import { makeInjury } from "./injuries";
import type { Rng } from "./rng";
import { pushMessage } from "./state";
import { grow } from "./training";
import type { Contract, ContractType, GameState, Gladiator, NationId, War } from "./types";

const now = (s: GameState) => absDay(s.season, s.day);

export interface ContractSpec {
  name: string;
  desc: string;
  slots: [number, number];
  days: [number, number];
  minCA: number;
  pay: number;
  risk: number;
}

export const CONTRACT_TYPES: Record<ContractType, ContractSpec> = {
  escort: { name: "호위", desc: "보급 행렬과 귀족을 지킨다. 위험이 낮다", slots: [1, 2], days: [14, 21], minCA: 40, pay: 400, risk: 0.02 },
  front: { name: "전선 파견", desc: "군단과 함께 전선에 선다", slots: [3, 5], days: [28, 42], minCA: 60, pay: 900, risk: 0.08 },
  siege: { name: "공성전", desc: "성벽을 오른다. 죽기 쉽지만 보수가 크다", slots: [3, 6], days: [21, 21], minCA: 70, pay: 1400, risk: 0.15 },
  raid: { name: "정찰·암습", desc: "적진에 숨어들어 지휘관을 노린다", slots: [1, 2], days: [14, 14], minCA: 55, pay: 1100, risk: 0.1 },
  duel: { name: "대리 결투", desc: "양국 대표가 1:1 사투로 전황을 가른다. 이기면 영웅, 지면 거의 죽는다", slots: [1, 1], days: [7, 7], minCA: 120, pay: 5000, risk: 0 },
  levy: { name: "징발령", desc: "조국이 루두스에 검투사를 내놓으라 한다. 보수는 적고, 거부하면 대가가 크다", slots: [2, 2], days: [28, 28], minCA: 30, pay: 450, risk: 0.06 },
};

export const relation = (s: GameState, a: NationId, b: NationId) => s.relations[a]?.[b] ?? 0;

function setRelation(s: GameState, a: NationId, b: NationId, v: number) {
  s.relations[a][b] = v;
  s.relations[b][a] = v;
}

export const activeWars = (s: GameState) => s.wars.filter((w) => !w.over);

export function favorOf(s: GameState, n: NationId): number {
  return s.ludi[s.playerLudusId].favor?.[n] ?? 0;
}

function addFavor(s: GameState, n: NationId, d: number) {
  const p = s.ludi[s.playerLudusId];
  p.favor ??= {};
  p.favor[n] = Math.max(-100, Math.min(100, (p.favor[n] ?? 0) + d));
}

/** 매일: 관계 변화, 전쟁 발발·진행·종전, 의뢰 게시·만료·판정 */
export function dailyWar(s: GameState, rng: Rng): void {
  const t = now(s);

  // 전쟁 발발: 관계가 -50 이하인 나라 쌍
  for (let i = 0; i < NATIONS.length; i++) {
    for (let j = i + 1; j < NATIONS.length; j++) {
      const a = NATIONS[i].id, b = NATIONS[j].id;
      const rel = relation(s, a, b);
      if (activeWars(s).some((w) => (w.a === a && w.b === b) || (w.a === b && w.b === a))) continue;
      // 평화로운 날에도 관계는 조금씩 흔들린다
      if (rng.chance(0.02)) setRelation(s, a, b, Math.max(-100, Math.min(100, rel + rng.int(-6, 4))));
      if (rel > -50) continue;
      if (rng.chance(((-rel - 50) * 0.005) / 7)) startWar(s, rng, a, b);
    }
  }

  for (const w of activeWars(s)) {
    w.front = Math.max(-100, Math.min(100, w.front + rng.normal(0, 3)));
    if (t >= w.endsAt || Math.abs(w.front) >= 100) endWar(s, rng, w);
    else if ((t - w.startedAt) % 7 === 0) postContracts(s, rng, w);
  }

  for (const c of s.contracts) {
    if (c.status === "open" && t > c.expiresAt) {
      c.status = "expired";
      if (c.type === "levy") {
        // 징발령을 무시하면 조국이 등을 돌린다
        addFavor(s, c.employer, -30);
        s.ludi[s.playerLudusId].rp -= 50;
        pushMessage(s, { kind: "warning", title: "징발령을 거부했다", body: `${getNation(c.employer).name}이(가) 분노했다. 호감도 -30, RP -50. 사람들은 우리를 매국 루두스라 부른다.` });
      }
    }
    if (c.status !== "active") continue;
    if ((t - c.startedAt!) % 7 === 0 && t < c.endsAt!) weeklyResolve(s, rng, c);
    if (t >= c.endsAt!) finishContract(s, rng, c);
  }
}

function startWar(s: GameState, rng: Rng, a: NationId, b: NationId): void {
  const w: War = { id: `W${s.nextId++}`, a, b, startedAt: now(s), endsAt: now(s) + rng.int(28, 84), front: 0, over: false };
  s.wars.push(w);
  const na = getNation(a), nb = getNation(b);
  const ours = s.nation === a || s.nation === b;
  pushMessage(s, {
    kind: "warning",
    title: `전쟁: ${na.short} ↔ ${nb.short}`,
    body: `${na.name}과(와) ${nb.name} 사이에 전쟁이 터졌다. 양국이 루두스들에 용병을 구한다.\n대륙 정세 → 전쟁·용병에서 의뢰를 볼 수 있다.` +
      (ours ? "\n\n조국이 전쟁에 휘말렸다. 적국의 의뢰는 받을 수 없고, 징발령이 내려올 수 있다." : ""),
  });
  postContracts(s, rng, w);
  // 조국이 참전하면 1부 상위 루두스에 징발령
  const me = s.ludi[s.playerLudusId];
  if (ours && me.tier === 1) {
    const rank = Object.values(s.ludi).filter((l) => l.tier === 1 && l.nation === s.nation).sort((x, y) => y.rp - x.rp).findIndex((l) => l.isPlayer) + 1;
    if (rank <= 5) addContract(s, rng, w, s.nation, s.nation === a ? b : a, "levy");
  }
}

function addContract(s: GameState, rng: Rng, w: War, employer: NationId, enemy: NationId, type: ContractType): void {
  const spec = CONTRACT_TYPES[type];
  const t = now(s);
  s.contracts.push({
    id: `K${s.nextId++}`, warId: w.id, employer, enemy, type,
    slots: rng.int(spec.slots[0], spec.slots[1]),
    minCA: Math.round(spec.minCA * rng.range(0.9, 1.15)),
    days: rng.int(spec.days[0], spec.days[1]),
    pay: Math.round((spec.pay * rng.range(0.85, 1.25)) / 10) * 10,
    risk: spec.risk,
    postedAt: t, expiresAt: t + 7, status: "open", assigned: [], merit: 0,
  });
}

function postContracts(s: GameState, rng: Rng, w: War): void {
  for (const [emp, enemy] of [[w.a, w.b], [w.b, w.a]] as [NationId, NationId][]) {
    const n = rng.int(1, 2);
    for (let i = 0; i < n; i++) {
      const type = rng.weighted<ContractType>({ escort: 3, front: 3, siege: 1.5, raid: 1.5, duel: 0.5 });
      addContract(s, rng, w, emp, enemy, type);
    }
  }
}

/** 의뢰를 받을 수 있는가 (조국의 적국 의뢰 금지) */
export function canTake(s: GameState, c: Contract): string | null {
  if (c.status !== "open") return "이미 마감됐다";
  if (c.enemy === s.nation) return "조국을 치는 의뢰는 받을 수 없다";
  return null;
}

/** 파견할 수 있는 검투사인가 */
export function eligible(s: GameState, g: Gladiator, c: Contract): string | null {
  if (!g.alive) return "사망";
  if (g.deployment) return "이미 파견 중";
  if (g.injury?.severity === "serious") return "중상";
  if (computeCA(g) < c.minCA) return `능력 부족 (CA ${c.minCA} 필요)`;
  if (s.challenges.some((x) => x.status === "scheduled" && (x.fromGladiator === g.id || x.toGladiator === g.id))) return "경기가 잡혀 있다";
  return null;
}

/** 의뢰 수락: 선금 30%, 파견 기간 주급은 고용국이 낸다 */
export function takeContract(s: GameState, contractId: string, ids: string[]): string | null {
  const c = s.contracts.find((x) => x.id === contractId);
  if (!c) return "의뢰가 없다";
  const no = canTake(s, c);
  if (no) return no;
  if (!ids.length || ids.length > c.slots) return `1~${c.slots}명을 보내라`;
  for (const id of ids) {
    const why = eligible(s, s.gladiators[id], c);
    if (why) return `${s.gladiators[id].name}: ${why}`;
  }
  const me = s.ludi[s.playerLudusId];
  c.status = "active";
  c.assigned = ids;
  c.startedAt = now(s);
  c.endsAt = now(s) + c.days;
  for (const id of ids) {
    const g = s.gladiators[id];
    g.deployment = { contractId: c.id, until: c.endsAt };
    logCareer(s, g, `${getNation(c.employer).short}의 ${CONTRACT_TYPES[c.type].name} 의뢰로 전장에 나갔다`, "other");
    // 노예는 끌려가니 사기가 떨어진다
    if (g.status === "slave") g.morale = Math.max(0, g.morale - 10);
  }
  addMoney(s, me, Math.round(c.pay * ids.length * 0.3), `${CONTRACT_TYPES[c.type].name} 선금`);
  if (c.type !== "levy") {
    addFavor(s, c.employer, 5);
    addFavor(s, c.enemy, -5);
  }
  return null;
}

/** 주간 판정: 검투사마다 전공·무사·부상·사망 */
function weeklyResolve(s: GameState, rng: Rng, c: Contract): void {
  if (c.type === "duel") return;
  const war = s.wars.find((w) => w.id === c.warId);
  const lines: string[] = [];
  const weeks = Math.max(1, Math.round(c.days / 7));
  for (const id of [...c.assigned]) {
    const g = s.gladiators[id];
    if (!g?.alive || !g.deployment) continue;
    const margin = (computeCA(g) - c.minCA) / 100;
    const death = Math.max(0.005, (c.risk / weeks) * (1 - margin));
    const roll = rng.next();
    grow(g, rng, "match", 10, 1, 1);
    if (roll < death) {
      g.alive = false;
      g.diedAt = { season: s.season, day: s.day, cause: `${getNation(c.employer).short}의 ${CONTRACT_TYPES[c.type].name}에서 전사` };
      g.deployment = undefined;
      const me = s.ludi[s.playerLudusId];
      me.gladiators = me.gladiators.filter((x) => x !== id);
      s.fallen.push(id);
      c.assigned = c.assigned.filter((x) => x !== id);
      logCareer(s, g, "전장에서 쓰러져 돌아오지 못했다", "death");
      lines.push(`${josa(g.name, "이/가")} 전사했다.`);
      for (const other of me.gladiators) s.gladiators[other].morale = Math.max(0, s.gladiators[other].morale - 5);
    } else if (roll < death + 0.12) {
      g.injury = makeInjury(rng, rng.chance(0.3) ? "moderate" : "minor", rng.pick(["arm", "leg", "torso", "head"] as const));
      seasonRecord(s, g).injuries++;
      lines.push(`${g.name}: ${g.injury.desc}`);
    } else if (rng.chance(0.25 + margin)) {
      c.merit++;
      lines.push(`${g.name}: 전공을 세웠다`);
      if (war) war.front += (war.a === c.employer ? 1 : -1) * 4;
    }
  }
  if (lines.length) pushMessage(s, { kind: lines.some((l) => l.includes("전사")) ? "death" : "info", title: `전장 소식: ${CONTRACT_TYPES[c.type].name}`, body: lines.join("\n") });
}

function finishContract(s: GameState, rng: Rng, c: Contract): void {
  const me = s.ludi[s.playerLudusId];
  const war = s.wars.find((w) => w.id === c.warId);
  c.status = "done";
  if (c.type === "duel") {
    const champ = s.gladiators[c.assigned[0]];
    if (champ?.alive) {
      const foe = generateGladiator(rng, { id: "foe", nation: c.enemy, target: Math.min(18, c.minCA / 10 + 2) });
      foe.name = `${getNation(c.enemy).short}의 챔피언 ${foe.name}`;
      const r = simulateMatch(champ, foe, { rule: "sudden", seed: rng.int(0, 2 ** 31 - 1) });
      if (r.winnerId === champ.id) {
        c.merit += 10;
        if (war) war.front += (war.a === c.employer ? 1 : -1) * 40;
        addTrait(champ, "war_hero");
        champ.popularity = Math.min(100, champ.popularity + 15);
        logCareer(s, champ, `대리 결투에서 ${foe.name}을(를) 꺾고 영웅이 됐다`, "milestone");
        addFavor(s, c.employer, 25);
        addFavor(s, c.enemy, -25);
        pushMessage(s, { kind: "match", title: `대리 결투 승리: ${champ.name}`, body: `${josa(champ.name, "이/가")} ${foe.name}을(를) 쓰러뜨렸다. 전황이 크게 기울었다.` });
      } else if (r.died) {
        champ.alive = false;
        champ.diedAt = { season: s.season, day: s.day, cause: `대리 결투에서 ${foe.name}에게 패배` };
        me.gladiators = me.gladiators.filter((x) => x !== champ.id);
        s.fallen.push(champ.id);
        c.assigned = [];
        logCareer(s, champ, `대리 결투에서 ${foe.name}에게 졌다`, "death");
        pushMessage(s, { kind: "death", title: `대리 결투 패배: ${champ.name}`, body: `${josa(champ.name, "이/가")} 돌아오지 못했다.` });
      }
    }
  }
  const survivors = c.assigned.filter((id) => s.gladiators[id]?.alive);
  const rest = Math.round(c.pay * survivors.length * 0.7 + c.merit * 150);
  if (rest) addMoney(s, me, rest, `${CONTRACT_TYPES[c.type].name} 완료 보수`);
  // 전사자 위로금은 고용국이 낸다
  const dead = c.assigned.length - survivors.length + (c.type === "duel" && !survivors.length ? 1 : 0);
  for (const id of survivors) {
    const g = s.gladiators[id];
    g.deployment = undefined;
    if (c.merit >= 3 && rng.chance(0.3)) addTrait(g, "war_hero");
    logCareer(s, g, `${getNation(c.employer).short}의 전장에서 돌아왔다`, "other");
  }
  if (c.type !== "levy") addFavor(s, c.employer, 10);
  pushMessage(s, {
    kind: "info",
    title: `${CONTRACT_TYPES[c.type].name} 의뢰 완료`,
    body: `돌아온 자 ${survivors.length}명${dead ? ` · 돌아오지 못한 자 ${dead}명` : ""} · 전공 ${c.merit}\n완료 보수 ${rest.toLocaleString()} D (세전)`,
  });
}

function addTrait(g: Gladiator, id: string) {
  if (!g.traits.includes(id) && getTrait(id)) g.traits.push(id);
}

function endWar(s: GameState, rng: Rng, w: War): void {
  w.over = true;
  w.winner = w.front >= 0 ? w.a : w.b;
  const loser = w.winner === w.a ? w.b : w.a;
  setRelation(s, w.a, w.b, -30);
  // 패전국 포로가 경매장에 나온다
  if (loser !== s.nation) s.powPending = loser;
  for (const c of s.contracts) if (c.warId === w.id && c.status === "open") c.status = "expired";
  pushMessage(s, {
    kind: "info",
    title: `종전: ${getNation(w.winner).short} 승리`,
    body: `${getNation(w.winner).name}이(가) ${getNation(loser).name}을(를) 꺾었다. ${loser !== s.nation ? "다음 경매에 포로들이 싼값에 나온다." : "조국이 졌다. 다음 시즌 살림이 빠듯해질 것이다."}`,
  });
  void rng;
}
