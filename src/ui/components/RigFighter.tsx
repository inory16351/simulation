// 전투 장면의 관절 인형. 골반 → 몸통 → 목·어깨 → 팔꿈치 → 손(무기), 골반 → 엉덩이 → 무릎으로 이어진 뼈대를
// 동작별 키프레임으로 움직인다. 그림 파츠가 있는 종족은 그림을, 없는 종족은 도형을 같은 뼈대에 붙인다.
// 비율은 약 2.5등신: 머리 ≈ 32, 몸 전체 ≈ 82 (땅 0, 위가 음수).
import { useEffect, useRef, useState } from "react";
import { avatarOf } from "../../core/avatar";
import { getArmor, getWeapon } from "../../core/data";
import { PORTRAIT_CELL, hasPainted, paintedSpec, sideNeck } from "../../core/portrait2";
import type { Gladiator } from "../../core/types";
import RIG from "../../../data/rig.json";
import { asset } from "../assets";
import { AvatarSvg } from "./Avatar";
import { PaintedAvatar } from "./PaintedAvatar";

export type Pose = "idle" | "attack" | "hurt" | "block" | "dodge" | "step" | "cast" | "stagger" | "down" | "dead" | "kneel" | "cheer";

const INK = "#1a120e";
// 뼈 길이 (장면 단위)
const B = { pelvisY: -25, neck: 23, shoulderY: -18, upperArm: 11, forearm: 10, thigh: 12.5, shin: 12.5, chest: 22, hips: 11, hipsUp: 5 };
const HEAD_S = 0.098;
/** 팔다리·몸통 굵기 배율 (기준 그림처럼 다부지게) */
const THICK = { arm: 1.65, thigh: 1.5, shin: 1.3, chest: 1.6, hips: 1.55 };

/**
 * 관절 각도. 팔다리는 0이 아래로 늘어진 자세, +가 앞(보는 쪽)으로 도는 쪽.
 * 무릎은 −가 뒤로 굽힘, 팔꿈치는 +가 앞으로 굽힘.
 * grip: 무기 날의 방향 = 아래팔 방향을 grip만큼 앞으로 돌린 쪽 (0이면 아래팔을 그대로 잇는다)
 */
interface J {
  rx: number; ry: number; spin: number; lean: number; neck: number;
  sF: number; eF: number; sB: number; eB: number; grip: number;
  hF: number; kF: number; hB: number; kB: number;
}

type Frame = [number, Partial<J>];
const ease = (x: number) => (x < 0.5 ? 4 * x * x * x : 1 - Math.pow(-2 * x + 2, 3) / 2);

/** 키프레임 사이를 부드럽게 잇는다. 첫 프레임 전은 기본 자세, 마지막 뒤는 마지막 자세 유지 */
function keyframes(t: number, frames: Frame[], base: J): J {
  const full = frames.map(([ft, p]) => [ft, { ...base, ...p }] as const);
  if (t <= 0) return { ...base };
  let prev: readonly [number, J] = [0, base];
  for (const f of full) {
    if (t <= f[0]) {
      const k = ease((t - prev[0]) / Math.max(1e-6, f[0] - prev[0]));
      const out = {} as J;
      for (const key of Object.keys(base) as (keyof J)[]) out[key] = prev[1][key] + (f[1][key] - prev[1][key]) * k;
      return out;
    }
    prev = f;
  }
  return { ...prev[1] };
}

/** 무기 쓰는 방식: 한손검+방패, 두손 내려찍기, 두손 찌르기(긴 자루), 한손 찌르기, 맨주먹, 주술 */
type Style = "slash" | "overhead" | "pole" | "stab" | "punch" | "cast";
const styleOf = (g: Gladiator): Style => {
  const c = getWeapon(g.equipment.main)?.class ?? "unarmed";
  if (c === "focus") return "cast";
  if (c === "unarmed") return "punch";
  if (c === "greatsword" || c === "axe" || c === "mace") return "overhead";
  if (c === "spear" || c === "trident_net" || c === "throwing") return "pole";
  if (c === "dagger") return "stab";
  return "slash";
};
// 다리를 넓게 벌린 경계 자세와 내딛기 (발이 땅에 닿게 골반 높이는 그릴 때 계산한다)
const LEGS = { hF: 46, kF: -50, hB: -42, kB: -4, ry: 0 };
const LUNGE = { hF: 62, kF: -68, hB: -55, kB: -2, ry: 0 };

