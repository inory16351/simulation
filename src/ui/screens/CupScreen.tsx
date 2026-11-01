import { useState } from "react";
import { dateLabel } from "../../core/calendar";
import { availability } from "../../core/challenge";
import { CUP, GROUP_LETTERS, STAGE_NAMES, coefficientRanking, groupTable, playTie, todaysPlayerTie, type CupStage, type CupTie } from "../../core/cup";
import { getNation } from "../../core/data";
import { canFight, computeCA } from "../../core/gladiator";
import { Rng } from "../../core/rng";
import { autoLineup } from "../../core/series";
import { now } from "../../core/challenge";
import type { GameState, Id } from "../../core/types";
import type { Ctx } from "../App";
import { NationCrest } from "../components/common";
import { SPEEDS, SeriesRunner, seriesLives, type Live } from "./MatchScreen";

const POSITIONS = ["선봉", "중견", "대장"];

function TeamName({ state, id }: { state: GameState; id: Id }) {
  const l = state.ludi[id];
  if (!l) return <span>?</span>;
  return (
    <span className="name-cell" style={{ fontWeight: l.isPlayer ? 700 : undefined, color: l.isPlayer ? "var(--torch)" : undefined }}>
      <NationCrest nation={l.nation} size={18} /> {l.name}
    </span>
  );
}

