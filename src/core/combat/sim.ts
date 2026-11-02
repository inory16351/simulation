import { BALANCE, getArmor, getRace, getSkill, getStyle, getWeapon, type ArmorDef, type SkillDef, type WeaponDef } from "../data";
import { performance } from "../condition";
import { areaOf, makeInjury } from "../injuries";
import { effectiveAttr } from "../gladiator";
import { Rng } from "../rng";
import { defaultDoctrine, type Doctrine, type Focus, type RuleAction, type Stance } from "../tactics";
import type { AttrId, DeathRule, Gladiator, Id, Injury } from "../types";
import {
  ADV, SKILL_MOTION, affinityHit, cell, emptyBook, habitsOf, learnWeight, outcomeOf, planAttack, planGuard,
  type Dir, type Guard, type Habits, type Mind, type Motion, type Outcome, type ReadBook,
} from "./exchange";

export type EndReason = "ko" | "surrender" | "timeout";
export type HighlightKind = "entrance" | "crit" | "race_skill" | "execute" | "comeback" | "missio" | "death";
export type BodyPart = "head" | "neck" | "chest" | "side" | "shoulder" | "arm" | "thigh" | "leg";

export type CombatEvent =
  | { t: "start"; a: Id; b: Id; rule: DeathRule }
  | { t: "round"; round: number }
  | { t: "move"; actor: Id; dist: number; closer: boolean; skill?: string }
  | {
      t: "attack"; actor: Id; target: Id; skill?: string; hit: boolean; blocked: boolean; crit: boolean;
      damage: number; part: BodyPart; hpAfter: number; maxHp: number; magic: boolean;
      /** 수 싸움에서 완전히 막혔다 (중계는 exchange 문장으로 대신한다) */
      parried?: boolean;
      /** 완전 방어 뒤 되받아친 공격 */
      counter?: boolean;
    }
  | {
      /** 한 합의 수 싸움 (docs/15_combat_v2.md) */
      t: "exchange"; actor: Id; target: Id; dir: Dir; motion: Motion; guardDir: Dir; guard: Guard; outcome: Outcome;
      feint?: "ok" | "fail"; improv?: boolean;
      /** 방어자가 이 칸을 이번 경기에서 몇 번 봤는지 (이번 합 전까지) */
      seen: number;
    }
  | { t: "status"; target: Id; status: string; turns: number; resisted?: boolean }
  | { t: "self"; actor: Id; skill: string }
  | { t: "crowd"; delta: number; value: number }
  | { t: "rest"; actor: Id }
  | { t: "poison"; target: Id; damage: number; hpAfter: number }
  | { t: "skip"; actor: Id; reason: "stun" | "prone" | "grappled" }
  | { t: "enrage"; actor: Id }
  | { t: "unbreakable"; actor: Id }
  | { t: "surrender"; actor: Id }
  | { t: "doctrine"; actor: Id; action: RuleAction }
  | { t: "ko"; target: Id; crit: boolean }
  | { t: "timeout" }
  | { t: "verdict"; target: Id; rule: DeathRule; deathChance: number; died: boolean }
  | { t: "end"; winner: Id; loser: Id; reason: EndReason }
  | { t: "highlight"; kind: HighlightKind; actors: Id[] };

export interface MatchOptions {
  rule: DeathRule;
  seed: number;
  /** 지정하지 않으면 각 검투사의 교범을 쓴다 */
  doctrines?: [Doctrine | undefined, Doctrine | undefined];
  medicalLevel?: [number, number];
  /** 견습 투기처럼 죽음이 없는 경기 */
  noDeath?: boolean;
  /** 나라 리그 특징: 미시오 사망 확률 배율 */
  missioDeathMult?: number;
  /** 단체전 연속 대결: 앞 판에서 남은 체력 비율 */
  startHp?: [number | undefined, number | undefined];
}

export interface MatchResult {
  events: CombatEvent[];
  winnerId: Id;
  loserId: Id;
  reason: EndReason;
  died: Id | null;
  rounds: number;
  crowd: number;
  damageDealt: Record<Id, number>;
  crowdContribution: Record<Id, number>;
  injuries: Record<Id, Injury | undefined>;
  /** 끝났을 때 남은 체력 비율 (0~1) */
  hpLeft: Record<Id, number>;
}

const C = BALANCE.combat;
const D = BALANCE.death;

interface Fighter {
  g: Gladiator;
  idx: 0 | 1;
  hp: number;
  maxHp: number;
  sta: number;
  maxSta: number;
  mana: number;
  momentum: number;
  statuses: Map<string, number>;
  cooldowns: Map<string, number>;
  main: WeaponDef;
  off?: WeaponDef;
  shield?: ArmorDef;
  armor?: ArmorDef;
  helmet?: ArmorDef;
  ammo: number;
  netReady: boolean;
  wardHp: number;
  usedUnbreakable: boolean;
  usedRaceSkill: boolean;
  doctrine: Doctrine;
  stance: Stance;
  /** 이번 판에서 이미 알린 조건 명령 */
  firedRules: Set<number>;
  /** 조건 명령이 지정한 최우선 기술 */
  priorityStake?: string;
  damageDealt: number;
  damageTaken: number;
  crowdContribution: number;
  hasPassive: (id: string) => boolean;
  /** 컨디션(사기·감각·혹사·피로·미납)을 모은 경기력 배율 */
  perf: number;
  /** 가장 크게 맞은 부위 (부상 부위) */
  worstPart?: BodyPart;
  worstHit: number;
  habits: Habits;
  mind: Mind;
  /** 상대에 대해 이번 경기에 본 것 */
  book: ReadBook;
  /** 상대 공격 칸을 본 횟수 */
  seenN: number[];
}

