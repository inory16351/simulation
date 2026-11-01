import { useState } from "react";
import { absDay, dateLabel, fromAbs } from "../../core/calendar";
import { NATIONS, getNation } from "../../core/data";
import { computeCA } from "../../core/gladiator";
import { CONTRACT_TYPES, activeWars, canTake, eligible, favorOf, takeContract } from "../../core/war";
import type { Contract } from "../../core/types";
import type { Ctx } from "../App";
import { NationCrest, Portrait, Stars } from "./common";

export function WarPanel({ state, act, nav }: Ctx) {
  const wars = activeWars(state);
  const t = absDay(state.season, state.day);
  const open = state.contracts.filter((c) => c.status === "open");
  const active = state.contracts.filter((c) => c.status === "active");

  return (
    <>
      <div className="panel">
        <h3>진행 중인 전쟁</h3>
        <div className="pad">
          {wars.length === 0 && <div className="hint">대륙은 지금 잠잠하다. 관계가 -50 아래로 떨어진 나라끼리는 언제든 전쟁이 날 수 있다.</div>}
          {wars.map((w) => {
            const pct = (w.front + 100) / 2;
            return (
              <div key={w.id} className="war-row">
                <div className="war-side"><NationCrest nation={w.a} size={28} /> {getNation(w.a).short}</div>
                <div className="war-bar" title={`전황 ${Math.round(w.front)}`}>
                  <i style={{ width: `${pct}%`, background: getNation(w.a).color }} />
                  <i style={{ width: `${100 - pct}%`, background: getNation(w.b).color }} />
                </div>
                <div className="war-side right">{getNation(w.b).short} <NationCrest nation={w.b} size={28} /></div>
                <div className="hint">{w.endsAt - t > 0 ? `최대 ${w.endsAt - t}일 더` : "곧 끝난다"}</div>
              </div>
            );
          })}
        </div>
      </div>

      {active.length > 0 && (
        <div className="panel">
          <h3>전장에 나간 검투사</h3>
          <table className="t">
            <tbody>
              {active.map((c) => (
                <tr key={c.id}>
                  <td>{getNation(c.employer).short} · {CONTRACT_TYPES[c.type].name}</td>
                  <td>{c.assigned.map((id) => state.gladiators[id]).filter((g) => g?.alive).map((g) => (
                    <span key={g.id} className="name-cell" style={{ display: "inline-flex", marginRight: 10, cursor: "pointer" }} onClick={() => nav.openGladiator(g.id)}><Portrait g={g} size={24} /> {g.name}</span>
                  ))}</td>
                  <td className="hint">전공 {c.merit} · {dateLabel(fromAbs(c.endsAt!).day)} 귀환</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <div className="panel">
        <h3>용병 의뢰 게시판</h3>
        <div className="pad">
          {open.length === 0 && <div className="hint">지금 올라온 의뢰가 없다. 전쟁이 나면 양국이 의뢰를 붙인다.</div>}
          {open.map((c) => <ContractCard key={c.id} c={c} ctx={{ state, act, nav } as Ctx} />)}
        </div>
      </div>

      <div className="panel">
        <h3>나라별 호감도</h3>
        <table className="t">
          <tbody>
            {NATIONS.map((n) => {
              const f = favorOf(state, n.id);
              return (
                <tr key={n.id}>
                  <td>{n.short}{n.id === state.nation && <span className="tag" style={{ marginLeft: 6 }}>조국</span>}</td>
                  <td className="num" style={{ color: f >= 30 ? "var(--good)" : f <= -30 ? "#e08b80" : "var(--text)" }}>{f > 0 ? `+${f}` : f}</td>
                  <td className="hint">{f <= -60 ? "적대: 그 나라 관중이 야유한다" : f >= 50 ? "우호: 의뢰를 먼저 받는다" : ""}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </>
  );
}

function ContractCard({ c, ctx }: { c: Contract; ctx: Ctx }) {
  const { state, act } = ctx;
  const [picked, setPicked] = useState<string[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const spec = CONTRACT_TYPES[c.type];
  const blocked = canTake(state, c);
  const roster = state.ludi[state.playerLudusId].gladiators.map((id) => state.gladiators[id]).filter((g) => g?.alive);
  const t = absDay(state.season, state.day);
  const toggle = (id: string) => setPicked((p) => (p.includes(id) ? p.filter((x) => x !== id) : p.length < c.slots ? [...p, id] : p));

  const send = () => {
    for (const id of picked) {
      const why = eligible(state, state.gladiators[id], c);
      if (why) return setErr(`${state.gladiators[id].name}: ${why}`);
    }
    setErr(null);
    act((d) => { takeContract(d, c.id, picked); });
  };

  return (
    <div className={`contract ${c.type === "levy" ? "levy" : ""}`}>
      <div className="contract-head">
        <NationCrest nation={c.employer} size={30} />
        <div style={{ flex: 1 }}>
          <div className="nc-name">{getNation(c.employer).short} · {spec.name}{c.type === "levy" && <span className="tag neg" style={{ marginLeft: 6 }}>징발령</span>}</div>
          <div className="hint">{spec.desc} · 적: {getNation(c.enemy).short}</div>
        </div>
        <div className="contract-terms">
          <div>1인당 <b>{c.pay.toLocaleString()} D</b> (선금 30%)</div>
          <div className="hint">{c.slots}명까지 · {c.days}일 · CA {c.minCA} 이상 · 마감 {c.expiresAt - t}일</div>
        </div>
      </div>
      {blocked ? <div className="tag neg">{blocked}</div> : (
        <>
          <div className="contract-pick">
            {roster.map((g) => {
              const why = eligible(state, g, c);
              return (
                <label key={g.id} className={`pick ${why ? "off" : ""} ${picked.includes(g.id) ? "on" : ""}`} title={why ?? ""}>
                  <input type="checkbox" disabled={!!why} checked={picked.includes(g.id)} onChange={() => toggle(g.id)} />
                  <Portrait g={g} size={24} /> {g.name} <Stars value={computeCA(g)} />
                </label>
              );
            })}
          </div>
          {err && <div className="tag neg">{err}</div>}
          <button className={`btn ${c.type === "duel" ? "danger" : "primary"}`} disabled={!picked.length} onClick={send}>
            {picked.length}명 파견
          </button>
        </>
      )}
    </div>
  );
}