/** 경계 자세 (기준 그림 1·4번): 다리를 벌리고 앞무릎을 굽힌다 */
function guard(style: Style): J {
  const base = { rx: 0, spin: 0, lean: 6, neck: -4, ...LEGS };
  switch (style) {
    case "overhead": return { ...base, sF: 40, eF: 70, sB: 30, eB: 75, grip: 40 };
    case "pole": return { ...base, sF: 55, eF: 35, sB: 25, eB: 65, grip: 0 };
    case "punch": return { ...base, sF: 45, eF: 80, sB: 30, eB: 90, grip: 0 };
    case "cast": return { ...base, sF: 50, eF: 40, sB: 30, eB: 60, grip: 70 };
    default: return { ...base, sF: 40, eF: 45, sB: 50, eB: 40, grip: 10 };
  }
}

/** 동작별 자세: t는 동작이 시작된 뒤 흐른 초 */
export function poseAt(pose: Pose, t: number, style: Style): J {
  const G = guard(style);
  const breathe = Math.sin(t * 2.6);
  const idle = { ...G, ry: G.ry + breathe * 0.5, sF: G.sF + breathe * 2, sB: G.sB - breathe * 1.5 };
  const kf = (frames: Frame[]) => keyframes(t, frames, G);
  switch (pose) {
    case "idle":
      return idle;
    case "attack":
      if (style === "overhead") return kf([   // 기준 그림 6·7번: 두 손으로 머리 위로 들었다 앞으로 찍는다
        [0.2, { sF: 160, eF: 20, sB: 150, eB: 30, grip: 30, lean: -12, rx: -5, ...LEGS, hF: 18, kF: -25 }],
        [0.3, { sF: 60, eF: -5, sB: 50, eB: 5, grip: 0, lean: 26, rx: 14, ...LUNGE }],
        [0.44, { sF: 45, eF: -5, sB: 38, eB: 5, grip: -5, lean: 28, rx: 14, ...LUNGE }],
        [0.8, {}],
      ]);
      if (style === "pole") return kf([       // 기준 그림 4·5번: 자루를 당겼다가 쭉 내지른다
        [0.14, { sF: 30, eF: 60, sB: 10, eB: 80, rx: -4, lean: -4 }],
        [0.22, { sF: 85, eF: 5, sB: 65, eB: 25, rx: 17, lean: 14, ...LUNGE }],
        [0.36, { sF: 82, eF: 8, sB: 62, eB: 28, rx: 16, lean: 14, ...LUNGE }],
        [0.7, {}],
      ]);
      if (style === "stab" || style === "punch") return kf([
        [0.12, { sF: 10, eF: 110, rx: -3, lean: -6 }],
        [0.2, { sF: 88, eF: 0, grip: 0, rx: 13, lean: 16, ...LUNGE }],
        [0.32, { sF: 85, eF: 4, rx: 13, lean: 15, ...LUNGE }],
        [0.62, {}],
      ]);
      if (style === "cast") return kf([
        [0.18, { sF: 80, eF: 60, lean: -6 }],
        [0.32, { sF: 110, eF: 0, sB: 85, eB: 10, lean: 8, rx: 3, grip: 50 }],
        [0.55, { sF: 110, eF: 0, sB: 85, eB: 10, lean: 8, rx: 3, grip: 50 }],
        [0.85, {}],
      ]);
      return kf([                              // 기준 그림 2·3번: 칼을 머리 뒤로 들었다가 앞으로 베어 내린다
        [0.16, { sF: 165, eF: 15, grip: 25, lean: -10, rx: -3, sB: 60, eB: 50 }],
        [0.25, { sF: 70, eF: -10, grip: 0, lean: 18, rx: 13, sB: 30, eB: 30, ...LUNGE }],
        [0.36, { sF: 45, eF: -8, grip: -5, lean: 22, rx: 14, sB: 25, eB: 30, ...LUNGE }],
        [0.72, {}],
      ]);
    case "hurt":                                // 기준 그림 9번: 팔이 펼쳐지며 뒤로 밀린다
      return kf([
        [0.06, { rx: -11, lean: -24, neck: -26, sF: 120, eF: -20, sB: 150, eB: -10, grip: 20, hF: 25, kF: -10, hB: -40, kB: -30, ry: 0 }],
        [0.22, { rx: -12, lean: -12, neck: -10 }],
        [0.55, {}],
      ]);
    case "block":                               // 기준 그림 8번: 방패를 들어 올리며 몸을 뒤로
      return kf([
        [0.1, { sB: 115, eB: 35, lean: -14, rx: -3, sF: 20, eF: 60, grip: 20, kF: -55, kB: -20 }],
        [0.4, { sB: 112, eB: 38, lean: -12, rx: -3, sF: 20, eF: 60, grip: 20, kF: -55, kB: -20 }],
        [0.62, {}],
      ]);
    case "dodge":
      return kf([
        [0.12, { rx: -18, ry: -6, lean: -20, hF: 45, kF: -65, hB: -25, kB: -55 }],
        [0.3, { rx: -15, ry: 2, lean: -8 }],
        [0.55, {}],
      ]);
    case "step": {
      // 경계 자세를 유지한 채 다리를 번갈아 내딛는다 (0.45초에 한 걸음)
      const p = Math.min(1, t / 0.45), w = Math.sin(p * Math.PI * 2);
      return { ...idle, hF: G.hF + 18 * w, kF: G.kF - 15 * Math.max(0, -w), hB: G.hB - 18 * w, kB: G.kB - 18 * Math.max(0, w), ry: G.ry - Math.abs(w) * 1.5 };
    }
    case "stagger":
      return kf([
        [0.15, { lean: -16, neck: 12, sF: 10, eF: 20, sB: -10, rx: -4 }],
        [0.32, { lean: 12, neck: -8, rx: 2 }],
        [0.5, { lean: -6, neck: 4 }],
        [0.8, {}],
      ]);
    case "down":
    case "dead":
      // 뒤로 넘어간다: 발을 축으로 몸 전체가 돈다
      return kf([
        [0.15, { lean: -20, neck: -20, rx: -4, kF: -40, kB: -40, ry: 4 }],
        [0.5, { spin: -84, lean: 0, neck: pose === "dead" ? 25 : 10, rx: -6, ry: 0, sF: 120, eF: 10, sB: 140, eB: 20, hF: 10, kF: -5, hB: -5, kB: -5 }],
      ]);
    case "kneel":
      return kf([[0.4, { ry: 9, hF: 70, kF: -110, hB: -10, kB: -95, lean: 18, neck: 18, sF: 5, eF: 10, sB: 5, eB: 5, grip: -60 }]]);
    case "cheer": {
      const w = Math.sin(t * 9);
      return { ...idle, sF: 170 + w * 8, eF: 10, grip: 0, lean: -6, ry: G.ry - Math.abs(w) * 2 };
    }
  }
  return idle;
}

