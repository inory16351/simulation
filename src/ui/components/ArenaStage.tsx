// 경기 장면: 위에서 내려다본 모래 마당에서 동그란 초상화 말 두 개가 전투 이벤트대로 움직인다.
// 장면 전체가 SVG 하나라서 tools/render-stage.tsx로 정지 화면을 뽑을 수 있다.
import type { CSSProperties } from "react";
import type { BodyPart, CombatEvent } from "../../core/combat/sim";
import { avatarOf } from "../../core/avatar";
import { getNation, getWeapon } from "../../core/data";
import { paintedSpec } from "../../core/portrait2";
import type { Gladiator, Id } from "../../core/types";
import { asset } from "../assets";
import { AvatarSvg } from "./Avatar";
import { PaintedAvatar } from "./PaintedAvatar";

const INK = "#1a120e";
const W = 400, H = 160;
const CX = 200, CY = 82;
/** 말 반지름 */
const R = 28;
/** 거리 상태(0 근접, 1 중거리, 2 원거리)별 중심에서 말까지 거리 */
const HALF: Record<number, number> = { 0: 38, 1: 66, 2: 102 };

export type Pose = "idle" | "attack" | "hurt" | "block" | "dodge" | "step" | "cast" | "stagger" | "down" | "dead" | "kneel" | "cheer";

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
  /** 정지 화면 렌더링용 (예전 리깅 장면과 같은 인터페이스) */
  fixedTime?: number;
}

/** 두 사람이 서로를 마주 보는 축의 각도와 거리로 두 말의 위치를 낸다 */
function place(dist: number, angle: number): [[number, number], [number, number]] {
  const d = HALF[dist] ?? HALF[2];
  const dx = Math.cos(angle) * d, dy = Math.sin(angle) * d * 0.55;
  return [[CX - dx, CY - dy], [CX + dx, CY + dy]];
}

export function ArenaStage({ a, b, events, crowd }: Props) {
  const { dist, angle, views, blood, burst, dodge, shake, crowdHype } = derive(a, b, events);
  const pos = place(dist, angle);
  // a에서 b를 향한 단위 벡터
  const ux = (pos[1][0] - pos[0][0]) / Math.max(1, Math.hypot(pos[1][0] - pos[0][0], pos[1][1] - pos[0][1]));
  const uy = (pos[1][1] - pos[0][1]) / Math.max(1, Math.hypot(pos[1][0] - pos[0][0], pos[1][1] - pos[0][1]));
  const dirOf = (s: number): [number, number] => (s === 0 ? [ux, uy] : [-ux, -uy]);

  return (
    <div className={`arena-stage topdown ${shake ? "shake" : ""}`} key={shake ? `s${events.length}` : "s"}>
      <svg viewBox={`0 0 ${W} ${H}`} preserveAspectRatio="xMidYMid slice">
        <Floor crowd={crowd} hype={crowdHype} />
        {/* 모래에 남은 핏자국 */}
        {blood.map((p, i) => (
          <image key={i} href={asset("fx/blood_pool.webp")} x={p.x - p.r * 2.2} y={p.y - p.r * 1.1} width={p.r * 4.4} height={p.r * 2.2}
            preserveAspectRatio="none" opacity="0.85" transform={`rotate(${(i * 67) % 360} ${p.x} ${p.y})`} />
        ))}
        {dodge && <FxSprite key={`d${dodge.key}`} name="dust" x={pos[dodge.side][0]} y={pos[dodge.side][1] + 8} size={50} className="fx-dust" />}

        {/* 쓰러진 쪽을 먼저 그려 선 쪽이 위에 오게 */}
        {(views[1].pose === "down" || views[1].pose === "dead" ? [1, 0] : [0, 1]).map((s) => (
          <Token key={s} v={views[s]} x={pos[s][0]} y={pos[s][1]} dir={dirOf(s)} />
        ))}

        {burst && <HitFx key={`h${burst.key}`} x={pos[burst.side][0]} y={pos[burst.side][1]} hit={burst} from={dirOf(burst.side)} />}
        <rect width={W} height={H} fill="url(#tdVignette)" pointerEvents="none" />
        {burst?.big && <rect key={`f${burst.key}`} className="impact-frame" width={W} height={H} fill="#fff6dc" pointerEvents="none" />}
      </svg>
    </div>
  );
}

