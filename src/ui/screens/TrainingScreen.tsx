import { performance } from "../../core/condition";
import { getStyle } from "../../core/data";
import { FOCUS_DESC, FOCUS_NAMES, INTENSITY_NAMES } from "../../core/training";
import type { TrainingFocus, TrainingIntensity } from "../../core/types";
import type { Ctx } from "../App";
import { Bar, InjuryTag, Portrait } from "../components/common";

const FOCI = Object.keys(FOCUS_NAMES) as TrainingFocus[];
const INTENSITIES = Object.keys(INTENSITY_NAMES) as TrainingIntensity[];

export function TrainingScreen({ state, update, nav }: Ctx) {
  const player = state.ludi[state.playerLudusId];
  const roster = player.gladiators.map((id) => state.gladiators[id]).filter((g) => g.alive);

  const setPlan = (id: string, patch: Partial<{ focus: TrainingFocus; intensity: TrainingIntensity }>) =>
    update((d) => { Object.assign(d.gladiators[id].training, patch); });
  const setAll = (patch: Partial<{ focus: TrainingFocus; intensity: TrainingIntensity }>) =>
    update((d) => { for (const id of d.ludi[d.playerLudusId].gladiators) Object.assign(d.gladiators[id].training, patch); });

  return (
    <>
      <h1 className="screen-title">연무장</h1>
      <div className="screen-sub">
        훈련은 매일 조금씩 쌓인다. 강하게 굴릴수록 빨리 크지만 지치고 다친다. 실전(훈련전·미시오·사투)도 성장시킨다.
      </div>
      <div className="panel">
        <h3>일괄 지시</h3>
        <div className="pad" style={{ display: "flex", gap: 10, flexWrap: "wrap", alignItems: "center" }}>
          <span className="hint">모두에게</span>
          <select onChange={(e) => e.target.value && setAll({ focus: e.target.value as TrainingFocus })} value="">
            <option value="">훈련 내용…</option>
            {FOCI.map((f) => <option key={f} value={f}>{FOCUS_NAMES[f]}</option>)}
          </select>
          <select onChange={(e) => e.target.value && setAll({ intensity: e.target.value as TrainingIntensity })} value="">
            <option value="">강도…</option>
            {INTENSITIES.map((i) => <option key={i} value={i}>{INTENSITY_NAMES[i]}</option>)}
          </select>
        </div>
      </div>
      <div className="panel">
        <table className="t">
          <thead>
            <tr>
              <th>검투사</th>
              <th>스타일</th>
              <th>훈련 내용</th>
              <th>강도</th>
              <th>체력</th>
              <th title="경기를 뛰면 오르고 쉬면 떨어진다">경기감각</th>
              <th title="짧은 간격으로 계속 뛰면 쌓인다. 85 이상이면 탈진">혹사도</th>
              <th>교범 숙련</th>
              <th className="num">경기력</th>
              <th>상태</th>
            </tr>
          </thead>
          <tbody>
            {roster.map((g) => {
              const perf = performance(g);
              return (
                <tr key={g.id}>
                  <td>
                    <div className="name-cell" style={{ cursor: "pointer" }} onClick={() => nav.openGladiator(g.id)}>
                      <Portrait g={g} size={28} />
                      <span className="n">{g.name}</span>
                    </div>
                  </td>
                  <td>{getStyle(g.style).name}</td>
                  <td>
                    <select value={g.training.focus} title={FOCUS_DESC[g.training.focus]} onChange={(e) => setPlan(g.id, { focus: e.target.value as TrainingFocus })}>
                      {FOCI.map((f) => <option key={f} value={f}>{FOCUS_NAMES[f]}</option>)}
                    </select>
                  </td>
                  <td>
                    <select value={g.training.intensity} disabled={g.training.focus === "rest"} onChange={(e) => setPlan(g.id, { intensity: e.target.value as TrainingIntensity })}>
                      {INTENSITIES.map((i) => <option key={i} value={i}>{INTENSITY_NAMES[i]}</option>)}
                    </select>
                  </td>
                  <td><Bar value={100 - g.fatigue} /></td>
                  <td><Bar value={g.sharpness} color="var(--bronze-hi)" /> {Math.round(g.sharpness)}</td>
                  <td><Bar value={g.wear} color={g.wear >= 85 ? "var(--blood)" : g.wear >= 50 ? "var(--torch)" : "var(--good)"} /> {Math.round(g.wear)}</td>
                  <td>{Math.round(g.doctrine.familiarity)}</td>
                  <td className="num" title={perf.parts.map((p) => `${p.label} ${p.value > 0 ? "+" : ""}${Math.round(p.value * 100)}%`).join("\n")}
                    style={{ color: perf.total >= 1 ? "var(--good)" : perf.total >= 0.85 ? "var(--parchment)" : "#e08b80" }}>
                    {Math.round(perf.total * 100)}%
                  </td>
                  <td>
                    {g.injury ? <InjuryTag g={g} />
                      : g.wear >= 85 ? <span className="tag neg">탈진 — 쉬게 하라</span>
                      : g.unpaidWeeks >= 3 ? <span className="tag neg">태업</span>
                      : <span style={{ color: "var(--good)" }}>정상</span>}
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
