import { useEffect, useMemo, useRef, useState } from "react";
import { BALANCE, getStyle } from "../../core/data";
import { buildCommentary, type CommentaryLine } from "../../core/combat/commentary";
import { simulateMatch, type HighlightKind, type MatchResult } from "../../core/combat/sim";
import { FOCUS_NAMES, RANGE_NAMES, STANCE_NAMES, type Stance } from "../../core/tactics";
import { canFight, computeCA } from "../../core/gladiator";
import { applyMatchResult } from "../../core/match";
import { availability, now, playChallenge, todaysPlayerMatches } from "../../core/challenge";
import { autoLineup, FORMAT_NAMES, type Series } from "../../core/series";
import { Rng } from "../../core/rng";
import { pushMessage } from "../../core/state";
import type { DeathRule, GameState, Gladiator, Id } from "../../core/types";
import { leagueRules } from "../../core/nationRules";
import type { Ctx, Nav } from "../App";
import { ArenaStage } from "../components/ArenaStage";
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


export const SPEEDS = [
  { id: "normal", label: "보통", ms: 650 },
  { id: "fast", label: "빠르게", ms: 220 },
  { id: "instant", label: "결과만", ms: 0 },
] as const;

export interface Live {
  /** 단체전·컵: 시작 체력 비율 [왼쪽, 오른쪽] */
  startHp?: [number, number];
  title?: string;
  sub?: string;
  result: MatchResult;
  items: Item[];
  a: Gladiator;
  b: Gladiator;
  seed: number;
  rule: DeathRule;
  /** 공식 경기는 미리 계산한 결과 상태를 중계가 끝난 뒤 반영한다 */
  commit?: GameState;
}

function buildItems(result: MatchResult, a: Gladiator, b: Gladiator, seed: number): Item[] {
  const lines = buildCommentary(result.events, { [a.id]: a, [b.id]: b }, seed);
  const items: Item[] = [];
  for (const e of result.events) {
    if (e.t === "highlight") items.push({ kind: "cut", cut: e.kind });
    else for (const l of lines) if (l.event === e) items.push({ kind: "line", line: l });
  }
  return items;
}

/** 여러 판짜리 경기를 중계용으로 바꾼다. 우리 쪽이 늘 왼쪽 */
export function seriesLives(series: Series, mineIsSide0: boolean, rule: DeathRule, title: string): Live[] {
  let s0 = 0, s1 = 0;
  return series.bouts.map((bt, i) => {
    if (bt.result.winnerId === bt.a.id) s0++; else s1++;
    const [a, b] = mineIsSide0 ? [bt.a, bt.b] : [bt.b, bt.a];
    const hp: [number, number] = mineIsSide0 ? bt.startHp : [bt.startHp[1], bt.startHp[0]];
    const score = mineIsSide0 ? `${s0}:${s1}` : `${s1}:${s0}`;
    return {
      result: bt.result, a, b, seed: 0, rule, startHp: hp,
      items: buildItems(bt.result, bt.a, bt.b, bt.result.rounds + i),
      title, sub: `${i + 1}번째 판 · 이 판까지 ${score}`,
    };
  });
}

/** 공식 경기 중계: 판을 차례로 보여 주고, 마지막 판이 끝나면 미리 계산한 상태를 반영한다 */
export function SeriesRunner({ lives, commit, speedMs, replace, onExit, nav }: { lives: Live[]; commit: GameState; speedMs: number; replace: (s: GameState) => void; onExit: () => void; nav: Nav }) {
  const [idx, setIdx] = useState(0);
  const last = idx >= lives.length - 1;
  return (
    <Playback
      key={idx}
      live={lives[idx]}
      speedMs={speedMs}
      closeLabel={last ? "돌아간다" : "다음 판"}
      onDone={() => { if (last) replace(commit); }}
      onClose={() => (last ? onExit() : setIdx(idx + 1))}
      nav={nav}
    />
  );
}

