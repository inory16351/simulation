import { useMemo, useState } from "react";
import { ludusOf } from "../../core/career";
import { NATIONS, RACES, STYLES } from "../../core/data";
import { josa } from "../../core/josa";
import { marketValue, releaseGladiator, rosterCapacity } from "../../core/economy";
import { ageStage } from "../../core/gladiator";
import { ludusReputation, REPUTATION_NAMES } from "../../core/reputation";
import { Rng } from "../../core/rng";
import { assignScout, caRange, knowledgeOf, paRange, scoutSlots, scoutingOf } from "../../core/scouting";
import { acceptOffer, completeTransfer, inWindow, rejectOffer, transferDemand, transferOf, windowLabel } from "../../core/transfers";
import type { Gladiator, RaceId } from "../../core/types";
import type { Ctx } from "../App";
import { Bar, Portrait, RaceTag, StyleTag } from "../components/common";
import { Negotiate } from "../components/Negotiate";

type Tab = "search" | "shortlist" | "deals" | "mine";

/** 범위 별점: 추정치가 넓을수록 범위로 보인다 */
export function RangeStars({ lo, hi }: { lo: number; hi: number }) {
  const st = (v: number) => Math.max(0.5, Math.min(5, Math.round((v / 40) * 2) / 2));
  const draw = (v: number) => "★".repeat(Math.floor(v)) + (v % 1 ? "☆" : "");
  const a = st(lo), b = st(hi);
  return <span className="stars" title={`${lo}~${hi}`}>{draw(a)}{b > a && <span className="empty">~{draw(b)}</span>}</span>;
}

export function MarketScreen(ctx: Ctx) {
  const { state } = ctx;
  const [tab, setTab] = useState<Tab>("search");
  const t = transferOf(state);
  const me = state.ludi[state.playerLudusId];
  const rep = ludusReputation(state, me);
  return (
    <>
      <h1 className="screen-title">이적 시장</h1>
      <div className="screen-sub">
        우리 명성 {rep} ({REPUTATION_NAMES(rep)}) · 막사 {me.gladiators.length}/{rosterCapacity(me)}명 ·
        {inWindow(state) ? <b style={{ color: "var(--good)" }}> 이적 시장 열림</b> : <span> 이적은 {windowLabel}에만 (무소속 영입은 언제든)</span>}
      </div>
      <div className="seg" style={{ marginBottom: 12 }}>
        <button className={tab === "search" ? "on" : ""} onClick={() => setTab("search")}>선수 찾기</button>
        <button className={tab === "shortlist" ? "on" : ""} onClick={() => setTab("shortlist")}>관심 명단 ({state.shortlist?.length ?? 0})</button>
        <button className={tab === "deals" ? "on" : ""} onClick={() => setTab("deals")}>협상·제안 ({t.deals.length + t.offers.length})</button>
        <button className={tab === "mine" ? "on" : ""} onClick={() => setTab("mine")}>우리 검투사</button>
      </div>
      {tab === "search" && <Search {...ctx} />}
      {tab === "shortlist" && <ResultTable ctx={ctx} list={(state.shortlist ?? []).map((id) => state.gladiators[id]).filter((g) => g?.alive)} />}
      {tab === "deals" && <Deals {...ctx} />}
      {tab === "mine" && <Mine {...ctx} />}
    </>
  );
}

