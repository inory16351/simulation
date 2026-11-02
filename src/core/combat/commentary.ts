import { getSkill, getWeapon } from "../data";
import { josa } from "../josa";
import { Rng } from "../rng";
import { STANCE_NAMES } from "../tactics";
import type { Gladiator, Id, WeaponClassId } from "../types";
import type { BodyPart, CombatEvent } from "./sim";
import { DIR_NAMES, GUARD_NAMES, MOTION_NAMES } from "./exchange";

export type GoreLevel = "strong" | "normal" | "mild";

export interface CommentaryLine {
  round: number;
  text: string;
  tone: "normal" | "hit" | "crit" | "blood" | "crowd" | "system" | "death";
  event: CombatEvent;
}

const PART_NAMES: Record<BodyPart, string> = {
  head: "머리", neck: "목", chest: "가슴", side: "옆구리", shoulder: "어깨", arm: "팔", thigh: "허벅지", leg: "정강이",
};

const STATUS_TEXT: Record<string, (n: string) => string> = {
  stun: (n) => `${josa(n, "이/가")} 비틀거리며 정신을 놓았습니다.`,
  bound: (n) => `그물이 ${josa(n, "을/를")} 휘감았습니다!`,
  prone: (n) => `${josa(n, "이/가")} 모래 위로 나뒹굽니다.`,
  grappled: (n) => `${josa(n, "이/가")} 목을 졸린 채 꼼짝 못 합니다.`,
  blinded: (n) => `${n}의 눈에 모래가 들어갔습니다. 앞이 안 보입니다.`,
  armor_break: (n) => `${n}의 갑옷이 쩍 갈라졌습니다.`,
  poison: (n) => `${n}의 상처에서 검게 독이 번집니다.`,
  weakened: (n) => `${n}의 팔다리에서 힘이 쭉 빠집니다.`,
  hobbled: (n) => `${josa(n, "이/가")} 다리를 절뚝입니다.`,
  arm_wound: (n) => `${n}의 무기 든 팔이 축 늘어집니다.`,
};

const SELF_TEXT: Record<string, string> = {
  "skill.iron_wall": "방패를 땅에 박고 몸을 웅크립니다",
  "skill.riposte": "칼끝을 낮추고 상대의 빈틈을 노립니다",
  "skill.parry": "발놀림을 가볍게 하며 공격을 흘려낼 채비를 합니다",
  "skill.catch_breath": "한 걸음 물러나 거친 숨을 고릅니다",
  "skill.taunt": "무기를 내리고 관중을 향해 두 팔을 벌립니다",
  "skill.flame_weapon": "칼날을 손으로 훑자 불길이 확 일어납니다",
  "skill.ward_rune": "허공에 룬을 그립니다. 푸른 빛이 몸을 감쌉니다",
  "skill.adapt": "상대의 수를 읽고 자세를 바꿉니다",
  "skill.war_cry": "짐승 같은 함성을 내지릅니다",
  "skill.wind_step": "몸놀림이 바람처럼 가벼워집니다",
  "skill.earth_stance": "두 발을 모래에 단단히 박습니다. 이제 꿈쩍도 않습니다",
  "skill.war_roar": "포효합니다. 상대의 눈빛이 흔들립니다",
};

function pickLine(rng: Rng, lines: string[]): string {
  return rng.pick(lines);
}

