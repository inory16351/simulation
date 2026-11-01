import { FACILITIES, MAX_LEVEL, constructionLabel, startUpgrade, upgradeCost, weeklyUpkeep } from "../../core/facilities";
import type { Ctx } from "../App";

export function FacilitiesScreen({ state, act }: Ctx) {
  const player = state.ludi[state.playerLudusId];
  const building = constructionLabel(state, player);
  return (
    <>
      <h1 className="screen-title">증축</h1>
      <div className="screen-sub">
        한 번에 한 곳만 공사할 수 있다. 시설마다 급료일에 유지비가 나간다 (지금 주 {weeklyUpkeep(player)} D).
      </div>
      {building && <div className="panel"><div className="pad" style={{ color: "var(--torch)" }}>🔨 {building}</div></div>}
      <div className="facility-grid">
        {FACILITIES.map((f) => {
          const lvl = player.facilities[f.id];
          const next = lvl < MAX_LEVEL ? upgradeCost(f.id, lvl + 1) : null;
          const busy = !!player.construction;
          const inProgress = player.construction?.facility === f.id;
          return (
            <div key={f.id} className={`panel facility ${inProgress ? "building" : ""}`}>
              <h3>{f.name}</h3>
              <div className="pad">
                <div className="fac-levels">
                  {Array.from({ length: MAX_LEVEL }, (_, i) => <i key={i} className={i < lvl ? "on" : ""} />)}
                  <span>{lvl}단계</span>
                </div>
                <div className="hint">{f.desc}</div>
                <div className="fac-effect">지금: {lvl ? f.effect(lvl) : "아직 없다"}</div>
                {next && <div className="fac-effect next">다음: {f.effect(lvl + 1)}</div>}
                {next ? (
                  <button className="btn primary" disabled={busy || player.treasury < next.cost}
                    onClick={() => act((d) => { startUpgrade(d, d.ludi[d.playerLudusId], f.id); })}>
                    {inProgress ? "공사 중" : `${lvl ? "증축" : "건설"} · ${next.cost.toLocaleString()} D · ${next.days}일`}
                  </button>
                ) : <span className="tag">최고 단계</span>}
              </div>
            </div>
          );
        })}
      </div>
    </>
  );
}