/** 동작이 바뀐 뒤 흐른 시간. 화면에선 매 프레임 다시 그린다 */
function useAnimTime(key: number, fixed?: number): number {
  const [t, setT] = useState(fixed ?? 0);
  const start = useRef(0);
  useEffect(() => {
    if (fixed !== undefined) return;
    start.current = performance.now();
    let id = 0;
    const tick = () => { setT((performance.now() - start.current) / 1000); id = requestAnimationFrame(tick); };
    id = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(id);
  }, [key, fixed]);
  return fixed ?? t;
}

type RigName = keyof typeof RIG.parts;

/** 위 관절을 (0,0)에 두고 아래로 늘어뜨린 그림 파츠 (관절 사이 길이 = len) */
function Piece({ name, len, filter, thick = 1 }: { name: RigName; len: number; filter?: string; thick?: number }) {
  const p = RIG.parts[name];
  const sc = len / (p.bottom[1] - p.top[1]);
  const w = p.w * sc * thick;
  return <image href={asset(`rig/${name}.webp`)} x={-p.top[0] * sc * thick} y={-p.top[1] * sc} width={w} height={p.h * sc} filter={filter} preserveAspectRatio="none" />;
}

/** 무기 종류 → 그림, 장면 길이, 손으로 쥐는 자리(아래끝에서 전체 길이의 비율) */
const WEAPONS: Record<string, [RigName, number, number]> = {
  sword: ["w_sword", 40, 0.12], greatsword: ["w_greatsword", 56, 0.1], axe: ["w_axe", 50, 0.14], mace: ["w_mace", 40, 0.12],
  dagger: ["w_dagger", 22, 0.15], spear: ["w_spear", 86, 0.45], trident_net: ["w_trident", 80, 0.45], throwing: ["w_javelin", 72, 0.45], focus: ["w_staff", 62, 0.35],
};
/** 손(0,0)에 쥔 그림 무기. 그림은 날이 위(−y)로 그려져 있고, 쥐는 쪽에서 날 방향을 정한다 */
function PaintedWeapon({ cls }: { cls: string }) {
  const w = WEAPONS[cls];
  if (!w) return null;
  const [name, len, grip] = w;
  const p = RIG.parts[name];
  if (!p) return null;
  const sc = len / p.h;
  const gripY = p.h * (1 - grip);
  return <image href={asset(`rig/${name}.webp`)} x={-p.bottom[0] * sc} y={-gripY * sc} width={p.w * sc} height={p.h * sc} />;
}

