// 경기 장면: 림월드풍 꼬마 검투사 두 명을 코드로 리깅해 전투 이벤트대로 움직인다.
import { avatarOf, type AvatarSpec } from "../../core/avatar";
import type { CombatEvent } from "../../core/combat/sim";
import { getArmor, getNation, getWeapon } from "../../core/data";
import type { Gladiator, Id } from "../../core/types";
import { AvatarSvg } from "./Avatar";

const INK = "#1a120e";
const GROUND = 128;
/** 거리 상태(0 근접, 1 중거리, 2 원거리)별 두 사람의 x 위치 */
const POS: Record<number, [number, number]> = { 0: [168, 232], 1: [138, 262], 2: [104, 296] };

type Pose = "idle" | "attack" | "hurt" | "block" | "dodge" | "step" | "cast" | "stagger" | "down" | "dead" | "kneel" | "cheer";

interface FighterView {
  g: Gladiator;
  pose: Pose;
  /** 같은 동작이 이어져도 애니메이션이 다시 돌게 하는 키 */
  key: number;
  hpRatio: number;
}

interface Props {
  a: Gladiator;
  b: Gladiator;
  /** 지금까지 보여 준 이벤트 */
  events: CombatEvent[];
  crowd: boolean;
}

export function ArenaStage({ a, b, events, crowd }: Props) {
  const { dist, views, blood, burst, shake, crowdHype } = derive(a, b, events);
  const [xa, xb] = POS[dist] ?? POS[2];
  const nation = getNation(a.nation);

  return (
    <div className={`arena-stage ${shake ? "shake" : ""}`} key={shake ? `s${events.length}` : "s"}>
      <svg viewBox="0 0 400 160" preserveAspectRatio="xMidYMid slice">
        {/* 배경: 벽, 관중, 모래 */}
        <rect width="400" height="160" fill="#1a1411" />
        <rect y="0" width="400" height="70" fill="#2a201a" />
        {crowd && <Crowd hype={crowdHype} color={nation.color} />}
        <rect y="66" width="400" height="8" fill="#3b2d22" stroke={INK} strokeWidth="1.5" />
        {[40, 120, 200, 280, 360].map((x) => <rect key={x} x={x - 3} y="40" width="6" height="30" fill="#4a382a" stroke={INK} strokeWidth="1.2" />)}
        {[40, 360].map((x) => <g key={x}><path d={`M${x} 38 q4 -8 0 -14 q-4 6 0 14`} fill="#e39a3b" className="torch" /></g>)}
        <ellipse cx="200" cy="140" rx="230" ry="44" fill="#9a7b52" />
        <ellipse cx="200" cy="140" rx="230" ry="44" fill="url(#sandShade)" />
        <defs>
          <radialGradient id="sandShade" cx="50%" cy="20%" r="80%">
            <stop offset="0" stopColor="#b8956a" stopOpacity="0.6" />
            <stop offset="1" stopColor="#5a4530" stopOpacity="0.8" />
          </radialGradient>
        </defs>
        {/* 모래에 남은 핏자국 */}
        {blood.map((p, i) => <ellipse key={i} cx={p.x} cy={GROUND + 4 + p.dy} rx={p.r * 1.6} ry={p.r * 0.5} fill="#6e1712" opacity="0.85" />)}

        <Fighter v={views[0]} x={xa} facing={1} />
        <Fighter v={views[1]} x={xb} facing={-1} />

        {/* 피 튐 */}
        {burst && <BloodBurst key={burst.key} x={burst.side === 0 ? xa : xb} big={burst.big} dir={burst.side === 0 ? -1 : 1} />}
      </svg>
    </div>
  );
}

function Crowd({ hype, color }: { hype: number; color: string }) {
  const heads = [];
  for (let row = 0; row < 3; row++) {
    for (let i = 0; i < 26; i++) {
      const x = 8 + i * 15.5 + (row % 2) * 7;
      const y = 22 + row * 14;
      const fill = (i + row) % 5 === 0 ? color : (i * 7 + row) % 3 === 0 ? "#5a4636" : "#3e3028";
      heads.push(<g key={`${row}-${i}`} className={hype > 55 && (i + row) % 2 ? "cheer-head" : ""} style={{ animationDelay: `${(i % 5) * 0.08}s` }}>
        <circle cx={x} cy={y} r="5" fill={fill} stroke={INK} strokeWidth="1" />
        <rect x={x - 6} y={y + 4} width="12" height="8" rx="3" fill={fill} stroke={INK} strokeWidth="1" />
      </g>);
    }
  }
  return <g opacity="0.85">{heads}</g>;
}