/** 위에서 본 원형 경기장: 관중석 고리, 벽, 갈퀴 자국 난 모래 */
function Floor({ crowd, hype }: { crowd: boolean; hype: number }) {
  // 관중: 관중석 고리를 따라 점을 뿌린다 (항상 같은 배치)
  const heads: { x: number; y: number; c: string }[] = [];
  if (crowd) {
    const tones = ["#8a5a3a", "#6e4a33", "#a0703e", "#5c3c2a", "#b38452", "#7a3b2e", "#4d5a6e"];
    for (let ring = 0; ring < 3; ring++) {
      const rx = 206 + ring * 14, ry = 86 + ring * 10;
      const n = 70 + ring * 8;
      for (let i = 0; i < n; i++) {
        const t = (i / n) * Math.PI * 2 + ring * 0.13;
        heads.push({ x: CX + Math.cos(t) * rx, y: CY + Math.sin(t) * ry, c: tones[(i * 7 + ring * 3) % tones.length] });
      }
    }
  }
  return (
    <g>
      <defs>
        <radialGradient id="tdSand" cx="50%" cy="45%" r="60%">
          <stop offset="0" stopColor="#d9b383" />
          <stop offset="0.7" stopColor="#c09060" />
          <stop offset="1" stopColor="#8e6640" />
        </radialGradient>
        <radialGradient id="tdVignette" cx="50%" cy="50%" r="70%">
          <stop offset="0.6" stopColor="#000" stopOpacity="0" />
          <stop offset="1" stopColor="#0a0610" stopOpacity="0.7" />
        </radialGradient>
        <filter id="tdGrain">
          <feTurbulence type="fractalNoise" baseFrequency="0.8" numOctaves="2" seed="5" />
          <feColorMatrix type="saturate" values="0" />
        </filter>
        <clipPath id="tdSandClip"><ellipse cx={CX} cy={CY} rx="186" ry="74" /></clipPath>
      </defs>
      <rect width={W} height={H} fill={crowd ? "#2a1f18" : "#1c1712"} />
      {crowd && <ellipse cx={CX} cy={CY} rx="240" ry="116" fill="#3a2b20" />}
      {heads.map((h, i) => (
        <circle key={i} cx={h.x} cy={h.y} r="3.4" fill={h.c} className={hype > 55 && i % 3 === 0 ? "crowd-bob" : undefined}
          style={hype > 55 ? { animationDelay: `${(i % 7) * 0.07}s` } : undefined} />
      ))}
      {/* 벽 */}
      <ellipse cx={CX} cy={CY} rx="194" ry="80" fill="#5a4535" stroke={INK} strokeWidth="2" />
      <ellipse cx={CX} cy={CY} rx="188" ry="76" fill="url(#tdSand)" stroke="#3a2a1e" strokeWidth="1.5" />
      {/* 갈퀴 자국과 모래 결 */}
      <g clipPath="url(#tdSandClip)" opacity="0.18" stroke="#6b4a2a" strokeWidth="0.8" fill="none">
        {Array.from({ length: 9 }, (_, i) => <ellipse key={i} cx={CX} cy={CY} rx={20 + i * 20} ry={8 + i * 8} />)}
      </g>
      <ellipse cx={CX} cy={CY} rx="188" ry="76" filter="url(#tdGrain)" opacity="0.12" style={{ mixBlendMode: "overlay" }} />
      {/* 양쪽 출입문 */}
      <rect x="2" y={CY - 13} width="14" height="26" rx="2" fill="#1a120e" stroke="#7a5c33" strokeWidth="1.5" />
      <rect x={W - 16} y={CY - 13} width="14" height="26" rx="2" fill="#1a120e" stroke="#7a5c33" strokeWidth="1.5" />
      {crowd && hype > 55 && <ellipse cx={CX} cy={CY} rx="230" ry="110" fill="none" stroke="#ff9a3c" strokeWidth="18" className="crowd-glow" opacity="0.12" />}
    </g>
  );
}

