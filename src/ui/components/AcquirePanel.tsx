import { useState } from "react";
import { ludusOf } from "../../core/career";
import { freeAgentDemand, marketValue, signFreeAgent, signingFee } from "../../core/economy";
import { Rng } from "../../core/rng";
import { assignScout, knowledgeOf, scoutingOf } from "../../core/scouting";
import { bid, inWindow, transferOf, windowLabel } from "../../core/transfers";
import type { Gladiator } from "../../core/types";
import type { Ctx } from "../App";
import { Bar } from "./common";
import { Negotiate } from "./Negotiate";

/** 남의 검투사 상세 화면: 정찰, 관심 명단, 무소속 영입 협상, 이적료 제안 */
export function AcquirePanel({ g, ctx }: { g: Gladiator; ctx: Ctx }) {
  const { state, act, update, nav } = ctx;
  const owner = ludusOf(state, g.id);
  const free = state.freeAgents.includes(g.id);
  const [msg, setMsg] = useState<string | null>(null);
  const [fee, setFee] = useState(Math.round(marketValue(g) / 50) * 50);
  const k = knowledgeOf(state, g);
  const scouting = scoutingOf(state).assignments.includes(g.id);
  const short = state.shortlist?.includes(g.id);
  const deal = transferOf(state).deals.find((d) => d.gladiatorId === g.id);
  const roll = () => new Rng((state.rngState ^ state.nextId) >>> 0).next();

  return (
    <div className="panel">
      <h3>{free ? "무소속 — 영입 협상" : owner ? `${owner.name} 소속 — 이적` : "영입할 수 없다"}</h3>
      <div className="pad">
        <div style={{ display: "flex", gap: 14, alignItems: "center", marginBottom: 10, flexWrap: "wrap" }}>
          <span>정보량 <Bar value={k} color="var(--bronze-hi)" /> {Math.round(k)}</span>
          <button className="btn" disabled={scouting || k >= 100} onClick={() => {
            const err = assignScout(structuredClone(state), g.id);
            setMsg(err);
            if (!err) act((d) => { assignScout(d, g.id); });
          }}>{scouting ? "정찰 중" : k >= 100 ? "정찰 끝" : "정찰을 맡긴다"}</button>
          <button className="btn" onClick={() => update((d) => {
            d.shortlist ??= [];
            d.shortlist = short ? d.shortlist.filter((x) => x !== g.id) : [...d.shortlist, g.id];
          })}>{short ? "★ 관심 명단에서 빼기" : "☆ 관심 명단"}</button>
        </div>
        {msg && <div className="tag neg" style={{ marginBottom: 8 }}>{msg}</div>}

        {free && (
          <>
            <div className="hint" style={{ marginBottom: 6 }}>계약금은 제안 주급의 4주치({signingFee(freeAgentDemand(state, g).wage).toLocaleString()} D 안팎)다.</div>
            <Negotiate demand={freeAgentDemand(state, g)} seasons label="계약 제안" onOffer={(wage, seasons) => {
              const r = roll();
              setMsg(signFreeAgent(structuredClone(state), g.id, wage, seasons, r) ?? "계약했다!");
              act((d) => { signFreeAgent(d, g.id, wage, seasons, r); });
            }} />
          </>
        )}

        {owner && !owner.isPlayer && (
          deal ? (
            <div>이적료 {deal.fee.toLocaleString()} D에 합의했다. <button className="btn primary" onClick={() => nav.go("market")}>개인 계약 협상으로</button></div>
          ) : (
            <div className="negotiate">
              <span className="hint">추정 가치 {Math.round(marketValue(g) / 50) * 50} D</span>
              <input type="number" value={fee} min={0} step={50} onChange={(e) => setFee(Number(e.target.value))} />
              <button className="btn primary" disabled={!inWindow(state)} title={!inWindow(state) ? `이적 시장 기간: ${windowLabel}` : undefined} onClick={() => {
                const r = bid(structuredClone(state), g.id, fee);
                if (r.status === "accepted") {
                  setMsg(null);
                  act((d) => { bid(d, g.id, fee); });
                } else if (r.status === "countered") {
                  setMsg(`${owner.name}: "${r.asking.toLocaleString()} D면 생각해 보지."`);
                  setFee(r.asking);
                } else setMsg(r.reason);
              }}>이적료 제안</button>
            </div>
          )
        )}
      </div>
    </div>
  );
}
