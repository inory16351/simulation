import { useState } from "react";
import { dateLabel } from "../../core/calendar";
import { getEvent, resolveEvent } from "../../core/events";
import type { Ctx } from "../App";

export function InboxScreen({ state, update, act }: Ctx) {
  const [sel, setSel] = useState(state.inbox[0]?.id);
  const msg = state.inbox.find((m) => m.id === sel);
  const treasury = state.ludi[state.playerLudusId].treasury;

  const open = (id: string) => {
    setSel(id);
    update((d) => {
      const m = d.inbox.find((x) => x.id === id);
      if (m) m.read = true;
    });
  };

  return (
    <>
      <h1 className="screen-title">전령의 서판</h1>
      <div className="screen-sub">전령이 가져온 소식과 경기 결과가 여기 쌓인다.</div>

      {state.pendingEvents.map((p) => {
        const def = getEvent(p.eventId);
        return (
          <div key={p.id} className="panel event-card">
            <h3>결정이 필요하다 — {p.title}</h3>
            <div className="pad">
              <p className="event-text">{p.text}</p>
              <div className="event-choices">
                {def.choices.map((c, i) => (
                  <button key={i} className="btn" disabled={!!c.cost && treasury < c.cost}
                    onClick={() => act((d, rng) => { resolveEvent(d, rng, p.id, i); })}>
                    {c.label}
                  </button>
                ))}
              </div>
            </div>
          </div>
        );
      })}

      <div className="inbox">
        <div className="panel list">
          {state.inbox.map((m) => (
            <div
              key={m.id}
              className={`item ${m.id === sel ? "sel" : ""} ${m.read ? "" : "unread"} ${m.kind}`}
              onClick={() => open(m.id)}
            >
              <div className="title">{m.title}</div>
              <div className="meta">시즌 {m.season} · {dateLabel(m.day)}</div>
            </div>
          ))}
        </div>
        <div className="panel read">
          <div className="pad">
            {msg ? (
              <>
                <h2>{msg.title}</h2>
                <div className="meta" style={{ color: "var(--text-faint)", marginBottom: 14 }}>
                  시즌 {msg.season} · {dateLabel(msg.day)}
                </div>
                <div className="body">{msg.body}</div>
              </>
            ) : (
              <div className="empty-state">소식이 없다.</div>
            )}
          </div>
        </div>
      </div>
    </>
  );
}
