import { useState } from "react";
import { dateLabel } from "../../core/calendar";
import type { GameState } from "../../core/types";

export function InboxScreen({ state, update }: { state: GameState; update: (fn: (d: GameState) => void) => void }) {
  const [sel, setSel] = useState(state.inbox[0]?.id);
  const msg = state.inbox.find((m) => m.id === sel);

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
              <div className="empty-state">메시지가 없습니다.</div>
            )}
          </div>
        </div>
      </div>
    </>
  );
}