/** 초상화 말 하나. dir은 상대를 향한 단위 벡터 */
function Token({ v, x, y, dir }: { v: FighterView; x: number; y: number; dir: [number, number] }) {
  const g = v.g;
  const color = getNation(g.nation).color;
  const spec2 = paintedSpec(g);
  const down = v.pose === "down" || v.pose === "dead";
  const clip = `tdClip${g.id}`;
  const facing = (Math.atan2(dir[1], dir[0]) * 180) / Math.PI;
  const hp = Math.max(0, Math.min(1, v.hpRatio));
  const ringR = R + 4;
  const circ = 2 * Math.PI * ringR;
  const hpColor = hp > 0.6 ? "#7fc463" : hp > 0.3 ? "#e0b84a" : "#d4483a";
  // 동작마다 움직일 방향 (CSS 변수로 넘긴다)
  const vars = {
    "--fx": `${dir[0] * 16}px`, "--fy": `${dir[1] * 16}px`,
    "--bx": `${-dir[0] * 9}px`, "--by": `${-dir[1] * 9}px`,
    "--sx": `${-dir[1] * 14}px`, "--sy": `${dir[0] * 14}px`,
  } as CSSProperties;

  return (
    <g className="token" style={{ transform: `translate(${x}px, ${y}px)` }}>
      {/* 그림자 */}
      <ellipse cx="2" cy="4" rx={R + 3} ry={R + 1} fill="#000" opacity="0.35" />
      <g key={v.key} className={`token-body pose-${v.pose}`} style={vars}>
        {/* 바라보는 쪽 화살촉 */}
        {!down && <path d={`M ${ringR + 2} -5 L ${ringR + 9} 0 L ${ringR + 2} 5 Z`} fill={color} stroke={INK} strokeWidth="1.2" transform={`rotate(${facing})`} />}
        <circle r={ringR + 1.5} fill={INK} />
        <circle r={ringR - 2} fill={color} />
        <clipPath id={clip}><circle r={R - 1} /></clipPath>
        <g clipPath={`url(#${clip})`} className={down ? "token-down" : undefined}>
          <circle r={R} fill="#120d0a" />
          <svg x={-R - 3} y={-R - 1} width={(R + 3) * 2} height={(R + 3) * 2}>
            {spec2
              ? <PaintedAvatar spec={spec2} bg={color} seed={g.portraitSeed % 997} />
              : <AvatarSvg spec={avatarOf(g)} bg={color} seed={g.portraitSeed % 997} dead={v.pose === "dead"} />}
          </svg>
          <circle className="hurt-flash" r={R} fill="#ff2a1a" opacity="0" />
          {down && <circle r={R} fill="#1a0a0a" opacity="0.45" />}
        </g>
        {/* 체력 고리 */}
        <circle r={ringR} fill="none" stroke="#2a1d16" strokeWidth="3.4" />
        <circle r={ringR} fill="none" stroke={hpColor} strokeWidth="3.4" strokeLinecap="round"
          strokeDasharray={`${circ * hp} ${circ}`} transform="rotate(-90)" className="hp-ring" />
        <WeaponBadge cls={getWeapon(g.equipment.main)?.class ?? "unarmed"} />
        {v.pose === "block" && <path className="shield-arc" d={arc(ringR + 6, -50, 50)} stroke="#e8d7a8" strokeWidth="4" fill="none" strokeLinecap="round" transform={`rotate(${facing})`} />}
        {v.pose === "cast" && <circle className="cast-ring" r={ringR + 4} fill="none" stroke="#7ad0ff" strokeWidth="3" />}
        {v.pose === "cheer" && <circle className="cheer-ring" r={ringR + 5} fill="none" stroke="#f2c14e" strokeWidth="2.5" />}
        {v.pose === "kneel" && <g transform={`translate(${-R + 2} ${-R - 2})`}><path d="M0 0 L0 -14" stroke="#7a5c33" strokeWidth="1.8" /><path d="M0 -14 L10 -11 L0 -8 Z" fill="#f4efe2" stroke={INK} strokeWidth="0.8" /></g>}
        {v.pose === "dead" && <path d="M-9 -9 L9 9 M9 -9 L-9 9" stroke="#8a1c14" strokeWidth="4" strokeLinecap="round" opacity="0.85" />}
      </g>
    </g>
  );
}

