import { useState } from "react";
import { NATIONS, getNation, getStyle } from "../../core/data";
import type { NationId } from "../../core/types";
import type { Ctx } from "../App";
import { NationCrest } from "../components/common";
import { WarPanel } from "../components/WarPanel";
import { activeWars, relation } from "../../core/war";

const relColor = (v: number) => (v <= -50 ? "#e06a5a" : v < 0 ? "#c9a07a" : v >= 30 ? "var(--good)" : "var(--text)");
const relLabel = (v: number) => (v <= -50 ? "적대" : v < 0 ? "긴장" : v >= 30 ? "우호" : "중립");

export function WorldScreen(ctx: Ctx) {
  const { state } = ctx;
  const [sel, setSel] = useState<NationId>(state.nation);
  const [tab, setTab] = useState<"nations" | "war">("nations");
  const n = getNation(sel);

  return (
    <>
      <h1 className="screen-title">대륙 정세</h1>
      <div className="screen-sub">베르시아 대륙의 여섯 나라. 지금 우리 루두스는 {getNation(state.nation).name}의 {getNation(state.nation).league}에서 싸운다.</div>
      <div className="seg" style={{ marginBottom: 12 }}>
        <button className={tab === "nations" ? "on" : ""} onClick={() => setTab("nations")}>나라</button>
        <button className={tab === "war" ? "on" : ""} onClick={() => setTab("war")}>전쟁·용병{activeWars(state).length ? ` (${activeWars(state).length})` : ""}</button>
      </div>
      {tab === "war" ? <WarPanel {...ctx} /> : <>
      <div className="nation-pick compact">
        {NATIONS.map((x) => (
          <button key={x.id} className={`nation-card ${x.id === sel ? "on" : ""}`} style={{ ["--nc" as string]: x.color }} onClick={() => setSel(x.id)}>
            <NationCrest nation={x.id} size={36} />
            <div>
              <div className="nc-name">{x.short}{x.id === state.nation && <span className="tag" style={{ marginLeft: 6 }}>우리 나라</span>}</div>
              <div className="nc-motif">{x.league}</div>
            </div>
          </button>
        ))}
      </div>
      <div className="grid2" style={{ gridTemplateColumns: "1.6fr 1fr" }}>
        <div className="panel">
          <h3>{n.name}</h3>
          <div className="pad">
            <div className="nd-meta">수도 {n.capital} · 대표 투기장 {n.arena} · 리그 「{n.league}」</div>
            <p className="nd-lore">{n.lore}</p>
          </div>
        </div>
        <div>
          <div className="panel">
            <h3>리그 특징</h3>
            <div className="pad">
              <ul className="nd-rules">{n.ruleText.map((t) => <li key={t}>{t}</li>)}</ul>
              <div className="sublabel" style={{ marginTop: 10 }}>즐겨 쓰는 스타일</div>
              <div>{n.styles.map((s) => <span key={s} className="tag">{getStyle(s).name}</span>)}</div>
            </div>
          </div>
          <div className="panel">
            <h3>다른 나라와의 관계</h3>
            <table className="t">
              <tbody>
                {NATIONS.filter((x) => x.id !== sel).map((x) => {
                  const v = relation(state, sel, x.id);
                  return (
                    <tr key={x.id}>
                      <td>{x.short}</td>
                      <td className="num" style={{ color: relColor(v) }}>{v > 0 ? `+${v}` : v}</td>
                      <td style={{ color: relColor(v) }}>{relLabel(v)}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      </div>
      </>}
    </>
  );
}