export function MatchScreen({ state, update, replace, nav }: Ctx) {
  const player = state.ludi[state.playerLudusId];
  const mine = player.gladiators.map((id) => state.gladiators[id]).filter((g) => canFight(g) && !availability(state, g));
  const others = Object.values(state.ludi).filter((l) => !l.isPlayer && l.nation === state.nation).sort((x, y) => y.rp - x.rp);
  const official = todaysPlayerMatches(state);

  const [myId, setMyId] = useState<Id | undefined>(mine[0]?.id);
  const [oppLudus, setOppLudus] = useState<Id>(others.find((l) => l.tier === player.tier)?.id ?? others[0].id);
  const oppList = state.ludi[oppLudus].gladiators.map((id) => state.gladiators[id]).filter(canFight);
  const [oppId, setOppId] = useState<Id | undefined>(oppList[0]?.id);
  const [rule, setRule] = useState<DeathRule>("training");
  /** 이번 경기만 자세를 바꾼다. 비어 있으면 교범 그대로 */
  const [stanceOverride, setStanceOverride] = useState<Stance | "">("");
  const [speed, setSpeed] = useState<(typeof SPEEDS)[number]["id"]>("normal");
  const [live, setLive] = useState<Live | null>(null);
  const [series, setSeries] = useState<{ lives: Live[]; commit: GameState } | null>(null);
  /** 단체전 출전 순서 (경기별) */
  const [lineups, setLineups] = useState<Record<string, Id[]>>({});

  useEffect(() => {
    if (!oppList.find((g) => g.id === oppId)) setOppId(oppList[0]?.id);
  }, [oppLudus]); // eslint-disable-line react-hooks/exhaustive-deps

  /** 공식 경기: 결과를 미리 계산해 두고, 중계가 끝나면 반영한다 */
  const playOfficial = (challengeId: string) => {
    const draft = structuredClone(state);
    const rng = new Rng(draft.rngState);
    const c = draft.challenges.find((x) => x.id === challengeId)!;
    const meFirst = draft.ludi[c.from].isPlayer;
    const order = lineups[challengeId];
    if (c.format && order) {
      if (meFirst) c.fromLineup = order; else c.toLineup = order;
    }
    const played = playChallenge(draft, c, rng);
    draft.rngState = rng.state;
    if (!played.result) {
      pushMessage(draft, { kind: "match", title: "기권", body: played.forfeit ?? "" });
      replace(draft);
      return;
    }
    if (played.series && c.format) {
      const s = played.series;
      const myLudus = draft.playerLudusId;
      const mineSide0 = meFirst;
      const won = (s.winner === 0) === mineSide0;
      const opp = draft.ludi[meFirst ? c.to : c.from];
      const rp = played.summary?.rpChange[myLudus] ?? 0;
      const money = played.summary?.money[myLudus] ?? 0;
      const dead = s.bouts.filter((b) => b.result.died).map((b) => draft.gladiators[b.result.died!].name);
      pushMessage(draft, {
        kind: dead.length ? "death" : "match",
        title: `${FORMAT_NAMES[c.format]} ${won ? "승리" : "패배"}: ${opp.name}`,
        body: s.bouts.map((b, i) => `${i + 1}. ${draft.gladiators[b.result.winnerId].name} > ${draft.gladiators[b.result.loserId].name}`).join("\n") +
          `\nRP ${rp >= 0 ? "+" : ""}${rp} · 수입 ${money.toLocaleString()} D (세전)` +
          (dead.length ? `\n\n죽은 자: ${dead.join(", ")}` : ""),
      });
      setSeries({ lives: seriesLives(s, mineSide0, c.rule, `${FORMAT_NAMES[c.format]} · ${BALANCE.rules[c.rule].name}`), commit: draft });
      return;
    }
    const r = played.result;
    const myLudus = draft.playerLudusId;
    const w = draft.gladiators[r.winnerId];
    const l = draft.gladiators[r.loserId];
    const died = r.died ? draft.gladiators[r.died] : null;
    const rp = played.summary?.rpChange[myLudus] ?? 0;
    const money = played.summary?.money[myLudus] ?? 0;
    pushMessage(draft, {
      kind: died ? "death" : "match",
      title: died ? `${died.name}, 모래 위에서 숨을 거두다` : `${BALANCE.rules[c.rule].name}: ${w.name} 승리`,
      body: `${w.name} vs ${l.name} — ${w.name} 승리 (${{ ko: "KO", surrender: "항복", timeout: "판정" }[r.reason]}, ${r.rounds}라운드)\n` +
        `RP ${rp >= 0 ? "+" : ""}${rp} · 수입 ${money.toLocaleString()} D (세전)` +
        (died ? `\n\n${died.name}의 이름이 비석에 새겨졌다. 다시는 돌아오지 않는다.` : ""),
    });
    setLive({
      result: r, a: meFirst ? played.a : played.b, b: meFirst ? played.b : played.a,
      items: buildItems(r, played.a, played.b, r.rounds), seed: 0, rule: c.rule, commit: draft, sub: "공식 경기",
    });
  };

  const start = () => {
    const a = state.gladiators[myId!];
    const b = state.gladiators[oppId!];
    const r = new Rng(state.rngState);
    const seed = r.int(0, 2 ** 31 - 1);
    update((d) => { d.rngState = r.state; });
    const myDoctrine = stanceOverride ? { ...a.doctrine, stance: stanceOverride } : a.doctrine;
    const result = simulateMatch(a, b, { rule, seed, doctrines: [myDoctrine, undefined], missioDeathMult: leagueRules(state).missioDeathMult });
    setLive({ result, items: buildItems(result, a, b, seed), a: structuredClone(a), b: structuredClone(b), seed, rule });
  };

  if (series) {
    return <SeriesRunner lives={series.lives} commit={series.commit} speedMs={SPEEDS.find((s) => s.id === speed)!.ms} replace={replace} onExit={() => setSeries(null)} nav={nav} />;
  }

  if (live) {
    return (
      <Playback
        live={live}
        speedMs={SPEEDS.find((s) => s.id === speed)!.ms}
        onDone={() => {
          if (live.commit) {
            replace(live.commit);
            return;
          }
          update((d) => {
            applyMatchResult(d, live.result, { rule: live.rule, ranked: false }, new Rng(live.seed));
            const w = d.gladiators[live.result.winnerId];
            const l = d.gladiators[live.result.loserId];
            const died = live.result.died ? d.gladiators[live.result.died] : null;
            pushMessage(d, {
              kind: died ? "death" : "match",
              title: died ? `${died.name}, 모래 위에서 숨을 거두다` : `연습 시합: ${w.name} 승리`,
              body: `${BALANCE.rules[live.rule].name} · ${w.name} vs ${l.name}\n` +
                `${w.name} 승리 (${{ ko: "KO", surrender: "항복", timeout: "판정" }[live.result.reason]}, ${live.result.rounds}라운드)` +
                (died ? `\n\n${died.name}의 이름이 비석에 새겨졌다. 다시는 돌아오지 않는다.` : ""),
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
      <h1 className="screen-title">모래 마당</h1>
      {official.length > 0 && (
        <div className="panel official">
          <h3>오늘의 공식 경기</h3>
          <div className="pad">
            {official.filter((c) => c.format).map((c) => {
              const iAmFrom = state.ludi[c.from].isPlayer;
              const opp = state.ludi[iAmFrom ? c.to : c.from];
              const n = c.format!;
              const pool = player.gladiators.map((id) => state.gladiators[id]).filter((g) => canFight(g) && !availability(state, g));
              const order = lineups[c.id] ?? autoLineup(state, player, n, now(state), [iAmFrom ? c.fromGladiator : c.toGladiator!].filter(Boolean)).map((g) => g.id);
              return (
                <div key={c.id} className="official-row team">
                  <div>
                    <span className={`tag ${c.rule === "sudden" ? "neg" : ""}`}>{FORMAT_NAMES[n]} · {BALANCE.rules[c.rule].name}</span>
                    <div className="hint">상대: {opp.name} — 이긴 사람이 남아 다음 상대와 싸운다. 체력은 조금만 돌아온다</div>
                  </div>
                  <div className="lineup">
                    {Array.from({ length: n }, (_, i) => (
                      <label key={i}>{i === 0 ? "선봉" : i === n - 1 ? "대장" : `${i + 1}번`}
                        <select value={order[i] ?? ""} onChange={(e) => {
                          const next = [...order];
                          const j = next.indexOf(e.target.value);
                          if (j >= 0) next[j] = next[i];
                          next[i] = e.target.value;
                          setLineups({ ...lineups, [c.id]: next.filter(Boolean) });
                        }}>
                          <option value="">—</option>
                          {pool.map((g) => <option key={g.id} value={g.id}>{g.name} · CA {computeCA(g)}</option>)}
                        </select>
                      </label>
                    ))}
                  </div>
                  <button className={`btn ${c.rule === "sudden" ? "danger" : "primary"}`} onClick={() => playOfficial(c.id)}>경기장에 들어간다</button>
                </div>
              );
            })}
            {official.filter((c) => !c.format).map((c) => {
              const iAmFrom = state.ludi[c.from].isPlayer;
              const me = state.gladiators[iAmFrom ? c.fromGladiator : c.toGladiator!];
              const them = state.gladiators[iAmFrom ? c.toGladiator! : c.fromGladiator];
              const opp = state.ludi[iAmFrom ? c.to : c.from];
              return (
                <div key={c.id} className="official-row">
                  <div className="name-cell"><Portrait g={me} size={48} /><span className="n">{me.name}</span></div>
                  <div className="vs">
                    <span className={`tag ${c.rule === "sudden" ? "neg" : ""}`}>{BALANCE.rules[c.rule].name}</span>
                    <div className="hint">{opp.name}</div>
                  </div>
                  <div className="name-cell"><span className="n">{them.name}</span><Portrait g={them} size={48} /></div>
                  <button className={`btn ${c.rule === "sudden" ? "danger" : "primary"}`} onClick={() => playOfficial(c.id)}>경기장에 들어간다</button>
                </div>
              );
            })}
            <div className="field" style={{ marginTop: 10 }}>
              <label>중계 속도</label>
              <div className="seg">
                {SPEEDS.map((s) => (
                  <button key={s.id} className={speed === s.id ? "on" : ""} onClick={() => setSpeed(s.id)}>{s.label}</button>
                ))}
              </div>
            </div>
          </div>
        </div>
      )}
      <div className="screen-sub">
        연습 시합: RP와 돈은 오가지 않지만 성장·피로·혹사는 실전과 같고, 생사 규칙도 그대로다. 죽으면 끝이다.
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
                  <button key={r} className={rule === r ? "on" : ""} disabled={r === "sudden" && leagueRules(state).suddenBanned} onClick={() => setRule(r)}>{BALANCE.rules[r].name}</button>
                ))}
              </div>
              <div style={{ color: "var(--text-faint)", fontSize: 11, marginTop: 4 }}>
                {rule === "training" && "쓰러지면 종료. 사고사 1%"}
                {rule === "missio" && "쓰러지면 관중이 살릴지 죽일지 정한다 (3~25%)"}
                {rule === "sudden" && "지면 거의 죽는다 (85~95%)"}
              </div>
            </div>
            {myId && (
              <div className="field">
                <label>전투 교범</label>
                <div className="doctrine-mini">
                  {(() => {
                    const d = state.gladiators[myId].doctrine;
                    return (
                      <>
                        <span className="tag">{STANCE_NAMES[d.stance]}</span>
                        <span className="tag">{RANGE_NAMES[d.range]}</span>
                        <span className="tag">노림: {FOCUS_NAMES[d.focus]}</span>
                        <span className="tag">숙련 {d.familiarity}</span>
                        <button className="btn" style={{ padding: "2px 8px" }} onClick={() => nav.openGladiator(myId)}>교범 고치기</button>
                      </>
                    );
                  })()}
                </div>
                <label style={{ marginTop: 6 }}>이번 경기만 자세 바꾸기</label>
                <select value={stanceOverride} onChange={(e) => setStanceOverride(e.target.value as Stance | "")}>
                  <option value="">교범대로</option>
                  {(Object.keys(STANCE_NAMES) as Stance[]).map((st) => <option key={st} value={st}>{STANCE_NAMES[st]}</option>)}
                </select>
              </div>
            )}
            <div className="field">
              <label>중계 속도</label>
              <div className="seg">
                {SPEEDS.map((s) => (
                  <button key={s.id} className={speed === s.id ? "on" : ""} onClick={() => setSpeed(s.id)}>{s.label}</button>
                ))}
              </div>
            </div>
            <button className={`btn ${rule === "sudden" ? "danger" : "primary"}`} disabled={!myId || !oppId} onClick={start} style={{ width: "100%", marginTop: 6 }}>
              {rule === "sudden" ? "사투 시작" : "경기 시작"}
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

export function Playback({ live, speedMs, onDone, onClose, nav, closeLabel = "한 판 더" }: { live: Live; speedMs: number; onDone: () => void; onClose: () => void; nav: Nav; closeLabel?: string }) {
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
  // 장면은 지금까지 보여 준 문장의 이벤트까지 진행한다
  const stageEvents = useMemo(() => {
    let lastIdx = -1;
    for (const it of visible) {
      if (it.kind !== "line") continue;
      const i = live.result.events.indexOf(it.line.event);
      if (i > lastIdx) lastIdx = i;
    }
    return live.result.events.slice(0, lastIdx + 1);
  }, [visible, live.result.events]);
  const hp = useMemo(() => {
    const h: Record<Id, { hp: number; max: number }> = {};
    if (live.startHp) {
      h[live.a.id] = { hp: live.startHp[0] * 100, max: 100 };
      h[live.b.id] = { hp: live.startHp[1] * 100, max: 100 };
    }
    for (const it of visible) {
      if (it.kind !== "line") continue;
      const e = it.line.event;
      if (e.t === "attack") h[e.target] = { hp: e.hpAfter, max: e.maxHp };
      if (e.t === "poison") h[e.target] = { hp: e.hpAfter, max: h[e.target]?.max ?? e.hpAfter };
    }
    return h;
  }, [visible, live.startHp, live.a.id, live.b.id]);
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
      <h1 className="screen-title">{live.title ?? BALANCE.rules[live.rule].name}</h1>
      <div className="screen-sub">{live.sub ?? "모래 마당 연습 시합"}</div>
      <div className="arena">
        <div className="arena-head">
          {fighterBox(live.a, false)}
          <div className="crowd-meter"><b>{crowdValue}</b>관중 열광</div>
          {fighterBox(live.b, true)}
        </div>
        <ArenaStage a={live.a} b={live.b} events={stageEvents} crowd={BALANCE.rules[live.rule].crowd} />
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
        {!finished && <button className="btn" onClick={() => setShown(live.items.length)}>결과 보기</button>}
        {finished && <button className="btn primary" onClick={onClose}>{closeLabel}</button>}
        {finished && <button className="btn" onClick={() => nav.go("roster")}>막사로</button>}
      </div>
    </>
  );
}
