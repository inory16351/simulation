import { dateLabel, daysToPayday, fromAbs } from "../../core/calendar";
import { weeklyFood, weeklyWages } from "../../core/economy";
import type { Ctx } from "../App";

export function FinanceScreen({ state }: Ctx) {
  const player = state.ludi[state.playerLudusId];
  const wages = weeklyWages(state, player);
  const food = Math.round(weeklyFood(state, player));
  const weekly = wages + food;
  const runway = weekly > 0 ? Math.floor(player.treasury / weekly) : 99;
  const unpaid = player.gladiators.map((id) => state.gladiators[id]).filter((g) => g.unpaidWeeks > 0);

  return (
    <>
      <h1 className="screen-title">금고</h1>
      <div className="screen-sub">급료는 7일마다 나간다. 수입에는 세금 10%가 붙는다.</div>
      <div className="grid3">
        <div className="panel">
          <h3>금고</h3>
          <div className="pad">
            <div className={`big-num ${player.treasury < 0 ? "neg" : ""}`}>{player.treasury.toLocaleString()} D</div>
            <div className="hint">다음 급료일까지 {daysToPayday(state.day)}일</div>
          </div>
        </div>
        <div className="panel">
          <h3>매주 나가는 돈</h3>
          <div className="pad">
            <div className="big-num">{weekly.toLocaleString()} D</div>
            <div className="hint">주급 {wages} D · 식비 {food} D</div>
          </div>
        </div>
        <div className="panel">
          <h3>버틸 수 있는 기간</h3>
          <div className="pad">
            <div className={`big-num ${runway < 4 ? "neg" : ""}`}>{runway}주</div>
            <div className="hint">수입 없이 급료만 낼 때</div>
          </div>
        </div>
      </div>
      {unpaid.length > 0 && (
        <div className="panel">
          <h3>밀린 급료</h3>
          <div className="pad">
            {unpaid.map((g) => <span key={g.id} className="tag neg">{g.name} {g.unpaidWeeks}회 · {g.wage * g.unpaidWeeks} D</span>)}
            <div className="hint" style={{ marginTop: 6 }}>다음 급료일에 금고가 넉넉하면 밀린 몫까지 한꺼번에 나간다.</div>
          </div>
        </div>
      )}
      <div className="panel">
        <h3>장부</h3>
        <table className="t">
          <thead><tr><th>날짜</th><th>내용</th><th className="num">금액</th></tr></thead>
          <tbody>
            {state.ledger.slice(0, 80).map((e, i) => {
              const d = fromAbs(e.at);
              return (
                <tr key={i}>
                  <td className="hint">시즌 {d.season} · {dateLabel(d.day)}</td>
                  <td>{e.desc}</td>
                  <td className="num" style={{ color: e.amount >= 0 ? "var(--good)" : "#e08b80" }}>
                    {e.amount >= 0 ? "+" : ""}{e.amount.toLocaleString()}
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