function Search(ctx: Ctx) {
  const { state } = ctx;
  const [scope, setScope] = useState<"all" | "free" | "clubs">("all");
  const [race, setRace] = useState<RaceId | "">("");
  const [style, setStyle] = useState("");
  const [maxAge, setMaxAge] = useState("");
  const [minCa, setMinCa] = useState(0);
  const [minPa, setMinPa] = useState(0);
  const [maxWage, setMaxWage] = useState("");
  const [name, setName] = useState("");
  const [nation, setNation] = useState<string>(state.nation);

  const pool = useMemo(() => {
    const me = state.ludi[state.playerLudusId];
    const free = state.freeAgents.map((id) => state.gladiators[id]).filter(Boolean);
    // 다른 나라 루두스도 찾아볼 수 있다
    const clubs = Object.values(state.ludi).filter((l) => !l.isPlayer && (!nation || l.nation === nation)).flatMap((l) => l.gladiators.map((id) => state.gladiators[id]));
    const all = scope === "free" ? free : scope === "clubs" ? clubs : [...free, ...clubs];
    return all.filter((g) => g?.alive && !me.gladiators.includes(g.id) && ageStage(g) !== "youth");
  }, [state, scope, nation]);

  const results = pool
    .filter((g) => !race || g.race === race)
    .filter((g) => !style || g.style === style)
    .filter((g) => !maxAge || g.age <= Number(maxAge))
    .filter((g) => !maxWage || g.wage <= Number(maxWage))
    .filter((g) => !name || g.name.includes(name))
    .map((g) => ({ g, ca: caRange(state, g), pa: paRange(state, g) }))
    .filter((r) => r.ca.mid >= minCa && r.pa.mid >= minPa)
    .sort((a, b) => b.ca.mid - a.ca.mid)
    .slice(0, 80);

  return (
    <>
      <div className="panel">
        <h3>검색 조건</h3>
        <div className="pad search-filters">
          <label>대상 <select value={scope} onChange={(e) => setScope(e.target.value as typeof scope)}>
            <option value="all">전체</option><option value="free">무소속만</option><option value="clubs">다른 루두스만</option>
          </select></label>
          <label>나라 <select value={nation} onChange={(e) => setNation(e.target.value)}>
            <option value="">전부</option>{NATIONS.map((n) => <option key={n.id} value={n.id}>{n.name}</option>)}
          </select></label>
          <label>종족 <select value={race} onChange={(e) => setRace(e.target.value as RaceId | "")}>
            <option value="">전부</option>{RACES.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
          </select></label>
          <label>스타일 <select value={style} onChange={(e) => setStyle(e.target.value)}>
            <option value="">전부</option>{STYLES.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </select></label>
          <label>나이 ≤ <input type="number" value={maxAge} onChange={(e) => setMaxAge(e.target.value)} placeholder="제한 없음" /></label>
          <label>추정 능력 ≥ <select value={minCa} onChange={(e) => setMinCa(Number(e.target.value))}>
            {[0, 40, 60, 80, 100, 120].map((v) => <option key={v} value={v}>{v ? `★${v / 40}` : "제한 없음"}</option>)}
          </select></label>
          <label>추정 잠재 ≥ <select value={minPa} onChange={(e) => setMinPa(Number(e.target.value))}>
            {[0, 60, 80, 100, 120, 140, 160].map((v) => <option key={v} value={v}>{v ? `★${v / 40}` : "제한 없음"}</option>)}
          </select></label>
          <label>주급 ≤ <input type="number" value={maxWage} onChange={(e) => setMaxWage(e.target.value)} placeholder="제한 없음" /></label>
          <label>이름 <input type="text" value={name} onChange={(e) => setName(e.target.value)} /></label>
        </div>
      </div>
      <ResultTable ctx={ctx} list={results.map((r) => r.g)} />
    </>
  );
}

function ResultTable({ ctx, list }: { ctx: Ctx; list: Gladiator[] }) {
  const { state, act, update, nav } = ctx;
  const sc = scoutingOf(state);
  const [msg, setMsg] = useState<string | null>(null);
  return (
    <div className="panel">
      <h3>{list.length}명 · 정찰 {sc.assignments.length}/{scoutSlots(state)}</h3>
      {msg && <div className="pad"><span className="tag neg">{msg}</span></div>}
      <table className="t">
        <thead><tr><th>이름</th><th>종족</th><th>나이</th><th>스타일</th><th>소속</th><th>능력 (추정)</th><th>잠재 (추정)</th><th>정보</th><th className="num">주급</th><th className="num">추정 가치</th><th /></tr></thead>
        <tbody>
          {list.length === 0 && <tr><td colSpan={11} className="hint">조건에 맞는 검투사가 없다.</td></tr>}
          {list.map((g) => {
            const owner = ludusOf(state, g.id);
            const ca = caRange(state, g), pa = paRange(state, g);
            const k = knowledgeOf(state, g);
            const short = state.shortlist?.includes(g.id);
            const scouting = sc.assignments.includes(g.id);
            return (
              <tr key={g.id}>
                <td><div className="name-cell" style={{ cursor: "pointer" }} onClick={() => nav.openGladiator(g.id)}><Portrait g={g} size={28} /><span className="n">{g.name}</span></div></td>
                <td><RaceTag race={g.race} /></td>
                <td>{g.age}</td>
                <td><StyleTag style={g.style} /></td>
                <td>{owner ? owner.name : <span style={{ color: "var(--good)" }}>무소속</span>}</td>
                <td><RangeStars lo={ca.lo} hi={ca.hi} /></td>
                <td><RangeStars lo={pa.lo} hi={pa.hi} /></td>
                <td><Bar value={k} color="var(--bronze-hi)" /></td>
                <td className="num">{g.wage} D</td>
                <td className="num">{owner ? `${Math.round(marketValue(g) / 50) * 50} D` : "-"}</td>
                <td style={{ whiteSpace: "nowrap" }}>
                  <button className="mini" title={short ? "관심 명단에서 빼기" : "관심 명단에 넣기"} onClick={() => update((d) => {
                    d.shortlist ??= [];
                    d.shortlist = short ? d.shortlist.filter((x) => x !== g.id) : [...d.shortlist, g.id];
                  })}>{short ? "★" : "☆"}</button>{" "}
                  <button className="mini" disabled={scouting || k >= 100} title="정찰을 맡긴다" onClick={() => {
                    const err = assignScout(structuredClone(state), g.id);
                    setMsg(err);
                    if (!err) act((d) => { assignScout(d, g.id); });
                  }}>{scouting ? "정찰 중" : "정찰"}</button>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function Deals({ state, act, nav }: Ctx) {
  const t = transferOf(state);
  const [msg, setMsg] = useState<string | null>(null);
  const roll = () => new Rng((state.rngState ^ state.nextId) >>> 0).next();
  return (
    <>
      {msg && <div className="panel"><div className="pad"><span className="tag neg">{msg}</span></div></div>}
      <div className="panel">
        <h3>이적료 합의 — 개인 계약 협상</h3>
        <table className="t">
          <tbody>
            {t.deals.length === 0 && <tr><td className="hint">진행 중인 협상이 없다. 다른 루두스 검투사의 상세 화면에서 이적료를 제안하라.</td></tr>}
            {t.deals.map((d) => {
              const g = state.gladiators[d.gladiatorId];
              return (
                <tr key={d.id}>
                  <td><div className="name-cell" style={{ cursor: "pointer" }} onClick={() => nav.openGladiator(g.id)}><Portrait g={g} size={28} /><span className="n">{g.name}</span></div></td>
                  <td>{state.ludi[d.fromLudus].name} · 이적료 {d.fee.toLocaleString()} D</td>
                  <td>
                    <Negotiate demand={transferDemand(state, g)} seasons label="계약 제안" onOffer={(wage, seasons) => {
                      const r = roll();
                      setMsg(completeTransfer(structuredClone(state), d.id, wage, seasons, r));
                      act((s) => { completeTransfer(s, d.id, wage, seasons, r); });
                    }} />
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
      <div className="panel">
        <h3>받은 이적 제안</h3>
        <table className="t">
          <tbody>
            {t.offers.length === 0 && <tr><td className="hint">받은 제안이 없다.</td></tr>}
            {t.offers.map((o) => {
              const g = state.gladiators[o.gladiatorId];
              return (
                <tr key={o.id}>
                  <td><div className="name-cell"><Portrait g={g} size={28} /><span className="n">{g.name}</span></div></td>
                  <td>{state.ludi[o.fromLudus].name}</td>
                  <td className="num">{o.fee.toLocaleString()} D</td>
                  <td style={{ display: "flex", gap: 6 }}>
                    <button className="btn primary" onClick={() => act((s) => { acceptOffer(s, o.id); })}>판다</button>
                    <button className="btn" onClick={() => act((s) => rejectOffer(s, o.id))}>거절</button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </>
  );
}

function Mine({ state, act }: Ctx) {
  const me = state.ludi[state.playerLudusId];
  const list = me.gladiators.map((id) => state.gladiators[id]).filter((g) => g?.alive);
  return (
    <div className="panel">
      <h3>우리 검투사 — 방출</h3>
      <div className="pad hint">방출하면 무소속이 되고 한 푼도 받지 못한다. 팔려면 이적 시장 기간에 다른 루두스의 제안을 기다려라.</div>
      <table className="t">
        <tbody>
          {list.map((g) => (
            <tr key={g.id}>
              <td><div className="name-cell"><Portrait g={g} size={26} /><span className="n">{g.name}</span></div></td>
              <td><StyleTag style={g.style} /></td>
              <td className="num">주급 {g.wage} D</td>
              <td className="num">추정 가치 {Math.round(marketValue(g) / 50) * 50} D</td>
              <td><button className="btn danger" disabled={!!g.deployment} onClick={() => { if (confirm(`${josa(g.name, "을/를")} 방출할까요?`)) act((s) => releaseGladiator(s, g.id)); }}>방출</button></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
