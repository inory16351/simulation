import { buyFromMarket, buyPrice, marketValue, rosterCapacity, sellGladiator } from "../../core/economy";
import { paSpread, ratingOf, sellMult } from "../../core/staff";
import { BALANCE, getStyle } from "../../core/data";
import { AGE_STAGE_NAMES, ageStage, computeCA } from "../../core/gladiator";
import type { Ctx } from "../App";
import { Portrait, RaceTag, Stars, StyleTag } from "../components/common";

export function MarketScreen({ state, act, nav }: Ctx) {
  const player = state.ludi[state.playerLudusId];
  const cap = rosterCapacity(player);
  const full = player.gladiators.length >= cap;
  const roster = player.gladiators.map((id) => state.gladiators[id]).filter((g) => g.alive);

  return (
    <>
      <h1 className="screen-title">노예 경매</h1>
      <div className="screen-sub">
        서리달 1일과 꽃달 22일에 새 노예가 들어온다. 막사 {player.gladiators.length}/{cap}명 · 금고 {player.treasury.toLocaleString()} D
      </div>
      <div className="panel">
        <h3>오늘의 매물</h3>
        <table className="t">
          <thead>
            <tr><th>이름</th><th>종족</th><th>나이</th><th>스타일</th><th>현재</th><th>잠재 (추정)</th><th className="num">예상 주급</th><th className="num">값</th><th /></tr>
          </thead>
          <tbody>
            {state.market.length === 0 && <tr><td colSpan={9} className="hint">매물이 없다.</td></tr>}
            {state.market.map(({ gladiatorId, price: listed }) => {
              const price = buyPrice(state, listed);
              const g = state.gladiators[gladiatorId];
              const ca = computeCA(g);
              // 잠재력은 정찰꾼 실력만큼 좁혀서 보인다
              return (
                <tr key={g.id}>
                  <td>
                    <div className="name-cell" style={{ cursor: "pointer" }} onClick={() => nav.openGladiator(g.id)}>
                      <Portrait g={g} size={30} />
                      <span className="n">{g.name}</span>
                    </div>
                  </td>
                  <td><RaceTag race={g.race} /></td>
                  <td>{g.age} <span className="hint">{AGE_STAGE_NAMES[ageStage(g)]}</span></td>
                  <td><StyleTag style={g.style} /></td>
                  <td><Stars value={ca} /></td>
                  <td><Stars value={Math.max(ca, g.hidden.pa - paSpread(state, g))} range={Math.min(200, g.hidden.pa + paSpread(state, g))} /></td>
                  <td className="num">{g.wage} D</td>
                  <td className="num" style={{ color: "var(--parchment)", fontWeight: 700 }}>{price.toLocaleString()} D</td>
                  <td>
                    <button className="btn primary" disabled={full || player.treasury < price}
                      onClick={() => act((d) => { buyFromMarket(d, g.id); })}>
                      산다
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
      <div className="panel">
        <h3>노예 상인에게 넘기기 <span className="hint">(시세의 {Math.round(BALANCE.economy.sellShare * 100)}%)</span></h3>
        <table className="t">
          <tbody>
            {roster.filter((g) => g.status === "slave").map((g) => {
              const price = Math.round(marketValue(g) * BALANCE.economy.sellShare * sellMult(ratingOf(state, player, "agent")) / 10) * 10;
              return (
                <tr key={g.id}>
                  <td><div className="name-cell"><Portrait g={g} size={26} /><span className="n">{g.name}</span></div></td>
                  <td>{getStyle(g.style).name}</td>
                  <td><Stars value={computeCA(g)} /></td>
                  <td className="num">{price.toLocaleString()} D</td>
                  <td>
                    <button className="btn danger" onClick={() => {
                      if (confirm(`${g.name}을(를) ${price} D에 넘길까요?`)) act((d) => { sellGladiator(d, g.id); });
                    }}>판다</button>
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
