import { dateLabel } from "../../core/calendar";
import { computeCA } from "../../core/gladiator";
import type { Gladiator } from "../../core/types";

const KIND_ICON = { join: "◇", match: "⚔", milestone: "★", injury: "✚", death: "✝", other: "·" } as const;

/** 시즌별 기록 표와 일대기 (FM의 경력 화면에 해당) */
export function CareerPanel({ g }: { g: Gladiator }) {
  const history = [...g.history].sort((a, b) => a.season - b.season);
  const total = history.reduce(
    (t, h) => ({ apps: t.apps + h.apps, wins: t.wins + h.wins, losses: t.losses + h.losses, kills: t.kills + h.kills, earnings: t.earnings + h.earnings }),
    { apps: 0, wins: 0, losses: 0, kills: 0, earnings: 0 },
  );
  return (
    <div className="grid2" style={{ gridTemplateColumns: "1.3fr 1fr" }}>
      <div className="panel">
        <h3>시즌별 기록</h3>
        <table className="t">
          <thead>
            <tr><th>시즌</th><th>루두스</th><th className="num">출전</th><th className="num">승</th><th className="num">패</th><th className="num">살해</th><th className="num">부상</th><th className="num">능력</th><th className="num">인기</th><th className="num">벌어들인 돈</th></tr>
          </thead>
          <tbody>
            {history.map((h) => {
              const caEnd = h.season === history[history.length - 1].season && g.alive ? computeCA(g) : h.caEnd;
              const delta = caEnd - h.caStart;
              return (
                <tr key={h.season}>
                  <td>{h.season}</td>
                  <td>{h.ludus} <span className="hint">{h.tier ? `${h.tier}부` : ""}</span></td>
                  <td className="num">{h.apps}</td>
                  <td className="num">{h.wins}</td>
                  <td className="num">{h.losses}</td>
                  <td className="num">{h.kills}</td>
                  <td className="num">{h.injuries}</td>
                  <td className="num">{caEnd} <span style={{ color: delta > 0 ? "var(--good)" : delta < 0 ? "#e08b80" : "var(--text-faint)", fontSize: 11 }}>{delta > 0 ? `+${delta}` : delta < 0 ? delta : "±0"}</span></td>
                  <td className="num">{h.popularity}</td>
                  <td className="num">{h.earnings.toLocaleString()}</td>
                </tr>
              );
            })}
            <tr className="total">
              <td colSpan={2}>통산</td>
              <td className="num">{total.apps}</td>
              <td className="num">{total.wins}</td>
              <td className="num">{total.losses}</td>
              <td className="num">{total.kills}</td>
              <td colSpan={3} />
              <td className="num">{total.earnings.toLocaleString()}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <div className="panel">
        <h3>일대기</h3>
        <div className="career-log">
          {[...g.career].reverse().map((e, i) => (
            <div key={i} className={`career-item ${e.kind}`}>
              <span className="ci-icon">{KIND_ICON[e.kind]}</span>
              <span className="ci-date">시즌 {e.season} · {dateLabel(e.day)}</span>
              <span className="ci-text">{e.text}</span>
            </div>
          ))}
          {g.career.length === 0 && <div className="hint" style={{ padding: 12 }}>아직 남긴 기록이 없다.</div>}
        </div>
      </div>
    </div>
  );
}
