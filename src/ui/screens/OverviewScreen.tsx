import { computeCA, canFight } from "../../core/gladiator";
import type { GameState } from "../../core/types";
import type { Nav } from "../App";
import { Bar, Portrait, Stars } from "../components/common";

export function OverviewScreen({ state, nav }: { state: GameState; nav: Nav }) {
  const player = state.ludi[state.playerLudusId];
  const roster = player.gladiators.map((id) => state.gladiators[id]);
  const wages = roster.reduce((s, g) => s + g.wage, 0);
  const avgMorale = roster.length ? Math.round(roster.reduce((s, g) => s + g.morale, 0) / roster.length) : 0;
  const best = [...roster].sort((a, b) => computeCA(b) - computeCA(a)).slice(0, 3);
  const tier = Object.values(state.ludi).filter((l) => l.tier === player.tier).sort((a, b) => b.rp - a.rp);
  const rank = tier.findIndex((l) => l.id === player.id);
  const weeksOfCash = wages > 0 ? Math.floor(player.treasury / wages) : 99;

  return (
    <>
      <h1 className="screen-title">{player.name}</h1>
      <div className="screen-sub">루두스 개요</div>
      <div className="grid3">
        <div className="panel">
          <h3>재정</h3>
          <div className="pad">
            <div style={{ fontSize: 24, color: "var(--parchment)", fontWeight: 700 }}>{player.treasury.toLocaleString()} D</div>
            <div style={{ color: "var(--text-dim)", marginTop: 4 }}>주급 합계 {wages} D/주</div>
            <div style={{ marginTop: 8, color: weeksOfCash < 8 ? "#e08b80" : "var(--text)" }}>
              현재 금고로 급료를 <b>{weeksOfCash}주</b> 버틸 수 있다
            </div>
          </div>
        </div>
        <div className="panel">
          <h3>리그</h3>
          <div className="pad">
            <div style={{ fontSize: 24, color: "var(--parchment)", fontWeight: 700 }}>{player.tier}부 {rank + 1}위</div>
            <div style={{ color: "var(--text-dim)", marginTop: 4 }}>RP {player.rp} · 1위와 {tier[0].rp - player.rp}점 차이</div>
            <button className="btn" style={{ marginTop: 10 }} onClick={() => nav.go("league")}>순위표 보기</button>
          </div>
        </div>
        <div className="panel">
          <h3>숙소</h3>
          <div className="pad">
            <div>평균 사기 <Bar value={avgMorale} /> {avgMorale}</div>
            <div style={{ marginTop: 6 }}>출전 가능 {roster.filter(canFight).length} / {roster.length}명</div>
            <div style={{ marginTop: 6 }}>부상자 {roster.filter((g) => g.injury).length}명</div>
          </div>
        </div>
      </div>
      <div className="panel">
        <h3>주력 검투사</h3>
        <div className="pad" style={{ display: "flex", gap: 18 }}>
          {best.map((g) => (
            <div key={g.id} style={{ cursor: "pointer", textAlign: "center" }} onClick={() => nav.openGladiator(g.id)}>
              <Portrait g={g} size={110} />
              <div style={{ color: "var(--parchment)", marginTop: 6, fontWeight: 700 }}>{g.name}</div>
              <Stars value={computeCA(g)} />
            </div>
          ))}
        </div>
      </div>
    </>
  );
}
