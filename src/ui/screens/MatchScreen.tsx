import { useEffect, useMemo, useRef, useState } from "react";
import { BALANCE, getStyle } from "../../core/data";
import { buildCommentary, type CommentaryLine } from "../../core/combat/commentary";
import { simulateMatch, type HighlightKind, type MatchResult, type Tactic } from "../../core/combat/sim";
import { canFight, computeCA } from "../../core/gladiator";
import { applyMatchResult } from "../../core/match";
import { Rng } from "../../core/rng";
import { pushMessage } from "../../core/state";
import type { DeathRule, GameState, Gladiator, Id } from "../../core/types";
import type { Nav } from "../App";
import { Portrait, Stars } from "../components/common";

type Item = { kind: "line"; line: CommentaryLine } | { kind: "cut"; cut: HighlightKind };

const CUT_LABEL: Record<HighlightKind, string> = {
  entrance: "입장",
  crit: "치명타",
  execute: "처형",
  race_skill: "각성",
  comeback: "역전",
  missio: "미시오",
  death: "사망",
};

const TACTICS: { id: Tactic; label: string }[] = [
  { id: "balanced", label: "균형" },
  { id: "aggressive", label: "공격적" },
  { id: "defensive", label: "수비적" },
  { id: "showman", label: "쇼맨십" },
  { id: "quick", label: "빠른 결착" },
];

const SPEEDS = [
  { id: "normal", label: "보통", ms: 650 },
  { id: "fast", label: "빠르게", ms: 220 },
  { id: "instant", label: "결과만", ms: 0 },
] as const;

interface Live {
  result: MatchResult;
  items: Item[];
  a: Gladiator;
  b: Gladiator;
  seed: number;
  rule: DeathRule;
}

