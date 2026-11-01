import { makeAvatar } from "../../core/avatar";
import { getNation } from "../../core/data";
import { ROLES, ROLE_DESC, ROLE_NAMES, fireStaff, hireStaff, staffOf, staffWages } from "../../core/staff";
import type { Gladiator, Staff } from "../../core/types";
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
  const candidates = state.staffMarket.map((id) => state.staff[id]).filter(Boolean);

  return (
    <>
      <h1 className="screen-title">식솔</h1>
      <div className="screen-sub">자리마다 한 명씩 둔다. 식솔 급료는 급료일에 함께 나간다 (지금 주 {staffWages(state, player)} D). 후보는 경매장이 바뀔 때 함께 바뀐다.</div>
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
                    <button className="btn danger" onClick={() => { if (confirm(`${s.name}을(를) 내보낼까요?`)) act((d) => fireStaff(d, s.id)); }}>내보낸다</button>
                  </div>
                ) : <div className="hint">비어 있다.</div>}
              </div>
            </div>
          );
        })}
      </div>
      <div className="panel">
        <h3>고용 후보</h3>
        <table className="t">
          <thead><tr><th>이름</th><th>자리</th><th>실력</th><th className="num">주급</th><th /></tr></thead>
          <tbody>
            {candidates.map((s) => {
              const taken = !!staffOf(state, player, s.role);
              return (
                <tr key={s.id}>
                  <td><div className="name-cell"><StaffFace s={s} nation={color} size={30} /><span className="n">{s.name}</span></div></td>
                  <td>{ROLE_NAMES[s.role]}</td>
                  <td><Bar value={s.rating} max={20} color="var(--torch)" /> {s.rating}</td>
                  <td className="num">{s.wage} D</td>
                  <td><button className="btn primary" disabled={taken} title={taken ? "그 자리는 이미 차 있다" : undefined} onClick={() => act((d) => { hireStaff(d, s.id); })}>고용</button></td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </>
  );
}
