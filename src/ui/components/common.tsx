import { useState } from "react";
import { getNation } from "../../core/data";
import { avatarOf } from "../../core/avatar";
import { crestUrl } from "../assets";
import { AvatarSvg } from "./Avatar";
import { stars } from "../../core/gladiator";
import type { Gladiator, NationId } from "../../core/types";

export function Stars({ value, range }: { value: number; range?: number }) {
  const s = stars(value);
  const hi = range !== undefined ? stars(range) : undefined;
  const render = (v: number) => {
    const full = Math.floor(v);
    const half = v - full >= 0.5;
    return "★".repeat(full) + (half ? "☆" : "");
  };
  return (
    <span className="stars" title={hi ? `${s}~${hi}성` : `${s}성`}>
      {render(s)}
      {hi !== undefined && hi > s && <span className="empty">~{render(hi)}</span>}
    </span>
  );
}

export function Bar({ value, max = 100, color }: { value: number; max?: number; color?: string }) {
  const pct = Math.max(0, Math.min(100, (value / max) * 100));
  const c = color ?? (pct >= 70 ? "var(--good)" : pct >= 40 ? "var(--bronze-hi)" : "var(--blood)");
  return (
    <span className="bar">
      <i style={{ width: `${pct}%`, background: c }} />
    </span>
  );
}

export function attrClass(v: number): string {
  return v <= 5 ? "a1" : v <= 10 ? "a2" : v <= 15 ? "a3" : "a4";
}

/** 실크송·림월드풍 초상화. 흉터·부상이 얼굴에 쌓인다 */
export function Portrait({ g, size = 40 }: { g: Gladiator; size?: number }) {
  const fights = g.record.wins + g.record.losses;
  const scars = Math.floor(fights / 6) + (g.traits.includes("scarred") ? 2 : 0);
  return (
    <div className={`portrait ${g.alive ? "" : "dead"}`} style={{ width: size, height: size }}>
      <AvatarSvg spec={avatarOf(g)} bg={getNation(g.nation).color} scars={scars} injured={g.injury?.severity} dead={!g.alive} seed={g.portraitSeed % 997} />
    </div>
  );
}

export function NationCrest({ nation, size = 32 }: { nation: NationId; size?: number }) {
  const n = getNation(nation);
  const [failed, setFailed] = useState(false);
  if (!failed) {
    return <img className="crest" src={crestUrl(nation)} width={size} height={size} alt={n.short} onError={() => setFailed(true)} />;
  }
  return (
    <svg className="crest" width={size} height={size} viewBox="0 0 40 40">
      <path d="M20 2 L36 8 V20 C36 30 28 36 20 38 C12 36 4 30 4 20 V8 Z" fill={n.color} stroke="var(--bronze-hi)" strokeWidth="1.5" />
      <text x="20" y="25" textAnchor="middle" fontSize="13" fontFamily="var(--serif)" fill="#f1e2c4" fontWeight="800">
        {n.short[0]}
      </text>
    </svg>
  );
}
