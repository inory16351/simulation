import { makeAvatar } from "../../core/avatar";
import { getNation } from "../../core/data";
import { josa } from "../../core/josa";
import { useState } from "react";
import { ROLES, ROLE_DESC, ROLE_NAMES, fireStaff, hireStaff, staffDemand, staffOf, staffWages } from "../../core/staff";
import { ludusReputation, REPUTATION_NAMES } from "../../core/reputation";
import { Rng } from "../../core/rng";
import { Negotiate } from "../components/Negotiate";
import type { Gladiator, Staff, StaffRole } from "../../core/types";
import type { Ctx } from "../App";
import { AvatarSvg } from "../components/Avatar";
import { Bar } from "../components/common";

/** 식솔 초상화: 검투사와 같은 그림을 쓰되 평상복 차림 */
function StaffFace({ s, nation, size = 44 }: { s: Staff; nation: string; size?: number }) {
  const fake = {
    race: s.race, gender: s.gender, traits: [], portraitSeed: s.portraitSeed, age: s.age,
    equipment: { main: "gladius", off: null, armor: "leather", helmet: null }, style: s.role === "medicus" ? "sorcerer" : "murmillo",
  } as unknown as Gladiator;
  return (
    <div className="portrait" style={{ width: size, height: size }}>
      <AvatarSvg spec={{ ...makeAvatar(fake), helmet: null }} bg={nation} seed={s.portraitSeed % 997} />
    </div>
  );
}

export function StaffScreen({ state, act }: Ctx) {
  const player = state.ludi[state.playerLudusId];
  const color = getNation(state.nation).color;
  const rep = ludusReputation(state, player);
  const [role, setRole] = useState<StaffRole | "">("");
  const [minRating, setMinRating] = useState(0);
  const [onlyWilling, setOnlyWilling] = useState(false);
  const [msg, setMsg] = useState<string | null>(null);
  const list = state.staffMarket.map((id) => state.staff[id]).filter(Boolean)
    .filter((c) => (!role || c.role === role) && c.rating >= minRating)
    .map((c) => ({ s: c, d: staffDemand(state, c) }))
    .filter((x) => !onlyWilling || !x.d.refuse)
    .sort((x, y) => y.s.rating - x.s.rating);

  return (
    <>
      <h1 className="screen-title">식솔</h1>
      <div className="screen-sub">자리마다 한 명씩 둔다. 식솔 급료는 급료일에 함께 나간다 (지금 주 {staffWages(state, player)} D). 후보는 이적 시장이 열릴 때 바뀐다.</div>
      <div className="staff-grid">
        {ROLES.map((role) => {
          const s = staffOf(state, player, role);
          return (
            <div key={role} className="panel">
              <h3>{ROLE_NAMES[role]}</h3>
              <div className="pad">
                <div className="hint" style={{ marginBottom: 8 }}>{ROLE_DESC[role]}</div>
                {s ? (
                  <div className="staff-card">
                    <StaffFace s={s} nation={color} />
                    <div style={{ flex: 1 }}>
                      <div className="n" style={{ color: "var(--parchment)", fontWeight: 700 }}>{s.name}</div>
                      <div>실력 <Bar value={s.rating} max={20} color="var(--torch)" /> {s.rating}</div>
                      <div className="hint">주급 {s.wage} D · {s.age}세</div>
                    </div>
                    <button className="btn danger" onClick={() => { if (confirm(`${josa(s.name, "을/를")} 내보낼까요?`)) act((d) => fireStaff(d, s.id)); }}>내보낸다</button>
                  </div>
                ) : <div className="hint">비어 있다.</div>}
              </div>
            </div>
          );
        })}
      </div>
      <div className="panel">
        <h3>식솔 찾기</h3>
        <div className="pad search-filters">
          <label>자리 <select value={role} onChange={(e) => setRole(e.target.value as StaffRole | "")}>
            <option value="">전부</option>{ROLES.map((r) => <option key={r} value={r}>{ROLE_NAMES[r]}</option>)}
          </select></label>
          <label>실력 ≥ <select value={minRating} onChange={(e) => setMinRating(Number(e.target.value))}>
            {[0, 5, 10, 15].map((v) => <option key={v} value={v}>{v || "제한 없음"}</option>)}
          </select></label>
          <label><input type="checkbox" checked={onlyWilling} onChange={(e) => setOnlyWilling(e.target.checked)} /> 올 마음이 있는 사람만</label>
          <span className="hint">우리 명성 {rep} ({REPUTATION_NAMES(rep)}). 실력 좋은 사람일수록 이름난 루두스를 원한다</span>
        </div>
        {msg && <div className="pad"><span className="tag neg">{msg}</span></div>}
        <table className="t">
          <thead><tr><th>이름</th><th>자리</th><th>실력</th><th>협상</th></tr></thead>
          <tbody>
            {list.length === 0 && <tr><td colSpan={4} className="hint">조건에 맞는 사람이 없다. 후보는 이적 시장이 열릴 때 바뀐다.</td></tr>}
            {list.map(({ s: c, d }) => {
              const taken = !!staffOf(state, player, c.role);
              return (
                <tr key={c.id}>
                  <td><div className="name-cell"><StaffFace s={c} nation={color} size={30} /><span className="n">{c.name}</span><span className="hint">{c.age}세</span></div></td>
                  <td>{ROLE_NAMES[c.role]}</td>
                  <td><Bar value={c.rating} max={20} color="var(--torch)" /> {c.rating}</td>
                  <td>
                    <Negotiate demand={d} disabled={taken ? "그 자리는 이미 차 있다. 먼저 내보내라" : undefined} label="고용 제안" onOffer={(wage) => {
                      const r = new Rng((state.rngState ^ state.nextId) >>> 0).next();
                      setMsg(hireStaff(structuredClone(state), c.id, wage, r));
                      act((st) => { hireStaff(st, c.id, wage, r); });
                    }} />
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
