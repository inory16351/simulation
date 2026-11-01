import { useState } from "react";
import type { Demand } from "../../core/reputation";

/** 협상 칸: 원하는 조건을 보여 주고 주급을 제안한다 */
export function Negotiate({ demand, disabled, onOffer, seasons, label = "제안" }: {
  demand: Demand;
  disabled?: string;
  onOffer: (wage: number, seasons: number) => void;
  /** 검투사 계약은 기간도 정한다 */
  seasons?: boolean;
  label?: string;
}) {
  const [wage, setWage] = useState(demand.wage);
  const [years, setYears] = useState(2);
  if (demand.refuse) return <span className="tag neg tip" data-tip="루두스 명성을 올리면 마음이 바뀔 수 있다">관심 없음 · {demand.reason}</span>;
  return (
    <div className="negotiate">
      <span className="hint tip" data-tip={demand.reason}>원함 {demand.wage} D</span>
      <input type="number" min={1} value={wage} onChange={(e) => setWage(Number(e.target.value))} />
      {seasons && (
        <select value={years} onChange={(e) => setYears(Number(e.target.value))}>
          {[1, 2, 3, 4].map((y) => <option key={y} value={y}>{y}시즌</option>)}
        </select>
      )}
      <button className="btn primary" disabled={!!disabled || wage <= 0} title={disabled} onClick={() => onOffer(wage, years)}>{label}</button>
    </div>
  );
}