/** 위끝이 top, 높이가 height가 되게 가운데 맞춰 놓는다 (몸통·하반신) */
function Fit({ name, top, height, wide = 1 }: { name: RigName; top: number; height: number; wide?: number }) {
  const p = RIG.parts[name];
  if (!p) return null;
  const sc = height / p.h;
  return <image href={asset(`rig/${name}.webp`)} x={-p.top[0] * sc * wide} y={top} width={p.w * sc * wide} height={height} preserveAspectRatio="none" />;
}

/** 도형 팔다리 (그림 파츠가 없는 종족) */
const Stick = ({ len, w, color }: { len: number; w: number; color: string }) => (
  <path d={`M0 0 L0 ${len}`} stroke={color} strokeWidth={w} strokeLinecap="round" />
);

export interface RigFighterProps {
  g: Gladiator;
  pose: Pose;
  /** 같은 동작이 다시 나와도 처음부터 움직이게 하는 키 */
  poseKey: number;
  hpRatio: number;
  /** 치명타: 아주 잠깐 멈췄다가 움직인다 */
  hitstop?: boolean;
  /** 정지 화면 렌더링용 고정 시간 */
  fixedTime?: number;
  weapon: React.ReactNode;
}

export function RigFighter({ g, pose, poseKey, hpRatio, hitstop, fixedTime, weapon }: RigFighterProps) {
  const raw = useAnimTime(poseKey, fixedTime);
  const t = Math.max(0, raw - (hitstop ? 0.1 : 0));
  const j = poseAt(pose, t, styleOf(g));
  const s = avatarOf(g);
  const painted = hasPainted(g.race, "side") ? paintedSpec(g) : null;
  const neckPt = painted ? sideNeck(painted) : null;
  const skin = painted ? (RIG.skin as Record<string, number[][]>)[g.race]?.[painted.head] : null;
  const rig = painted && neckPt && skin;
  const skinId = `sk${g.portraitSeed % 99991}`;
  const [br, bgc, bb] = RIG.baseSkin;
  const weight = getArmor(g.equipment.armor)?.weight;
  const kit = weight === "heavy" || weight === "medium" ? "iron" : weight === "light" ? "leather" : "tunic";
  const chestName = `chest_${kit}` as RigName, hipsName = `hips_${kit}` as RigName;
  const shield = g.equipment.off ? getArmor(g.equipment.off)?.slot === "shield" : false;
  const bodyColor = s.armor === "heavy" ? "#b08a4f" : s.armor === "medium" ? "#6f7378" : s.cloth;
  const flash = pose === "hurt" && raw < 0.09;

  const limbFilter = rig ? `url(#${skinId})` : undefined;
  const rad = Math.PI / 180;
  const footY = (h: number, k: number) => B.thigh * Math.cos(h * rad) + B.shin * Math.cos((h + k) * rad);
  const plant = -B.pelvisY - Math.max(footY(j.hF, j.kF), footY(j.hB, j.kB));
  const leg = (front: boolean) => {
    const h = front ? j.hF : j.hB, k = front ? j.kF : j.kB;
    return (
      <g transform={`translate(${front ? 2 : -2} 0) rotate(${-h})`}>
        {rig ? <Piece name="thigh_bare" len={B.thigh} filter={limbFilter} thick={THICK.thigh} /> : <Stick len={B.thigh} w={7} color="#3a2c22" />}
        <g transform={`translate(0 ${B.thigh}) rotate(${-k})`}>
          {rig ? <Piece name="shin" len={B.shin * 0.82} filter={limbFilter} thick={THICK.shin} /> : <Stick len={B.shin} w={6} color="#3a2c22" />}
        </g>
      </g>
    );
  };
  const arm = (front: boolean) => {
    const sh = front ? j.sF : j.sB, el = front ? j.eF : j.eB;
    // 손 좌표계에서 아래팔 방향은 +y. 무기 그림(날이 −y)을 180° 돌려 아래팔을 잇게 하고 grip만큼 앞으로
    const held = front
      ? <g transform={`rotate(${180 - j.grip})`}>{rig ? <PaintedWeapon cls={getWeapon(g.equipment.main)?.class ?? "unarmed"} /> : <g transform="rotate(180)">{weapon}</g>}</g>
      : null;
    return (
      <g transform={`translate(${front ? 3 : -3} ${B.shoulderY}) rotate(${-sh})`}>
        {rig ? <Piece name="upper_arm" len={B.upperArm} filter={limbFilter} thick={THICK.arm} /> : <Stick len={B.upperArm} w={5.5} color={s.skin} />}
        <g transform={`translate(0 ${B.upperArm}) rotate(${-el})`}>
          {front && <g transform={`translate(0 ${B.forearm})`}>{held}</g>}
          {rig ? <Piece name="forearm" len={B.forearm} filter={limbFilter} thick={THICK.arm} /> : <Stick len={B.forearm} w={5} color={s.skin} />}
          {/* 방패는 몸 앞에 그려야 해서 따로 (shieldItem) */}
        </g>
      </g>
    );
  };

  // 방패: 뒷손 자리를 같은 관절 사슬로 따라가되, 몸통·머리보다 앞에 그린다
  const shieldItem = shield && (
    <g transform={`translate(-3 ${B.shoulderY}) rotate(${-j.sB}) translate(0 ${B.upperArm}) rotate(${-j.eB}) translate(0 ${B.forearm}) rotate(${j.sB + j.eB - j.lean})`}>
      {rig
        ? <image href={asset("rig/shield.webp")} x={-9} y={-20} width={(RIG.parts.shield.w / RIG.parts.shield.h) * 36} height={36} />
        : <g transform="translate(-2 -4)"><ellipse rx="10" ry="14" fill="#a8823f" stroke={INK} strokeWidth="1.6" /><circle r="2.6" fill="#7a5c33" /></g>}
    </g>
  );

  const head = rig ? (
    <svg x={-neckPt.x * HEAD_S} y={-neckPt.y * HEAD_S + 2} width={PORTRAIT_CELL * HEAD_S} height={PORTRAIT_CELL * HEAD_S} viewBox={`0 0 ${PORTRAIT_CELL} ${PORTRAIT_CELL}`} overflow="visible">
      <PaintedAvatar spec={painted} view="side" seed={g.portraitSeed % 997} />
    </svg>
  ) : (
    <svg x="-22" y="-40" width="44" height="44" viewBox="0 0 100 100" overflow="visible">
      <AvatarSvg spec={s} bg="none" headOnly injured={hpRatio < 0.35 ? "serious" : hpRatio < 0.7 ? "light" : undefined} seed={g.portraitSeed % 997} />
    </svg>
  );

  return (
    <g style={{ filter: flash ? "brightness(1.8) saturate(0.4)" : pose === "dead" ? "grayscale(0.85) brightness(0.7)" : undefined }}>
      {rig && <defs><filter id={skinId}><feColorMatrix type="matrix" values={`${(skin[0] / br).toFixed(3)} 0 0 0 0  0 ${(skin[1] / bgc).toFixed(3)} 0 0 0  0 0 ${(skin[2] / bb).toFixed(3)} 0 0  0 0 0 1 0`} /></filter></defs>}
      {/* 넘어질 때는 발을 축으로 몸 전체가 돈다 */}
      <g transform={`translate(${j.rx} ${j.ry + (j.spin ? 0 : plant)}) rotate(${j.spin})`}>
        <g transform={`translate(0 ${B.pelvisY})`}>
          {leg(false)}
          {/* 윗몸: 골반을 축으로 기운다 */}
          <g transform={`rotate(${j.lean})`}>
            {arm(false)}
            {rig
              ? <Fit name={chestName} top={-B.neck + 1} height={B.chest} wide={THICK.chest} />
              : <rect x="-11" y={-B.neck - 2} width="22" height={B.neck + 6} rx="8" fill={bodyColor} stroke={INK} strokeWidth="1.6" />}
          </g>
          {leg(true)}
          {/* 하반신: 골반에 붙어 다리 윗부분을 덮는다 */}
          {rig && <Fit name={hipsName} top={-B.hipsUp} height={B.hips} wide={THICK.hips} />}
          <g transform={`rotate(${j.lean})`}>
            <g transform={`translate(0 ${-B.neck}) rotate(${j.neck})`}>{head}</g>
            {shieldItem}
            {arm(true)}
          </g>
        </g>
      </g>
    </g>
  );
}
