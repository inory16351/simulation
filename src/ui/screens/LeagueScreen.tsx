import { useState } from "react";
import { computeCA } from "../../core/gladiator";
import { NATIONS } from "../../core/data";
import type { GameState, NationId } from "../../core/types";
import { Stars } from "../components/common";

const PERSONALITY_NAMES = { aggressive: "공격형", stable: "안정형", grudge: "원한형", merchant: "상인형", developer: "육성형" };

export function LeagueScreen({ state }: { state: GameState }) {
  const player = state.ludi[state.playerLudusId];
  const [tier, setTier] = useState<1 | 2>(player.tier);
  const [nation, setNation] = useState<NationId>(state.nation);
  const foreign = nation !== state.nation;
  const ludi = Object.values(state.ludi).filter((l) => l.tier === (foreign ? 1 : tier) && l.nation === nation).sort((a, b) => b.rp - a.rp);

  const zone = (i: number) => {
    if (foreign) return "";
    if (tier === 2) return i < 2 ? "zone-up" : "";
    return i >= 8 ? "zone-down" : "";
  };

  return (
    <>
      <h1 className="screen-title">서열표</h1>
      <div className="screen-sub">
        시즌이 끝날 때 RP 순위로 오르내린다. 2부 1·2위는 1부로 올라가고, 1부 9·10위는 2부로 떨어진다. 다른 나라는 1부만 볼 수 있다.
      </div>
      <div style={{ marginBottom: 12 }} className="seg">
        <button className={tier === 1 || foreign ? "on" : ""} onClick={() => setTier(1)}>1부 리그</button>
        <button className={tier === 2 && !foreign ? "on" : ""} disabled={foreign} onClick={() => setTier(2)}>2부 리그</button>
        <select value={nation} onChange={(e) => setNation(e.target.value as NationId)} style={{ marginLeft: 10 }}>
          {NATIONS.map((n) => <option key={n.id} value={n.id}>{n.name}{n.id === state.nation ? " (우리 나라)" : ""}</option>)}
        </select>
      </div>
      <div className="panel">
        <table className="t">
          <thead>
            <tr>
              <th>순위</th>
              <th>루두스</th>
              <th>성향</th>
              <th className="num">검투사</th>
              <th>평균 전력</th>
              <th className="num">승-패</th>
              <th className="num">RP</th>
            </tr>
          </thead>
          <tbody>
            {ludi.map((l, i) => {
              const alive = l.gladiators.map((id) => state.gladiators[id]).filter((g) => g.alive);
              const avg = alive.length ? alive.reduce((s, g) => s + computeCA(g), 0) / alive.length : 0;
              return (
                <tr key={l.id} className={`${l.isPlayer ? "me" : ""} ${zone(i)}`}>
                  <td className="num" style={{ width: 40 }}>{i + 1}</td>
                  <td style={{ color: "var(--parchment)", fontWeight: l.isPlayer ? 700 : 400 }}>{l.name}</td>
                  <td style={{ color: "var(--text-dim)" }}>{l.isPlayer ? "—" : PERSONALITY_NAMES[l.aiPersonality!]}</td>
                  <td className="num">{alive.length}</td>
                  <td><Stars value={avg} /></td>
                  <td className="num">{l.record.wins}-{l.record.losses}</td>
                  <td className="num" style={{ color: "var(--parchment)", fontWeight: 700 }}>{l.rp}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </>
  );
}