const PART_WEIGHTS: Record<BodyPart, number> = {
  head: 1, neck: 0.4, chest: 3, side: 2, shoulder: 2, arm: 2.5, thigh: 2, leg: 1.5,
};

const FOCUS_HIT: Record<Focus, number> = { spread: 0, head: -0.15, body: 0.1, legs: -0.05, arm: -0.1 };
const FOCUS_PARTS: Record<Focus, Partial<Record<BodyPart, number>>> = {
  spread: PART_WEIGHTS,
  head: { head: 4, neck: 1, shoulder: 1 },
  body: { chest: 4, side: 3, shoulder: 1 },
  legs: { thigh: 3, leg: 3, side: 0.5 },
  arm: { arm: 4, shoulder: 2 },
};

function makeFighter(g: Gladiator, idx: 0 | 1, doctrine: Doctrine): Fighter {
  const main = getWeapon(g.equipment.main) ?? getWeapon("cestus")!;
  const offW = g.equipment.off ? getWeapon(g.equipment.off) : undefined;
  const offA = g.equipment.off ? getArmor(g.equipment.off) : undefined;
  const armor = getArmor(g.equipment.armor);
  const helmet = g.equipment.helmet ? getArmor(g.equipment.helmet) : undefined;
  const tou = effectiveAttr(g, "tou");
  const maxHp = Math.round(C.baseHp + tou * C.hpPerTou + effectiveAttr(g, "str") * C.hpPerStr);
  const maxSta = Math.round(C.baseSta + effectiveAttr(g, "end") * C.staPerEnd);
  return {
    g, idx, hp: maxHp, maxHp, sta: maxSta, maxSta,
    mana: effectiveAttr(g, "mag") * C.manaPerMag,
    momentum: 50,
    statuses: new Map(),
    cooldowns: new Map(),
    main,
    off: offW,
    shield: offA?.slot === "shield" ? offA : undefined,
    armor, helmet,
    ammo: offW?.ammo ?? main.ammo ?? 0,
    netReady: offW?.id === "net",
    wardHp: 0,
    usedUnbreakable: false,
    usedRaceSkill: false,
    doctrine, stance: doctrine.stance, firedRules: new Set(),
    damageDealt: 0, damageTaken: 0, crowdContribution: 0,
    hasPassive: (id) => g.skills.passive.includes(id),
    perf: performance(g).total,
    worstHit: 0,
    habits: habitsOf(g, main.class, {
      focus: doctrine.focus, shield: offA?.slot === "shield", armorAgiPenalty: g.race === "dwarf" ? 0 : armor?.agiPenalty ?? 0,
      agg: effectiveAttr(g, "agg"), genius: effectiveAttr(g, "genius"), blk: effectiveAttr(g, "blk"), eva: effectiveAttr(g, "eva"),
    }),
    mind: {
      read: effectiveAttr(g, "read"), feint: effectiveAttr(g, "feint"), genius: effectiveAttr(g, "genius"), adapt: effectiveAttr(g, "adapt"),
      dec: effectiveAttr(g, "dec"), rea: effectiveAttr(g, "rea"), comp: effectiveAttr(g, "comp"), form: 0, late: 0,
    },
    book: emptyBook(),
    seenN: Array(9).fill(0),
  };
}

/** 상태이상을 반영한 전투 중 능력치 */
function attr(f: Fighter, a: AttrId): number {
  let v = effectiveAttr(f.g, a);
  if (f.statuses.has("weakened") && (a === "str" || a === "acc")) v -= 3;
  if (f.statuses.has("hobbled") && (a === "agi" || a === "eva")) v -= 4;
  if (a === "agi" && f.armor && !(f.g.race === "dwarf")) v -= f.armor.agiPenalty ?? 0;
  if (a === "acc" && f.helmet?.accPenalty) v -= f.helmet.accPenalty;
  if (a === "show" && f.helmet?.showBonus) v += f.helmet.showBonus;
  return Math.max(1, v);
}