export function MatchScreen({ state, update, nav }: { state: GameState; update: (fn: (d: GameState) => void) => void; nav: Nav }) {
  const player = state.ludi[state.playerLudusId];
  const mine = player.gladiators.map((id) => state.gladiators[id]).filter(canFight);
  const others = Object.values(state.ludi).filter((l) => !l.isPlayer).sort((x, y) => y.rp - x.rp);

  const [myId, setMyId] = useState<Id | undefined>(mine[0]?.id);
  const [oppLudus, setOppLudus] = useState<Id>(others.find((l) => l.tier === player.tier)?.id ?? others[0].id);
  const oppList = state.ludi[oppLudus].gladiators.map((id) => state.gladiators[id]).filter(canFight);
  const [oppId, setOppId] = useState<Id | undefined>(oppList[0]?.id);
  const [rule, setRule] = useState<DeathRule>("training");
  const [tactic, setTactic] = useState<Tactic>("balanced");
  const [speed, setSpeed] = useState<(typeof SPEEDS)[number]["id"]>("normal");
  const [live, setLive] = useState<Live | null>(null);

  useEffect(() => {
    if (!oppList.find((g) => g.id === oppId)) setOppId(oppList[0]?.id);
  }, [oppLudus]); // eslint-disable-line react-hooks/exhaustive-deps

  const start = () => {
    const a = state.gladiators[myId!];
    const b = state.gladiators[oppId!];
    const r = new Rng(state.rngState);
    const seed = r.int(0, 2 ** 31 - 1);
    update((d) => { d.rngState = r.state; });
    const oppTactic = (["balanced", "aggressive", "defensive"] as Tactic[])[seed % 3];
    const result = simulateMatch(a, b, { rule, seed, tactics: [tactic, oppTactic] });
    const lines = buildCommentary(result.events, { [a.id]: a, [b.id]: b }, seed);
    const items: Item[] = [];
    for (const e of result.events) {
      if (e.t === "highlight") items.push({ kind: "cut", cut: e.kind });
      else for (const l of lines) if (l.event === e) items.push({ kind: "line", line: l });
    }
    setLive({ result, items, a: structuredClone(a), b: structuredClone(b), seed, rule });
  };

  if (live) {
    return (
      <Playback
        live={live}
        speedMs={SPEEDS.find((s) => s.id === speed)!.ms}
        onDone={() => {
          update((d) => {
            applyMatchResult(d, live.result, { ranked: false, rpMult: 1 });
            const w = d.gladiators[live.result.winnerId];
            const l = d.gladiators[live.result.loserId];
            const died = live.result.died ? d.gladiators[live.result.died] : null;
            pushMessage(d, {
              kind: died ? "death" : "match",
              title: died ? `${died.name}, 모래 위에서 숨을 거두다` : `친선 경기: ${w.name} 승리`,
              body: `${BALANCE.rules[live.rule].name} · ${w.name} vs ${l.name}\n` +
                `${w.name} 승리 (${{ ko: "KO", surrender: "항복", timeout: "판정" }[live.result.reason]}, ${live.result.rounds}라운드)` +
                (died ? `\n\n${died.name}의 이름이 묘비에 새겨졌다. 되돌릴 수 없다.` : ""),
            });
          });
        }}
        onClose={() => setLive(null)}
        nav={nav}
      />
    );
  }

  return (
    <>
      <h1 className="screen-title">친선 경기</h1>
      <div className="screen-sub">
        RP 변동 없는 연습 경기. 단, 생사 규칙은 실제로 적용된다 — 죽으면 돌아오지 않는다.
      </div>
      <div className="match-setup">
        <div className="panel">
          <h3>우리 검투사</h3>
          <div className="pad">
            <FighterPicker list={mine} value={myId} onChange={setMyId} />
          </div>
        </div>
        <div className="panel">
          <h3>상대</h3>
          <div className="pad">
            <div className="field">
              <label>루두스</label>
              <select value={oppLudus} onChange={(e) => setOppLudus(e.target.value)}>
                {others.map((l) => (
                  <option key={l.id} value={l.id}>{l.tier}부 · {l.name} (RP {l.rp})</option>
                ))}
              </select>
            </div>
            <FighterPicker list={oppList} value={oppId} onChange={setOppId} />
          </div>
        </div>
        <div className="panel">
          <h3>조건</h3>
          <div className="pad">
            <div className="field">
              <label>생사 규칙</label>
              <div className="seg">
                {(["training", "missio", "sudden"] as DeathRule[]).map((r) => (
                  <button key={r} className={rule === r ? "on" : ""} onClick={() => setRule(r)}>{BALANCE.rules[r].name}</button>
                ))}
              </div>
              <div style={{ color: "var(--text-faint)", fontSize: 11, marginTop: 4 }}>
                {rule === "training" && "쓰러지면 종료. 사고사 1%"}
                {rule === "missio" && "쓰러지면 관중이 생사를 판정한다 (5~40%)"}
                {rule === "sudden" && "패자는 거의 확실히 죽는다 (85~95%)"}
              </div>
            </div>
            <div className="field">
              <label>전술 지시</label>
              <select value={tactic} onChange={(e) => setTactic(e.target.value as Tactic)}>
                {TACTICS.map((t) => <option key={t.id} value={t.id}>{t.label}</option>)}
              </select>
            </div>
            <div className="field">
              <label>중계 속도</label>
              <div className="seg">
                {SPEEDS.map((s) => (
                  <button key={s.id} className={speed === s.id ? "on" : ""} onClick={() => setSpeed(s.id)}>{s.label}</button>
                ))}
              </div>
            </div>
            <button className={`btn ${rule === "sudden" ? "danger" : "primary"}`} disabled={!myId || !oppId} onClick={start} style={{ width: "100%", marginTop: 6 }}>
              {rule === "sudden" ? "사투를 시작한다" : "경기 시작"}
            </button>
          </div>
        </div>
      </div>
    </>
  );
}

