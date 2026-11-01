import { PORTRAIT_CELL, paintedLayers, type PaintedSpec } from "../../core/portrait2";
import { asset } from "../assets";

/** 그림 파츠를 겹쳐 그린 초상화. view가 side면 오른쪽을 보는 옆모습(머리만) */
export function PaintedAvatar({ spec, bg, view = "front", seed = 1 }: { spec: PaintedSpec; bg?: string; view?: "front" | "side"; seed?: number }) {
  const layers = paintedLayers(spec, view);
  if (!layers) return null;
  const C = PORTRAIT_CELL;
  const id = `pa${seed}${view}`;
  return (
    <svg viewBox={`0 0 ${C} ${C}`} className="avatar painted" overflow="visible">
      <defs>
        {bg && (
          <radialGradient id={`${id}bg`} cx="45%" cy="35%" r="75%">
            <stop offset="0" stopColor={bg} stopOpacity="0.9" />
            <stop offset="1" stopColor="#120d0a" />
          </radialGradient>
        )}
        {layers.map((l, i) => l.clipBottom !== undefined && (
          <clipPath key={i} id={`${id}c${i}`}><rect x="0" y="0" width={C} height={l.clipBottom} /></clipPath>
        ))}
      </defs>
      {bg && <rect width={C} height={C} fill={`url(#${id}bg)`} />}
      {layers.map((l, i) => (
        <image key={i} href={asset(l.src)} x={l.rect[0]} y={l.rect[1]} width={l.rect[2]} height={l.rect[3]}
          preserveAspectRatio="none" clipPath={l.clipBottom !== undefined ? `url(#${id}c${i})` : undefined} />
      ))}
    </svg>
  );
}