/** 전투 이벤트 로그를 텍스트 중계로 바꾼다. 같은 seed면 같은 문장이 나온다. */
export function buildCommentary(
  events: CombatEvent[],
  fighters: Record<Id, Gladiator>,
  seed: number,
  gore: GoreLevel = "strong",
): CommentaryLine[] {
  const rng = new Rng(seed ^ 0x5bd1e995);
  const out: CommentaryLine[] = [];
  const name = (id: Id) => fighters[id]?.name ?? "?";
  let round = 0;
  const start = events.find((x) => x.t === "start");
  const noCrowd = start?.t === "start" && start.rule === "training";
  const push = (text: string, tone: CommentaryLine["tone"], event: CombatEvent) => {
    // 훈련전은 관중이 없으니 관중 이야기를 뺀다
    const t = noCrowd ? withoutCrowd(text) : text;
    if (t) out.push({ round, text: t, tone, event });
  };

  for (const e of events) {
    switch (e.t) {
      case "start": {
        const a = fighters[e.a], b = fighters[e.b];
        push(noCrowd
          ? `텅 빈 연습장. ${josa(a.name, "과/와")} ${josa(b.name, "이/가")} 마주 섭니다. 들리는 건 교관의 호루라기뿐입니다.`
          : `${josa(a.name, "과/와")} ${josa(b.name, "이/가")} 모래 위에 섰습니다. 관중석이 술렁입니다.`, "system", e);
        break;
      }
      case "round":
        round = e.round;
        break;
      case "move": {
        const n = name(e.actor);
        if (e.skill && e.skill !== "skill.disengage") break; // 돌진류는 공격 문장에서 처리
        if (e.closer) push(pickLine(rng, [`${josa(n, "이/가")} 거리를 좁힙니다.`, `${josa(n, "이/가")} 모래를 박차고 다가섭니다.`, `${josa(n, "이/가")} 천천히 원을 그리며 다가갑니다.`]), "normal", e);
        else push(pickLine(rng, [`${josa(n, "이/가")} 뒤로 물러나 거리를 벌립니다.`, `${josa(n, "은/는")} 한 걸음 빠지며 간격을 잽니다.`]), "normal", e);
        break;
      }
      case "exchange": {
        const a = name(e.actor), d = name(e.target);
        const dir = DIR_NAMES[e.dir], mo = MOTION_NAMES[e.motion], gd = GUARD_NAMES[e.guard];
        const lead = e.improv
          ? (e.dir === 3 ? "사선으로 꺾여 드는 변칙 수! " : "본 적 없는 수입니다! ")
          : e.feint === "ok" ? `${josa(a, "이/가")} 치는 척하다 방향을 꺾습니다! `
          : e.feint === "fail" ? `${a}의 속임수가 읽혔습니다. `
          : "";
        if (e.outcome === "parry") {
          const how = [
            "정면으로 받아낸 칼날이 튕겨 나갑니다.",
            "칼날을 비스듬히 흘려 보냅니다.",
            "몸을 비틀어 헛치게 만듭니다. 크게 빈틈이 났습니다!",
          ][e.guard];
          const nth = ["", "", "", "세", "네", "다섯"][e.seen + 1] ?? "또 한";
          const read = e.seen >= 2
            ? `${nth} 번째입니다. ${josa(d, "은/는")} 이미 그 ${josa(mo, "을/를")} 기다리고 있었습니다. `
            : `${josa(d, "이/가")} ${dir} ${josa(mo, "을/를")} 읽었습니다! `;
          push(lead + read + how, "hit", e);
        } else if (e.outcome === "break") {
          push(`${lead}${josa(mo, "이/가")} ${d}의 ${josa(gd, "을/를")} 그대로 짓누릅니다. 자세가 무너졌습니다!`, "hit", e);
        } else if (e.outcome === "clean" && (lead || rng.chance(0.3))) {
          push(lead.trim() || `${josa(d, "은/는")} ${DIR_NAMES[e.guardDir]}을 막았지만, ${a}의 ${josa(mo, "은/는")} ${dir}으로 파고듭니다.`, "normal", e);
        } else if (e.outcome === "late" && rng.chance(0.4)) {
          push(`${lead}방향을 잘못 읽었습니다… ${josa(d, "이/가")} 늦게나마 몸을 틉니다.`, "normal", e);
        } else if (lead) push(lead.trim(), "normal", e);
        break;
      }
      case "attack": {
        if (e.parried) break; // 수 싸움 문장으로 이미 말했다
        const a = name(e.actor), d = name(e.target);
        const skill = e.skill ? getSkill(e.skill) : undefined;
        const prefix = (e.counter ? "반격! " : "") + (skill ? `${a}의 ${skill.name}! ` : "");
        const part = PART_NAMES[e.part];
        if (!e.hit) {
          // 기술 이름으로 이미 공격자를 말했으면 문장에서 이름을 다시 쓰지 않는다
          const who = skill ? "" : `${a}의 `;
          push(prefix + pickLine(rng, [
            `${who}공격이 허공을 가릅니다.`,
            `${josa(d, "이/가")} 몸을 틀어 피했습니다!`,
            skill ? "하지만 빗나갔습니다. 관중석에서 탄식이 터집니다." : `${a}의 공격이 빗나갔습니다. 관중석에서 탄식이 터집니다.`,
          ]), "normal", e);
          break;
        }
        if (e.blocked) {
          push(prefix + pickLine(rng, [
            `${josa(d, "이/가")} 막아냈습니다! 쇳소리가 경기장을 울립니다.`,
            `${a}의 공격이 방패에 튕겨 나갑니다.`,
          ]) + ` (${d} ${e.hpAfter}/${e.maxHp})`, "normal", e);
          break;
        }
        const ratio = e.damage / e.maxHp;
        const hpText = ` (${d} ${e.hpAfter}/${e.maxHp})`;
        const weapon = weaponWord(fighters[e.actor], e.skill);
        if (e.magic) {
          push(`${prefix}${skill ? "" : `${a}의 `}주술이 ${josa(d, "을/를")} 덮칩니다! 살 타는 냄새가 퍼집니다.${hpText}`, e.crit ? "crit" : "hit", e);
        } else if (e.crit) {
          push(prefix + bloodLine(rng, gore, a, d, part, "crit", weapon, !skill) + hpText, "crit", e);
        } else if (ratio > 0.12) {
          push(prefix + bloodLine(rng, gore, a, d, part, "heavy", weapon, !skill) + hpText, "blood", e);
        } else {
          push(prefix + bloodLine(rng, gore, a, d, part, "light", weapon, !skill) + hpText, "hit", e);
        }
        break;
      }
      case "status": {
        const n = name(e.target);
        if (e.resisted) push(`${josa(n, "은/는")} 버텨냈습니다!`, "normal", e);
        else if (STATUS_TEXT[e.status]) push(STATUS_TEXT[e.status](n), "hit", e);
        break;
      }
      case "self": {
        const s = getSkill(e.skill);
        push(`${josa(name(e.actor), "이/가")} ${SELF_TEXT[e.skill] ?? "자세를 가다듬습니다"}. (${s.name})`, "normal", e);
        break;
      }
      case "crowd":
        if (e.delta >= 8) push(pickLine(rng, ["관중이 일어섭니다! 함성이 터집니다.", "환호성이 경기장을 뒤흔듭니다!"]), "crowd", e);
        else if (e.delta <= -8) push(pickLine(rng, ["야유가 쏟아집니다.", "관중석에서 돌멩이가 날아듭니다."]), "crowd", e);
        break;
      case "rest":
        push(`${josa(name(e.actor), "이/가")} 숨을 몰아쉽니다.`, "normal", e);
        break;
      case "poison":
        push(`독이 돕니다. ${josa(name(e.target), "이/가")} 검은 피를 토합니다. (${e.hpAfter})`, "blood", e);
        break;
      case "skip": {
        const n = name(e.actor);
        const t = { stun: "아직 정신을 차리지 못했습니다", prone: "모래를 짚고 겨우 일어납니다", grappled: "붙잡힌 채 버둥거립니다" }[e.reason];
        push(`${josa(n, "이/가")} ${t}.`, "normal", e);
        break;
      }
      case "enrage":
        push(`▶ 피의 격노 — ${name(e.actor)}의 눈이 붉게 물듭니다.`, "crit", e);
        break;
      case "unbreakable":
        push(`▶ 불굴 — 쓰러지나 싶던 ${josa(name(e.actor), "이/가")} 피투성이가 된 채 다시 일어섭니다!`, "crit", e);
        break;
      case "doctrine": {
        const n = name(e.actor);
        const a = e.action;
        const text =
          a.type === "stance" ? `${josa(n, "이/가")} 자세를 ${josa(STANCE_NAMES[a.stance], "으로/로")} 바꿉니다.`
          : a.type === "skill" ? `${josa(n, "이/가")} 기회를 노립니다. 노리는 건 ${getSkill(a.skill)?.name ?? "한 방"}입니다.`
          : a.type === "rest" ? `${josa(n, "이/가")} 교범대로 물러나 숨을 돌립니다.`
          : `${josa(n, "이/가")} 거리를 좁혀 들어갑니다.`;
        push(`▶ 교범 — ${text}`, "system", e);
        break;
      }
      case "surrender":
        push(`${josa(name(e.actor), "이/가")} 무릎을 꿇고 손가락을 들어 올립니다. 항복입니다.`, "system", e);
        break;
      case "ko":
        push(e.crit
          ? `${josa(name(e.target), "이/가")} 피를 뿜으며 쓰러졌습니다! 모래가 검붉게 젖어 듭니다.`
          : `${josa(name(e.target), "이/가")} 쓰러졌습니다. 관중이 숨을 죽입니다…`, "death", e);
        break;
      case "timeout":
        push("시간 종료! 심판들이 판정에 들어갑니다.", "system", e);
        break;
      case "verdict": {
        const n = name(e.target);
        if (e.rule === "missio") {
          push(`▶ 미시오 판정 — 주최자가 관중을 둘러봅니다… (사망 확률 ${Math.round(e.deathChance * 100)}%)`, "system", e);
          push(e.died
            ? `엄지가 아래를 향합니다. ${josa(opponentOf(events, e.target, fighters), "이/가")} ${n}의 숨통을 끊습니다. 피가 모래 위로 쏟아집니다.`
            : `엄지가… 위로! ${josa(n, "은/는")} 살아남았습니다.`, e.died ? "death" : "crowd", e);
        } else if (e.died) {
          push(e.rule === "sudden"
            ? `사투에 자비는 없습니다. ${josa(opponentOf(events, e.target, fighters), "이/가")} 마지막 일격을 내리꽂습니다. ${josa(n, "은/는")} 다시 일어나지 못했습니다.`
            : `${josa(n, "이/가")} 일어나지 못합니다. 의무병이 뛰어들었지만 이미 숨이 끊긴 뒤였습니다.`, "death", e);
        } else if (e.rule === "sudden") {
          push(`기적입니다! 아직 숨이 붙어 있습니다. ${josa(n, "이/가")} 들것에 실려 나갑니다.`, "crowd", e);
        }
        break;
      }
      case "end": {
        const reason = { ko: "KO", surrender: "항복", timeout: "판정" }[e.reason];
        push(`■ ${name(e.winner)} 승리 (${reason})`, "system", e);
        break;
      }
      case "highlight":
        break;
    }
  }
  return out;
}