export function CupScreen({ state, replace, nav }: Ctx) {
  const cup = state.cup;
  const player = state.ludi[state.playerLudusId];
  const tie = todaysPlayerTie(state);
  const [order, setOrder] = useState<Id[] | null>(null);
  const [speed, setSpeed] = useState<(typeof SPEEDS)[number]["id"]>("fast");
  const [run, setRun] = useState<{ lives: Live[]; commit: GameState } | null>(null);
  const ranking = coefficientRanking(state);

  if (run) {
    return <SeriesRunner lives={run.lives} commit={run.commit} speedMs={SPEEDS.find((s) => s.id === speed)!.ms} replace={replace} onExit={() => setRun(null)} nav={nav} />;
  }

  const pool = player.gladiators.map((id) => state.gladiators[id]).filter((g) => canFight(g) && !availability(state, g));
  const lineup = order ?? autoLineup(state, player, CUP.bouts, now(state)).map((g) => g.id);

  const play = (t: CupTie) => {
    const draft = structuredClone(state);
    const rng = new Rng(draft.rngState);
    const r = playTie(draft, t.id, rng, lineup);
    draft.rngState = rng.state;
    if (typeof r === "string") return;
    const homeIsMe = t.home === state.playerLudusId;
    if (!r.series.bouts.length) { replace(draft); return; }
    setRun({ lives: seriesLives(r.series, homeIsMe, "missio", `판테온 컵 ${STAGE_NAMES[t.stage]}`), commit: draft });
  };

  const mine = cup?.ties.filter((t) => t.home === player.id || t.away === player.id) ?? [];

  return (
    <>
      <h1 className="screen-title">판테온 컵</h1>
      <div className="screen-sub">
        여섯 나라 1부 상위 루두스 16곳의 대륙 대회. 나라 계수가 높을수록 출전권이 많다. 대결마다 세 명이 미시오로 한 판씩, 두 판을 먼저 따면 이긴다.
        리그 RP는 오가지 않지만 상금과 명성이 크다.
      </div>

      {tie && cup && (
        <div className="panel official">
          <h3>오늘의 컵 경기 — {STAGE_NAMES[tie.stage]}</h3>
          <div className="pad">
            <div style={{ marginBottom: 8 }}>
              <TeamName state={state} id={tie.home} /> <span className="hint">vs</span> <TeamName state={state} id={tie.away} />
            </div>
            <div className="lineup">
              {POSITIONS.map((p, i) => (
                <label key={p}>{p}
                  <select value={lineup[i] ?? ""} onChange={(e) => {
                    const next = [...lineup];
                    const j = next.indexOf(e.target.value);
                    if (j >= 0) next[j] = next[i];
                    next[i] = e.target.value;
                    setOrder(next.filter(Boolean));
                  }}>
                    <option value="">—</option>
                    {pool.map((g) => <option key={g.id} value={g.id}>{g.name} · CA {computeCA(g)}</option>)}
                  </select>
                </label>
              ))}
            </div>
            {lineup.length < CUP.bouts && <div className="tag neg" style={{ marginTop: 6 }}>세 명을 채우지 못하면 빈 자리는 기권패다.</div>}
            <div className="field" style={{ marginTop: 10 }}>
              <label>중계 속도</label>
              <div className="seg">
                {SPEEDS.map((s) => <button key={s.id} className={speed === s.id ? "on" : ""} onClick={() => setSpeed(s.id)}>{s.label}</button>)}
              </div>
            </div>
            <button className="btn primary" onClick={() => play(tie)}>경기장에 들어간다</button>
          </div>
        </div>
      )}

      <div className="grid2">
        <div className="panel">
          <h3>나라 계수</h3>
          <table className="t">
            <thead><tr><th>나라</th><th>최근 3시즌</th><th>합</th><th>출전권</th></tr></thead>
            <tbody>
              {ranking.map((r) => (
                <tr key={r.nation} className={r.nation === state.nation ? "me" : ""}>
                  <td><span className="name-cell"><NationCrest nation={r.nation} size={18} /> {getNation(r.nation).name}</span></td>
                  <td className="hint">{(state.coefficients?.[r.nation] ?? []).join(" · ")}</td>
                  <td>{r.coef}</td>
                  <td>{r.slots}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="pad hint">대결 승리 2점, 8강·4강·결승 진출 1점씩, 우승 2점. 나라마다 출전한 루두스의 평균 점수가 그 시즌 계수가 된다.</div>
        </div>
        <div className="panel">
          <h3>우리 루두스</h3>
          <div className="pad">
            {!cup || cup.season !== state.season ? (
              <div className="hint">이번 시즌 조 추첨은 {dateLabel(CUP.drawDay)}에 한다. 그날 자기 나라 1부 순위가 출전권 안에 들어야 나간다.</div>
            ) : !cup.teams.includes(player.id) ? (
              <div className="hint">이번 시즌에는 출전권이 없다. 1부에 올라 상위권에 들어야 한다.</div>
            ) : (
              <table className="t">
                <tbody>
                  {mine.map((t) => {
                    const opp = t.home === player.id ? t.away : t.home;
                    const myScore = t.score ? (t.home === player.id ? t.score[0] : t.score[1]) : null;
                    const oppScore = t.score ? (t.home === player.id ? t.score[1] : t.score[0]) : null;
                    return (
                      <tr key={t.id}>
                        <td>{dateLabel(t.day)}</td>
                        <td>{STAGE_NAMES[t.stage]}</td>
                        <td><TeamName state={state} id={opp} /> {t.home === player.id ? <span className="hint">(홈)</span> : null}</td>
                        <td>{t.winner ? <span className={`tag ${t.winner === player.id ? "pos" : "neg"}`}>{myScore}:{oppScore}</span> : <span className="hint">예정</span>}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </div>
        </div>
      </div>

      {cup && (
        <>
          <div className="cup-groups">
            {cup.groups.map((_, gi) => (
              <div className="panel" key={gi}>
                <h3>{GROUP_LETTERS[gi]}조</h3>
                <table className="t">
                  <thead><tr><th>루두스</th><th>경기</th><th>승</th><th>판 득실</th><th>승점</th></tr></thead>
                  <tbody>
                    {groupTable(state, cup, gi).map((r, i) => (
                      <tr key={r.id} className={state.ludi[r.id]?.isPlayer ? "me" : ""}>
                        <td>{i < 2 ? "▲ " : ""}<TeamName state={state} id={r.id} /></td>
                        <td>{r.played}</td>
                        <td>{r.won}</td>
                        <td>{r.boutsFor - r.boutsAgainst >= 0 ? "+" : ""}{r.boutsFor - r.boutsAgainst}</td>
                        <td><b>{r.pts}</b></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ))}
          </div>
          <div className="panel">
            <h3>토너먼트 {cup.champion && <span className="tag pos" style={{ marginLeft: 8 }}>우승: {state.ludi[cup.champion]?.name}</span>}</h3>
            <div className="pad bracket">
              {(["qf", "sf", "final"] as CupStage[]).map((stage) => {
                const ties = cup.ties.filter((t) => t.stage === stage);
                return (
                  <div key={stage} className="bracket-col">
                    <div className="hint">{STAGE_NAMES[stage]} · {dateLabel(stage === "qf" ? CUP.qfDay : stage === "sf" ? CUP.sfDay : CUP.finalDay)}</div>
                    {ties.length === 0 && <div className="hint">아직 정해지지 않았다</div>}
                    {ties.map((t) => (
                      <div key={t.id} className="bracket-tie">
                        {[t.home, t.away].map((id, k) => (
                          <div key={id} className={t.winner === id ? "won" : t.winner ? "lost" : ""}>
                            <TeamName state={state} id={id} /> <b>{t.score?.[k] ?? ""}</b>
                          </div>
                        ))}
                      </div>
                    ))}
                  </div>
                );
              })}
            </div>
          </div>
        </>
      )}
    </>
  );
}