function FighterPicker({ list, value, onChange }: { list: Gladiator[]; value?: Id; onChange: (id: Id) => void }) {
  if (list.length === 0) return <div style={{ color: "var(--text-dim)" }}>출전 가능한 검투사가 없다.</div>;
  return (
    <table className="t">
      <tbody>
        {list.map((g) => (
          <tr key={g.id} className={`click ${g.id === value ? "me" : ""}`} onClick={() => onChange(g.id)}>
            <td>
              <div className="name-cell">
                <Portrait g={g} size={28} />
                <span className="n">{g.name}</span>
              </div>
            </td>
            <td style={{ color: "var(--text-dim)" }}>{getStyle(g.style).name}</td>
            <td><Stars value={computeCA(g)} /></td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function Playback({ live, speedMs, onDone, onClose, nav }: { live: Live; speedMs: number; onDone: () => void; onClose: () => void; nav: Nav }) {
  const [shown, setShown] = useState(speedMs === 0 ? live.items.length : 0);
  const [cut, setCut] = useState<{ kind: HighlightKind; key: number } | null>(null);
  const [flash, setFlash] = useState(0);
  const doneRef = useRef(false);
  const feedRef = useRef<HTMLDivElement>(null);
  const finished = shown >= live.items.length;

  useEffect(() => {
    if (finished) return;
    const item = live.items[shown];
    const delay = item.kind === "cut" ? (item.cut === "death" ? 2400 : item.cut === "missio" ? 1600 : 900) : speedMs;
    if (item.kind === "cut" && speedMs > 0) setCut({ kind: item.cut, key: shown });
    if (item.kind === "line" && (item.line.tone === "crit" || item.line.tone === "blood")) setFlash(shown);
    const t = setTimeout(() => setShown((s) => s + 1), speedMs === 0 ? 0 : delay);
    return () => clearTimeout(t);
  }, [shown, finished, speedMs, live.items]);

  useEffect(() => {
    if (finished && !doneRef.current) {
      doneRef.current = true;
      onDone();
    }
  }, [finished, onDone]);

  useEffect(() => {
    feedRef.current?.scrollTo({ top: feedRef.current.scrollHeight });
  }, [shown]);

  const visible = live.items.slice(0, shown);
  const hp = useMemo(() => {
    const h: Record<Id, { hp: number; max: number }> = {};
    for (const it of visible) {
      if (it.kind !== "line") continue;
      const e = it.line.event;
      if (e.t === "attack") h[e.target] = { hp: e.hpAfter, max: e.maxHp };
      if (e.t === "poison") h[e.target] = { hp: e.hpAfter, max: h[e.target]?.max ?? e.hpAfter };
    }
    return h;
  }, [visible]);
  const crowd = [...visible].reverse().find((i) => i.kind === "line" && i.line.event.t === "crowd");
  const crowdValue = crowd && crowd.kind === "line" && crowd.line.event.t === "crowd" ? crowd.line.event.value : BALANCE.combat.crowdStart;
  const dead = finished ? live.result.died : null;

  const fighterBox = (g: Gladiator, right: boolean) => {
    const h = hp[g.id];
    const pct = h ? (h.hp / h.max) * 100 : 100;
    return (
      <div className={`fighter ${right ? "right" : ""}`}>
        <Portrait g={{ ...g, alive: dead !== g.id }} size={64} />
        <div>
          <div className="fname">{g.name}</div>
          <div style={{ color: "var(--text-dim)", fontSize: 12 }}>{getStyle(g.style).name} · <Stars value={computeCA(g)} /></div>
          <div className="hpbar"><i style={{ width: `${pct}%` }} /></div>
        </div>
      </div>
    );
  };

  return (
    <>
      <h1 className="screen-title">{BALANCE.rules[live.rule].name}</h1>
      <div className="screen-sub">친선 경기</div>
      <div className="arena">
        <div className="arena-head">
          {fighterBox(live.a, false)}
          <div className="crowd-meter"><b>{crowdValue}</b>관중 열광</div>
          {fighterBox(live.b, true)}
        </div>
        <div className="feed" ref={feedRef}>
          {visible.map((it, i) =>
            it.kind === "line" ? (
              <div key={i} className={`line ${it.line.tone}`}>
                <span className="round">{it.line.round > 0 ? `${it.line.round}R` : ""}</span>
                {it.line.text}
              </div>
            ) : null,
          )}
        </div>
        {flash > 0 && speedMs > 0 && <div key={`f${flash}`} className="flash" />}
        {cut && speedMs > 0 && (
          <div key={cut.key} className={`cut ${cut.kind}`}>
            {(cut.kind === "crit" || cut.kind === "execute" || cut.kind === "death") &&
              Array.from({ length: 7 }, (_, i) => (
                <span key={i} className="splat" style={{
                  left: `${20 + ((cut.key * 37 + i * 53) % 60)}%`, top: `${15 + ((cut.key * 17 + i * 29) % 65)}%`,
                  width: 20 + ((i * 23) % 60), height: 20 + ((i * 31) % 50),
                }} />
              ))}
            <div className="label">{CUT_LABEL[cut.kind]}</div>
          </div>
        )}
      </div>
      <div style={{ marginTop: 12, display: "flex", gap: 8 }}>
        {!finished && <button className="btn" onClick={() => setShown(live.items.length)}>결과로 건너뛰기</button>}
        {finished && <button className="btn primary" onClick={onClose}>다른 경기</button>}
        {finished && <button className="btn" onClick={() => nav.go("roster")}>로스터로</button>}
      </div>
    </>
  );
}
