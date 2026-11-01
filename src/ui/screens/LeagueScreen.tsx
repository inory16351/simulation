import { useState } from "react";
import { computeCA } from "../../core/gladiator";
import type { GameState } from "../../core/types";
import { Stars } from "../components/common";

const PERSONALITY_NAMES = { aggressive: "공격형", stable: "안정형", grudge: "원한형", merchant: "상인형", developer: "육성형" };

export function LeagueScreen({ state }: { state: GameState }) {
  const player = state.ludi[state.playerLudusId];
  const [tier, setTier] = useState<1 | 2>(player.tier);
  const ludi = Object.values(state.ludi).filter((l) => l.tier === tier).sort((a, b) => b.rp - a.rp);

  const zone = (i: number) => {
    if (tier === 2) return i < 2 ? "zone-up" : i === 2 ? "zone-po" : "";
    return i >= 8 ? "zone-down" : i === 7 ? "zone-po" : "";
  };

  return (
    <>
      <h1 className="screen-title">일정·리그</h1>
      <div className="screen-sub">
        RP 순위로 승강이 결정된다. 2부 1·2위 자동 승격, 3위 승강전 / 1부 8위 승강전, 9·10위 강등.
      </div>
      <div style={{ marginBottom: 12 }} className="seg">
        <button className={tier === 1 ? "on" : ""} onClick={() => setTier(1)}>1부 리그</button>
        <button className={tier === 2 ? "on" : ""} onClick={() => setTier(2)}>2부 리그</button>
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
