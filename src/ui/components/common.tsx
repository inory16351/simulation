import { getNation, getRace } from "../../core/data";
import { stars } from "../../core/gladiator";
import { Rng } from "../../core/rng";
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

/** 이미지 에셋이 준비되기 전까지 쓰는 절차 생성 초상화 */
export function Portrait({ g, size = 40 }: { g: Gladiator; size?: number }) {
  const race = getRace(g.race);
  const nation = getNation(g.nation);
  const r = new Rng(g.portraitSeed);
  const headW = 26 + r.int(0, 8) + (g.race === "minotaur" ? 8 : 0);
  const headH = 32 + r.int(0, 6);
  const shoulder = 70 + r.int(0, 14) + (g.race === "minotaur" || g.race === "orc" ? 10 : 0);
  const hairDark = ["#1e1712", "#2f241b", "#4a3a2a", "#6b5a45", "#121010"][r.int(0, 4)];
  const injured = !!g.injury;
  const scarCount = Math.min(3, Math.floor((g.record.wins + g.record.losses) / 8));
  return (
    <div className={`portrait ${g.alive ? "" : "dead"}`} style={{ width: size, height: size }}>
      <svg viewBox="0 0 100 100">
        <defs>
          <radialGradient id={`bg${g.id}`} cx="50%" cy="35%" r="70%">
            <stop offset="0" stopColor={nation.color} stopOpacity="0.55" />
            <stop offset="1" stopColor="#0c0a08" />
          </radialGradient>
          <linearGradient id={`sk${g.id}`} x1="0" x2="1">
            <stop offset="0" stopColor={race.color} />
            <stop offset="1" stopColor="#1a1410" />
          </linearGradient>
        </defs>
        <rect width="100" height="100" fill={`url(#bg${g.id})`} />
        {/* 어깨 */}
        <path d={`M${50 - shoulder / 2} 100 Q50 ${66 - r.int(0, 4)} ${50 + shoulder / 2} 100 Z`} fill="#2a211a" />
        {/* 목 */}
        <rect x={50 - headW / 4} y="58" width={headW / 2} height="16" fill={`url(#sk${g.id})`} />
        {/* 머리 */}
        <ellipse cx="50" cy="45" rx={headW / 2} ry={headH / 2} fill={`url(#sk${g.id})`} />
        {/* 종족 특징 */}
        {g.race === "elf" && (
          <>
            <path d={`M${50 - headW / 2} 44 l-9 -10 l7 14 Z`} fill={race.color} />
            <path d={`M${50 + headW / 2} 44 l9 -10 l-7 14 Z`} fill="#3b4a40" />
          </>
        )}
        {g.race === "minotaur" && (
          <>
            <path d={`M${50 - headW / 2 + 2} 34 q-14 -4 -16 -18 q8 10 18 12 Z`} fill="#cdbb98" />
            <path d={`M${50 + headW / 2 - 2} 34 q14 -4 16 -18 q-8 10 -18 12 Z`} fill="#a9987a" />
          </>
        )}
        {g.race === "orc" && <path d="M44 56 l2 -6 l2 6 Z M52 56 l2 -6 l2 6 Z" fill="#e8dcc0" />}
        {g.race === "beastkin" && (
          <>
            <path d={`M${50 - headW / 2 + 3} 34 l-4 -14 l10 9 Z`} fill={race.color} />
            <path d={`M${50 + headW / 2 - 3} 34 l4 -14 l-10 9 Z`} fill="#5a4320" />
          </>
        )}
        {g.race === "lizardfolk" && <path d={`M44 30 q6 -10 12 0`} stroke="#2c3f37" strokeWidth="3" fill="none" />}
        {/* 머리카락 / 수염 */}
        {g.race !== "lizardfolk" && g.race !== "minotaur" && r.chance(0.7) && (
          <path d={`M${50 - headW / 2} 42 Q50 ${22 - r.int(0, 6)} ${50 + headW / 2} 42 Q50 30 ${50 - headW / 2} 42 Z`} fill={hairDark} />
        )}
        {g.race === "dwarf" && <path d={`M${50 - headW / 2 + 2} 50 Q50 ${78 + r.int(0, 6)} ${50 + headW / 2 - 2} 50 Z`} fill={hairDark} />}
        {/* 눈 (그늘) */}
        <rect x="40" y="43" width="7" height="2.5" fill="#0d0a08" />
        <rect x="53" y="43" width="7" height="2.5" fill="#0d0a08" />
        {g.traits.includes("one_eyed") && <path d="M38 40 L62 48" stroke="#0d0a08" strokeWidth="2" />}
        {/* 흉터: 경기를 치를수록 쌓인다 */}
        {Array.from({ length: scarCount + (g.traits.includes("scarred") ? 2 : 0) }, (_, i) => (
          <path key={i} d={`M${40 + i * 7} ${36 + i * 6} l${6 + i} ${8 - i}`} stroke="#6e1c16" strokeWidth="1.3" />
        ))}
        {/* 부상: 붕대와 핏자국 */}
        {injured && (
          <>
            <path d={`M${50 - headW / 2} 34 Q50 28 ${50 + headW / 2} 34 l0 5 Q50 33 ${50 - headW / 2} 39 Z`} fill="#cbbd9e" opacity="0.9" />
            <circle cx={50 + headW / 4} cy="35" r="2.6" fill="#8e1d14" />
          </>
        )}
        {/* 측광 */}
        <path d="M0 0 H38 L18 100 H0 Z" fill="#000" opacity="0.28" />
      </svg>
    </div>
  );
}

export function NationCrest({ nation, size = 32 }: { nation: NationId; size?: number }) {
  const n = getNation(nation);
  return (
    <svg className="crest" width={size} height={size} viewBox="0 0 40 40">
      <path d="M20 2 L36 8 V20 C36 30 28 36 20 38 C12 36 4 30 4 20 V8 Z" fill={n.color} stroke="var(--bronze-hi)" strokeWidth="1.5" />
      <text x="20" y="25" textAnchor="middle" fontSize="13" fontFamily="var(--serif)" fill="#f1e2c4" fontWeight="800">
        {n.short[0]}
      </text>
    </svg>
  );
}
