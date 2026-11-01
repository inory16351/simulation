import { getSkill } from "../data";
import { josa } from "../josa";
import { Rng } from "../rng";
import type { Gladiator, Id } from "../types";
import type { BodyPart, CombatEvent } from "./sim";

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

const STATUS_TEXT: Record<string, string> = {
  stun: "비틀거리며 정신을 잃었다",
  bound: "그물에 휘감겼다",
  prone: "모래 위로 나뒹굴었다",
  grappled: "목이 졸린 채 붙잡혔다",
  blinded: "눈에 모래가 들어가 앞을 보지 못한다",
  armor_break: "갑옷이 쩍 갈라졌다",
  poison: "상처에서 검게 독이 번진다",
  weakened: "팔다리에 힘이 빠져나간다",
};

const SELF_TEXT: Record<string, string> = {
  "skill.iron_wall": "방패를 땅에 박고 몸을 웅크린다",
  "skill.riposte": "칼끝을 낮추고 상대의 빈틈을 노린다",
  "skill.parry": "발끝으로 모래를 디디며 몸을 흔든다",
  "skill.catch_breath": "한 걸음 물러나 거친 숨을 고른다",
  "skill.taunt": "무기를 내리고 관중을 향해 두 팔을 벌린다",
  "skill.flame_weapon": "칼날을 쓸어내리자 불꽃이 붙는다",
  "skill.ward_rune": "허공에 룬을 그린다. 푸른 빛이 몸을 감싼다",
  "skill.adapt": "상대의 수를 읽고 자세를 바꾼다",
  "skill.war_cry": "짐승 같은 함성을 내지른다",
  "skill.wind_step": "바람처럼 가벼워진다",
  "skill.earth_stance": "두 발을 땅에 박아 넣는다. 꿈쩍도 하지 않는다",
  "skill.war_roar": "포효한다. 상대의 눈빛이 흔들린다",
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
  const push = (text: string, tone: CommentaryLine["tone"], event: CombatEvent) => out.push({ round, text, tone, event });

  for (const e of events) {
    switch (e.t) {
      case "start": {
        const a = fighters[e.a], b = fighters[e.b];
        push(`${josa(a.name, "과/와")} ${josa(b.name, "이/가")} 모래 위에 섰다. 관중석이 술렁인다.`, "system", e);
        break;
      }
      case "round":
        round = e.round;
        break;
      case "move": {
        const n = name(e.actor);
        if (e.skill && e.skill !== "skill.disengage") break; // 돌진류는 공격 문장에서 처리
        if (e.closer) push(pickLine(rng, [`${josa(n, "이/가")} 거리를 좁힌다.`, `${josa(n, "이/가")} 모래를 박차고 다가선다.`, `${josa(n, "이/가")} 천천히 원을 그리며 다가간다.`]), "normal", e);
        else push(pickLine(rng, [`${josa(n, "이/가")} 뒤로 물러나 거리를 벌린다.`, `${josa(n, "은/는")} 한 걸음 빠지며 간격을 잰다.`]), "normal", e);
        break;
      }
      case "attack": {
        const a = name(e.actor), d = name(e.target);
        const skill = e.skill ? getSkill(e.skill) : undefined;
        const prefix = skill ? `[${skill.name}] ` : "";
        const part = PART_NAMES[e.part];
        if (!e.hit) {
          push(prefix + pickLine(rng, [
            `${a}의 공격이 허공을 가른다.`,
            `${josa(d, "이/가")} 몸을 틀어 피했다!`,
            `${a}의 일격이 빗나갔다. 관중석에서 탄식이 터진다.`,
          ]), "normal", e);
          break;
        }
        if (e.blocked) {
          push(prefix + pickLine(rng, [
            `${josa(d, "이/가")} 막아냈다! 쇳소리가 경기장을 울린다.`,
            `${a}의 공격이 방패에 튕겨 나간다.`,
          ]) + ` (${d} ${e.hpAfter}/${e.maxHp})`, "normal", e);
          break;
        }
        const ratio = e.damage / e.maxHp;
        const hpText = ` (${d} ${e.hpAfter}/${e.maxHp})`;
        if (e.magic) {
          push(`${prefix}${a}의 주술이 ${josa(d, "을/를")} 덮친다! 살이 타는 냄새가 퍼진다.${hpText}`, e.crit ? "crit" : "hit", e);
        } else if (e.crit) {
          push(prefix + bloodLine(rng, gore, a, d, part, "crit") + hpText, "crit", e);
        } else if (ratio > 0.12) {
          push(prefix + bloodLine(rng, gore, a, d, part, "heavy") + hpText, "blood", e);
        } else {
          push(prefix + bloodLine(rng, gore, a, d, part, "light") + hpText, "hit", e);
        }
        break;
      }
      case "status": {
        const n = name(e.target);
        if (e.resisted) push(`${josa(n, "은/는")} 버텨냈다!`, "normal", e);
        else if (STATUS_TEXT[e.status]) push(`${josa(n, "이/가")} ${STATUS_TEXT[e.status]}.`, "hit", e);
        break;
      }
      case "self": {
        const s = getSkill(e.skill);
        push(`[${s.name}] ${josa(name(e.actor), "이/가")} ${SELF_TEXT[e.skill] ?? "자세를 가다듬는다"}.`, "normal", e);
        break;
      }
      case "crowd":
        if (e.delta >= 8) push(pickLine(rng, ["관중이 일어선다! 함성이 터진다.", "환호성이 경기장을 뒤흔든다!"]), "crowd", e);
        else if (e.delta <= -8) push(pickLine(rng, ["야유가 쏟아진다.", "관중석에서 돌멩이가 날아든다."]), "crowd", e);
        break;
      case "rest":
        push(`${josa(name(e.actor), "이/가")} 숨을 몰아쉰다.`, "normal", e);
        break;
      case "poison":
        push(`독이 퍼진다. ${josa(name(e.target), "이/가")} 피를 토한다. (${e.hpAfter})`, "blood", e);
        break;
      case "skip": {
        const n = name(e.actor);
        const t = { stun: "아직 정신을 차리지 못했다", prone: "모래를 짚고 겨우 일어난다", grappled: "붙잡힌 채 버둥거린다" }[e.reason];
        push(`${josa(n, "이/가")} ${t}.`, "normal", e);
        break;
      }
      case "enrage":
        push(`▶ 피의 격노 — ${name(e.actor)}의 눈이 붉게 물든다.`, "crit", e);
        break;
      case "unbreakable":
        push(`▶ 불굴 — 쓰러져야 할 ${josa(name(e.actor), "이/가")} 피투성이로 다시 일어선다!`, "crit", e);
        break;
      case "surrender":
        push(`${josa(name(e.actor), "이/가")} 무릎을 꿇고 손가락을 들어 올린다. 항복이다.`, "system", e);
        break;
      case "ko":
        push(e.crit
          ? `${josa(name(e.target), "이/가")} 피를 뿜으며 쓰러졌다! 모래가 검붉게 젖어 든다.`
          : `${josa(name(e.target), "이/가")} 쓰러졌다. 관중이 숨을 죽인다…`, "death", e);
        break;
      case "timeout":
        push("시간이 다 됐다! 심판이 판정에 들어간다.", "system", e);
        break;
      case "verdict": {
        const n = name(e.target);
        if (e.rule === "missio") {
          push(`▶ 미시오 판정 — 주최자가 관중을 둘러본다… (사망 확률 ${Math.round(e.deathChance * 100)}%)`, "system", e);
          push(e.died
            ? `엄지가 아래를 향한다. ${opponentOf(events, e.target, fighters)}의 칼이 ${n}의 목을 꿰뚫는다. 피가 모래 위로 쏟아진다.`
            : `엄지가… 위로! ${josa(n, "은/는")} 살아남았다.`, e.died ? "death" : "crowd", e);
        } else if (e.died) {
          push(e.rule === "sudden"
            ? `사투에 자비는 없다. ${josa(opponentOf(events, e.target, fighters), "이/가")} 마지막 일격을 내리꽂는다. ${josa(n, "은/는")} 다시 일어나지 못했다.`
            : `${josa(n, "이/가")} 일어나지 못한다… 의무병이 달려가지만, 이미 숨이 멎었다.`, "death", e);
        } else if (e.rule === "sudden") {
          push(`기적이다! 숨이 붙어 있다. ${josa(n, "은/는")} 들것에 실려 나간다.`, "crowd", e);
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

function opponentOf(events: CombatEvent[], id: Id, fighters: Record<Id, Gladiator>): string {
  const start = events.find((e) => e.t === "start");
  if (!start || start.t !== "start") return "?";
  return fighters[start.a === id ? start.b : start.a].name;
}

function bloodLine(rng: Rng, gore: GoreLevel, a: string, d: string, part: string, level: "light" | "heavy" | "crit"): string {
  if (gore === "mild") {
    return level === "light" ? `${a}의 공격이 ${d}의 ${josa(part, "을/를")} 스쳤다.` : `${a}의 강한 일격이 ${d}의 ${josa(part, "을/를")} 때렸다!`;
  }
  const strong = gore === "strong";
  const lines = {
    light: [
      `${a}의 칼날이 ${d}의 ${josa(part, "을/를")} 스친다. 붉은 선이 그어진다.`,
      `${d}의 ${josa(part, "이/가")} 베였다. 피가 한 방울씩 모래에 떨어진다.`,
      `${a}의 공격이 ${d}의 ${josa(part, "을/를")} 파고든다.`,
    ],
    heavy: [
      `${a}의 일격이 ${d}의 ${josa(part, "을/를")} 깊게 가른다! 피가 튄다.`,
      `${d}의 ${part}에서 피가 솟구친다! ${strong ? "모래가 붉게 젖는다." : ""}`,
      `둔탁한 소리. ${d}의 ${josa(part, "이/가")} 짓이겨졌다.`,
    ],
    crit: strong
      ? [
          `치명타! ${a}의 무기가 ${d}의 ${josa(part, "을/를")} 꿰뚫었다. 피가 분수처럼 뿜어져 나온다!`,
          `치명타! 살이 찢기고 뼈가 부러지는 소리. ${d}의 ${part}에서 검붉은 피가 쏟아진다!`,
          `치명타! ${a}의 일격에 ${d}의 ${josa(part, "이/가")} 갈라졌다. 관중석 앞줄까지 피가 튄다!`,
        ]
      : [`치명타! ${a}의 일격이 ${d}의 ${josa(part, "을/를")} 정통으로 갈랐다!`],
  };
  return rng.pick(lines[level]).trim();
}
