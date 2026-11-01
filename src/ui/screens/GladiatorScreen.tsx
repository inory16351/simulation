import { getArmor, getNation, getRace, getSkill, getStyle, getTrait, getWeapon } from "../../core/data";
import { AGE_STAGE_NAMES, ageStage, computeCA, effectiveAttr } from "../../core/gladiator";
import {
  MENTAL, PHYSICAL, TECHNICAL, ATTR_NAMES, WEAPON_CLASS_NAMES, WEAPON_CLASSES,
  type AttrId, type GameState,
} from "../../core/types";
import type { Nav } from "../App";
import { Bar, Portrait, Stars, attrClass } from "../components/common";

const STATUS_NAMES = { slave: "노예", free: "자유민", freedman: "해방민" };

export function GladiatorScreen({ state, id, nav }: { state: GameState; id: string; nav: Nav }) {
  const g = state.gladiators[id];
  const race = getRace(g.race);
  const style = getStyle(g.style);
  const ca = computeCA(g);
  const owner = Object.values(state.ludi).find((l) => l.gladiators.includes(g.id));
  const isMine = owner?.isPlayer ?? state.fallen.includes(g.id);

  const attrBlock = (title: string, ids: readonly AttrId[]) => (
    <div className="panel">
      <h3>{title}</h3>
      <table className="t attr-table">
        <tbody>
          {ids.map((a) => {
            const v = effectiveAttr(g, a);
            return (
              <tr key={a}>
                <td>{ATTR_NAMES[a]}</td>
                <td className={`v ${attrClass(v)}`}>{v}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );

  return (
    <>
      <div style={{ marginBottom: 10 }}>
        <button className="btn" onClick={() => nav.go("roster")}>← 로스터</button>
      </div>
      <div className="panel">
        <div className="pad profile-head">
          <Portrait g={g} size={180} />
          <div>
            <div className="big-name">
              {g.name} {g.nickname && <span style={{ fontSize: 15, color: "var(--text-dim)" }}>"{g.nickname}"</span>}
            </div>
            <div style={{ color: "var(--text-dim)" }}>
              {race.name} · {getNation(g.nation).short} 출신 · {g.age}세 ({AGE_STAGE_NAMES[ageStage(g)]}) · {owner?.name ?? "무소속"}
            </div>
            <dl className="kv">
              <dt>스타일</dt><dd>{style.name} — {style.desc}</dd>
              <dt>신분</dt><dd>{STATUS_NAMES[g.status]}</dd>
              <dt>주급</dt><dd>{g.wage} D {g.unpaidWeeks > 0 && <span className="tag neg">미납 {g.unpaidWeeks}주</span>}</dd>
              <dt>현재 능력</dt><dd><Stars value={ca} /> <span style={{ color: "var(--text-faint)" }}>(CA {ca})</span></dd>
              <dt>잠재력</dt>
              <dd>
                {isMine
                  ? <Stars value={Math.max(ca, g.hidden.pa - 15)} range={Math.min(200, g.hidden.pa + 15)} />
                  : <span style={{ color: "var(--text-faint)" }}>스카우트 필요</span>}
              </dd>
              <dt>사기</dt><dd><Bar value={g.morale} /> {g.morale}</dd>
              <dt>인기</dt><dd><Bar value={g.popularity} color="var(--torch)" /> {g.popularity}</dd>
              <dt>피로</dt><dd><Bar value={100 - g.fatigue} /> {g.fatigue}</dd>
              <dt>상태</dt>
              <dd>
                {!g.alive
                  ? <span className="tag neg">사망 — 시즌 {g.diedAt?.season} {g.diedAt?.week}주차, {g.diedAt?.cause}</span>
                  : g.injury
                    ? <span className="tag inj">{g.injury.desc} ({g.injury.severity === "serious" ? "중상" : "경상"}, {g.injury.weeksLeft}주)</span>
                    : "출전 가능"}
              </dd>
              <dt>전적</dt><dd>{g.record.wins}승 {g.record.losses}패 · 살해 {g.record.kills}</dd>
            </dl>
            <div style={{ marginTop: 10 }}>
              <span className="tag" title={race.racialTrait.desc}>◆ {race.racialTrait.name}</span>
              {g.traits.map((t) => {
                const def = getTrait(t);
                if (def.hidden && !isMine) return <span key={t} className="tag hidden">?</span>;
                return (
                  <span key={t} className={`tag ${def.negative ? "neg" : ""} ${def.hidden ? "hidden" : ""}`} title={def.desc}>
                    {def.name}
                  </span>
                );
              })}
            </div>
          </div>
        </div>
      </div>

      <div className="grid3" style={{ gridTemplateColumns: "1fr 1fr 1fr 1.2fr" }}>
        {attrBlock("신체", PHYSICAL)}
        {attrBlock("기술", TECHNICAL)}
        {attrBlock("정신", MENTAL)}
        <div className="panel">
          <h3>무기 숙련</h3>
          <table className="t attr-table">
            <tbody>
              {WEAPON_CLASSES.map((w) => (
                <tr key={w}>
                  <td>{WEAPON_CLASS_NAMES[w]}</td>
                  <td><Bar value={g.prof[w]} max={20} color="var(--bronze-hi)" /></td>
                  <td className={`v ${attrClass(g.prof[w])}`}>{g.prof[w]}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      <div className="grid2">
        <div className="panel">
          <h3>스킬</h3>
          <div className="pad">
            {g.skills.active.map((s) => <span key={s} className="tag">{getSkill(s)?.name ?? s}</span>)}
            {Array.from({ length: 4 - g.skills.active.length }, (_, i) => <span key={i} className="tag hidden">빈 칸</span>)}
            <div style={{ marginTop: 6 }}>
              {g.skills.passive.map((s) => <span key={s} className="tag">{getSkill(s)?.name}</span>)}
              {Array.from({ length: 2 - g.skills.passive.length }, (_, i) => <span key={i} className="tag hidden">패시브 빈 칸</span>)}
            </div>
          </div>
        </div>
        <div className="panel">
          <h3>장비</h3>
          <div className="pad">
            <span className="tag">주무기: {getWeapon(g.equipment.main)?.name}</span>
            {g.equipment.off && <span className="tag">보조: {getWeapon(g.equipment.off)?.name ?? getArmor(g.equipment.off)?.name}</span>}
            <span className="tag">갑옷: {getArmor(g.equipment.armor)?.name}</span>
            {g.equipment.helmet && <span className="tag">투구: {getArmor(g.equipment.helmet)?.name}</span>}
          </div>
        </div>
      </div>
    </>
  );
}