function Fighter({ v, x, facing }: { v: FighterView; x: number; facing: 1 | -1 }) {
  const s = avatarOf(v.g);
  const weapon = getWeapon(v.g.equipment.main);
  const off = v.g.equipment.off;
  const shield = off ? getArmor(off)?.slot === "shield" : false;
  const bodyColor = armorColor(s);
  const legColor = "#3a2c22";
  return (
    <g className="fighter" style={{ transform: `translate(${x}px, ${GROUND}px) scale(${facing}, 1)` }}>
      <ellipse cx="0" cy="1" rx="16" ry="3.5" fill="#000" opacity="0.3" />
      <g key={v.key} className={`rig pose-${v.pose}`}>
        <g className="body">
          {/* 다리 */}
          <g className="legs">
            <path className="leg-back" d="M-4 -18 L-7 0" stroke={legColor} strokeWidth="6" strokeLinecap="round" />
            <path className="leg-front" d="M4 -18 L7 0" stroke={legColor} strokeWidth="6" strokeLinecap="round" />
          </g>
          {/* 뒷팔 + 방패 */}
          <g transform="translate(-6 -34)">
            <g className="arm-back">
              <path d="M0 0 L-2 14" stroke={s.skin} strokeWidth="5" strokeLinecap="round" />
              <path d="M0 0 L-2 14" stroke={INK} strokeWidth="6.6" strokeLinecap="round" opacity="0.0" />
              {shield && <g transform="translate(-2 12)"><ellipse rx="9" ry="12" fill="#a8823f" stroke={INK} strokeWidth="1.6" /><circle r="2.4" fill="#7a5c33" /></g>}
            </g>
          </g>
          {/* 몸통 */}
          <rect x="-11" y="-42" width="22" height="26" rx="8" fill={bodyColor} stroke={INK} strokeWidth="1.6" />
          <path d="M-9 -28 L9 -20" stroke="#5a3b22" strokeWidth="3" opacity={s.armor === "light" ? 1 : 0} />
          {/* 머리 */}
          <g className="head">
            <svg x="-25" y="-75" width="50" height="50" viewBox="0 0 100 100" overflow="visible">
              <AvatarSvg spec={s} bg="none" headOnly injured={v.hpRatio < 0.35 ? "serious" : v.hpRatio < 0.7 ? "light" : undefined} seed={v.g.portraitSeed % 997} />
            </svg>
          </g>
          {/* 앞팔 + 무기 */}
          <g transform="translate(7 -34)">
            <g className="arm-front">
              <path d="M0 0 L2 14" stroke={s.skin} strokeWidth="5" strokeLinecap="round" />
              <g transform="translate(2 14)"><Weapon cls={weapon?.class ?? "unarmed"} /></g>
            </g>
          </g>
          {/* 맞았을 때 붉은 번쩍임 */}
          <rect className="hurt-flash" x="-14" y="-78" width="28" height="78" rx="10" fill="#e02a1a" opacity="0" />
          {v.pose === "cast" && <circle className="cast-glow" cx="12" cy="-24" r="8" fill="#7ad0ff" opacity="0.7" />}
        </g>
      </g>
    </g>
  );
}

function Weapon({ cls }: { cls: string }) {
  const metal = "#c9ccd2";
  switch (cls) {
    case "greatsword":
      return <g><path d="M0 0 L0 -40" stroke={metal} strokeWidth="4" strokeLinecap="round" /><path d="M0 -40 L0 -40" /><path d="M-6 -3 L6 -3" stroke="#7a5c33" strokeWidth="3" strokeLinecap="round" /><path d="M0 0 L0 -40" stroke={INK} strokeWidth="0.8" /></g>;
    case "axe":
      return <g><path d="M0 4 L0 -30" stroke="#6b4a2a" strokeWidth="3" strokeLinecap="round" /><path d="M0 -30 C10 -34 12 -22 0 -20 Z" fill={metal} stroke={INK} strokeWidth="1.3" /></g>;
    case "mace":
      return <g><path d="M0 4 L0 -22" stroke="#6b4a2a" strokeWidth="3" strokeLinecap="round" /><circle cx="0" cy="-25" r="5" fill="#8a8e94" stroke={INK} strokeWidth="1.3" /></g>;
    case "spear":
    case "trident_net":
      return <g><path d="M0 10 L0 -44" stroke="#6b4a2a" strokeWidth="2.6" strokeLinecap="round" />{cls === "spear"
        ? <path d="M-3 -44 L0 -54 L3 -44 Z" fill={metal} stroke={INK} strokeWidth="1.2" />
        : <path d="M-5 -44 L-5 -52 M0 -44 L0 -54 M5 -44 L5 -52 M-5 -44 L5 -44" stroke={metal} strokeWidth="2" strokeLinecap="round" />}</g>;
    case "dagger":
      return <g><path d="M0 0 L0 -12" stroke={metal} strokeWidth="2.6" strokeLinecap="round" /><path d="M-3 -1 L3 -1" stroke="#7a5c33" strokeWidth="2" /></g>;
    case "focus":
      return <g><path d="M0 8 L0 -36" stroke="#5a3b22" strokeWidth="2.6" strokeLinecap="round" /><circle cx="0" cy="-38" r="4" fill="#7ad0ff" stroke={INK} strokeWidth="1.2" /></g>;
    case "unarmed":
      return <circle r="3.2" fill="#c49a6c" stroke={INK} strokeWidth="1.2" />;
    default:
      return <g><path d="M0 0 L0 -24" stroke={metal} strokeWidth="3.2" strokeLinecap="round" /><path d="M-5 -2 L5 -2" stroke="#7a5c33" strokeWidth="2.6" strokeLinecap="round" /></g>;
  }
}

