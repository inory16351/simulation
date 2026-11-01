import { useState } from "react";
import { AGE_STAGE_NAMES, ageStage, computeCA } from "../../core/gladiator";
import type { GameState, Gladiator } from "../../core/types";
import type { Nav } from "../App";
import { Bar, Portrait, RaceTag, Stars, StyleTag } from "../components/common";
import { paSpread } from "../../core/staff";

type SortKey = "ca" | "age" | "wage" | "morale" | "name";

export function GladiatorTable({ list, nav, state, showPa = true }: { list: Gladiator[]; nav: Nav; state: GameState; showPa?: boolean }) {
  const [sort, setSort] = useState<SortKey>("ca");
  const sorted = [...list].sort((a, b) => {
    switch (sort) {
      case "ca": return computeCA(b) - computeCA(a);
      case "age": return a.age - b.age;
      case "wage": return b.wage - a.wage;
      case "morale": return b.morale - a.morale;
      case "name": return a.name.localeCompare(b.name, "ko");
    }
  });
  const th = (k: SortKey, label: string, cls = "") => (
    <th className={cls} style={{ cursor: "pointer", color: sort === k ? "var(--torch)" : undefined }} onClick={() => setSort(k)}>
      {label}
    </th>
  );

  return (
    <table className="t">
      <thead>
        <tr>
          {th("name", "이름")}
          <th>종족</th>
          {th("age", "나이")}
          <th>스타일</th>
          {th("ca", "현재")}
          {showPa && <th>잠재</th>}
          {th("morale", "사기")}
          <th>체력</th>
          <th title="경기감각">감각</th>
          <th title="혹사도. 85 이상이면 탈진">혹사</th>
          <th>상태</th>
          <th className="num">전적</th>
          {th("wage", "주급", "num")}
        </tr>
      </thead>
      <tbody>
        {sorted.map((g) => {
          const ca = computeCA(g);
          const stage = ageStage(g);
          return (
            <tr key={g.id} className={`click ${g.alive ? "" : "dead"}`} onClick={() => nav.openGladiator(g.id)}>
              <td>
                <div className="name-cell">
                  <Portrait g={g} size={30} />
                  <div>
                    <div className="n">{g.name}</div>
                    {g.nickname && <div className="nick">"{g.nickname}"</div>}
                  </div>
                </div>
              </td>
              <td><RaceTag race={g.race} /></td>
              <td>
                {g.age} <span style={{ color: "var(--text-faint)", fontSize: 11 }}>{AGE_STAGE_NAMES[stage]}</span>
              </td>
              <td><StyleTag style={g.style} /></td>
              <td><Stars value={ca} /></td>
              {showPa && <td><Stars value={Math.max(ca, g.hidden.pa - paSpread(state, g))} range={Math.min(200, g.hidden.pa + paSpread(state, g))} /></td>}
              <td><Bar value={g.morale} /></td>
              <td><Bar value={100 - g.fatigue} /></td>
              <td><Bar value={g.sharpness} color="var(--bronze-hi)" /></td>
              <td><Bar value={g.wear} color={g.wear >= 85 ? "var(--blood)" : g.wear >= 50 ? "var(--torch)" : "var(--good)"} /></td>
              <td>
                {!g.alive ? <span className="tag neg">사망</span>
                  : g.injury ? <span className="tag inj">{g.injury.desc} {g.injury.daysLeft}일</span>
                  : <span style={{ color: "var(--good)" }}>출전 가능</span>}
              </td>
              <td className="num">{g.record.wins}-{g.record.losses}</td>
              <td className="num">{g.wage} D</td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}

export function RosterScreen({ state, nav }: { state: GameState; nav: Nav }) {
  const player = state.ludi[state.playerLudusId];
  const list = player.gladiators.map((id) => state.gladiators[id]);
  const fallen = state.fallen.map((id) => state.gladiators[id]);
  return (
    <>
      <h1 className="screen-title">막사</h1>
      <div className="screen-sub">
        검투사 {list.length}명 · 주급 합계 {list.reduce((s, g) => s + g.wage, 0).toLocaleString()} D
      </div>
      <div className="panel">
        <GladiatorTable list={list} nav={nav} state={state} />
      </div>
      {fallen.length > 0 && (
        <div className="panel">
          <h3>모래에 묻힌 자들</h3>
          <GladiatorTable list={fallen} nav={nav} state={state} showPa={false} />
        </div>
      )}
    </>
  );
}
