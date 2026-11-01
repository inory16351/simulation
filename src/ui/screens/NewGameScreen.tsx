import { useState } from "react";
import { NATIONS } from "../../core/data";
import type { NationId } from "../../core/types";
import { NationCrest } from "../components/common";

/** 새 루두스 세우기: 나라와 이름을 고른다 */
export function NewGameScreen({ onStart, onCancel }: { onStart: (nation: NationId, name: string) => void; onCancel?: () => void }) {
  const [nation, setNation] = useState<NationId>("aurelia");
  const [name, setName] = useState("모래의 집");
  const n = NATIONS.find((x) => x.id === nation)!;

  return (
    <div className="newgame">
      <h1 className="screen-title">새 루두스 세우기</h1>
      <div className="screen-sub">어느 나라에 루두스를 세울지 고르라. 나라마다 리그의 규칙과 관중, 돈의 흐름이 다르다.</div>
      <div className="nation-pick">
        {NATIONS.map((x) => (
          <button key={x.id} className={`nation-card ${x.id === nation ? "on" : ""}`} style={{ ["--nc" as string]: x.color }} onClick={() => setNation(x.id)}>
            <NationCrest nation={x.id} size={48} />
            <div>
              <div className="nc-name">{x.name}</div>
              <div className="nc-motif">모티브: {x.motif}</div>
            </div>
          </button>
        ))}
      </div>
      <div className="panel nation-detail">
        <h3>{n.name} — {n.league}</h3>
        <div className="pad nd-grid">
          <div>
            <div className="nd-meta">수도 {n.capital} · 대표 투기장 {n.arena}</div>
            <p className="nd-lore">{n.lore}</p>
          </div>
          <div>
            <div className="sublabel">리그 특징</div>
            <ul className="nd-rules">{n.ruleText.map((t) => <li key={t}>{t}</li>)}</ul>
            <div className="sublabel" style={{ marginTop: 12 }}>문화</div>
            <p style={{ margin: 0 }}>{n.culture}</p>
          </div>
        </div>
      </div>
      <div className="newgame-start">
        <label>루두스 이름 <input type="text" value={name} maxLength={16} onChange={(e) => setName(e.target.value)} /></label>
        <button className="btn primary" disabled={!name.trim()} onClick={() => onStart(nation, name.trim())}>{n.short}에 루두스를 세운다</button>
        {onCancel && <button className="btn" onClick={onCancel}>돌아가기</button>}
      </div>
    </div>
  );
}