function BloodBurst({ x, big, dir }: { x: number; big: boolean; dir: number }) {
  const drops = Array.from({ length: big ? 12 : 6 }, (_, i) => {
    const ang = (-30 - i * (big ? 12 : 22)) * (Math.PI / 180);
    const d = (big ? 34 : 22) + (i % 3) * 6;
    return { dx: Math.cos(ang) * d * dir, dy: Math.sin(ang) * d, r: big ? 2.6 : 2 };
  });
  return (
    <g transform={`translate(${x} ${GROUND - 32})`}>
      {drops.map((p, i) => (
        <circle key={i} className="blood-drop" r={p.r} fill="#b3261c" style={{ ["--dx" as string]: `${p.dx}px`, ["--dy" as string]: `${p.dy}px`, animationDelay: `${i * 0.015}s` }} />
      ))}
    </g>
  );
}

function armorColor(s: AvatarSpec): string {
  return s.armor === "heavy" ? "#b08a4f" : s.armor === "medium" ? "#6f7378" : s.cloth;
}

/** 이벤트 목록에서 지금 장면 상태를 계산한다 */
function derive(a: Gladiator, b: Gladiator, events: CombatEvent[]) {
  const side = (id: Id) => (id === a.id ? 0 : 1);
  let dist = 2;
  const views: [FighterView, FighterView] = [
    { g: a, pose: "idle", key: 0, hpRatio: 1 },
    { g: b, pose: "idle", key: 0, hpRatio: 1 },
  ];
  const blood: { x: number; dy: number; r: number }[] = [];
  const persistent: (Pose | null)[] = [null, null];
  let burst = null as { side: number; big: boolean; key: number } | null;
  let shake = false as boolean;
  let crowdHype = 30;

  for (let idx = 0; idx < events.length; idx++) {
    const e = events[idx];
    const last = idx === events.length - 1;
    const set = (s: number, pose: Pose) => {
      if (persistent[s]) return;
      if (last) { views[s].pose = pose; views[s].key = idx; }
    };
    switch (e.t) {
      case "move":
        dist = e.dist;
        set(side(e.actor), "step");
        break;
      case "attack": {
        const t = side(e.target);
        set(side(e.actor), e.magic ? "cast" : "attack");
        set(t, !e.hit ? "dodge" : e.blocked ? "block" : "hurt");
        if (e.hit) views[t].hpRatio = e.hpAfter / e.maxHp;
        if (e.hit && !e.blocked && e.damage > 0) {
          const [xa, xb] = POS[dist] ?? POS[2];
          if (e.damage / e.maxHp > 0.08 || e.crit) blood.push({ x: t === 0 ? xa - 6 : xb + 6, dy: (idx % 5) - 2, r: Math.min(9, 3 + (e.damage / e.maxHp) * 30) });
          if (last) { burst = { side: t, big: e.crit, key: idx }; shake = e.crit; }
        }
        break;
      }
      case "self":
        set(side(e.actor), "cast");
        break;
      case "status":
        if (e.status === "stun" || e.status === "prone") set(side(e.target), "stagger");
        break;
      case "poison":
        views[side(e.target)].hpRatio = Math.max(0, e.hpAfter) / Math.max(1, e.hpAfter + e.damage);
        break;
      case "crowd":
        crowdHype = e.value;
        break;
      case "surrender":
        persistent[side(e.actor)] = "kneel";
        break;
      case "ko":
        persistent[side(e.target)] = "down";
        views[side(e.target)].hpRatio = 0;
        break;
      case "verdict":
        if (e.died) {
          persistent[side(e.target)] = "dead";
          const [xa, xb] = POS[dist] ?? POS[2];
          blood.push({ x: side(e.target) === 0 ? xa : xb, dy: 2, r: 14 });
        }
        break;
      case "end":
        if (!persistent[side(e.winner)]) persistent[side(e.winner)] = "cheer";
        break;
    }
  }
  persistent.forEach((p, i) => { if (p) { views[i].pose = p; } });
  return { dist, views, blood, burst, shake, crowdHype };
}