/** 각도(도) a0~a1 사이의 원호 경로 */
function arc(r: number, a0: number, a1: number): string {
  const p = (a: number) => `${Math.cos((a * Math.PI) / 180) * r} ${Math.sin((a * Math.PI) / 180) * r}`;
  return `M ${p(a0)} A ${r} ${r} 0 0 1 ${p(a1)}`;
}

/** 오른쪽 아래에 붙는 무기 표식 */
function WeaponBadge({ cls }: { cls: string }) {
  const metal = "#d8dbe0", wood = "#8a6238";
  let glyph;
  switch (cls) {
    case "greatsword": glyph = <><path d="M-4 4 L4 -4" stroke={metal} strokeWidth="2.4" strokeLinecap="round" /><path d="M-5 1 L-1 5" stroke={wood} strokeWidth="1.6" /></>; break;
    case "axe": glyph = <><path d="M-4 5 L3 -3" stroke={wood} strokeWidth="1.6" strokeLinecap="round" /><path d="M1 -6 C6 -6 7 -1 4 1 Z" fill={metal} /></>; break;
    case "mace": glyph = <><path d="M-4 5 L2 -1" stroke={wood} strokeWidth="1.6" strokeLinecap="round" /><circle cx="2.5" cy="-2.5" r="2.8" fill={metal} /></>; break;
    case "spear": glyph = <><path d="M-5 5 L3 -3" stroke={wood} strokeWidth="1.4" strokeLinecap="round" /><path d="M2 -6 L6 -6 L6 -2 Z" fill={metal} /></>; break;
    case "trident_net": glyph = <><path d="M-5 5 L3 -3 M1 -6 L4 -3 L6 -1 M3 -3 L6 -6" stroke={metal} strokeWidth="1.4" strokeLinecap="round" fill="none" /></>; break;
    case "dagger": glyph = <><path d="M-2 2 L3 -3" stroke={metal} strokeWidth="2" strokeLinecap="round" /><path d="M-4 0 L0 4" stroke={wood} strokeWidth="1.4" /></>; break;
    case "focus": glyph = <><path d="M-4 5 L2 -1" stroke={wood} strokeWidth="1.6" strokeLinecap="round" /><circle cx="3" cy="-3" r="2.6" fill="#7ad0ff" /></>; break;
    case "unarmed": glyph = <circle r="3.4" fill="#c49a6c" stroke={INK} strokeWidth="0.8" />; break;
    default: glyph = <><path d="M-3 3 L4 -4" stroke={metal} strokeWidth="2.2" strokeLinecap="round" /><path d="M-5 0 L0 5" stroke={wood} strokeWidth="1.6" /></>;
  }
  return (
    <g transform={`translate(${R * 0.72} ${R * 0.72})`}>
      <circle r="8" fill="#241912" stroke="#b08a4a" strokeWidth="1.3" />
      {glyph}
    </g>
  );
}

interface Hit { side: number; big: boolean; heavy: boolean; blunt: boolean; key: number; part: BodyPart }

/** 그림 이펙트 한 장. 가운데를 (x, y)에 맞춘다 */
function FxSprite({ name, x, y, size, rotate = 0, className }: { name: string; x: number; y: number; size: number; rotate?: number; className?: string }) {
  return (
    <g transform={`translate(${x} ${y}) rotate(${rotate})`}>
      <image className={`fx-sprite ${className ?? ""}`} href={asset(`fx/${name}.webp`)} x={-size / 2} y={-size / 2} width={size} height={size} />
    </g>
  );
}

