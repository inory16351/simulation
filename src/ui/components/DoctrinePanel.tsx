import { getSkill } from "../../core/data";
import {
  ACTION_NAMES, COND_NAMES, FAMILIARITY_CHANGE_COST, FOCUS_DESC, FOCUS_NAMES, RANGE_NAMES, STANCE_DESC, STANCE_NAMES,
  describeRule, type Doctrine, type DoctrineRule, type Focus, type RangePref, type RuleAction, type RuleCond, type Stance,
} from "../../core/tactics";
import type { Gladiator } from "../../core/types";
import { Bar } from "./common";

const skillName = (id: string) => getSkill(id)?.name ?? id;

export function DoctrinePanel({ g, editable, onChange }: { g: Gladiator; editable: boolean; onChange: (d: Doctrine) => void }) {
  const d = g.doctrine;
  /** 교범을 바꾸면 숙련도가 떨어진다 */
  const change = (patch: Partial<Doctrine>) =>
    onChange({ ...d, ...patch, familiarity: Math.max(0, d.familiarity - FAMILIARITY_CHANGE_COST) });

  const moveSkill = (i: number, dir: -1 | 1) => {
    const order = [...d.order];
    const j = i + dir;
    if (j < 0 || j >= order.length) return;
    [order[i], order[j]] = [order[j], order[i]];
    change({ order });
  };

  const setRule = (i: number, rule: DoctrineRule | null) => {
    const rules = [...d.rules];
    if (rule) rules[i] = rule;
    else rules.splice(i, 1);
    change({ rules });
  };

  return (
    <div className="panel doctrine">
      <h3>전투 교범</h3>
      <div className="pad">
        <div className="doctrine-fam">
          교범 숙련도 <Bar value={d.familiarity} color="var(--torch)" /> {d.familiarity}
          {editable && <span className="hint">고칠 때마다 숙련도 -{FAMILIARITY_CHANGE_COST}. 연무장 수련과 실전으로 다시 오른다.</span>}
        </div>

        <div className="doctrine-grid">
          <Choice label="자세" value={d.stance} names={STANCE_NAMES} desc={STANCE_DESC} disabled={!editable}
            onPick={(v) => change({ stance: v as Stance })} />
          <Choice label="거리" value={d.range} names={RANGE_NAMES} disabled={!editable}
            onPick={(v) => change({ range: v as RangePref })} />
          <Choice label="노리는 곳" value={d.focus} names={FOCUS_NAMES} desc={FOCUS_DESC} disabled={!editable}
            onPick={(v) => change({ focus: v as Focus })} />
        </div>

        <div className="doctrine-cols">
          <div>
            <div className="sublabel">기술 순서</div>
            {d.order.map((s, i) => (
              <div key={s} className="order-row">
                <span className="order-n">{i + 1}</span>
                <span className="order-name">{skillName(s)}</span>
                {editable && (
                  <>
                    <button className="mini" disabled={i === 0} onClick={() => moveSkill(i, -1)}>▲</button>
                    <button className="mini" disabled={i === d.order.length - 1} onClick={() => moveSkill(i, 1)}>▼</button>
                  </>
                )}
              </div>
            ))}
            {d.order.length === 0 && <div className="hint">익힌 기술이 없다.</div>}
          </div>
          <div>
            <div className="sublabel">조건 명령 <span className="hint">위에서부터 먼저 맞는 한 줄만 실행</span></div>
            {d.rules.map((r, i) =>
              editable
                ? <RuleEditor key={i} rule={r} skills={d.order} onChange={(nr) => setRule(i, nr)} />
                : <div key={i} className="rule-text">{i + 1}. {describeRule(r, skillName)}</div>,
            )}
            {editable && d.rules.length < 3 && (
              <button className="btn" style={{ marginTop: 6 }} onClick={() => setRule(d.rules.length, {
                cond: { type: "myHpBelow", value: 40 }, action: { type: "stance", stance: "bulwark" },
              })}>+ 조건 명령 추가</button>
            )}
            <label className="check">
              <input type="checkbox" checked={!d.allowSurrender} disabled={!editable}
                onChange={(e) => change({ allowSurrender: !e.target.checked })} />
              항복하지 않고 끝까지 싸운다 <span className="hint">(살아날 길이 줄어든다)</span>
            </label>
          </div>
        </div>
      </div>
    </div>
  );
}

function Choice<T extends string>({ label, value, names, desc, disabled, onPick }: {
  label: string; value: T; names: Record<T, string>; desc?: Record<T, string>; disabled: boolean; onPick: (v: T) => void;
}) {
  return (
    <div>
      <div className="sublabel">{label}</div>
      <div className="seg wrap">
        {(Object.keys(names) as T[]).map((k) => (
          <button key={k} className={k === value ? "on" : ""} disabled={disabled && k !== value} title={desc?.[k]}
            onClick={() => k !== value && onPick(k)}>
            {names[k]}
          </button>
        ))}
      </div>
      {desc && <div className="hint" style={{ marginTop: 4 }}>{desc[value]}</div>}
    </div>
  );
}

function RuleEditor({ rule, skills, onChange }: { rule: DoctrineRule; skills: string[]; onChange: (r: DoctrineRule | null) => void }) {
  const c = rule.cond;
  const a = rule.action;
  const hasValue = c.type !== "oppDisabled";
  const setCond = (type: RuleCond["type"]) => {
    const cond: RuleCond = type === "oppDisabled" ? { type } : type === "roundAtLeast" ? { type, value: 10 } : { type, value: 40 };
    onChange({ ...rule, cond });
  };
  const setAction = (type: RuleAction["type"]) => {
    const action: RuleAction =
      type === "stance" ? { type, stance: "bulwark" } : type === "skill" ? { type, skill: skills[0] ?? "skill.power_strike" } : { type };
    onChange({ ...rule, action });
  };
  return (
    <div className="rule-row">
      <span>만약</span>
      <select value={c.type} onChange={(e) => setCond(e.target.value as RuleCond["type"])}>
        {(Object.keys(COND_NAMES) as RuleCond["type"][]).map((k) => <option key={k} value={k}>{COND_NAMES[k]}</option>)}
      </select>
      {hasValue && (
        <input type="number" value={c.value} min={1} max={c.type === "roundAtLeast" ? 30 : 99}
          onChange={(e) => onChange({ ...rule, cond: { ...c, value: Number(e.target.value) } as RuleCond })} />
      )}
      <span>→</span>
      <select value={a.type} onChange={(e) => setAction(e.target.value as RuleAction["type"])}>
        {(Object.keys(ACTION_NAMES) as RuleAction["type"][]).map((k) => <option key={k} value={k}>{ACTION_NAMES[k]}</option>)}
      </select>
      {a.type === "stance" && (
        <select value={a.stance} onChange={(e) => onChange({ ...rule, action: { type: "stance", stance: e.target.value as Stance } })}>
          {(Object.keys(STANCE_NAMES) as Stance[]).map((k) => <option key={k} value={k}>{STANCE_NAMES[k]}</option>)}
        </select>
      )}
      {a.type === "skill" && (
        <select value={a.skill} onChange={(e) => onChange({ ...rule, action: { type: "skill", skill: e.target.value } })}>
          {skills.map((s) => <option key={s} value={s}>{skillName(s)}</option>)}
        </select>
      )}
      <button className="mini" title="삭제" onClick={() => onChange(null)}>✕</button>
    </div>
  );
}