const CROWD_WORDS = /관중|환호|야유|탄식/;

/** 관중이 나오는 문장만 지운다. 남는 게 없으면 빈 문자열 */
function withoutCrowd(text: string): string {
  return text
    .split(/(?<=[.!…])\s+/)
    .filter((sentence) => !CROWD_WORDS.test(sentence))
    .join(" ")
    .trim();
}

function opponentOf(events: CombatEvent[], id: Id, fighters: Record<Id, Gladiator>): string {
  const start = events.find((e) => e.t === "start");
  if (!start || start.t !== "start") return "?";
  return fighters[start.a === id ? start.b : start.a].name;
}

type WeaponKind = "cut" | "stab" | "blunt";

const WEAPON_WORDS: Record<WeaponClassId, { noun: string; kind: WeaponKind }> = {
  sword: { noun: "칼", kind: "cut" },
  greatsword: { noun: "대검", kind: "cut" },
  axe: { noun: "도끼", kind: "cut" },
  dagger: { noun: "단검", kind: "stab" },
  spear: { noun: "창", kind: "stab" },
  trident_net: { noun: "삼지창", kind: "stab" },
  throwing: { noun: "투창", kind: "stab" },
  mace: { noun: "철퇴", kind: "blunt" },
  unarmed: { noun: "주먹", kind: "blunt" },
  shield: { noun: "방패", kind: "blunt" },
  focus: { noun: "지팡이", kind: "blunt" },
};

