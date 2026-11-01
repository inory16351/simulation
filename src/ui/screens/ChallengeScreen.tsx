import { useState } from "react";
import { dateLabel, fromAbs } from "../../core/calendar";
import { availability, canChallenge, createChallenge, now, respond } from "../../core/challenge";
import { BALANCE, getStyle } from "../../core/data";
import { computeCA } from "../../core/gladiator";
import type { DeathRule, Gladiator } from "../../core/types";
import type { Ctx } from "../App";
import { Portrait, Stars } from "../components/common";

const RULES: DeathRule[] = ["training", "missio", "sudden"];
const RULE_HINT: Record<DeathRule, string> = {
  training: "관중 없음. 돈·명성 없이 성장만. RP 절반",
  missio: "상금 ×3, 관중 수입, 명성. 지면 관중이 생사를 정한다",
  sudden: "상금 ×6, 관중 수입 ×2.2, 명성 ×2. 지면 거의 죽는다",
};

export function ChallengeScreen({ state, act, nav }: Ctx) {
  const player = state.ludi[state.playerLudusId];
  const mine = player.gladiators.map((id) => state.gladiators[id]).filter((g) => g.alive);
  const incoming = state.challenges.filter((c) => c.status === "pending" && c.to === player.id);
  const outgoing = state.challenges.filter((c) => c.status === "pending" && c.from === player.id);
  const scheduled = state.challenges.filter((c) => c.status === "scheduled" && (c.from === player.id || c.to === player.id))
    .sort((a, b) => a.matchAt! - b.matchAt!);
  const L = BALANCE.league;

  const targets = Object.values(state.ludi).filter((l) => !l.isPlayer).sort((a, b) => b.rp - a.rp);
  const [targetId, setTargetId] = useState(targets.find((l) => !canChallenge(state, player, l))?.id ?? targets[0].id);
  const target = state.ludi[targetId];
  const [myId, setMyId] = useState<string | undefined>(mine.find((g) => !availability(state, g))?.id);
  const [theirId, setTheirId] = useState<string>("");
  const [rule, setRule] = useState<DeathRule>("training");
  const [error, setError] = useState<string | null>(null);
  const [defenders, setDefenders] = useState<Record<string, string>>({});
  const blockReason = canChallenge(state, player, target);

  // 상태 갱신 함수 안에서 화면 상태를 바꾸지 않도록, 검증은 먼저 한다
  const send = () => {
    const busy = myId ? availability(state, state.gladiators[myId]) : "출전자를 고르라";
    const err = blockReason ?? busy;
    setError(err);
    if (!err) act((d) => { createChallenge(d, d.ludi[player.id], d.ludi[targetId], myId!, rule, theirId || undefined); });
  };

  const fighterLabel = (g: Gladiator) => `${g.name} · ${getStyle(g.style).name} · CA ${computeCA(g)}`;

  return (
    <>
      <h1 className="screen-title">도전장</h1>
      <div className="screen-sub">
        이번 시즌 {player.matchesThisSeason}/{L.minMatches}경기 · 거절 {player.refusalsUsed}/{L.freeRefusals}회 무료 ·
        정규 기간은 서리달 15일부터 그림자달 14일까지
      </div>

      {incoming.length > 0 && (
        <div className="panel">
          <h3>받은 도전장</h3>
          <table className="t">
            <thead><tr><th>상대</th><th>상대 검투사</th><th>규칙</th><th>답할 기한</th><th>우리 출전자</th><th /></tr></thead>
            <tbody>
              {incoming.map((c) => {
                const from = state.ludi[c.from];
                const their = state.gladiators[c.fromGladiator];
                const ready = mine.filter((g) => !availability(state, g, now(state) + L.matchDelayMin));
                const pick = defenders[c.id] ?? ready[0]?.id;
                return (
                  <tr key={c.id}>
                    <td>{from.name} <span className="hint">RP {from.rp}</span></td>
                    <td><div className="name-cell"><Portrait g={their} size={26} /><span className="n">{their.name}</span> <Stars value={computeCA(their)} /></div></td>
                    <td><span className={`tag ${c.rule === "sudden" ? "neg" : ""}`}>{BALANCE.rules[c.rule].name}</span></td>
                    <td>{dateLabel(fromAbs(c.respondBy).day)}</td>
                    <td>
                      <select value={pick} onChange={(e) => setDefenders({ ...defenders, [c.id]: e.target.value })}>
                        {ready.map((g) => <option key={g.id} value={g.id}>{fighterLabel(g)}</option>)}
                      </select>
                    </td>
                    <td style={{ display: "flex", gap: 6 }}>
                      <button className="btn primary" disabled={!pick} onClick={() => act((d, rng) => { respond(d, d.challenges.find((x) => x.id === c.id)!, true, rng, pick); })}>받는다</button>
                      <button className="btn" onClick={() => act((d, rng) => { respond(d, d.challenges.find((x) => x.id === c.id)!, false, rng); })}>거절</button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      <div className="grid2">
        <div className="panel">
          <h3>도전장 보내기</h3>
          <div className="pad">
            <div className="field">
              <label>상대 루두스</label>
              <select value={targetId} onChange={(e) => { setTargetId(e.target.value); setTheirId(""); }}>
                {targets.map((l) => (
                  <option key={l.id} value={l.id} disabled={!!canChallenge(state, player, l)}>
                    {l.tier}부 · {l.name} (RP {l.rp}){canChallenge(state, player, l) ? " — 불가" : ""}
                  </option>
                ))}
              </select>
              {blockReason && <span className="hint">{blockReason}</span>}
            </div>
            <div className="field">
              <label>우리 출전자</label>
              <select value={myId ?? ""} onChange={(e) => setMyId(e.target.value)}>
                {mine.map((g) => {
                  const busy = availability(state, g);
                  return <option key={g.id} value={g.id} disabled={!!busy}>{fighterLabel(g)}{busy ? ` — ${busy}` : ""}</option>;
                })}
              </select>
            </div>
            <div className="field">
              <label>상대 지명 (선택)</label>
              <select value={theirId} onChange={(e) => setTheirId(e.target.value)}>
                <option value="">상대가 고르게 둔다</option>
                {target.gladiators.map((id) => state.gladiators[id]).filter((g) => g.alive).map((g) => (
                  <option key={g.id} value={g.id}>{g.name} · {getStyle(g.style).name}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>생사 규칙</label>
              <div className="seg">
                {RULES.map((r) => <button key={r} className={rule === r ? "on" : ""} onClick={() => setRule(r)}>{BALANCE.rules[r].name}</button>)}
              </div>
              <span className="hint">{RULE_HINT[rule]}</span>
            </div>
            {error && <div className="tag neg" style={{ marginBottom: 8 }}>{error}</div>}
            <button className={`btn ${rule === "sudden" ? "danger" : "primary"}`} disabled={!myId || !!blockReason} onClick={send}>도전장을 보낸다</button>
          </div>
        </div>

        <div>
          <div className="panel">
            <h3>잡힌 경기</h3>
            <table className="t">
              <tbody>
                {scheduled.length === 0 && <tr><td className="hint">아직 잡힌 경기가 없다.</td></tr>}
                {scheduled.map((c) => {
                  const iAmFrom = c.from === player.id;
                  const opp = state.ludi[iAmFrom ? c.to : c.from];
                  const me = state.gladiators[iAmFrom ? c.fromGladiator : c.toGladiator!];
                  const them = state.gladiators[iAmFrom ? c.toGladiator! : c.fromGladiator];
                  const today = c.matchAt === now(state);
                  return (
                    <tr key={c.id} className={today ? "me" : ""}>
                      <td>{today ? <b style={{ color: "var(--torch)" }}>오늘</b> : dateLabel(fromAbs(c.matchAt!).day)}</td>
                      <td><span className={`tag ${c.rule === "sudden" ? "neg" : ""}`}>{BALANCE.rules[c.rule].name}</span></td>
                      <td>{me?.name} vs {them?.name}</td>
                      <td className="hint">{opp.name}</td>
                      <td>{today && <button className="btn primary" onClick={() => nav.go("match")}>모래 마당으로</button>}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <div className="panel">
            <h3>답을 기다리는 도전장</h3>
            <table className="t">
              <tbody>
                {outgoing.length === 0 && <tr><td className="hint">없다.</td></tr>}
                {outgoing.map((c) => (
                  <tr key={c.id}>
                    <td>{state.ludi[c.to].name}</td>
                    <td><span className="tag">{BALANCE.rules[c.rule].name}</span></td>
                    <td>{state.gladiators[c.fromGladiator].name}</td>
                    <td className="hint">내일 답이 온다</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </>
  );
}
