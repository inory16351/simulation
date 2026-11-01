// 경기 장면: 림월드풍 꼬마 검투사 두 명을 코드로 리깅해 전투 이벤트대로 움직인다.
import { avatarOf, type AvatarSpec } from "../../core/avatar";
import type { CombatEvent } from "../../core/combat/sim";
import { getArmor, getWeapon } from "../../core/data";
import type { Gladiator, Id } from "../../core/types";
import { asset } from "../assets";
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

  return (
    <div className={`arena-stage storybook ${shake ? "shake hitstop" : ""}`} key={shake ? `s${events.length}` : "s"}>
      <svg viewBox="0 0 400 160" preserveAspectRatio="xMidYMid slice">
        <PaintDefs />
        <g filter="url(#paintEdge)">
        {/* 배경: 붓으로 그린 경기장 (훈련전은 관중 없는 훈련장) */}
        <rect width="400" height="160" fill="#1a1411" />
        <image href={asset(crowd ? "ui/arena_provincial.webp" : "ui/bg_main.webp")} x="0" y="0" width="400" height="160" preserveAspectRatio={crowd ? "xMidYMax slice" : "xMidYMid slice"} />
        {/* 관중이 달아오르면 관중석이 횃불빛으로 일렁인다 */}
        {crowd && <rect width="400" height="90" fill="#ff9a3c" className={crowdHype > 55 ? "crowd-glow" : ""} opacity={Math.max(0, (crowdHype - 40) / 300)} style={{ mixBlendMode: "screen" }} />}
        <ellipse cx="200" cy="146" rx="240" ry="34" fill="url(#sandShade)" opacity="0.55" />
        <defs>
          <radialGradient id="sandShade" cx="50%" cy="20%" r="80%">
            <stop offset="0" stopColor="#c9a070" stopOpacity="0.35" />
            <stop offset="1" stopColor="#3a2a1e" stopOpacity="0.7" />
          </radialGradient>
        </defs>
        {/* 모래에 남은 핏자국 */}
        {blood.map((p, i) => <ellipse key={i} cx={p.x} cy={GROUND + 4 + p.dy} rx={p.r * 1.6} ry={p.r * 0.5} fill="#6e1712" opacity="0.85" />)}

        <Fighter v={views[0]} x={xa} facing={1} />
        <Fighter v={views[1]} x={xb} facing={-1} />

        {/* 피 튐 */}
        {burst && <BloodBurst key={burst.key} x={burst.side === 0 ? xa : xb} big={burst.big} dir={burst.side === 0 ? -1 : 1} />}
        </g>
        {/* 붓으로 그린 타격 효과 */}
        {burst && <BrushHit key={`h${burst.key}`} x={burst.side === 0 ? xa : xb} big={burst.big} dir={burst.side === 0 ? -1 : 1} />}
        {/* 동화책 조명: 왼쪽 횃불의 따뜻한 빛, 오른쪽 아래 푸른 그늘, 가장자리 어둠 */}
        <rect width="400" height="160" fill="url(#warmKey)" style={{ mixBlendMode: "screen" }} pointerEvents="none" />
        <rect width="400" height="160" fill="url(#coolShade)" style={{ mixBlendMode: "multiply" }} pointerEvents="none" />
        <rect width="400" height="160" fill="url(#vignette)" pointerEvents="none" />
        <rect width="400" height="160" filter="url(#canvasGrain)" opacity="0.16" style={{ mixBlendMode: "overlay" }} pointerEvents="none" />
        {burst?.big && <rect key={`f${burst.key}`} className="impact-frame" width="400" height="160" fill="#fff6dc" pointerEvents="none" />}
      </svg>
    </div>
  );
}

/** 붓질 느낌을 내는 SVG 필터와 조명 그라디언트 */
function PaintDefs() {
  return (
    <defs>
      {/* 윤곽을 살짝 흔들어 손으로 칠한 가장자리처럼 */}
      <filter id="paintEdge" x="-5%" y="-5%" width="110%" height="110%">
        <feTurbulence type="fractalNoise" baseFrequency="0.035" numOctaves="2" seed="7" result="n" />
        <feDisplacementMap in="SourceGraphic" in2="n" scale="2.4" xChannelSelector="R" yChannelSelector="G" />
      </filter>
      {/* 캔버스 결 */}
      <filter id="canvasGrain">
        <feTurbulence type="fractalNoise" baseFrequency="0.9" numOctaves="2" seed="3" />
        <feColorMatrix type="saturate" values="0" />
      </filter>
      {/* 붓 자국 가장자리를 거칠게 */}
      <filter id="brushRough" x="-20%" y="-20%" width="140%" height="140%">
        <feTurbulence type="fractalNoise" baseFrequency="0.09" numOctaves="2" seed="11" result="n" />
        <feDisplacementMap in="SourceGraphic" in2="n" scale="3.5" xChannelSelector="R" yChannelSelector="G" />
      </filter>
      <linearGradient id="warmKey" x1="0" y1="0" x2="1" y2="0">
        <stop offset="0" stopColor="#ff9a3c" stopOpacity="0.38" />
        <stop offset="0.45" stopColor="#ff9a3c" stopOpacity="0.06" />
        <stop offset="1" stopColor="#ff9a3c" stopOpacity="0" />
      </linearGradient>
      <linearGradient id="coolShade" x1="0.2" y1="0" x2="1" y2="1">
        <stop offset="0" stopColor="#ffffff" />
        <stop offset="0.6" stopColor="#c9c3e6" />
        <stop offset="1" stopColor="#3d4f7a" />
      </linearGradient>
      <radialGradient id="vignette" cx="50%" cy="55%" r="70%">
        <stop offset="0.55" stopColor="#000" stopOpacity="0" />
        <stop offset="1" stopColor="#0a0610" stopOpacity="0.75" />
      </radialGradient>
    </defs>
  );
}

/** 타격: 붓으로 한 번에 그은 듯한 베기 자국, 치명타면 빛살이 터진다 */
function BrushHit({ x, big, dir }: { x: number; big: boolean; dir: number }) {
  const spikes = big ? 9 : 0;
  return (
    <g transform={`translate(${x} ${GROUND - 36}) scale(${dir * (big ? 1.5 : 1.15)} ${big ? 1.5 : 1.15})`} filter="url(#brushRough)">
      {spikes > 0 && (
        <g className="impact-star">
          {Array.from({ length: spikes }, (_, i) => {
            const a = (i / spikes) * Math.PI * 2;
            const len = 26 + (i % 3) * 9;
            const w = 4;
            const tip = [Math.cos(a) * len, Math.sin(a) * len];
            const l = [Math.cos(a + 1.57) * w, Math.sin(a + 1.57) * w];
            return <path key={i} d={`M${l[0]} ${l[1]} L${tip[0]} ${tip[1]} L${-l[0]} ${-l[1]} Z`} fill={i % 2 ? "#ffd36b" : "#fff3d1"} />;
          })}
          <circle r="9" fill="#fff8e6" />
        </g>
      )}
      <path className="brush-slash" d="M-30 -24 C-10 -22 14 -2 30 24 C20 22 -6 -2 -30 -24 Z" fill="#fff4d8" />
      <path className="brush-slash red" d="M-24 -16 C-8 -12 10 4 24 20 C14 18 -6 2 -24 -16 Z" fill="#c4271c" />
    </g>
  );
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
          {/* 휘두른 자리에 남는 붓 자국 */}
          {v.pose === "attack" && <path className="smear" d="M2 -62 C30 -60 40 -30 26 -4 C30 -28 22 -48 2 -54 Z" fill="#fff1cf" opacity="0" />}
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
