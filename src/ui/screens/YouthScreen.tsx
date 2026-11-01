import { getRace } from "../../core/data";
import { AGE_STAGE_NAMES, ageStage, computeCA } from "../../core/gladiator";
import { paSpread } from "../../core/staff";
import { FOCUS_NAMES, INTENSITY_NAMES } from "../../core/training";
import type { TrainingFocus, TrainingIntensity } from "../../core/types";
import { APPRENTICE_INTERVAL, promoteYouth, releaseYouth, youthCapacity } from "../../core/youth";
import { rosterCapacity } from "../../core/economy";
import type { Ctx } from "../App";
import { Portrait, RaceTag, Stars, StyleTag } from "../components/common";

const FOCI: TrainingFocus[] = ["physical", "weapon", "defense", "grapple", "mental", "show", "rest"];

export function YouthScreen({ state, update, act, nav }: Ctx) {
  const player = state.ludi[state.playerLudusId];
  const lvl = player.facilities.academy;
  const kids = (player.youth ?? []).map((id) => state.gladiators[id]).filter(Boolean);
  const full = player.gladiators.length >= rosterCapacity(player);

  if (!lvl) {
    return (
      <>
        <h1 className="screen-title">유소년 학당</h1>
        <div className="panel"><div className="pad">
          학당이 아직 없다. <b>증축</b>에서 유소년 학당을 지으면 매 시즌 서리달 1일에 아이들이 들어온다. 처음 지으면 바로 첫 입학생을 받는다.
          <div style={{ marginTop: 10 }}><button className="btn primary" onClick={() => nav.go("facilities")}>증축으로</button></div>
        </div></div>
      </>
    );
  }

  return (
    <>
      <h1 className="screen-title">유소년 학당</h1>
      <div className="screen-sub">
        학당 {lvl}단계 · 학생 {kids.length}/{youthCapacity(player)}명 · {APPRENTICE_INTERVAL}일마다 목검으로 견습 투기를 치른다 (죽지 않는다). 데뷔 나이가 되면 1군으로 올릴 수 있다.
      </div>
      <div className="panel">
        <table className="t">
          <thead><tr><th>이름</th><th>종족</th><th>나이</th><th>스타일</th><th>현재</th><th>잠재 (추정)</th><th>훈련</th><th>강도</th><th /></tr></thead>
          <tbody>
            {kids.length === 0 && <tr><td colSpan={9} className="hint">아직 학생이 없다. 다음 입학은 서리달 1일.</td></tr>}
            {kids.map((g) => {
              const ca = computeCA(g);
              const ready = ageStage(g) !== "youth";
              const spread = paSpread(state, g);
              return (
                <tr key={g.id}>
                  <td><div className="name-cell" style={{ cursor: "pointer" }} onClick={() => nav.openGladiator(g.id)}><Portrait g={g} size={30} /><span className="n">{g.name}</span></div></td>
                  <td><RaceTag race={g.race} /></td>
                  <td>{g.age} <span className="hint">{ready ? "데뷔 가능" : `데뷔까지 ${getRace(g.race).age.debut - g.age}년`}</span></td>
                  <td><StyleTag style={g.style} /></td>
                  <td><Stars value={ca} /></td>
                  <td><Stars value={Math.max(ca, g.hidden.pa - spread)} range={Math.min(200, g.hidden.pa + spread)} /></td>
                  <td>
                    <select value={g.training.focus} onChange={(e) => update((d) => { d.gladiators[g.id].training.focus = e.target.value as TrainingFocus; })}>
                      {FOCI.map((f) => <option key={f} value={f}>{FOCUS_NAMES[f]}</option>)}
                    </select>
                  </td>
                  <td>
                    <select value={g.training.intensity} onChange={(e) => update((d) => { d.gladiators[g.id].training.intensity = e.target.value as TrainingIntensity; })}>
                      {(["low", "normal", "high"] as TrainingIntensity[]).map((i) => <option key={i} value={i}>{INTENSITY_NAMES[i]}</option>)}
                    </select>
                  </td>
                  <td style={{ display: "flex", gap: 6 }}>
                    <button className="btn primary" disabled={!ready || full} title={!ready ? `${AGE_STAGE_NAMES.youth}: 아직 실전에 못 나간다` : full ? "막사가 꽉 찼다" : undefined}
                      onClick={() => act((d) => { promoteYouth(d, d.ludi[d.playerLudusId], g.id); })}>1군 승격</button>
                    <button className="btn danger" onClick={() => { if (confirm(`${g.name}을(를) 학당에서 내보낼까요?`)) act((d) => releaseYouth(d, d.ludi[d.playerLudusId], g.id)); }}>내보내기</button>
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
