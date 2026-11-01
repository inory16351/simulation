import { getNation, getRace, getStyle } from "../../core/data";
import { avatarOf } from "../../core/avatar";
import { TIER_NAMES, getInjury } from "../../core/injuries";
import { AvatarSvg } from "./Avatar";
import { Crest } from "./Icons";
import { stars } from "../../core/gladiator";
import type { Gladiator, NationId, RaceId } from "../../core/types";

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
  return <Crest nation={nation} color={getNation(nation).color} size={size} />;
}

const ATTR_SHORT: Record<string, string> = {
  str: "근력", agi: "민첩", end: "지구력", tou: "강인함", rea: "반응", rec: "회복력", acc: "정확도", eva: "회피", blk: "막기",
  grp: "제압", mag: "마력", grit: "투지", comp: "침착", dec: "판단력", show: "쇼맨십", agg: "공격성", disc: "규율", lead: "리더십",
};

/** 종족 이름. 마우스를 올리면 종족 특성·능력치 보정·수명이 보인다 */
export function RaceTag({ race }: { race: RaceId }) {
  const r = getRace(race);
  const mods = Object.entries(r.attrMods).map(([k, v]) => `${ATTR_SHORT[k]} ${v! > 0 ? "+" : ""}${v}`).join(", ");
  const tip = `◆ ${r.racialTrait.name}: ${r.racialTrait.desc}\n능력치: ${mods || "보정 없음"}\n데뷔 ${r.age.debut}세 · 전성기 ${r.age.growthEnd + 1}~${r.age.primeEnd}세 · 은퇴 무렵 ${r.age.retire}세` +
    (r.wageMult !== 1 ? `\n주급 ×${r.wageMult}` : "");
  return <span className="tip-inline" data-tip={tip}>{r.name}</span>;
}

/** 전투 스타일 이름. 마우스를 올리면 설명과 상성이 보인다 */
export function StyleTag({ style }: { style: string }) {
  const s = getStyle(style);
  const strong = s.counters.map((c) => getStyle(c).name).join(", ");
  return <span className="tip-inline" data-tip={`${s.desc}${strong ? `\n유리한 상대: ${strong}` : ""}`}>{s.name}</span>;
}

/** 부상 표시. 마우스를 올리면 능력치 저하와 영구 손상 위험이 보인다 */
export function InjuryTag({ g }: { g: Gladiator }) {
  const inj = g.injury;
  if (!inj) return null;
  const def = getInjury(inj.type);
  const tier = inj.tier ? TIER_NAMES[inj.tier] : inj.severity === "serious" ? "중상" : "경상";
  const pen = def ? Object.entries(def.penalty).map(([k, v]) => `${ATTR_SHORT[k]} ${v}`).join(", ") : "";
  const tip = `${inj.desc} (${tier})\n남은 회복 ${Math.ceil(inj.daysLeft)}일${inj.total ? ` / ${inj.total}일` : ""}` +
    (pen ? `\n부상 중: ${pen}` : "") +
    (def?.permanent ? `\n영구 손상 위험 ${Math.round(def.permanent * 100)}% (의무실·메디쿠스가 줄여 준다)` : "") +
    (inj.severity === "light" ? "\n출전은 되지만 경기력 -15%. 상처가 덧날 수 있다" : "\n출전 불가");
  return <span className="tag inj tip" data-tip={tip}>✚ {inj.desc} {Math.ceil(inj.daysLeft)}일</span>;
}