/** 타격: 맞은 말 위에 베기·둔기 충격, 공격자 반대쪽으로 피가 튄다. from은 맞은 쪽에서 공격자를 향한 방향 */
function HitFx({ x, y, hit, from }: { x: number; y: number; hit: Hit; from: [number, number] }) {
  const k = hit.big ? 1.3 : hit.heavy ? 1.05 : 0.85;
  const ang = (Math.atan2(from[1], from[0]) * 180) / Math.PI;
  // 맞은 쪽 가장자리(공격자 쪽)와 피가 튀는 바깥쪽
  const ex = x + from[0] * R * 0.5, ey = y + from[1] * R * 0.5;
  const ox = x - from[0] * R * 0.9, oy = y - from[1] * R * 0.9;
  return (
    <g>
      {hit.big && <FxSprite name="crit_star" x={x} y={y} size={60} className="fx-star" />}
      {hit.blunt
        ? <FxSprite name="impact_blunt" x={ex} y={ey} size={46 * k} className="fx-pop" />
        : <FxSprite name={hit.heavy || hit.big ? "slash_blood" : "slash_gold"} x={ex} y={ey} size={50 * k} rotate={ang + 90} className="fx-slash" />}
      {(hit.heavy || hit.big)
        ? <FxSprite name="blood_splash" x={ox} y={oy} size={48 * k} rotate={ang + 180} className="fx-spray" />
        : <FxSprite name="blood_drops" x={ox} y={oy} size={30} rotate={ang + 180} className="fx-spray" />}
      {hit.big && <FxSprite name="blood_gush" x={ox} y={oy + 6} size={44} className="fx-gush" />}
    </g>
  );
}

/** 이벤트 목록에서 지금 장면 상태를 계산한다 */
function derive(a: Gladiator, b: Gladiator, events: CombatEvent[]) {
  const side = (id: Id) => (id === a.id ? 0 : 1);
  let dist = 2;
  // 두 사람이 서로 돌며 싸우도록 이동할 때마다 축이 조금씩 돈다
  let angle = 0;
  const views: [FighterView, FighterView] = [
    { g: a, pose: "idle", key: 0, hpRatio: 1 },
    { g: b, pose: "idle", key: 0, hpRatio: 1 },
  ];
  const blood: { x: number; y: number; r: number }[] = [];
  const persistent: (Pose | null)[] = [null, null];
  let burst = null as Hit | null;
  let dodge = null as { side: number; key: number } | null;
  const bluntOf = (g: Gladiator) => ["mace", "unarmed", "shield"].includes(getWeapon(g.equipment.main)?.class ?? "unarmed");
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
      case "move": {
        dist = e.dist;
        const turn = (((idx * 37) % 5) - 2) * 0.22;
        angle = Math.max(-0.75, Math.min(0.75, angle + turn));
        set(side(e.actor), "step");
        break;
      }
      case "attack": {
        const t = side(e.target);
        set(side(e.actor), e.magic ? "cast" : "attack");
        set(t, !e.hit ? "dodge" : e.blocked ? "block" : "hurt");
        if (e.hit) views[t].hpRatio = e.hpAfter / e.maxHp;
        if (e.hit && !e.blocked && e.damage > 0) {
          const p = place(dist, angle)[t];
          if (e.damage / e.maxHp > 0.08 || e.crit) {
            const j = (idx * 53) % 360 * (Math.PI / 180);
            blood.push({ x: p[0] + Math.cos(j) * 14, y: p[1] + Math.sin(j) * 8, r: Math.min(10, 4 + (e.damage / e.maxHp) * 30) });
          }
          if (last) {
            burst = { side: t, big: e.crit, heavy: e.damage / e.maxHp > 0.12, blunt: bluntOf(views[side(e.actor)].g), key: idx, part: e.part };
            shake = e.crit;
          }
        }
        if (!e.hit && last) dodge = { side: t, key: idx };
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
          const p = place(dist, angle)[side(e.target)];
          blood.push({ x: p[0], y: p[1] + 4, r: 16 });
        }
        break;
      case "end":
        if (!persistent[side(e.winner)]) persistent[side(e.winner)] = "cheer";
        break;
    }
  }
  persistent.forEach((p, i) => { if (p) { views[i].pose = p; } });
  return { dist, angle, views, blood, burst, dodge, shake, crowdHype };
}
