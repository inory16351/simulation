import { useState } from "react";
import { absDay } from "../../core/calendar";
import { getNation, QUALITY_NAMES } from "../../core/data";
import {
  DUNGEONS, FORMATIONS, MAX_PARTY, cannotSend, expeditionsOf, getDungeon, questsOf, startExpedition,
  type DungeonId, type Formation,
} from "../../core/dungeon";
import { computeCA } from "../../core/gladiator";
import type { GameState, Id } from "../../core/types";
import type { Ctx } from "../App";
import { Portrait, Stars } from "../components/common";
import { ItemName } from "../components/GearPanels";

export function DungeonScreen(ctx: Ctx) {
  const { state } = ctx;
  const t = absDay(state.season, state.day);
  const [dungeon, setDungeon] = useState<DungeonId>("sewer");
  const [questId, setQuestId] = useState<Id | undefined>();
  const [party, setParty] = useState<Id[]>([]);
  const [formation, setFormation] = useState<Formation>("shieldwall");
  const [msg, setMsg] = useState<string | null>(null);
  const me = state.ludi[state.playerLudusId];
  const quest = questId ? questsOf(state).find((q) => q.id === questId && q.status === "open") : undefined;
  const d = getDungeon(quest?.dungeon ?? dungeon);
  const roster = me.gladiators.map((id) => state.gladiators[id]).filter((g) => g?.alive).sort((a, b) => computeCA(b) - computeCA(a));
  const wages = party.reduce((sum, id) => sum + (state.gladiators[id]?.wage ?? 0), 0) * Math.ceil(d.days / 7);
  const active = expeditionsOf(state).filter((x) => x.status === "active");
  const done = expeditionsOf(state).filter((x) => x.status !== "active").slice(-4).reverse();
  const open = questsOf(state).filter((q) => q.status === "open");

  const toggle = (id: Id) => setParty((p) => (p.includes(id) ? p.filter((x) => x !== id) : p.length >= MAX_PARTY ? p : [...p, id]));
  const go = () => {
    const run = (s: GameState) => startExpedition(s, d.id, party, formation, quest?.id);
    const err = run(structuredClone(state));
    setMsg(err);
    if (!err) {
      ctx.update((s) => { run(s); });
      setParty([]);
      setQuestId(undefined);
    }
  };

  return (
    <>
      <h1 className="screen-title">던전</h1>
      <div className="screen-sub">검투사를 최대 {MAX_PARTY}명 보내 장비를 캐 온다. 돈과 명성은 거의 없고, 쓰러지면 죽을 수도 있다. 싸움은 층마다 5:5 진형 전투.</div>

      {open.length > 0 && (
        <div className="panel quest-panel">
          <h3>나라의 토벌 의뢰 — 아주 어렵다</h3>
          <div className="pad">
            {open.map((q) => (
              <div key={q.id} className={`quest ${questId === q.id ? "on" : ""}`}>
                <div>
                  <b>{getNation(q.nation).short}: {getDungeon(q.dungeon).name} 토벌</b>
                  <div className="hint">몬스터 실력 ×{q.hard.toFixed(2)} · 현상금 {q.bounty.toLocaleString()} D · RP +{q.rp} · {q.expiresAt - t}일 안에 출발</div>
                </div>
                <button className="btn primary" onClick={() => setQuestId(questId === q.id ? undefined : q.id)}>{questId === q.id ? "의뢰 취소" : "이 의뢰로 꾸리기"}</button>
              </div>
            ))}
          </div>
        </div>
      )}

      <div className="dungeon-grid">
        {DUNGEONS.map((x) => (
          <button key={x.id} className={`dungeon-card tier${x.tier} ${d.id === x.id ? "on" : ""}`} disabled={!!quest && quest.dungeon !== x.id} onClick={() => setDungeon(x.id)}>
            <div className="dname">{x.name}</div>
            <div className="hint">{x.desc}</div>
            <div className="dmeta">
              <span>난이도 <Stars value={x.level * 10} /></span>
              <span>{x.floors}층 · {x.days}일</span>
              <span>죽음 {Math.round(x.death * 100)}%</span>
            </div>
            <div className="dloot">
              {x.loot.map((w, i) => w > 0 && <span key={i} className={`item-name q${i}`}>{QUALITY_NAMES[i] || "보통"} {Math.round((w / x.loot.reduce((a, b) => a + b, 0)) * 100)}%</span>)}
            </div>
          </button>
        ))}
      </div>

      <div className="grid2">
        <div className="panel">
          <h3>원정대 · {party.length}/{MAX_PARTY}명 → {d.name}{quest ? " (토벌 의뢰)" : ""}</h3>
          <table className="t">
            <tbody>
              {roster.map((g) => {
                const why = cannotSend(state, g);
                const on = party.includes(g.id);
                return (
                  <tr key={g.id} className={on ? "picked" : ""} style={{ opacity: why ? 0.5 : 1, cursor: why ? "default" : "pointer" }} onClick={() => !why && toggle(g.id)}>
                    <td style={{ width: 24 }}><input type="checkbox" checked={on} disabled={!!why} readOnly /></td>
                    <td><div className="name-cell"><Portrait g={g} size={28} /><span className="n">{g.name}</span></div></td>
                    <td><Stars value={computeCA(g)} /></td>
                    <td className="hint">{why ?? (g.injury ? g.injury.desc : "")}</td>
                    <td className="num">{g.wage} D</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        <div className="panel">
          <h3>진형과 출발</h3>
          <div className="pad">
            <div className="formation-pick">
              {(Object.keys(FORMATIONS) as Formation[]).map((f) => (
                <button key={f} className={`formation ${formation === f ? "on" : ""}`} onClick={() => setFormation(f)}>
                  <b>{FORMATIONS[f].name}</b>
                  <span className="hint">{FORMATIONS[f].desc}</span>
                </button>
              ))}
            </div>
            <p className="hint">원정 동안에도 급료는 루두스가 낸다: 약 {wages.toLocaleString()} D. 층마다 동전 몇십 D가 전부다.</p>
            {msg && <p><span className="tag neg">{msg}</span></p>}
            <button className="continue" disabled={!party.length} onClick={go}>{quest ? "토벌대 출발" : "원정 출발"}</button>
          </div>
        </div>
      </div>

      {active.map((ex) => {
        const dd = getDungeon(ex.dungeon);
        return (
          <div key={ex.id} className="panel">
            <h3>진행 중: {dd.name} · {ex.floor}/{dd.floors}층 · {Math.max(0, ex.endsAt - t)}일 뒤 귀환 · {FORMATIONS[ex.formation].name}</h3>
            <div className="pad">
              <div className="expedition-party">{ex.party.map((id) => state.gladiators[id] && <Portrait key={id} g={state.gladiators[id]} size={44} />)}</div>
              {ex.loot.length > 0 && <div style={{ margin: "6px 0" }}>전리품: {ex.loot.map((it, i) => <span key={i} className="tag"><ItemName id={it} /></span>)}</div>}
              <div className="expedition-log">{ex.log.length ? ex.log.map((l, i) => <div key={i}>{l}</div>) : <span className="hint">아직 첫 층에 닿지 않았다.</span>}</div>
            </div>
          </div>
        );
      })}

      {done.length > 0 && (
        <div className="panel">
          <h3>지난 원정</h3>
          <table className="t">
            <tbody>
              {done.map((ex) => (
                <tr key={ex.id}>
                  <td>{getDungeon(ex.dungeon).name}{ex.questId ? " (토벌)" : ""}</td>
                  <td>{ex.status === "done" ? <span style={{ color: "var(--good)" }}>완주</span> : <span className="neg">퇴각</span>} · {ex.floor}/{getDungeon(ex.dungeon).floors}층</td>
                  <td>{ex.loot.map((it, i) => <span key={i} className="tag"><ItemName id={it} /></span>)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