export function simulateMatch(a: Gladiator, b: Gladiator, opt: MatchOptions): MatchResult {
  const rng = new Rng(opt.seed);
  const docs = [opt.doctrines?.[0] ?? a.doctrine ?? defaultDoctrine(a), opt.doctrines?.[1] ?? b.doctrine ?? defaultDoctrine(b)];
  const F: [Fighter, Fighter] = [
    makeFighter(a, 0, docs[0]),
    makeFighter(b, 1, docs[1]),
  ];
  F.forEach((f, i) => {
    const r = opt.startHp?.[i];
    if (r !== undefined) f.hp = Math.max(1, Math.round(f.maxHp * r));
    // 일관성이 낮으면 그날그날 읽는 감이 들쭉날쭉하다
    f.mind.form = (rng.next() - 0.5) * (21 - f.g.hidden.consistency) * 0.06;
  });
  // 위압감: 마주 서는 것만으로 기세가 꺾인다
  for (const f of F) {
    const o = F[1 - f.idx];
    const d = Math.max(0, effectiveAttr(f.g, "aura") - effectiveAttr(o.g, "comp"));
    o.momentum = Math.max(0, o.momentum - (d / 2) * (effectiveAttr(o.g, "grit") < 10 ? 2 : 1));
  }
  const events: CombatEvent[] = [];
  const emit = (e: CombatEvent) => events.push(e);
  let dist = 2;
  let crowd = C.crowdStart;
  let stall = 0;
  let ended: { winner: Fighter; loser: Fighter; reason: EndReason; crit: boolean } | null = null;
  let round = 0;

  // 훈련전은 관중 없이 치른다
  const hasCrowd = BALANCE.rules[opt.rule].crowd;
  const addCrowd = (delta: number, by?: Fighter) => {
    if (delta === 0 || !hasCrowd) return;
    const before = crowd;
    crowd = Math.max(0, Math.min(100, crowd + delta));
    if (by) by.crowdContribution += crowd - before;
    emit({ t: "crowd", delta: crowd - before, value: crowd });
  };
  const opp = (f: Fighter) => F[1 - f.idx];
  const reach = (f: Fighter) => Math.max(f.main.range, 0);
  const hpRatio = (f: Fighter) => f.hp / f.maxHp;

  emit({ t: "start", a: a.id, b: b.id, rule: opt.rule });
  emit({ t: "highlight", kind: "entrance", actors: [a.id, b.id] });

  const applyStatus = (target: Fighter, status: string, turns: number) => {
    const immuneByStance = target.statuses.has("earth_stance") && ["prone", "bound", "stun"].includes(status);
    const immuneJaw = status === "stun" && target.g.traits.includes("iron_jaw");
    let resisted = immuneByStance || immuneJaw;
    if (status === "bound" && target.helmet?.netResist && rng.chance(target.helmet.netResist)) resisted = true;
    if (status === "bound" && target.g.race === "minotaur") turns = Math.ceil(turns / 2);
    if (!resisted) target.statuses.set(status, Math.max(target.statuses.get(status) ?? 0, turns));
    emit({ t: "status", target: target.g.id, status, turns, resisted: resisted || undefined });
  };

  const dealDamage = (att: Fighter, def: Fighter, amount: number) => {
    let dmg = amount;
    if (def.statuses.has("adapt")) dmg = Math.round(dmg * 0.5);
    if (def.stance === "bulwark") dmg = Math.round(dmg * 0.9);
    else if (def.stance === "assault") dmg = Math.round(dmg * 1.05);
    if (def.wardHp > 0) {
      const absorbed = Math.min(def.wardHp, dmg);
      def.wardHp -= absorbed;
      dmg -= absorbed;
    }
    def.hp -= dmg;
    def.damageTaken += dmg;
    att.damageDealt += dmg;
    if (def.hp <= 0 && def.hasPassive("skill.unbreakable") && !def.usedUnbreakable) {
      def.hp = 1;
      def.usedUnbreakable = true;
      emit({ t: "unbreakable", actor: def.g.id });
    }
    return dmg;
  };

  interface AttackSpec {
    skill?: SkillDef;
    weapon: WeaponDef;
    mult: number;
    hitMod: number;
    pierceAdd: number;
    critAdd: number;
    autoHit: boolean;
    magicDamage?: number;
    counter?: boolean;
  }

  /** 한 합의 수 싸움. 근접 공격이고 방어자가 움직일 수 있을 때만 */
  const canExchange = (def: Fighter, spec: AttackSpec): boolean =>
    spec.magicDamage === undefined && !spec.autoHit && !spec.counter && !spec.skill?.ranged && spec.weapon.class !== "throwing"
    && !["bound", "prone", "stun", "grappled"].some((s) => def.statuses.has(s));

  const runExchange = (att: Fighter, def: Fighter, spec: AttackSpec): { outcome: Outcome; motion: Motion; adv: number; dir: Dir } => {
    const fixed = spec.skill ? SKILL_MOTION[spec.skill.id] : undefined;
    const feintBias = att.stance === "harass" || att.stance === "showman" ? 0.05 : 0;
    // def.book = 방어자가 공격자에 대해 본 것 = 공격자가 보여 준 버릇
    const plan = planAttack(rng, att.mind, att.habits, def.book, att.book, spec.weapon.class, fixed, def.mind.read, feintBias);
    if (plan.feint) att.sta = Math.max(0, att.sta - 4);
    const pressure = Math.max(0, attr(att, "aura") - attr(def, "comp")) * 0.06
      + (hpRatio(def) < 0.3 && def.mind.comp < 12 ? (12 - def.mind.comp) * 0.05 : 0)
      + (def.statuses.has("exposed") ? 0.5 : 0);
    const guard = planGuard(rng, def.mind, def.habits, def.book, att.mind.feint, pressure, plan.feint === "ok" ? plan.shown : undefined);
    let outcome = outcomeOf(rng, plan, guard, attr(def, "rea"));
    // 곡검: 방패를 돌아 들어가 막기에 완전히 걸리지 않는다
    if (outcome === "parry" && guard.guard === 0 && spec.weapon.blockBypass) outcome = "neutral";
    const c = plan.dir === 3 ? -1 : cell(plan.dir, plan.motion);
    const seen = c >= 0 ? def.seenN[c] : 0;
    if (c >= 0) {
      def.book.atk[c] += learnWeight(def.mind.adapt);
      def.seenN[c]++;
    }
    att.book.guard[guard.guard] += learnWeight(att.mind.adapt);
    emit({
      t: "exchange", actor: att.g.id, target: def.g.id, dir: plan.dir, motion: plan.motion, guardDir: guard.dir, guard: guard.guard,
      outcome, feint: plan.feint, improv: plan.improv || undefined, seen,
    });
    return { outcome, motion: plan.motion, adv: ADV[plan.motion][guard.guard], dir: plan.dir };
  };

  const counters = (x: Fighter, y: Fighter) => getStyle(x.g.style).counters.includes(y.g.style);

  const resolveHit = (att: Fighter, def: Fighter, spec: AttackSpec, allowRiposte = true): void => {
    const w = spec.weapon;
    const magic = spec.magicDamage !== undefined;
    const ex = allowRiposte && canExchange(def, spec) ? runExchange(att, def, spec) : undefined;
    if (ex?.outcome === "parry") {
      // 완전 방어: 피해 없음, 공격자는 자세가 흐트러지고 방어자는 되받아칠 틈을 본다
      emit({ t: "attack", actor: att.g.id, target: def.g.id, skill: spec.skill?.id, hit: false, blocked: true, crit: false, damage: 0, part: "chest", hpAfter: def.hp, maxHp: def.maxHp, magic, parried: true });
      att.statuses.set("exposed", Math.max(att.statuses.get("exposed") ?? 0, 1));
      def.momentum = Math.min(100, def.momentum + 6);
      att.momentum = Math.max(0, att.momentum - 4 - Math.max(0, attr(def, "aura") - attr(att, "comp")) / 3);
      addCrowd(2, def);
      if (dist <= reach(def) && rng.chance(0.2 + attr(def, "timing") * 0.04)) {
        resolveHit(def, att, { ...basicSpec(def), counter: true }, false);
      }
      return;
    }
    let hitChance = C.baseHit + (attr(att, "acc") - attr(def, "eva")) * C.hitPerAttrDiff;
    if (ex) hitChance += affinityHit(w.class, ex.motion) + (ex.outcome === "clean" ? 0.15 : 0) + (ex.dir === 3 ? -0.15 : 0)
      // 타이밍: 상대 호흡이 끊기는 순간에 들어간다
      + (attr(att, "timing") - 10) * 0.012;
    // 마무리 본능: 빈사·무너진 상대를 놓치지 않는다 (낮으면 오히려 머뭇거린다)
    const finishing = hpRatio(def) < 0.3 || def.statuses.has("exposed");
    if (finishing) hitChance += attr(att, "kill") * 0.01 - (attr(att, "kill") <= 6 ? 0.1 : 0);
    hitChance += (att.g.prof[w.class] - 10) * 0.01 + spec.hitMod;
    if (counters(att, def)) hitChance += C.counterHit;
    if (counters(def, att)) hitChance -= C.counterEvade;
    if (att.momentum >= 80) hitChance += 0.1;
    hitChance += (att.perf - 1) * 0.5 - (def.perf - 1) * 0.3;
    if (attr(att, "str") < w.reqStr) hitChance -= 0.1;
    if (att.sta <= 0) hitChance -= C.exhaustedPenalty;
    if (att.statuses.has("blinded")) hitChance -= 0.4;
    if (def.statuses.has("evade_up")) hitChance -= 0.3;
    if (def.statuses.has("wind_step")) hitChance -= 0.4;
    if (def.statuses.has("bound")) hitChance += 0.25;
    if (spec.magicDamage === undefined) hitChance += FOCUS_HIT[att.doctrine.focus];
    if (def.statuses.has("prone")) hitChance += 0.15;
    if (def.statuses.has("exposed")) hitChance += 0.2;
    if (def.g.race === "elf") hitChance -= 0.1;
    // 무거운 갑옷은 회피를 어렵게 한다 (드워프는 강철 뼈로 무시)
    if (def.g.race !== "dwarf") hitChance += (def.armor?.agiPenalty ?? 0) * C.heavyArmorHit;
    if (def.g.traits.includes("shadow") && round <= 3) hitChance -= 0.2;
    hitChance = Math.max(C.hitMin, Math.min(C.hitMax, hitChance));

    const part = rng.weighted(spec.magicDamage !== undefined ? PART_WEIGHTS : FOCUS_PARTS[att.doctrine.focus]);
    const hit = spec.autoHit || ex?.outcome === "break" || rng.chance(hitChance);
    if (!hit) {
      emit({ t: "attack", actor: att.g.id, target: def.g.id, skill: spec.skill?.id, hit: false, blocked: false, crit: false, damage: 0, part, hpAfter: def.hp, maxHp: def.maxHp, magic, counter: spec.counter });
      def.momentum = Math.min(100, def.momentum + 4);
      att.momentum = Math.max(0, att.momentum - 2);
      if (allowRiposte && def.statuses.has("riposte") && dist <= reach(def)) {
        def.statuses.delete("riposte");
        resolveHit(def, att, basicSpec(def), false);
      }
      return;
    }

    let blocked = false;
    // 방향을 틀렸거나 방어가 무너졌으면 막기 판정이 없다
    const openHit = ex?.outcome === "clean" || ex?.outcome === "break";
    if (!magic && !openHit && !def.statuses.has("bound") && !def.statuses.has("prone")) {
      let blockChance = attr(def, "blk") * C.blockPerBlk + (def.shield?.block ?? 0);
      if (def.statuses.has("guard")) blockChance += 0.5;
      if (def.statuses.has("earth_stance")) blockChance += 0.2;
      blockChance -= w.blockBypass ?? 0;
      blocked = rng.chance(Math.max(0, Math.min(0.85, blockChance)));
    }

    let raw: number;
    let critChance = attr(att, "acc") * C.critPerAcc + w.crit + spec.critAdd + (att.doctrine.focus === "head" ? 0.1 : 0);
    if (ex?.outcome === "clean" && ex.adv > 0) critChance += 0.15;
    if (magic) {
      const focusBonus = (att.main.magicBonus ?? 0) + (att.off?.magicBonus ?? 0);
      raw = spec.magicDamage! * (1 + attr(att, "mag") / 20) * (1 + focusBonus);
      critChance *= 0.5;
    } else {
      const profCoef = C.profBase + att.g.prof[w.class] * C.profPer;
      raw = w.damage * (1 + attr(att, "str") / 40) * profCoef * spec.mult;
      if (attr(att, "str") < w.reqStr) raw *= 0.85;
      if (att.statuses.has("flame")) raw *= 1.4;
      if (def.statuses.has("bound") && w.boundBonus) raw *= 1 + w.boundBonus;
      if (def.statuses.has("prone")) raw *= 1.3;
      if (att.statuses.has("arm_wound")) raw *= 0.75;
    }
    // 상성 배율: 클린 히트 ×1.25 (동작까지 유리하면 ×1.5), 방어 무너짐 ×0.9 (일부 막았지만 상성으로 밀림)
    if (ex?.outcome === "clean") raw *= ex.adv > 0 ? 1.5 : 1.25;
    else if (ex?.outcome === "break") raw *= 0.9;
    if (finishing) raw *= 1 + attr(att, "kill") * 0.015;
    if (att.g.race === "orc" && hpRatio(att) <= 0.3) raw *= 1.25;
    if (def.g.race === "orc" && hpRatio(def) <= 0.3) raw *= 1.1;
    const crit = !blocked && rng.chance(critChance);

    let defense = 0;
    if (!magic) {
      defense = (def.armor?.defense ?? 0) + (def.helmet?.defense ?? 0);
      if (part === "head" || part === "neck") defense = (def.helmet?.defense ?? 0) + 1;
      if (def.statuses.has("armor_break")) defense *= 0.7;
      defense *= 1 - Math.min(1, w.pierce + spec.pierceAdd);
    }
    // 방어구는 비율로 피해를 줄인다 (고정 차감은 약한 무기를 무력화하므로)
    let dmg = Math.max(1, raw * (1 - defense / (defense + C.armorScale)));
    if (crit) dmg *= C.critMult;
    if (blocked) dmg *= 1 - C.blockReduction;
    if (part === "head" || part === "neck") dmg *= 1.2;
    dmg *= att.perf;
    const dealt = dealDamage(att, def, Math.max(1, Math.round(dmg)));
    if (dealt > def.worstHit) { def.worstHit = dealt; def.worstPart = part; }

    emit({ t: "attack", actor: att.g.id, target: def.g.id, skill: spec.skill?.id, hit: true, blocked, crit, damage: dealt, part, hpAfter: Math.max(0, def.hp), maxHp: def.maxHp, magic, counter: spec.counter });
    att.momentum = Math.min(100, att.momentum + (crit ? 12 : blocked ? 2 : 6));
    def.momentum = Math.max(0, def.momentum - (crit ? 12 : blocked ? 2 : 6));
    if (ex?.outcome === "break" && def.hp > 0) def.statuses.set("exposed", Math.max(def.statuses.get("exposed") ?? 0, 2));
    if (crit) def.momentum = Math.max(0, def.momentum - Math.max(0, attr(att, "aura") - attr(def, "comp")) / 3);
    if (crit) {
      addCrowd(8, att);
      emit({ t: "highlight", kind: spec.skill?.id === "skill.execute" ? "execute" : "crit", actors: [att.g.id, def.g.id] });
    } else if (dealt > def.maxHp * 0.15) addCrowd(3, att);

    if (!blocked && !magic && def.hp > 0) {
      const focus = att.doctrine.focus;
      if (focus === "legs" && rng.chance(0.3)) applyStatus(def, "hobbled", 2);
      else if (focus === "arm" && rng.chance(0.3)) applyStatus(def, "arm_wound", 2);
      else if (focus === "head" && rng.chance(0.12 * (def.g.traits.includes("glass_jaw") ? 2 : 1))) applyStatus(def, "stun", 1);
    }
    if (!blocked && w.stun && def.hp > 0) {
      const p = w.stun * (def.g.traits.includes("glass_jaw") ? 2 : 1);
      if (rng.chance(p)) applyStatus(def, "stun", 1);
    }
    if (spec.skill?.apply && def.hp > 0 && !blocked && rng.chance(spec.skill.apply.chance)) {
      applyStatus(def, spec.skill.apply.status, spec.skill.apply.turns);
    }
    if (att.g.race === "orc" && hpRatio(att) <= 0.3 && !att.statuses.has("enraged")) {
      att.statuses.set("enraged", 99);
      emit({ t: "enrage", actor: att.g.id });
    }
  };

  const basicSpec = (f: Fighter): AttackSpec => ({ weapon: f.main, mult: 1, hitMod: 0, pierceAdd: 0, critAdd: 0, autoHit: false });

  const skillUsable = (f: Fighter, s: SkillDef): boolean => {
    if (s.kind === "passive") return false;
    if ((f.cooldowns.get(s.id) ?? 0) > 0) return false;
    if ((s.cost ?? 0) > f.sta) return false;
    if ((s.mana ?? 0) > f.mana) return false;
    const t = opp(f);
    if (s.requireShield && !f.shield) return false;
    if (s.requireNet && !f.netReady) return false;
    if (s.useAmmo && f.ammo <= 0) return false;
    if (s.requireTargetHpBelow !== undefined && hpRatio(t) > s.requireTargetHpBelow) return false;
    if (s.self && f.statuses.has(s.self.status)) return false;
    if (s.kind === "move") return dist === 0 && !f.statuses.has("bound");
    if (s.kind === "attack" || s.kind === "control") {
      if (s.apply && t.statuses.has(s.apply.status)) return false;
      if (s.closeIn) return dist > 0 && (s.fromAnyRange || dist === 1) && !f.statuses.has("bound");
      if (s.ranged) return true;
      if (s.useGrapple) return dist === 0;
      return dist <= reach(f);
    }
    return true;
  };

  const scoreSkill = (f: Fighter, s: SkillDef): number => {
    const t = opp(f);
    let score = 1.2;
    const st = f.stance;
    if (s.kind === "attack") score += st === "assault" ? 0.5 : st === "blitz" ? 0.8 : st === "bulwark" ? 0 : 0.3;
    if (s.kind === "control") score += st === "harass" ? 0.9 : 0.6;
    if (s.kind === "self") score += st === "bulwark" ? 0.7 : 0.1;
    if (s.kind === "move") score += st === "harass" ? 0.8 : 0;
    // 기술 순서: 앞일수록 먼저
    const idx = f.doctrine.order.indexOf(s.id);
    if (idx >= 0) score += (4 - idx) * 0.25;
    if (f.priorityStake === s.id) score += 5;
    if (s.id === "skill.catch_breath") score = f.sta < f.maxSta * 0.25 ? 3 : -1;
    if (s.id === "skill.taunt") score = st === "showman" ? 2.5 : hpRatio(f) > 0.6 && hpRatio(t) < 0.5 ? 1.2 : 0.2;
    if (s.id === "skill.flourish") score = 3;
    if (s.id === "skill.execute") score = 3;
    if (s.id === "skill.ward_rune" || s.id === "skill.iron_wall") score += hpRatio(f) < 0.5 ? 0.8 : 0;
    if (s.id === "skill.net_throw") score = 2.2;
    if (s.id === "skill.sand_throw") score = st === "showman" ? 0 : 1;
    if (s.race) score += 0.4;
    return score;
  };

  /** 사거리가 닿는 한에서 교범이 원하는 거리 */
  const desiredRange = (f: Fighter, styleRange: number): number => {
    const pref = f.doctrine.range;
    const want = pref === "style" ? styleRange : pref === "close" ? 0 : pref === "mid" ? 1 : 2;
    // 실제로 쓸 수 있는 원거리 수단이 남아 있을 때만 멀리 선다
    const hasRanged = f.g.skills.active.some((id) => {
      const sk = getSkill(id);
      return !!sk?.ranged && (sk.mana ?? 0) <= f.mana && (!sk.useAmmo || f.ammo > 0) && (!sk.requireNet || f.netReady);
    });
    return Math.min(want, hasRanged ? 2 : reach(f));
  };

  /** 조건 명령 판정. 숙련도와 판단력이 낮으면 상황을 늦게 알아챈다 */
  const checkRules = (f: Fighter): RuleAction | undefined => {
    const t = opp(f);
    const notice = Math.min(1, 0.4 + f.doctrine.familiarity / 200 + attr(f, "dec") / 50);
    for (let i = 0; i < f.doctrine.rules.length; i++) {
      const { cond, action } = f.doctrine.rules[i];
      const met =
        cond.type === "myHpBelow" ? hpRatio(f) * 100 <= cond.value
        : cond.type === "oppHpBelow" ? hpRatio(t) * 100 <= cond.value
        : cond.type === "staBelow" ? (f.sta / f.maxSta) * 100 <= cond.value
        : cond.type === "oppDisabled" ? t.statuses.has("bound") || t.statuses.has("prone") || t.statuses.has("stun")
        : round >= cond.value;
      if (!met || !rng.chance(notice)) continue;
      // 자세 변경은 한 번만. 기술·휴식·접근은 조건이 맞는 동안 계속 적용
      if (action.type === "stance") {
        if (f.firedRules.has(i)) continue;
        f.firedRules.add(i);
        f.stance = action.stance;
        emit({ t: "doctrine", actor: f.g.id, action });
        continue;
      }
      if (action.type === "skill") {
        if (f.priorityStake !== action.skill) emit({ t: "doctrine", actor: f.g.id, action });
        f.priorityStake = action.skill;
        return action;
      }
      if (!f.firedRules.has(i)) {
        f.firedRules.add(i);
        emit({ t: "doctrine", actor: f.g.id, action });
      }
      return action;
    }
    f.priorityStake = undefined;
    return undefined;
  };

  const act = (f: Fighter) => {
    const t = opp(f);
    const options: { score: number; run: () => void }[] = [];
    const noise = ((21 - attr(f, "dec")) / 20) * 0.8;
    const style = getStyle(f.g.style);
    const ruleAction = checkRules(f);
    if (ruleAction?.type === "rest") {
      const breath = getSkill("skill.catch_breath");
      if (f.g.skills.active.includes(breath.id) && skillUsable(f, breath)) useSkill(f, t, breath);
      else rest(f);
      return;
    }
    if (ruleAction?.type === "close" && dist > 0 && !f.statuses.has("bound")) {
      move(f, -1);
      return;
    }

    for (const sid of f.g.skills.active) {
      const s = getSkill(sid);
      if (!s || !skillUsable(f, s)) continue;
      options.push({ score: scoreSkill(f, s) + rng.next() * noise, run: () => useSkill(f, t, s) });
    }
    if (dist <= reach(f)) {
      const stanceMult = { assault: 1.35, balanced: 1, bulwark: 0.85, harass: 0.95, showman: 0.85, blitz: 1.15 }[f.stance];
      const aggr = (style.aggression + attr(f, "agg") / 40) * stanceMult;
      options.push({ score: 0.6 + aggr + rng.next() * noise, run: () => basicAttack(f, t) });
    }
    if (!f.statuses.has("bound")) {
      const desired = desiredRange(f, style.preferredRange);
      const keepAway = f.stance === "harass" ? 0.2 : 0;
      if (dist > Math.max(reach(f), desired)) options.push({ score: 1.5 + rng.next() * noise, run: () => move(f, -1) });
      else if (dist < desired) options.push({ score: 0.9 + keepAway + rng.next() * noise, run: () => move(f, 1) });
    }
    if (f.sta < f.maxSta * 0.15) options.push({ score: 1.4, run: () => rest(f) });
    if (options.length === 0) options.push({ score: 0, run: () => rest(f) });

    options.sort((x, y) => y.score - x.score);
    options[0].run();
  };

  const move = (f: Fighter, dir: -1 | 1, skill?: string) => {
    dist = Math.max(0, Math.min(2, dist + dir));
    f.sta = Math.max(0, f.sta - C.moveSta);
    emit({ t: "move", actor: f.g.id, dist, closer: dir < 0, skill });
  };

  const rest = (f: Fighter) => {
    f.sta = Math.min(f.maxSta, f.sta + f.maxSta * 0.15);
    emit({ t: "rest", actor: f.g.id });
  };

  const basicAttack = (f: Fighter, t: Fighter) => {
    f.sta = Math.max(0, f.sta - C.basicAttackSta * (1 + (f.armor?.staCost ?? 0)));
    resolveHit(f, t, basicSpec(f));
  };

  const useSkill = (f: Fighter, t: Fighter, s: SkillDef) => {
    f.sta = Math.max(0, f.sta - (s.cost ?? 0) * (1 + (f.armor?.staCost ?? 0)));
    f.mana -= s.mana ?? 0;
    f.cooldowns.set(s.id, (s.cd ?? 0) + 1);
    if (s.race && !f.usedRaceSkill) {
      f.usedRaceSkill = true;
      emit({ t: "highlight", kind: "race_skill", actors: [f.g.id] });
    }
    if (s.crowd) addCrowd(s.crowd, f);

    if (s.kind === "move") {
      move(f, 1, s.id);
      return;
    }
    if (s.kind === "self") {
      emit({ t: "self", actor: f.g.id, skill: s.id });
      if (s.self) {
        f.statuses.set(s.self.status, s.self.turns);
        if (s.self.status === "ward") f.wardHp = Math.round(f.maxHp * 0.2);
      }
      if (s.restoreSta) f.sta = Math.min(f.maxSta, f.sta + f.maxSta * s.restoreSta);
      if (s.selfMomentum) f.momentum = Math.min(100, f.momentum + s.selfMomentum);
      if (s.enemyMomentum) t.momentum = Math.max(0, t.momentum + s.enemyMomentum);
      return;
    }
    if (s.closeIn) {
      dist = 0;
      emit({ t: "move", actor: f.g.id, dist, closer: true, skill: s.id });
    }
    if (s.kind === "control") {
      if (s.requireNet) f.netReady = false;
      const chance = s.autoHit ? 1 : s.useGrapple
        ? 0.5 + (attr(f, "grp") - attr(t, "grp")) * 0.04
        : 0.6 + (attr(f, "acc") - attr(t, "eva")) * 0.03 + (s.magic ? 0.15 : 0);
      if (rng.chance(Math.max(0.1, Math.min(0.95, chance)))) {
        if (s.apply) applyStatus(t, s.apply.status, s.apply.turns);
        if (s.id === "skill.grapple") f.statuses.set("grappling", 2);
      } else {
        emit({ t: "attack", actor: f.g.id, target: t.g.id, skill: s.id, hit: false, blocked: false, crit: false, damage: 0, part: "chest", hpAfter: t.hp, maxHp: t.maxHp, magic: !!s.magic });
      }
      return;
    }
    // attack
    if (s.useAmmo) f.ammo--;
    const weapon = s.useAmmo && f.off?.ammo ? f.off : f.main;
    const hits = s.hits ?? 1;
    for (let i = 0; i < hits && t.hp > 0; i++) {
      resolveHit(f, t, {
        skill: s, weapon, mult: s.mult ?? 1, hitMod: s.hitMod ?? 0, pierceAdd: s.pierceAdd ?? 0,
        critAdd: s.critAdd ?? 0, autoHit: !!s.autoHit, magicDamage: s.magicDamage,
      });
    }
  };

  /** 턴 시작 처리. false면 이번 턴 행동 불가 */
  const startTurn = (f: Fighter): boolean => {
    for (const [k, v] of f.cooldowns) f.cooldowns.set(k, Math.max(0, v - 1));
    let canAct = true;
    if (f.statuses.has("poison")) {
      const dmg = Math.max(1, Math.round(f.maxHp * 0.03));
      f.hp -= dmg;
      f.damageTaken += dmg;
      opp(f).damageDealt += dmg;
      emit({ t: "poison", target: f.g.id, damage: dmg, hpAfter: Math.max(0, f.hp) });
    }
    if (f.statuses.has("stun")) { emit({ t: "skip", actor: f.g.id, reason: "stun" }); canAct = false; }
    else if (f.statuses.has("grappled")) {
      const dmg = Math.max(1, Math.round(f.maxHp * 0.02));
      dealDamage(opp(f), f, dmg);
      emit({ t: "skip", actor: f.g.id, reason: "grappled" });
      canAct = false;
    } else if (f.statuses.has("prone")) { emit({ t: "skip", actor: f.g.id, reason: "prone" }); canAct = false; }
    for (const [k, v] of [...f.statuses]) {
      if (v - 1 <= 0) f.statuses.delete(k);
      else f.statuses.set(k, v - 1);
    }
    if (!f.netReady && f.off?.id === "net" && dist >= 1 && canAct && rng.chance(0.5)) f.netReady = true;
    if (hpRatio(f) < 0.3) f.momentum = Math.max(0, f.momentum - 3);
    return canAct;
  };

  const checkKo = (): boolean => {
    for (const f of F) {
      if (f.hp <= 0) {
        const lastCrit = [...events].reverse().find((e) => e.t === "attack" && e.target === f.g.id);
        ended = { winner: opp(f), loser: f, reason: "ko", crit: !!(lastCrit && lastCrit.t === "attack" && lastCrit.crit) };
        f.hp = 0;
        emit({ t: "ko", target: f.g.id, crit: ended.crit });
        addCrowd(10, opp(f));
        return true;
      }
    }
    return false;
  };

  const checkSurrender = (f: Fighter): boolean => {
    if (!f.doctrine.allowSurrender || f.momentum > C.surrenderMomentum || hpRatio(f) > C.surrenderHpRatio) return false;
    const grit = attr(f, "grit");
    if (grit >= 20) return false;
    if (rng.chance((20 - grit) * C.surrenderPerGrit)) {
      emit({ t: "surrender", actor: f.g.id });
      addCrowd(-8, f);
      ended = { winner: opp(f), loser: f, reason: "surrender", crit: false };
      return true;
    }
    return false;
  };

  for (round = 1; round <= C.maxRounds && !ended; round++) {
    emit({ t: "round", round });
    for (const f of F) f.mind.late = (f.mind.adapt - 8) * 0.01 * Math.min(round, 20);
    const init = F.map((f) => {
      // 공격권: 타이밍이 가장 크고, 무너진 쪽은 선공을 잡기 어렵다
      let v = attr(f, "timing") + attr(f, "rea") / 2 + attr(f, "agi") / 2 - f.main.speed * 2 + rng.next() * 8 - (f.statuses.has("exposed") ? 6 : 0)
        // 상대가 무너졌거나 지쳤으면 타이밍 좋은 쪽이 파고든다
        + (F[1 - f.idx].statuses.has("exposed") || F[1 - f.idx].sta <= 0 ? attr(f, "timing") * 0.6 : 0);
      if (round === 1 && f.g.race === "beastkin") v += 5;
      return v;
    });
    const order = init[0] >= init[1] ? [F[0], F[1]] : [F[1], F[0]];
    const dmgBefore = F[0].damageTaken + F[1].damageTaken;

    for (const f of order) {
      if (ended) break;
      const canAct = startTurn(f);
      if (checkKo()) break;
      if (checkSurrender(f)) break;
      if (canAct) act(f);
      if (checkKo()) break;
      // 단검처럼 빠른 무기는 추가 공격 기회
      if (canAct && f.main.speed <= 1 && dist <= reach(f) && rng.chance(0.2)) {
        basicAttack(f, opp(f));
        if (checkKo()) break;
      }
    }
    for (const f of F) f.sta = Math.min(f.maxSta, f.sta + attr(f, "end") * C.staRegenPerEnd + 2);

    if (F[0].damageTaken + F[1].damageTaken === dmgBefore) {
      stall++;
      if (stall >= 5) addCrowd(-3);
    } else stall = 0;
  }

  if (!ended) {
    emit({ t: "timeout" });
    const score = (f: Fighter) => f.damageDealt + f.crowdContribution * 0.5;
    const w = score(F[0]) >= score(F[1]) ? F[0] : F[1];
    ended = { winner: w, loser: opp(w), reason: "timeout", crit: false };
  }
  const { winner, loser, reason, crit } = ended as { winner: Fighter; loser: Fighter; reason: EndReason; crit: boolean };

  let died: Id | null = null;
  if (reason !== "timeout" && !(reason === "surrender" && opt.rule === "training")) {
    const medical = opt.medicalLevel?.[loser.idx] ?? 0;
    let deathChance: number;
    if (opt.rule === "training") deathChance = crit ? D.trainingCritKo : D.trainingKo;
    else if (opt.rule === "missio") {
      deathChance = D.missioBase - loser.g.popularity * D.missioPerPopularity - Math.max(0, loser.crowdContribution) * D.missioPerCrowd;
      if (loser.hasPassive("skill.crowd_hand")) deathChance -= D.missioCrowdHand;
      deathChance = Math.max(D.missioMin, Math.min(D.missioMax, deathChance)) * (opt.missioDeathMult ?? 1);
      emit({ t: "highlight", kind: "missio", actors: [loser.g.id] });
    } else {
      deathChance = Math.max(D.suddenMin, D.suddenBase - medical * D.suddenPerMedical);
    }
    const isDead = !opt.noDeath && rng.chance(deathChance);
    emit({ t: "verdict", target: loser.g.id, rule: opt.rule, deathChance, died: isDead });
    if (isDead) {
      died = loser.g.id;
      emit({ t: "highlight", kind: "death", actors: [loser.g.id, winner.g.id] });
    }
  }
  if (!died && hpRatio(winner) <= 0.3) emit({ t: "highlight", kind: "comeback", actors: [winner.g.id] });
  emit({ t: "end", winner: winner.g.id, loser: loser.g.id, reason });

  const injuries: Record<Id, Injury | undefined> = {};
  for (const f of F) {
    if (f.g.id === died) continue;
    injuries[f.g.id] = rollInjury(rng, f, reason === "ko" && f === loser, BALANCE.rules[opt.rule].injuryMult);
  }

  return {
    events,
    winnerId: winner.g.id,
    loserId: loser.g.id,
    reason,
    died,
    rounds: Math.min(round, C.maxRounds),
    crowd,
    damageDealt: { [a.id]: F[0].damageDealt, [b.id]: F[1].damageDealt },
    crowdContribution: { [a.id]: F[0].crowdContribution, [b.id]: F[1].crowdContribution },
    injuries,
    hpLeft: { [a.id]: Math.max(0, F[0].hp / F[0].maxHp), [b.id]: Math.max(0, F[1].hp / F[1].maxHp) },
  };
}


function rollInjury(rng: Rng, f: Fighter, knockedOut: boolean, mult: number): Injury | undefined {
  const proneMult = f.g.hidden.injuryProne / 10 * (f.g.traits.includes("injury_prone") ? 1.5 : 1);
  const p = ((f.damageTaken / f.maxHp) * 0.35 * proneMult + (knockedOut ? 0.25 : 0)) * mult;
  if (!rng.chance(p)) return undefined;
  // 드워프는 강철 뼈 덕에 크게 다치는 일이 적다
  const tough = getRace(f.g.race).id === "dwarf" ? 0.6 : 1;
  const severe = (0.05 + (knockedOut ? 0.08 : 0)) * tough;
  const moderate = (0.2 + (knockedOut ? 0.15 : 0)) * tough;
  const roll = rng.next();
  const tier = roll < severe ? "severe" : roll < severe + moderate ? "moderate" : "minor";
  return makeInjury(rng, tier, areaOf(f.worstPart));
}