function weaponWord(g: Gladiator | undefined, skillId?: string): { noun: string; kind: WeaponKind } {
  if (skillId === "skill.javelin") return WEAPON_WORDS.throwing;
  if (skillId === "skill.shield_bash") return WEAPON_WORDS.shield;
  if (skillId === "skill.horn_charge") return { noun: "뿔", kind: "stab" };
  if (skillId === "skill.venom_bite") return { noun: "이빨", kind: "stab" };
  if (skillId === "skill.pounce") return { noun: "발톱", kind: "cut" };
  const w = g ? getWeapon(g.equipment.main) : undefined;
  return w ? WEAPON_WORDS[w.class] : WEAPON_WORDS.unarmed;
}

function bloodLine(rng: Rng, gore: GoreLevel, a: string, d: string, part: string, level: "light" | "heavy" | "crit", weapon: { noun: string; kind: WeaponKind }, withName = true): string {
  const w = withName ? `${a}의 ${josa(weapon.noun, "이/가")}` : josa(weapon.noun, "이/가");
  const p = josa(part, "을/를");
  const pSubj = josa(part, "이/가");
  if (gore === "mild") {
    return level === "light" ? `${w} ${d}의 ${p} 스쳤습니다.` : level === "heavy" ? `${w} ${d}의 ${p} 제대로 때렸습니다!` : `치명타! ${w} ${d}의 ${p} 정통으로 맞혔습니다!`;
  }
  const strong = gore === "strong";
  const lines: Record<WeaponKind, Record<typeof level, string[]>> = {
    cut: {
      light: [`${w} ${d}의 ${p} 스칩니다. 피가 배어 나옵니다.`, `${d}의 ${pSubj} 얕게 베였습니다.`],
      heavy: [`${w} ${d}의 ${p} 깊게 가릅니다! 피가 튑니다.`, `${d}의 ${pSubj} 쩍 갈라집니다!${strong ? " 모래가 붉게 젖어 듭니다." : ""}`],
      crit: strong
        ? [`치명타! ${w} ${d}의 ${p} 그대로 갈라 버립니다. 관중석 앞줄까지 피가 튑니다!`, `치명타! ${d}의 ${part}에서 피가 뿜어져 나옵니다. 살점이 모래 위로 떨어집니다!`]
        : [`치명타! ${w} ${d}의 ${p} 깊숙이 베었습니다!`],
    },
    stab: {
      light: [`${w} ${d}의 ${p} 얕게 찌릅니다. 피가 번집니다.`, `${w} ${d}의 ${p} 긁고 지나갑니다.`],
      heavy: [`${w} ${d}의 ${part}에 깊이 박힙니다! 뽑아내자 피가 솟습니다.`, `${w} ${d}의 ${p} 파고듭니다! ${d}의 입에서 신음이 터집니다.`],
      crit: strong
        ? [`치명타! ${w} ${d}의 ${p} 꿰뚫었습니다. 피가 왈칵 쏟아집니다!`, `치명타! ${w} ${d}의 ${p} 관통했습니다. 등 뒤로 핏방울이 흩날립니다!`]
        : [`치명타! ${w} ${d}의 ${p} 꿰뚫었습니다!`],
    },
    blunt: {
      light: [`${w} ${d}의 ${p} 때립니다. 살갗이 터지며 피가 맺힙니다.`, `${w} ${d}의 ${p} 스치듯 후려칩니다.`],
      heavy: [`퍽! ${withName ? `${a}의 ` : ""}${weapon.noun}에 ${d}의 ${pSubj} 짓이겨집니다.`, `둔탁한 소리와 함께 ${josa(d, "이/가")} 휘청입니다. ${part}에서 피가 흐릅니다.`],
      crit: strong
        ? [`치명타! 뼈 부러지는 소리가 경기장에 울립니다. ${d}의 ${pSubj} 기괴하게 꺾였습니다!`, `치명타! ${w} ${d}의 ${p} 으스러뜨립니다. 피와 뼛조각이 모래 위로 튑니다!`]
        : [`치명타! ${w} ${d}의 ${p} 정통으로 후려쳤습니다!`],
    },
  };
  return rng.pick(lines[weapon.kind][level]);
}
